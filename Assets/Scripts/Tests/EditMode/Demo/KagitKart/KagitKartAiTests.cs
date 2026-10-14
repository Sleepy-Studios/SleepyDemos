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

        [TestCase("meadow")]
        [TestCase("bosphorus")]
        [TestCase("glacier")]
        [TestCase("desk")]
        public void DedicatedAuthorityBuildsEightCarsWithoutALocalPlayer(string trackId)
        {
            var source = ReadSource();
            var data = ReadTrack(trackId);
            var grid = Array.ConvertAll(source.roster, character => character.id);
            using var session = new KagitKartSession(source, data, ReadPlan(trackId, data), KagitKartRaceMode.SingleRace,
                null, 120, 12345, grid: grid, humanIds: new[] { grid[0], grid[1] });
            Assert.That(session.Player, Is.Null);
            Assert.That(session.Karts.Count, Is.EqualTo(8));
            var inputs = new System.Collections.Generic.Dictionary<string, KagitKartControls>
            {
                [grid[0]] = new KagitKartControls { Throttle = .6f, Steer = -.3f },
                [grid[1]] = new KagitKartControls { Throttle = .7f, Steer = .4f }
            };
            for (int i = 0; i < 480; i++) session.AdvanceNetwork((float)Step, inputs);
            Assert.That(session.Race.State, Is.EqualTo(KagitKartRaceState.Running));
            Assert.That(session.Karts[0].Controls.Throttle, Is.EqualTo(.6f));
            Assert.That(session.Karts[1].Controls.Steer, Is.EqualTo(.4f));
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
        public void AiRespectsEveryHumanInputAndCanTakeOverDisconnectedCars(string trackId, int speedClass)
        {
            var source = ReadSource();
            var data = ReadTrack(trackId);
            var grid = Array.ConvertAll(source.roster, character => character.id);
            using var session = new KagitKartSession(source, data, ReadPlan(trackId, data), KagitKartRaceMode.SingleRace,
                grid[0], speedClass, 12345, grid: grid, humanIds: new[] { grid[0], grid[1] });
            for (int i = 0; i < 120 * 10 && session.Race.State == KagitKartRaceState.Countdown; i++)
            {
                session.Race.Advance(Step, 0);
            }
            Assert.That(session.Race.State, Is.EqualTo(KagitKartRaceState.Running));
            var first = new KagitKartControls { Throttle = .5f, Steer = -.3f, Drift = true };
            var second = new KagitKartControls { Throttle = .7f, Steer = .4f };
            session.Karts[0].Controls = first;
            session.Karts[1].Controls = second;
            session.Ai.Advance(Step, 0);
            Assert.That(session.Karts[0].Controls.Throttle, Is.EqualTo(first.Throttle));
            Assert.That(session.Karts[0].Controls.Steer, Is.EqualTo(first.Steer));
            Assert.That(session.Karts[1].Controls.Throttle, Is.EqualTo(second.Throttle));
            Assert.That(session.Karts[1].Controls.Steer, Is.EqualTo(second.Steer));
            SetAutopilot(session.Karts[1]);
            session.Karts[1].Controls = default;
            session.Ai.Advance(Step, 0);
            Assert.That(session.Karts[1].Controls.Throttle, Is.GreaterThan(0), "掉线后的真人车由服务器 AI 接管");
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

    }
}
