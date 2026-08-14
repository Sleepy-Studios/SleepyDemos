using UnityEngine;

namespace Hotfix.DroneFlight
{
    internal static class DroneEquipmentPhysicsMath
    {
        internal static Vector3 CalculateHarpoonImpulse(Vector3 direction, float projectileMass, float muzzleSpeed)
        {
            return direction.sqrMagnitude > 0.000001f && float.IsFinite(projectileMass)
                   && float.IsFinite(muzzleSpeed) && projectileMass > 0f && muzzleSpeed > 0f
                ? direction.normalized * (projectileMass * muzzleSpeed)
                : Vector3.zero;
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
