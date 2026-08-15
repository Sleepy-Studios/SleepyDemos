using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>HUD 与 F3 共同消费的不可变 UI 快照。</summary>
    public readonly struct DroneFlightUiSnapshot
    {
        internal DroneFlightUiSnapshot(
            DroneHudSnapshot hud,
            DroneEquipmentSnapshot equipment,
            string equipmentText,
            string warningText,
            string debugText,
            float resetProgress,
            float resetHoldSeconds,
            bool telemetryVisible)
        {
            Hud = hud;
            Equipment = equipment;
            EquipmentText = equipmentText;
            WarningText = warningText;
            DebugText = debugText;
            ResetProgress = resetProgress;
            ResetHoldSeconds = resetHoldSeconds;
            TelemetryVisible = telemetryVisible;
        }

        internal DroneHudSnapshot Hud { get; }
        public DroneEquipmentSnapshot Equipment { get; }
        public string EquipmentText { get; }
        public string WarningText { get; }
        public string DebugText { get; }
        public float ResetProgress { get; }
        public float ResetHoldSeconds { get; }
        public bool TelemetryVisible { get; }
    }

    /// <summary>从当前无人机实例生成 UI 快照；View 不搜索场景对象。</summary>
    public sealed class DroneFlightUiTelemetrySource : MonoBehaviour
    {
        [SerializeField] private float refreshIntervalSeconds = 0.1f;

        private DroneFlightSceneContext context;
        private Vector3 homePosition;
        private float nextRefreshTime;

        public event Action<DroneFlightUiSnapshot> SnapshotChanged;

        public DroneFlightUiSnapshot Current { get; private set; }

        internal void Configure(DroneFlightSceneContext value)
        {
            context = value;
            var body = context != null ? context.FlightController?.Body : null;
            homePosition = body != null ? body.position : transform.position;
            Publish();
        }

        private void Update()
        {
            if (context == null || Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshIntervalSeconds);
            Publish();
        }

        private void Publish()
        {
            var controller = context?.FlightController;
            var body = controller != null ? controller.Body : null;
            var cameraRig = context?.CameraRig;
            if (controller == null || body == null || cameraRig == null)
            {
                return;
            }

            var equipment = context.EquipmentHost != null ? context.EquipmentHost.Snapshot : default;
            var planarVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.z);
            var planarOffset = new Vector2(body.position.x - homePosition.x, body.position.z - homePosition.z);
            var hud = new DroneHudSnapshot(
                controller.OperationState,
                controller.ResponseProfile,
                controller.IsArmed,
                controller.CurrentControlInput.Lift,
                body.position.y,
                planarVelocity.magnitude,
                body.linearVelocity.y,
                planarOffset.magnitude,
                controller.LastMotorOutput.IsSaturated,
                cameraRig.Mode,
                cameraRig.GimbalYawDegrees,
                cameraRig.GimbalPitchDegrees,
                cameraRig.FieldOfView,
                context.LandingGear != null ? context.LandingGear.State : DroneLandingGearState.Deployed);
            var equipmentText = equipment.Kind switch
            {
                DroneEquipmentKind.Grapple =>
                    $"四爪 {FormatEquipmentState(equipment.State)}   吊点 {equipment.TravelMeters:F2} m   候选 {equipment.ContactCount}   载荷 {equipment.SupportedPayloadMassKilograms:F2}/{equipment.PayloadMassKilograms:F2} kg",
                DroneEquipmentKind.Harpoon =>
                    $"渔叉 {FormatEquipmentState(equipment.State)}   绳长 {equipment.TravelMeters:F1} m   张力 {equipment.TensionNewtons:F1} N",
                _ => "纯无人机   无附加模块"
            };
            var warning = context.EquipmentHost != null && !string.IsNullOrEmpty(context.EquipmentHost.LastHint)
                ? context.EquipmentHost.LastHint
                : DroneHudFormatter.FormatWarning(hud);
            var debug = FormatDebug(controller, equipment, context.LandingGear);
            var progress = context.PlayerInput != null ? context.PlayerInput.ResetProgress : 0f;
            var holdSeconds = controller.Config != null ? controller.Config.ResetHoldSeconds : 5f;
            var visible = context.RemoteExperience == null
                          || context.RemoteExperience.State == DroneControlSessionState.Active;

            Current = new DroneFlightUiSnapshot(
                hud,
                equipment,
                equipmentText,
                warning,
                debug,
                progress,
                holdSeconds,
                visible);
            SnapshotChanged?.Invoke(Current);
        }

        private static string FormatDebug(
            DroneFlightController controller,
            DroneEquipmentSnapshot equipment,
            DroneLandingGearController landingGear)
        {
            var motors = controller.LastMotorOutput;
            var roll = controller.RollRateTelemetry;
            var pitch = controller.PitchRateTelemetry;
            var yaw = controller.YawRateTelemetry;
            var equipmentText = equipment.Kind switch
            {
                DroneEquipmentKind.Grapple =>
                    $"四爪状态 {FormatEquipmentState(equipment.State)}  捕获候选 {equipment.ContactCount}\n"
                    + $"升降行程 {equipment.TravelMeters:F2} m  抓取拉力 {equipment.TensionNewtons:F1} N\n"
                    + $"载荷真实/受支持 {equipment.PayloadMassKilograms:F2}/{equipment.SupportedPayloadMassKilograms:F2} kg",
                DroneEquipmentKind.Harpoon =>
                    $"渔叉状态 {FormatEquipmentState(equipment.State)}  可发射 {(equipment.CanUsePrimary ? "是" : "否")}\n"
                    + $"绳长 {equipment.TravelMeters:F2} m  绳索张力 {equipment.TensionNewtons:F1} N  命中 {equipment.ContactCount}\n"
                    + $"瞄准方向 {FormatVector(equipment.AimDirection)}",
                _ => "装备状态 无附加模块"
            };
            return $"电机 左前(FL) {motors.FrontLeft:F3}  右前(FR) {motors.FrontRight:F3}\n"
                   + $"电机 左后(RL) {motors.RearLeft:F3}  右后(RR) {motors.RearRight:F3}\n"
                   + FormatAxis("横滚(Roll)", roll) + "\n"
                   + FormatAxis("俯仰(Pitch)", pitch) + "\n"
                   + FormatAxis("偏航(Yaw)", yaw) + "\n"
                   + $"总升力 {controller.LastTotalThrustNewtons:F1} N  物理步长 {Time.fixedDeltaTime:F3} s\n"
                   + $"主刚体 {controller.Body.mass:F2} kg  附加装备 0.00 kg  当前总承载 {controller.CurrentSupportedMassKilograms:F2} kg\n"
                   + $"额定载重 {controller.Config.RatedPayloadKilograms:F2} kg  当前悬停指令 {controller.CurrentHoverCommand:F3}  动力余量 {controller.CurrentPowerReserve:P0}\n"
                   + $"起落架 {FormatGear(landingGear != null ? landingGear.State : DroneLandingGearState.Deployed)}\n"
                   + equipmentText;
        }

        private static string FormatAxis(string name, DronePidTelemetry telemetry) =>
            $"{name} 误差 {telemetry.Error:F3}  比例P {telemetry.Proportional:F3}  积分I {telemetry.Integral:F3}  微分D {telemetry.Derivative:F3}";

        private static string FormatVector(Vector3 value) => $"({value.x:F2}, {value.y:F2}, {value.z:F2})";

        private static string FormatGear(DroneLandingGearState state) => state switch
        {
            DroneLandingGearState.Deploying => "放下中",
            DroneLandingGearState.Retracted => "已收起",
            DroneLandingGearState.Retracting => "收起中",
            _ => "已放下"
        };

        private static string FormatEquipmentState(DroneEquipmentState state) => state switch
        {
            DroneEquipmentState.Deploying => "放下中",
            DroneEquipmentState.Ready => "就绪",
            DroneEquipmentState.Retracting => "收纳中",
            DroneEquipmentState.Carrying => "携带中",
            DroneEquipmentState.Fired => "飞行中",
            DroneEquipmentState.Attached => "已命中",
            DroneEquipmentState.Recovering => "回收中",
            DroneEquipmentState.Broken => "已断裂",
            _ => "已收纳"
        };
    }
}
