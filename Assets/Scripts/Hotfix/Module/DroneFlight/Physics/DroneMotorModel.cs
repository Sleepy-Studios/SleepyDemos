using System;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// 单个旋翼的动态和力学参数。
    /// </summary>
    internal readonly struct DroneMotorSettings
    {
        internal DroneMotorSettings(
            float responseTime,
            float maximumRpm,
            float thrustCoefficient,
            float reactionTorqueCoefficient)
        {
            ResponseTime = PositiveFiniteOrZero(responseTime);
            MaximumRpm = PositiveFiniteOrZero(maximumRpm);
            ThrustCoefficient = PositiveFiniteOrZero(thrustCoefficient);
            ReactionTorqueCoefficient = PositiveFiniteOrZero(reactionTorqueCoefficient);
        }

        internal float ResponseTime { get; }

        internal float MaximumRpm { get; }

        internal float ThrustCoefficient { get; }

        internal float ReactionTorqueCoefficient { get; }

        private static float PositiveFiniteOrZero(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Math.Abs(value);
        }
    }

    /// <summary>
    /// 单个旋翼在当前物理帧的输出状态。
    /// </summary>
    internal readonly struct DroneMotorState
    {
        internal DroneMotorState(
            float normalizedOutput,
            float rpm,
            float thrustNewtons,
            float reactionTorqueNewtonMeters,
            bool hadInvalidInput)
        {
            NormalizedOutput = normalizedOutput;
            Rpm = rpm;
            ThrustNewtons = thrustNewtons;
            ReactionTorqueNewtonMeters = reactionTorqueNewtonMeters;
            HadInvalidInput = hadInvalidInput;
        }

        internal float NormalizedOutput { get; }

        internal float Rpm { get; }

        internal float ThrustNewtons { get; }

        internal float ReactionTorqueNewtonMeters { get; }

        internal bool HadInvalidInput { get; }
    }

    /// <summary>
    /// 使用一阶惯性逼近电机转速响应，并由转速平方计算推力。
    /// </summary>
    internal sealed class DroneMotorModel
    {
        private readonly DroneMotorSettings settings;
        private float normalizedOutput;

        internal DroneMotorModel(DroneMotorSettings settings)
        {
            this.settings = settings;
        }

        internal DroneMotorState Step(float normalizedCommand, float deltaTime)
        {
            if (!IsFinite(normalizedCommand) || !IsFinite(deltaTime) || deltaTime <= 0f)
            {
                normalizedOutput = 0f;
                return new DroneMotorState(0f, 0f, 0f, 0f, true);
            }

            var command = Clamp01(normalizedCommand);
            if (settings.ResponseTime <= 0f)
            {
                normalizedOutput = command;
            }
            else
            {
                var response = 1f - (float)Math.Exp(-deltaTime / settings.ResponseTime);
                normalizedOutput += response * (command - normalizedOutput);
                normalizedOutput = Clamp01(normalizedOutput);
            }

            var rpm = normalizedOutput * settings.MaximumRpm;
            var thrust = settings.ThrustCoefficient * rpm * rpm;
            var reactionTorque = thrust * settings.ReactionTorqueCoefficient;
            return new DroneMotorState(normalizedOutput, rpm, thrust, reactionTorque, false);
        }

        /// <summary>立即清除电机响应历史。</summary>
        internal void Reset()
        {
            normalizedOutput = 0f;
        }

        private static float Clamp01(float value)
        {
            return Math.Max(0f, Math.Min(1f, value));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
