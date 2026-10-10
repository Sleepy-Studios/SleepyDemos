using System;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartSceneryDynamicsTests
    {
        [TestCase("meadow")]
        [TestCase("bosphorus")]
        [TestCase("glacier")]
        [TestCase("desk")]
        public void ChapterUpdatersMatchOriginalJavaScript(string id)
        {
            var fixtures = JsonUtility.FromJson<Fixtures>(AssetDatabase.LoadAssetAtPath<TextAsset>(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalSceneryDynamics.json").text);
            Assert.That(fixtures.sourceCommit, Is.EqualTo(KagitKartCatalog.SourceCommit));
            var run = Array.Find(fixtures.runs, r => r.id == id);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/LoadResources/Demos/kagit_kart/Prefabs/Tracks/" + KagitKartCatalog.TrackAssetName(id) + ".prefab"));
            var cameraObject = new GameObject("SceneryDynamicsFixtureCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.transform.position = new Vector3(-12, 8, -24);
                var world = instance.GetComponent<KagitKartSceneryWorld>();
                world.Render(camera, 0, 0, 0);
                var poses = (Matrix4x4[][])Field(world, "poses");
                // 原作对照固定纸偶完整立起；风车叶片仍读取同一帧父纸偶矩阵。
                for (int b = 0; b < poses.Length; b++)
                {
                    Array.Copy(world.Asset.batches[b].poses, poses[b], poses[b].Length);
                }
                var bindings = JsonUtility.FromJson<BindingData>(world.Asset.dynamicsData.text).bindings;
                var advance = typeof(KagitKartSceneryWorld).GetMethod("AdvanceDynamics", BindingFlags.Instance | BindingFlags.NonPublic);
                var shared = (Matrix4x4[])world.Asset.batches[0].poses.Clone();
                for (int step = 1; step <= 480; step++)
                {
                    advance.Invoke(world, new object[] { (double)(1f / 60), camera });
                    var frame = Array.Find(run.frames, f => f.step == step);
                    if (frame == null)
                    {
                        continue;
                    }
                    foreach (var group in frame.poses)
                    {
                        int batch = (int)bindings.GetType().GetField(group.role).GetValue(bindings);
                        Assert.That(batch, Is.GreaterThanOrEqualTo(0), id + " " + group.role);
                        Assert.That(poses[batch].Length, Is.EqualTo(group.matrices.Length));
                        for (int i = 0; i < poses[batch].Length; i++)
                        {
                            for (int col = 0; col < 4; col++)
                            {
                                for (int row = 0; row < 4; row++)
                                {
                                    float expected = group.matrices[i].values[col * 4 + row] * (row == 0 ? -1 : 1) * (col == 0 ? -1 : 1);
                                    Assert.That(poses[batch][i][row, col], Is.EqualTo(expected).Within(0.001f),
                                        id + " " + step + " " + group.role + " " + i + " " + row + "," + col);
                                }
                            }
                        }
                    }
                }
                Assert.That(world.Asset.batches[0].poses, Is.EqualTo(shared), "动态不能写共享资产");
                double time = (double)Field(world, "dynamicsTime");
                advance.Invoke(world, new object[] { 0.0, camera });
                Assert.That((double)Field(world, "dynamicsTime"), Is.EqualTo(time), "暂停时冻结动态时间");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(cameraObject);
            }
        }

        private static object Field(object target, string field) => target.GetType().GetField(field,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

        [Serializable]
        private sealed class Fixtures
        {
            public string sourceCommit;
            public Run[] runs;
        }

        [Serializable]
        private sealed class Run
        {
            public string id;
            public Frame[] frames;
        }

        [Serializable]
        private sealed class Frame
        {
            public int step;
            public Poses[] poses;
        }

        [Serializable]
        private sealed class Poses
        {
            public string role;
            public Matrix[] matrices;
        }

        [Serializable]
        private sealed class Matrix
        {
            public float[] values;
        }

        [Serializable]
        private sealed class BindingData
        {
            public Bindings bindings;
        }

        [Serializable]
        private sealed class Bindings
        {
            public int cloud, strings, sails, ferry, ferryLights, smoke, gondola, steam;
        }
    }
}
