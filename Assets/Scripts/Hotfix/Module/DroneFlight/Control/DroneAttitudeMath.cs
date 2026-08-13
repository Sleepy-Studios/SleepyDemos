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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
