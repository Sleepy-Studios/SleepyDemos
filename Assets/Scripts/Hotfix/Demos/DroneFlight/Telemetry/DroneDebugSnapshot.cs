using Core.Runtime;
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
            $"{name}<pos=24%>{Number(value.Error)}<pos=44%>{Number(value.Proportional)}<pos=64%>{Number(value.Integral)}<pos=84%>{Number(value.Derivative)}";

        private static string Number(float value) =>
            ColorUtil.WrapText($"<mspace=0.6em>{value:F3}</mspace>", DroneFlightUIColors.TelemetryAccent);

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
            _ => "纯无人机\n" + ColorUtil.WrapText("无附加模块", DroneFlightUIColors.EquipmentSecondary)
        };
    }
}
