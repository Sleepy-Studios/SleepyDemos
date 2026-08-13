using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    [Serializable]
    internal sealed class DronePidAxisConfig
    {
        [SerializeField] private float proportionalGain = 0.1f;
        [SerializeField] private float integralGain = 0.02f;
        [SerializeField] private float derivativeGain = 0.002f;
        [SerializeField] private float outputLimit = 0.25f;
        [SerializeField] private float integralLimit = 0.2f;
        [SerializeField] private float derivativeFilterHz = 8f;

        internal DronePidSettings CreateSettings()
        {
            return new DronePidSettings(
                proportionalGain,
                integralGain,
                derivativeGain,
                outputLimit,
                integralLimit,
                derivativeFilterHz);
        }
    }

    [Serializable]
    internal sealed class DroneResponseProfileConfig
    {
        [SerializeField] private float maximumHorizontalSpeed = 4f;
        [SerializeField] private float maximumHorizontalAcceleration = 2.5f;
        [SerializeField] private float maximumTiltDegrees = 25f;
        [SerializeField] private float maximumVerticalSpeed = 2f;
        [SerializeField] private float maximumYawSpeedDegrees = 90f;
        [SerializeField] private float inputRiseRate = 3f;

        internal DroneResponseProfileConfig()
        {
        }

        internal DroneResponseProfileConfig(
            float maximumHorizontalSpeed,
            float maximumHorizontalAcceleration,
            float maximumTiltDegrees,
            float maximumVerticalSpeed,
            float maximumYawSpeedDegrees,
            float inputRiseRate)
        {
            this.maximumHorizontalSpeed = maximumHorizontalSpeed;
            this.maximumHorizontalAcceleration = maximumHorizontalAcceleration;
            this.maximumTiltDegrees = maximumTiltDegrees;
            this.maximumVerticalSpeed = maximumVerticalSpeed;
            this.maximumYawSpeedDegrees = maximumYawSpeedDegrees;
            this.inputRiseRate = inputRiseRate;
        }

        internal DroneResponseProfileParameters CreateParameters()
        {
            return new DroneResponseProfileParameters(
                maximumHorizontalSpeed,
                maximumHorizontalAcceleration,
                maximumTiltDegrees,
                maximumVerticalSpeed,
                maximumYawSpeedDegrees,
                inputRiseRate);
        }

        internal bool IsValid()
        {
            return IsPositiveFinite(maximumHorizontalSpeed)
                && IsPositiveFinite(maximumHorizontalAcceleration)
                && IsPositiveFinite(maximumTiltDegrees)
                && maximumTiltDegrees < 90f
                && IsPositiveFinite(maximumVerticalSpeed)
                && IsPositiveFinite(maximumYawSpeedDegrees)
                && IsPositiveFinite(inputRiseRate);
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>
    /// 基础 X 架机体、电机和内环控制参数的单一配置资产。
    /// </summary>
    [CreateAssetMenu(fileName = "DroneFlightConfig", menuName = "SleepyDemos/Drone Flight/Config")]
    public sealed class DroneFlightConfig : ScriptableObject
    {
        [Header("Payload Friendly Tuning")]
        [SerializeField] private DronePowerConfigurationMode powerConfigurationMode = DronePowerConfigurationMode.AutomaticPayloadTuning;
        [SerializeField] private float ratedPayloadKilograms = 1f;
        [SerializeField] private float bodyMassMultiplier = 1.2f;
        [SerializeField, Range(1f, 1.5f)] private float maximumPayloadMultiplier = 1.25f;
        [SerializeField, Range(0.5f, 0.95f)] private float ratedPayloadHoverCommand = 0.9f;
        [SerializeField, Range(0f, 100f)] private float motorResponsiveness = 70f;

        [Header("Airframe")]
        [SerializeField] private float bodyMassKilograms = 1.2f;
        [SerializeField] private float bodyLinearDamping;
        [SerializeField] private float bodyAngularDamping = 0.05f;

        [Header("Motor")]
        [SerializeField] private float motorResponseTimeSeconds = 0.08f;
        [SerializeField] private float maximumRpm = 10000f;
        [SerializeField] private float thrustCoefficient = 0.00000006f;
        [SerializeField] private float reactionTorqueCoefficient = 0.018f;

        [Header("Attitude")]
        [SerializeField] private float attitudeGain = 4f;
        [SerializeField] private float maximumRateRadiansPerSecond = 3.5f;
        [SerializeField] private DronePidAxisConfig rollRate = new();
        [SerializeField] private DronePidAxisConfig pitchRate = new();
        [SerializeField] private DronePidAxisConfig yawRate = new();

        [Header("Altitude")]
        [SerializeField] private float altitudeGain = 1.5f;
        [SerializeField] private float maximumVerticalSpeedMetersPerSecond = 2f;
        [SerializeField] private float verticalSpeedProportionalGain = 0.08f;
        [SerializeField] private float verticalSpeedIntegralGain = 0.03f;
        [SerializeField] private float verticalSpeedDerivativeGain = 0.01f;
        [SerializeField] private float verticalSpeedOutputLimit = 0.25f;
        [SerializeField] private float verticalSpeedIntegralLimit = 3f;
        [SerializeField] private float verticalSpeedDerivativeFilterHz = 5f;

        [Header("Horizontal Position")]
        [SerializeField] private float maximumHorizontalSpeedMetersPerSecond = 4f;
        [SerializeField] private float horizontalPositionGain = 0.35f;
        [SerializeField] private float horizontalVelocityGain = 1.2f;
        [SerializeField] private float maximumHorizontalAccelerationMetersPerSecondSquared = 2.5f;

        [Header("Response Profiles")]
        [SerializeField] private DroneResponseProfileConfig cineProfile = new(
            2f, 1.2f, 15f, 1f, 45f, 2f);
        [SerializeField] private DroneResponseProfileConfig normalProfile = new(
            4f, 2.5f, 25f, 2f, 90f, 3f);
        [SerializeField] private DroneResponseProfileConfig sportProfile = new(
            7f, 5f, 35f, 3f, 150f, 5f);

        [Header("Automatic Flight")]
        [SerializeField] private float automaticTakeoffHeightMeters = 1.5f;
        [SerializeField] private float automaticLandingSpeedMetersPerSecond = 0.5f;
        [SerializeField] private DroneResponseProfile defaultResponseProfile = DroneResponseProfile.Normal;

        [Header("Landing Gear")]
        [SerializeField] private float landingGearTransitionSeconds = 0.45f;

        [Header("Winch And Grapple")]
        [SerializeField] private float grappleHardwareMassKilograms = 0.05f;
        [SerializeField] private float winchStowedLengthMeters = 0.08f;
        [SerializeField] private float winchDeployedLengthMeters = 0.45f;
        [SerializeField] private float winchCarryLengthMeters = 0.24f;
        [SerializeField] private float winchSpeedMetersPerSecond = 0.35f;
        [SerializeField] private float maximumPayloadMassKilograms = 0.6f;
        [SerializeField] private float grappleBreakForceNewtons = 180f;
        [SerializeField] private float grappleBreakTorqueNewtonMeters = 80f;
        [SerializeField, Range(0f, 100f)] private float grappleStrength = 50f;
        [SerializeField] private float grappleLinearFreedomMeters = 0.035f;
        [SerializeField] private float grappleAngularFreedomDegrees = 12f;
        [SerializeField] private float resetHoldSeconds = 5f;

        internal DronePowerConfigurationMode PowerConfigurationMode => powerConfigurationMode;

        internal float RatedPayloadKilograms => ratedPayloadKilograms;

        internal float BodyMassMultiplier => bodyMassMultiplier;

        internal float MaximumPayloadMultiplier => maximumPayloadMultiplier;

        internal float RatedPayloadHoverCommand => ratedPayloadHoverCommand;

        internal float MotorResponsiveness => motorResponsiveness;

        internal DronePayloadTuningResult AutomaticTuning => DronePayloadTuningCalculator.Calculate(
            new DronePayloadTuningInput(
                ratedPayloadKilograms,
                bodyMassMultiplier,
                maximumPayloadMultiplier,
                ratedPayloadHoverCommand,
                maximumRpm,
                grappleHardwareMassKilograms,
                Mathf.Abs(Physics.gravity.y)));

        /// <summary>机体裸机质量，单位 kg。</summary>
        internal float BodyMassKilograms => powerConfigurationMode == DronePowerConfigurationMode.AutomaticPayloadTuning
            ? AutomaticTuning.BodyMassKilograms
            : bodyMassKilograms;

        internal float BodyLinearDamping => bodyLinearDamping;

        internal float BodyAngularDamping => bodyAngularDamping;

        /// <summary>电机一阶响应时间常数，单位 s。</summary>
        internal float MotorResponseTimeSeconds => powerConfigurationMode == DronePowerConfigurationMode.AutomaticPayloadTuning
            ? DronePayloadTuningCalculator.MapMotorResponsivenessToResponseTime(motorResponsiveness)
            : motorResponseTimeSeconds;

        /// <summary>归一化满量程对应转速，单位 rpm。</summary>
        internal float MaximumRpm => maximumRpm;

        /// <summary>`T = k * rpm²` 中的推力系数。</summary>
        internal float ThrustCoefficient => powerConfigurationMode == DronePowerConfigurationMode.AutomaticPayloadTuning
            ? AutomaticTuning.ThrustCoefficient
            : thrustCoefficient;

        /// <summary>`Q = T * coefficient` 中的反扭矩比例。</summary>
        internal float ReactionTorqueCoefficient => reactionTorqueCoefficient;

        /// <summary>姿态误差到目标角速度的比例。</summary>
        internal float AttitudeGain => attitudeGain;

        /// <summary>内环目标角速度限幅，单位 rad/s。</summary>
        internal float MaximumRateRadiansPerSecond => maximumRateRadiansPerSecond;

        /// <summary>高度误差到目标垂直速度的比例。</summary>
        internal float AltitudeGain => altitudeGain;

        /// <summary>目标垂直速度限幅，单位 m/s。</summary>
        internal float MaximumVerticalSpeedMetersPerSecond => maximumVerticalSpeedMetersPerSecond;

        /// <summary>Normal 档最大水平速度，单位 m/s。</summary>
        internal float MaximumHorizontalSpeedMetersPerSecond => maximumHorizontalSpeedMetersPerSecond;

        /// <summary>位置误差到目标水平速度的比例。</summary>
        internal float HorizontalPositionGain => horizontalPositionGain;

        /// <summary>速度误差到目标水平加速度的比例。</summary>
        internal float HorizontalVelocityGain => horizontalVelocityGain;

        /// <summary>水平加速度限幅，单位 m/s²。</summary>
        internal float MaximumHorizontalAccelerationMetersPerSecondSquared => maximumHorizontalAccelerationMetersPerSecondSquared;

        internal float AutomaticTakeoffHeightMeters => automaticTakeoffHeightMeters;

        internal float AutomaticLandingSpeedMetersPerSecond => automaticLandingSpeedMetersPerSecond;

        internal DroneResponseProfile DefaultResponseProfile => defaultResponseProfile;

        internal float LandingGearTransitionSeconds => landingGearTransitionSeconds;

        internal float GrappleHardwareMassKilograms => grappleHardwareMassKilograms;

        internal float WinchStowedLengthMeters => winchStowedLengthMeters;

        internal float WinchDeployedLengthMeters => winchDeployedLengthMeters;

        internal float WinchCarryLengthMeters => winchCarryLengthMeters;

        internal float WinchSpeedMetersPerSecond => winchSpeedMetersPerSecond;

        internal float MaximumPayloadMassKilograms => ratedPayloadKilograms * maximumPayloadMultiplier;

        internal float GrappleBreakForceNewtons => powerConfigurationMode == DronePowerConfigurationMode.AutomaticPayloadTuning
            ? DronePayloadTuningCalculator.MapGripStrengthToBreakForce(grappleStrength)
            : grappleBreakForceNewtons;

        internal float GrappleBreakTorqueNewtonMeters => powerConfigurationMode == DronePowerConfigurationMode.AutomaticPayloadTuning
            ? DronePayloadTuningCalculator.MapGripStrengthToBreakTorque(grappleStrength)
            : grappleBreakTorqueNewtonMeters;

        internal float GrappleStrength => grappleStrength;

        internal float GrappleLinearFreedomMeters => grappleLinearFreedomMeters;

        internal float GrappleAngularFreedomDegrees => grappleAngularFreedomDegrees;

        internal float ResetHoldSeconds => resetHoldSeconds;

        internal DronePidSettings CreateRollRateSettings()
        {
            return rollRate.CreateSettings();
        }

        internal DronePidSettings CreatePitchRateSettings()
        {
            return pitchRate.CreateSettings();
        }

        internal DronePidSettings CreateYawRateSettings()
        {
            return yawRate.CreateSettings();
        }

        internal DronePidSettings CreateVerticalSpeedSettings()
        {
            return new DronePidSettings(
                verticalSpeedProportionalGain,
                verticalSpeedIntegralGain,
                verticalSpeedDerivativeGain,
                verticalSpeedOutputLimit,
                verticalSpeedIntegralLimit,
                verticalSpeedDerivativeFilterHz);
        }

        internal DroneResponseProfileParameters GetProfile(DroneResponseProfile profile)
        {
            return profile switch
            {
                DroneResponseProfile.Cine => cineProfile.CreateParameters(),
                DroneResponseProfile.Sport => sportProfile.CreateParameters(),
                _ => normalProfile.CreateParameters()
            };
        }

        /// <summary>为确定性测试和运行时调校工具设置自动载重输入，不修改飞行档位。</summary>
        internal void ConfigureAutomaticPayloadTuning(
            float ratedPayload,
            float payloadMultiplier,
            float hoverCommand,
            float massMultiplier = 1.2f,
            float rpm = 10000f,
            float responsiveness = 70f)
        {
            powerConfigurationMode = DronePowerConfigurationMode.AutomaticPayloadTuning;
            ratedPayloadKilograms = ratedPayload;
            maximumPayloadMultiplier = payloadMultiplier;
            ratedPayloadHoverCommand = hoverCommand;
            bodyMassMultiplier = massMultiplier;
            maximumRpm = rpm;
            motorResponsiveness = responsiveness;
        }

        /// <summary>为确定性测试和运行时调校工具设置手动物理参数。</summary>
        internal void ConfigureManualPhysics(
            float ratedPayload,
            float mass,
            float rpm,
            float coefficient,
            float responseTime)
        {
            powerConfigurationMode = DronePowerConfigurationMode.ManualPhysics;
            ratedPayloadKilograms = ratedPayload;
            bodyMassKilograms = mass;
            maximumRpm = rpm;
            thrustCoefficient = coefficient;
            motorResponseTimeSeconds = responseTime;
        }

        internal void ConfigureGrappleHardwareMass(float massKilograms)
        {
            grappleHardwareMassKilograms = massKilograms;
        }

        internal bool TryValidate(out string diagnostic)
        {
            var tuning = AutomaticTuning;
            if (!IsPositiveFinite(ratedPayloadKilograms))
            {
                diagnostic = "额定载重必须是大于 0 的有限值。";
                return false;
            }

            if (!IsPositiveFinite(maximumPayloadMassKilograms))
            {
                diagnostic = "兼容用最大载荷质量必须是大于 0 的有限值。";
                return false;
            }

            if (!float.IsFinite(maximumPayloadMultiplier) || maximumPayloadMultiplier < 1f)
            {
                diagnostic = "最大载荷倍率必须是大于等于 1 的有限值。";
                return false;
            }

            if (powerConfigurationMode == DronePowerConfigurationMode.AutomaticPayloadTuning && !tuning.IsValid)
            {
                diagnostic = tuning.Diagnostic;
                return false;
            }

            if (powerConfigurationMode == DronePowerConfigurationMode.ManualPhysics && !IsPositiveFinite(bodyMassKilograms))
            {
                diagnostic = "机体质量必须是大于 0 的有限值。";
                return false;
            }

            if (!IsPositiveFinite(maximumRpm)
                || (powerConfigurationMode == DronePowerConfigurationMode.ManualPhysics && !IsPositiveFinite(thrustCoefficient)))
            {
                diagnostic = "最大转速和推力系数必须是大于 0 的有限值。";
                return false;
            }

            if (!IsPositiveFinite(MotorResponseTimeSeconds) || !IsPositiveFinite(reactionTorqueCoefficient))
            {
                diagnostic = "电机响应时间和反扭矩系数必须是大于 0 的有限值。";
                return false;
            }

            if (!IsPositiveFinite(altitudeGain) || !IsPositiveFinite(maximumVerticalSpeedMetersPerSecond))
            {
                diagnostic = "高度增益和最大垂直速度必须是大于 0 的有限值。";
                return false;
            }

            if (!IsPositiveFinite(maximumHorizontalSpeedMetersPerSecond)
                || !IsPositiveFinite(horizontalPositionGain)
                || !IsPositiveFinite(horizontalVelocityGain)
                || !IsPositiveFinite(maximumHorizontalAccelerationMetersPerSecondSquared))
            {
                diagnostic = "水平速度、位置增益和加速度限制必须是大于 0 的有限值。";
                return false;
            }

            if (cineProfile == null || normalProfile == null || sportProfile == null
                || !cineProfile.IsValid() || !normalProfile.IsValid() || !sportProfile.IsValid())
            {
                diagnostic = "Cine、Normal、Sport 响应档位必须全部存在且参数有效。";
                return false;
            }

            if (!IsPositiveFinite(landingGearTransitionSeconds))
            {
                diagnostic = "起落架过渡时间必须为正数。";
                return false;
            }

            if (!IsPositiveFinite(grappleHardwareMassKilograms)
                || !IsPositiveFinite(winchStowedLengthMeters)
                || !IsPositiveFinite(winchCarryLengthMeters)
                || !IsPositiveFinite(winchDeployedLengthMeters)
                || !(winchStowedLengthMeters < winchCarryLengthMeters
                     && winchCarryLengthMeters < winchDeployedLengthMeters)
                || !IsPositiveFinite(winchSpeedMetersPerSecond)
                || !IsPositiveFinite(MaximumPayloadMassKilograms)
                || !IsPositiveFinite(GrappleBreakForceNewtons)
                || !IsPositiveFinite(GrappleBreakTorqueNewtonMeters)
                || !IsPositiveFinite(grappleLinearFreedomMeters)
                || !float.IsFinite(grappleAngularFreedomDegrees)
                || grappleAngularFreedomDegrees <= 0f
                || grappleAngularFreedomDegrees > 45f
                || !IsPositiveFinite(resetHoldSeconds))
            {
                diagnostic = "卷扬长度必须满足收纳 < 运输 < 放出，且抓斗、载荷与复位参数必须为正数。";
                return false;
            }

            if (!IsPositiveFinite(automaticTakeoffHeightMeters)
                || !IsPositiveFinite(automaticLandingSpeedMetersPerSecond))
            {
                diagnostic = "自动起飞高度和自动降落速度必须是大于 0 的有限值。";
                return false;
            }

            var manualHover = DronePayloadTuningCalculator.CalculateHoverCommand(
                BodyMassKilograms + grappleHardwareMassKilograms + ratedPayloadKilograms,
                Mathf.Abs(Physics.gravity.y),
                MaximumRpm,
                ThrustCoefficient);
            if (!float.IsFinite(manualHover) || manualHover >= 1f)
            {
                diagnostic = powerConfigurationMode == DronePowerConfigurationMode.ManualPhysics
                    ? "手动物理参数的最大推力不足，额定载重工况无法悬停。"
                    : "自动载重调校无法满足额定工况悬停。";
                return false;
            }

            diagnostic = string.Empty;
            return true;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
