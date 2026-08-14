using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>吊挂系统提供给飞控的低频运动状态。</summary>
    internal readonly struct DroneSuspensionState
    {
        internal DroneSuspensionState(
            bool isCableTaut,
            float supportedMassKilograms,
            float lengthMeters,
            Vector3 cableDirection,
            Vector3 relativeVelocity,
            float swingAngleDegrees = 0f,
            float swingRateDegreesPerSecond = 0f,
            float hardwareMassKilograms = 0f,
            float payloadMassKilograms = 0f,
            float supportedPayloadFraction = 0f,
            Vector3 centerOfMassWorldPosition = default)
        {
            IsCableTaut = isCableTaut;
            SupportedMassKilograms = supportedMassKilograms;
            LengthMeters = lengthMeters;
            CableDirection = cableDirection;
            RelativeVelocity = relativeVelocity;
            SwingAngleDegrees = swingAngleDegrees;
            SwingRateDegreesPerSecond = swingRateDegreesPerSecond;
            HardwareMassKilograms = hardwareMassKilograms;
            PayloadMassKilograms = payloadMassKilograms;
            SupportedPayloadFraction = supportedPayloadFraction;
            CenterOfMassWorldPosition = centerOfMassWorldPosition;
        }

        internal bool IsCableTaut { get; }
        internal bool IsActive => IsCableTaut;
        internal float SupportedMassKilograms { get; }
        internal float LengthMeters { get; }
        internal Vector3 CableDirection { get; }
        internal Vector3 RelativeVelocity { get; }
        internal float SwingAngleDegrees { get; }
        internal float SwingRateDegreesPerSecond { get; }
        internal float HardwareMassKilograms { get; }
        internal float PayloadMassKilograms { get; }
        internal float SupportedPayloadFraction { get; }
        internal Vector3 CenterOfMassWorldPosition { get; }
    }

    /// <summary>只通过目标加速度温和衰减吊载摆动，不直接修改任何刚体。</summary>
    internal static class DroneSuspendedLoadAssist
    {
        internal static Vector3 CalculateCorrection(
            DroneSuspensionState state,
            float strengthPercent,
            float configuredMaximumAcceleration,
            float profileMaximumAcceleration,
            bool isSport)
        {
            if (!state.IsCableTaut || state.LengthMeters < 0.03f
                || !IsFinite(state.CableDirection) || !IsFinite(state.RelativeVelocity))
            {
                return Vector3.zero;
            }

            var strength = Mathf.Clamp01(strengthPercent / 100f) * (isSport ? 0.5f : 1f);
            if (strength <= 0f)
            {
                return Vector3.zero;
            }

            var horizontalCable = new Vector3(state.CableDirection.x, 0f, state.CableDirection.z);
            var horizontalRelativeVelocity = new Vector3(
                state.RelativeVelocity.x,
                0f,
                state.RelativeVelocity.z);
            var naturalFrequency = Mathf.Sqrt(Mathf.Abs(Physics.gravity.y) / state.LengthMeters);
            var correction = strength * (
                Mathf.Abs(Physics.gravity.y) * horizontalCable
                + 2f * naturalFrequency * horizontalRelativeVelocity);
            var maximum = Mathf.Min(
                Mathf.Max(0f, configuredMaximumAcceleration),
                Mathf.Max(0f, profileMaximumAcceleration) * 0.25f);
            return Vector3.ClampMagnitude(correction, maximum);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }
    }
}
