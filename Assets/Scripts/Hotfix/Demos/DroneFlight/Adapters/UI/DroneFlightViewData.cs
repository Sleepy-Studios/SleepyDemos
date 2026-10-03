using Hotfix.DroneFlight;

namespace Hotfix.DroneFlight.Adapters
{
    /// <summary>SleepyDemos HUD 与调试 View 使用的强类型数据。</summary>
    public sealed class DroneFlightViewData
    {
        public DroneFlightViewData(DroneFlightUiTelemetrySource telemetrySource, string sessionId, DronePlayerInput input = null)
        {
            TelemetrySource = telemetrySource;
            SessionId = sessionId; Input = input;
        }

        /// 当前无人机的遥测快照源。
        public DroneFlightUiTelemetrySource TelemetrySource { get; }

        /// 当前 DroneFlight 会话标识。
        public string SessionId { get; }
        /// 当前会话输入，HUD 提示从同一动作副本解析。
        public DronePlayerInput Input { get; }
    }
}
