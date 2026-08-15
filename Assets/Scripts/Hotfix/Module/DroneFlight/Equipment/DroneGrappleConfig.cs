using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>四爪抓斗独立玩法与物理配置。</summary>
    [CreateAssetMenu(fileName = "DroneGrappleConfig", menuName = "SleepyDemos/Drone Flight/Grapple Config")]
    public sealed class DroneGrappleConfig : ScriptableObject
    {
        [SerializeField] private float armLengthMeters = 0.08f;
        [SerializeField] private float maximumLiftTravelMeters = 0.35f;
        [SerializeField] private float liftSpeedMetersPerSecond = 0.18f;
        [SerializeField] private float liftAccelerationMetersPerSecondSquared = 0.45f;
        [SerializeField] private float swingLimitDegrees = 35f;
        [SerializeField, Range(0f, 1f)] private float dampingRatio = 0.45f;
        [SerializeField] private float maximumDampingTorqueNewtonMeters = 3f;

        [SerializeField] private float openAngleDegrees;
        [SerializeField] private float closedAngleDegrees = 75f;
        [SerializeField] private float clawSpring = 90f;
        [SerializeField] private float clawDamper = 10f;
        [SerializeField] private float enclosureRadiusMeters = 0.23f;
        [SerializeField] private float enclosureHalfHeightMeters = 0.2f;

        [SerializeField] private float breakForceNewtons = 180f;
        [SerializeField] private float breakTorqueNewtonMeters = 80f;
        [SerializeField] private float supportedLoadSmoothingSeconds = 0.18f;

        internal float ArmLengthMeters => armLengthMeters;
        internal float MaximumLiftTravelMeters => maximumLiftTravelMeters;
        internal float LiftSpeedMetersPerSecond => liftSpeedMetersPerSecond;
        internal float LiftAccelerationMetersPerSecondSquared => liftAccelerationMetersPerSecondSquared;
        internal float SwingLimitDegrees => swingLimitDegrees;
        internal float DampingRatio => dampingRatio;
        internal float MaximumDampingTorqueNewtonMeters => maximumDampingTorqueNewtonMeters;
        internal float OpenAngleDegrees => openAngleDegrees;
        internal float ClosedAngleDegrees => closedAngleDegrees;
        internal float ClawSpring => clawSpring;
        internal float ClawDamper => clawDamper;
        internal float EnclosureRadiusMeters => enclosureRadiusMeters;
        internal float EnclosureHalfHeightMeters => enclosureHalfHeightMeters;
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
            if (!IsPositive(armLengthMeters) || armLengthMeters > 0.2f)
            {
                return DroneConfigValidationResult.Invalid(
                    "抓斗固定吊臂长度必须位于 (0, 0.2] m。",
                    "Grapple fixed-arm length must be within (0, 0.2] m.");
            }

            if (!IsPositive(maximumLiftTravelMeters) || maximumLiftTravelMeters > 1f
                || !IsPositive(liftSpeedMetersPerSecond) || liftSpeedMetersPerSecond > 2f
                || !IsPositive(liftAccelerationMetersPerSecondSquared)
                || liftAccelerationMetersPerSecondSquared > 5f)
            {
                return DroneConfigValidationResult.Invalid(
                    "抓斗升降行程、速度或加速度无效。",
                    "Grapple lift travel, speed, or acceleration is invalid.");
            }

            if (!IsAngle(swingLimitDegrees)
                || !float.IsFinite(dampingRatio) || dampingRatio <= 0f || dampingRatio > 1f)
            {
                return DroneConfigValidationResult.Invalid(
                    "抓斗万向节双轴摆角或阻尼参数无效。",
                    "Grapple universal-joint swing or damping parameters are invalid.");
            }

            if (!IsPositive(clawSpring) || !IsPositive(clawDamper)
                || !IsPositive(enclosureRadiusMeters) || !IsPositive(enclosureHalfHeightMeters)
                || !IsPositive(breakForceNewtons) || !IsPositive(breakTorqueNewtonMeters)
                || !IsPositive(supportedLoadSmoothingSeconds))
            {
                return DroneConfigValidationResult.Invalid(
                    "四爪驱动、包围范围或刚性抓取参数无效。",
                    "Claw drive, enclosure, or rigid-grip parameters are invalid.");
            }

            return DroneConfigValidationResult.Valid;
        }

        private static bool IsPositive(float value) => float.IsFinite(value) && value > 0f;
        private static bool IsAngle(float value) => float.IsFinite(value) && value > 0f && value <= 60f;
    }
}
