using System;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Core.Runtime.Presentation
{
    /// 正式 Prefab 的三个独立表现通道，每个通道只维护当前操作。
    [DisallowMultipleComponent]
    public sealed class CinematicPresenter : MonoBehaviour
    {
        private static readonly int ProgressId = Shader.PropertyToID("_Progress");
        private static readonly int PatternId = Shader.PropertyToID("_Pattern");
        private static readonly int DirectionId = Shader.PropertyToID("_Direction");
        private static readonly int ViewportId = Shader.PropertyToID("_ViewportRect");
        private static readonly int SegmentsId = Shader.PropertyToID("_Segments");
        private static readonly int PixelColumnsId = Shader.PropertyToID("_PixelColumns");

        [SerializeField] private RectTransform frame;
        [SerializeField] private CanvasGroup transitionGroup;
        [SerializeField] private Image curtain;
        [SerializeField] private Image doorFirst;
        [SerializeField] private Image doorSecond;
        [SerializeField] private Image topBar;
        [SerializeField] private Image bottomBar;
        [SerializeField] private CanvasGroup titleGroup;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private Material maskTemplate;

        private readonly Vector3[] corners = new Vector3[4];
        private RectTransform viewport;
        private Material mask;
        private Material originalMaterial;
        private Color originalColor;
        private Sequence curtainTween;
        private Sequence titleTween;
        private Tween letterboxTween;
        private CinematicTransitionStyle style;
        private float coverage;
        private float barRatio;
        private float barAspect;
        private float barProgress;
        private float barStartRatio;
        private int curtainVersion;
        private int titleVersion;

        public bool IsInitialized { get; private set; }
        public bool IsTransitioning => curtainTween != null;
        public bool IsTitlePlaying => titleTween != null;
        public bool IsLetterboxVisible => topBar != null && topBar.enabled;

        /// 校验制作引用，创建特殊图案材质副本并恢复空白状态。
        public void Initialize()
        {
            if (IsInitialized)
            {
                return;
            }
            if (frame == null || transitionGroup == null || curtain == null || doorFirst == null || doorSecond == null
                || topBar == null || bottomBar == null || titleGroup == null || title == null || subtitle == null
                || maskTemplate == null || !maskTemplate.HasProperty(ProgressId) || !maskTemplate.HasProperty(PatternId)
                || !maskTemplate.HasProperty(ViewportId))
            {
                throw new InvalidOperationException("电影演出 Prefab 缺少制作引用或材质属性。");
            }
            if (transitionGroup == titleGroup || doorFirst == doorSecond || topBar == bottomBar
                || topBar.rectTransform.parent != frame || bottomBar.rectTransform.parent != frame
                || !doorFirst.transform.IsChildOf(transitionGroup.transform)
                || !doorSecond.transform.IsChildOf(transitionGroup.transform))
            {
                throw new InvalidOperationException("黑幕、黑边和标题必须使用独立节点。");
            }
            originalMaterial = curtain.material;
            originalColor = curtain.color;
            mask = new Material(maskTemplate) { hideFlags = HideFlags.HideAndDontSave };
            mask.SetColor("_Color", Color.white);
            doorFirst.raycastTarget = doorSecond.raycastTarget = false;
            topBar.raycastTarget = bottomBar.raycastTarget = false;
            title.raycastTarget = subtitle.raycastTarget = false;
            IsInitialized = true;
            ResetEffects();
            RefreshViewport();
        }

        /// <summary>选择目标画面，null 使用全屏；切换容器会停止现有演出。</summary>
        /// <param name="value">目标 RectTransform，不修改其层级及布局。</param>
        public void SetViewport(RectTransform value)
        {
            if (viewport != value)
            {
                ResetEffects();
                viewport = value;
            }
            RefreshViewport();
        }

        /// 跟随目标容器，并按当前遮挡状态重算尺寸。
        public void RefreshViewport()
        {
            if (frame == null)
            {
                return;
            }
            var root = (RectTransform)transform;
            if (viewport == null)
            {
                frame.anchorMin = Vector2.zero;
                frame.anchorMax = Vector2.one;
                frame.offsetMin = frame.offsetMax = Vector2.zero;
            }
            else
            {
                viewport.GetWorldCorners(corners);
                var bottom = root.InverseTransformPoint(corners[0]);
                var top = root.InverseTransformPoint(corners[2]);
                frame.anchorMin = frame.anchorMax = root.pivot;
                frame.pivot = Vector2.one * 0.5f;
                frame.anchoredPosition = (bottom + top) * 0.5f;
                frame.sizeDelta = new Vector2(top.x - bottom.x, top.y - bottom.y);
            }
            if (IsInitialized)
            {
                ApplyCoverage(coverage);
                ApplyLetterbox();
            }
        }

        /// <summary>闭幕、中点、固定停留、开幕；失败清幕，不排队。</summary>
        /// <param name="onCovered">全遮挡后的同步业务；异步业务使用单独闭幕和开幕。</param>
        /// <param name="closedHold">中点后的未缩放停留秒数，默认零。</param>
        /// <param name="onComplete">正常开幕后调用，停止或失败不调用。</param>
        /// <param name="style">图案，默认淡黑。</param>
        /// <param name="closeDuration">未缩放闭幕秒数，默认 0.35。</param>
        /// <param name="openDuration">未缩放开幕秒数，默认 0.35。</param>
        /// <param name="ease">缓动，默认 InOutSine。</param>
        /// <param name="direction">擦幕和百叶方向，默认从左到右。</param>
        /// <param name="color">不透明颜色，默认黑色。</param>
        /// <param name="blindCount">百叶段数，默认 12，范围 2–64。</param>
        /// <param name="pixelColumns">像素横向格数，默认 48，范围 8–128。</param>
        /// <param name="onFailed">中点异常处理，省略时记录 Console；忙碌和参数错误直接抛出。</param>
        public void StartTransition(Action onCovered = null, float closedHold = 0, Action onComplete = null,
            CinematicTransitionStyle style = CinematicTransitionStyle.Fade, float closeDuration = 0.35f,
            float openDuration = 0.35f, Ease ease = Ease.InOutSine,
            CinematicWipeDirection direction = CinematicWipeDirection.LeftToRight, Color? color = null,
            int blindCount = 12, int pixelColumns = 48, Action<Exception> onFailed = null)
        {
            EnsureCurtainAvailable();
            ValidateTime(closeDuration, nameof(closeDuration));
            ValidateTime(openDuration, nameof(openDuration));
            ValidateTime(closedHold, nameof(closedHold), 60);
            ConfigureCurtain(style, direction, color ?? Color.black, blindCount, pixelColumns);
            int version = ++curtainVersion;
            var sequence = CreateSequence();
            curtainTween = sequence;
            sequence.Append(CoverageTween(1, closeDuration, ease));
            sequence.AppendCallback(() =>
            {
                if (version != curtainVersion)
                {
                    return;
                }
                try
                {
                    onCovered?.Invoke();
                }
                catch (Exception exception)
                {
                    if (version == curtainVersion)
                    {
                        StopCurtain();
                    }
                    if (onFailed != null)
                    {
                        onFailed(exception);
                    }
                    else
                    {
                        Debug.LogException(exception, this);
                    }
                }
            });
            sequence.AppendInterval(closedHold);
            // 起点固定为全遮挡，避免序列预初始化读取闭幕前的进度。
            sequence.Append(DOTween.To(() => 1f, ApplyCoverage, 0, ResolveDuration(openDuration)).SetEase(ease));
            CompleteCurtain(sequence, version, true, onComplete);
        }

        /// <summary>单独闭幕，完成后保持遮挡，业务就绪后调用 OpenCurtain。</summary>
        /// <param name="style">图案，默认淡黑。</param>
        /// <param name="duration">未缩放秒数，默认 0.35。</param>
        /// <param name="onComplete">全遮挡后调用，停止不调用。</param>
        /// <param name="ease">缓动，默认 InOutSine。</param>
        /// <param name="direction">擦幕和百叶方向，默认从左到右。</param>
        /// <param name="color">不透明颜色，默认黑色。</param>
        /// <param name="blindCount">百叶段数，默认 12，范围 2–64。</param>
        /// <param name="pixelColumns">像素横向格数，默认 48，范围 8–128。</param>
        public void CloseCurtain(CinematicTransitionStyle style = CinematicTransitionStyle.Fade, float duration = 0.35f,
            Action onComplete = null, Ease ease = Ease.InOutSine,
            CinematicWipeDirection direction = CinematicWipeDirection.LeftToRight, Color? color = null,
            int blindCount = 12, int pixelColumns = 48)
        {
            EnsureCurtainAvailable();
            ValidateTime(duration, nameof(duration));
            ConfigureCurtain(style, direction, color ?? Color.black, blindCount, pixelColumns);
            int version = ++curtainVersion;
            var sequence = CreateSequence();
            curtainTween = sequence;
            sequence.Append(CoverageTween(1, duration, ease));
            CompleteCurtain(sequence, version, false, onComplete);
        }

        /// <summary>沿用当前图案开幕，完成后解除射线拦截。</summary>
        /// <param name="duration">未缩放秒数，默认 0.35。</param>
        /// <param name="onComplete">正常开幕后调用，停止不调用。</param>
        /// <param name="ease">缓动，默认 InOutSine。</param>
        public void OpenCurtain(float duration = 0.35f, Action onComplete = null, Ease ease = Ease.InOutSine)
        {
            EnsureCurtainAvailable();
            ValidateTime(duration, nameof(duration));
            int version = ++curtainVersion;
            var sequence = CreateSequence();
            curtainTween = sequence;
            sequence.Append(CoverageTween(0, duration, ease));
            CompleteCurtain(sequence, version, true, onComplete);
        }

        /// <summary>显示或调整唯一黑边状态，从当前厚度继续。</summary>
        /// <param name="heightRatio">每侧占容器高度比例，默认 0.12，范围 0–0.5。</param>
        /// <param name="duration">未缩放秒数，默认 0.3。</param>
        /// <param name="onComplete">正常到达目标时调用，替换或停止不调用。</param>
        /// <param name="ease">缓动，默认 InOutSine。</param>
        public void ShowLetterbox(float heightRatio = 0.12f, float duration = 0.3f,
            Action onComplete = null, Ease ease = Ease.InOutSine)
        {
            EnsureInitialized();
            if (!float.IsFinite(heightRatio) || heightRatio < 0 || heightRatio > 0.5f)
            {
                throw new ArgumentOutOfRangeException(nameof(heightRatio));
            }
            AnimateLetterbox(heightRatio, 0, duration, onComplete, ease);
        }

        /// <summary>按内容宽高比显示唯一黑边状态，超宽容器允许零黑边。</summary>
        /// <param name="targetAspect">有限且大于零的宽高比，默认 2.39。</param>
        /// <param name="duration">未缩放秒数，默认 0.3。</param>
        /// <param name="onComplete">正常到达目标时调用，替换或停止不调用。</param>
        /// <param name="ease">缓动，默认 InOutSine。</param>
        public void ShowLetterboxForAspect(float targetAspect = 2.39f, float duration = 0.3f,
            Action onComplete = null, Ease ease = Ease.InOutSine)
        {
            EnsureInitialized();
            CalculateBarHeight(frame.rect.size, targetAspect);
            AnimateLetterbox(0, targetAspect, duration, onComplete, ease);
        }

        /// <summary>从当前厚度收回黑边，不恢复历史状态。</summary>
        /// <param name="duration">未缩放秒数，默认 0.3。</param>
        /// <param name="onComplete">正常收回后调用，停止不调用。</param>
        /// <param name="ease">缓动，默认 InOutSine。</param>
        public void HideLetterbox(float duration = 0.3f, Action onComplete = null, Ease ease = Ease.InOutSine)
        {
            ShowLetterbox(0, duration, onComplete, ease);
        }

        /// <summary>按语言键播放一次标题，播放中重复调用报告忙碌。</summary>
        /// <param name="titleKey">主标题语言键，显示时解析字体。</param>
        /// <param name="subtitleKey">副标题语言键，默认不显示。</param>
        /// <param name="fadeIn">未缩放淡入秒数，默认 0.3。</param>
        /// <param name="hold">未缩放阅读秒数，默认 2。</param>
        /// <param name="fadeOut">未缩放淡出秒数，默认 0.3。</param>
        /// <param name="onComplete">正常清空后调用，停止或失败不调用。</param>
        public void ShowTitle(string titleKey, string subtitleKey = null, float fadeIn = 0.3f, float hold = 2,
            float fadeOut = 0.3f, Action onComplete = null)
        {
            EnsureInitialized();
            if (IsTitlePlaying)
            {
                throw new InvalidOperationException("标题正在播放，请先停止当前演出。");
            }
            ValidateTime(fadeIn, nameof(fadeIn));
            ValidateTime(hold, nameof(hold), 60);
            ValidateTime(fadeOut, nameof(fadeOut));
            try
            {
                LanguageService.SetText(title, titleKey);
                if (string.IsNullOrWhiteSpace(subtitleKey))
                {
                    subtitle.text = string.Empty;
                }
                else
                {
                    LanguageService.SetText(subtitle, subtitleKey);
                }
            }
            catch
            {
                HideTitle();
                throw;
            }
            titleGroup.alpha = 0;
            titleGroup.interactable = titleGroup.blocksRaycasts = false;
            int version = ++titleVersion;
            var sequence = CreateSequence();
            titleTween = sequence;
            sequence.Append(titleGroup.DOFade(1, ResolveDuration(fadeIn)).SetEase(Ease.InOutSine));
            sequence.AppendInterval(hold);
            sequence.Append(titleGroup.DOFade(0, ResolveDuration(fadeOut)).SetEase(Ease.InOutSine));
            sequence.OnComplete(() =>
            {
                if (version != titleVersion || titleTween != sequence)
                {
                    return;
                }
                titleTween = null;
                HideTitle();
                onComplete?.Invoke();
            });
            sequence.OnKill(() =>
            {
                if (titleTween != sequence)
                {
                    return;
                }
                titleTween = null;
                HideTitle();
            });
            if (sequence.Duration() == 0)
            {
                sequence.Complete(true);
            }
        }

        /// 停止三个通道并清空表现，不触发完成回调。
        public void ResetEffects()
        {
            StopCurtain();
            ++titleVersion;
            var previousTitle = titleTween;
            titleTween = null;
            previousTitle?.Kill();
            letterboxTween?.Kill();
            letterboxTween = null;
            barRatio = barAspect = barStartRatio = 0;
            barProgress = 1;
            HideTitle();
            if (frame != null && topBar != null && bottomBar != null)
            {
                ApplyLetterbox();
            }
        }

        /// 停止演出并释放材质副本，不销毁 UIManager 拥有的节点。
        public void Release()
        {
            ResetEffects();
            viewport = null;
            if (IsInitialized && curtain != null)
            {
                curtain.material = originalMaterial;
                curtain.color = originalColor;
            }
            CoreUtils.Destroy(mask);
            mask = null;
            IsInitialized = false;
        }

        /// <summary>按实际容器计算单侧黑边高度，不产生负厚度。</summary>
        /// <param name="containerSize">有限且非负的宽高。</param>
        /// <param name="targetAspect">有限且大于零的宽高比。</param>
        /// <returns>单侧黑边在容器中的高度。</returns>
        public static float CalculateBarHeight(Vector2 containerSize, float targetAspect)
        {
            if (!float.IsFinite(targetAspect) || targetAspect <= 0 || !float.IsFinite(containerSize.x)
                || !float.IsFinite(containerSize.y) || containerSize.x < 0 || containerSize.y < 0)
            {
                throw new ArgumentException("电影黑边容器或宽高比无效。");
            }
            return Mathf.Max(0, (containerSize.y - containerSize.x / targetAspect) * 0.5f);
        }

        private void ConfigureCurtain(CinematicTransitionStyle value, CinematicWipeDirection direction, Color color, int blinds, int pixels)
        {
            if (!Enum.IsDefined(typeof(CinematicTransitionStyle), value) || !Enum.IsDefined(typeof(CinematicWipeDirection), direction)
                || !float.IsFinite(color.r) || !float.IsFinite(color.g) || !float.IsFinite(color.b) || !float.IsFinite(color.a)
                || color.r < 0 || color.r > 1 || color.g < 0 || color.g > 1 || color.b < 0 || color.b > 1
                || !Mathf.Approximately(color.a, 1) || blinds < 2 || blinds > 64 || pixels < 8 || pixels > 128)
            {
                throw new ArgumentException("黑幕图案、颜色或分段参数无效。");
            }
            style = value;
            transitionGroup.interactable = false;
            transitionGroup.blocksRaycasts = true;
            curtain.enabled = true;
            curtain.raycastTarget = true;
            curtain.type = value == CinematicTransitionStyle.Wipe ? Image.Type.Filled : Image.Type.Simple;
            curtain.fillMethod = direction <= CinematicWipeDirection.RightToLeft ? Image.FillMethod.Horizontal : Image.FillMethod.Vertical;
            curtain.fillOrigin = direction == CinematicWipeDirection.RightToLeft || direction == CinematicWipeDirection.TopToBottom ? 1 : 0;
            bool doors = value == CinematicTransitionStyle.DoorsHorizontal || value == CinematicTransitionStyle.DoorsVertical;
            doorFirst.enabled = doorSecond.enabled = doors;
            doorFirst.color = doorSecond.color = color;
            curtain.color = doors ? new Color(color.r, color.g, color.b, 0) : color;
            curtain.material = value >= CinematicTransitionStyle.Iris ? mask : null;
            mask.SetFloat(PatternId, (int)value);
            mask.SetFloat(DirectionId, (int)direction);
            mask.SetFloat(SegmentsId, blinds);
            mask.SetFloat(PixelColumnsId, pixels);
            ApplyCoverage(coverage);
        }

        private Tween CoverageTween(float target, float duration, Ease ease) => DOTween.To(() => coverage, ApplyCoverage, target, ResolveDuration(duration)).SetEase(ease);
        private Sequence CreateSequence() => DOTween.Sequence().SetUpdate(true).SetTarget(this).SetLink(gameObject, LinkBehaviour.KillOnDestroy);

        private void CompleteCurtain(Sequence sequence, int version, bool clear, Action onComplete)
        {
            sequence.OnComplete(() =>
            {
                if (version != curtainVersion || curtainTween != sequence)
                {
                    return;
                }
                curtainTween = null;
                if (clear)
                {
                    HideCurtain();
                }
                onComplete?.Invoke();
            });
            sequence.OnKill(() =>
            {
                if (curtainTween != sequence)
                {
                    return;
                }
                curtainTween = null;
                HideCurtain();
            });
            if (sequence.Duration() == 0)
            {
                sequence.Complete(true);
            }
        }

        private void ApplyCoverage(float value)
        {
            coverage = Mathf.Clamp01(value);
            transitionGroup.alpha = style == CinematicTransitionStyle.Fade ? coverage : coverage > 0 ? 1 : 0;
            if (style == CinematicTransitionStyle.Wipe)
            {
                curtain.fillAmount = coverage;
            }
            else if (style == CinematicTransitionStyle.DoorsHorizontal || style == CinematicTransitionStyle.DoorsVertical)
            {
                bool horizontal = style == CinematicTransitionStyle.DoorsHorizontal;
                var first = doorFirst.rectTransform;
                var second = doorSecond.rectTransform;
                first.anchorMin = Vector2.zero;
                first.anchorMax = horizontal ? new Vector2(0, 1) : new Vector2(1, 0);
                first.pivot = horizontal ? new Vector2(0, 0.5f) : new Vector2(0.5f, 0);
                second.anchorMin = horizontal ? new Vector2(1, 0) : new Vector2(0, 1);
                second.anchorMax = Vector2.one;
                second.pivot = horizontal ? new Vector2(1, 0.5f) : new Vector2(0.5f, 1);
                first.anchoredPosition = second.anchoredPosition = Vector2.zero;
                first.sizeDelta = second.sizeDelta = horizontal
                    ? new Vector2(frame.rect.width * 0.5f * coverage, 0)
                    : new Vector2(0, frame.rect.height * 0.5f * coverage);
            }
            else if (style >= CinematicTransitionStyle.Iris)
            {
                var rect = curtain.rectTransform.rect;
                mask.SetVector(ViewportId, new Vector4(rect.x, rect.y, rect.width, rect.height));
                mask.SetFloat(ProgressId, coverage);
            }
        }

        private void AnimateLetterbox(float ratio, float aspect, float duration, Action onComplete, Ease ease)
        {
            ValidateTime(duration, nameof(duration));
            letterboxTween?.Kill();
            letterboxTween = null;
            barStartRatio = frame.rect.height > 0 ? topBar.rectTransform.rect.height / frame.rect.height : 0;
            barRatio = ratio;
            barAspect = aspect;
            barProgress = 0;
            float seconds = ResolveDuration(duration);
            if (seconds == 0)
            {
                barProgress = 1;
                ApplyLetterbox();
                onComplete?.Invoke();
                return;
            }
            var tween = DOTween.To(() => barProgress, value =>
            {
                barProgress = value;
                ApplyLetterbox();
            }, 1, seconds).SetEase(ease).SetUpdate(true).SetTarget(this).SetLink(gameObject, LinkBehaviour.KillOnDestroy);
            letterboxTween = tween;
            tween.OnComplete(() =>
            {
                if (letterboxTween != tween)
                {
                    return;
                }
                letterboxTween = null;
                onComplete?.Invoke();
            });
            tween.OnKill(() =>
            {
                if (letterboxTween == tween)
                {
                    letterboxTween = null;
                }
            });
        }

        private void ApplyLetterbox()
        {
            float height = frame.rect.height;
            float target = barAspect > 0 ? CalculateBarHeight(frame.rect.size, barAspect) : height * barRatio;
            float current = Mathf.Lerp(height * barStartRatio, target, barProgress);
            topBar.enabled = bottomBar.enabled = current > 0;
            topBar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, current);
            bottomBar.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, current);
        }

        private void StopCurtain()
        {
            ++curtainVersion;
            var previous = curtainTween;
            curtainTween = null;
            previous?.Kill();
            HideCurtain();
        }

        private void HideCurtain()
        {
            coverage = 0;
            if (transitionGroup != null)
            {
                transitionGroup.alpha = 0;
                transitionGroup.interactable = transitionGroup.blocksRaycasts = false;
            }
            if (curtain != null)
            {
                curtain.raycastTarget = false;
            }
            if (doorFirst != null && doorSecond != null)
            {
                doorFirst.enabled = doorSecond.enabled = false;
            }
        }

        private void HideTitle()
        {
            if (titleGroup != null)
            {
                titleGroup.alpha = 0;
                titleGroup.interactable = titleGroup.blocksRaycasts = false;
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
            if (!IsInitialized || !isActiveAndEnabled)
            {
                throw new InvalidOperationException("电影演出组件尚未初始化或当前未启用。");
            }
        }

        private void EnsureCurtainAvailable()
        {
            EnsureInitialized();
            if (IsTransitioning)
            {
                throw new InvalidOperationException("黑幕正在播放，请先停止当前演出。");
            }
        }

        private static void ValidateTime(float value, string parameter, float maximum = 30)
        {
            if (!float.IsFinite(value) || value < 0 || value > maximum)
            {
                throw new ArgumentOutOfRangeException(parameter);
            }
        }

        private static float ResolveDuration(float duration) => GameSettingsService.Current.Accessibility.IsReducedMotion ? 0 : duration;
        private void LateUpdate() => RefreshViewport();
        private void OnDisable() => ResetEffects();
        private void OnDestroy() => Release();
    }
}
