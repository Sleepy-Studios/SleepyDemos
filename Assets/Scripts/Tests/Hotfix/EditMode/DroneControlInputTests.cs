using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    public sealed class DroneControlInputTests
    {
        [Test]
        public void Create_ClampsEveryAxisToNormalizedRange()
        {
            var input = DroneControlInput.Create(2f, -2f, 1.5f, -1.5f);

            Assert.That(input.Lift, Is.EqualTo(1f));
            Assert.That(input.Yaw, Is.EqualTo(-1f));
            Assert.That(input.Forward, Is.EqualTo(1f));
            Assert.That(input.Right, Is.EqualTo(-1f));
        }

        [Test]
        public void Create_ReplacesInvalidAxisWithZero()
        {
            var input = DroneControlInput.Create(float.NaN, float.PositiveInfinity, 0.25f, -0.5f);

            Assert.That(input.Lift, Is.Zero);
            Assert.That(input.Yaw, Is.Zero);
            Assert.That(input.Forward, Is.EqualTo(0.25f));
            Assert.That(input.Right, Is.EqualTo(-0.5f));
            Assert.That(input.HadInvalidValue, Is.True);
        }
    }
}

