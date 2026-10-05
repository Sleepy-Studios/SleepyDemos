using Core.Runtime;
using Hotfix.SceneManagement;

namespace Hotfix
{
    /// 大厅选择、进入和反馈命令。
    public abstract class MainMenuAction : IAction
    {
    }

    /// 选择一张大厅 Demo 卡片。
    public sealed class MainMenuSelectAction : MainMenuAction
    {
        public int Index;

        /// <summary>
        /// 选择一张大厅 Demo 卡片。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="index">从零开始的 Demo 卡片索引；越界值被忽略。</param>
        public MainMenuSelectAction(int index)
        {
            Index = index;
        }
    }

    /// 请求进入指定 Demo。
    public sealed class MainMenuEnterAction : MainMenuAction
    {
        public GameSceneId Target;

        /// <summary>
        /// 请求进入指定 Demo。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="target">本次需要进入的 Demo 场景。</param>
        public MainMenuEnterAction(GameSceneId target)
        {
            Target = target;
        }
    }

    /// 同步场景导航对入口交互的阻断。
    internal sealed class MainMenuAvailabilityAction : MainMenuAction
    {
        internal bool Transitioning;

        /// <summary>
        /// 同步场景导航对入口交互的阻断。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="transitioning">场景导航是否正在进行；true 时阻断入口。</param>
        internal MainMenuAvailabilityAction(bool transitioning)
        {
            Transitioning = transitioning;
        }
    }

    /// 更新大厅入口反馈。
    internal sealed class MainMenuFeedbackAction : MainMenuAction
    {
        internal string Message;

        /// <summary>
        /// 更新大厅入口反馈。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="message">用户可见的业务反馈，null 表示清除。</param>
        internal MainMenuFeedbackAction(string message)
        {
            Message = message;
        }
    }
}
