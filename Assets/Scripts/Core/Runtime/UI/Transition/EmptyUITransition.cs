using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class EmptyUITransition : IUITransition
    {
        /// <summary>
        /// 初始化空过渡；不会修改根节点。
        /// </summary>
        /// <param name="root">当前 View 的根节点。</param>
        public void Initialize(Transform root)
        {
        }

        /// <summary>
        /// 立即完成进入过渡。
        /// </summary>
        /// <param name="context">本次导航操作的过渡上下文。</param>
        /// <param name="cancellationToken">取消令牌；空过渡无需等待。</param>
        /// <returns>已完成任务。</returns>
        public UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 立即完成退出过渡。
        /// </summary>
        /// <param name="context">本次导航操作的过渡上下文。</param>
        /// <param name="cancellationToken">取消令牌；空过渡无需等待。</param>
        /// <returns>已完成任务。</returns>
        public UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// 立即完成指定方向；空过渡无状态需要同步。
        /// </summary>
        /// <param name="direction">需要立即完成的过渡方向。</param>
        public void CompleteImmediately(UITransitionDirection direction)
        {
        }

        /// 释放空过渡；没有资源需要清理。
        public void Dispose()
        {
        }
    }
}
