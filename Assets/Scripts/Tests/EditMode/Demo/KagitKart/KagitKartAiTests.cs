using System;
using System.Reflection;
using Core.Runtime.Inputs;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartAiTests
    {
        private const string Root = "Assets/LoadResources/Demos/kagit_kart/Data/";
        private const double Step = KagitKartSimulationClock.Step;
        private static readonly PropertyInfo SpeedProperty = typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.Speed));
        private static readonly PropertyInfo VelocityProperty = typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.Velocity));
        private static readonly PropertyInfo ProgressProperty = typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.Progress));
        private static readonly PropertyInfo PlaceProperty = typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.Place));
        private static readonly PropertyInfo ItemProperty = typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.Item));
        private static readonly PropertyInfo ItemCountProperty = typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.ItemCount));
        private static readonly PropertyInfo InkProperty = typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.InkTime));
        private static readonly FieldInfo PreviousControlsField = typeof(KagitKartVehicle).GetField("previousControls", BindingFlags.Instance | BindingFlags.NonPublic);

        [TestCase("meadow", 80)]
        [TestCase("meadow", 120)]
        [TestCase("meadow", 200)]
        [TestCase("bosphorus", 80)]
        [TestCase("bosphorus", 120)]
        [TestCase("bosphorus", 200)]
        [TestCase("glacier", 80)]
        [TestCase("glacier", 120)]
        [TestCase("glacier", 200)]
        [TestCase("desk", 80)]
        [TestCase("desk", 120)]
        [TestCase("desk", 200)]
        public void DriverCommandsMatchOriginalAiOnIdenticalStateTape(string trackId, int speedClass)
        {
            var source = ReadSource();
            var data = ReadTrack(trackId);
            var tuning = source.config.track;
            var track = new KagitKartTrackQuery(data, tuning.gridCell, tuning.waterDrop, tuning.deskDrop);
            var plan = ReadPlan(trackId, data);
            var karts = new KagitKartVehicle[8];
            for (int i = 0; i < karts.Length; i++)
            {
                karts[i] = new KagitKartVehicle(source.config, track, source.roster[i], speedClass, i == 0);
            }
            using var race = new KagitKartRace(source.config, track, karts, KagitKartRaceMode.SingleRace);
            for (int i = 0; i < 360; i++)
            {
                race.Advance(Step, 0);
            }
            SetAutopilot(karts[0]);
            // 这里隔离车手决策，输入带不执行道具效果；完整模拟由另一组用例验收。
            using var items = new KagitKartItems(source.config, track, karts, new KagitKartRandom(12345), speedClass, true);
            using var ai = new KagitKartAi(source.config, track, plan, race, items, karts, new KagitKartRandom(12345).Fork("ai"), speedClass);
            var fixture = JsonUtility.FromJson<Fixtures>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalAi.json").text);
            Assert.That(fixture.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            var run = Array.Find(fixture.runs, value => value.track == trackId && value.speedClass == speedClass);
            string[] ids = { "rocket", "rocket3", "gum", "plane", "homing", "ink", "foil", "scissors" };
            var samples = data.data.samples;
            int frame = 0;
            for (int step = 0; step < 2400; step++)
            {
                for (int i = 0; i < karts.Length; i++)
                {
                    var kart = karts[i];
                    int index = (data.data.startIndex + 40 + step / 4 + i * 8) % data.data.sampleCount;
                    float lateral = (float)(Math.Sin(step * 0.03 + i) * 0.5);
                    var position = new Vector3((float)(samples.px[index] + (double)samples.hx[index] * lateral), samples.py[index],
                        (float)(samples.pz[index] + (double)samples.hz[index] * lateral));
                    float heading = (float)(samples.heading[index] + Math.Sin(step / 60.0 + i) * 0.04);
                    kart.PlaceAt(position, heading);
                    float speed = (float)(kart.BaseTop * 0.85);
                    SpeedProperty.SetValue(kart, speed);
                    VelocityProperty.SetValue(kart, new Vector3((float)(Math.Sin(heading) * speed), 0, (float)(Math.Cos(heading) * speed)));
                    ProgressProperty.SetValue(kart, step / 4.0 + i * 8);
                    PlaceProperty.SetValue(kart, 8 - i);
                    ItemProperty.SetValue(kart, ids[i]);
                    ItemCountProperty.SetValue(kart, ids[i] == "rocket3" ? 3 : 1);
                    InkProperty.SetValue(kart, step >= 720 && step < 960 ? 1f : 0f);
                }
                ai.Advance(Step, (step + 1) * Step);
                if (frame < run.frames.Length && run.frames[frame].step == step)
                {
                    var expectedFrame = run.frames[frame++];
                    var expected = expectedFrame.controls;
                    for (int i = 0; i < karts.Length; i++)
                    {
                        string message = trackId + "/" + speedClass + " step " + step + " " + karts[i].Id;
                        var actual = karts[i].Controls;
                        Assert.That(actual.Steer, Is.EqualTo(expected[i].steer).Within(0.0003), message);
                        Assert.That(actual.Throttle, Is.EqualTo(expected[i].throttle).Within(0.00001), message);
                        Assert.That(actual.Brake, Is.EqualTo(expected[i].brake).Within(0.00001), message);
                        Assert.That(actual.Drift, Is.EqualTo(expected[i].drift), message);
                        Assert.That(actual.Item, Is.EqualTo(expected[i].item), message);
                        Assert.That(actual.LookBack, Is.False, message);
                    }
                }
                for (int i = 0; i < karts.Length; i++)
                {
                    PreviousControlsField.SetValue(karts[i], karts[i].Controls);
                }
            }
            Assert.That(frame, Is.EqualTo(run.frames.Length));
        }

        [TestCase("meadow", 80)]
        [TestCase("meadow", 120)]
        [TestCase("meadow", 200)]
        [TestCase("bosphorus", 80)]
        [TestCase("bosphorus", 120)]
        [TestCase("bosphorus", 200)]
        [TestCase("glacier", 80)]
        [TestCase("glacier", 120)]
        [TestCase("glacier", 200)]
        [TestCase("desk", 80)]
        [TestCase("desk", 120)]
        [TestCase("desk", 200)]
        public void AutopilotActuallyDrivesThreeLapsAtEverySpeedClass(string trackId, int speedClass)
        {
            using var session = CreateSession(trackId, speedClass, KagitKartRaceMode.TimeTrial);
            SetAutopilot(session.Player);
            int drifts = 0;
            int uses = 0;
            session.Items.Signaled += signal =>
            {
                if (signal.Kind == "itemUse")
                {
                    uses++;
                }
            };
            session.Player.Signaled += (_, signal) =>
            {
                if (signal == KagitKartVehicleSignal.DriftStart)
                {
                    drifts++;
                }
            };
            for (int i = 0; i < 120 * 480 && session.Race.State != KagitKartRaceState.Done; i++)
            {
                session.AdvanceFrame(Step, default);
            }
            Assert.That(session.Race.State, Is.EqualTo(KagitKartRaceState.Done),
                "lap=" + session.Race.Entries[0].Progress.Lap + " progress=" + session.Player.Progress + " speed=" + session.Player.Speed);
            Assert.That(session.Race.Results[0].Estimated, Is.False);
            Assert.That(session.Race.Entries[0].Progress.LapTimes.Count, Is.EqualTo(3));
            Assert.That(uses, Is.EqualTo(3));
            Assert.That(drifts, Is.GreaterThan(0));
            Assert.That(session.Race.GhostRecorder.Snapshot(session.Race.Results[0].Time,
                session.Race.Entries[0].Progress.LapTimes).IsValid(), Is.True);
        }

        [TestCase("meadow")]
        [TestCase("bosphorus")]
        [TestCase("glacier")]
        [TestCase("desk")]
        public void SevenCpuRaceUsesItemsAndProducesCompleteClassification(string trackId)
        {
            using var session = CreateSession(trackId, 120, KagitKartRaceMode.SingleRace);
            SetAutopilot(session.Player);
            int uses = 0;
            session.Items.Signaled += signal =>
            {
                if (signal.Kind == "itemUse" && !signal.Kart.IsPlayer)
                {
                    uses++;
                }
            };
            for (int i = 0; i < 120 * 480 && session.Race.State != KagitKartRaceState.Done; i++)
            {
                session.AdvanceFrame(Step, default);
            }
            Assert.That(session.Race.State, Is.EqualTo(KagitKartRaceState.Done));
            Assert.That(session.Karts.Count, Is.EqualTo(8));
            Assert.That(session.Race.Results.Count, Is.EqualTo(8));
            Assert.That(uses, Is.GreaterThan(0));
            foreach (var result in session.Race.Results)
            {
                Assert.That(result.Time, Is.GreaterThan(0));
                Assert.That(double.IsInfinity(result.Time), Is.False);
            }
        }

        [Test]
        public void PauseFreezesFullSimulationAndSessionDisposeClearsHeldInput()
        {
            var session = CreateSession("meadow", 120, KagitKartRaceMode.SingleRace);
            for (int i = 0; i < 600; i++)
            {
                session.AdvanceFrame(Step, new KagitKartControls { Throttle = 1 });
            }
            var position = session.Player.Position;
            double time = session.RaceTime;
            session.Pause.SetApplicationFocus(false);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(session.AdvanceFrame(0.1, new KagitKartControls { Throttle = 1, Drift = true, Item = true }), Is.Zero);
            }
            Assert.That(session.Player.Position, Is.EqualTo(position));
            Assert.That(session.RaceTime, Is.EqualTo(time));
            Assert.That(session.Player.Controls.Throttle, Is.Zero);
            session.Pause.SetApplicationFocus(true);
            Assert.That(session.Pause.IsPaused, Is.True);
            Assert.That(session.Pause.TryResume(), Is.True);
            session.AdvanceFrame(Step, default);
            Assert.That(session.RaceTime, Is.EqualTo(time + Step).Within(1e-9));
            session.Dispose();
            session.Dispose();
            Assert.That(session.Player.Controls, Is.EqualTo(default(KagitKartControls)));
            Assert.Throws<ObjectDisposedException>(() => session.AdvanceFrame(Step, default));
        }

        [Test]
        public void FullSimulationGrandPrixRunsAllChaptersAndReversesStandingsGrid()
        {
            var source = ReadSource();
            var grandPrix = new KagitKartGrandPrix(source.roster);
            for (int round = 0; round < 4; round++)
            {
                var data = ReadTrack(grandPrix.TrackId);
                var standings = grandPrix.SortedStandings();
                var expectedGrid = new string[standings.Count];
                for (int i = 0; i < expectedGrid.Length; i++)
                {
                    expectedGrid[i] = standings[standings.Count - 1 - i].CharacterId;
                }
                using (var session = new KagitKartSession(source, data, ReadPlan(grandPrix.TrackId, data),
                    KagitKartRaceMode.GrandPrix, "tilki", 120, (uint)(12345 + round), grandPrix))
                {
                    if (round > 0)
                    {
                        for (int i = 0; i < expectedGrid.Length; i++)
                        {
                            Assert.That(session.Karts[i].Id, Is.EqualTo(expectedGrid[i]));
                        }
                    }
                    SetAutopilot(session.Player);
                    for (int i = 0; i < 120 * 480 && session.Race.State != KagitKartRaceState.Done; i++)
                    {
                        session.AdvanceFrame(Step, default);
                    }
                    Assert.That(session.Race.State, Is.EqualTo(KagitKartRaceState.Done), grandPrix.TrackId);
                    Assert.That(grandPrix.RoundComplete, Is.True);
                    int total = 0;
                    foreach (var standing in grandPrix.SortedStandings())
                    {
                        total += standing.Points;
                        Assert.That(standing.TotalTime, Is.GreaterThan(0));
                    }
                    Assert.That(total, Is.EqualTo(58 * (round + 1)));
                }
                Assert.That(grandPrix.NextRound(), Is.EqualTo(round < 3));
            }
            Assert.That(grandPrix.Completed, Is.True);
            Assert.That(grandPrix.SortedStandings()[0].Points, Is.GreaterThan(0));
        }

        private static KagitKartSession CreateSession(string trackId, int speedClass, KagitKartRaceMode mode)
        {
            var source = ReadSource();
            var data = ReadTrack(trackId);
            return new KagitKartSession(source, data, ReadPlan(trackId, data), mode, "tilki", speedClass, 12345);
        }

        private static KagitKartSourceCatalog ReadSource() => KagitKartSourceCatalog.Parse(
            AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "SourceCatalog.json").text);

        private static KagitKartTrackData ReadTrack(string id) => KagitKartTrackData.Parse(
            AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Tracks/" + KagitKartCatalog.TrackAssetName(id) + ".json").text);

        private static KagitKartAiPlan ReadPlan(string id, KagitKartTrackData data) => KagitKartAiPlan.Parse(
            AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "AiPlans/" + KagitKartCatalog.TrackAssetName(id) + ".json").text, data);

        private static void SetAutopilot(KagitKartVehicle kart) =>
            typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.Autopilot)).SetValue(kart, true);

        [Serializable]
        private sealed class Fixtures
        {
            public string sourceCommit;
            public Run[] runs;
        }

        [Serializable]
        private sealed class Run
        {
            public string track;
            public int speedClass;
            public Frame[] frames;
        }

        [Serializable]
        private sealed class Frame
        {
            public int step;
            public Controls[] controls;
        }

        [Serializable]
        private sealed class Controls
        {
            public float steer, throttle, brake;
            public bool drift, item;
        }
    }
}
