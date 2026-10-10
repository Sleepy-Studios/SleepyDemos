using System;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartItemTests
    {
        private const string Root = "Assets/LoadResources/Demos/kagit_kart/Data/";
        private const double Step = KagitKartSimulationClock.Step;
        private KagitKartSourceCatalog source;
        private KagitKartTrackQuery track;
        private KagitKartVehicle[] karts;
        private KagitKartItems items;
        private double time;

        [SetUp]
        public void SetUp()
        {
            source = KagitKartSourceCatalog.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "SourceCatalog.json").text);
            var data = KagitKartTrackData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Tracks/Meadow.json").text);
            var tuning = source.config.track;
            track = new KagitKartTrackQuery(data, tuning.gridCell, tuning.waterDrop, tuning.deskDrop);
            karts = new[]
            {
                new KagitKartVehicle(source.config, track, source.roster[0], 120, true),
                new KagitKartVehicle(source.config, track, source.roster[1], 120, false)
            };
            karts[0].Teleport(0.0237f, 0, false);
            karts[1].Teleport(0.0237f + 80 / track.Length, 0, false);
            Set(karts[0], nameof(KagitKartVehicle.Place), 2);
            Set(karts[1], nameof(KagitKartVehicle.Place), 1);
            Set(karts[1], nameof(KagitKartVehicle.Progress), 80.0);
            items = new KagitKartItems(source.config, track, karts, new KagitKartRandom(12345), 120, false);
            time = 0;
        }

        [TearDown]
        public void TearDown() => items.Dispose();

        [TestCase("plane-forward")]
        [TestCase("plane-backward")]
        [TestCase("homing")]
        [TestCase("scissors")]
        public void ProjectileFixedStepsMatchOriginalJavaScript(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalItems.json");
            var fixtures = JsonUtility.FromJson<Fixtures>(asset.text);
            Assert.That(fixtures.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            var run = Array.Find(fixtures.runs, value => value.name == name);
            karts[0].PlaceAt(Vector(run.ownerPosition), run.ownerHeading);
            karts[1].PlaceAt(Vector(run.targetPosition), run.targetHeading);
            Give(karts[0], name.StartsWith("plane") ? "plane" : name);
            karts[0].Controls = new KagitKartControls { Item = true, Brake = name == "plane-backward" ? 1 : 0 };
            int frame = 0;
            for (int step = 1; step <= 180; step++)
            {
                items.Advance(Step, step * Step, true);
                karts[0].Controls = default;
                if (frame == run.frames.Length || run.frames[frame].step != step)
                {
                    continue;
                }
                var expected = run.frames[frame++].projectiles;
                Assert.That(items.Projectiles.Count, Is.EqualTo(expected.Length), name + " count at " + step);
                for (int i = 0; i < expected.Length; i++)
                {
                    var actual = items.Projectiles[i];
                    Assert.That(actual.Kind, Is.EqualTo(expected[i].kind));
                    Assert.That(Vector3.Distance(actual.Position, Vector(expected[i].position)), Is.LessThan(0.04), name + " at " + step);
                    Assert.That(Mathf.DeltaAngle(actual.Heading * Mathf.Rad2Deg, expected[i].heading * Mathf.Rad2Deg),
                        Is.EqualTo(0).Within(0.15), name + " heading at " + step);
                    Assert.That(actual.Age, Is.EqualTo(expected[i].age).Within(1e-9));
                    Assert.That(actual.Phase, Is.EqualTo(expected[i].phase));
                    Assert.That(actual.Locked, Is.EqualTo(expected[i].locked));
                    Assert.That(actual.Falling, Is.EqualTo(expected[i].falling));
                    Assert.That(actual.Bounces, Is.EqualTo(expected[i].bounces));
                }
            }
        }

        [Test]
        public void TripleRocketConsumesOnlyPressEdgesIncludingInputReset()
        {
            Give(karts[0], "rocket3", 3);
            karts[0].Controls = new KagitKartControls { Item = true };
            Tick(120);
            Assert.That(karts[0].ItemCount, Is.EqualTo(2));
            Assert.That(karts[0].BoostSource, Is.EqualTo("item"));
            items.ClearInput();
            Tick();
            Assert.That(karts[0].ItemCount, Is.EqualTo(2));
            karts[0].Controls = new KagitKartControls { Item = true };
            Tick(120);
            Assert.That(karts[0].ItemCount, Is.EqualTo(1));
            karts[0].Controls = default;
            Tick();
            karts[0].Controls = new KagitKartControls { Item = true };
            Tick();
            Assert.That(karts[0].Item, Is.Null);
            Assert.That(karts[0].ItemCount, Is.Zero);
        }

        [Test]
        public void GumIsHeldUntilReleaseAndHitsAnotherKart()
        {
            Give(karts[0], "gum");
            karts[0].Controls = new KagitKartControls { Item = true };
            Tick(30);
            Assert.That(karts[0].ItemHeld, Is.True);
            Assert.That(items.Hazards.Count, Is.EqualTo(1));
            Assert.That(items.Hazards[0].Held, Is.SameAs(karts[0]));
            items.ClearInput();
            Tick();
            Assert.That(karts[0].ItemHeld, Is.False);
            Assert.That(karts[0].Item, Is.Null);
            Assert.That(items.Hazards[0].Held, Is.Null);
            Assert.That(items.Hazards[0].HeldFor, Is.EqualTo(0.25).Within(Step));
            karts[1].PlaceAt(items.Hazards[0].Position, karts[0].Heading);
            Tick();
            Assert.That(karts[1].SpinCause, Is.EqualTo("gum"));
            Assert.That(karts[1].LastHitBy, Is.SameAs(karts[0]));
            Assert.That(items.Hazards.Count, Is.Zero);
        }

        [Test]
        public void HeldGumBlocksIncomingPlaneAndClearsSlot()
        {
            Give(karts[0], "gum");
            karts[0].Controls = new KagitKartControls { Item = true };
            Tick();
            var gum = items.Hazards[0];
            karts[1].PlaceAt(gum.Position - KagitKartVehicle.Forward(karts[0].Heading) * 5, karts[0].Heading);
            Give(karts[1], "plane");
            karts[1].Controls = new KagitKartControls { Item = true };
            Tick(20);
            Assert.That(karts[0].SpinTime, Is.Zero);
            Assert.That(karts[0].ItemHeld, Is.False);
            Assert.That(karts[0].Item, Is.Null);
            Assert.That(items.Hazards.Count, Is.Zero);
            Assert.That(items.Projectiles.Count, Is.Zero);
        }

        [Test]
        public void InkUsesPlacesAtThrowRatherThanAtImpact()
        {
            Give(karts[0], "ink");
            karts[0].Controls = new KagitKartControls { Item = true };
            Tick();
            Set(karts[0], nameof(KagitKartVehicle.Place), 1);
            Set(karts[1], nameof(KagitKartVehicle.Place), 2);
            karts[0].Controls = default;
            Tick(105);
            Assert.That(karts[1].InkTime, Is.EqualTo(3.5f));
            Assert.That(karts[0].InkTime, Is.Zero);
            Assert.That(items.Projectiles.Count, Is.Zero);
        }

        [Test]
        public void FoilGrantsSevenSecondsAndDestroysGumWithoutSpin()
        {
            Give(karts[1], "gum");
            karts[1].Controls = new KagitKartControls { Item = true };
            Tick();
            karts[1].Controls = default;
            Tick();
            karts[0].PlaceAt(items.Hazards[0].Position, karts[1].Heading);
            Give(karts[0], "foil");
            karts[0].Controls = new KagitKartControls { Item = true };
            Tick();
            Assert.That(karts[0].InvincibleTime, Is.EqualTo(7));
            Assert.That(karts[0].SpinTime, Is.Zero);
            Assert.That(items.Hazards.Count, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ScissorsRetargetsLeaderAndBoostDodgesSplash(bool boost)
        {
            Give(karts[0], "scissors");
            karts[0].Controls = new KagitKartControls { Item = true };
            Tick(3);
            Set(karts[0], nameof(KagitKartVehicle.Place), 1);
            Set(karts[1], nameof(KagitKartVehicle.Place), 2);
            if (boost)
            {
                karts[0].ApplyBoost(5, 1, "item");
            }
            int early = 0;
            int late = 0;
            items.Signaled += signal =>
            {
                if (signal.Kind == "threat" && signal.Kart == karts[0])
                {
                    if (signal.Eta > 0.5f)
                    {
                        early++;
                    }
                    else
                    {
                        late++;
                    }
                }
            };
            karts[0].Controls = default;
            Tick(180);
            Assert.That(items.Projectiles.Count, Is.Zero);
            Assert.That(karts[0].SpinCause, boost ? Is.Null : Is.EqualTo("scissors"));
            Assert.That(early, Is.LessThanOrEqualTo(1));
            Assert.That(late, Is.EqualTo(1));
            Assert.That(karts[1].SpinTime, Is.Zero);
        }

        [Test]
        public void BoxesRespawnWithoutRestartingOccupiedRoulette()
        {
            var box = items.Boxes[0];
            karts[0].PlaceAt(box.Position, box.Heading);
            Tick();
            Assert.That(box.Active, Is.False);
            Assert.That(karts[0].Roulette, Is.EqualTo(1.6));
            karts[0].Teleport(0.0237f, 0, false);
            Tick(145);
            Assert.That(box.Active, Is.True);
            double remaining = karts[0].Roulette;
            karts[0].PlaceAt(box.Position, box.Heading);
            Tick();
            Assert.That(karts[0].Roulette, Is.EqualTo(remaining - Step).Within(1e-9));
            Tick(50);
            Assert.That(karts[0].Item, Is.Not.Null);
            Assert.That(karts[0].Roulette, Is.Zero);
        }

        [Test]
        public void CloseLeaderCapsLotteryAndScissorsGateIncludesInventory()
        {
            Assert.That(items.RowFor(karts[0]), Is.EqualTo(7));
            Set(karts[1], nameof(KagitKartVehicle.Progress), 24.0);
            Assert.That(items.RowFor(karts[0]), Is.EqualTo(2));
            items.Advance(Step, 19.9, true);
            Assert.That(items.ScissorsAvailable(), Is.False);
            items.Advance(Step, 20, true);
            Assert.That(items.ScissorsAvailable(), Is.True);
            Give(karts[1], "scissors");
            Assert.That(items.ScissorsAvailable(), Is.False);
            karts[1].Controls = new KagitKartControls { Item = true };
            time = 20;
            Tick(300);
            Assert.That(items.ScissorsAvailable(), Is.False);
            items.Advance(Step, 36, true);
            Assert.That(items.ScissorsAvailable(), Is.True);
        }

        [Test]
        public void TimeTrialHasThreeRocketsAndDisposeReleasesState()
        {
            items.Dispose();
            items = new KagitKartItems(source.config, track, karts, new KagitKartRandom(12345), 120, true);
            Assert.That(items.Boxes.Count, Is.Zero);
            Assert.That(karts[0].Item, Is.EqualTo("rocket3"));
            Assert.That(karts[0].ItemCount, Is.EqualTo(3));
            Give(karts[0], "gum");
            karts[0].Controls = new KagitKartControls { Item = true };
            Tick();
            items.Dispose();
            Assert.That(karts[0].ItemHeld, Is.False);
            Assert.That(karts[0].Item, Is.Null);
            Assert.That(items.Hazards.Count, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => Tick());
        }

        private void Tick(int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                time += Step;
                items.Advance(Step, time, true);
            }
        }

        private static void Give(KagitKartVehicle kart, string item, int count = 1)
        {
            Set(kart, nameof(KagitKartVehicle.Item), item);
            Set(kart, nameof(KagitKartVehicle.ItemCount), count);
        }

        private static void Set<T>(KagitKartVehicle kart, string property, T value) =>
            typeof(KagitKartVehicle).GetProperty(property, BindingFlags.Public | BindingFlags.Instance).SetValue(kart, value);

        private static Vector3 Vector(float[] value) => new Vector3(value[0], value[1], value[2]);

        [Serializable]
        private sealed class Fixtures
        {
            public string sourceCommit;
            public Run[] runs;
        }

        [Serializable]
        private sealed class Run
        {
            public string name;
            public float[] ownerPosition;
            public float ownerHeading;
            public float[] targetPosition;
            public float targetHeading;
            public Frame[] frames;
        }

        [Serializable]
        private sealed class Frame
        {
            public int step;
            public Projectile[] projectiles;
        }

        [Serializable]
        private sealed class Projectile
        {
            public string kind;
            public float[] position;
            public float heading;
            public double age;
            public int phase;
            public bool locked;
            public bool falling;
            public int bounces;
        }
    }
}
