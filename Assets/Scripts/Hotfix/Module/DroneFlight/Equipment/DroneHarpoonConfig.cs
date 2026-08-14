using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>渔叉发射器、弹体和柔性绳索独立配置。</summary>
    [CreateAssetMenu(fileName = "DroneHarpoonConfig", menuName = "SleepyDemos/Drone Flight/Harpoon Config")]
    public sealed class DroneHarpoonConfig : ScriptableObject
    {
        [Header("Launcher")]
        [SerializeField] private float hardwareMassKilograms = 0.05f;
        [SerializeField] private float projectileMassKilograms = 0.02f;
        [SerializeField] private float muzzleSpeedMetersPerSecond = 18f;
        [SerializeField] private float maximumFlightDistanceMeters = 30f;
        [SerializeField] private float gimbalYawLimitDegrees = 55f;
        [SerializeField] private float gimbalPitchUpLimitDegrees = 25f;
        [SerializeField] private float gimbalPitchDownLimitDegrees = 60f;
        [SerializeField] private float allowedAimErrorDegrees = 2.5f;

        [Header("Rope")]
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

        [Header("Hit Rules")]
        [SerializeField] private LayerMask hittableLayers = ~0;
        [SerializeField] private LayerMask ignoredLayers;

        internal float HardwareMassKilograms => hardwareMassKilograms;
        internal float ProjectileMassKilograms => projectileMassKilograms;
        internal float MuzzleSpeedMetersPerSecond => muzzleSpeedMetersPerSecond;
        internal float MaximumFlightDistanceMeters => maximumFlightDistanceMeters;
        internal float GimbalYawLimitDegrees => gimbalYawLimitDegrees;
        internal float GimbalPitchUpLimitDegrees => gimbalPitchUpLimitDegrees;
        internal float GimbalPitchDownLimitDegrees => gimbalPitchDownLimitDegrees;
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
            if (!IsPositive(hardwareMassKilograms) || !IsPositive(projectileMassKilograms)
                || projectileMassKilograms >= hardwareMassKilograms)
            {
                diagnostic = "渔叉设备质量必须为正，且弹体质量必须小于设备总质量。";
                return false;
            }

            if (!IsPositive(muzzleSpeedMetersPerSecond) || !IsPositive(maximumFlightDistanceMeters)
                || !IsPositive(minimumRopeLengthMeters) || maximumRopeLengthMeters <= minimumRopeLengthMeters)
            {
                diagnostic = "渔叉速度、距离或绳长范围无效。";
                return false;
            }

            if (!IsPositive(ropeSpringNewtonsPerMeter) || !IsPositive(ropeDamperNewtonSecondsPerMeter)
                || !IsPositive(maximumTensionNewtons) || ropeBreakForceNewtons <= maximumTensionNewtons
                || !IsPositive(automaticRecoverySpeedMetersPerSecond))
            {
                diagnostic = "绳索弹簧、阻尼、张力或回收参数无效。";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool IsPositive(float value) => float.IsFinite(value) && value > 0f;
    }
}
