using System;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartRuleTests
    {
        [Test]
        public void RandomMatchesOriginalAndForkDoesNotConsumeParent()
        {
            var random = new KagitKartRandom(12345);
            var ai = random.Fork("ai");
            double[] expected = { 0.9797282677609473, 0.3067522644996643, 0.484205421525985, 0.817934412509203 };
            double[] expectedAi = { 0.3489504905883223, 0.4254279488231987, 0.8727936572395265, 0.05130691174417734 };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(random.Next(), Is.EqualTo(expected[i]));
                Assert.That(ai.Next(), Is.EqualTo(expectedAi[i]));
            }
        }

        [TestCase(0, KagitKartStartGrade.Burnout)]
        [TestCase(239, KagitKartStartGrade.Perfect)]
        [TestCase(288, KagitKartStartGrade.Good)]
        [TestCase(350, KagitKartStartGrade.None)]
        public void CountdownStartsOnceAndUsesOriginalThrottleWindows(int pressStep, KagitKartStartGrade grade)
        {
            var countdown = new KagitKartCountdown();
            int starts = 0;
            for (int i = 0; i < 500; i++)
                if (countdown.Advance(i >= pressStep))
                {
                    starts++;
                }
            Assert.That(starts, Is.EqualTo(1));
            Assert.That(countdown.Number, Is.Zero);
            Assert.That(countdown.Grade, Is.EqualTo(grade));
        }

        [Test]
        public void ClockClampsBacklogAndDoesNotCatchUpAfterPause()
        {
            var clock = new KagitKartSimulationClock();
            int simulated = 0;
            Assert.That(clock.Advance(1, false, _ => simulated++), Is.EqualTo(8));
            Assert.That(clock.Alpha, Is.LessThan(1));
            Assert.That(clock.Advance(10, true, _ => simulated++), Is.Zero);
            Assert.That(clock.Advance(KagitKartSimulationClock.Step, false, _ => simulated++), Is.EqualTo(1));
            Assert.That(simulated, Is.EqualTo(9));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.NaN, false, _ => { }));
        }

        [TestCase("meadow")]
        [TestCase("bosphorus")]
        [TestCase("glacier")]
        [TestCase("desk")]
        public void OriginalTrackCanCompleteThreeValidatedLaps(string id)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/LoadResources/Demos/kagit_kart/Data/Tracks/" + KagitKartCatalog.TrackAssetName(id) + ".json");
            Assert.That(asset, Is.Not.Null);
            var track = KagitKartTrackData.Parse(asset.text);
            double length = track.data.length;
            var progress = new KagitKartRaceProgress(length, 3, length - 5);
            double time = 0;
            double travel = length - 5;
            for (int i = 0; i < 30000 && !progress.Finished; i++)
            {
                time += KagitKartSimulationClock.Step;
                travel += 25 * KagitKartSimulationClock.Step;
                progress.Advance(travel % length, 25, KagitKartSimulationClock.Step, time);
            }
            Assert.That(progress.Finished, Is.True);
            Assert.That(progress.LapTimes.Count, Is.EqualTo(3));
            foreach (double lapTime in progress.LapTimes)
                Assert.That(lapTime, Is.EqualTo(length / 25).Within(0.02));
            double finish = progress.FinishTime;
            progress.Advance(0, 25, KagitKartSimulationClock.Step, time + 10);
            Assert.That(progress.FinishTime, Is.EqualTo(finish));
            Assert.That(KagitKartCatalog.TrackName(id), Does.Not.Contain(id));
        }

        [Test]
        public void SkippedCheckpointsAndReverseLineCrossingsDoNotAwardLap()
        {
            var progress = new KagitKartRaceProgress(1200, 3, 1195);
            progress.Advance(0, 25, KagitKartSimulationClock.Step, 0);
            Assert.That(progress.Lap, Is.EqualTo(1));
            progress.Advance(1199, -25, KagitKartSimulationClock.Step, 1);
            progress.Advance(0, 25, KagitKartSimulationClock.Step, 2);
            Assert.That(progress.Lap, Is.EqualTo(1));
            progress.Advance(800, 25, KagitKartSimulationClock.Step, 3);
            progress.Advance(1199, 25, KagitKartSimulationClock.Step, 4);
            progress.Advance(0, 25, KagitKartSimulationClock.Step, 5);
            Assert.That(progress.PassedCheckpoints, Is.Zero);
            Assert.That(progress.Lap, Is.EqualTo(1));
            Assert.That(progress.Finished, Is.False);
        }

        [Test]
        public void WrongWayRequiresSustainedDirectionAndClearsDuringRescue()
        {
            var progress = new KagitKartRaceProgress(1200, 3, 1195);
            progress.UpdateWrongWay(-1, 25, false, 1.49);
            Assert.That(progress.WrongWay, Is.False);
            progress.UpdateWrongWay(-1, 25, false, 0.02);
            Assert.That(progress.WrongWay, Is.True);
            progress.UpdateWrongWay(-1, 25, true, 0.5);
            Assert.That(progress.WrongWay, Is.False);
        }

        [Test]
        public void GhostInterpolatesAcrossPiAndRejectsCorruptRecords()
        {
            var data = new KagitKartGhostData
            {
                frames = new[] { 0f, 0, 0, 3.1f, 10, 0, 0, -3.1f },
                checkpoints = new[] { 0.05 },
                total = 0.05,
                lapTimes = new[] { 0.05 }
            };
            Assert.That(data.IsValid(), Is.True);
            Assert.That(KagitKartGhostPlayback.TrySample(data, 0.025, out var position, out float heading), Is.True);
            Assert.That(position.x, Is.EqualTo(5).Within(0.001));
            Assert.That(heading, Is.EqualTo(Math.PI).Within(0.001));
            Assert.That(KagitKartGhostPlayback.TrySample(data, 0.05, out _, out _), Is.False);
            data.frames[0] = float.NaN;
            Assert.That(data.IsValid(), Is.False);
        }

        [Test]
        public void GhostSnapshotIsIndependentAndUsesOriginalSamplingRate()
        {
            var recorder = new KagitKartGhostRecorder();
            for (int i = 0; i <= 120; i++)
                recorder.Record(new Vector3(i / 12f, 0, 0), 0, i / 120.0);
            recorder.Mark(1);
            var data = recorder.Snapshot(1, new[] { 1.0 });
            Assert.That(data.IsValid(), Is.True);
            Assert.That(data.frames.Length / 4, Is.InRange(20, 21));
            data.frames[0] = 999;
            Assert.That(recorder.Snapshot(1, new[] { 1.0 }).frames[0], Is.Zero);
        }

        [Test]
        public void GhostMillisecondRoundingAllowsHalfMillisecondButRejectsFutureSplits()
        {
            var recorder = new KagitKartGhostRecorder();
            double finish = 122 / 120.0;
            for (int i = 0; i < 122; i++)
            {
                recorder.Record(Vector3.zero, 0, i / 120.0);
            }
            recorder.Mark(finish);
            var data = recorder.Snapshot(finish, new[] { finish });
            Assert.That(data.checkpoints[0], Is.GreaterThan(finish));
            Assert.That(data.IsValid(), Is.True);
            data.checkpoints[0] = finish + 0.001;
            Assert.That(data.IsValid(), Is.False);
        }

        [Test]
        public void ItemLotteryPreservesOriginalOrderAndScissorsGate()
        {
            Assert.That(KagitKartCatalog.PickItem(1, 0, true), Is.EqualTo("gum"));
            Assert.That(KagitKartCatalog.PickItem(1, 0.5, true), Is.EqualTo("plane"));
            Assert.That(KagitKartCatalog.PickItem(1, 0.9, true), Is.EqualTo("rocket"));
            Assert.That(KagitKartCatalog.PickItem(8, 0.999, true), Is.EqualTo("scissors"));
            for (int place = 1; place <= 8; place++)
            {
                for (int i = 0; i < 1000; i++)
                {
                    string item = KagitKartCatalog.PickItem(place, i / 1000.0, false);
                    Assert.That(item, Is.Not.EqualTo("scissors"));
                    Assert.That(KagitKartCatalog.ItemName(item), Does.Not.Contain(item));
                }
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => KagitKartCatalog.PickItem(1, double.NaN, false));
        }
    }
}
