using Core.Runtime;

namespace Hotfix.DroneFlight
{
    /// 无人机会话的选择、操作和遥测命令。
    public abstract class DroneFlightAction : IAction
    {
    }

    /// 选择待启动的机型。
    public sealed class DroneFlightSelectAction : DroneFlightAction
    {
        public DroneVehicleKind Kind;

        /// <summary>
        /// 选择待启动的机型。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="kind">待启动机型，必须为当前支持的机型枚举。</param>
        public DroneFlightSelectAction(DroneVehicleKind kind)
        {
            Kind = kind;
        }
    }

    /// 启动当前选中的机型。
    public sealed class DroneFlightStartAction : DroneFlightAction
    {
    }

    /// 请求返回 Hub。
    public sealed class DroneFlightExitAction : DroneFlightAction
    {
    }

    /// 接收本次机体准备的异步结果。
    internal sealed class DroneFlightSelectionResultAction : DroneFlightAction
    {
        internal int Version;

        internal bool Succeeded;

        internal string Message;

        /// <summary>
        /// 接收本次机体准备的异步结果。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="version">发起准备时的会话版本；过期结果被忽略。</param>
        /// <param name="succeeded">机体与界面是否成功准备完成。</param>
        /// <param name="message">用户可见的业务反馈，null 表示清除。默认值为 null。</param>
        internal DroneFlightSelectionResultAction(int version, bool succeeded, string message = null)
        {
            Version = version;
            Succeeded = succeeded;
            Message = message;
        }
    }

    /// 执行一个已有输入动作的业务命令。
    public sealed class DroneFlightControlAction : DroneFlightAction
    {
        public string Command;

        /// <summary>
        /// 执行一个已有输入动作的业务命令。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="command">已配置的业务输入动作名称；保持原输入资产的命名。</param>
        public DroneFlightControlAction(string command)
        {
            Command = command;
        }
    }

    /// 同步需要保持或释放的触屏动作。
    public sealed class DroneFlightHoldAction : DroneFlightAction
    {
        public string Command;

        public bool Held;

        /// <summary>
        /// 同步需要保持或释放的触屏动作。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="command">已配置的业务输入动作名称；保持原输入资产的命名。</param>
        /// <param name="held">是否保持动作；false 必须释放对应输入。</param>
        public DroneFlightHoldAction(string command, bool held)
        {
            Command = command;
            Held = held;
        }
    }

    /// 发布当前会话的 HUD 和 Debug 遥测快照。
    internal sealed class DroneFlightTelemetryAction : DroneFlightAction
    {
        internal string SessionId;

        internal DroneFlightUiSnapshot Snapshot;

        /// <summary>
        /// 发布当前会话的 HUD 和 Debug 遥测快照。构造后通过 GlobalData.Dispatch 派发。
        /// </summary>
        /// <param name="sessionId">遥测所属会话 ID；其它会话的快照被忽略。</param>
        /// <param name="snapshot">按现有采样频率采集的显示快照。</param>
        internal DroneFlightTelemetryAction(string sessionId, DroneFlightUiSnapshot snapshot)
        {
            SessionId = sessionId;
            Snapshot = snapshot;
        }
    }

    /// 更新机型选择反馈并恢复可选择状态。
    internal sealed class DroneFlightFeedbackAction : DroneFlightAction
    {
        /// 发起请求的会话；旧实例请求被忽略。
        internal DroneFlightData Source;

        /// 显示给用户的准备反馈。
        internal string Message;

        /// <summary>
        /// 更新机型选择反馈并恢复可选择状态。
        /// </summary>
        /// <param name="source">发起请求的会话；旧实例请求被忽略。</param>
        /// <param name="message">显示给用户的准备反馈。</param>
        internal DroneFlightFeedbackAction(DroneFlightData source, string message)
        {
            Source = source;
            Message = message;
        }
    }

