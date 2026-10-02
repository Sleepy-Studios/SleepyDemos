using System;
using System.Collections.Generic;
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        /// <summary>烘焙四区视觉内衬；不操作原建筑、碰撞、机台、安全点或区域权限。</summary>
        /// <param name="area">已生成的18×24米区域。</param>
        /// <param name="contents">区域已有的权限内容根。</param>
        /// <param name="palette">已持久化的十二色共享URP材质。</param>
        private static void InstallFormalArchitecture(JinxCasinoWorldArea area, Transform contents,
            Dictionary<string, Material> palette)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("建筑烘焙仅允许在Editor装配阶段执行。");
            string[] regionIds = { "lobby", "arcade", "backroom", "penthouse" };
            string[] wallIds = { "PaperLight", "Mint", "CreamYellow", "PaperLight" };
            string[] accentIds = { "PlumRed", "color_blue", "PlumRed", "color_violet" };
            int index = area.Index;
            if (index < 0 || index >= regionIds.Length || contents == null) throw new ArgumentException("建筑需要四区及原权限内容根。");
            string wall = wallIds[index], accent = accentIds[index];
            var batches = new Dictionary<string, List<CombineInstance>>(StringComparer.Ordinal);
            var old = contents.Find("FormalArchitecture"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("FormalArchitecture").transform; root.SetParent(contents, false);
            root.position = area.SafePosition + new Vector3(0, -0.03f, 7.5f); root.rotation = Quaternion.identity;
            var cube = CreateFormalArchitectureCube();
            try
            {
                // 顶棚下表面4.02米；现墙高4米，最高区域装饰3.98米。只视觉闭合，不增加顶棚碰撞。
                AddFormalArchitectureBox(batches, cube, wall, new Vector3(0, 4.10f, 0), new Vector3(18.2f, .16f, 24.2f));
                AddFormalArchitectureBox(batches, cube, "InkBlue", new Vector3(0, .003f, 0), new Vector3(17.72f, .006f, 23.5f));
                foreach (float z in new[] { -11.755f, 11.755f })
                {
                    AddFormalArchitectureBox(batches, cube, wall, new Vector3(0, 2, z), new Vector3(17.7f, 4, .018f));
                    AddFormalArchitectureBox(batches, cube, accent, new Vector3(0, .62f, z - Mathf.Sign(z) * .018f), new Vector3(17.7f, .62f, .02f));
                    AddFormalArchitectureBox(batches, cube, "CopperGold", new Vector3(0, 3.48f, z - Mathf.Sign(z) * .025f), new Vector3(17.7f, .075f, .025f));
                    foreach (float x in new[] { -7.9f, -2.65f, 2.65f, 7.9f })
                        AddFormalArchitectureBox(batches, cube, accent, new Vector3(x, 2.02f, z - Mathf.Sign(z) * .034f), new Vector3(.16f, 3.25f, .035f));
                }
                foreach (float x in new[] { -8.855f, 8.855f })
                {
                    // 侧门原空档[-9,-6]完整保留；前墙段[-12,-9]、后墙段[-6,12]分别内衬。
                    foreach (var segment in new[] { new Vector2(-10.5f, 3), new Vector2(3, 18) })
                    {
                        AddFormalArchitectureBox(batches, cube, wall, new Vector3(x, 2, segment.x), new Vector3(.018f, 4, segment.y));
                        AddFormalArchitectureBox(batches, cube, accent, new Vector3(x - Mathf.Sign(x) * .018f, .62f, segment.x), new Vector3(.02f, .62f, segment.y));
                        AddFormalArchitectureBox(batches, cube, "CopperGold", new Vector3(x - Mathf.Sign(x) * .025f, 3.48f, segment.x), new Vector3(.025f, .075f, segment.y));
                    }
                }
                // 各区采用不同的梁间距；宽色带及金边复用四个共享材质，不新建灯或发光材质。
                int ribs = index + 3;
                for (int rib = 0; rib < ribs; rib++)
                {
                    float z = Mathf.Lerp(-9, 9, rib / (float)(ribs - 1));
                    AddFormalArchitectureBox(batches, cube, accent, new Vector3(0, 3.99f, z), new Vector3(17.5f, .04f, .23f));
                    AddFormalArchitectureBox(batches, cube, "CopperGold", new Vector3(0, 3.965f, z), new Vector3(17.5f, .015f, .035f));
                }
                // 平面装饰顶面≤0.014米，不进入CC/任务的实际体积；入口到机台前留清晰中心引导。
                foreach (float x in new[] { -8.05f, 8.05f })
                    AddFormalArchitectureBox(batches, cube, accent, new Vector3(x, .009f, 0), new Vector3(.3f, .006f, 23));
                for (int arrow = 0; arrow < 4; arrow++)
                    foreach (float side in new[] { -1f, 1f })
                        AddFormalArchitectureBox(batches, cube, accent, new Vector3(side * .17f, .01f, -7 + arrow * .85f),
                            new Vector3(.12f, .006f, .52f), Quaternion.Euler(0, side * 35, 0));
                foreach (float side in new[] { -1f, 1f })
                    AddFormalArchitectureBox(batches, cube, "CopperGold", new Vector3(side * 5.7f, .01f, -7.5f), new Vector3(4.5f, .006f, .10f));

                // 四个正式Region实例烘焙进同四组材质；独立轮廓仍保留，不为重复FBX增加逐实例draw。
                BakeFormalRegionDecoration(root, "Region_" + regionIds[index], new Vector3(0, 0, 11), Quaternion.Euler(0, 180, 0), batches, palette, wall, accent);
                BakeFormalRegionDecoration(root, "Region_" + regionIds[index], new Vector3(-6.5f, 0, -7.5f), Quaternion.identity, batches, palette, wall, accent);
                BakeFormalRegionDecoration(root, "Region_" + regionIds[index], new Vector3(-8.15f, 0, 5), Quaternion.Euler(0, 90, 0), batches, palette, wall, accent);
                BakeFormalRegionDecoration(root, "Region_" + regionIds[index], new Vector3(8.15f, 0, 5), Quaternion.Euler(0, 270, 0), batches, palette, wall, accent);
                if (batches.Count > 4) throw new InvalidOperationException("四区建筑及装饰不得超过四个共享材质段。");
                EnsureFolder(Root + "/Art/Meshes");
                foreach (var batch in batches.OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    var mesh = new Mesh { name = "FormalRoom" + index + "_" + batch.Key };
                    mesh.CombineMeshes(batch.Value.ToArray(), true, true, false); mesh.RecalculateBounds();
                    string path = Root + "/Art/Meshes/" + mesh.name + ".asset";
                    var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (saved == null) { AssetDatabase.CreateAsset(mesh, path); saved = mesh; }
                    else { EditorUtility.CopySerialized(mesh, saved); EditorUtility.SetDirty(saved); Object.DestroyImmediate(mesh); }
                    var surface = new GameObject(batch.Key, typeof(MeshFilter), typeof(MeshRenderer)); surface.transform.SetParent(root, false);
                    surface.GetComponent<MeshFilter>().sharedMesh = saved;
                    var renderer = surface.GetComponent<MeshRenderer>(); renderer.sharedMaterial = palette[batch.Key];
                    // 包含顶棚的静态建筑段全部不投影，避免遮断原唯一Directional Light；仍接收机台阴影。
                    renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                }
                foreach (string name in new[] { "FormalBackDecor", "FormalFrontDecor" })
                { var previous = contents.Find(name); if (previous != null) Object.DestroyImmediate(previous.gameObject); }
            }
            finally { Object.DestroyImmediate(cube); }
        }

        private static void BakeFormalRegionDecoration(Transform root, string modelId, Vector3 position, Quaternion rotation,
            Dictionary<string, List<CombineInstance>> batches, Dictionary<string, Material> palette, string wall, string accent)
        {
            var staging = new GameObject("RegionBake").transform; staging.SetParent(root, false);
            staging.localPosition = position; staging.localRotation = rotation;
            try
            {
                var model = InstantiateFormalModel(modelId, staging, palette);
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<MeshRenderer>(); var materials = renderer.sharedMaterials;
                    if (materials.Length != filter.sharedMesh.subMeshCount) throw new InvalidOperationException("Region材质槽与子网格不匹配：" + modelId);
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        string source = materials[slot].name;
                        string key = source == "InkBlue" || source == "CopperGold" ? source : source == "PlumRed" ? accent : wall;
                        AddFormalArchitecturePart(batches, key, new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = slot,
                            transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                    }
                }
            }
            finally { Object.DestroyImmediate(staging.gameObject); }
        }

        private static void AddFormalArchitectureBox(Dictionary<string, List<CombineInstance>> batches, Mesh cube,
            string material, Vector3 position, Vector3 size, Quaternion rotation = default)
        {
            if (rotation.Equals(default(Quaternion))) rotation = Quaternion.identity;
            AddFormalArchitecturePart(batches, material, new CombineInstance { mesh = cube, subMeshIndex = 0,
                transform = Matrix4x4.TRS(position, rotation, size) });
        }
        private static void AddFormalArchitecturePart(Dictionary<string, List<CombineInstance>> batches, string material, CombineInstance part)
        {
            if (!batches.TryGetValue(material, out var parts)) { parts = new List<CombineInstance>(); batches.Add(material, parts); }
            parts.Add(part);
        }

        private static Mesh CreateFormalArchitectureCube()
        {
            // Editor临时单位立方体，仅有网格数据；不通过CreatePrimitive创建任何Collider。
            var normals = new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            var tangents = new[] { Vector3.back, Vector3.forward, Vector3.right, Vector3.right, Vector3.right, Vector3.left };
            var bitangents = new[] { Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up };
            var vertices = new Vector3[24]; var vertexNormals = new Vector3[24]; var uv = new Vector2[24]; var triangles = new int[36];
            for (int face = 0; face < 6; face++)
            {
                int vertex = face * 4; Vector3 center = normals[face] * .5f, tangent = tangents[face] * .5f, bitangent = bitangents[face] * .5f;
                vertices[vertex] = center - tangent - bitangent; vertices[vertex + 1] = center + tangent - bitangent;
                vertices[vertex + 2] = center + tangent + bitangent; vertices[vertex + 3] = center - tangent + bitangent;
                for (int corner = 0; corner < 4; corner++) vertexNormals[vertex + corner] = normals[face];
                uv[vertex] = Vector2.zero; uv[vertex + 1] = Vector2.right; uv[vertex + 2] = Vector2.one; uv[vertex + 3] = Vector2.up;
                int triangle = face * 6; triangles[triangle] = vertex; triangles[triangle + 1] = vertex + 1; triangles[triangle + 2] = vertex + 2;
                triangles[triangle + 3] = vertex; triangles[triangle + 4] = vertex + 2; triangles[triangle + 5] = vertex + 3;
            }
            var mesh = new Mesh { name = "FormalArchitectureCube", vertices = vertices, normals = vertexNormals, uv = uv, triangles = triangles };
            mesh.RecalculateBounds(); return mesh;
        }
    }
}
