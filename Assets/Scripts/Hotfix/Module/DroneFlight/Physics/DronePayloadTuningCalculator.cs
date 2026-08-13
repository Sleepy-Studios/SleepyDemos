using System;

namespace Hotfix.DroneFlight
{
    /// <summary>无人机动力参数由策划载重自动派生，或完全使用手动物理值。</summary>
    internal enum DronePowerConfigurationMode
    {
        AutomaticPayloadTuning,
        ManualPhysics
    }

    internal enum DronePayloadOperatingZone
    {
        Rated,
        AboveRated,
        OverloadRejected
    }

    /// <summary>自动载重调校的纯数据输入。</summary>
    internal readonly struct DronePayloadTuningInput
    {
        internal DronePayloadTuningInput(
            float ratedPayloadKilograms,
            float bodyMassMultiplier,
            float maximumPayloadMultiplier,
            float ratedPayloadHoverCommand,
            float maximumRpm,
            float deployedHardwareMassKilograms,
            float gravityMetersPerSecondSquared)
        {
            RatedPayloadKilograms = ratedPayloadKilograms;
            BodyMassMultiplier = bodyMassMultiplier;
            MaximumPayloadMultiplier = maximumPayloadMultiplier;
            RatedPayloadHoverCommand = ratedPayloadHoverCommand;
            MaximumRpm = maximumRpm;
            DeployedHardwareMassKilograms = deployedHardwareMassKilograms;
            GravityMetersPerSecondSquared = gravityMetersPerSecondSquared;
        }

        internal float RatedPayloadKilograms { get; }
        internal float BodyMassMultiplier { get; }
        internal float MaximumPayloadMultiplier { get; }
        internal float RatedPayloadHoverCommand { get; }
        internal float MaximumRpm { get; }
        internal float DeployedHardwareMassKilograms { get; }
        internal float GravityMetersPerSecondSquared { get; }
    }

    /// <summary>自动载重调校的只读派生结果。</summary>
    internal readonly struct DronePayloadTuningResult
    {
        internal DronePayloadTuningResult(
            bool isValid,
            string diagnostic,
            float bodyMassKilograms,
            float maximumPayloadKilograms,
            float ratedOperatingMassKilograms,
            float thrustCoefficient,
            float ratedPowerReserve,
            float maximumPayloadHoverCommand)
        {
            IsValid = isValid;
            Diagnostic = diagnostic;
            BodyMassKilograms = bodyMassKilograms;
            MaximumPayloadKilograms = maximumPayloadKilograms;
            RatedOperatingMassKilograms = ratedOperatingMassKilograms;
            ThrustCoefficient = thrustCoefficient;
            RatedPowerReserve = ratedPowerReserve;
            MaximumPayloadHoverCommand = maximumPayloadHoverCommand;
        }

        internal bool IsValid { get; }
        internal string Diagnostic { get; }
        internal float BodyMassKilograms { get; }
        internal float MaximumPayloadKilograms { get; }
        internal float RatedOperatingMassKilograms { get; }
        internal float ThrustCoefficient { get; }
        internal float RatedPowerReserve { get; }
        internal float MaximumPayloadHoverCommand { get; }
        internal bool CanHoverAtMaximumPayload => IsValid && MaximumPayloadHoverCommand < 1f;
    }

    /// <summary>策划载重到真实四旋翼动力参数的确定性计算器。</summary>
    internal static class DronePayloadTuningCalculator
    {
        internal const float DefaultDeployedHardwareMassKilograms = 0.05f;

