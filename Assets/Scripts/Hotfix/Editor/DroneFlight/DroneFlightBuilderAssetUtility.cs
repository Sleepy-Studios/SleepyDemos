using System;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.DroneFlight
{
    /// <summary>DroneFlight 编辑期 Builder 共享的资源、层级和序列化辅助。</summary>
    internal static class DroneFlightBuilderAssetUtility
    {
        internal static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        internal static Transform EnsureChild(Transform parent, string name, Vector3 localPosition)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent, false);
            }
            child.localPosition = localPosition;
            child.localRotation = Quaternion.identity;
            child.localScale = Vector3.one;
            return child;
        }

        internal static Transform FindDeepChild(Transform root, string name)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }

        internal static void DestroyChild(Transform root, string path)
        {
            var child = root.Find(path);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }

        internal static void RemoveAll<T>(GameObject root) where T : Component
        {
            foreach (var component in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(component);
            }
        }

        internal static T EnsureComponent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }

        internal static void SetReference(Object target, string propertyName, Object value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name} 缺少序列化字段 {propertyName}。");
            }
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void SetFloat(Object target, string propertyName, float value)
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException($"{target.GetType().Name} 缺少序列化字段 {propertyName}。");
            }
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[index]);
                }
                current = next;
            }
        }
    }
}
