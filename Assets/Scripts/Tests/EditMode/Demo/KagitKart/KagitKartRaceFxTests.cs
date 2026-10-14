using System;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartRaceFxTests
    {
        private const string Root = "Assets/LoadResources/Demos/kagit_kart/";

        [TestCase("tier1")]
        [TestCase("tier2")]
        [TestCase("tier3")]
        [TestCase("drift1")]
        [TestCase("drift2")]
        [TestCase("drift3")]
        [TestCase("offroad")]
        [TestCase("sand")]
        [TestCase("draft")]
        [TestCase("scrape")]
        [TestCase("land")]
        [TestCase("wall")]
        [TestCase("hit")]
        [TestCase("burnout")]
        [TestCase("finish")]
        [TestCase("boost")]
        [TestCase("rescue")]
        [TestCase("reduced")]
        public void EmissionsAndRescueMatchOriginalFxJavaScript(string scenario)
        {
            var source = KagitKartSourceCatalog.Parse(Read("Data/SourceCatalog.json"));
            var track = KagitKartTrackData.Parse(Read("Data/Tracks/Meadow.json"));
            var plan = KagitKartAiPlan.Parse(Read("Data/AiPlans/Meadow.json"), track);
            using var session = new KagitKartSession(source, track, plan, KagitKartRaceMode.TimeTrial, "tilki", 120, 12345);
            Set(session.Race, "State", KagitKartRaceState.Running);
            var kart = session.Player;
            kart.Position = Vector3.zero;
            kart.Velocity = new Vector3(0, 0, 12);
            Set(kart, "Heading", 0f);
            Set(kart, "Speed", 0f);
            Set(kart, "Surface", "road");
            Field(kart, "info", new KagitKartQueryInfo { HasGround = true, GroundY = 0, Lateral = 0, Distance = 0, Right = Vector3.left, Up = Vector3.up });
            if (scenario.StartsWith("drift"))
            {
                Set(kart, "Drifting", true);
                Set(kart, "DriftTier", scenario[^1] - '0');
                Set(kart, "DriftDirection", 1);
                Set(kart, "Speed", 15f);
            }
            if (scenario is "offroad" or "sand")
            {
                Set(kart, "Surface", scenario);
                Set(kart, "Speed", 15f);
            }
            if (scenario == "draft")
            {
                var charge = (float[])typeof(KagitKartCollisions).GetField("draftCharge", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session.Collisions);
                charge[0] = 0.8f;
                Set(kart, "Speed", 15f);
            }
            if (scenario == "scrape")
            {
                Field(kart, "wallContact", true);
                Set(kart, "Speed", 15f);
            }
            if (scenario is "boost" or "reduced")
            {
                Set(kart, "BoostTime", 1f);
                Set(kart, "BoostStrength", 1f);
                Set(kart, "BoostSource", "item");
            }
            if (scenario == "rescue")
            {
                Set(kart, "RescueStage", 1);
                Field(kart, "rescueFrom", Vector3.zero);
                Field(kart, "rescueTo", new Vector3(10, 0, 10));
                Field(kart, "rescueHeading", 0.5f);
            }
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/Characters/Fox.prefab"));
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/RaceFx.prefab"));
            var cameraObject = new GameObject("FxFixtureCamera");
            var sunObject = new GameObject("FxFixtureSun");
            var target = new RenderTexture(1280, 720, 24);
            try
            {
                actor.GetComponent<KagitKartCharacterVisual>().Bind(kart, source.config.kart);
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.targetTexture = target;
                camera.fieldOfView = 54;
                camera.transform.position = new Vector3(0, 3, -7);
                camera.transform.LookAt(new Vector3(0, 1, 0));
                var sun = sunObject.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.color = Color.white;
                sun.intensity = 2;
                var fx = instance.GetComponent<KagitKartRaceFx>();
                fx.Bind(session, new[] { actor.transform }, AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/Materials/Stage/Print11.mat"), sun, 12345, source.config.kart);
                var signaled = (Action<KagitKartVehicle, KagitKartVehicleSignal>)typeof(KagitKartVehicle).GetField("Signaled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(kart);
                if (scenario.StartsWith("tier"))
                {
                    Set(kart, "DriftTier", scenario[^1] - '0');
                    signaled(kart, KagitKartVehicleSignal.DriftTier);
                }
                if (scenario == "land")
                {
                    Field(kart, "airTime", 0.7f);
                    signaled(kart, KagitKartVehicleSignal.Land);
                }
                if (scenario == "wall")
                {
                    Set(kart, "LastImpactSpeed", 15f);
                    signaled(kart, KagitKartVehicleSignal.WallHit);
                }
                if (scenario is "hit" or "burnout")
                {
                    Set(kart, "SpinCause", scenario == "burnout" ? "burnout" : "gum");
                    signaled(kart, KagitKartVehicleSignal.Hit);
                }
                if (scenario == "finish")
                {
                    var raceSignal = (Action<KagitKartRaceEvent>)typeof(KagitKartRace).GetField("Signaled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session.Race);
                    raceSignal(new KagitKartRaceEvent("finish", kart));
                }
                if (scenario is "boost" or "reduced")
                {
                    signaled(kart, KagitKartVehicleSignal.Boost);
                }
                if (scenario == "rescue")
                {
                    signaled(kart, KagitKartVehicleSignal.RescueLift);
                }
                var fixtures = JsonUtility.FromJson<Fixtures>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalRaceFx.json").text);
                Assert.That(fixtures.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
                var run = Array.Find(fixtures.runs, value => value.scenario == scenario);
                Assert.That(run, Is.Not.Null);
                for (int step = 1; step <= 120; step++)
                {
                    if (scenario == "rescue")
                    {
                        Set(kart, "RescueStage", step < 30 ? 1 : step < 60 ? 2 : step < 90 ? 3 : 0);
                    }
                    fx.Advance(1f / 60, camera, scenario == "reduced");
                    var frame = Array.Find(run.frames, value => value.step == step);
                    if (frame != null)
                    {
                        Check(fx, frame, scenario + " frame " + step);
                    }
                }
                Assert.That(kart.Position, Is.EqualTo(Vector3.zero));
                Assert.Throws<ArgumentException>(() => fx.Advance(float.NaN, camera));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(actor);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(sunObject);
                Object.DestroyImmediate(target);
            }
        }

        private static void Check(KagitKartRaceFx fx, Frame frame, string context)
        {
            int count = (int)typeof(KagitKartRaceFx).GetField("spawned", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fx);
            Assert.That(count, Is.EqualTo(frame.bits.Length), context + " spawned");
            for (int i = 0; i < frame.bits.Length; i++)
            {
                var actual = fx.bitStates[i];
                var expected = frame.bits[i];
                Equal(actual.P0, expected.aP0, context + " P0 " + i);
                Equal(actual.V0, expected.aV0, context + " V0 " + i);
                Equal(actual.Phys, expected.aPhys, context + " Physics " + i);
                Equal(actual.Shape, expected.aShape, context + " Shape " + i);
                Equal(actual.Look, expected.aLook, context + " Look " + i);
                Equal(actual.ColA, expected.aColA, context + " front " + i);
                Equal(actual.ColB, expected.aColB, context + " back " + i);
            }
            int cards = (int)typeof(KagitKartRaceFx).GetField("cardCount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fx);
            Assert.That(cards, Is.EqualTo(frame.cards.Length), context + " cards");
            for (int i = 0; i < cards; i++)
            {
                Equal(fx.cardStates[i].A, frame.cards[i].aA, context + " card A " + i);
                Equal(fx.cardStates[i].B, frame.cards[i].aB, context + " card B " + i);
                Equal(fx.cardStates[i].Color, frame.cards[i].aCol, context + " card color " + i);
                Equal(fx.cardStates[i].Variant, frame.cards[i].aVar, context + " card variant " + i);
            }
            Assert.That(fx.cranes.BodyCount, Is.EqualTo(frame.bodies.Length), context + " crane bodies");
            Assert.That(fx.cranes.WingCount, Is.EqualTo(frame.wings.Length), context + " crane wings");
            Matrices(fx.cranes.bodyPoses, frame.bodies, context);
            Matrices(fx.cranes.wingPoses, frame.wings, context);
            double intensity = (double)typeof(KagitKartRaceFx).GetField("lineIntensity", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fx);
            Assert.That(intensity, Is.EqualTo(frame.lines).Within(0.00001), context + " speed lines");
        }

        private static void Equal(Vector4 actual, float[] expected, string context)
        {
            for (int i = 0; i < 4; i++)
            {
                Assert.That(actual[i], Is.EqualTo(expected[i]).Within(0.003f), context + " " + i);
            }
        }

        private static void Matrices(Matrix4x4[] actual, Matrix[] expected, string context)
        {
            for (int i = 0; i < expected.Length; i++)
            {
                for (int column = 0; column < 4; column++)
                {
                    for (int row = 0; row < 4; row++)
                    {
                        float value = expected[i].values[column * 4 + row] * (row == 0 ? -1 : 1) * (column == 0 ? -1 : 1);
                        Assert.That(actual[i][row, column], Is.EqualTo(value).Within(0.003f), context + " crane " + i + " " + row + "," + column);
                    }
                }
            }
        }

        private static string Read(string path) => AssetDatabase.LoadAssetAtPath<TextAsset>(Root + path).text;

        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);

        private static void Field(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [Serializable]
        private sealed class Fixtures
        {
            public string sourceCommit;
            public Run[] runs;
        }

        [Serializable]
        private sealed class Run
        {
            public string scenario;
            public Frame[] frames;
        }

        [Serializable]
        private sealed class Frame
        {
            public int step;
            public Bit[] bits;
            public Card[] cards;
            public Matrix[] bodies, wings;
            public double lines;
        }

        [Serializable]
        private sealed class Bit
        {
            public float[] aP0, aV0, aPhys, aShape, aLook, aColA, aColB;
        }

        [Serializable]
        private sealed class Card
        {
            public float[] aA, aB, aCol, aVar;
        }

        [Serializable]
        private sealed class Matrix
        {
            public float[] values;
        }
    }
}
