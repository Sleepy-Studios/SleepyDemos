using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>渔叉发射器、弹体和柔性绳索独立配置。</summary>
    [CreateAssetMenu(fileName = "DroneHarpoonConfig", menuName = "SleepyDemos/Drone Flight/Harpoon Config")]
    public sealed class DroneHarpoonConfig : ScriptableObject
    {
        [SerializeField] private float hardwareMassKilograms = 0.05f;
        [SerializeField] private float projectileMassKilograms = 0.02f;
        [SerializeField] private float launchImpulseNewtonSeconds = 0.12f;
        [SerializeField] private float maximumFlightDistanceMeters = 30f;
        [SerializeField] private float maximumAimRadiusMeters = 3f;
        [SerializeField] private float maximumAimConeDegrees = 25f;
        [SerializeField] private float allowedAimErrorDegrees = 2.5f;

        [SerializeField] private float minimumRopeLengthMeters = 0.25f;
        [SerializeField] private float maximumRopeLengthMeters = 30f;
        [SerializeField] private float reelSpeedMetersPerSecond = 3f;
        [SerializeField] private float ropeSpringNewtonsPerMeter = 90f;
        [SerializeField] private float ropeDamperNewtonSecondsPerMeter = 12f;
        [SerializeField] private float maximumTensionNewtons = 180f;
        [SerializeField] private float ropeBreakForceNewtons = 240f;
        [SerializeField] private float automaticRecoverySpeedMetersPerSecond = 6f;
        [SerializeField] private float dockPositionToleranceMeters = 0.08f;
        [SerializeField] private float dockSpeedToleranceMetersPerSecond = 0.8f;

        [SerializeField] private LayerMask hittableLayers = ~0;
        [SerializeField] private LayerMask ignoredLayers;

        internal float HardwareMassKilograms => hardwareMassKilograms;
        internal float ProjectileMassKilograms => projectileMassKilograms;
        internal float LaunchImpulseNewtonSeconds => launchImpulseNewtonSeconds;
        internal float MaximumFlightDistanceMeters => maximumFlightDistanceMeters;
        internal float MaximumAimRadiusMeters => maximumAimRadiusMeters;
        internal float MaximumAimConeDegrees => maximumAimConeDegrees;
        internal float AllowedAimErrorDegrees => allowedAimErrorDegrees;
        internal float MinimumRopeLengthMeters => minimumRopeLengthMeters;
        internal float MaximumRopeLengthMeters => maximumRopeLengthMeters;
        internal float ReelSpeedMetersPerSecond => reelSpeedMetersPerSecond;
        internal float RopeSpringNewtonsPerMeter => ropeSpringNewtonsPerMeter;
        internal float RopeDamperNewtonSecondsPerMeter => ropeDamperNewtonSecondsPerMeter;
        internal float MaximumTensionNewtons => maximumTensionNewtons;
        internal float RopeBreakForceNewtons => ropeBreakForceNewtons;
        internal float AutomaticRecoverySpeedMetersPerSecond => automaticRecoverySpeedMetersPerSecond;
        internal float DockPositionToleranceMeters => dockPositionToleranceMeters;
        internal float DockSpeedToleranceMetersPerSecond => dockSpeedToleranceMetersPerSecond;
        internal LayerMask HittableLayers => hittableLayers;
        internal LayerMask IgnoredLayers => ignoredLayers;

        internal bool TryValidate(out string diagnostic)
        {
            var result = Validate();
            diagnostic = result.ChineseMessage;
            return result.IsValid;
        }

        /// 返回供运行时与双语 Inspector 共用的结构化校验结果。
        internal DroneConfigValidationResult Validate()
        {
            if (!IsPositive(hardwareMassKilograms) || !IsPositive(projectileMassKilograms)
                || projectileMassKilograms >= hardwareMassKilograms)
            {
                return DroneConfigValidationResult.Invalid(
                    "渔叉设备质量必须为正，且弹体质量必须小于设备总质量。",
                    "Harpoon hardware mass must be positive and projectile mass must be lower than total hardware mass.");
            }

            if (!IsPositive(launchImpulseNewtonSeconds) || !IsPositive(maximumFlightDistanceMeters)
                || !IsPositive(minimumRopeLengthMeters) || maximumRopeLengthMeters <= minimumRopeLengthMeters)
            {
                return DroneConfigValidationResult.Invalid(
                    "渔叉发射冲量、距离或绳长范围无效。",
                    "Harpoon launch impulse, flight distance, or rope-length range is invalid.");
            }

            if (!IsPositive(ropeSpringNewtonsPerMeter) || !IsPositive(ropeDamperNewtonSecondsPerMeter)
                || !IsPositive(maximumTensionNewtons) || ropeBreakForceNewtons <= maximumTensionNewtons
                || !IsPositive(automaticRecoverySpeedMetersPerSecond))
            {
                return DroneConfigValidationResult.Invalid(
                    "绳索弹簧、阻尼、张力或回收参数无效。",
                    "Rope spring, damping, tension, or recovery parameters are invalid.");
            }

            if (!IsPositive(maximumAimRadiusMeters)
                || !float.IsFinite(maximumAimConeDegrees) || maximumAimConeDegrees <= 0f
                || maximumAimConeDegrees > 60f)
            {
                return DroneConfigValidationResult.Invalid(
                    "渔叉瞄准半径或向下圆锥角无效。",
                    "Harpoon aim radius or downward cone angle is invalid.");
            }

            return DroneConfigValidationResult.Valid;
        }

        private static bool IsPositive(float value) => float.IsFinite(value) && value > 0f;
    }
}
