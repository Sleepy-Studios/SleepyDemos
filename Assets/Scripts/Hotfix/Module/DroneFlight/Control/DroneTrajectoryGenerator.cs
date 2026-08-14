using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>固定步速度、加速度和 Jerk 连续的玩家轨迹目标生成器。</summary>
    internal sealed class DroneTrajectoryGenerator
    {
        private Vector3 shapedVelocity;
        private Vector3 shapedAcceleration;
        private float shapedYawRateRadians;
        private float shapedYawAccelerationRadians;
        private float targetYawDegrees;

        internal DroneTrajectorySetpoint Step(
            DroneControlInput input,
            float actualYawDegrees,
            DroneResponseProfileParameters profile,
            float deltaTime)
        {
            if (!IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return new DroneTrajectorySetpoint(
                    shapedVelocity,
                    shapedAcceleration,
                    targetYawDegrees,
                    shapedYawRateRadians,
                    shapedYawAccelerationRadians);
            }

            var horizontalInput = Vector2.ClampMagnitude(new Vector2(input.Right, input.Forward), 1f);
            var horizontalTarget = DroneAttitudeMath.CalculateHeadingRelativeWorldVelocity(
                horizontalInput,
                actualYawDegrees,
                profile.MaximumHorizontalSpeed);
            var verticalTarget = input.Lift * profile.MaximumVerticalSpeed;
            var targetVelocity = new Vector3(horizontalTarget.x, verticalTarget, horizontalTarget.z);

            StepHorizontal(targetVelocity, profile, deltaTime);
            StepVertical(targetVelocity.y, profile, deltaTime);
            StepYaw(input.Yaw, actualYawDegrees, profile, deltaTime);

            return new DroneTrajectorySetpoint(
                shapedVelocity,
                shapedAcceleration,
                targetYawDegrees,
                shapedYawRateRadians,
                shapedYawAccelerationRadians);
        }

        internal void Reset(Vector3 currentVelocity, float yawDegrees)
        {
            shapedVelocity = IsFinite(currentVelocity) ? currentVelocity : Vector3.zero;
            shapedAcceleration = Vector3.zero;
            shapedYawRateRadians = 0f;
            shapedYawAccelerationRadians = 0f;
            targetYawDegrees = float.IsFinite(yawDegrees) ? yawDegrees : 0f;
        }

        private void StepHorizontal(
            Vector3 targetVelocity,
            DroneResponseProfileParameters profile,
            float deltaTime)
        {
            var currentHorizontal = new Vector3(shapedVelocity.x, 0f, shapedVelocity.z);
            var targetHorizontal = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
            var desiredAcceleration = Vector3.ClampMagnitude(
                (targetHorizontal - currentHorizontal) / deltaTime,
                profile.MaximumHorizontalAcceleration);
            var currentAcceleration = new Vector3(shapedAcceleration.x, 0f, shapedAcceleration.z);
            currentAcceleration = Vector3.MoveTowards(
                currentAcceleration,
                desiredAcceleration,
                Mathf.Max(0.01f, profile.MaximumHorizontalJerk) * deltaTime);
            var nextVelocity = currentHorizontal + currentAcceleration * deltaTime;
            if (Vector3.Dot(targetHorizontal - currentHorizontal, targetHorizontal - nextVelocity) <= 0f)
            {
                nextVelocity = targetHorizontal;
            }

            shapedVelocity.x = nextVelocity.x;
            shapedVelocity.z = nextVelocity.z;
            shapedAcceleration.x = currentAcceleration.x;
            shapedAcceleration.z = currentAcceleration.z;
        }

        private void StepVertical(
            float targetVelocity,
            DroneResponseProfileParameters profile,
            float deltaTime)
        {
            var desiredAcceleration = Mathf.Clamp(
                (targetVelocity - shapedVelocity.y) / deltaTime,
                -profile.MaximumVerticalAcceleration,
                profile.MaximumVerticalAcceleration);
            shapedAcceleration.y = Mathf.MoveTowards(
                shapedAcceleration.y,
                desiredAcceleration,
                Mathf.Max(0.01f, profile.MaximumVerticalJerk) * deltaTime);
            var nextVelocity = shapedVelocity.y + shapedAcceleration.y * deltaTime;
            if ((targetVelocity - shapedVelocity.y) * (targetVelocity - nextVelocity) <= 0f)
            {
                nextVelocity = targetVelocity;
            }

            shapedVelocity.y = nextVelocity;
        }

        private void StepYaw(
            float yawInput,
            float actualYawDegrees,
            DroneResponseProfileParameters profile,
            float deltaTime)
        {
            var targetRate = yawInput * profile.MaximumYawSpeedDegrees * Mathf.Deg2Rad;
            var maximumAcceleration = profile.MaximumYawAccelerationDegrees * Mathf.Deg2Rad;
            var previousRate = shapedYawRateRadians;
            shapedYawRateRadians = Mathf.MoveTowards(
                shapedYawRateRadians,
                targetRate,
                maximumAcceleration * deltaTime);
            shapedYawAccelerationRadians = (shapedYawRateRadians - previousRate) / deltaTime;
            targetYawDegrees = DroneAttitudeMath.AdvanceBoundedYawTarget(
                targetYawDegrees,
                actualYawDegrees,
                shapedYawRateRadians * Mathf.Rad2Deg * deltaTime,
                60f);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
