using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>航点到达末端后的推进方式。</summary>
    public enum DroneCruiseMode
    {
        Once,
        Loop,
        PingPong
    }

    /// <summary>无人机飞向航点时采用的机头朝向。</summary>
    public enum DroneCruiseHeadingMode
    {
        AlongRoute,
        KeepCurrent,
        UseWaypointForward
    }

    /// <summary>一个场景航点及其局部巡航设置。</summary>
    [Serializable]
    public sealed class DroneCruiseWaypoint
    {
        [SerializeField, InspectorName("航点")]
        [Tooltip("无人机需要抵达的场景 Transform。")]
        private Transform target;

        [SerializeField, Min(0f), InspectorName("停留时间 (秒)")]
        [Tooltip("稳定抵达后在此点保持的时间。")]
        private float waitSeconds;

        [SerializeField, Min(0f), InspectorName("速度覆盖 (米/秒)")]
        [Tooltip("大于 0 时覆盖路线默认速度；为 0 时使用路线默认速度。")]
        private float speedOverride;

        [SerializeField, InspectorName("朝向策略")]
        [Tooltip("沿路线转向、保持进入本段时的朝向，或使用航点自身的 Forward。")]
        private DroneCruiseHeadingMode headingMode = DroneCruiseHeadingMode.AlongRoute;

        internal Transform Target => target;
        internal float WaitSeconds => Mathf.Max(0f, waitSeconds);
        internal float SpeedOverride => Mathf.Max(0f, speedOverride);
        internal DroneCruiseHeadingMode HeadingMode => headingMode;

        internal void Configure(
            Transform value,
            float wait,
            float speed,
            DroneCruiseHeadingMode heading)
        {
            target = value;
            waitSeconds = Mathf.Max(0f, wait);
            speedOverride = Mathf.Max(0f, speed);
            headingMode = heading;
        }
    }

    /// <summary>场景内可复用的有序航点路线。</summary>
    public sealed class DroneCruiseRoute : MonoBehaviour
    {
        [SerializeField, InspectorName("巡航模式")]
        [Tooltip("单次、循环或在首尾航点之间往返。")]
        private DroneCruiseMode mode = DroneCruiseMode.Once;

        [SerializeField, Min(0.1f), InspectorName("默认速度 (米/秒)")]
        [Tooltip("航点没有单独覆盖速度时使用的飞行速度。")]
        private float defaultSpeedMetersPerSecond = 4f;

        [SerializeField, InspectorName("有序航点")]
        [Tooltip("按数组顺序飞行；至少需要两个有效航点。")]
        private DroneCruiseWaypoint[] waypoints = Array.Empty<DroneCruiseWaypoint>();

        public DroneCruiseMode Mode => mode;
        public int WaypointCount => waypoints?.Length ?? 0;
        internal float DefaultSpeedMetersPerSecond => Mathf.Max(0.1f, defaultSpeedMetersPerSecond);

        internal bool TryGetWaypoint(int index, out DroneCruiseWaypoint waypoint)
        {
            waypoint = index >= 0 && index < WaypointCount ? waypoints[index] : null;
            return waypoint?.Target != null && IsFinite(waypoint.Target.position);
        }

        internal bool IsValid(out string error)
        {
            if (WaypointCount < 2)
            {
                error = "通用巡航至少需要两个航点。";
                return false;
            }

            for (var index = 0; index < WaypointCount; index++)
            {
                if (!TryGetWaypoint(index, out _))
                {
                    error = $"第 {index + 1} 个巡航航点为空或坐标非法。";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        internal void Configure(DroneCruiseMode value, float speed, DroneCruiseWaypoint[] values)
        {
            mode = value;
            defaultSpeedMetersPerSecond = Mathf.Max(0.1f, speed);
            waypoints = values ?? Array.Empty<DroneCruiseWaypoint>();
        }

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
    }
}
