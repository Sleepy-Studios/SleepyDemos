#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    /// Unity实际Importer门禁，验证作者源的机台热区与运行坐标；不能代替最终画面和射线实玩。
    public sealed class JinxCasinoImmersionModelTests
    {
        private const string Root = "Assets/LoadResources/Demos/jinx_casino/Art/Immersion";

        [TestCase("S1Slots")]
        [TestCase("S1Blackjack")]
        [TestCase("S1Levers")]
        [TestCase("S1Hall")]
        public void ImportedModelHasIdentityTransformsAndDedicatedUrpMaterials(string name)
        {
            string path = Root + "/Models/" + name + ".fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(model, Is.Not.Null);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.globalScale, Is.EqualTo(1));
            Assert.That(importer.importAnimation || importer.importCameras || importer.importLights || importer.addCollider || importer.isReadable, Is.False);
            Assert.That(model.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(model.GetComponentsInChildren<Collider>(true), Is.Empty);
            foreach (var node in model.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(Quaternion.Angle(node.localRotation, Quaternion.identity), Is.LessThan(.01f), node.name);
                Assert.That(Vector3.Distance(node.localScale, Vector3.one), Is.LessThan(.0001f), node.name);
            }
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, renderer.name);
                    Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                    Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith(Root + "/Materials/"));
                }
            long indices = model.GetComponentsInChildren<MeshFilter>(true).Sum(filter =>
                Enumerable.Range(0, filter.sharedMesh.subMeshCount).Sum(index => (long)filter.sharedMesh.GetIndexCount(index)));
            Assert.That(indices / 3, Is.InRange(1000, 40000), "单模型预算含牌库，不是场景每帧预算。");
        }

        [Test]
        public void AllTwentyTwoAuthoredOperationNodesMatchUnityTableCoordinates()
        {
            var layout = JsonUtility.FromJson<Blueprint>(File.ReadAllText(Root + "/S1Layout.json"));
            int count = 0;
            foreach (var table in layout.stations)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Models/" + table.model + ".fbx");
                Assert.That(model, Is.Not.Null, table.model);
                var nodes = model.GetComponentsInChildren<Transform>(true);
                foreach (var target in table.targets)
                {
                    var node = nodes.Single(value => value.name == target.node);
                    var actual = model.transform.InverseTransformPoint(node.position);
                    var expected = new Vector3(target.position[0], target.position[1], target.position[2]);
                    Assert.That(Vector3.Distance(actual, expected), Is.LessThan(.001f), table.model + "/" + target.node);
                    Assert.That(node.GetComponentsInChildren<Renderer>(true), Is.Not.Empty);
                    count++;
                }
            }
            Assert.That(count, Is.EqualTo(22));
        }

        [Serializable] private sealed class Blueprint { public Station[] stations; }
        [Serializable] private sealed class Station { public string model; public Target[] targets; }
        [Serializable] private sealed class Target { public string node; public float[] position; }
    }
}
#endif
