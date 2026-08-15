using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.DroneFlight
{
    /// <summary>
    /// 在 Unity 原生轴烘焙之后校正正式 FBX 的航向手性，使 Blender -Y 前向落到 Unity +Z。
    /// </summary>
    internal sealed class DroneFlightModelAxisPostprocessor : AssetPostprocessor
    {
        private const string ModelPath =
            "Assets/LoadResources/Demos/drone_flight/Art/Models/DroneFlight.fbx";

        private static readonly Quaternion HeadingCorrection = Quaternion.Euler(0f, 180f, 0f);
        private static readonly Quaternion InverseHeadingCorrection = Quaternion.Inverse(HeadingCorrection);

        private void OnPostprocessModel(GameObject modelRoot)
        {
            if (assetPath != ModelPath || assetImporter is not ModelImporter importer
                                       || !importer.bakeAxisConversion)
            {
                return;
            }

            CorrectNodeTransforms(modelRoot.transform);
            CorrectImportedMeshes(modelRoot);
            BakeImportedNodeRotations(modelRoot.transform);
        }

        private static void CorrectNodeTransforms(Transform modelRoot)
        {
            foreach (var node in modelRoot.GetComponentsInChildren<Transform>(true))
            {
                if (node == modelRoot)
                {
                    continue;
                }

                node.localPosition = HeadingCorrection * node.localPosition;
                var correctedRotation = HeadingCorrection * node.localRotation * InverseHeadingCorrection;
                node.localRotation = Quaternion.Angle(correctedRotation, Quaternion.identity) < 0.0001f
                    ? Quaternion.identity
                    : Quaternion.Normalize(correctedRotation);
            }
        }

        private static void CorrectImportedMeshes(GameObject modelRoot)
        {
            var correctedMeshes = new HashSet<Mesh>();
            foreach (var filter in modelRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                RotateMesh(filter.sharedMesh, HeadingCorrection, correctedMeshes);
            }

            foreach (var renderer in modelRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                RotateMesh(renderer.sharedMesh, HeadingCorrection, correctedMeshes);
            }
        }

        private static void BakeImportedNodeRotations(Transform modelRoot)
        {
            var correctedMeshes = new HashSet<Mesh>();
            foreach (Transform child in modelRoot)
            {
                BakeNodeRotation(child, correctedMeshes);
            }
        }

        private static void BakeNodeRotation(Transform node, ISet<Mesh> correctedMeshes)
        {
            var localRotation = node.localRotation;
            if (Quaternion.Angle(localRotation, Quaternion.identity) >= 0.0001f)
            {
                var meshFilter = node.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    RotateMesh(meshFilter.sharedMesh, localRotation, correctedMeshes);
                }

                var skinnedRenderer = node.GetComponent<SkinnedMeshRenderer>();
                if (skinnedRenderer != null)
                {
                    RotateMesh(skinnedRenderer.sharedMesh, localRotation, correctedMeshes);
                }

                foreach (Transform child in node)
                {
                    child.localPosition = localRotation * child.localPosition;
                    child.localRotation = localRotation * child.localRotation;
                }
                node.localRotation = Quaternion.identity;
            }

            foreach (Transform child in node)
            {
                BakeNodeRotation(child, correctedMeshes);
            }
        }

        private static void RotateMesh(Mesh mesh, Quaternion rotation, ISet<Mesh> correctedMeshes)
        {
            if (mesh == null || !correctedMeshes.Add(mesh))
            {
                return;
            }

            var vertices = mesh.vertices;
            for (var index = 0; index < vertices.Length; index++)
            {
                vertices[index] = rotation * vertices[index];
            }
            mesh.vertices = vertices;

            var normals = mesh.normals;
            for (var index = 0; index < normals.Length; index++)
            {
                normals[index] = rotation * normals[index];
            }
            mesh.normals = normals;

            var tangents = mesh.tangents;
            for (var index = 0; index < tangents.Length; index++)
            {
                var direction = rotation
                                * new Vector3(tangents[index].x, tangents[index].y, tangents[index].z);
                tangents[index] = new Vector4(direction.x, direction.y, direction.z, tangents[index].w);
            }
            mesh.tangents = tangents;
            mesh.RecalculateBounds();
        }
    }
}
