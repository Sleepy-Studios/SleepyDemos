using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;

namespace Hotfix.DroneFlight
{
    public enum DroneFlightSessionMode { Selecting, Loading, Active, Leaving }
    /// 机型选择、会话模式和UI快照；飞控与物理状态保留在实际机体。
    public sealed class DroneFlightData : IData
    {
        public List<IHandler> Handlers { get; }
        internal DroneFlightHandler Handler { get; }
        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public DroneFlightSessionMode Mode { get; internal set; }
        public DroneVehicleKind SelectedKind { get; internal set; }
        public string Feedback { get; internal set; }
        public DroneFlightUiSnapshot Snapshot { get; internal set; }
        public bool HasSnapshot { get; internal set; }
        public InputDeviceKind DeviceKind { get; internal set; }
        public bool PanelVisible { get; internal set; }
        public bool ControlsSuppressed { get; internal set; }
        public bool HelpRequested { get; internal set; }
        public bool DebugRequested { get; internal set; }
        public bool DebugVisible { get; internal set; }
        public bool DebugDrawVisible { get; internal set; }
        public bool ShuttingDown { get; internal set; }
        public bool TouchLook { get; internal set; }
        internal int Version { get; set; }
        public DroneFlightData() : this(null, null) { }
        public DroneFlightData(Action<DroneVehicleKind> start, Action back)
        {
            Handler = new DroneFlightHandler(start, back);
            Handlers = new() { Handler };
        }
        public void ClearData()
        {
            Version++; Mode=DroneFlightSessionMode.Selecting; SelectedKind=DroneVehicleKind.Plain; Feedback=null;
            Snapshot=default; HasSnapshot=PanelVisible=ControlsSuppressed=HelpRequested=DebugRequested=DebugVisible=DebugDrawVisible=ShuttingDown=TouchLook=false;
        }
    }
}
