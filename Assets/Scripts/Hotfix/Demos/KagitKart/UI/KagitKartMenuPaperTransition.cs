using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 只把已有纸卡退出接入 UIManager 等待链；进入继续由业务 SetData 播放。
    public sealed class KagitKartMenuPaperTransition : IUITransition
    {
        private KagitKartMenuPaperMotion motion;

        /// <summary>缓存本页已有的纸卡动效，不新增运行时节点。</summary>
        /// <param name="root">正式菜单 Prefab 的根节点。</param>
        public void Initialize(Transform root) => motion = root.GetComponentInChildren<KagitKartMenuPaperMotion>(true);

        /// <summary>保留正常入口弹起；导航回滚时恢复已折卡的页面。</summary>
        /// <param name="context">公共导航上下文。</param>
        /// <param name="cancellationToken">导航取消令牌。</param>
        /// <returns>已完成的进入任务。</returns>
        public UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            motion?.RestoreAfterFold();
            return UniTask.CompletedTask;
        }

        /// <summary>等待本 Demo 0.16 秒折卡结束，再让 View 隐藏并释放。</summary>
        /// <param name="context">公共导航上下文。</param>
        /// <param name="cancellationToken">导航取消令牌；回滚由 View 恢复表现。</param>
        /// <returns>本页退出任务。</returns>
        public UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return motion == null ? UniTask.CompletedTask : motion.FoldAsync(cancellationToken);
        }

        /// <summary>按公共立即完成/回滚契约同步折卡终态，正常首次进入不跳过入口动效。</summary>
        /// <param name="direction">要完成的进入或退出方向。</param>
        public void CompleteImmediately(UITransitionDirection direction)
        {
            if (direction == UITransitionDirection.Enter)
            {
                motion?.RestoreAfterFold();
            }
            else
            {
                motion?.CompleteFold();
            }
        }

        /// 只释放本页缓存，不持有独立补间或世界对象。
        public void Dispose() => motion = null;
    }
}
