using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// <summary>HUD 格式化输入，保持显示逻辑可脱离场景测试。</summary>
    internal readonly struct DroneHudSnapshot
    {
        internal DroneHudSnapshot(
            DroneFlightOperationState operationState,
            DroneResponseProfile profile,
            bool isArmed,
            float liftInput,
            float height,
            float horizontalSpeed,
            float verticalSpeed,
            float distance,
            bool isMotorSaturated,
            DroneCameraMode cameraMode,
            float gimbalYaw,
            float gimbalPitch,
            float fieldOfView,
            bool hasPayload,
            string payloadType,
            float payloadMass,
            PayloadReleaseReason releaseReason)
        {
            OperationState = operationState;
            Profile = profile;
            IsArmed = isArmed;
            LiftInput = liftInput;
            Height = height;
            HorizontalSpeed = horizontalSpeed;
            VerticalSpeed = verticalSpeed;
            Distance = distance;
            IsMotorSaturated = isMotorSaturated;
            CameraMode = cameraMode;
            GimbalYaw = gimbalYaw;
            GimbalPitch = gimbalPitch;
            FieldOfView = fieldOfView;
            HasPayload = hasPayload;
            PayloadType = payloadType;
            PayloadMass = payloadMass;
            ReleaseReason = releaseReason;
        }

        internal DroneFlightOperationState OperationState { get; }
        internal DroneResponseProfile Profile { get; }
        internal bool IsArmed { get; }
        internal float LiftInput { get; }
        internal float Height { get; }
        internal float HorizontalSpeed { get; }
        internal float VerticalSpeed { get; }
        internal float Distance { get; }
        internal bool IsMotorSaturated { get; }
        internal DroneCameraMode CameraMode { get; }
        internal float GimbalYaw { get; }
        internal float GimbalPitch { get; }
        internal float FieldOfView { get; }
        internal bool HasPayload { get; }
        internal string PayloadType { get; }
        internal float PayloadMass { get; }
        internal PayloadReleaseReason ReleaseReason { get; }
    }

    /// <summary>无人机玩家 HUD 的确定性文本格式化器。</summary>
    internal static class DroneHudFormatter
    {
        internal static string FormatFlight(DroneHudSnapshot snapshot)
        {
            var armState = snapshot.IsArmed ? "ARMED" : "DISARMED";
            return $"POSITION  {snapshot.Profile}  {armState}\n"
                   + $"高度 {snapshot.Height:F1} m   距离 {snapshot.Distance:F1} m\n"
                   + $"水平 {snapshot.HorizontalSpeed:F1} m/s   垂直 {snapshot.VerticalSpeed:+0.0;-0.0;0.0} m/s   升降 {snapshot.LiftInput:+0.00;-0.00;0.00}";
        }

        internal static string FormatCamera(DroneHudSnapshot snapshot)
        {
            return $"{snapshot.CameraMode}   云台 Y {snapshot.GimbalYaw:F0}° / P {snapshot.GimbalPitch:F0}°   FOV {snapshot.FieldOfView:F0}°";
        }

        internal static string FormatPayload(DroneHudSnapshot snapshot)
        {
            return snapshot.HasPayload
                ? $"抓钩 已挂载  {snapshot.PayloadType}  {snapshot.PayloadMass:F2} kg"
                : "抓钩 空闲";
        }

        internal static string FormatWarning(DroneHudSnapshot snapshot)
        {
            if (snapshot.OperationState == DroneFlightOperationState.Fault)
            {
                return "飞控故障：请复位机体";
            }

            if (snapshot.ReleaseReason == PayloadReleaseReason.Overload)
            {
                return "载荷超重：挂载已拒绝";
            }

            return snapshot.IsMotorSaturated ? "电机输出饱和" : string.Empty;
        }
    }

    /// <summary>
    /// 从真实飞控、相机和挂载状态刷新玩家 HUD；F3 仅切换独立调试面板。
    /// </summary>
    public sealed class DroneHudPresenter : MonoBehaviour
    {
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private DroneCameraRig cameraRig;
        [SerializeField] private PayloadMount payloadMount;
        [SerializeField] private DroneRemoteControllerExperience remoteExperience;
        [SerializeField] private CanvasGroup playerHudRoot;
        [SerializeField] private TMP_Text flightText;
        [SerializeField] private TMP_Text cameraText;
        [SerializeField] private TMP_Text payloadText;
        [SerializeField] private TMP_Text warningText;
        [SerializeField] private GameObject debugPanel;
        [SerializeField] private TMP_Text debugText;
        [SerializeField] private float refreshIntervalSeconds = 0.1f;

        private Vector3 homePosition;
        private float nextRefreshTime;

        private void Awake()
        {
            if (flightController != null && flightController.Body != null)
            {
                homePosition = flightController.Body.position;
            }

            if (debugPanel != null)
            {
                debugPanel.SetActive(false);
            }

            RefreshVisibility();
            RefreshText();
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame && debugPanel != null)
            {
                debugPanel.SetActive(!debugPanel.activeSelf);
            }

            RefreshVisibility();
            if (Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, refreshIntervalSeconds);
            RefreshText();
        }

        /// <summary>
        /// 由场景装配器或测试夹具绑定 HUD 数据源和文本节点。
        /// </summary>
        internal void Configure(
            DroneFlightController controller,
            DroneCameraRig rig,
            PayloadMount mount,
            DroneRemoteControllerExperience remote,
            CanvasGroup hudRoot,
            TMP_Text flight,
            TMP_Text camera,
            TMP_Text payload,
            TMP_Text warning,
            GameObject diagnosticsPanel,
            TMP_Text diagnostics)
        {
            flightController = controller;
            cameraRig = rig;
            payloadMount = mount;
            remoteExperience = remote;
            playerHudRoot = hudRoot;
            flightText = flight;
            cameraText = camera;
            payloadText = payload;
            warningText = warning;
            debugPanel = diagnosticsPanel;
            debugText = diagnostics;
        }

        private void RefreshVisibility()
        {
            if (playerHudRoot == null)
            {
                return;
            }

            var visible = remoteExperience == null
                          || remoteExperience.State == DroneRemoteControlState.Fullscreen;
            playerHudRoot.alpha = visible ? 1f : 0f;
            playerHudRoot.interactable = visible;
            playerHudRoot.blocksRaycasts = visible;
        }

        private void RefreshText()
        {
            if (flightController == null || flightController.Body == null || cameraRig == null)
            {
                return;
            }

            var body = flightController.Body;
            var planarVelocity = new Vector2(body.linearVelocity.x, body.linearVelocity.z);
            var planarOffset = new Vector2(body.position.x - homePosition.x, body.position.z - homePosition.z);
            var hasPayload = payloadMount != null && payloadMount.HasPayload;
            var snapshot = new DroneHudSnapshot(
                flightController.OperationState,
                flightController.ResponseProfile,
                flightController.IsArmed,
                flightController.CurrentControlInput.Lift,
                body.position.y,
                planarVelocity.magnitude,
                body.linearVelocity.y,
                planarOffset.magnitude,
                flightController.LastMotorOutput.IsSaturated,
                cameraRig.Mode,
                cameraRig.GimbalYawDegrees,
                cameraRig.GimbalPitchDegrees,
                cameraRig.FieldOfView,
                hasPayload,
                hasPayload ? payloadMount.AttachedPayload.PayloadType : string.Empty,
                payloadMount != null ? payloadMount.AttachedMassKilograms : 0f,
                payloadMount != null ? payloadMount.LastReleaseReason : PayloadReleaseReason.None);

            if (flightText != null)
            {
                flightText.text = DroneHudFormatter.FormatFlight(snapshot);
            }

            if (cameraText != null)
            {
                cameraText.text = DroneHudFormatter.FormatCamera(snapshot);
            }

            if (payloadText != null)
            {
                payloadText.text = DroneHudFormatter.FormatPayload(snapshot);
            }

            if (warningText != null)
            {
                warningText.text = DroneHudFormatter.FormatWarning(snapshot);
            }

            if (debugText != null && debugPanel != null && debugPanel.activeSelf)
            {
                var motors = flightController.LastMotorOutput;
                var roll = flightController.RollRateTelemetry;
                var pitch = flightController.PitchRateTelemetry;
                var yaw = flightController.YawRateTelemetry;
                debugText.text = $"Motor FL {motors.FrontLeft:F3}  FR {motors.FrontRight:F3}\n"
                                 + $"Motor RL {motors.RearLeft:F3}  RR {motors.RearRight:F3}\n"
                                 + $"Roll P/I/D {roll.Proportional:F3}/{roll.Integral:F3}/{roll.Derivative:F3}  e {roll.Error:F3}\n"
                                 + $"Pitch P/I/D {pitch.Proportional:F3}/{pitch.Integral:F3}/{pitch.Derivative:F3}  e {pitch.Error:F3}\n"
                                 + $"Yaw P/I/D {yaw.Proportional:F3}/{yaw.Integral:F3}/{yaw.Derivative:F3}  e {yaw.Error:F3}\n"
                                 + $"Attitude scale {motors.AttitudeScale:F3}  Thrust {flightController.LastTotalThrustNewtons:F2} N\n"
                                 + $"fixedDeltaTime {Time.fixedDeltaTime:F3} s";
            }
        }
    }
}
