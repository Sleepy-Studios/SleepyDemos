using Hotfix.DroneFlight;

namespace Hotfix.DroneFlight.Adapters
{
    /// <summary>SleepyDemos HUD 与调试 View 使用的强类型数据。</summary>
    public sealed class DroneFlightViewData
    {
        public DroneFlightViewData(DroneFlightUiTelemetrySource telemetrySource, string sessionId)
        {
            TelemetrySource = telemetrySource;
            SessionId = sessionId;
        }

        /// 当前无人机的遥测快照源。
        public DroneFlightUiTelemetrySource TelemetrySource { get; }

        /// 当前 DroneFlight 会话标识。
        public string SessionId { get; }
    }
}
