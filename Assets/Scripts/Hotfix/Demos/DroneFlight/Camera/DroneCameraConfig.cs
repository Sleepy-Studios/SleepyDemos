using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>无人机运行镜头的统一可编辑参数。</summary>
    [CreateAssetMenu(fileName = "DroneCameraConfig", menuName = "SleepyDemos/Drone Flight/Camera Config")]
    public sealed class DroneCameraConfig : ScriptableObject
    {
        [Header("第三人称与环绕")]
        [SerializeField, InspectorName("第三人称相机偏移")]
        [Tooltip("相对无人机朝向的相机位置，单位为米。Z 为负数表示位于机尾后方。")]
        private Vector3 thirdPersonOffset = new(0f, 0.85f, -2.2f);

        [SerializeField, Min(0f), InspectorName("速度前视时间 (秒)")]
        [Tooltip("根据当前速度向前预测观察点的时间。数值过大会让镜头明显领先机体。")]
        private float thirdPersonLookAheadSeconds = 0.18f;

        [SerializeField, Min(0.1f), InspectorName("环绕距离 (米)")]
        [Tooltip("自由环绕视角与无人机焦点之间的距离。")]
        private float orbitDistanceMeters = 2.5f;

        [Header("平滑与切换")]
        [SerializeField, Min(0.01f), InspectorName("视角切换时长 (秒)")]
        [Tooltip("不同镜头模式之间完成位置、旋转和视场角过渡所需时间。")]
        private float transitionSeconds = 0.35f;

        [SerializeField, Min(0.01f), InspectorName("跟随平滑时间 (秒)")]
        [Tooltip("第三人称与环绕视角的位置平滑时间。越大越柔和，但跟随延迟也越明显。")]
        private float followSmoothTimeSeconds = 0.16f;

        [SerializeField, Min(0.01f), InspectorName("旋转响应速度")]
        [Tooltip("镜头旋转追随目标方向的指数响应速度。")]
        private float rotationSharpness = 12f;

        [Header("相机防穿模")]
        [SerializeField, Min(0.01f), InspectorName("碰撞探测半径 (米)")]
        [Tooltip("第三人称相机防穿模球形探测的半径。")]
        private float collisionRadiusMeters = 0.18f;

        [SerializeField, Min(0.01f), InspectorName("碰撞最小距离 (米)")]
        [Tooltip("发生遮挡时相机仍需与无人机焦点保持的最小距离。")]
        private float collisionMinimumDistanceMeters = 0.55f;

        [SerializeField, Min(0f), InspectorName("碰撞安全余量 (米)")]
        [Tooltip("相机从碰撞表面额外向焦点收回的距离。")]
        private float collisionBufferMeters = 0.1f;

        [Header("云台与视场角")]
        [SerializeField, Range(-90f, 30f), InspectorName("云台最低俯仰角 (度)")]
        [Tooltip("云台可向下转动的极限角度。")]
        private float gimbalPitchMinimum = -90f;

        [SerializeField, Range(-90f, 90f), InspectorName("云台最高俯仰角 (度)")]
        [Tooltip("云台可向上转动的极限角度。")]
        private float gimbalPitchMaximum = 30f;

        [SerializeField, Range(10f, 120f), InspectorName("最小视场角 (度)")]
        [Tooltip("允许缩放到的最窄视野。数值越小，画面放大越明显。")]
        private float minimumFieldOfView = 20f;

        [SerializeField, Range(10f, 120f), InspectorName("最大视场角 (度)")]
        [Tooltip("允许缩放到的最宽视野。")]
        private float maximumFieldOfView = 80f;

        public Vector3 ThirdPersonOffset => thirdPersonOffset;
        public float ThirdPersonLookAheadSeconds => thirdPersonLookAheadSeconds;
        public float OrbitDistanceMeters => orbitDistanceMeters;
        public float TransitionSeconds => transitionSeconds;
        public float FollowSmoothTimeSeconds => followSmoothTimeSeconds;
        public float RotationSharpness => rotationSharpness;
        public float CollisionRadiusMeters => collisionRadiusMeters;
        public float CollisionMinimumDistanceMeters => collisionMinimumDistanceMeters;
        public float CollisionBufferMeters => collisionBufferMeters;
        public float GimbalPitchMinimum => gimbalPitchMinimum;
        public float GimbalPitchMaximum => gimbalPitchMaximum;
        public float MinimumFieldOfView => minimumFieldOfView;
        public float MaximumFieldOfView => maximumFieldOfView;
    }
}
