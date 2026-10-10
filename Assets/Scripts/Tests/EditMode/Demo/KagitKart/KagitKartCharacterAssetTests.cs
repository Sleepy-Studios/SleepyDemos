using Core.Editor.AssetNaming;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartCharacterAssetTests
    {
        [Test]
        public void DemoResourcesFollowProjectNamesAndRequiredLabels()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets/LoadResources/Demos/kagit_kart" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path) || LoadResourcesAssetNamingRules.ShouldSkipAssetPath(path))
                {
                    continue;
                }
                foreach (var issue in LoadResourcesAssetNamingRules.Validate(path))
                {
                    Assert.That(issue.Severity, Is.Not.EqualTo(NamingSeverity.Error), issue.ToString());
                }
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                var labels = AssetDatabase.GetLabels(asset);
                foreach (string label in LoadResourcesAssetNamingRules.GetLabels(path))
                {
                    Assert.That(labels, Does.Contain(label), path);
                }
            }
        }

        [TestCase("Fox")]
        [TestCase("Frog")]
        [TestCase("Penguin")]
        [TestCase("Bear")]
        [TestCase("Cat")]
        [TestCase("Owl")]
        [TestCase("Rabbit")]
        [TestCase("Octopus")]
        public void SavedCharacterHasBoundSkinAndOriginalPaperAtlas(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/LoadResources/Demos/kagit_kart/Prefabs/Characters/" + name + ".prefab");
            Assert.That(prefab, Is.Not.Null);
            var instance = Object.Instantiate(prefab);
            var baked = new Mesh();
            try
            {
                var renderer = instance.GetComponent<SkinnedMeshRenderer>();
                Assert.That(renderer, Is.Not.Null);
                Assert.That(renderer.rootBone, Is.Not.Null);
                Assert.That(renderer.sharedMesh, Is.Not.Null);
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.GetTexture("_BaseMap"), Is.Not.Null);
                var mesh = renderer.sharedMesh;
                Assert.That(mesh.bindposes.Length, Is.EqualTo(renderer.bones.Length));
                Assert.That(mesh.boneWeights.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.normals.Length, Is.EqualTo(mesh.vertexCount));
                foreach (var bone in renderer.bones)
                {
                    Assert.That(bone, Is.Not.Null);
                    Assert.That(bone.IsChildOf(instance.transform), Is.True);
                }
                foreach (var weight in mesh.boneWeights)
                {
                    Assert.That(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3, Is.EqualTo(1).Within(0.001));
                    if (weight.weight0 > 0)
                    {
                        Assert.That(weight.boneIndex0, Is.InRange(0, renderer.bones.Length - 1));
                    }
                    if (weight.weight1 > 0)
                    {
                        Assert.That(weight.boneIndex1, Is.InRange(0, renderer.bones.Length - 1));
                    }
                    if (weight.weight2 > 0)
                    {
                        Assert.That(weight.boneIndex2, Is.InRange(0, renderer.bones.Length - 1));
                    }
                    if (weight.weight3 > 0)
                    {
                        Assert.That(weight.boneIndex3, Is.InRange(0, renderer.bones.Length - 1));
                    }
                }
                renderer.BakeMesh(baked);
                var expected = mesh.vertices;
                var actual = baked.vertices;
                Assert.That(actual.Length, Is.EqualTo(expected.Length));
                for (int i = 0; i < actual.Length; i++)
                {
                    Assert.That(Vector3.Distance(actual[i], expected[i]), Is.LessThan(0.001), name + " bind pose vertex " + i);
                }
            }
            finally
            {
                Object.DestroyImmediate(baked);
                Object.DestroyImmediate(instance);
            }
        }
    }
}
