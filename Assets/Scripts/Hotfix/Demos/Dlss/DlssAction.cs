using Core.Runtime;

namespace Hotfix.Dlss
{
    /// DLSS 观察场景的操作命令。
    public abstract class DlssAction : IAction
    {
    }

    /// 请求重置视角、打开设置或返回大厅。
    public sealed class DlssControlAction : DlssAction
    {
        public string Command;

        /// <summary>
        /// 请求重置视角、打开设置或返回大厅。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="command">已配置的业务输入动作名称；保持原输入资产的命名。</param>
        public DlssControlAction(string command)
        {
            Command = command;
        }
    }

    /// 同步触屏加速按钮的保持状态。
    public sealed class DlssSprintAction : DlssAction
    {
        public bool Held;

        /// <summary>
        /// 同步触屏加速按钮的保持状态。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="held">是否保持动作；false 必须释放对应输入。</param>
        public DlssSprintAction(bool held)
        {
            Held = held;
        }
    }

    /// 公共设置关闭后恢复观察控件。
    internal sealed class DlssSettingsClosedAction : DlssAction
    {
        internal DlssData Source;

        internal DlssSettingsClosedAction(DlssData source)
        {
            Source = source;
        }
    }

    /// 返回未成功时恢复观察状态。
    internal sealed class DlssRestoreAction : DlssAction
    {
        /// 发起导航的会话；旧实例结果被忽略。
        internal DlssData Source;

        /// <summary>
        /// 返回未成功时恢复观察状态。
        /// </summary>
        /// <param name="source">发起导航的会话；旧实例结果被忽略。</param>
        internal DlssRestoreAction(DlssData source)
        {
            Source = source;
        }
    }
}
