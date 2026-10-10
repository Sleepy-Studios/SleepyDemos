using System;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartCharacterAnimationTests
    {
        [TestCase("Fox")]
        [TestCase("Frog")]
        [TestCase("Penguin")]
        [TestCase("Bear")]
        [TestCase("Cat")]
        [TestCase("Owl")]
        [TestCase("Rabbit")]
        [TestCase("Octopus")]
        public void BonesMatchOriginalAnimateJavaScript(string character)
        {
            const string root = "Assets/LoadResources/Demos/kagit_kart/";
            var source = KagitKartSourceCatalog.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "Data/SourceCatalog.json").text);
            var data = KagitKartTrackData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "Data/Tracks/Meadow.json").text);
            var track = new KagitKartTrackQuery(data, source.config.track.gridCell, source.config.track.waterDrop, source.config.track.deskDrop);
            var fixtures = JsonUtility.FromJson<Fixtures>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalCharacterAnimation.json").text);
            Assert.That(fixtures.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            int index = Array.IndexOf(new[] { "Fox", "Frog", "Penguin", "Bear", "Cat", "Owl", "Rabbit", "Octopus" }, character);
            foreach (var run in fixtures.runs)
            {
                if (run.character != character)
                {
                    continue;
                }
                var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(root + "Prefabs/Characters/" + character + ".prefab"));
                try
                {
                    var skin = actor.GetComponent<SkinnedMeshRenderer>();
                    var visual = actor.GetComponent<KagitKartCharacterVisual>();
                    Assert.That(visual, Is.Not.Null, character);
                    Assert.That(skin.sharedMaterial.shader.name, Is.EqualTo("KagitKart/Paper"));
                    foreach (string anchor in new[] { "wheelRL", "wheelRR", "exhaust", "head" })
                    {
                        Assert.That(visual.GetAnchor(anchor), Is.Not.Null, character + " " + anchor);
                    }
                    var bones = skin.bones;
                    bool driving = run.scenario is not ("idle" or "cheer" or "sad" or "dizzy");
                    var vehicle = driving ? new KagitKartVehicle(source.config, track, source.roster[index], 120, false) : null;
                    if (vehicle != null)
                    {
                        vehicle.Controls = new KagitKartControls { Steer = 0.65f, Throttle = 1, LookBack = true };
                        Set(vehicle, "Speed", 22f);
                        Set(vehicle, "Drifting", true);
                        Set(vehicle, "DriftDirection", 1);
                        Set(vehicle, "DriftTier", 2);
                        Set(vehicle, "BoostTime", 1f);
                        Set(vehicle, "InkTime", 1f);
                        Set(vehicle, "Surface", "sand");
                        if (run.scenario == "rescue")
                        {
                            Set(vehicle, "RescueStage", 1);
                        }
                    }
                    visual.Bind(vehicle, source.config.kart);
                    if (!driving)
                    {
                        visual.SetEmotion(run.scenario);
                    }
                    for (int step = 0; step < run.steps; step++)
                    {
                        if (run.scenario is "gum" or "plane" or "foil" or "burnout" or "scissors" or "default")
                        {
                            Set(vehicle, "SpinCause", run.scenario);
                            Set(vehicle, "SpinTime", Mathf.Max(0, source.config.kart.spins[run.scenario].time - step * run.dt));
                        }
                        if (run.scenario == "landing")
                        {
                            Set(vehicle, "Grounded", step >= 15);
                        }
                        if (run.scenario == "drive" && step == 0)
                        {
                            visual.Bump(source.config.kart.bumpImpulse);
                        }
                        if (run.scenario == "trick" && step == 0)
                        {
                            var signal = (Action<KagitKartVehicle, KagitKartVehicleSignal>)typeof(KagitKartVehicle)
                                .GetField("Signaled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(vehicle);
                            signal(vehicle, KagitKartVehicleSignal.Trick);
                        }
                        visual.Advance(run.dt, run.scenario == "reduced");
                    }
                    CheckPose(bones[0], run.rig, character + " " + run.scenario + " rig");
                    for (int i = 1; i < bones.Length; i++)
                    {
                        CheckPose(bones[i], run.bones[i], character + " " + run.scenario + " " + bones[i].name);
                    }
                    var previous = bones[10].localRotation;
                    visual.Advance(0);
                    Assert.That(bones[10].localRotation, Is.EqualTo(previous), "零时间必须冻结表现");
                    Assert.That(actor.transform.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(actor.transform.localRotation, Is.EqualTo(Quaternion.identity));
                    Assert.Throws<ArgumentOutOfRangeException>(() => visual.Advance(float.NaN));
                }
                finally
                {
                    Object.DestroyImmediate(actor);
                }
            }
        }

        private static void Set(KagitKartVehicle vehicle, string property, object value)
        {
            // 对照数据直接注入规则快照，避免把运动模拟误差混入纸偶算法验证。
            typeof(KagitKartVehicle).GetProperty(property).SetValue(vehicle, value);
        }

        private static void CheckPose(Transform bone, Pose pose, string context)
        {
            var position = new Vector3(-pose.position[0], pose.position[1], pose.position[2]);
            var rotation = new Quaternion(pose.rotation[0], -pose.rotation[1], -pose.rotation[2], pose.rotation[3]);
            var scale = new Vector3(pose.scale[0], pose.scale[1], pose.scale[2]);
            Assert.That(Vector3.Distance(bone.localPosition, position), Is.LessThan(0.0002f), context + " position");
            Assert.That(1 - Mathf.Abs(Quaternion.Dot(bone.localRotation, rotation)), Is.LessThan(0.000002f), context + " rotation");
            Assert.That(Vector3.Distance(bone.localScale, scale), Is.LessThan(0.0002f), context + " scale");
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
            public string character;
            public string scenario;
            public float dt;
            public int steps;
            public Pose rig;
            public Pose[] bones;
        }

        [Serializable]
        private sealed class Pose
        {
            public float[] position;
            public float[] rotation;
            public float[] scale;
        }
    }
}
