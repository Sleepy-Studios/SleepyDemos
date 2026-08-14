using UnityEngine;

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
                : $"{FormatGrappleState(snapshot.GrappleState)} 接触 {snapshot.GrappleContacts}/2";
            return $"四爪 {payload}   卷扬 {FormatWinchState(snapshot.WinchState)}   起落架 {FormatLandingGearState(snapshot.LandingGearState)}";
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
            return FormatControls(DroneEquipmentKind.Grapple);
        }

        internal static string FormatControls(DroneEquipmentKind kind)
        {
            var equipment = kind switch
            {
                DroneEquipmentKind.Grapple => "J  抓斗收放    H  四爪开合\n",
                DroneEquipmentKind.Harpoon => "H  发射 / 解除回收    J / K  收线 / 放线\n",
                _ => string.Empty
            };
            return "操作说明\n"
                   + "F  开始控制（第三人称）\n"
                   + "T / G  自动起飞 / 降落\n"
                   + "R  解锁 / 锁定（长按重新运行场景）\n"
                   + "WASD  水平移动    Q / E  偏航\n"
                   + "Space / 左 Ctrl  升降\n"
                   + "1 / 2 / 3  平稳 / 普通 / 运动\n"
                   + "C  切换视角    方向键 / - / =  镜头\n"
                   + "L  起落架收放    " + equipment
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
}
