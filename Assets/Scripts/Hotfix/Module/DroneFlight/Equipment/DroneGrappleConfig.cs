using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>四爪抓斗独立玩法与物理配置。</summary>
    [CreateAssetMenu(fileName = "DroneGrappleConfig", menuName = "SleepyDemos/Drone Flight/Grapple Config")]
    public sealed class DroneGrappleConfig : ScriptableObject
    {
        [SerializeField] private float hardwareMassKilograms = 0.05f;
        [SerializeField] private float stowedDistanceMeters = 0.08f;
        [SerializeField] private float deployedDistanceMeters = 0.26f;
        [SerializeField] private float travelSpeedMetersPerSecond = 0.3f;
        [SerializeField] private float dockPositionToleranceMeters = 0.015f;
        [SerializeField] private float dockSpeedToleranceMetersPerSecond = 0.12f;

        [SerializeField] private float twistLimitDegrees = 25f;
        [SerializeField] private float swingLimitDegrees = 35f;
        [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.45f;
        [SerializeField] private float maximumDampingTorqueNewtonMeters = 3f;

        [SerializeField] private float openAngleDegrees = 42f;
        [SerializeField] private float closedAngleDegrees = -18f;
        [SerializeField] private float clawSpring = 90f;
        [SerializeField] private float clawDamper = 10f;
        [SerializeField] private int stableContactSteps = 3;
        [SerializeField] private float enclosureRadiusMeters = 0.23f;
        [SerializeField] private float enclosureHalfHeightMeters = 0.2f;

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
            var result = Validate();
            diagnostic = result.ChineseMessage;
            return result.IsValid;
        }

        /// 返回供运行时与双语 Inspector 共用的结构化校验结果。
        internal DroneConfigValidationResult Validate()
        {
            if (!IsPositive(hardwareMassKilograms) || hardwareMassKilograms > 0.5f)
            {
                return DroneConfigValidationResult.Invalid(
                    "抓斗设备总质量必须位于 (0, 0.5] kg。",
                    "Grapple hardware mass must be within (0, 0.5] kg.");
            }

            if (!IsPositive(stowedDistanceMeters) || deployedDistanceMeters <= stowedDistanceMeters
                || !IsPositive(travelSpeedMetersPerSecond))
            {
                return DroneConfigValidationResult.Invalid(
                    "抓斗距离必须满足 0 < 收纳距离 < 放下距离，且短行程速度为正数。",
                    "Grapple travel must satisfy 0 < stowed < deployed, and travel speed must be positive.");
            }

            if (!IsAngle(twistLimitDegrees) || !IsAngle(swingLimitDegrees)
                || !float.IsFinite(dampingRatio) || dampingRatio <= 0f || dampingRatio > 1f)
            {
                return DroneConfigValidationResult.Invalid(
                    "抓斗摆角、扭转限位和阻尼参数无效。",
                    "Grapple swing, twist limits, or damping parameters are invalid.");
            }

            if (!IsPositive(clawSpring) || !IsPositive(clawDamper)
                || stableContactSteps < 1 || !IsPositive(enclosureRadiusMeters)
                || !IsPositive(enclosureHalfHeightMeters) || !IsPositive(linearFreedomMeters)
                || !IsPositive(constraintSpring) || !IsPositive(constraintDamper)
                || !IsPositive(breakForceNewtons) || !IsPositive(breakTorqueNewtonMeters)
                || !IsPositive(supportedLoadSmoothingSeconds))
            {
                return DroneConfigValidationResult.Invalid(
                    "四爪驱动、接触门禁或辅助约束参数无效。",
                    "Claw drive, contact gate, or assisted-constraint parameters are invalid.");
            }

            return DroneConfigValidationResult.Valid;
        }

        private static bool IsPositive(float value) => float.IsFinite(value) && value > 0f;
        private static bool IsAngle(float value) => float.IsFinite(value) && value > 0f && value <= 60f;
    }
}
