using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证 PID 的比例、积分、微分滤波、输出限幅、复位和外部执行器饱和回滚行为。
     */
    public sealed class DronePidControllerTests
    {
        [Test]
        public void Step_ProportionalOnlyReturnsScaledError()
        {
            var controller = new DronePidController(new DronePidSettings(
                proportionalGain: 2f,
                integralGain: 0f,
                derivativeGain: 0f,
                outputLimit: 10f,
                integralLimit: 5f,
                derivativeFilterHz: 0f));

            var output = controller.Step(1.5f, 0.02f);

            Assert.That(output, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(controller.Telemetry.Proportional, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(controller.Telemetry.IsSaturated, Is.False);
        }

        [Test]
        public void Step_IntegralAccumulatesAndHonorsLimit()
        {
            var controller = new DronePidController(new DronePidSettings(
                proportionalGain: 0f,
                integralGain: 2f,
                derivativeGain: 0f,
                outputLimit: 100f,
                integralLimit: 0.5f,
                derivativeFilterHz: 0f));

            for (var index = 0; index < 20; index++)
            {
                controller.Step(1f, 0.1f);
            }

            Assert.That(controller.Telemetry.IntegralState, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(controller.Telemetry.Integral, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Step_WhenOutputSaturatesDoesNotWindUpFurther()
        {
            var controller = new DronePidController(new DronePidSettings(
                proportionalGain: 10f,
                integralGain: 2f,
                derivativeGain: 0f,
                outputLimit: 1f,
                integralLimit: 10f,
                derivativeFilterHz: 0f));

            controller.Step(1f, 0.1f);
            var firstIntegral = controller.Telemetry.IntegralState;
            for (var index = 0; index < 20; index++)
            {
                controller.Step(1f, 0.1f);
            }

            Assert.That(controller.Telemetry.IsSaturated, Is.True);
            Assert.That(controller.Telemetry.IntegralState, Is.EqualTo(firstIntegral).Within(0.0001f));
            Assert.That(controller.Telemetry.ClampedOutput, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Reset_ClearsIntegralAndDerivativeHistory()
        {
            var controller = new DronePidController(new DronePidSettings(1f, 1f, 1f, 10f, 5f, 10f));
            controller.Step(1f, 0.1f);
            controller.Step(0f, 0.1f);

            controller.Reset();

            Assert.That(controller.Telemetry.Error, Is.Zero);
            Assert.That(controller.Telemetry.IntegralState, Is.Zero);
            Assert.That(controller.Telemetry.Derivative, Is.Zero);
            Assert.That(controller.HasHistory, Is.False);
        }

        [Test]
        public void Step_DerivativeFilterReducesStepSpikeAndSettles()
        {
            var controller = new DronePidController(new DronePidSettings(0f, 0f, 1f, 100f, 0f, 5f));
            controller.Step(0f, 0.02f);

            var spike = controller.Step(1f, 0.02f);
            var settled = 0f;
            for (var index = 0; index < 50; index++)
            {
                settled = controller.Step(1f, 0.02f);
            }

            Assert.That(spike, Is.GreaterThan(0f));
            Assert.That(spike, Is.LessThan(50f));
            Assert.That(settled, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Step_InvalidInputReturnsFiniteZeroAndResetsHistory()
        {
            var controller = new DronePidController(new DronePidSettings(1f, 1f, 1f, 10f, 5f, 5f));
            controller.Step(1f, 0.02f);

            var output = controller.Step(float.NaN, 0.02f);

            Assert.That(output, Is.Zero);
            Assert.That(float.IsNaN(controller.Telemetry.ClampedOutput), Is.False);
            Assert.That(controller.Telemetry.HadInvalidInput, Is.True);
            Assert.That(controller.HasHistory, Is.False);
        }

        [Test]
        public void ExternalActuatorSaturation_RollsBackLatestIntegralStep()
        {
            var controller = new DronePidController(new DronePidSettings(0f, 1f, 0f, 10f, 10f, 0f));
            controller.Step(1f, 0.5f);
            Assert.That(controller.Telemetry.IntegralState, Is.EqualTo(0.5f).Within(0.0001f));

            controller.ApplyActuatorSaturation(true);

            Assert.That(controller.Telemetry.IntegralState, Is.Zero.Within(0.0001f));
            Assert.That(controller.Telemetry.IsSaturated, Is.True);
            Assert.That(controller.Telemetry.RawOutput, Is.Zero.Within(0.0001f));
        }
    }
}
