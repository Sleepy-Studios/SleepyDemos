using System;
using System.Collections.Generic;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartItemVisualTests
    {
        private const string Root = "Assets/LoadResources/Demos/kagit_kart/";
        private static readonly string[] Names = { "items:boxes", "items:gum", "items:flyers", "items:pens", "items:ink-bottles", "items:ink-drops", "items:scissors", "items:ink-splats", "items:shadows", "items:flight-dashes", "items:foil" };

        [TestCase("box")]
        [TestCase("box-open")]
        [TestCase("gum-held")]
        [TestCase("gum-drop")]
        [TestCase("rocket3")]
        [TestCase("rocket-fire")]
        [TestCase("plane")]
        [TestCase("homing")]
        [TestCase("ink-bottle")]
        [TestCase("ink-burst")]
        [TestCase("gum-pop")]
        [TestCase("scissors")]
        [TestCase("snip")]
        [TestCase("foil")]
        public void InstancesMatchOriginalItemVisualsJavaScript(string scenario)
        {
            var fixture = JsonUtility.FromJson<Fixtures>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalItemVisuals.json").text);
            Assert.That(fixture.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            var source = KagitKartSourceCatalog.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Data/SourceCatalog.json").text);
            var data = KagitKartTrackData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Data/Tracks/Meadow.json").text);
            var plan = KagitKartAiPlan.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(Root + "Data/AiPlans/Meadow.json").text, data);
            using var session = new KagitKartSession(source, data, plan, KagitKartRaceMode.TimeTrial, "tilki", 120, 1234);
            Set(session.Race, "State", KagitKartRaceState.Done);
            var kart = session.Player;
            kart.Item = scenario == "rocket3" ? "rocket3" : null;
            kart.ItemCount = scenario == "rocket3" ? 3 : 0;
            kart.Position = Vector3.zero;
            kart.Velocity = new Vector3(0, 0, 12);
            Set(kart, "BoostTime", scenario == "rocket-fire" ? 1f : 0f);
            Set(kart, "BoostSource", "item");
            Set(kart, "InvincibleTime", scenario == "foil" ? 7f : 0f);
            var boxes = (List<KagitKartItemBox>)session.Items.Boxes;
            if (scenario.StartsWith("box"))
            {
                boxes.Add(new KagitKartItemBox { Position = new Vector3(2, 0, 12), Up = Vector3.up, Active = scenario == "box", TakenAt = 0 });
            }
            if (scenario is "gum-held" or "gum-drop")
            {
                ((List<KagitKartGum>)session.Items.Hazards).Add(new KagitKartGum
                {
                    Id = 1, Position = new Vector3(0, 0, -1.9f), PreviousPosition = new Vector3(0, 0, -1.9f),
                    Up = Vector3.up, Held = scenario == "gum-held" ? kart : null, HeldFor = 0.5, DroppedAt = 0
                });
            }
            KagitKartProjectile projectile = null;
            if (scenario is "plane" or "homing" or "ink-bottle" or "scissors")
            {
                projectile = new KagitKartProjectile
                {
                    Id = 3, Kind = scenario == "ink-bottle" ? "ink" : scenario,
                    Position = new Vector3(0, 1, 20), PreviousPosition = new Vector3(0, 1, 20),
                    Heading = 0.3f, PreviousHeading = 0.3f, TurnRate = 0.8f, Up = Vector3.up, To = new Vector3(0, 0, 25), FlipAt = -9
                };
                ((List<KagitKartProjectile>)session.Items.Projectiles).Add(projectile);
            }
            var actor = new GameObject("FixtureKart");
            var cameraObject = new GameObject("FixtureCamera");
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/ItemVisuals.prefab"));
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(0, 3, -7);
                var visual = instance.GetComponent<KagitKartItemVisuals>();
                var environment = AssetDatabase.LoadAssetAtPath<Material>(Root + "Art/Materials/Stage/Print11.mat");
                visual.Bind(session, new[] { actor.transform }, environment);
                if (scenario is "rocket-fire" or "ink-burst" or "gum-pop" or "snip")
                {
                    string kind = scenario switch { "rocket-fire" => "pen", "ink-burst" => "ink", "gum-pop" => "pop", _ => "snip" };
                    var signaled = (Action<KagitKartItemEvent>)typeof(KagitKartItems).GetField("Signaled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(session.Items);
                    signaled(new KagitKartItemEvent(kind, null, new Vector3(0, 5.5f, 15), scenario == "rocket-fire" ? kart : null));
                }
                Run run = null;
                foreach (var candidate in fixture.runs)
                {
                    if (candidate.scenario == scenario)
                    {
                        run = candidate;
                        break;
                    }
                }
                Assert.That(run, Is.Not.Null);
                const float dt = 1f / 60;
                for (int step = 1; step <= 60; step++)
                {
                    Set(session, "RaceTime", step / 60.0);
                    if (projectile != null)
                    {
                        projectile.Age = step / 60.0;
                        projectile.PhaseTime = step / 60.0;
                        projectile.PreviousPosition = projectile.Position;
                        projectile.Position.z = 20 + step * 0.4f;
                        if (scenario == "scissors" && step >= 30)
                        {
                            projectile.Phase = 2;
                            projectile.PhaseTime = (step - 30) / 60.0;
                        }
                    }
                    if (scenario == "rocket-fire" && step >= 30)
                    {
                        Set(kart, "BoostTime", 0f);
                    }
                    if (scenario == "foil" && step >= 30)
                    {
                        Set(kart, "InvincibleTime", 0.9f);
                    }
                    visual.Advance(dt, camera);
                    foreach (var frame in run.frames)
                    {
                        if (frame.step == step)
                        {
                            CheckFrame(visual, frame, scenario + " frame " + step);
                        }
                    }
                }
                Assert.Throws<ArgumentException>(() => visual.Advance(float.NaN, camera));
                Assert.That(kart.Position, Is.EqualTo(Vector3.zero), "表现不能写回规则位置");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(actor);
            }
        }

        private static void CheckFrame(KagitKartItemVisuals visual, Frame frame, string context)
        {
            var batches = (Array)typeof(KagitKartItemVisuals).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(visual);
            for (int kind = 0; kind < Names.Length; kind++)
            {
                var batch = batches.GetValue(kind);
                Type type = batch.GetType();
                int count = (int)type.GetField("count").GetValue(batch);
                var poses = (Matrix4x4[])type.GetField("poses").GetValue(batch);
                var parameters = (Vector4[])type.GetField("parameters").GetValue(batch);
                Batch expected = null;
                foreach (var candidate in frame.batches)
                {
                    if (candidate.name == Names[kind])
                    {
                        expected = candidate;
                        break;
                    }
                }
                int expectedCount = expected?.matrices.Length ?? 0;
                Assert.That(count, Is.EqualTo(expectedCount), context + " " + Names[kind] + " count");
                for (int i = 0; i < count; i++)
                {
                    for (int column = 0; column < 4; column++)
                    {
                        for (int row = 0; row < 4; row++)
                        {
                            float value = expected.matrices[i].values[column * 4 + row];
                            value *= (row == 0 ? -1 : 1) * (column == 0 ? -1 : 1);
                            Assert.That(poses[i][row, column], Is.EqualTo(value).Within(0.001f), context + " " + Names[kind] + " instance " + i + " matrix " + row + "," + column);
                        }
                    }
                    if (expected.parameters.Length > i)
                    {
                        Assert.That(parameters[i].x, Is.EqualTo(expected.parameters[i]).Within(0.0002f), context + " " + Names[kind] + " parameter " + i);
                    }
                }
            }
        }

        private static void Set(object target, string property, object value)
        {
            target.GetType().GetProperty(property).SetValue(target, value);
        }

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
            public Batch[] batches;
        }

        [Serializable]
        private sealed class Batch
        {
            public string name;
            public Matrix[] matrices;
            public float[] parameters;
        }

        [Serializable]
        private sealed class Matrix
        {
            public float[] values;
        }
    }
}
