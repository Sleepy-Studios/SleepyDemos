using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    public sealed class DroneResetHoldTrackerTests
    {
        [Test]
        public void ReleaseBeforeThreshold_IsShortPressAndClearsProgress()
        {
            var tracker = new DroneResetHoldTracker(5f);
            tracker.Begin();

            Assert.That(tracker.Step(4.99f), Is.False);
            Assert.That(tracker.Release(), Is.EqualTo(DroneResetReleaseResult.ShortPress));
            Assert.That(tracker.Progress, Is.Zero);
        }

        [Test]
        public void ReachingThreshold_CompletesExactlyOnceAndSuppressesShortPress()
        {
            var tracker = new DroneResetHoldTracker(5f);
            tracker.Begin();

            Assert.That(tracker.Step(5f), Is.True);
            Assert.That(tracker.Step(1f), Is.False);
            Assert.That(tracker.Release(), Is.EqualTo(DroneResetReleaseResult.None));
        }

        [Test]
        public void ExceedingThreshold_CompletesOnce()
        {
            var tracker = new DroneResetHoldTracker(5f);
            tracker.Begin();

            Assert.That(tracker.Step(6f), Is.True);
            Assert.That(tracker.Step(6f), Is.False);
        }
    }
}
