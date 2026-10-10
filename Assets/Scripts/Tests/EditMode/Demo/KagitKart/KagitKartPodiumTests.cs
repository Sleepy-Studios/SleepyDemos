using System;
using System.Collections;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartPodiumTests
    {
        [TestCase("normal")]
        [TestCase("reduced")]
        [TestCase("portrait")]
        [TestCase("fallback")]
        public void CeremonyMatchesOriginalPodiumJavaScript(string scenario)
        {
            var fixture = JsonUtility.FromJson<Fixtures>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalPodium.json").text);
            Assert.That(fixture.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            var run = Array.Find(fixture.runs, r => r.scenario == scenario);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/LoadResources/Demos/kagit_kart/Prefabs/PodiumStage.prefab"));
            var cameraObject = new GameObject("PodiumFixtureCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.aspect = run.aspect;
                var podium = instance.GetComponent<KagitKartPodiumStage>();
                podium.Initialize(camera);
                int completed = 0;
                podium.Completed += () => completed++;
                podium.Show(run.ids, run.sadId, run.rm);
                for (int step = 1; step <= 600; step++)
                {
                    podium.Advance(1f / 60);
                    var frame = Array.Find(run.frames, f => f.step == step);
                    if (frame != null)
                    {
                        Check(podium, camera, frame, scenario + " frame " + step);
                    }
                }
                Assert.That(completed, Is.EqualTo(1));
                Assert.Throws<ArgumentException>(() => podium.Advance(float.NaN));
                podium.Hide();
                Assert.That(Get<int>(podium, "used"), Is.Zero);
                Assert.That(Get<IList>(podium, "heroes"), Is.Empty);
                Assert.That(podium.IsComplete, Is.False);
                Assert.That(podium.blocksFilter.gameObject.activeSelf, Is.False);
                // 重复一轮的随机流与角色生命周期必须复位。
                podium.Show(run.ids, run.sadId, run.rm);
                podium.Advance(1f / 60);
                Check(podium, camera, run.frames[0], scenario + " reset");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static void Check(KagitKartPodiumStage podium, Camera camera, Frame expected, string context)
        {
            Assert.That(podium.IsComplete, Is.EqualTo(expected.complete), context + " complete");
            var mesh = podium.blocksFilter.sharedMesh;
            Vectors(mesh.vertices, expected.positions, context + " positions");
            Vectors(mesh.normals, expected.normals, context + " normals");
            var blocks = Get<Array>(podium, "blocks");
            for (int i = 0; i < blocks.Length; i++)
            {
                var actual = blocks.GetValue(i);
                var b = expected.blocks[i];
                Assert.That(Get<double>(actual, "u"), Is.EqualTo(b.u).Within(0.00001), context + " unfold " + i);
                Assert.That(Get<double>(actual, "v"), Is.EqualTo(b.v).Within(0.0001), context + " spring " + i);
                Assert.That(Get<double>(actual, "target"), Is.EqualTo(b.target), context + " target " + i);
                Assert.That(Get<double>(actual, "delay"), Is.EqualTo(b.delay).Within(0.00001), context + " delay " + i);
            }
            var heroes = Get<IList>(podium, "heroes");
            Assert.That(heroes.Count, Is.EqualTo(expected.karts.Length));
            for (int i = 0; i < heroes.Count; i++)
            {
                object hero = heroes[i];
                var e = expected.karts[i];
                var holder = Get<Transform>(hero, "holder");
                Assert.That(Get<int>(hero, "place"), Is.EqualTo(e.place));
                Assert.That(Get<bool>(hero, "landed"), Is.EqualTo(e.landed));
                Assert.That(holder.gameObject.activeSelf, Is.EqualTo(e.visible), context + " visibility " + i);
                Vector(holder.localPosition, e.position, true, context + " hero position " + i);
                Vector(holder.localScale, e.scale, false, context + " hero scale " + i);
                Assert.That(Get<double>(hero, "sq"), Is.EqualTo(e.sq).Within(0.00001), context + " hero spring " + i);
                Assert.That(Get<double>(hero, "sqv"), Is.EqualTo(e.sqv).Within(0.0001), context + " hero velocity " + i);
            }
            Assert.That(Get<int>(podium, "used"), Is.EqualTo(expected.confetti.Length), context + " confetti count");
            var poses = Get<Matrix4x4[]>(podium, "confettiPoses");
            for (int i = 0; i < expected.confetti.Length; i++)
            {
                for (int column = 0; column < 4; column++)
                {
                    for (int row = 0; row < 4; row++)
                    {
                        float value = expected.confetti[i].values[column * 4 + row] * (row == 0 ? -1 : 1) * (column == 0 ? -1 : 1);
                        Assert.That(poses[i][row, column], Is.EqualTo(value).Within(0.004f), context + " paper " + i + " " + row + "," + column);
                    }
                }
            }
            Vector(camera.transform.position, expected.cameraPosition, true, context + " camera");
            var target = new Vector3(-expected.cameraLook[0], expected.cameraLook[1], expected.cameraLook[2]);
            Assert.That(Vector3.Distance(camera.transform.forward, (target - camera.transform.position).normalized), Is.LessThan(0.0001f), context + " camera direction");
            Assert.That(camera.fieldOfView, Is.EqualTo(expected.vfov).Within(0.0001f), context + " fov");
        }

        private static void Vectors(Vector3[] actual, float[] expected, string context)
        {
            Assert.That(actual.Length * 3, Is.EqualTo(expected.Length));
            for (int i = 0; i < actual.Length; i++)
            {
                Assert.That(actual[i].x, Is.EqualTo(-expected[i * 3]).Within(0.0001f), context + " x " + i);
                Assert.That(actual[i].y, Is.EqualTo(expected[i * 3 + 1]).Within(0.0001f), context + " y " + i);
                Assert.That(actual[i].z, Is.EqualTo(expected[i * 3 + 2]).Within(0.0001f), context + " z " + i);
            }
        }

        private static void Vector(Vector3 actual, float[] expected, bool reflect, string context)
        {
            var value = new Vector3(expected[0] * (reflect ? -1 : 1), expected[1], expected[2]);
            Assert.That(Vector3.Distance(actual, value), Is.LessThan(0.0001f), context);
        }

        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(target);

        [Serializable]
        private sealed class Fixtures
        {
            public string sourceCommit;
            public Run[] runs;
        }

        [Serializable]
        private sealed class Run
        {
            public string scenario, sadId;
            public bool rm;
            public float aspect;
            public string[] ids;
            public Frame[] frames;
        }

        [Serializable]
        private sealed class Frame
        {
            public int step;
            public bool complete;
            public float[] positions, normals, cameraPosition, cameraLook;
            public float vfov;
            public Block[] blocks;
            public Hero[] karts;
            public Matrix[] confetti;
        }

        [Serializable]
        private sealed class Block
        {
            public double u, v, target, delay;
        }

        [Serializable]
        private sealed class Hero
        {
            public int place;
            public bool visible, landed;
            public float[] position, scale;
            public double sq, sqv;
        }

        [Serializable]
        private sealed class Matrix
        {
            public float[] values;
        }
    }
}
