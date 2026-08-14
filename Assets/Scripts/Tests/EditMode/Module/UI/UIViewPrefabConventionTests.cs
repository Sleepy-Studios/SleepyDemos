using System.Collections.Generic;
using Core.Editor.MvcBind;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Module
{
    public sealed class UIViewPrefabConventionTests
    {
        [Test]
        public void PublicUIViewPrefabs_RootDoesNotContainCanvasComponents()
        {
            var violations = new List<string>();
            var prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { MvcBindPathUtility.DefaultUiPrefabRoot });

            foreach (var guid in prefabGuids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefabRoot == null)
                {
                    continue;
                }

                var forbiddenComponents = new List<string>();
                AddIfPresent<Canvas>(prefabRoot, forbiddenComponents);
                AddIfPresent<CanvasScaler>(prefabRoot, forbiddenComponents);
                AddIfPresent<GraphicRaycaster>(prefabRoot, forbiddenComponents);
                if (forbiddenComponents.Count > 0)
                {
                    violations.Add($"{path}: {string.Join(", ", forbiddenComponents)}");
                }
            }

            Assert.That(
                violations,
                Is.Empty,
                "业务 View 根节点禁止 Canvas 三件套；局部 Sub-Canvas 只能位于后代节点。\n" +
                string.Join("\n", violations));
        }

        private static void AddIfPresent<T>(GameObject prefabRoot, ICollection<string> components)
            where T : Component
        {
            if (prefabRoot.GetComponent<T>() != null)
            {
                components.Add(typeof(T).Name);
            }
        }
    }
}
