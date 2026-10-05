using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;

namespace Hotfix.DroneFlight
{
    public enum DroneFlightSessionMode
    {
        Selecting,
        Loading,
        Active,
        Leaving
    }

    /// 机型选择、会话模式和UI快照；飞控与物理状态保留在实际机体。
    public sealed class DroneFlightData : IData
    {
        public List<IHandler> Handlers { get; }

        internal DroneFlightHandler Handler { get; }

        /// 本场遥测的唯一身份；机型切换仍属于同一场景会话。
        public string SessionId { get; } = Guid.NewGuid().ToString("N");

        /// 选择、准备、飞行或离场阶段。
        public DroneFlightSessionMode Mode { get; internal set; }

        /// 待启动的机型。
        public DroneVehicleKind SelectedKind { get; internal set; }

        /// 准备或导航失败的页面反馈。
        public string Feedback { get; internal set; }

        /// 最近一次 UI 遥测快照，物理真源仍在机体。
        public DroneFlightUiSnapshot Snapshot { get; internal set; }

        /// 是否已经接收到本场有效遥测。
        public bool HasSnapshot { get; internal set; }

        /// 当前公共输入设备类型。
        public InputDeviceKind DeviceKind { get; internal set; }

        /// 专属触屏控制面板是否打开。
        public bool PanelVisible { get; internal set; }

        /// 帮助或调试页面是否阻断玩法控制。
        public bool ControlsSuppressed { get; internal set; }

        /// 帮助页面的目标打开状态。
        public bool HelpRequested { get; internal set; }

        /// 调试页面的目标打开状态。
        public bool DebugRequested { get; internal set; }

        /// 调试页面实际是否已显示。
        public bool DebugVisible { get; internal set; }

        /// 场景调试图形是否显示。
        public bool DebugDrawVisible { get; internal set; }

        /// 页面协调器是否正在关闭本场界面。
        public bool ShuttingDown { get; internal set; }

        /// 触屏视角修饰模式是否启用。
        public bool TouchLook { get; internal set; }

        internal int Version { get; set; }

        public DroneFlightData() : this(null, null)
        {
        }

        /// <summary>
        /// 创建本场状态与命令处理器，具体场景在初始化时配置启动与返回行为。
        /// </summary>
        /// <param name="start">机型确认后的场景启动回调；null 时等待场景配置。</param>
        /// <param name="back">返回大厅回调；null 时等待场景配置。</param>
        public DroneFlightData(Action<DroneVehicleKind> start, Action back)
        {
            Handler = new DroneFlightHandler(start, back);
            Handlers = new()
            {
                Handler
            };
        }

        /// 重置业务状态，使上一轮请求结果失效。
        public void ClearData()
        {
            Version++;
            Mode = DroneFlightSessionMode.Selecting;
            SelectedKind = DroneVehicleKind.Plain;
            Feedback = null;
            Snapshot = default;
            HasSnapshot = PanelVisible = ControlsSuppressed = HelpRequested = DebugRequested = DebugVisible = DebugDrawVisible = ShuttingDown = TouchLook = false;
        }
    }
}
