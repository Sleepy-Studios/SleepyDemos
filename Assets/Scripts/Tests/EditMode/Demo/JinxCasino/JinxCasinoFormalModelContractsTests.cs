using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    /// 直接读取65个FBX资产，不实例化到用户场景；源manifest是可维护的形状、层级与轴合同。
    public sealed class JinxCasinoFormalModelContractsTests
    {
        private const string ModelRoot = "Assets/LoadResources/Demos/jinx_casino/Art/Models/";
        public static IEnumerable FormalModels()
        {
            foreach (var entry in ReadManifest().models) yield return new TestCaseData(entry.name).SetName("FormalModel_" + entry.name);
        }

        [Test]
        public void ManifestContainsTheCompleteOriginalModelFamiliesAndUnityAxisContract()
        {
            var manifest = ReadManifest(); Assert.That(manifest.models.Length, Is.EqualTo(65));
            Assert.That(manifest.models.Select(model => model.name).Distinct().Count(), Is.EqualTo(65));
            foreach (var count in new Dictionary<string, int> { { "Avatar", 1 }, { "Station", 17 }, { "Item", 24 }, { "Hat", 5 }, { "Face", 8 }, { "Mission", 6 }, { "Region", 4 } })
                Assert.That(manifest.models.Count(model => model.category == count.Key), Is.EqualTo(count.Value), count.Key);
            CollectionAssert.AreEquivalent(CasinoContentCatalog.Games.Select(game => game.Kind.ToString()), manifest.models.Where(model => model.category == "Station").Select(model => model.name));
            CollectionAssert.AreEquivalent(CasinoContentCatalog.Items.Select(item => "Item_" + item.Id), manifest.models.Where(model => model.category == "Item").Select(model => model.name));
            Assert.That(manifest.sourceFront, Is.EqualTo("-Y")); Assert.That(manifest.sourceUp, Is.EqualTo("+Z")); Assert.That(manifest.sourceRight, Is.EqualTo("-X"));
            Assert.That(manifest.unityFront, Is.EqualTo("+Z")); Assert.That(manifest.unityUp, Is.EqualTo("+Y")); Assert.That(manifest.unityRight, Is.EqualTo("+X"));
        }

        [TestCaseSource(nameof(FormalModels))]
        public void FormalFbxPreservesShapeSemanticNodesIdentityAndUrpMaterials(string name)
        {
            var entry = ReadManifest().models.Single(model => model.name == name);
            string path = ModelRoot + entry.file;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); Assert.That(prefab, Is.Not.Null, path);
            var nodes = prefab.GetComponentsInChildren<Transform>(true);
            var invalidPoses = new List<string>();
            foreach (var node in nodes)
            {
                float angle = Quaternion.Angle(node.localRotation, Quaternion.identity);
                if (angle > 0.01f || node.localScale.x <= 0 || node.localScale.y <= 0 || node.localScale.z <= 0)
                    invalidPoses.Add(node.name + " angle=" + angle + " quaternion=" + node.localRotation.ToString("F6") + " scale=" + node.localScale.ToString("F6"));
            }
            Assert.That(invalidPoses, Is.Empty, name + " 内部轴合同失败：\n" + string.Join("\n", invalidPoses));
            Assert.That(Vector3.Distance(prefab.transform.localScale, Vector3.one), Is.LessThan(0.0001f), "FBX根米制Scale必须为1。");
            Assert.That(prefab.GetComponentsInChildren<Camera>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<AudioListener>(true), Is.Empty);
            foreach (string nodeName in entry.nodeNames) Assert.That(nodes.Any(node => node.name == nodeName), Is.True, name + " 缺少 " + nodeName);
            foreach (var pivot in entry.pivots)
            {
                var node = nodes.Single(value => value.name == pivot.name);
                Vector3 expected = SourceToUnity(pivot.sourcePosition);
                Vector3 position = prefab.transform.InverseTransformPoint(node.position);
                Assert.That(Vector3.Distance(position, expected), Is.LessThan(0.003f), pivot.name + " 世界Pivot预期=" + expected + " 实际=" + position);
                Assert.That(Vector3.Distance(node.localPosition, SourceToUnity(pivot.localPosition)), Is.LessThan(0.003f), pivot.name + " 局部Pivot");
                Assert.That(Vector3.Dot(prefab.transform.InverseTransformDirection(node.right).normalized, Vector3.right), Is.GreaterThan(0.999f), pivot.name + " +X");
                Assert.That(Vector3.Dot(prefab.transform.InverseTransformDirection(node.up).normalized, Vector3.up), Is.GreaterThan(0.999f), pivot.name + " +Y");
                Assert.That(Vector3.Dot(prefab.transform.InverseTransformDirection(node.forward).normalized, Vector3.forward), Is.GreaterThan(0.999f), pivot.name + " +Z");
            }
            var filters = prefab.GetComponentsInChildren<MeshFilter>(true); Assert.That(filters.Length, Is.EqualTo(entry.meshCount));
            long triangles = 0; bool hasBounds = false; var bounds = new Bounds();
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh; Assert.That(mesh, Is.Not.Null);
                for (int sub = 0; sub < mesh.subMeshCount; sub++) { Assert.That(mesh.GetTopology(sub), Is.EqualTo(MeshTopology.Triangles)); triangles += mesh.GetIndexCount(sub) / 3; }
                Matrix4x4 matrix = prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var localBounds = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 offset = Vector3.Scale(localBounds.extents, new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    Vector3 point = matrix.MultiplyPoint3x4(localBounds.center + offset);
                    if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; } else bounds.Encapsulate(point);
                }
            }
            Assert.That(triangles, Is.EqualTo(entry.triangles), "导入三角面数与导出manifest一致。");
            Assert.That(Vector3.Distance(bounds.min, Vector(entry.boundsUnity.min)), Is.LessThan(0.015f), name + " min实际=" + bounds.min + " manifest=" + Vector(entry.boundsUnity.min));
            Assert.That(Vector3.Distance(bounds.max, Vector(entry.boundsUnity.max)), Is.LessThan(0.015f), name + " max实际=" + bounds.max + " manifest=" + Vector(entry.boundsUnity.max));
            if (entry.category == "Station") { Assert.That(bounds.size.x, Is.LessThanOrEqualTo(3.001f)); Assert.That(bounds.size.z, Is.LessThanOrEqualTo(3.001f)); Assert.That(Mathf.Abs(bounds.min.y), Is.LessThan(0.003f)); }
            var materialNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, name); Assert.That(AssetDatabase.Contains(material), Is.True, "共享材质需要持久化资产引用。");
                    Assert.That(material.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal), Is.True, name + "/" + material.name + "=" + material.shader.name);
                    Assert.That(material.enableInstancing, Is.True); materialNames.Add(material.name);
                }
            CollectionAssert.AreEquivalent(entry.materialNames, materialNames);
            using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create())
                Assert.That(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant(), Is.EqualTo(entry.sha256), name + " Source复制SHA");
        }

        private static ModelManifest ReadManifest()
        { return JsonUtility.FromJson<ModelManifest>(File.ReadAllText(ModelRoot + "manifest.json")); }
        private static Vector3 Vector(float[] values) => new Vector3(values[0], values[1], values[2]);
        // 已通过AxisGate真实Unity门禁的映射；不是运行时补偿，也不修改任何资产Transform。
        private static Vector3 SourceToUnity(float[] values) => new Vector3(-values[0], values[2], -values[1]);
        [Serializable] private sealed class ModelManifest { public string sourceFront, sourceUp, sourceRight, unityFront, unityUp, unityRight; public ModelEntry[] models; }
        [Serializable] private sealed class ModelEntry { public string name, category, file, sha256; public int triangles, meshCount; public string[] nodeNames, materialNames; public ModelBounds boundsUnity; public ModelPivot[] pivots; }
        [Serializable] private sealed class ModelBounds { public float[] min, max; }
        [Serializable] private sealed class ModelPivot { public string name; public float[] sourcePosition, localPosition; }
    }
}
