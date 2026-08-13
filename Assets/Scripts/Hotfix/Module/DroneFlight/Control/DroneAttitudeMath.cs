using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// 四元数姿态误差到机体局部旋转向量的纯数学转换。
    /// </summary>
    internal static class DroneAttitudeMath
    {
        internal static Vector3 CalculateLocalRotationVector(Quaternion current, Quaternion target)
        {
            var localDelta = Quaternion.Inverse(current) * target;
            if (localDelta.w < 0f)
            {
                localDelta = new Quaternion(-localDelta.x, -localDelta.y, -localDelta.z, -localDelta.w);
            }

            localDelta.ToAngleAxis(out var angleDegrees, out var axis);
            if (float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z) || angleDegrees < 0.0001f)
            {
                return Vector3.zero;
            }

            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            return axis.normalized * (angleDegrees * Mathf.Deg2Rad);
        }

        internal static Vector3 CalculateTargetRate(
            Quaternion current,
            Quaternion target,
            float gain,
            float maximumRate)
        {
            if (!IsFinite(gain) || !IsFinite(maximumRate) || maximumRate <= 0f)
            {
                return Vector3.zero;
            }

            return Vector3.ClampMagnitude(CalculateLocalRotationVector(current, target) * gain, maximumRate);
        }

        internal static float AdvanceBoundedYawTarget(
            float targetYawDegrees,
            float actualYawDegrees,
            float commandedDeltaDegrees,
            float maximumLeadDegrees)
        {
            if (!IsFinite(targetYawDegrees) || !IsFinite(actualYawDegrees)
                || !IsFinite(commandedDeltaDegrees) || !IsFinite(maximumLeadDegrees)
                || maximumLeadDegrees <= 0f)
            {
                return actualYawDegrees;
            }

            var advancedTarget = targetYawDegrees + commandedDeltaDegrees;
            var boundedLead = Mathf.Clamp(
                Mathf.DeltaAngle(actualYawDegrees, advancedTarget),
                -maximumLeadDegrees,
                maximumLeadDegrees);
            return actualYawDegrees + boundedLead;
        }

        internal static Vector3 CalculateHeadingRelativeWorldVelocity(
            Vector2 horizontalInput,
            float actualYawDegrees,
            float maximumSpeed)
        {
            if (!float.IsFinite(horizontalInput.x) || !float.IsFinite(horizontalInput.y)
                || !IsFinite(actualYawDegrees) || !IsFinite(maximumSpeed) || maximumSpeed <= 0f)
            {
                return Vector3.zero;
            }

            return Quaternion.Euler(0f, actualYawDegrees, 0f)
                   * new Vector3(horizontalInput.x, 0f, horizontalInput.y)
                   * maximumSpeed;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
