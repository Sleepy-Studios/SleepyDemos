using UnityEngine;

namespace Hotfix.DroneFlight
{
    internal static class DroneEquipmentPhysicsMath
    {
        internal static Vector3 CalculateHarpoonImpulse(Vector3 direction, float impulseNewtonSeconds)
        {
            return direction.sqrMagnitude > 0.000001f && float.IsFinite(impulseNewtonSeconds)
                   && impulseNewtonSeconds > 0f
                ? direction.normalized * impulseNewtonSeconds
                : Vector3.zero;
        }

        internal static bool TryCalculateBallisticDirection(
            Vector3 origin,
            Vector3 target,
            float launchSpeed,
            Vector3 gravity,
            out Vector3 direction)
        {
            direction = Vector3.zero;
            var gravityMagnitude = gravity.magnitude;
            if (!float.IsFinite(launchSpeed) || launchSpeed <= 0f
                || !float.IsFinite(gravityMagnitude) || gravityMagnitude <= 0.0001f)
            {
                return false;
            }

            var up = -gravity / gravityMagnitude;
            var displacement = target - origin;
            var verticalDistance = Vector3.Dot(displacement, up);
            var horizontalDisplacement = displacement - up * verticalDistance;
            var horizontalDistance = horizontalDisplacement.magnitude;
            if (horizontalDistance <= 0.0001f)
            {
                direction = verticalDistance >= 0f ? up : -up;
                return verticalDistance <= 0f
                       || launchSpeed * launchSpeed >= 2f * gravityMagnitude * verticalDistance;
            }

            var speedSquared = launchSpeed * launchSpeed;
            var discriminant = speedSquared * speedSquared
                               - gravityMagnitude * (gravityMagnitude * horizontalDistance * horizontalDistance
                                                     + 2f * verticalDistance * speedSquared);
            if (!float.IsFinite(discriminant) || discriminant < 0f)
            {
                return false;
            }

            var tangent = (speedSquared - Mathf.Sqrt(discriminant))
                          / (gravityMagnitude * horizontalDistance);
            direction = (horizontalDisplacement.normalized + up * tangent).normalized;
            return direction.sqrMagnitude > 0.0001f;
        }

        internal static bool IsWithinHarpoonAimEnvelope(
            Vector3 bodyPosition,
            Vector3 launchPosition,
            Vector3 bodyDown,
            Vector3 target,
            float maximumHorizontalRadius,
            float maximumConeDegrees)
        {
            var horizontal = Vector3.ProjectOnPlane(target - bodyPosition, Vector3.up).magnitude;
            var direction = target - launchPosition;
            return direction.sqrMagnitude > 0.000001f
                   && horizontal <= Mathf.Max(0f, maximumHorizontalRadius)
                   && Vector3.Angle(bodyDown, direction) <= Mathf.Max(0f, maximumConeDegrees);
        }

        internal static float CalculateTension(
            float distance,
            float targetLength,
            float separatingSpeed,
            float spring,
            float damper,
            float maximumTension)
        {
            return Mathf.Min(
                CalculateRawTension(distance, targetLength, separatingSpeed, spring, damper),
                Mathf.Max(0f, maximumTension));
        }

        internal static float CalculateRawTension(
            float distance,
            float targetLength,
            float separatingSpeed,
            float spring,
            float damper)
        {
            if (distance <= targetLength || targetLength < 0f)
            {
                return 0f;
            }

            return Mathf.Max(
                0f,
                (distance - targetLength) * spring + Mathf.Max(0f, separatingSpeed) * damper);
        }

        internal static float CalculateSupportedMass(float downwardTension, float gravity, float payloadMass)
        {
            return Mathf.Clamp(downwardTension / Mathf.Max(0.01f, gravity), 0f, Mathf.Max(0f, payloadMass));
        }
    }
}
