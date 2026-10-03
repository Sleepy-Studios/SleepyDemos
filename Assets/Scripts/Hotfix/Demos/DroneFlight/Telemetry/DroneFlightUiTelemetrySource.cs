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
            DroneDebugSnapshot debug,
            float resetProgress,
            float resetHoldSeconds,
            bool telemetryVisible)
        {
            Hud = hud;
            Equipment = equipment;
            EquipmentText = equipmentText;
            WarningText = warningText;
            Debug = debug;
            ResetProgress = resetProgress;
            ResetHoldSeconds = resetHoldSeconds;
            TelemetryVisible = telemetryVisible;
        }

        internal DroneHudSnapshot Hud { get; }
        public DroneEquipmentSnapshot Equipment { get; }
        public string EquipmentText { get; }
        public string WarningText { get; }
        internal DroneDebugSnapshot Debug { get; }
        public float ResetProgress { get; }
        public float ResetHoldSeconds { get; }
        public bool TelemetryVisible { get; }
    }

    /// <summary>从当前无人机实例生成 UI 快照；View 不搜索场景对象。</summary>
    public sealed class DroneFlightUiTelemetrySource : MonoBehaviour
    {
        [SerializeField, InspectorName("诊断配置")]
        [Tooltip("集中管理遥测样本容量和界面刷新频率。")]
        private DroneDiagnosticsConfig config;

        private DroneFlightSceneContext context;
        private Vector3 homePosition;
        private float nextRefreshTime;

        public event Action<DroneFlightUiSnapshot> SnapshotChanged;

        public DroneFlightUiSnapshot Current { get; private set; }

        internal void Configure(DroneFlightSceneContext value, DroneDiagnosticsConfig diagnosticsConfig = null)
        {
            context = value;
            if (diagnosticsConfig != null)
            {
                config = diagnosticsConfig;
            }
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

            var refreshInterval = config != null ? config.UiRefreshIntervalSeconds : 0.1f;
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshInterval);
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
            var warning = DroneHudFormatter.FormatWarning(hud);
            if (string.IsNullOrEmpty(warning)) warning = context.EquipmentHost?.LastHint ?? string.Empty;
            var debug = new DroneDebugSnapshot(controller, context.LandingGear != null ? context.LandingGear.State : DroneLandingGearState.Deployed);
            var progress = context.PlayerInput != null ? context.PlayerInput.ResetProgress : 0f;
            var holdSeconds = context.PlayerInput != null ? context.PlayerInput.ResetHoldSeconds : 5f;
            var visible = context.ControlSession == null || context.ControlSession.IsActive;

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

        internal static string FormatGear(DroneLandingGearState state) => state switch
        {
            DroneLandingGearState.Deploying => "放下中",
            DroneLandingGearState.Retracted => "已收起",
            DroneLandingGearState.Retracting => "收起中",
            _ => "已放下"
        };

        internal static string FormatEquipmentState(DroneEquipmentState state) => state switch
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
