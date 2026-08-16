using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>通用航点自动驾驶将位置误差转换为飞控输入的参数。</summary>
    [CreateAssetMenu(fileName = "DroneAutopilotConfig", menuName = "SleepyDemos/Drone Flight/Autopilot Config")]
    public sealed class DroneAutopilotConfig : ScriptableObject
    {
        [SerializeField, Min(0.1f), InspectorName("最大指令速度 (米/秒)")]
        [Tooltip("自动驾驶允许提交给飞控的最大水平速度。不会绕过飞行档位和动力限制。")]
        private float maximumCommandSpeed = 5f;

        [SerializeField, Min(0.1f), InspectorName("水平位置跟随增益")]
        [Tooltip("位置误差转换为期望速度的比例。过大可能导致目标点附近往复修正。")]
        private float horizontalPositionGain = 0.9f;

        [SerializeField, Min(1f), InspectorName("满偏航输入角差 (度)")]
        [Tooltip("目标朝向相差多少度时输出完整偏航指令。")]
        private float yawFullInputAngle = 55f;

        [SerializeField, Min(0.05f), InspectorName("水平到点容差 (米)")]
        [Tooltip("水平距离小于此值时才可能判定抵达航点。")]
        private float horizontalArrivalTolerance = 0.6f;

        [SerializeField, Min(0.05f), InspectorName("垂直到点容差 (米)")]
        [Tooltip("高度误差小于此值时才可能判定抵达航点。")]
        private float verticalArrivalTolerance = 0.35f;

        [SerializeField, Min(0.05f), InspectorName("到点速度容差 (米/秒)")]
        [Tooltip("无人机速度低于此值后才判定稳定抵达，避免高速掠过航点。")]
        private float arrivalSpeedTolerance = 0.9f;

        public float MaximumCommandSpeed => maximumCommandSpeed;
        public float HorizontalPositionGain => horizontalPositionGain;
        public float YawFullInputAngle => yawFullInputAngle;
        public float HorizontalArrivalTolerance => horizontalArrivalTolerance;
        public float VerticalArrivalTolerance => verticalArrivalTolerance;
        public float ArrivalSpeedTolerance => arrivalSpeedTolerance;
    }
}
