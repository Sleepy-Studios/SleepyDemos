using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public interface IUITransition : IDisposable
    {
        /// <summary>
        /// 使用 View 根节点初始化过渡实例。
        /// </summary>
        /// <param name="root">当前 View 的根节点。</param>
        void Initialize(Transform root);

        /// <summary>
        /// 播放进入过渡。
        /// </summary>
        /// <param name="context">本次导航操作的过渡上下文。</param>
        /// <param name="cancellationToken">取消本次过渡的令牌。</param>
        /// <returns>进入过渡任务。</returns>
        UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 播放退出过渡。
        /// </summary>
        /// <param name="context">本次导航操作的过渡上下文。</param>
        /// <param name="cancellationToken">取消本次过渡的令牌。</param>
        /// <returns>退出过渡任务。</returns>
        UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 立即完成指定方向的过渡并同步到最终状态。
        /// </summary>
        /// <param name="direction">需要立即完成的过渡方向。</param>
        void CompleteImmediately(UITransitionDirection direction);
    }

    public interface IUIWorldTransition
    {
        /// <summary>
        /// 播放世界进入过渡。
        /// </summary>
        /// <param name="context">本次导航操作的过渡上下文。</param>
        /// <param name="cancellationToken">取消本次过渡的令牌。</param>
        /// <returns>世界进入过渡任务。</returns>
        UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 播放世界退出过渡。
        /// </summary>
        /// <param name="context">本次导航操作的过渡上下文。</param>
        /// <param name="cancellationToken">取消本次过渡的令牌。</param>
        /// <returns>世界退出过渡任务。</returns>
        UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken);

        /// <summary>
        /// 立即完成指定方向的世界过渡并同步到最终状态。
        /// </summary>
        /// <param name="direction">需要立即完成的过渡方向。</param>
        void CompleteImmediately(UITransitionDirection direction);
    }

    public interface IUIWorldTransitionProvider
    {
        /// <summary>
        /// 解析指定 View 使用的世界过渡。
        /// </summary>
        /// <param name="view">需要解析世界过渡的 View。</param>
        /// <returns>匹配的世界过渡；没有匹配项时可返回 null。</returns>
        IUIWorldTransition Resolve(View view);
    }
}
