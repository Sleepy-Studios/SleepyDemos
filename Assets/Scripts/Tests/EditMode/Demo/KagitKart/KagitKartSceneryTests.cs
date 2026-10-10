using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartSceneryTests
    {
        [TestCase("Meadow", "meadow")]
        [TestCase("Bosphorus", "bosphorus")]
        [TestCase("Glacier", "glacier")]
        [TestCase("Desk", "desk")]
        public void TrackWorldUsesSavedResourcesAndPauseDoesNotMoveRamp(string name, string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/LoadResources/Demos/kagit_kart/Prefabs/Tracks/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            var cameraObject = new GameObject("纸景测试摄像机");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                var world = instance.GetComponent<KagitKartSceneryWorld>();
                var asset = world.Asset;
                Assert.That(asset.TrackId, Is.EqualTo(id));
                Assert.That(asset.length, Is.GreaterThan(0));
                Assert.That(asset.batches, Is.Not.Empty);
                Assert.That(asset.popups, Is.Not.Empty);
                Assert.That(asset.ramps, Is.Not.Empty);
                foreach (var batch in asset.batches)
                {
                    Assert.That(batch.poses.Length, Is.InRange(1, 1023));
                    Assert.That(batch.colors.Length, Is.EqualTo(batch.poses.Length));
                    Assert.That(AssetDatabase.Contains(batch.mesh), Is.True);
                    foreach (var material in batch.materials)
                    {
                        Assert.That(AssetDatabase.Contains(material), Is.True);
                        Assert.That(material.enableInstancing, Is.True);
                        Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
                    }
                }
                foreach (var group in asset.popups)
                {
                    foreach (int batch in group.batches)
                    {
                        Assert.That(batch, Is.InRange(0, asset.batches.Length - 1));
                        Assert.That(asset.batches[batch].poses.Length, Is.EqualTo(group.items.Length));
                    }
                }
                var firstSavedPose = asset.batches[0].poses[0];
                float distance = asset.ramps[0].distance;
                world.Render(camera, distance, 0, 0, false, true);
                Quaternion upright = world.rampTransforms[0].localRotation;
                world.Render(camera, distance + 90, 0.1f, 0.1f);
                Quaternion folding = world.rampTransforms[0].localRotation;
                Assert.That(Quaternion.Angle(upright, folding), Is.GreaterThan(0.01f));
                world.Render(camera, distance + 90, 0, 0.1f);
                Assert.That(Quaternion.Angle(folding, world.rampTransforms[0].localRotation), Is.LessThan(0.001f));
                Assert.That(asset.batches[0].poses[0], Is.EqualTo(firstSavedPose), "共享资产不应被比赛修改。");
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(instance);
            }
        }
    }
}
