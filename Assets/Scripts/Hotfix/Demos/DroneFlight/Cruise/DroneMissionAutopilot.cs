using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>把世界空间位置目标转换为现有飞控归一化输入，不直接移动刚体。</summary>
    public sealed class DroneMissionAutopilot : MonoBehaviour
    {
        [SerializeField, InspectorName("自动驾驶配置")]
        [Tooltip("集中管理速度、位置增益、偏航响应和到点容差。")]
        private DroneAutopilotConfig config;

        private DroneFlightController controller;
        private Rigidbody body;
        private Vector3 targetPosition;
        private Vector3 targetForward = Vector3.forward;
        private float requestedSpeed = 4f;
        private bool hasTarget;

        /// 当前刚体是否进入目标位置和速度容差。
        internal bool HasArrived
        {
            get
            {
                if (!hasTarget || body == null)
                {
                    return false;
                }

                var delta = targetPosition - body.position;
                var horizontal = Vector3.ProjectOnPlane(delta, Vector3.up).magnitude;
                return horizontal <= HorizontalArrivalTolerance
                       && Mathf.Abs(delta.y) <= VerticalArrivalTolerance
                       && body.linearVelocity.magnitude <= ArrivalSpeedTolerance;
            }
        }

        /// 当前无人机到轨迹目标的世界空间距离。
        internal float TrackingError => body != null && hasTarget
            ? Vector3.Distance(body.position, targetPosition)
            : float.PositiveInfinity;

        private void FixedUpdate()
        {
            if (!hasTarget || controller == null || body == null)
            {
                return;
            }

            controller.SetTargetHeight(targetPosition.y);
            var error = Vector3.ProjectOnPlane(targetPosition - body.position, Vector3.up);
            var desiredWorldVelocity = Vector3.ClampMagnitude(
                error * HorizontalPositionGain,
                Mathf.Min(MaximumCommandSpeed, requestedSpeed));
            var yaw = body.rotation.eulerAngles.y;
            var localVelocity = Quaternion.Inverse(Quaternion.Euler(0f, yaw, 0f)) * desiredWorldVelocity;
            var speedScale = Mathf.Max(0.1f, MaximumCommandSpeed);
            var desiredYaw = targetForward.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(targetForward.x, targetForward.z) * Mathf.Rad2Deg
                : yaw;
            var yawInput = Mathf.Clamp(Mathf.DeltaAngle(yaw, desiredYaw) / YawFullInputAngle, -1f, 1f);
            controller.SetControlInput(DroneControlInput.Create(
                0f,
                yawInput,
                localVelocity.z / speedScale,
                localVelocity.x / speedScale));
        }

        internal void Configure(
            DroneFlightController flightController,
            Rigidbody droneBody,
            DroneAutopilotConfig autopilotConfig = null)
        {
            controller = flightController;
            body = droneBody;
            if (autopilotConfig != null)
            {
                config = autopilotConfig;
            }
        }

        internal void SetTarget(Vector3 position, Vector3 forward, float speed)
        {
            if (!IsFinite(position) || !IsFinite(forward) || !float.IsFinite(speed))
            {
                return;
            }

            targetPosition = position;
            if (forward.sqrMagnitude > 0.0001f)
            {
                targetForward = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            }

            requestedSpeed = Mathf.Max(0.1f, speed);
            hasTarget = true;
        }

        internal void StopAtCurrentPosition()
        {
            if (body == null)
            {
                return;
            }

            targetPosition = body.position;
            targetForward = body.transform.forward;
            requestedSpeed = 0.1f;
            hasTarget = true;
        }

        internal static DroneControlInput CalculateInput(
            Vector3 bodyPosition,
            float bodyYawDegrees,
            Vector3 worldTarget,
            Vector3 targetForward,
            float positionGain,
            float maximumSpeed,
            float yawFullAngle)
        {
            var error = Vector3.ProjectOnPlane(worldTarget - bodyPosition, Vector3.up);
            var desiredVelocity = Vector3.ClampMagnitude(error * positionGain, maximumSpeed);
            var localVelocity = Quaternion.Inverse(Quaternion.Euler(0f, bodyYawDegrees, 0f)) * desiredVelocity;
            var desiredYaw = targetForward.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(targetForward.x, targetForward.z) * Mathf.Rad2Deg
                : bodyYawDegrees;
            return DroneControlInput.Create(
                0f,
                Mathf.Clamp(Mathf.DeltaAngle(bodyYawDegrees, desiredYaw) / Mathf.Max(1f, yawFullAngle), -1f, 1f),
                localVelocity.z / Mathf.Max(0.1f, maximumSpeed),
                localVelocity.x / Mathf.Max(0.1f, maximumSpeed));
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private float MaximumCommandSpeed => config != null ? config.MaximumCommandSpeed : 5f;
        private float HorizontalPositionGain => config != null ? config.HorizontalPositionGain : 0.9f;
        private float YawFullInputAngle => config != null ? config.YawFullInputAngle : 55f;
        private float HorizontalArrivalTolerance => config != null ? config.HorizontalArrivalTolerance : 0.6f;
        private float VerticalArrivalTolerance => config != null ? config.VerticalArrivalTolerance : 0.35f;
        private float ArrivalSpeedTolerance => config != null ? config.ArrivalSpeedTolerance : 0.9f;
    }
}
