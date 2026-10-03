using UnityEngine;

namespace Hotfix.DroneFlight
{
    // HUD 的业务状态标签；不把遥测字符串重新解析成读数。
    internal static class DroneHudPresentation
    {
        internal static string Status(DroneHudSnapshot value) => value.OperationState switch
        {
            DroneFlightOperationState.Disarmed => "已锁定",
            DroneFlightOperationState.ArmedIdle => "已解锁 · 待机",
            DroneFlightOperationState.TakingOff => "自动起飞",
            DroneFlightOperationState.Landing => "自动降落",
            DroneFlightOperationState.Fault => "飞控故障",
            _ => "已解锁 · 飞行中"
        };

        internal static string Camera(DroneHudSnapshot value, string switchKey, string aimKey)
        {
            string mode = value.CameraMode switch
            {
                DroneCameraMode.Gimbal => "云台",
                DroneCameraMode.ThirdPerson => "第三人称",
                DroneCameraMode.Orbit => "环绕",
                DroneCameraMode.FixedForward => "机头",
                DroneCameraMode.Belly => "机腹",
                _ => "渔叉瞄准"
            };
            string hint = value.CameraMode == DroneCameraMode.HarpoonAim ? aimKey + " 退出瞄准" : switchKey + " 切换";
            string angles = value.CameraMode == DroneCameraMode.Gimbal ? $"Y {value.GimbalYaw:F0}° / P {value.GimbalPitch:F0}°  ·  " : "";
            return $"<b>{mode}</b>    <size=65%>{hint}</size>\n<size=60%>{angles}FOV {value.FieldOfView:F0}°</size>";
        }

        internal static string Gear(DroneLandingGearState value) => value switch
        {
            DroneLandingGearState.Deploying => "放下中",
            DroneLandingGearState.Retracting => "收起中",
            DroneLandingGearState.Retracted => "已收起",
            _ => "已放下"
        };
    }
}
