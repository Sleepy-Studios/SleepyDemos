using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>四爪抓斗独立玩法与物理配置。</summary>
    [CreateAssetMenu(fileName = "DroneGrappleConfig", menuName = "SleepyDemos/Drone Flight/Grapple Config")]
    public sealed class DroneGrappleConfig : ScriptableObject
    {
        [Header("Mass And Travel")]
        [SerializeField] private float hardwareMassKilograms = 0.05f;
        [SerializeField] private float stowedDistanceMeters = 0.08f;
        [SerializeField] private float deployedDistanceMeters = 0.26f;
        [SerializeField] private float travelSpeedMetersPerSecond = 0.3f;
        [SerializeField] private float dockPositionToleranceMeters = 0.015f;
        [SerializeField] private float dockSpeedToleranceMetersPerSecond = 0.12f;

        [Header("Suspension")]
        [SerializeField] private float twistLimitDegrees = 25f;
        [SerializeField] private float swingLimitDegrees = 35f;
        [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.45f;
        [SerializeField] private float maximumDampingTorqueNewtonMeters = 3f;

        [Header("Claws")]
        [SerializeField] private float openAngleDegrees = 42f;
        [SerializeField] private float closedAngleDegrees = -18f;
        [SerializeField] private float clawSpring = 90f;
        [SerializeField] private float clawDamper = 10f;
        [SerializeField] private float clawMaximumForce = 80f;
        [SerializeField] private int stableContactSteps = 3;
        [SerializeField] private float enclosureRadiusMeters = 0.23f;
        [SerializeField] private float enclosureHalfHeightMeters = 0.2f;

        [Header("Assisted Grip")]
        [SerializeField] private float linearFreedomMeters = 0.02f;
        [SerializeField] private float constraintSpring = 220f;
        [SerializeField] private float constraintDamper = 24f;
        [SerializeField] private float breakForceNewtons = 180f;
        [SerializeField] private float breakTorqueNewtonMeters = 80f;
        [SerializeField] private float supportedLoadSmoothingSeconds = 0.18f;

        internal float HardwareMassKilograms => hardwareMassKilograms;
        internal float StowedDistanceMeters => stowedDistanceMeters;
        internal float DeployedDistanceMeters => deployedDistanceMeters;
        internal float TravelSpeedMetersPerSecond => travelSpeedMetersPerSecond;
        internal float DockPositionToleranceMeters => dockPositionToleranceMeters;
        internal float DockSpeedToleranceMetersPerSecond => dockSpeedToleranceMetersPerSecond;
        internal float TwistLimitDegrees => twistLimitDegrees;
        internal float SwingLimitDegrees => swingLimitDegrees;
        internal float DampingRatio => dampingRatio;
        internal float MaximumDampingTorqueNewtonMeters => maximumDampingTorqueNewtonMeters;
        internal float OpenAngleDegrees => openAngleDegrees;
        internal float ClosedAngleDegrees => closedAngleDegrees;
        internal float ClawSpring => clawSpring;
        internal float ClawDamper => clawDamper;
        internal float ClawMaximumForce => clawMaximumForce;
        internal int StableContactSteps => stableContactSteps;
        internal float EnclosureRadiusMeters => enclosureRadiusMeters;
        internal float EnclosureHalfHeightMeters => enclosureHalfHeightMeters;
        internal float LinearFreedomMeters => linearFreedomMeters;
        internal float ConstraintSpring => constraintSpring;
        internal float ConstraintDamper => constraintDamper;
        internal float BreakForceNewtons => breakForceNewtons;
        internal float BreakTorqueNewtonMeters => breakTorqueNewtonMeters;
        internal float SupportedLoadSmoothingSeconds => supportedLoadSmoothingSeconds;

        internal bool TryValidate(out string diagnostic)
        {
            if (!IsPositive(hardwareMassKilograms) || hardwareMassKilograms > 0.5f)
            {
                diagnostic = "抓斗设备总质量必须位于 (0, 0.5] kg。";
                return false;
            }

            if (!IsPositive(stowedDistanceMeters) || deployedDistanceMeters <= stowedDistanceMeters
                || !IsPositive(travelSpeedMetersPerSecond))
            {
                diagnostic = "抓斗距离必须满足 0 < 收纳距离 < 放下距离，且短行程速度为正数。";
                return false;
            }

            if (!IsAngle(twistLimitDegrees) || !IsAngle(swingLimitDegrees)
                || !float.IsFinite(dampingRatio) || dampingRatio <= 0f || dampingRatio > 1f)
            {
                diagnostic = "抓斗摆角、扭转限位和阻尼参数无效。";
                return false;
            }

            if (!IsPositive(clawSpring) || !IsPositive(clawDamper) || !IsPositive(clawMaximumForce)
                || stableContactSteps < 1 || !IsPositive(enclosureRadiusMeters)
                || !IsPositive(enclosureHalfHeightMeters) || !IsPositive(linearFreedomMeters)
                || !IsPositive(constraintSpring) || !IsPositive(constraintDamper)
                || !IsPositive(breakForceNewtons) || !IsPositive(breakTorqueNewtonMeters)
                || !IsPositive(supportedLoadSmoothingSeconds))
            {
                diagnostic = "四爪驱动、接触门禁或辅助约束参数无效。";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool IsPositive(float value) => float.IsFinite(value) && value > 0f;
        private static bool IsAngle(float value) => float.IsFinite(value) && value > 0f && value <= 60f;
    }
}
