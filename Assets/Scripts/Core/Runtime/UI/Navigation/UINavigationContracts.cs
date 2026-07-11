using System;

namespace Core.Runtime
{
    public enum UIViewMode
    {
        Page,
        Modal,
        Widget
    }

    public enum UINavigationAction
    {
        Push,
        Replace,
        Close,
        Back,
        Preload,
        CloseAll
    }

    public enum UIOperationStatus
    {
        Succeeded = 1,
        Canceled = 2,
        Failed = 3,
        Ignored = 4
    }

    public enum UITransitionDirection
    {
        Enter,
        Exit
    }

    public readonly struct UITransitionContext
    {
        /// <summary>
        /// 创建 UI Transition 上下文。
        /// </summary>
        /// <param name="operationId">导航操作标识。</param>
        /// <param name="action">导航操作类型。</param>
        /// <param name="enteringView">正在进入的 View；没有时为 null。</param>
        /// <param name="exitingView">正在退出的 View；没有时为 null。</param>
        /// <param name="animated">是否播放过渡效果。</param>
        public UITransitionContext(
            long operationId,
            UINavigationAction action,
            View enteringView,
            View exitingView,
            bool animated)
        {
            OperationId = operationId;
            Action = action;
            EnteringView = enteringView;
            ExitingView = exitingView;
            Animated = animated;
        }

        /// 导航操作标识。
        public long OperationId { get; }

        /// 导航操作类型。
        public UINavigationAction Action { get; }

        /// 正在进入的 View；没有时为 null。
        public View EnteringView { get; }

        /// 正在退出的 View；没有时为 null。
        public View ExitingView { get; }

        /// 是否播放过渡效果。
        public bool Animated { get; }
    }

    public readonly struct UIShowOptions
    {
        private readonly bool? animated;
        private readonly bool? hidePrevious;

        /// <summary>
        /// 创建 View 显示选项。
        /// </summary>
        /// <param name="animated">是否播放显示动画。</param>
        public UIShowOptions(bool animated)
        {
            this.animated = animated;
            hidePrevious = null;
        }

        /// <summary>
        /// 创建 View 显示选项。
        /// </summary>
        /// <param name="animated">是否播放显示动画。</param>
        /// <param name="hidePrevious">是否隐藏同层级的上一 View。</param>
        public UIShowOptions(bool animated, bool hidePrevious)
        {
            this.animated = animated;
            this.hidePrevious = hidePrevious;
        }

        /// 是否播放显示动画；默认值为 true。
        public bool Animated => animated ?? true;

        /// 是否隐藏同层级的上一 View；默认值为 true。
        public bool HidePrevious => hidePrevious ?? true;
    }

    public readonly struct UIOperationResult
    {
        private UIOperationResult(
            long operationId,
            UINavigationAction action,
            UIOperationStatus status,
            View view,
            Exception exception)
        {
            OperationId = operationId;
            Action = action;
            Status = status;
            View = view;
            Exception = exception;
        }

        /// 导航操作标识。
        public long OperationId { get; }

        /// 导航操作类型。
        public UINavigationAction Action { get; }

        /// 导航操作状态。
        public UIOperationStatus Status { get; }

        /// 导航操作关联的 View。
        public View View { get; }

        /// 导航操作失败时的异常。
        public Exception Exception { get; }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        /// <param name="id">导航操作标识。</param>
        /// <param name="action">导航操作类型。</param>
        /// <param name="view">导航操作关联的 View。</param>
        /// <returns>成功状态的导航操作结果。</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="view"/> 为 null，且 <paramref name="action"/> 不是 CloseAll。
        /// </exception>
        public static UIOperationResult Succeeded(long id, UINavigationAction action, View view)
        {
            if (view == null && action != UINavigationAction.CloseAll)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return new UIOperationResult(
                id,
                action,
                UIOperationStatus.Succeeded,
                view,
                null);
        }

        /// <summary>
        /// 创建忽略结果。
        /// </summary>
        /// <param name="id">导航操作标识。</param>
        /// <param name="action">导航操作类型。</param>
        /// <param name="view">导航操作关联的 View。</param>
        /// <returns>忽略状态的导航操作结果。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="view"/> 为 null。</exception>
        public static UIOperationResult Ignored(long id, UINavigationAction action, View view)
        {
            return new UIOperationResult(
                id,
                action,
                UIOperationStatus.Ignored,
                view ?? throw new ArgumentNullException(nameof(view)),
                null);
        }

        /// <summary>
        /// 创建取消结果。
        /// </summary>
        /// <param name="id">导航操作标识。</param>
        /// <param name="action">导航操作类型。</param>
        /// <param name="view">导航操作关联的 View。</param>
        /// <returns>取消状态的导航操作结果。</returns>
        public static UIOperationResult Canceled(long id, UINavigationAction action, View view)
        {
            return new UIOperationResult(id, action, UIOperationStatus.Canceled, view, null);
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        /// <param name="id">导航操作标识。</param>
        /// <param name="action">导航操作类型。</param>
        /// <param name="view">导航操作关联的 View。</param>
        /// <param name="exception">导航操作失败时的异常。</param>
        /// <returns>失败状态的导航操作结果。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="exception"/> 为 null。</exception>
        public static UIOperationResult Failed(
            long id,
            UINavigationAction action,
            View view,
            Exception exception)
        {
            return new UIOperationResult(
                id,
                action,
                UIOperationStatus.Failed,
                view,
                exception ?? throw new ArgumentNullException(nameof(exception)));
        }
    }

    public static class UIViewModeResolver
    {
        /// <summary>
        /// 根据 UI 层级解析默认 View 模式。
        /// </summary>
        /// <param name="layer">目标 UI 层级。</param>
        /// <returns>该层级对应的默认 View 模式。</returns>
        public static UIViewMode Resolve(UILayer layer)
        {
            return layer switch
            {
                UILayer.Pop => UIViewMode.Modal,
                UILayer.Decorate => UIViewMode.Widget,
                UILayer.Tip => UIViewMode.Widget,
                _ => UIViewMode.Page
            };
        }
    }
}
