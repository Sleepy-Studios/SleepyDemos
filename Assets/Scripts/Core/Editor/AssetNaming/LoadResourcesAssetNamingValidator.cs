using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Editor.MvcBind;
using UnityEditor;

namespace Core.Editor.AssetNaming
{
    public static class LoadResourcesAssetNamingValidator
    {
        /// <summary>遍历 LoadResources 下全部资产，汇总命名问题（含 Error/Warning/Info 与地址冲突）。</summary>
        public static IReadOnlyList<NamingIssue> ValidateAll()
        {
            var issues = new List<NamingIssue>();
            var root = LoadResourcesAssetNamingRules.LoadResourcesRoot;
            if (!AssetDatabase.IsValidFolder(root))
            {
                issues.Add(new NamingIssue(root, NamingSeverity.Error, $"未找到目录: {root}"));
                return issues;
            }

            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            var addressOwners = new Dictionary<string, List<string>>();
            foreach (var file in files)
            {
                var assetPath = file.Replace('\\', '/');
                if (LoadResourcesAssetNamingRules.ShouldSkipAssetPath(assetPath))
                {
                    continue;
                }

                issues.AddRange(LoadResourcesAssetNamingRules.Validate(assetPath));
                AppendLabelIssues(assetPath, issues);

                var address = MvcBindPathUtility.ToRuntimeAddress(assetPath);
                if (!addressOwners.TryGetValue(address, out var owners))
                {
                    owners = new List<string>();
                    addressOwners[address] = owners;
                }

                owners.Add(assetPath);
            }

            foreach (var pair in addressOwners.Where(item => item.Value.Count > 1))
            {
                issues.Add(new NamingIssue(
                    pair.Value[0],
                    NamingSeverity.Error,
                    $"运行时地址冲突 '{pair.Key}'，由多个资产生成：{string.Join(", ", pair.Value)}。"));
            }

            return issues;
        }

        [MenuItem("Tools/SleepyDemos/校验 LoadResources 资源命名")]
        public static void ValidateFromMenu()
        {
            var issues = ValidateAll();
            var errors = issues.Where(item => item.Severity == NamingSeverity.Error).ToList();
            var warnings = issues.Where(item => item.Severity == NamingSeverity.Warning).ToList();
            var infos = issues.Where(item => item.Severity == NamingSeverity.Info).ToList();

            foreach (var issue in errors)
            {
                UnityEngine.Debug.LogError($"[AssetNaming] {issue}");
            }

            foreach (var issue in warnings)
            {
                UnityEngine.Debug.LogWarning($"[AssetNaming] {issue}");
            }

            foreach (var issue in infos)
            {
                UnityEngine.Debug.Log($"[AssetNaming] {issue}");
            }

            if (errors.Count == 0)
            {
                UnityEngine.Debug.Log(
                    $"[AssetNaming] LoadResources 命名校验通过（Warning {warnings.Count} 项，Info {infos.Count} 项）。");
                return;
            }

            UnityEngine.Debug.LogError(
                $"[AssetNaming] 共 {errors.Count} 项 Error、{warnings.Count} 项 Warning。规则见 docs/architecture/asset-naming.md");
        }

        private static void AppendLabelIssues(string assetPath, List<NamingIssue> issues)
        {
            var expected = LoadResourcesAssetNamingRules.GetLabels(assetPath);
            if (expected.Count == 0)
            {
                return;
            }

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                return;
            }

            var current = AssetDatabase.GetLabels(asset);
            var missing = expected
                .Where(label => !current.Contains(label, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            var stale = current
                .Where(label => LoadResourcesNamingSpec.ManagedLabels.Contains(label, StringComparer.OrdinalIgnoreCase) &&
                    !expected.Contains(label, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            if (missing.Length == 0 && stale.Length == 0)
            {
                return;
            }

            issues.Add(new NamingIssue(
                assetPath,
                NamingSeverity.Warning,
                $"Unity Asset Label 与目录矩阵不一致，缺失：{FormatLabels(missing)}；过期托管标签：{FormatLabels(stale)}。可运行 Tools/SleepyDemos/同步 LoadResources 资产 Label 自动修正。"));
        }

        private static string FormatLabels(string[] labels)
        {
            return labels.Length == 0 ? "无" : string.Join(" ", labels);
        }
    }
}
