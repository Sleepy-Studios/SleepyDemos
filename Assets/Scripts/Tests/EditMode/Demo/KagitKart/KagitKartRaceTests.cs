using System;
using System.Collections.Generic;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartRaceTests
    {
        private const string Root = "Assets/LoadResources/Demos/kagit_kart/Data/";
        private const double Step = KagitKartSimulationClock.Step;

        [Test]
        public void CountdownStartsOnceAndAppliesPlayerGrade()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var karts = Grid(source, track, 2);
            using var race = new KagitKartRace(source.config, track, karts, KagitKartRaceMode.SingleRace);
            int starts = 0;
            race.Signaled += signal =>
            {
                if (signal.Kind == "raceStart")
                {
                    starts++;
                }
            };
            for (int i = 0; i < 360; i++)
            {
                karts[0].Controls = new KagitKartControls { Throttle = i >= 240 ? 1 : 0 };
                race.Advance(Step, 0);
            }
            Assert.That(race.State, Is.EqualTo(KagitKartRaceState.Running));
            Assert.That(race.Countdown, Is.Zero);
            Assert.That(race.StartGrade, Is.EqualTo(KagitKartStartGrade.Perfect));
            Assert.That(karts[0].Pinned, Is.False);
            Assert.That(karts[0].BoostSource, Is.EqualTo("start"));
            Assert.That(karts[0].BoostTime, Is.EqualTo(source.config.kart.boosts["start"][0]));
            race.Advance(Step, Step);
            Assert.That(starts, Is.EqualTo(1));
        }

        [TestCase("meadow")]
        [TestCase("bosphorus")]
        [TestCase("glacier")]
        [TestCase("desk")]
        public void ThreeLapTimeTrialRecordsOrderedSplitsAndValidGhost(string id)
        {
            var source = ReadSource();
            var track = ReadTrack(id, source);
            var karts = Grid(source, track, 1);
            using var race = new KagitKartRace(source.config, track, karts, KagitKartRaceMode.TimeTrial);
            Countdown(race);
            int checkpoints = 0;
            int finalLaps = 0;
            int ends = 0;
            race.Signaled += signal =>
            {
                if (signal.Kind == "checkpoint")
                {
                    checkpoints++;
                }
                if (signal.Kind == "finalLap")
                {
                    finalLaps++;
                }
                if (signal.Kind == "raceEnd")
                {
                    ends++;
                }
            };
            double time = DriveQueries(race, track, karts);
            Assert.That(race.State, Is.EqualTo(KagitKartRaceState.Done));
            Assert.That(checkpoints, Is.EqualTo(9));
            Assert.That(finalLaps, Is.EqualTo(1));
            Assert.That(race.Results.Count, Is.EqualTo(1));
            Assert.That(race.Results[0].Estimated, Is.False);
            Assert.That(race.Entries[0].Progress.LapTimes.Count, Is.EqualTo(3));
            var ghost = race.GhostRecorder.Snapshot(race.Results[0].Time, race.Entries[0].Progress.LapTimes);
            Assert.That(ghost.IsValid(), Is.True);
            Assert.That(ghost.checkpoints.Length, Is.EqualTo(12));
            race.Advance(Step, time + Step);
            Assert.That(ends, Is.EqualTo(1));
            Assert.That(race.Results.Count, Is.EqualTo(1));
        }

        [Test]
        public void FinishWaitEstimatesPendingKartsOnceAndPreservesFinishOrder()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var karts = Grid(source, track, 2);
            using var race = new KagitKartRace(source.config, track, karts, KagitKartRaceMode.SingleRace);
            Countdown(race);
            double distance = karts[0].TrackInfo.Distance;
            double time = 0;
            while (!karts[0].Finished && time < 100)
            {
                distance += 60 * Step;
                PlaceDistance(karts[0], track, distance);
                time += Step;
                race.Advance(Step, time);
            }
            Assert.That(karts[0].Finished, Is.True);
            Assert.That(karts[0].Autopilot, Is.True);
            Assert.That(race.State, Is.EqualTo(KagitKartRaceState.Finishing));
            double playerFinish = time;
            int estimatedEvents = 0;
            race.Signaled += signal =>
            {
                if (signal.Kind == "finish" && signal.Estimated)
                {
                    estimatedEvents++;
                }
            };
            while (race.State != KagitKartRaceState.Done && time - playerFinish < 13)
            {
                time += Step;
                race.Advance(Step, time);
            }
            Assert.That(time - playerFinish, Is.EqualTo(12).Within(Step * 2));
            Assert.That(race.Results[0].CharacterId, Is.EqualTo(karts[0].Id));
            Assert.That(race.Results[0].Time, Is.EqualTo(playerFinish));
            Assert.That(race.Results[1].Estimated, Is.True);
            Assert.That(race.Results[1].Time, Is.GreaterThan(time));
            Assert.That(estimatedEvents, Is.EqualTo(1));
            race.Advance(Step, time + Step);
            Assert.That(estimatedEvents, Is.EqualTo(1));
        }

        [Test]
        public void GrandPrixCompletesFourTracksAndRestartRestoresPreviousTotals()
        {
            var source = ReadSource();
            var gp = new KagitKartGrandPrix(source.roster);
            string[] tracks = { "meadow", "bosphorus", "glacier", "desk" };
            for (int round = 0; round < 4; round++)
            {
                Assert.That(gp.TrackId, Is.EqualTo(tracks[round]));
                var track = ReadTrack(gp.TrackId, source);
                var karts = Grid(source, track, 8);
                using (var race = new KagitKartRace(source.config, track, karts, KagitKartRaceMode.GrandPrix, gp))
                {
                    Countdown(race);
                    DriveQueries(race, track, karts);
                    Assert.That(race.Results.Count, Is.EqualTo(8));
                    Assert.That(race.Results[0].Points, Is.EqualTo(15));
                    Assert.That(race.Results[0].TotalPoints, Is.EqualTo(15 * (round + 1)));
                    if (round == 0)
                    {
                        gp.RestartRound();
                        Assert.That(gp.SortedStandings()[0].Points, Is.Zero);
                        Assert.That(gp.SortedStandings()[0].LastPlace, Is.EqualTo(99));
                        Commit(gp, race.Results, source.config.race.points);
                    }
                    double finish = race.Results[0].Time;
                    race.Advance(Step, finish + 100);
                    Assert.That(gp.SortedStandings()[0].Points, Is.EqualTo(15 * (round + 1)));
                }
                Assert.That(gp.NextRound(), Is.EqualTo(round < 3));
            }
            Assert.That(gp.Completed, Is.True);
            Assert.That(gp.SortedStandings()[0].Points, Is.EqualTo(60));
            gp.RestartRound();
            Assert.That(gp.Completed, Is.False);
            Assert.That(gp.Index, Is.EqualTo(3));
            Assert.That(gp.SortedStandings()[0].Points, Is.EqualTo(45));
        }

        [Test]
        public void GrandPrixTieBreaksByLastPlaceThenTotalTime()
        {
            var source = ReadSource();
            var gp = new KagitKartGrandPrix(source.roster);
            var results = new List<KagitKartRaceResult>();
            for (int i = 0; i < 8; i++)
            {
                var result = new KagitKartRaceResult();
                Set(result, nameof(KagitKartRaceResult.CharacterId), source.roster[i].id);
                Set(result, nameof(KagitKartRaceResult.Place), i + 1);
                Set(result, nameof(KagitKartRaceResult.Time), 100.0 + i);
                results.Add(result);
            }
            Commit(gp, results, new float[] { 10, 10, 10, 10, 10, 10, 10, 10 });
            var sorted = gp.SortedStandings();
            Assert.That(sorted[0].CharacterId, Is.EqualTo(source.roster[0].id));
            Set(sorted[1], nameof(KagitKartStanding.LastPlace), 1);
            Set(sorted[1], nameof(KagitKartStanding.TotalTime), 99.0);
            Assert.That(gp.SortedStandings()[0].CharacterId, Is.EqualTo(source.roster[1].id));
            Assert.Throws<TargetInvocationException>(() => Commit(gp, results, source.config.race.points));
        }

        [Test]
        public void DisposeUnsubscribesRuleEventsAndClearsControls()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var karts = Grid(source, track, 1);
            var race = new KagitKartRace(source.config, track, karts, KagitKartRaceMode.TimeTrial);
            Countdown(race);
            int notifications = 0;
            race.Signaled += _ => notifications++;
            karts[0].Controls = new KagitKartControls { Item = true, Throttle = 1, Drift = true };
            race.Dispose();
            race.Dispose();
            Assert.That(karts[0].Controls.Item, Is.False);
            Assert.That(karts[0].Controls.Throttle, Is.Zero);
            Assert.That(karts[0].Pinned, Is.True);
            Assert.Throws<ObjectDisposedException>(() => race.Advance(Step, Step));
            var progress = race.Entries[0].Progress;
            double distance = track.Length - 1;
            for (int i = 0; i < 1500; i++)
            {
                distance = (distance + 1) % track.Length;
                progress.Advance(distance, 120, Step, (i + 1) * Step);
            }
            Assert.That(notifications, Is.Zero);
        }

        private static void Countdown(KagitKartRace race)
        {
            for (int i = 0; i < 360; i++)
            {
                race.Advance(Step, 0);
            }
        }

        // 合成沿路轨迹只验证规则会话，不能作为 AI、车辆驾驶或可玩场景验收。
        private static double DriveQueries(KagitKartRace race, KagitKartTrackQuery track, KagitKartVehicle[] karts)
        {
            double[] distances = new double[karts.Length];
            for (int i = 0; i < karts.Length; i++)
            {
                distances[i] = karts[i].TrackInfo.Distance;
            }
            double time = 0;
            while (race.State != KagitKartRaceState.Done && time < 120)
            {
                for (int i = 0; i < karts.Length; i++)
                {
                    distances[i] += (60 - i * 0.1) * Step;
                    PlaceDistance(karts[i], track, distances[i]);
                }
                time += Step;
                race.Advance(Step, time);
            }
            Assert.That(race.State, Is.EqualTo(KagitKartRaceState.Done));
            return time;
        }

        private static void PlaceDistance(KagitKartVehicle kart, KagitKartTrackQuery track, double distance)
        {
            var point = track.PointAt((float)((distance % track.Length) / track.Length), 0);
            kart.PlaceAt(point.Position, point.Heading);
        }

        private static KagitKartVehicle[] Grid(KagitKartSourceCatalog source, KagitKartTrackQuery track, int count)
        {
            var slots = track.GridSlots(count);
            var karts = new KagitKartVehicle[count];
            for (int i = 0; i < count; i++)
            {
                karts[i] = new KagitKartVehicle(source.config, track, source.roster[i], 120, i == 0);
                karts[i].PlaceAt(slots[i].Position, slots[i].Heading);
            }
            return karts;
        }

        private static void Set<T>(object target, string property, T value) =>
            target.GetType().GetProperty(property, BindingFlags.Public | BindingFlags.Instance).SetValue(target, value);

        private static void Commit(KagitKartGrandPrix gp, IReadOnlyList<KagitKartRaceResult> results, float[] points) =>
            typeof(KagitKartGrandPrix).GetMethod("Commit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(gp, new object[] { results, points });

        private static KagitKartSourceCatalog ReadSource() =>
            KagitKartSourceCatalog.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "SourceCatalog.json").text);

        private static KagitKartTrackQuery ReadTrack(string id, KagitKartSourceCatalog source)
        {
            var data = KagitKartTrackData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(
                Root + "Tracks/" + KagitKartCatalog.TrackAssetName(id) + ".json").text);
            var tuning = source.config.track;
            return new KagitKartTrackQuery(data, tuning.gridCell, tuning.waterDrop, tuning.deskDrop);
        }
    }
}
