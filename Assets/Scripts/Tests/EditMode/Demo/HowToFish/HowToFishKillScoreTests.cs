using System;
using Hotfix.HowToFish;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class HowToFishKillScoreTests
    {
        private static HowToFishKillHit Hit(HowToFishKillMethod method = HowToFishKillMethod.Ranged) => new HowToFishKillHit
        {
            Method = method, Health = 50, MaximumHealth = 100, Damage = 50, CameraDistance = 5, PlayerDistance = 5
        };

        [Test]
        public void CompatibleBonuses_MultiplyBeyondFiveAndAddImpressive()
        {
            var hit = Hit();
            hit.Health = hit.MaximumHealth = 20; hit.Damage = 100;
            hit.Endangered = hit.Headshot = hit.PlayerAirborne = hit.TargetAirborne = hit.LastBullet = true;
            hit.PlayerDistance = hit.CameraDistance = 1;
            var result = new HowToFishKillScore().Score(hit, 1);
            Assert.That(result.Multiplier, Is.EqualTo(12.0849609375f).Within(.0001f));
            StringAssert.Contains("惊艳", result.Bonuses);
            StringAssert.Contains("空战", result.Bonuses);
            StringAssert.DoesNotContain("空中射击", result.Bonuses);
            StringAssert.DoesNotContain("飞鱼射击", result.Bonuses);
            StringAssert.DoesNotContain("新手", result.Bonuses);
        }

        [Test]
        public void FinishMethod_SeparatesMeleeAndExplosionFromRangedChecks()
        {
            var hit = Hit(HowToFishKillMethod.Melee);
            hit.Headshot = hit.PlayerAirborne = hit.TargetAirborne = hit.LastBullet = true;
            hit.CameraDistance = 30; hit.PlayerDistance = 1; hit.Damage = 150;
            Assert.That(new HowToFishKillScore().Score(hit, 1).Multiplier, Is.EqualTo(1.05f));
            hit.Method = HowToFishKillMethod.Explosion; hit.Endangered = true;
            var explosion = new HowToFishKillScore().Score(hit, 1);
            Assert.That(explosion.Multiplier, Is.EqualTo(1.25f));
            Assert.That(explosion.Bonuses, Is.EqualTo("爆炸"));
            var aimed = new HowToFishKillScore(); aimed.RecordAim(true, 0);
            Assert.That(aimed.Score(Hit(), 1).Bonuses, Is.EqualTo("新手"));
        }

        [TestCase(true, true, 1.5f)]
        [TestCase(true, false, 1.25f)]
        [TestCase(false, true, 1.25f)]
        public void AirborneChecks_SelectOnlyOneBonus(bool playerAirborne, bool targetAirborne, float expected)
        {
            var score = new HowToFishKillScore(); score.RecordAim(true, 0);
            var hit = Hit(); hit.PlayerAirborne = playerAirborne; hit.TargetAirborne = targetAirborne;
            Assert.That(score.Score(hit, 1).Multiplier, Is.EqualTo(expected));
        }

        [Test]
        public void AimAndSpinWindows_UseActualTransitionsAndTurningPauses()
        {
            foreach (float delay in new[] { .49f, .51f })
            {
                var score = new HowToFishKillScore(); score.RecordAim(true, 0); score.RecordAim(true, .4f);
                Assert.That(score.Score(Hit(), delay).Multiplier, Is.EqualTo(delay < .5f ? 1.1f : 1.01f));
            }
            foreach (float delay in new[] { .24f, .25f })
            {
                var score = new HowToFishKillScore(); score.RecordAim(true, 0); score.RecordAim(false, 1);
                Assert.That(score.Score(Hit(), 1 + delay).Multiplier, Is.EqualTo(delay < .25f ? 1.01f : 1.2f));
            }
            foreach (float turnGap in new[] { .4f, .6f })
            {
                var score = new HowToFishKillScore(); score.RecordLook(160, 0); score.RecordLook(160, turnGap);
                Assert.That(score.Score(Hit(HowToFishKillMethod.Melee), turnGap + .1f).Multiplier,
                    Is.EqualTo(turnGap < .5f ? 1.575f : 1.05f).Within(.00001f));
            }
            var expired = new HowToFishKillScore(); expired.RecordLook(320, 0);
            Assert.That(expired.Score(Hit(HowToFishKillMethod.Melee), 1.01f).Multiplier, Is.EqualTo(1.05f));
        }

        [Test]
        public void ChainsAndAttackDuration_HaveIndependentWindowsAndResetOnKill()
        {
            var score = new HowToFishKillScore();
            for (int i = 0; i < 15; i++)
                Assert.That(score.Score(Hit(HowToFishKillMethod.Melee), i).Multiplier,
                    Is.EqualTo(1.05f * (1 + Math.Min(i, 10) * .05f)).Within(.00001f));
            Assert.That(score.Score(Hit(HowToFishKillMethod.Melee), 18).Multiplier, Is.EqualTo(1.05f));
            foreach (float interval in new[] { .1f, 1f })
            {
                var attacks = new HowToFishKillScore();
                for (int i = 0; i < 6; i++) attacks.RecordAttack(interval, i);
                Assert.That(attacks.Score(Hit(HowToFishKillMethod.Melee), 6).Multiplier,
                    Is.EqualTo(interval == 1 ? 1.155f : 1.05f).Within(.00001f));
                Assert.That(attacks.Score(Hit(HowToFishKillMethod.Melee), 10).Multiplier, Is.EqualTo(1.05f));
            }
            var idle = new HowToFishKillScore(); idle.RecordAttack(6, 0); idle.RecordAttack(.1f, 31);
            Assert.That(idle.Score(Hit(HowToFishKillMethod.Melee), 32).Multiplier, Is.EqualTo(1.05f));
        }

        [Test]
        public void KillstealOverkillAndInvalidDamage_RespectTargetFacts()
        {
            var hit = Hit(HowToFishKillMethod.Melee);
            hit.Health = hit.Damage = 30; hit.FirstPlayerHit = true;
            Assert.That(new HowToFishKillScore().Score(hit, 1).Multiplier, Is.EqualTo(1.365f).Within(.00001f));
            hit.FirstPlayerHit = false;
            Assert.That(new HowToFishKillScore().Score(hit, 1).Multiplier, Is.EqualTo(1.05f));
            hit = Hit(); hit.Damage = 121;
            var regular = new HowToFishKillScore(); regular.RecordAim(true, 0);
            Assert.That(regular.Score(hit, 1).Multiplier, Is.EqualTo(1.25f));
            hit.Boss = true;
            var boss = new HowToFishKillScore(); boss.RecordAim(true, 0);
            Assert.That(boss.Score(hit, 1).Multiplier, Is.EqualTo(1.01f));
            foreach (float invalid in new[] { -1f, 0, 49, float.NaN, float.PositiveInfinity })
            {
                hit.Damage = invalid;
                Assert.Throws<ArgumentException>(() => new HowToFishKillScore().Score(hit, 1));
            }
        }
    }
}
