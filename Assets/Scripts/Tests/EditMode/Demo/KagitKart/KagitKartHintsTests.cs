using System;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartHintsTests
    {
        [Test]
        public void CountdownAndPauseDoNotSpendTutorialTimeAndHintsWaitUntilCompletion()
        {
            var hints = new KagitKartHints();
            int completions = 0;
            hints.TutorialCompleted += () => completions++;
            hints.OnCountdown(2, false);
            Assert.That(hints.TutorialVisible, Is.False);
            hints.OnCountdown(3, false);
            hints.OnItemGet();
            hints.Advance(3, true, true, false, false, false, default, 0);
            hints.Advance(0, true, true, true, true, false, default, 0);
            Assert.That(hints.TutorialVisible, Is.True);
            Assert.That(hints.Current, Is.Null);
            Assert.That(hints.CopyHistory(), Is.Empty, "排队和教程不得提前标记情境提示已看。");
            hints.Advance(6.9f, true, true, true, false, false, default, 0);
            Assert.That(hints.TutorialVisible, Is.True);
            hints.Advance(0.11f, true, true, true, false, false, default, 0);
            Assert.That(hints.TutorialVisible, Is.False);
            Assert.That(completions, Is.EqualTo(1));
            Assert.That(hints.Current, Is.EqualTo(KagitKartHintKind.Item));
            Assert.That(hints.CopyHistory(), Is.EquivalentTo(new[] { KagitKartHintKind.Item }));
        }

        [Test]
        public void TeardownKeepsTutorialUnseenButLeavingRaceViewCompletesIt()
        {
            var hints = new KagitKartHints();
            int completions = 0;
            hints.TutorialCompleted += () => completions++;
            hints.OnCountdown(3, false);
            hints.Unbind();
            Assert.That(hints.TutorialVisible, Is.False);
            Assert.That(completions, Is.Zero, "重赛与离场不能提前记录 SeenTutorial。");
            hints.OnCountdown(3, false);
            Assert.That(hints.TutorialVisible, Is.True);
            hints.Advance(0, false, true, false, false, false, default, 0);
            Assert.That(completions, Is.EqualTo(1));
            hints.OnCountdown(3, true);
            Assert.That(hints.TutorialVisible, Is.False);
        }

        [Test]
        public void RealThresholdsQueueInSourceOrderAndOnlyDisplayedHintsPersist()
        {
            var hints = new KagitKartHints();
            var track = Track();
            hints.Bind(track);
            var info = new KagitKartQueryInfo { Index = 0, T = 0.05f };
            hints.Advance(0, true, true, true, true, false, info, 9);
            Assert.That(hints.Current, Is.Null, "暂停不弹出新卡，但原作保留排队。");
            hints.OnDriftTier(2);
            hints.OnDriftTier(1);
            hints.OnItemGet();
            hints.OnItemGet();
            hints.Advance(0, true, true, true, false, false, info, 9);
            Assert.That(hints.Current, Is.EqualTo(KagitKartHintKind.Corner));
            Assert.That(hints.CopyHistory(), Is.EquivalentTo(new[] { KagitKartHintKind.Corner }));
            hints.Advance(3.81f, true, true, true, false, false, info, 9);
            Assert.That(hints.Current, Is.EqualTo(KagitKartHintKind.Ramp));
            hints.Advance(3.81f, true, true, true, false, false, info, 9);
            Assert.That(hints.Current, Is.EqualTo(KagitKartHintKind.Tier));
            hints.Advance(3.81f, true, true, true, false, false, info, 9);
            Assert.That(hints.Current, Is.EqualTo(KagitKartHintKind.Item));
            hints.Advance(3.81f, true, true, true, false, false, info, 9);
            Assert.That(hints.Current, Is.Null);
            var restored = new KagitKartHints(hints.CopyHistory());
            restored.OnDriftTier(1);
            restored.OnItemGet();
            restored.Advance(0, true, true, true, false, false, info, 9);
            Assert.That(restored.Current, Is.Null);
            Assert.Throws<ArgumentException>(() => new KagitKartHints(new[] { (KagitKartHintKind)999 }));
        }

        [Test]
        public void PortraitNoteAppearsInMenusAndLastsLongerThanDrivingHints()
        {
            var hints = new KagitKartHints();
            hints.Advance(0, false, false, false, false, true, default, 0);
            Assert.That(hints.Current, Is.Null);
            hints.Advance(0, false, true, false, false, true, default, 0);
            Assert.That(hints.Current, Is.EqualTo(KagitKartHintKind.Portrait));
            hints.Advance(4.49f, false, true, false, false, true, default, 0);
            Assert.That(hints.Current, Is.EqualTo(KagitKartHintKind.Portrait));
            hints.Advance(0.02f, false, true, false, false, true, default, 0);
            Assert.That(hints.Current, Is.Null);
            hints.Unbind();
            hints.Advance(0, false, true, false, false, true, default, 0);
            Assert.That(hints.Current, Is.Null, "已显示竖屏提示在重赛后仍不重复。");
        }

        private static KagitKartTrackGeometry Track()
        {
            var samples = new KagitKartTrackSamples { tx = new float[100], tz = new float[100] };
            for (int i = 0; i < 100; i++)
            {
                samples.tx[i] = Mathf.Sin(i * 0.02f);
                samples.tz[i] = Mathf.Cos(i * 0.02f);
            }
            return new KagitKartTrackGeometry
            {
                sampleCount = 100,
                length = 500,
                samples = samples,
                ramps = new[] { new KagitKartRamp { t = 0.1f, trick = false }, new KagitKartRamp { t = 0.2f, trick = true } }
            };
        }
    }
}
