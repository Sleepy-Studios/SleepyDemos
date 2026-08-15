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
        internal DroneLandingGearState LandingGearState { get; }
    }

    /// <summary>无人机玩家 HUD 的确定性文本格式化器。</summary>
    internal static class DroneHudFormatter
    {
        internal static string FormatFlight(DroneHudSnapshot snapshot)
        {
            var armState = snapshot.IsArmed ? "ARMED" : "DISARMED";
            return $"{FormatProfile(snapshot.Profile)}  {armState}\n"
                   + $"H {snapshot.Height:F1} m   D {snapshot.Distance:F1} m\n"
                   + $"HS {snapshot.HorizontalSpeed:F1} m/s   VS {snapshot.VerticalSpeed:+0.0;-0.0;0.0} m/s";
        }

        internal static string FormatCamera(DroneHudSnapshot snapshot)
        {
            return $"{FormatCameraMode(snapshot.CameraMode)}   云台 Y {snapshot.GimbalYaw:F0}° / P {snapshot.GimbalPitch:F0}°   FOV {snapshot.FieldOfView:F0}°   F1 操作";
        }

        internal static string FormatWarning(DroneHudSnapshot snapshot)
        {
            if (snapshot.OperationState == DroneFlightOperationState.Fault)
            {
                return "飞控故障：请长按 R 重新运行场景";
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
                DroneEquipmentKind.Grapple => "H  四爪开合    J / K  上收 / 下放\n",
                DroneEquipmentKind.Harpoon => "V  机腹瞄准    H  发射 / 解除回收    J / K  收线 / 放线\n",
                _ => string.Empty
            };
            return "操作说明（F1 隐藏）\n"
                   + "F  开始控制（第三人称）\n"
                   + "T / G  自动起飞 / 降落\n"
                   + "R  解锁 / 锁定（长按重新运行场景）\n"
                   + "WASD  水平移动    Q / E  偏航\n"
                   + "Space / 左 Ctrl  升降\n"
                   + "1 / 2 / 3  平稳 / 普通 / 运动\n"
                   + "C  切换视角    方向键 / - / =  镜头\n"
                   + "L  起落架收放    " + equipment
                   + "F2  动力矢量    F3  调试面板    F4  复制遥测\n"
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
