using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class FadeScaleUITransition : IUITransition
    {
        private const float DefaultDuration = 0.2f;
        private const float DefaultHiddenScale = 0.95f;
        private readonly float duration;
        private readonly float hiddenScale;
        private Transform root;
        private CanvasGroup canvasGroup;
        private Sequence activeTween;
        private UniTaskCompletionSource activeCompletion;
        private CancellationToken activeCancellationToken;

        public FadeScaleUITransition() : this(DefaultDuration, DefaultHiddenScale)
        {
        }

        internal FadeScaleUITransition(float duration, float hiddenScale)
        {
            this.duration = Mathf.Max(0f, duration);
            this.hiddenScale = hiddenScale;
        }

        /// <summary>
        /// 缓存 View 根节点，并确保存在用于淡入淡出的 CanvasGroup。
        /// </summary>
        /// <param name="root">当前 View 的根节点。</param>
        public void Initialize(Transform root)
        {
            EnsureMainThread();
            if (this.root == root && canvasGroup != null)
            {
                return;
            }

            CancelActiveTween();
            this.root = root != null
                ? root
                : throw new ArgumentNullException(nameof(root));
            canvasGroup = root.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = root.gameObject.AddComponent<CanvasGroup>();
            }
        }

        /// <summary>
        /// 从透明缩小态并行过渡到完全可见态。
        /// </summary>
        /// <param name="context">本次导航操作的过渡上下文。</param>
        /// <param name="cancellationToken">取消本次过渡的令牌。</param>
        /// <returns>进入过渡任务。</returns>
        public UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            return PlayAsync(UITransitionDirection.Enter, cancellationToken);
        }

        /// <summary>
        /// 从当前状态并行过渡到透明缩小态。
        /// </summary>
        /// <param name="context">本次导航操作的过渡上下文。</param>
        /// <param name="cancellationToken">取消本次过渡的令牌。</param>
        /// <returns>退出过渡任务。</returns>
        public UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            return PlayAsync(UITransitionDirection.Exit, cancellationToken);
        }

        /// <summary>
        /// 停止当前补间，并同步到指定方向的确定终态。
        /// </summary>
        /// <param name="direction">需要立即完成的过渡方向。</param>
        public void CompleteImmediately(UITransitionDirection direction)
        {
            EnsureMainThread();
            CancelActiveTween();
            ApplyFinalState(direction);
        }

        /// 停止补间并释放当前根节点引用；重复调用安全。
        public void Dispose()
        {
            EnsureMainThread();
            CancelActiveTween();
            root = null;
            canvasGroup = null;
        }

        private async UniTask PlayAsync(
            UITransitionDirection direction,
            CancellationToken cancellationToken)
        {
            await UniTask.SwitchToMainThread();
            cancellationToken.ThrowIfCancellationRequested();
            EnsureInitialized();
            CancelActiveTween();

            if (direction == UITransitionDirection.Enter)
            {
                canvasGroup.alpha = 0f;
                root.localScale = Vector3.one * hiddenScale;
            }

            var completion = new UniTaskCompletionSource();
            var sequence = DOTween.Sequence();
            activeTween = sequence;
            activeCompletion = completion;
            activeCancellationToken = cancellationToken;

            sequence.Join(canvasGroup.DOFade(
                    direction == UITransitionDirection.Enter ? 1f : 0f,
                    duration)
                .SetEase(direction == UITransitionDirection.Enter ? Ease.OutCubic : Ease.InCubic));
            sequence.Join(root.DOScale(
                    direction == UITransitionDirection.Enter ? 1f : hiddenScale,
                    duration)
                .SetEase(direction == UITransitionDirection.Enter ? Ease.OutCubic : Ease.InCubic));
            sequence.SetLink(root.gameObject, LinkBehaviour.KillOnDestroy);
            sequence.OnComplete(() => completion.TrySetResult());
            sequence.OnKill(() => completion.TrySetCanceled(cancellationToken));

            var registration = cancellationToken.Register(() =>
            {
                // 始终延后到 PlayerLoop：让 CancellationToken 回调先返回，避免 await
                // 同步续体在回调栈内 Dispose 当前 registration 形成自等待。
                UniTask.Post(() => CancelTween(sequence, completion, cancellationToken));
            });

            try
            {
                await completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
                ApplyFinalState(direction);
            }
            finally
            {
                registration.Dispose();
                if (ReferenceEquals(activeTween, sequence))
                {
                    activeTween = null;
                    activeCompletion = null;
                    activeCancellationToken = default;
                }
            }
        }

        private void CancelActiveTween()
        {
            var tween = activeTween;
            var completion = activeCompletion;
            var cancellationToken = activeCancellationToken;
            activeTween = null;
            activeCompletion = null;
            activeCancellationToken = default;
            CancelTween(tween, completion, cancellationToken);
        }

        private static void CancelTween(
            Sequence tween,
            UniTaskCompletionSource completion,
            CancellationToken cancellationToken)
        {
            if (tween != null && tween.IsActive())
            {
                tween.Kill(false);
            }

            completion?.TrySetCanceled(cancellationToken);
        }

        private void ApplyFinalState(UITransitionDirection direction)
        {
            if (root == null || canvasGroup == null)
            {
                return;
            }

            var entered = direction == UITransitionDirection.Enter;
            canvasGroup.alpha = entered ? 1f : 0f;
            root.localScale = Vector3.one * (entered ? 1f : hiddenScale);
        }

        private void EnsureInitialized()
        {
            if (root == null || canvasGroup == null)
            {
                throw new InvalidOperationException("FadeScaleUITransition 尚未初始化。");
            }
        }

        private static void EnsureMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                throw new InvalidOperationException("UI Transition 只能在 Unity 主线程操作。");
            }
        }
    }
}
