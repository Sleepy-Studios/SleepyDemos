using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>把地面支持力变化转换为飞控承载比例。</summary>
    internal sealed class DronePayloadLoadTransferEstimator
    {
        internal float SupportedFraction { get; private set; }

        internal DronePayloadSupportState State { get; private set; }

        internal void Reset()
        {
            SupportedFraction = 0f;
            State = DronePayloadSupportState.None;
        }

        internal float Step(
            float payloadMassKilograms,
            bool supportStateConfirmed,
            bool isGroundSupported,
            float upwardSupportForceNewtons,
            float blendSeconds,
            float deltaTime)
        {
            if (!IsPositiveFinite(payloadMassKilograms)
                || !IsPositiveFinite(deltaTime)
                || !float.IsFinite(upwardSupportForceNewtons))
            {
                return SupportedFraction;
            }

            var weight = payloadMassKilograms * Mathf.Max(0.001f, Mathf.Abs(Physics.gravity.y));
            var externallySupported = !supportStateConfirmed || isGroundSupported;
            var targetFraction = externallySupported
                ? 1f - Mathf.Clamp01(Mathf.Max(0f, upwardSupportForceNewtons) / weight)
                : 1f;
            var safeBlendSeconds = Mathf.Max(0.01f, blendSeconds);
            var alpha = 1f - Mathf.Exp(-deltaTime / safeBlendSeconds);
            var previous = SupportedFraction;
            SupportedFraction = Mathf.Lerp(SupportedFraction, targetFraction, alpha);
            if (Mathf.Abs(SupportedFraction - targetFraction) < 0.001f)
            {
                SupportedFraction = targetFraction;
            }

            if (!externallySupported)
            {
                State = SupportedFraction >= 0.999f
                    ? DronePayloadSupportState.AirborneSupported
                    : DronePayloadSupportState.TakingLoad;
            }
            else if (targetFraction > previous + 0.001f)
            {
                State = DronePayloadSupportState.TakingLoad;
            }
            else if (SupportedFraction > targetFraction + 0.001f)
            {
                State = DronePayloadSupportState.Unloading;
            }
            else
            {
                State = DronePayloadSupportState.GroundSupported;
            }

            return SupportedFraction;
        }

        private static bool IsPositiveFinite(float value)
        {
            return float.IsFinite(value) && value > 0f;
        }
    }
}
