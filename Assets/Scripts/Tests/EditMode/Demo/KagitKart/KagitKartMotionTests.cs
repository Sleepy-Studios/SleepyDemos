using System;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartMotionTests
    {
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
        [TestCase("drift")]
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
                    Steer = name == "drift" && i >= 240 && i < 660 ? 0.65f : 0,
                    Drift = name == "drift" && i >= 240 && i < 660
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
