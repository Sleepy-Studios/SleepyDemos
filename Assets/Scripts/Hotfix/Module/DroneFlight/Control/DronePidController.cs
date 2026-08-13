using System;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// 单轴 PID 参数，所有限幅均使用绝对值。
    /// </summary>
    internal readonly struct DronePidSettings
    {
        internal DronePidSettings(
            float proportionalGain,
            float integralGain,
            float derivativeGain,
            float outputLimit,
            float integralLimit,
            float derivativeFilterHz)
        {
            ProportionalGain = FiniteOrZero(proportionalGain);
            IntegralGain = FiniteOrZero(integralGain);
            DerivativeGain = FiniteOrZero(derivativeGain);
            OutputLimit = PositiveFiniteOrZero(outputLimit);
            IntegralLimit = PositiveFiniteOrZero(integralLimit);
            DerivativeFilterHz = PositiveFiniteOrZero(derivativeFilterHz);
        }

        internal float ProportionalGain { get; }

        internal float IntegralGain { get; }

        internal float DerivativeGain { get; }

        internal float OutputLimit { get; }

        internal float IntegralLimit { get; }

        internal float DerivativeFilterHz { get; }

        private static float FiniteOrZero(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private static float PositiveFiniteOrZero(float value)
        {
            return Math.Abs(FiniteOrZero(value));
        }
    }

    /// <summary>
    /// 最近一次 PID 计算的可观测数据。
    /// </summary>
    internal readonly struct DronePidTelemetry
    {
        internal DronePidTelemetry(
            float error,
            float proportional,
            float integralState,
            float integral,
            float derivative,
            float rawOutput,
            float clampedOutput,
            bool isSaturated,
            bool hadInvalidInput)
        {
            Error = error;
            Proportional = proportional;
            IntegralState = integralState;
            Integral = integral;
            Derivative = derivative;
            RawOutput = rawOutput;
            ClampedOutput = clampedOutput;
            IsSaturated = isSaturated;
            HadInvalidInput = hadInvalidInput;
        }

        internal float Error { get; }

        internal float Proportional { get; }

        internal float IntegralState { get; }

        internal float Integral { get; }

        internal float Derivative { get; }

        internal float RawOutput { get; }

        internal float ClampedOutput { get; }

        internal bool IsSaturated { get; }

        internal bool HadInvalidInput { get; }
    }

    /// <summary>
    /// 可脱离场景测试的单轴 PID，包含积分限幅、条件抗饱和和一阶导数滤波。
    /// </summary>
    internal sealed class DronePidController
    {
        private readonly DronePidSettings settings;
        private float integralState;
        private float previousError;
        private float filteredDerivative;
        private float integralStateBeforeStep;

        internal DronePidController(DronePidSettings settings)
        {
            this.settings = settings;
            Reset();
        }

        /// <summary>最近一次计算数据。</summary>
        internal DronePidTelemetry Telemetry { get; private set; }

        /// <summary>控制器是否已有可用于微分项的上一帧误差。</summary>
        internal bool HasHistory { get; private set; }

        // 无效输入会清空历史，避免 NaN 或异常时间步污染后续物理帧。
        internal float Step(float error, float deltaTime)
        {
            if (!IsFinite(error) || !IsFinite(deltaTime) || deltaTime <= 0f)
            {
                Reset();
                Telemetry = new DronePidTelemetry(0f, 0f, 0f, 0f, 0f, 0f, 0f, false, true);
                return 0f;
            }

            integralStateBeforeStep = integralState;
            var proportional = settings.ProportionalGain * error;
            var derivativeState = CalculateDerivative(error, deltaTime);
            var derivative = settings.DerivativeGain * derivativeState;

            var candidateIntegralState = Clamp(
                integralState + error * deltaTime,
                -settings.IntegralLimit,
                settings.IntegralLimit);
            var candidateIntegral = settings.IntegralGain * candidateIntegralState;
            var candidateOutput = proportional + candidateIntegral + derivative;
            var candidateClampedOutput = ClampOutput(candidateOutput);
            var candidateSaturated = !Approximately(candidateOutput, candidateClampedOutput);

            // 仅当误差会继续把输出推向饱和方向时拒绝本次积分。
            if (!candidateSaturated || Math.Sign(error) != Math.Sign(candidateOutput))
            {
                integralState = candidateIntegralState;
            }

            var integral = settings.IntegralGain * integralState;
            var rawOutput = proportional + integral + derivative;
            var clampedOutput = ClampOutput(rawOutput);
            var isSaturated = !Approximately(rawOutput, clampedOutput);

            previousError = error;
            HasHistory = true;
            Telemetry = new DronePidTelemetry(
                error,
                proportional,
                integralState,
                integral,
                derivative,
                rawOutput,
                clampedOutput,
                isSaturated,
                false);
            return clampedOutput;
        }

        /// <summary>
        /// 执行器或混控器已饱和时撤销本固定步新增的积分，防止合成输出饱和造成 wind-up。
        /// </summary>
        internal void ApplyActuatorSaturation(bool isSaturated)
        {
            if (!isSaturated || !HasHistory)
            {
                return;
            }

            integralState = integralStateBeforeStep;
            var integral = settings.IntegralGain * integralState;
            var rawOutput = Telemetry.Proportional + integral + Telemetry.Derivative;
            var clampedOutput = ClampOutput(rawOutput);
            Telemetry = new DronePidTelemetry(
                Telemetry.Error,
                Telemetry.Proportional,
                integralState,
                integral,
                Telemetry.Derivative,
                rawOutput,
                clampedOutput,
                true,
                Telemetry.HadInvalidInput);
        }

        /// <summary>清除积分、微分和遥测历史。</summary>
        internal void Reset()
        {
            integralState = 0f;
            previousError = 0f;
            filteredDerivative = 0f;
            integralStateBeforeStep = 0f;
            HasHistory = false;
            Telemetry = default;
        }

        private float CalculateDerivative(float error, float deltaTime)
        {
            if (!HasHistory)
            {
                filteredDerivative = 0f;
                return 0f;
            }

            var rawDerivative = (error - previousError) / deltaTime;
            if (settings.DerivativeFilterHz <= 0f)
            {
                filteredDerivative = rawDerivative;
                return filteredDerivative;
            }

            var alpha = 1f - (float)Math.Exp(-2f * Math.PI * settings.DerivativeFilterHz * deltaTime);
            filteredDerivative += alpha * (rawDerivative - filteredDerivative);
            return filteredDerivative;
        }

        private float ClampOutput(float value)
        {
            return Clamp(value, -settings.OutputLimit, settings.OutputLimit);
        }

        private static float Clamp(float value, float minimum, float maximum)
        {
            return Math.Max(minimum, Math.Min(maximum, value));
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool Approximately(float left, float right)
        {
            return Math.Abs(left - right) <= 0.000001f;
        }
    }
}
