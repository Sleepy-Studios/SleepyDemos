using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>正式无人机 FBX、Prefab Builder 与契约测试共享的唯一模型结构约定。</summary>
    public static class DroneFlightModelContract
    {
        /// Unity Prefab 中正式模型实例的根节点名。
        public const string ModelRootName = "DroneModel";
        /// 机身主体节点名。
        public const string AirframeName = "Airframe";
        /// 逆时针旋翼共享源节点名。
        public const string CounterClockwiseBladeName = "RotorBlade_CCW";
        /// 顺时针旋翼共享源节点名。
        public const string ClockwiseBladeName = "RotorBlade_CW";
        /// 云台偏航节点名。
        public const string GimbalYawName = "GimbalYaw";
        /// 云台俯仰节点名。
        public const string GimbalPitchName = "GimbalPitch";
        /// 云台相机外壳节点名。
        public const string CameraBodyName = "CameraBody";
        /// Unity 侧碰撞代理根节点名。
        public const string CollisionProxyRootName = "CollisionProxies";
        /// 腹部模块化装备挂点名。
        public const string BellyEquipmentMountName = "BellyEquipmentMount";
        /// 正式 FBX 必须提供的对象数量。
        public const int FormalObjectCount = 14;
        /// 坐标和 Pivot 契约测试使用的米制容差。
        public const float CoordinateTolerance = 0.0001f;
        /// FBX 导入后模型根节点必须保持的单位缩放。
        public const float ImportScale = 1f;
        /// 起落架从放下姿态向机臂方向收起的局部旋转角。
        public const float LandingGearRetractionAngleDegrees = -67f;

        /// Unity 机体局部右轴。
        public static Vector3 RightAxis => Vector3.right;

        /// Unity 机体局部上轴。
        public static Vector3 UpAxis => Vector3.up;

        /// Unity 机体局部前轴。
        public static Vector3 ForwardAxis => Vector3.forward;

        /// 正式云台偏航节点的 Unity 本地旋转轴。
        public static Vector3 GimbalYawAxis => Vector3.up;

        /// 正式云台俯仰节点的 Unity 本地旋转轴。
        public static Vector3 GimbalPitchAxis => Vector3.right;

        /// 轴转换烘焙后 CameraBody 的 Unity 本地光轴。
        public static Vector3 GimbalOpticalAxis => Vector3.forward;

        /// Unity 机体坐标约定：+X 向右、+Y 向上、+Z 向前。
        public static Vector3 PhysicalThrustAxis => Vector3.up;

        /// 腹部装备挂点的机体局部坐标。
        public static Vector3 BellyEquipmentMountPosition => new(0f, -0.12f, 0f);

        /// 正式 FBX 的 14 个必需对象名。
        public static IReadOnlyList<string> FormalObjectNames { get; } = Array.AsReadOnly(new[]
        {
            AirframeName,
            "RotorHub_FL", "RotorHub_FR", "RotorHub_RL", "RotorHub_RR",
            CounterClockwiseBladeName, ClockwiseBladeName,
            "LandingGear_FL", "LandingGear_FR", "LandingGear_RL", "LandingGear_RR",
            GimbalYawName, GimbalPitchName, CameraBodyName
        });

        /// 按 FL、FR、RL、RR 排列的旋翼 Hub 节点名。
        public static IReadOnlyList<string> RotorHubNames { get; } = Array.AsReadOnly(new[]
        {
            "RotorHub_FL", "RotorHub_FR", "RotorHub_RL", "RotorHub_RR"
        });

        /// 四个旋翼轴心 Pivot 的机体局部坐标。
        public static IReadOnlyList<Vector3> RotorPositions { get; } = Array.AsReadOnly(new[]
        {
            new Vector3(-0.255f, 0.04f, 0.255f), new Vector3(0.255f, 0.04f, 0.255f),
            new Vector3(-0.255f, 0.04f, -0.255f), new Vector3(0.255f, 0.04f, -0.255f)
        });

        /// 按 FL、FR、RL、RR 排列的起落架节点名。
        public static IReadOnlyList<string> LandingGearNames { get; } = Array.AsReadOnly(new[]
        {
            "LandingGear_FL", "LandingGear_FR", "LandingGear_RL", "LandingGear_RR"
        });

        /// 四个起落架铰链 Pivot 的机体局部坐标。
        public static IReadOnlyList<Vector3> LandingGearHingePositions { get; } = Array.AsReadOnly(new[]
        {
            new Vector3(-0.112f, -0.035f, 0.118f), new Vector3(0.112f, -0.035f, 0.118f),
            new Vector3(-0.112f, -0.035f, -0.118f), new Vector3(0.112f, -0.035f, -0.118f)
        });

        /// 起落架完全展开时四个脚底基准点的机体局部坐标。
        public static IReadOnlyList<Vector3> LandingGearFootPositions { get; } = Array.AsReadOnly(new[]
        {
            new Vector3(-0.205f, -0.23f, 0.18f), new Vector3(0.205f, -0.23f, 0.18f),
            new Vector3(-0.205f, -0.23f, -0.18f), new Vector3(0.205f, -0.23f, -0.18f)
        });

        /// 正式 FBX 允许使用的材质槽名。
        public static IReadOnlyList<string> MaterialSlotNames { get; } = Array.AsReadOnly(new[]
        {
            "MAT_Graphite", "MAT_ShellTop", "MAT_MechanicalBlack",
            "MAT_SafetyOrange", "MAT_FrontLED", "MAT_CameraLens"
        });

        /// <summary>
        /// 返回指定旋翼位置应复用的桨叶节点名称。
        /// </summary>
        /// <param name="index">按 FL、FR、RL、RR 排列的索引。</param>
        /// <returns>CW 或 CCW 桨叶源节点名称。</returns>
        public static string GetRotorBladeName(int index) => index is 0 or 3
            ? CounterClockwiseBladeName
            : ClockwiseBladeName;

        /// <summary>
        /// 返回指定 FBX 材质槽对应的 Unity 材质资源名。
        /// </summary>
        /// <param name="slotName">FBX 中的 MAT_* 材质槽名。</param>
        /// <returns>Unity 材质名；未知槽返回 null。</returns>
        public static string GetMappedMaterialName(string slotName)
        {
            return slotName switch
            {
                "MAT_Graphite" => "DroneGraphite",
                "MAT_ShellTop" => "DroneShellTop",
                "MAT_MechanicalBlack" => "DroneMechanicalBlack",
                "MAT_SafetyOrange" => "DroneSafetyOrange",
                "MAT_FrontLED" => "DroneFrontLED",
                "MAT_CameraLens" => "DroneCameraLens",
                _ => null
            };
        }
    }
}
