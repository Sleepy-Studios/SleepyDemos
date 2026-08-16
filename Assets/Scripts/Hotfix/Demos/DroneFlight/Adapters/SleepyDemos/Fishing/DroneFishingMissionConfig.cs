using UnityEngine;

namespace Hotfix.DroneFlight.Adapters.SleepyDemos
{
    /// <summary>SleepyDemos 捕鱼演出的任务与固定机位参数。</summary>
    [CreateAssetMenu(fileName = "DroneFishingMissionConfig", menuName = "SleepyDemos/Drone Flight/Fishing Mission Config")]
    public sealed class DroneFishingMissionConfig : ScriptableObject
    {
        [Header("鱼与路线")]
        [SerializeField, InspectorName("鱼随机区域半尺寸")]
        [Tooltip("鱼相对任务根节点的 X/Z 随机范围，单位米。")]
        private Vector2 fishAreaHalfExtents = new(3f, 3f);

        [SerializeField, InspectorName("鱼所在深度 (米)")]
        [Tooltip("鱼生成点的局部 Y 坐标。负值表示水面以下。")]
        private float fishDepthMeters = -5f;

        [SerializeField, Range(1, 2), InspectorName("环绕圈数")]
        [Tooltip("进入俯冲前围绕目标飞行的完整圈数。")]
        private int orbitLoops = 2;

        [SerializeField, Min(0.1f), InspectorName("演出路线速度 (米/秒)")]
        [Tooltip("入场、环绕和俯冲沿贝塞尔路线推进的速度。")]
        private float routeSpeedMetersPerSecond = 4f;

        [SerializeField, Min(0.1f), InspectorName("返航速度 (米/秒)")]
        [Tooltip("渔叉命中并携带载荷后飞向返航点的速度。")]
        private float returnSpeedMetersPerSecond = 3f;

        [Header("容错与超时")]
        [SerializeField, Min(0.5f), InspectorName("最大路线跟踪误差 (米)")]
        [Tooltip("误差超过该值时暂停路径进度，等待真实飞控追上目标。")]
        private float maximumTrackingErrorMeters = 2.5f;

        [SerializeField, Min(1f), InspectorName("路线阶段超时 (秒)")]
        [Tooltip("任一飞行阶段超过此时间仍未完成则进入失败。")]
        private float routePhaseTimeoutSeconds = 60f;

        [SerializeField, Min(1f), InspectorName("发射阶段超时 (秒)")]
        [Tooltip("进入瞄准后等待自动渔叉命中的最长时间。")]
        private float firingTimeoutSeconds = 10f;

        [Header("固定机位")]
        [SerializeField, Range(10f, 80f), InspectorName("追踪视场角 (度)")]
        [Tooltip("固定机位跟拍时逐渐收窄到的视场角。")]
        private float trackingFieldOfView = 35f;

        [SerializeField, Min(0.1f), InspectorName("追踪旋转响应")]
        [Tooltip("固定机位转向无人机的平滑响应速度。")]
        private float cameraRotationSharpness = 3.5f;

        [SerializeField, Min(0.1f), InspectorName("视场角响应")]
        [Tooltip("固定机位视场角变化的平滑响应速度。")]
        private float fieldOfViewSharpness = 2.5f;

        public Vector2 FishAreaHalfExtents => fishAreaHalfExtents;
        public float FishDepthMeters => fishDepthMeters;
        public int OrbitLoops => orbitLoops;
        public float RouteSpeedMetersPerSecond => routeSpeedMetersPerSecond;
        public float ReturnSpeedMetersPerSecond => returnSpeedMetersPerSecond;
        public float MaximumTrackingErrorMeters => maximumTrackingErrorMeters;
        public float RoutePhaseTimeoutSeconds => routePhaseTimeoutSeconds;
        public float FiringTimeoutSeconds => firingTimeoutSeconds;
        public float TrackingFieldOfView => trackingFieldOfView;
        public float CameraRotationSharpness => cameraRotationSharpness;
        public float FieldOfViewSharpness => fieldOfViewSharpness;
    }
}
