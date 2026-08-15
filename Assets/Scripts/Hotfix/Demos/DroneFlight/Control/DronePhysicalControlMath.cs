using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>推力方向、姿态误差和惯性力矩的纯物理数学。</summary>
    internal static class DronePhysicalControlMath
    {
        internal static Vector3 LimitForceByTilt(Vector3 desiredForce, float maximumTiltDegrees)
        {
            if (!IsFinite(desiredForce) || desiredForce.y <= 0.0001f)
            {
                return Vector3.up * Mathf.Max(0f, desiredForce.y);
            }

            var maximumHorizontal = desiredForce.y
                                    * Mathf.Tan(Mathf.Clamp(maximumTiltDegrees, 0f, 80f) * Mathf.Deg2Rad);
            var horizontal = Vector3.ClampMagnitude(
                new Vector3(desiredForce.x, 0f, desiredForce.z),
                maximumHorizontal);
            return new Vector3(horizontal.x, desiredForce.y, horizontal.z);
        }

        internal static Quaternion BuildAttitudeFromForce(Vector3 desiredForce, float yawDegrees)
        {
            var desiredUp = desiredForce.sqrMagnitude > 0.000001f
                ? desiredForce.normalized
                : Vector3.up;
            var yawForward = Quaternion.Euler(0f, yawDegrees, 0f) * Vector3.forward;
            var desiredForward = Vector3.ProjectOnPlane(yawForward, desiredUp);
            if (desiredForward.sqrMagnitude < 0.000001f)
            {
                desiredForward = Vector3.ProjectOnPlane(Vector3.forward, desiredUp);
            }

            return Quaternion.LookRotation(desiredForward.normalized, desiredUp);
        }

        internal static Vector3 CalculateReducedAttitudeRate(
            Quaternion current,
            Vector3 desiredUp,
            float targetYawDegrees,
            float tiltGain,
            float yawGain,
            float yawWeight,
            float maximumRate)
        {
            if (desiredUp.sqrMagnitude < 0.000001f)
            {
                desiredUp = Vector3.up;
            }

            var tiltDelta = Quaternion.FromToRotation(current * Vector3.up, desiredUp.normalized);
            var tiltTarget = tiltDelta * current;
            var tiltRate = DroneAttitudeMath.CalculateTargetRate(current, tiltTarget, tiltGain, maximumRate);
            var actualYaw = current.eulerAngles.y;
            var yawRate = Mathf.Clamp(
                Mathf.DeltaAngle(actualYaw, targetYawDegrees) * Mathf.Deg2Rad * yawGain * Mathf.Clamp01(yawWeight),
                -maximumRate,
                maximumRate);
            return new Vector3(tiltRate.x, yawRate, tiltRate.z);
        }

        internal static Vector3 CalculateLocalTorque(
            Vector3 desiredLocalAngularAcceleration,
            Vector3 localAngularVelocity,
            Vector3 inertiaTensor,
            Quaternion inertiaTensorRotation)
        {
            if (!IsFinite(desiredLocalAngularAcceleration) || !IsFinite(localAngularVelocity)
                || !IsFinite(inertiaTensor))
            {
                return Vector3.zero;
            }

            var inverseRotation = Quaternion.Inverse(inertiaTensorRotation);
            var principalAcceleration = inverseRotation * desiredLocalAngularAcceleration;
            var principalVelocity = inverseRotation * localAngularVelocity;
            var angularMomentum = Vector3.Scale(inertiaTensor, principalVelocity);
            var principalTorque = Vector3.Scale(inertiaTensor, principalAcceleration)
                                  + Vector3.Cross(principalVelocity, angularMomentum);
            return inertiaTensorRotation * principalTorque;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