    /// 同步页面关闭阶段及输入阻断。
    internal sealed class DroneFlightShuttingDownAction : DroneFlightAction
    {
        /// 发起请求的会话；旧实例请求被忽略。
        internal DroneFlightData Source;

        /// 是否正在关闭会话页面。
        internal bool ShuttingDown;

        /// <summary>
        /// 同步页面关闭阶段及输入阻断。
        /// </summary>
        /// <param name="source">发起请求的会话；旧实例请求被忽略。</param>
        /// <param name="shuttingDown">是否正在关闭会话页面。</param>
        internal DroneFlightShuttingDownAction(DroneFlightData source, bool shuttingDown)
        {
            Source = source;
            ShuttingDown = shuttingDown;
        }
    }

    /// 同步帮助页面的实际请求状态。
    internal sealed class DroneFlightHelpResultAction : DroneFlightAction
    {
        /// 发起请求的会话；旧实例请求被忽略。
        internal DroneFlightData Source;

        /// 帮助页面是否仍需保持打开。
        internal bool Visible;

        /// <summary>
        /// 同步帮助页面的实际请求状态。
        /// </summary>
        /// <param name="source">发起请求的会话；旧实例请求被忽略。</param>
        /// <param name="visible">帮助页面是否仍需保持打开。</param>
        internal DroneFlightHelpResultAction(DroneFlightData source, bool visible)
        {
            Source = source;
            Visible = visible;
        }
    }

    /// 同步调试页面的实际显示结果。
    internal sealed class DroneFlightDebugResultAction : DroneFlightAction
    {
        /// 发起请求的会话；旧实例请求被忽略。
        internal DroneFlightData Source;

        /// 调试页面是否已显示。
        internal bool Visible;

        /// <summary>
        /// 同步调试页面的实际显示结果。
        /// </summary>
        /// <param name="source">发起请求的会话；旧实例请求被忽略。</param>
        /// <param name="visible">调试页面是否已显示。</param>
        internal DroneFlightDebugResultAction(DroneFlightData source, bool visible)
        {
            Source = source;
            Visible = visible;
        }
    }

    /// 设置本会话的调试绘制状态。
    internal sealed class DroneFlightDebugDrawAction : DroneFlightAction
    {
        /// 发起请求的会话；旧实例请求被忽略。
        internal DroneFlightData Source;

        /// 是否显示调试图形。
        internal bool Visible;

        /// <summary>
        /// 设置本会话的调试绘制状态。
        /// </summary>
        /// <param name="source">发起请求的会话；旧实例请求被忽略。</param>
        /// <param name="visible">是否显示调试图形。</param>
        internal DroneFlightDebugDrawAction(DroneFlightData source, bool visible)
        {
            Source = source;
            Visible = visible;
        }
    }

    /// 进入离场阶段并禁止新的选择。
    internal sealed class DroneFlightBeginLeavingAction : DroneFlightAction
    {
        /// 发起请求的会话；旧实例请求被忽略。
        internal DroneFlightData Source;

        /// <summary>
        /// 进入离场阶段并禁止新的选择。
        /// </summary>
        /// <param name="source">发起请求的会话；旧实例请求被忽略。</param>
        internal DroneFlightBeginLeavingAction(DroneFlightData source)
        {
            Source = source;
        }
    }

    /// 导航未完成时恢复实际会话模式。
    internal sealed class DroneFlightRestoreModeAction : DroneFlightAction
    {
        /// 发起请求的会话；旧实例请求被忽略。
        internal DroneFlightData Source;

        /// 机体仍存在时为 true，否则恢复选择页。
        internal bool Active;

        /// <summary>
        /// 导航未完成时恢复实际会话模式。
        /// </summary>
        /// <param name="source">发起请求的会话；旧实例请求被忽略。</param>
        /// <param name="active">机体仍存在时为 true，否则恢复选择页。</param>
        internal DroneFlightRestoreModeAction(DroneFlightData source, bool active)
        {
            Source = source;
            Active = active;
        }
    }
}
