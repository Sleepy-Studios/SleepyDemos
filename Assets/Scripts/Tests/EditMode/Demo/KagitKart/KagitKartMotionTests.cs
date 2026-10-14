using System;
using System.IO;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartMotionTests
    {
        [Test]
        public void DriftStartsOnGroundAndReleaseKeepsBoost()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var kart = new KagitKartVehicle(source.config, track, source.roster[0], 120, true);
            var start = track.PointAt(.0237f, 0);
            kart.PlaceAt(start.Position, start.Heading);
            kart.Controls = new KagitKartControls { Throttle = 1 };
            for (int i = 0; i < 180; i++)
            {
                kart.Advance((float)KagitKartSimulationClock.Step);
            }
            kart.Controls = new KagitKartControls { Throttle = 1, Steer = .65f, Drift = true };
            kart.Advance((float)KagitKartSimulationClock.Step);
            Assert.That(kart.Drifting, Is.True);
            Assert.That(kart.Grounded, Is.True);
            Assert.That(kart.DriftDirection, Is.EqualTo(1));
            typeof(KagitKartVehicle).GetProperty(nameof(KagitKartVehicle.DriftTier)).SetValue(kart, 1);
            kart.Controls = new KagitKartControls { Throttle = 1 };
            kart.Advance((float)KagitKartSimulationClock.Step);
            Assert.That(kart.Drifting, Is.False);
            Assert.That(kart.BoostTime, Is.GreaterThan(0));
        }

        [Test]
        public void ReconcileRestoresDriftAndReplaysWithoutRepeatingEffects()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var expected = new KagitKartVehicle(source.config, track, source.roster[0], 120, true);
            var replay = new KagitKartVehicle(source.config, track, source.roster[0], 120, true);
            var point = track.PointAt(.0237f, 0);
            expected.PlaceAt(point.Position, point.Heading);
            expected.Controls = new KagitKartControls { Throttle = 1 };
            for (int i = 0; i < 180; i++) expected.Advance((float)KagitKartSimulationClock.Step);
            expected.Controls = new KagitKartControls { Throttle = 1, Steer = .65f, Drift = true };
            for (int i = 0; i < 15; i++) expected.Advance((float)KagitKartSimulationClock.Step);
            using var saved = new MemoryStream();
            using var writer = new BinaryWriter(saved);
            expected.WriteNetworkState(writer);
            saved.Position = 0;
            using var reader = new BinaryReader(saved);
            replay.ReadNetworkState(reader, new[] { replay }, false);
            int effects = 0;
            replay.Signaled += (_, _) => effects++;
            replay.SuppressSignals = true;
            for (int i = 0; i < 120; i++)
            {
                var controls = new KagitKartControls { Throttle = 1, Steer = i < 60 ? .65f : 0, Drift = i < 60 };
                expected.Controls = replay.Controls = controls;
                expected.Advance((float)KagitKartSimulationClock.Step);
                replay.Advance((float)KagitKartSimulationClock.Step);
            }
            Assert.That(replay.Position, Is.EqualTo(expected.Position));
            Assert.That(replay.Velocity, Is.EqualTo(expected.Velocity));
            Assert.That(replay.DriftCharge, Is.EqualTo(expected.DriftCharge));
            Assert.That(replay.BoostTime, Is.EqualTo(expected.BoostTime));
            Assert.That(effects, Is.Zero);
        }

        [Test]
        public void NetworkSnapshotsAndPredictionDoNotRepeatDelayedBoostCue()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var server = new KagitKartVehicle(source.config, track, source.roster[0], 120, true);
            var remote = new KagitKartVehicle(source.config, track, source.roster[0], 120, true);
            var attacker = new KagitKartVehicle(source.config, track, source.roster[1], 120, false);
            var remoteAttacker = new KagitKartVehicle(source.config, track, source.roster[1], 120, false);
            var start = track.PointAt(.0237f, 0);
            server.PlaceAt(start.Position, start.Heading);
            server.Controls = new KagitKartControls { Throttle = 1 };
            for (int i = 0; i < 120; i++) server.Advance((float)KagitKartSimulationClock.Step);
            server.ApplyBoost(1.7f, 1.15f, "rocket");
            var originalCue = server.CaptureSignalContext();
            Assert.That(server.SpinOut("plane", attacker), Is.True);
            server.ApplyBoost(.9f, 1, "pad");
            server.ApplyInk(6);
            server.SetInvincible(7);
            server.SetGrace(1.5f);
            server.Controls = new KagitKartControls { Throttle = .6f, Steer = .25f };
            for (int i = 0; i < 30; i++) server.Advance((float)KagitKartSimulationClock.Step);

            int cues = 0;
            float cueDuration = 0;
            string cueSource = null;
            remote.Signaled += (kart, signal) =>
            {
                cues++;
                Assert.That(signal, Is.EqualTo(KagitKartVehicleSignal.Boost));
                cueDuration = kart.LastBoostDuration;
                cueSource = kart.BoostSource;
            };
            using var packet = new MemoryStream();
            using var writer = new BinaryWriter(packet);
            server.WriteNetworkState(writer);
            originalCue.Write(writer);
            packet.Position = 0;
            using var reader = new BinaryReader(packet);
            remote.ReadNetworkState(reader, new[] { remote, remoteAttacker }, false);
            var receivedCue = KagitKartVehicle.SignalContext.Read(reader);
            Assert.That(packet.Position, Is.EqualTo(packet.Length), "完整状态与可靠事件上下文均已消费");
            Assert.That(remote.LastHitBy, Is.SameAs(remoteAttacker), "攻击来源映射到客户端车对象");
            Assert.That(remote.SpinCause, Is.EqualTo("plane"));
            Assert.That(remote.SpinTime, Is.EqualTo(server.SpinTime));
            Assert.That(remote.InkTime, Is.EqualTo(server.InkTime));
            Assert.That(remote.InvincibleTime, Is.EqualTo(server.InvincibleTime));
            Assert.That(remote.GraceTime, Is.EqualTo(server.GraceTime));
            Assert.That(cues, Is.Zero, "接收状态快照不重复播放业务信号");

            remote.SuppressSignals = true;
            for (int i = 0; i < 60; i++)
            {
                server.Advance((float)KagitKartSimulationClock.Step);
                remote.Advance((float)KagitKartSimulationClock.Step);
            }
            remote.SuppressSignals = false;
            Assert.That(cues, Is.Zero, "协调重放不重复播放业务信号");
            Assert.That(remote.Position, Is.EqualTo(server.Position));
            Assert.That(remote.Velocity, Is.EqualTo(server.Velocity));
            Assert.That(remote.BoostTime, Is.EqualTo(server.BoostTime));
            float latestBoostTime = remote.BoostTime;
            remote.NotifyNetworkSignal(KagitKartVehicleSignal.Boost, receivedCue);
            Assert.That(cues, Is.EqualTo(1));
            Assert.That(cueDuration, Is.EqualTo(1.7f), "迟到的可靠通知仍使用原事件时长");
            Assert.That(cueSource, Is.EqualTo("rocket"));
            Assert.That(remote.LastBoostDuration, Is.EqualTo(.9f), "通知后恢复较新权威事件状态");
            Assert.That(remote.BoostSource, Is.EqualTo(server.BoostSource));
            Assert.That(remote.SpinCause, Is.EqualTo("plane"));
            Assert.That(remote.BoostTime, Is.EqualTo(latestBoostTime), "音画通知不重新应用玩法加速");
            Assert.That(remote.Controls, Is.EqualTo(server.Controls));
        }

        private const string DataRoot = "Assets/LoadResources/Demos/kagit_kart/Data/";

        [Test]
        public void TrackQueriesMatchOriginalJavaScript()
        {
            var source = ReadSource();
            var fixtures = ReadFixtures();
            foreach (string trackId in new[] { "meadow", "bosphorus", "glacier", "desk" })
            {
                var track = ReadTrack(trackId, source);
                var info = new KagitKartQueryInfo();
                foreach (var item in fixtures.queries)
                {
                    if (item.track != trackId)
                    {
                        continue;
                    }
                    var position = Vector(item.position);
                    track.Query(position, -1, ref info);
                    Assert.That(info.Surface, Is.EqualTo(item.surface), trackId + " surface");
                    Assert.That(info.EdgeCode, Is.EqualTo(item.edgeCode), trackId + " edge");
                    Assert.That(info.HasGround, Is.EqualTo(item.hasGround), trackId + " ground");
                    Assert.That(info.RampId, Is.EqualTo(item.rampId), trackId + " ramp");
                    Assert.That(info.PadId, Is.EqualTo(item.padId), trackId + " pad");
                    Assert.That(info.Lateral, Is.EqualTo(item.lateral).Within(0.015), trackId + " lateral");
                    Assert.That(info.GroundY, Is.EqualTo(item.groundY).Within(0.015), trackId + " height");
                    double distanceError = Math.Abs(info.Distance - item.distance);
                    distanceError = Math.Min(distanceError, Math.Abs(track.Length - distanceError));
                    Assert.That(distanceError, Is.LessThan(0.02), trackId + " distance");
                    var local = new KagitKartQueryInfo();
                    track.Query(position, info.Index, ref local);
                    Assert.That(local.Surface, Is.EqualTo(info.Surface));
                    Assert.That(local.HasGround, Is.EqualTo(info.HasGround));
                }
            }
        }

        [TestCase("straight-80")]
        [TestCase("straight-120")]
        [TestCase("straight-200")]
        [TestCase("brake-reverse")]
        [TestCase("rocket")]
        [TestCase("rescue")]
        public void VehicleMotionMatchesOriginalFixedSteps(string name)
        {
            var source = ReadSource();
            var fixtures = ReadFixtures();
            var scenario = Array.Find(fixtures.runs, value => value.name == name);
            Assert.That(scenario, Is.Not.Null);
            var track = ReadTrack(scenario.track, source);
            var vehicle = new KagitKartVehicle(source.config, track, source.roster[0], scenario.speedClass, true);
            var start = track.PointAt(0.0237f, 0);
            vehicle.PlaceAt(start.Position, start.Heading);
            int frame = 0;
            for (int i = 0; i < scenario.steps; i++)
            {
                vehicle.Controls = new KagitKartControls
                {
                    Throttle = name == "brake-reverse" && i >= 360 ? 0 : 1,
                    Brake = name == "brake-reverse" && i >= 360 ? 1 : 0,
                    Steer = 0,
                    Drift = false
                };
                if (name == "rocket" && i == 240)
                {
                    var boost = source.config.kart.boosts["rocket"];
                    vehicle.ApplyBoost(boost[0], boost[1], "rocket");
                }
                if (name == "rescue" && i == 120)
                {
                    vehicle.StartRescue();
                }
                vehicle.Advance((float)KagitKartSimulationClock.Step);
                if (frame >= scenario.frames.Length || scenario.frames[frame].step != i + 1)
                {
                    continue;
                }
                var expected = scenario.frames[frame++];
                Assert.That(Vector3.Distance(vehicle.Position, Vector(expected.position)), Is.LessThan(0.035), name + " position at " + (i + 1));
                Assert.That(vehicle.Speed, Is.EqualTo(expected.speed).Within(0.025), name + " speed at " + (i + 1));
                Assert.That(vehicle.Heading, Is.EqualTo(expected.heading).Within(0.003), name + " heading at " + (i + 1));
                Assert.That(vehicle.Surface, Is.EqualTo(expected.surface));
                Assert.That(vehicle.Grounded, Is.EqualTo(expected.grounded));
                Assert.That(vehicle.Drifting, Is.EqualTo(expected.drifting));
                Assert.That(vehicle.DriftTier, Is.EqualTo(expected.tier));
                Assert.That(vehicle.BoostTime, Is.EqualTo(expected.boostTime).Within(0.012));
                int rescueStage = expected.rescueStage == "lift" ? 1 : expected.rescueStage == "carry" ? 2 : expected.rescueStage == "drop" ? 3 : 0;
                Assert.That(vehicle.RescueStage, Is.EqualTo(rescueStage));
            }
            Assert.That(frame, Is.EqualTo(scenario.frames.Length));
        }

        [Test]
        public void TeleportClearsRescueSpinAndDriftAndResetsInterpolation()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var vehicle = new KagitKartVehicle(source.config, track, source.roster[0], 120, true);
            vehicle.Teleport(0.0237f, 0, false);
            vehicle.SpinOut("plane");
            vehicle.StartRescue();
            vehicle.Teleport(0.1f, 0, false);
            Assert.That(vehicle.Rescuing, Is.False);
            Assert.That(vehicle.SpinTime, Is.Zero);
            Assert.That(vehicle.Drifting, Is.False);
            Assert.That(vehicle.Position, Is.EqualTo(vehicle.PreviousPosition));
            Assert.That(vehicle.Rotation, Is.EqualTo(vehicle.PreviousRotation));
            Assert.That(vehicle.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void PlayerControlsRemainRightAfterUnityCoordinateConversion()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var vehicle = new KagitKartVehicle(source.config, track, source.roster[0], 120, true);
            vehicle.Teleport(0.0237f, 0, false);
            vehicle.Controls = new KagitKartControls { Throttle = 1, Steer = 1 };
            for (int i = 0; i < 120; i++)
            {
                vehicle.Advance((float)KagitKartSimulationClock.Step);
            }
            Assert.That(vehicle.Heading, Is.LessThan(0));
            Assert.That(KagitKartVehicle.ToUnity(vehicle.Position).x, Is.GreaterThan(0));
            Assert.That((vehicle.Rotation * Vector3.forward).x, Is.GreaterThan(0));
        }

        [Test]
        public void CollisionSeparatesByMassAndShieldKnocksOnlyOpponent()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var a = new KagitKartVehicle(source.config, track, source.roster[0], 120, true);
            var b = new KagitKartVehicle(source.config, track, source.roster[3], 120, false);
            a.Teleport(0.0237f, -0.5f, false);
            b.Teleport(0.0237f, 0.5f, false);
            Vector3 centre = (a.Position * a.Mass + b.Position * b.Mass) / (a.Mass + b.Mass);
            a.SetInvincible(7);
            var collisions = new KagitKartCollisions(new[] { a, b }, source.config.kart);
            collisions.Advance((float)KagitKartSimulationClock.Step);
            Assert.That(Vector3.Distance(a.Position, b.Position), Is.EqualTo(source.config.kart.radius * 2).Within(0.001));
            Vector3 after = (a.Position * a.Mass + b.Position * b.Mass) / (a.Mass + b.Mass);
            Assert.That(Vector3.Distance(centre, after), Is.LessThan(0.001));
            Assert.That(a.SpinTime, Is.Zero);
            Assert.That(b.SpinCause, Is.EqualTo("foil"));
            Assert.That(b.LastHitBy, Is.SameAs(a));
        }

        [Test]
        public void SlipstreamBoostHasCooldownAndDoesNotChargeBehindBoostedKart()
        {
            var source = ReadSource();
            var track = ReadTrack("meadow", source);
            var a = new KagitKartVehicle(source.config, track, source.roster[0], 120, true);
            var b = new KagitKartVehicle(source.config, track, source.roster[1], 120, false);
            foreach (var vehicle in new[] { a, b })
            {
                vehicle.Teleport(0.0237f, 0, false);
                vehicle.Controls = new KagitKartControls { Throttle = 1 };
                for (int i = 0; i < 240; i++)
                {
                    vehicle.Advance((float)KagitKartSimulationClock.Step);
                }
            }
            a.Teleport(0.0237f, 0);
            b.Teleport(0.0237f + 10 / track.Length, 0);
            var collisions = new KagitKartCollisions(new[] { a, b }, source.config.kart);
            int boosts = 0;
            a.Signaled += (_, signal) => { if (signal == KagitKartVehicleSignal.Boost) boosts++; };
            for (int i = 0; i < 150; i++)
            {
                collisions.Advance((float)KagitKartSimulationClock.Step);
            }
            Assert.That(boosts, Is.EqualTo(1));
            Assert.That(a.BoostSource, Is.EqualTo("draft"));
            for (int i = 0; i < 120; i++)
            {
                collisions.Advance((float)KagitKartSimulationClock.Step);
            }
            Assert.That(boosts, Is.EqualTo(1));
            b.ApplyBoost(10, 1, "rocket");
            for (int i = 0; i < 600; i++)
            {
                collisions.Advance((float)KagitKartSimulationClock.Step);
            }
            Assert.That(boosts, Is.EqualTo(1));
            Assert.That(collisions.GetDraftCharge(0), Is.Zero);
        }

        private static KagitKartSourceCatalog ReadSource() => KagitKartSourceCatalog.Parse(
            AssetDatabase.LoadAssetAtPath<TextAsset>(DataRoot + "SourceCatalog.json").text);

        private static KagitKartTrackQuery ReadTrack(string id, KagitKartSourceCatalog source)
        {
            var data = KagitKartTrackData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(
                DataRoot + "Tracks/" + KagitKartCatalog.TrackAssetName(id) + ".json").text);
            var tuning = source.config.track;
            return new KagitKartTrackQuery(data, tuning.gridCell, tuning.waterDrop, tuning.deskDrop);
        }

        private static OriginalFixtures ReadFixtures()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalMotion.json");
            var data = JsonUtility.FromJson<OriginalFixtures>(asset.text);
            Assert.That(data.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            return data;
        }

        private static Vector3 Vector(double[] values) => new Vector3((float)values[0], (float)values[1], (float)values[2]);

        [Serializable]
        private sealed class OriginalFixtures
        {
            public string sourceCommit;
            public OriginalQuery[] queries;
            public OriginalRun[] runs;
        }

        [Serializable]
        private sealed class OriginalQuery
        {
            public string track;
            public double[] position;
            public double distance;
            public double lateral;
            public double groundY;
            public string surface;
            public int edgeCode;
            public bool hasGround;
            public int rampId;
            public int padId;
        }

        [Serializable]
        private sealed class OriginalRun
        {
            public string name;
            public string track;
            public int speedClass;
            public int steps;
            public OriginalFrame[] frames;
        }

        [Serializable]
        private sealed class OriginalFrame
        {
            public int step;
            public double[] position;
            public double speed;
            public double heading;
            public string surface;
            public bool grounded;
            public bool drifting;
            public int tier;
            public double boostTime;
            public string rescueStage;
        }
    }
}