        internal static DronePayloadTuningResult Calculate(DronePayloadTuningInput input)
        {
            if (!IsPositiveFinite(input.RatedPayloadKilograms))
            {
                return Invalid("额定载重必须是大于 0 的有限值。");
            }

            if (!IsPositiveFinite(input.BodyMassMultiplier))
            {
                return Invalid("机体质量倍率必须是大于 0 的有限值。");
            }

            if (!float.IsFinite(input.MaximumPayloadMultiplier) || input.MaximumPayloadMultiplier < 1f)
            {
                return Invalid("最大载荷倍率必须是大于等于 1 的有限值。");
            }

            if (!float.IsFinite(input.RatedPayloadHoverCommand)
                || input.RatedPayloadHoverCommand <= 0f
                || input.RatedPayloadHoverCommand >= 1f)
            {
                return Invalid("满载动力占用必须大于 0 且小于 100%。");
            }

            if (!IsPositiveFinite(input.MaximumRpm)
                || !IsPositiveFinite(input.GravityMetersPerSecondSquared)
                || !float.IsFinite(input.DeployedHardwareMassKilograms)
                || input.DeployedHardwareMassKilograms < 0f)
            {
                return Invalid("最大转速、重力和吊挂设备质量必须是合法有限值。");
            }

            var bodyMass = input.RatedPayloadKilograms * input.BodyMassMultiplier;
            var maximumPayload = input.RatedPayloadKilograms * input.MaximumPayloadMultiplier;
            var ratedMass = bodyMass + input.DeployedHardwareMassKilograms + input.RatedPayloadKilograms;
            var ratedRpm = input.MaximumRpm * input.RatedPayloadHoverCommand;
            var thrustCoefficient = ratedMass * input.GravityMetersPerSecondSquared / (4f * ratedRpm * ratedRpm);
            var maximumMass = bodyMass + input.DeployedHardwareMassKilograms + maximumPayload;
            var maximumHover = CalculateHoverCommand(
                maximumMass,
                input.GravityMetersPerSecondSquared,
                input.MaximumRpm,
                thrustCoefficient);
            if (!IsPositiveFinite(bodyMass) || !IsPositiveFinite(thrustCoefficient) || !float.IsFinite(maximumHover))
            {
                return Invalid("自动机体质量或推力系数计算失败，请检查输入范围。");
            }

            return new DronePayloadTuningResult(
                true,
                string.Empty,
                bodyMass,
                maximumPayload,
                ratedMass,
                thrustCoefficient,
                1f - input.RatedPayloadHoverCommand,
                maximumHover);
        }

        internal static float CalculateHoverCommand(
            float supportedMassKilograms,
            float gravityMetersPerSecondSquared,
            float maximumRpm,
            float thrustCoefficient)
        {
            if (!IsPositiveFinite(supportedMassKilograms)
                || !IsPositiveFinite(gravityMetersPerSecondSquared)
                || !IsPositiveFinite(maximumRpm)
                || !IsPositiveFinite(thrustCoefficient))
            {
                return float.NaN;
            }

            var hoverRpm = (float)Math.Sqrt(
                supportedMassKilograms * gravityMetersPerSecondSquared / (4f * thrustCoefficient));
            return hoverRpm / maximumRpm;
        }

        internal static float MapMotorResponsivenessToResponseTime(float responsiveness)
        {
            var normalized = Math.Max(0f, Math.Min(100f, responsiveness)) / 100f;
            return 0.35f + (0.015f - 0.35f) * (float)Math.Pow(normalized, 0.55f);
        }

        internal static float MapGripStrengthToBreakForce(float strength)
        {
            return Lerp(60f, 300f, Clamp01(strength / 100f));
        }

        internal static float MapGripStrengthToBreakTorque(float strength)
        {
            return Lerp(25f, 140f, Clamp01(strength / 100f));
        }

        private static DronePayloadTuningResult Invalid(string diagnostic)
        {
            return new DronePayloadTuningResult(false, diagnostic, 0f, 0f, 0f, 0f, 0f, float.NaN);
        }

        private static bool IsPositiveFinite(float value)
        {
            return value > 0f && float.IsFinite(value);
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static float Lerp(float from, float to, float value)
        {
            return from + (to - from) * value;
        }
    }
}
