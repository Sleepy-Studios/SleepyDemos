using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
            PayloadReleaseReason releaseReason,
            DroneGrappleState grappleState = DroneGrappleState.Open,
            int grappleContacts = 0,
            DroneWinchState winchState = DroneWinchState.Stowed,
            DroneLandingGearState landingGearState = DroneLandingGearState.Deployed)
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
            GrappleState = grappleState;
            GrappleContacts = grappleContacts;
            WinchState = winchState;
            LandingGearState = landingGearState;
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
        internal DroneGrappleState GrappleState { get; }
        internal int GrappleContacts { get; }
        internal DroneWinchState WinchState { get; }
        internal DroneLandingGearState LandingGearState { get; }
    }

    /// <summary>无人机玩家 HUD 的确定性文本格式化器。</summary>
    internal static class DroneHudFormatter
    {
        internal static string FormatFlight(DroneHudSnapshot snapshot)
        {
            var armState = snapshot.IsArmed ? "ARMED" : "DISARMED";
            return $"位置保持  {FormatProfile(snapshot.Profile)}  {armState}\n"
                   + $"高度 {snapshot.Height:F1} m   距离 {snapshot.Distance:F1} m\n"
                   + $"水平 {snapshot.HorizontalSpeed:F1} m/s   垂直 {snapshot.VerticalSpeed:+0.0;-0.0;0.0} m/s   升降 {snapshot.LiftInput:+0.00;-0.00;0.00}";
        }

        internal static string FormatCamera(DroneHudSnapshot snapshot)
        {
            return $"{FormatCameraMode(snapshot.CameraMode)}   云台 Y {snapshot.GimbalYaw:F0}° / P {snapshot.GimbalPitch:F0}°   FOV {snapshot.FieldOfView:F0}°";
        }

        internal static string FormatPayload(DroneHudSnapshot snapshot)
        {
            var payload = snapshot.HasPayload
                ? $"已抓取 {snapshot.PayloadType} {snapshot.PayloadMass:F2} kg"
                : $"{FormatGrappleState(snapshot.GrappleState)} 接触 {snapshot.GrappleContacts}/3";
            return $"六爪 {payload}   卷扬 {FormatWinchState(snapshot.WinchState)}   起落架 {FormatLandingGearState(snapshot.LandingGearState)}";
        }

        internal static string FormatWarning(DroneHudSnapshot snapshot)
        {
            if (snapshot.OperationState == DroneFlightOperationState.Fault)
            {
                return "飞控故障：请长按 R 重新运行场景";
            }

            if (snapshot.ReleaseReason == PayloadReleaseReason.Overload)
            {
                return "载荷超重：挂载已拒绝";
            }

            if (snapshot.OperationState == DroneFlightOperationState.Landing
                && snapshot.LandingGearState is DroneLandingGearState.Retracted or DroneLandingGearState.Retracting)
            {
                return "降落警告：起落架尚未放下，请按 L";
            }

            return snapshot.IsMotorSaturated ? "电机输出饱和" : string.Empty;
        }

        internal static string FormatControls()
        {
            return "操作说明\n"
                   + "F  开始控制（第三人称）\n"
                   + "T / G  自动起飞 / 降落\n"
                   + "R  解锁 / 锁定（长按重新运行场景）\n"
                   + "WASD  水平移动    Q / E  偏航\n"
                   + "Space / 左 Ctrl  升降\n"
                   + "1 / 2 / 3  平稳 / 普通 / 运动\n"
                   + "C  切换视角    方向键 / - / =  镜头\n"
                   + "L  起落架收放    J  卷扬收放    H  六爪开合\n"
                   + "F3  调试面板    F4  复制遥测\n"
                   + "Backspace  返回主界面";
        }

        internal static string FormatProfile(DroneResponseProfile profile)
        {
            return profile switch
            {
                DroneResponseProfile.Cine => "平稳（Cine）",
                DroneResponseProfile.Sport => "运动（Sport）",
                _ => "普通（Normal）"
            };
        }

        private static string FormatCameraMode(DroneCameraMode mode)
        {
            return mode switch
            {
                DroneCameraMode.Gimbal => "云台（Gimbal）",
                DroneCameraMode.ThirdPerson => "第三人称（Third Person）",
                DroneCameraMode.Orbit => "环绕（Orbit）",
                DroneCameraMode.FixedForward => "机头（Forward）",
                DroneCameraMode.Belly => "机腹（Belly）",
                _ => mode.ToString()
            };
        }

        private static string FormatGrappleState(DroneGrappleState state)
        {
            return state switch
            {
                DroneGrappleState.Closing => "闭合中",
                DroneGrappleState.Contacting => "接触中",
                DroneGrappleState.AssistedGrip => "抓取",
                DroneGrappleState.Releasing => "释放中",
                DroneGrappleState.Broken => "已脱落",
                _ => "张开"
            };
        }

        private static string FormatWinchState(DroneWinchState state)
        {
            return state switch
            {
                DroneWinchState.Deploying => "放出中",
                DroneWinchState.Deployed => "已放出",
                DroneWinchState.Retracting => "收回中",
                DroneWinchState.Carrying => "运输高度",
                _ => "已收纳"
            };
        }

        private static string FormatLandingGearState(DroneLandingGearState state)
        {
            return state switch
            {
                DroneLandingGearState.Deploying => "放下中",
                DroneLandingGearState.Retracted => "已收起",
                DroneLandingGearState.Retracting => "收起中",
                _ => "已放下"
            };
        }
    }

    /// <summary>
    /// 从场景 Context 的真实飞控、相机和抓斗状态刷新正式玩家 HUD。
    /// </summary>
    public sealed class DroneHudPresenter : MonoBehaviour
    {
        [SerializeField] private DroneFlightController flightController;
        [SerializeField] private DroneCameraRig cameraRig;
        [SerializeField] private PayloadMount payloadMount;
        [SerializeField] private DroneMechanicalHook grapple;
        [SerializeField] private DroneWinchController winch;
        [SerializeField] private DroneLandingGearController landingGear;
        [SerializeField] private DronePlayerInput playerInput;
        [SerializeField] private DroneRemoteControllerExperience remoteExperience;
        [SerializeField] private CanvasGroup telemetryRoot;
        [SerializeField] private TMP_Text flightText;
        [SerializeField] private TMP_Text cameraText;
        [SerializeField] private TMP_Text payloadText;
        [SerializeField] private TMP_Text warningText;
        [SerializeField] private TMP_Text controlsText;
        [SerializeField] private GameObject resetProgressRoot;
        [SerializeField] private Image resetProgressFill;
        [SerializeField] private TMP_Text resetProgressText;
        [SerializeField] private float refreshIntervalSeconds = 0.1f;

        private Vector3 homePosition;
        private float nextRefreshTime;

        private void Awake()
        {
            if (flightController != null && flightController.Body != null)
            {
                homePosition = flightController.Body.position;
            }

            if (controlsText != null)
            {
                controlsText.text = DroneHudFormatter.FormatControls();
            }

            RefreshVisibility();
            RefreshText();
        }

        private void Update()
        {
            RefreshVisibility();
            RefreshResetProgress();
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
            TMP_Text controls,
            GameObject progressRoot,
            Image progressFill,
            TMP_Text progressText)
        {
            flightController = controller;
            cameraRig = rig;
            payloadMount = mount;
            remoteExperience = remote;
            telemetryRoot = hudRoot;
            flightText = flight;
            cameraText = camera;
            payloadText = payload;
            warningText = warning;
            controlsText = controls;
            resetProgressRoot = progressRoot;
            resetProgressFill = progressFill;
            resetProgressText = progressText;
            if (controlsText != null)
            {
                controlsText.text = DroneHudFormatter.FormatControls();
            }
        }

        /// <summary>
        /// 绑定当前 DroneFlight 场景上下文；View 每次显示时调用。
        /// </summary>
        /// <param name="context">当前加载场景唯一的无人机上下文。</param>
        internal void BindContext(DroneFlightSceneContext context)
        {
            if (context == null)
            {
                return;
            }

            flightController = context.FlightController;
            cameraRig = context.CameraRig;
            payloadMount = context.PayloadMount;
            grapple = context.Grapple;
            winch = context.Winch;
            landingGear = context.LandingGear;
            playerInput = context.PlayerInput;
            remoteExperience = context.RemoteExperience;
            if (flightController != null && flightController.Body != null)
            {
                homePosition = flightController.Body.position;
            }

            RefreshVisibility();
            RefreshResetProgress();
            RefreshText();
        }

        private void RefreshVisibility()
        {
            if (telemetryRoot == null)
            {
                return;
            }

            var visible = remoteExperience == null
                          || remoteExperience.State == DroneControlSessionState.Active;
            telemetryRoot.alpha = visible ? 1f : 0f;
            telemetryRoot.interactable = false;
            telemetryRoot.blocksRaycasts = false;
        }

        private void RefreshResetProgress()
        {
            var progress = playerInput != null ? playerInput.ResetProgress : 0f;
            if (resetProgressRoot != null)
            {
                resetProgressRoot.SetActive(progress > 0f);
            }

            if (resetProgressFill != null)
            {
                resetProgressFill.fillAmount = progress;
            }

            if (resetProgressText != null)
            {
                var holdSeconds = flightController != null && flightController.Config != null
                    ? flightController.Config.ResetHoldSeconds
                    : 5f;
                resetProgressText.text = $"重新运行场景 {progress * holdSeconds:F1} / {holdSeconds:F1} s";
            }
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
                payloadMount != null ? payloadMount.LastReleaseReason : PayloadReleaseReason.None,
                grapple != null ? grapple.State : DroneGrappleState.Open,
                grapple != null ? grapple.CurrentContactCount : 0,
                winch != null ? winch.State : DroneWinchState.Stowed,
                landingGear != null ? landingGear.State : DroneLandingGearState.Deployed);

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
                warningText.text = grapple != null && !string.IsNullOrEmpty(grapple.CurrentHint)
                    ? grapple.CurrentHint
                    : DroneHudFormatter.FormatWarning(snapshot);
            }

        }
    }
}
