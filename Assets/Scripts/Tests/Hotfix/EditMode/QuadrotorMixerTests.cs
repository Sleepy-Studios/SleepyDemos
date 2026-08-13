using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    public sealed class QuadrotorMixerTests
    {
        [Test]
        public void Mix_CollectiveRaisesAllMotorsEqually()
        {
            var output = QuadrotorMixer.Mix(0.5f, 0f, 0f, 0f);

            AssertAll(output, 0.5f, 0.5f, 0.5f, 0.5f);
            Assert.That(output.IsSaturated, Is.False);
        }

        [Test]
        public void Mix_PositiveRollRaisesLeftAndLowersRightMotors()
        {
            var output = QuadrotorMixer.Mix(0.5f, 0.1f, 0f, 0f);

            AssertAll(output, 0.6f, 0.4f, 0.6f, 0.4f);
        }

        [Test]
        public void Mix_PositivePitchRaisesRearAndLowersFrontMotors()
        {
            var output = QuadrotorMixer.Mix(0.5f, 0f, 0.1f, 0f);

            AssertAll(output, 0.4f, 0.4f, 0.6f, 0.6f);
        }

        [Test]
        public void Mix_PositiveYawRaisesCounterClockwisePair()
        {
            var output = QuadrotorMixer.Mix(0.5f, 0f, 0f, 0.1f);

            AssertAll(output, 0.6f, 0.4f, 0.4f, 0.6f);
        }

        [Test]
        public void Mix_DesaturatesByShiftingCollectiveBeforeScalingAttitude()
        {
            var output = QuadrotorMixer.Mix(0.95f, 0.2f, 0f, 0f);

            Assert.That(output.FrontLeft, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(output.FrontRight, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(output.RearLeft, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(output.RearRight, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(output.IsSaturated, Is.True);
            Assert.That(output.AttitudeScale, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Mix_WhenAttitudeRangeExceedsOneScalesItWithoutInvalidValues()
        {
            var output = QuadrotorMixer.Mix(0.5f, 1f, 1f, 1f);

            Assert.That(output.FrontLeft, Is.InRange(0f, 1f));
            Assert.That(output.FrontRight, Is.InRange(0f, 1f));
            Assert.That(output.RearLeft, Is.InRange(0f, 1f));
            Assert.That(output.RearRight, Is.InRange(0f, 1f));
            Assert.That(output.AttitudeScale, Is.LessThan(1f));
            Assert.That(output.IsSaturated, Is.True);
        }

        private static void AssertAll(
            QuadrotorMotorOutput output,
            float frontLeft,
            float frontRight,
            float rearLeft,
            float rearRight)
        {
            Assert.That(output.FrontLeft, Is.EqualTo(frontLeft).Within(0.0001f));
            Assert.That(output.FrontRight, Is.EqualTo(frontRight).Within(0.0001f));
            Assert.That(output.RearLeft, Is.EqualTo(rearLeft).Within(0.0001f));
            Assert.That(output.RearRight, Is.EqualTo(rearRight).Within(0.0001f));
        }
    }
}

