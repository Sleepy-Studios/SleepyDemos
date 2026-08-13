using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    public sealed class DroneMotorModelTests
    {
        [Test]
        public void Step_UsesFirstOrderResponseWithoutOvershoot()
        {
            var motor = new DroneMotorModel(new DroneMotorSettings(
                responseTime: 0.2f,
                maximumRpm: 6000f,
                thrustCoefficient: 0.0000001f,
                reactionTorqueCoefficient: 0.02f));

            var first = motor.Step(1f, 0.02f);
            var previous = first.NormalizedOutput;
            for (var index = 0; index < 100; index++)
            {
                var state = motor.Step(1f, 0.02f);
                Assert.That(state.NormalizedOutput, Is.GreaterThanOrEqualTo(previous));
                Assert.That(state.NormalizedOutput, Is.LessThanOrEqualTo(1f));
                previous = state.NormalizedOutput;
            }

            Assert.That(first.NormalizedOutput, Is.GreaterThan(0f).And.LessThan(1f));
            Assert.That(previous, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void Step_ClampsCommandAndProducesFiniteForceValues()
        {
            var motor = new DroneMotorModel(new DroneMotorSettings(0f, 5000f, 0.0000001f, 0.03f));

            var state = motor.Step(2f, 0.02f);

            Assert.That(state.NormalizedOutput, Is.EqualTo(1f));
            Assert.That(state.Rpm, Is.EqualTo(5000f));
            Assert.That(state.ThrustNewtons, Is.GreaterThan(0f));
            Assert.That(state.ReactionTorqueNewtonMeters, Is.GreaterThan(0f));
            Assert.That(float.IsNaN(state.ThrustNewtons), Is.False);
        }

        [Test]
        public void Step_InvalidDeltaTimeReturnsSafeZeroState()
        {
            var motor = new DroneMotorModel(new DroneMotorSettings(0.2f, 5000f, 0.0000001f, 0.03f));

            var state = motor.Step(1f, 0f);

            Assert.That(state.NormalizedOutput, Is.Zero);
            Assert.That(state.HadInvalidInput, Is.True);
        }

        [Test]
        public void UpdateSettings_PreservesCurrentRpmWithoutResettingMotor()
        {
            var motor = new DroneMotorModel(new DroneMotorSettings(0f, 10000f, 0.0000001f, 0.03f));
            var before = motor.Step(0.6f, 0.02f);

            motor.UpdateSettings(
                new DroneMotorSettings(0.08f, 12000f, 0.0000002f, 0.02f),
                preserveCurrentRpm: true);
            var after = motor.Step(0.5f, 0.000001f);

            Assert.That(after.Rpm, Is.EqualTo(before.Rpm).Within(1f));
            Assert.That(after.NormalizedOutput, Is.GreaterThan(0f));
            Assert.That(after.HadInvalidInput, Is.False);
        }
    }
}
