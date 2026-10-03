using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// 调试面板读取同一次采样的物理真值，不做视觉平滑。
    internal readonly struct DroneDebugSnapshot
    {
        internal DroneDebugSnapshot(DroneFlightController controller, DroneLandingGearState gear)
        {
            Motors = controller.LastMotorOutput;
            Roll = controller.RollRateTelemetry;
            Pitch = controller.PitchRateTelemetry;
            Yaw = controller.YawRateTelemetry;
            TotalThrust = controller.LastTotalThrustNewtons;
            FixedStep = Time.fixedDeltaTime;
            BodyMass = controller.Body.mass;
            SupportedMass = controller.CurrentSupportedMassKilograms;
            RatedPayload = controller.Config.RatedPayloadKilograms;
            HoverCommand = controller.CurrentHoverCommand;
            PowerReserve = controller.CurrentPowerReserve;
            Gear = gear;
        }

        internal QuadrotorMotorOutput Motors { get; }
        internal DronePidTelemetry Roll { get; }
        internal DronePidTelemetry Pitch { get; }
        internal DronePidTelemetry Yaw { get; }
        internal float TotalThrust { get; }
        internal float FixedStep { get; }
        internal float BodyMass { get; }
        internal float SupportedMass { get; }
        internal float RatedPayload { get; }
        internal float HoverCommand { get; }
        internal float PowerReserve { get; }
        internal DroneLandingGearState Gear { get; }
    }

    internal static class DroneDebugFormatting
    {
        internal static string Axis(string name, DronePidTelemetry value) =>
            $"{name}<pos=24%><color=#F4A23A><mspace=0.6em>{value.Error:F3}</mspace></color><pos=44%><color=#F4A23A><mspace=0.6em>{value.Proportional:F3}</mspace></color><pos=64%><color=#F4A23A><mspace=0.6em>{value.Integral:F3}</mspace></color><pos=84%><color=#F4A23A><mspace=0.6em>{value.Derivative:F3}</mspace></color>";

        internal static string Equipment(DroneEquipmentSnapshot value) => value.Kind switch
        {
            DroneEquipmentKind.Grapple =>
                $"四爪状态  {DroneFlightUiTelemetrySource.FormatEquipmentState(value.State)}<pos=56%>捕获候选  {value.ContactCount}\n"
                + $"升降行程  {value.TravelMeters:F2} m<pos=56%>抓取拉力  {value.TensionNewtons:F1} N\n"
                + $"真实载荷  {value.PayloadMassKilograms:F2} kg<pos=56%>受支持载荷  {value.SupportedPayloadMassKilograms:F2} kg",
            DroneEquipmentKind.Harpoon =>
                $"渔叉状态  {DroneFlightUiTelemetrySource.FormatEquipmentState(value.State)}<pos=56%>可发射  {(value.CanUsePrimary ? "是" : "否")}\n"
                + $"绳长  {value.TravelMeters:F2} m<pos=56%>张力  {value.TensionNewtons:F1} N\n"
                + $"命中数  {value.ContactCount}<pos=36%>瞄准方向  ({value.AimDirection.x:F2}, {value.AimDirection.y:F2}, {value.AimDirection.z:F2})",
            _ => "纯无人机\n<color=#9BA5AA>无附加模块</color>"
        };
    }
}
