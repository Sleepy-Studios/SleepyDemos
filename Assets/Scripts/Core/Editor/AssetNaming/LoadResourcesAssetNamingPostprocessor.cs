using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.AssetNaming
{
    public sealed class LoadResourcesAssetNamingPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            foreach (var assetPath in importedAssets)
            {
                Process(assetPath);
            }

            foreach (var assetPath in movedAssets)
            {
                Process(assetPath);
            }
        }

        private static void Process(string assetPath)
        {
            if (LoadResourcesAssetNamingRules.ShouldSkipAssetPath(assetPath))
            {
                return;
            }

            foreach (var issue in LoadResourcesAssetNamingRules.Validate(assetPath))
            {
                switch (issue.Severity)
                {
                    case NamingSeverity.Error:
                        Debug.LogError($"[AssetNaming] {issue}");
                        break;
                    case NamingSeverity.Warning:
                        Debug.LogWarning($"[AssetNaming] {issue}");
                        break;
                    default:
                        Debug.Log($"[AssetNaming] {issue}");
                        break;
                }
            }

            ApplyLabels(assetPath);
        }

        /// <summary>
        /// 扫描 `Assets/LoadResources` 下已有资产，并按目录矩阵同步 Unity Asset Label。
        /// </summary>
        [MenuItem("Tools/SleepyDemos/同步 LoadResources 资产 Label")]
        public static void SyncLabelsFromMenu()
        {
            var changed = SyncAllLabels();
            Debug.Log($"[AssetNaming] LoadResources 资产 Label 同步完成，更新 {changed} 个资产。");
        }

        /// <summary>
        /// 批量同步 `Assets/LoadResources` 下已有资产的 Unity Asset Label。
        /// </summary>
        /// <returns>实际写入 Label 的资产数量。</returns>
        public static int SyncAllLabels()
        {
            var root = LoadResourcesAssetNamingRules.LoadResourcesRoot;
            if (!AssetDatabase.IsValidFolder(root))
            {
                Debug.LogError($"[AssetNaming] 未找到目录: {root}");
                return 0;
            }

            var changed = 0;
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var assetPath = file.Replace('\\', '/');
                if (ApplyLabels(assetPath))
                {
                    changed++;
                }
            }

            AssetDatabase.SaveAssets();
            return changed;
        }

        /// <summary>
        /// 按目录矩阵给资产同步托管 Label：移除旧托管标签，写入新托管标签，并保留人工标签。
        /// </summary>
        /// <param name="assetPath">Unity 资产路径，例如 `Assets/LoadResources/UI/Views/MainMenuView.prefab`。</param>
        /// <returns>如果本次写入了 Label，返回 true；否则返回 false。</returns>
        public static bool ApplyLabels(string assetPath)
        {
            var expected = LoadResourcesAssetNamingRules.GetLabels(assetPath);
            if (expected.Count == 0)
            {
                return false;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                return false;
            }

            var current = AssetDatabase.GetLabels(asset);
            var desired = BuildDesiredLabels(current, expected);
            if (LabelsEqual(current, desired))
            {
                return false;
            }

            AssetDatabase.SetLabels(asset, desired);
            return true;
        }

        private static string[] BuildDesiredLabels(IEnumerable<string> current, IEnumerable<string> expected)
        {
            var managed = new HashSet<string>(LoadResourcesNamingSpec.ManagedLabels, StringComparer.OrdinalIgnoreCase);
            return current
                .Where(label => !managed.Contains(label))
                .Concat(expected)
                .Where(label => !string.IsNullOrWhiteSpace(label))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToArray();
        }

        private static bool LabelsEqual(string[] current, string[] desired)
        {
            return current.Length == desired.Length &&
                desired.All(label => current.Contains(label));
        }
    }
}
