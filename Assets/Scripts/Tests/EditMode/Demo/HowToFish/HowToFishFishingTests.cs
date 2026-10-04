using Hotfix.HowToFish;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class HowToFishFishingTests
    {
        [Test]
        public void ControlledReeling_LandsFishWhileHoldingContinuouslyBreaksLine()
        {
            var controlled = ReadyToReel();
            bool reel = true;
            for (int i = 0; i < 2000 && controlled.Phase == HowToFishFishingPhase.Reeling; i++)
            {
                if (controlled.Tension > 0.65f) reel = false;
                if (controlled.Tension < 0.25f) reel = true;
                controlled.Tick(0.02f, reel);
            }
            Assert.That(controlled.Phase, Is.EqualTo(HowToFishFishingPhase.Landed));
            var rushed = ReadyToReel();
            for (int i = 0; i < 500 && rushed.Phase == HowToFishFishingPhase.Reeling; i++) rushed.Tick(0.02f, true);
            Assert.That(rushed.Phase, Is.EqualTo(HowToFishFishingPhase.Escaped));
        }

        [Test]
        public void MissedBite_EscapesAndCanStartAnotherCast()
        {
            var fishing = new HowToFishFishingState();
            fishing.BeginCharge();
            fishing.Tick(1, false);
            Assert.That(fishing.Cast(), Is.GreaterThan(20));
            fishing.EnterWater(1, 1);
            fishing.Tick(1.1f, false);
            Assert.That(fishing.Phase, Is.EqualTo(HowToFishFishingPhase.Bite));
            fishing.Tick(2.3f, false);
            Assert.That(fishing.Phase, Is.EqualTo(HowToFishFishingPhase.Escaped));
            Assert.That(fishing.BeginCharge(), Is.True);
            Assert.That(fishing.Charge, Is.Zero);
        }

        [Test]
        public void PullBack_RequiresPressesAndTimesOutWithoutThem()
        {
            var fishing = ReadyToReel(true);
            fishing.Tick(1, true);
            Assert.That(fishing.Progress, Is.Zero, "持续按住不应模拟连按拉竿。");
            for (int press = 0; press < 20 && fishing.Phase == HowToFishFishingPhase.Reeling; press++)
            { fishing.Pull(); fishing.Tick(.1f, false); }
            Assert.That(fishing.Phase, Is.EqualTo(HowToFishFishingPhase.Landed));
            fishing = ReadyToReel(true);
            fishing.Tick(5, true);
            Assert.That(fishing.Phase, Is.EqualTo(HowToFishFishingPhase.Escaped));
        }

        private static HowToFishFishingState ReadyToReel(bool pullBack = false)
        {
            var fishing = new HowToFishFishingState();
            fishing.BeginCharge();
            fishing.Cast();
            fishing.EnterWater(1, 2);
            fishing.Tick(1, false);
            Assert.That(fishing.Hook(pullBack), Is.True);
            return fishing;
        }
    }
}
