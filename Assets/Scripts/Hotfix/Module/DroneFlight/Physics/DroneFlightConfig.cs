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
        [Header("Airframe")]
        [SerializeField] private float bodyMassKilograms = 1.2f;

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

        /// <summary>机体裸机质量，单位 kg。</summary>
        internal float BodyMassKilograms => bodyMassKilograms;

        /// <summary>电机一阶响应时间常数，单位 s。</summary>
        internal float MotorResponseTimeSeconds => motorResponseTimeSeconds;

        /// <summary>归一化满量程对应转速，单位 rpm。</summary>
        internal float MaximumRpm => maximumRpm;

        /// <summary>`T = k * rpm²` 中的推力系数。</summary>
        internal float ThrustCoefficient => thrustCoefficient;

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

        internal bool TryValidate(out string diagnostic)
        {
            if (!IsPositiveFinite(bodyMassKilograms))
            {
                diagnostic = "机体质量必须是大于 0 的有限值。";
                return false;
            }

            if (!IsPositiveFinite(maximumRpm) || !IsPositiveFinite(thrustCoefficient))
            {
                diagnostic = "最大转速和推力系数必须是大于 0 的有限值。";
                return false;
            }

            if (!IsPositiveFinite(motorResponseTimeSeconds) || !IsPositiveFinite(reactionTorqueCoefficient))
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

            diagnostic = string.Empty;
            return true;
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
