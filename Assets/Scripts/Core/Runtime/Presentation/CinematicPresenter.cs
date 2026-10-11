using System;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Core.Runtime.Presentation
{
    /// 由正式 View 传入制作引用的电影表现组件，三个表现通道互不借用节点。
    [DisallowMultipleComponent]
    public sealed class CinematicPresenter : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int PatternId = Shader.PropertyToID("_Pattern");
        private static readonly int DirectionId = Shader.PropertyToID("_Direction");
        private static readonly int ViewportId = Shader.PropertyToID("_ViewportRect");
        private static readonly int SegmentsId = Shader.PropertyToID("_Segments");
        private static readonly int PixelColumnsId = Shader.PropertyToID("_PixelColumns");

        private readonly List<LetterboxHandle> scopes = new();
        private readonly Dictionary<CanvasGroup, HudState> hudStates = new();
        private CanvasGroup transitionGroup;
        private Image curtain;
        private RectTransform letterboxContainer;
        private Image topBar;
        private Image bottomBar;
        private CanvasGroup titleGroup;
        private TMP_Text title;
        private TMP_Text subtitle;
        private Material mask;
        private Material originalMaterial;
        private Color originalCurtainColor;
        private Vector2 originalTopSize;
        private Vector2 originalBottomSize;
        private bool transitioning;

        /// 已显式接入全部制作引用。
        public bool IsInitialized { get; private set; }

        /// 当前黑边作用域数量。
        public int LetterboxScopeCount => scopes.Count;

        /// <summary>接入正式 Prefab 的三个独立表现通道，不创建 UI 模板。</summary>
        /// <param name="curtainGroup">专用全屏遮幕 CanvasGroup。</param>
        /// <param name="curtainImage">拉伸全屏、白色 Sprite 的遮幕 Image。</param>
        /// <param name="barContainer">黑边使用的实际画面容器。</param>
        /// <param name="upperBar">容器内顶部拉伸 Image。</param>
        /// <param name="lowerBar">容器内底部拉伸 Image。</param>
        /// <param name="cardGroup">专用字幕 CanvasGroup，不应覆盖整个输入遮罩。</param>
        /// <param name="mainTitle">正式公共字体的主标题 TMP。</param>
        /// <param name="secondaryTitle">正式公共字体的副标题 TMP。</param>
        /// <param name="maskTemplate">使用 UI/CinematicCurtain Shader 的制作 Material。</param>
        public void Initialize(CanvasGroup curtainGroup, Image curtainImage, RectTransform barContainer,
            Image upperBar, Image lowerBar, CanvasGroup cardGroup, TMP_Text mainTitle, TMP_Text secondaryTitle, Material maskTemplate)
        {
            CinematicRuntime.EnsureMainThread();
            if (curtainGroup == null || curtainImage == null || barContainer == null || upperBar == null
                || lowerBar == null || cardGroup == null || mainTitle == null || secondaryTitle == null || maskTemplate == null)
            {
                throw new ArgumentException("电影演出 Prefab 缺少制作引用。");
            }

            if (curtainGroup == cardGroup || upperBar == lowerBar || upperBar.rectTransform.parent != barContainer
                || lowerBar.rectTransform.parent != barContainer || !maskTemplate.HasProperty(ProgressId)
                || !maskTemplate.HasProperty(PatternId) || !maskTemplate.HasProperty(ViewportId))
            {
                throw new ArgumentException("电影演出通道必须独立，黑边必须直接归容器，遮幕材质必须具备正式属性。");
            }

            Release();
            transitionGroup = curtainGroup;
            curtain = curtainImage;
            letterboxContainer = barContainer;
            topBar = upperBar;
            bottomBar = lowerBar;
            titleGroup = cardGroup;
            title = mainTitle;
            subtitle = secondaryTitle;
            originalMaterial = curtain.material;
            originalCurtainColor = curtain.color;
            originalTopSize = topBar.rectTransform.sizeDelta;
            originalBottomSize = bottomBar.rectTransform.sizeDelta;
            topBar.raycastTarget = false;
            bottomBar.raycastTarget = false;
            mask = new Material(maskTemplate) { hideFlags = HideFlags.HideAndDontSave };
            mask.SetColor("_Color", Color.white);
            curtain.material = mask;
            IsInitialized = true;
            ResetEffects();
        }

        internal async UniTask PlayTransitionAsync(CinematicPreset preset, Func<CancellationToken, UniTask> midpoint,
            CancellationToken cancellationToken, CancellationToken registrationToken)
        {
            EnsureInitialized();
            float close = ResolveDuration(preset.CloseDuration);
            float open = ResolveDuration(preset.OpenDuration);
            transitioning = true;
            try
            {
                transitionGroup.alpha = 1;
                transitionGroup.interactable = false;
                transitionGroup.blocksRaycasts = true;
                curtain.raycastTarget = true;
                curtain.color = preset.CurtainColor;
                mask.SetFloat(PatternId, (int)preset.TransitionStyle);
                mask.SetFloat(DirectionId, (int)preset.Direction);
                mask.SetFloat(SegmentsId, preset.BlindCount);
                mask.SetFloat(PixelColumnsId, preset.PixelColumns);
                await AnimateAsync(close, progress => SetCoverage(preset.Evaluate(progress)), cancellationToken);
                SetCoverage(1);
                cancellationToken.ThrowIfCancellationRequested();
                if (midpoint != null)
                {
                    await midpoint(cancellationToken).AttachExternalCancellation(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                await WaitAsync(preset.ClosedHold, cancellationToken);
                await AnimateAsync(open, progress => SetCoverage(1 - preset.Evaluate(progress)), cancellationToken);
            }
            finally
            {
                // 注销已同步 ResetEffects；旧任务延后结束不能清理重新注册后的新演出。
                if (!registrationToken.IsCancellationRequested)
                {
                    transitioning = false;
                    HideCurtain();
                }
            }
        }

        internal LetterboxHandle PushLetterbox(CinematicPreset preset, IReadOnlyList<CanvasGroup> groups,
            CancellationToken userToken, CancellationToken registrationToken)
        {
            EnsureInitialized();
            if (preset == null)
            {
                throw new ArgumentNullException(nameof(preset));
            }

            preset.Validate();
            var scope = new LetterboxHandle(this, preset,
                topBar.enabled ? topBar.rectTransform.rect.height : 0, userToken, registrationToken);
            if (groups != null)
            {
                foreach (var group in groups)
                {
                    if (group == null || scope.Huds.ContainsKey(group))
                    {
                        continue;
                    }

                    if (group == transitionGroup || group == titleGroup || transform.IsChildOf(group.transform))
                    {
                        scope.Dispose();
                        throw new ArgumentException("HUD 作用域不能隐藏电影表现根或其父级。", nameof(groups));
                    }

                    if (!hudStates.TryGetValue(group, out var state))
                    {
                        state = new HudState(group);
                        hudStates.Add(group, state);
                    }

                    scope.Huds.Add(group, state.Coverage);
                    state.Owners.Add(scope);
                }
            }

            scopes.Add(scope);
            RefreshLetterbox();
            EnterLetterboxAsync(scope, preset, ResolveDuration(preset.LetterboxDuration)).Forget();
            return scope;
        }

        internal async UniTask ShowTitleAsync(string titleKey, string subtitleKey, float fadeIn, float hold,
            float fadeOut, CancellationToken cancellationToken, CancellationToken registrationToken)
        {
            EnsureInitialized();
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                LanguageService.SetText(title, titleKey);
                if (string.IsNullOrWhiteSpace(subtitleKey))
                {
                    subtitle.text = string.Empty;
                }
                else
                {
                    LanguageService.SetText(subtitle, subtitleKey);
                }

                titleGroup.interactable = false;
                titleGroup.blocksRaycasts = false;
                await AnimateAsync(ResolveDuration(fadeIn), value => titleGroup.alpha = value, cancellationToken);
                await WaitAsync(hold, cancellationToken);
                await AnimateAsync(ResolveDuration(fadeOut), value => titleGroup.alpha = 1 - value, cancellationToken);
            }
            finally
            {
                if (!registrationToken.IsCancellationRequested)
                {
                    HideTitle();
                }
            }
        }

        /// 清除三个表现通道，并恢复所有被本组件暂时隐藏的 HUD。
        public void ResetEffects()
        {
            transitioning = false;
            for (int index = scopes.Count - 1; index >= 0; index--)
            {
                scopes[index].Dispose();
            }

            HideCurtain();
            HideTitle();
            if (topBar != null)
            {
                topBar.enabled = false;
                topBar.rectTransform.sizeDelta = originalTopSize;
            }

            if (bottomBar != null)
            {
                bottomBar.enabled = false;
                bottomBar.rectTransform.sizeDelta = originalBottomSize;
            }
        }

        /// 释放材质副本及演出；制作根的销毁仍由 UIManager 管理。
        public void Release()
        {
            CinematicRuntime.Unregister(this);
            ResetEffects();
            if (curtain != null)
            {
                curtain.material = originalMaterial;
                curtain.color = originalCurtainColor;
            }

            CoreUtils.Destroy(mask);
            mask = null;
            IsInitialized = false;
        }

        private async UniTaskVoid EnterLetterboxAsync(LetterboxHandle scope, CinematicPreset preset, float duration)
        {
            try
            {
                await AnimateAsync(duration, value =>
                {
                    if (scope.IsExiting)
                    {
                        return;
                    }

                    scope.Progress = preset.Evaluate(value);
                    RefreshLetterbox();
                }, scope.EnterCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                // 正常退出只停止进入动画；生命周期取消才立即清理整个句柄。
                if (scope.Lifetime.IsCancellationRequested)
                {
                    scope.Dispose();
                }
            }
            catch (Exception exception)
            {
                scope.Dispose();
                Debug.LogException(exception, this);
            }
        }

        private async UniTask ExitLetterboxAsync(LetterboxHandle scope, float duration, CancellationToken cancellationToken)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, scope.Lifetime.Token);
            using var cancellation = linked.Token.Register(() =>
            {
                if (PlayerLoopHelper.IsMainThread)
                {
                    scope.Dispose();
                }
            });
            try
            {
                await AnimateAsync(ResolveDuration(duration), value =>
                {
                    scope.ExitProgress = scope.Preset.Evaluate(value);
                    RefreshLetterbox();
                }, linked.Token);
            }
            finally
            {
                scope.Dispose();
            }
        }

        private async UniTaskVoid CompleteExitAsync(LetterboxHandle scope, float duration, CancellationToken cancellationToken,
            UniTaskCompletionSource completion)
        {
            try
            {
                await ExitLetterboxAsync(scope, duration, cancellationToken);
                completion.TrySetResult();
            }
            catch (OperationCanceledException exception)
            {
                completion.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        private void LateUpdate()
        {
            for (int index = scopes.Count - 1; index >= 0; index--)
            {
                if (scopes[index].Lifetime.IsCancellationRequested)
                {
                    scopes[index].Dispose();
                }
            }

            if (scopes.Count > 0)
            {
                RefreshLetterbox();
            }

            if (transitioning && mask != null && curtain != null)
            {
                UpdateViewport();
            }
        }

        private void OnDisable() => CinematicRuntime.Unregister(this);

        private void OnDestroy() => Release();

        private void RefreshLetterbox()
        {
            if (topBar == null || bottomBar == null)
            {
                return;
            }

            float height = GetLetterboxHeight(scopes.Count - 1);

            topBar.enabled = height > 0;
            bottomBar.enabled = height > 0;
            topBar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            bottomBar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            foreach (var state in hudStates.Values)
            {
                state.Refresh();
            }
        }

        private float GetLetterboxHeight(int index)
        {
            if (index < 0)
            {
                return 0;
            }

            var active = scopes[index];
            return active.IsExiting
                ? Mathf.Lerp(active.ExitStartHeight, GetLetterboxHeight(index - 1), active.ExitProgress)
                : Mathf.Lerp(active.StartHeight, CalculateBarHeight(letterboxContainer.rect.size, active.Aspect), active.Progress);
        }

        /// <summary>由实际容器比例计算单侧黑边，超宽屏不会产生负尺寸。</summary>
        /// <param name="containerSize">实际 RectTransform 宽高。</param>
        /// <param name="targetAspect">目标内容宽高比。</param>
        /// <returns>单侧黑边高度。</returns>
        public static float CalculateBarHeight(Vector2 containerSize, float targetAspect)
        {
            if (targetAspect <= 0 || float.IsNaN(targetAspect) || float.IsInfinity(targetAspect)
                || containerSize.x < 0 || containerSize.y < 0 || float.IsNaN(containerSize.x) || float.IsNaN(containerSize.y)
                || float.IsInfinity(containerSize.x) || float.IsInfinity(containerSize.y))
            {
                throw new ArgumentException("电影黑边容器或比例无效。");
            }

            return Mathf.Max(0, (containerSize.y - containerSize.x / targetAspect) * 0.5f);
        }

        private void Remove(LetterboxHandle scope)
        {
            scopes.Remove(scope);
            foreach (var entry in scope.Huds)
            {
                var group = entry.Key;
                if (!hudStates.TryGetValue(group, out var state))
                {
                    continue;
                }

                state.Owners.Remove(scope);
                if (state.Owners.Count == 0)
                {
                    state.Restore();
                    hudStates.Remove(group);
                }
            }

            RefreshLetterbox();
        }

        private void SetCoverage(float progress)
        {
            UpdateViewport();
            mask.SetFloat(ProgressId, Mathf.Clamp01(progress));
        }

        private void UpdateViewport()
        {
            var rect = curtain.rectTransform.rect;
            mask.SetVector(ViewportId, new Vector4(rect.x, rect.y, rect.width, rect.height));
        }

        private void HideCurtain()
        {
            if (transitionGroup != null)
            {
                transitionGroup.alpha = 0;
                transitionGroup.interactable = false;
                transitionGroup.blocksRaycasts = false;
            }

            if (mask != null)
            {
                mask.SetFloat(ProgressId, 0);
            }

            if (curtain != null)
            {
                curtain.raycastTarget = false;
            }
        }

        private void HideTitle()
        {
            if (titleGroup != null)
            {
                titleGroup.alpha = 0;
                titleGroup.interactable = false;
                titleGroup.blocksRaycasts = false;
            }

            if (title != null)
            {
                title.text = string.Empty;
            }

            if (subtitle != null)
            {
                subtitle.text = string.Empty;
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || mask == null || transitionGroup == null)
            {
                throw new InvalidOperationException("电影表现组件尚未使用正式引用初始化。");
            }
        }

        private static float ResolveDuration(float duration)
        {
            return GameSettingsService.Current.Accessibility.IsReducedMotion ? 0 : duration;
        }

        private static async UniTask AnimateAsync(float duration, Action<float> apply, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            apply(0);
            float elapsed = 0;
            while (elapsed < duration)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                elapsed += Time.unscaledDeltaTime;
                apply(Mathf.Clamp01(elapsed / duration));
            }

            cancellationToken.ThrowIfCancellationRequested();
            apply(1);
        }

        private static UniTask WaitAsync(float seconds, CancellationToken cancellationToken)
        {
            return UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true, cancellationToken: cancellationToken);
        }

        /// 电影黑边句柄；正常退出渐变恢复下层，Dispose 用于异常或卸载时立即恢复。
        public sealed class LetterboxHandle : IDisposable
        {
            private readonly CinematicPresenter owner;
            private bool disposed;
            private readonly UniTaskCompletionSource exitCompletion = new();
            internal readonly float Aspect;
            internal readonly float StartHeight;
            internal readonly CinematicPreset Preset;
            internal readonly CancellationTokenSource Lifetime;
            internal readonly CancellationTokenSource EnterCancellation;
            internal readonly Dictionary<CanvasGroup, float> Huds = new();
            internal float Progress;
            internal float ExitProgress;
            internal float ExitStartHeight;
            internal bool IsExiting;

            internal LetterboxHandle(CinematicPresenter presenter, CinematicPreset preset, float startHeight, CancellationToken userToken,
                CancellationToken registrationToken)
            {
                owner = presenter;
                Preset = preset;
                Aspect = preset.TargetAspect;
                StartHeight = startHeight;
                Lifetime = CancellationTokenSource.CreateLinkedTokenSource(userToken, registrationToken);
                EnterCancellation = CancellationTokenSource.CreateLinkedTokenSource(Lifetime.Token);
            }

            /// <summary>停止本次进入动画，渐变释放自身黑边与 HUD 隐藏贡献；取消保证立即清理。</summary>
            /// <param name="duration">零到三十秒的非缩放退出时长；null 沿用进入预设时长，减少动态时为零。</param>
            /// <param name="cancellationToken">取消退出并立即恢复；重复调用等待首次退出，沿用首次参数和取消令牌。</param>
            /// <returns>下层黑边及有效 HUD 状态恢复后的任务；已 Dispose 时立即完成。</returns>
            public UniTask ExitAsync(float? duration = null, CancellationToken cancellationToken = default)
            {
                CinematicRuntime.EnsureMainThread();
                if (disposed)
                {
                    return UniTask.CompletedTask;
                }

                if (IsExiting)
                {
                    return exitCompletion.Task;
                }

                float seconds = duration ?? Preset.LetterboxDuration;
                if (!float.IsFinite(seconds) || seconds < 0 || seconds > 30)
                {
                    throw new ArgumentOutOfRangeException(nameof(duration));
                }

                ExitStartHeight = owner.GetLetterboxHeight(owner.scopes.IndexOf(this));
                IsExiting = true;
                EnterCancellation.Cancel();
                owner.CompleteExitAsync(this, seconds, cancellationToken, exitCompletion).Forget();
                return exitCompletion.Task;
            }

            /// 立即停止进入和退出，移除自身覆盖并恢复下层；重复释放安全。
            public void Dispose()
            {
                CinematicRuntime.EnsureMainThread();
                if (disposed)
                {
                    return;
                }

                disposed = true;
                Lifetime.Cancel();
                owner.Remove(this);
                EnterCancellation.Dispose();
                Lifetime.Dispose();
            }
        }

        private sealed class HudState
        {
            private readonly CanvasGroup group;
            private readonly float alpha;
            private readonly bool interactable;
            private readonly bool blocksRaycasts;
            internal readonly List<LetterboxHandle> Owners = new();

            internal float Coverage
            {
                get
                {
                    float coverage = 0;
                    foreach (var scope in Owners)
                    {
                        float ownCoverage = Mathf.Lerp(scope.Huds[group], 1, scope.Progress);
                        coverage = Mathf.Max(coverage, ownCoverage * (1 - scope.ExitProgress));
                    }

                    return coverage;
                }
            }

            internal HudState(CanvasGroup target)
            {
                group = target;
                alpha = target.alpha;
                interactable = target.interactable;
                blocksRaycasts = target.blocksRaycasts;
            }

            internal void Refresh()
            {
                if (group == null)
                {
                    return;
                }

                group.alpha = alpha * (1 - Coverage);
                group.interactable = false;
                group.blocksRaycasts = false;
            }

            internal void Restore()
            {
                if (group != null)
                {
                    group.alpha = alpha;
                    group.interactable = interactable;
                    group.blocksRaycasts = blocksRaycasts;
                }
            }
        }
    }
}
