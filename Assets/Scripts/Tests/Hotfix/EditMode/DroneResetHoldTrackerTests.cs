using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证 R 键短按、长按阈值和单次完成语义，防止重载重复触发或吞掉短按操作。
     */
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
