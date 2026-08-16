using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Core.Runtime;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.MvcBind
{
    internal static class MvcBindIndexDiscovery
    {
        internal static List<MvcBindViewRecord> BuildViewRecords(
            string scriptRoot = MvcBindToolConfig.ScriptRoot,
            string prefabRoot = MvcBindToolConfig.LoadResourcesRoot)
        {
            return Discover(scriptRoot, prefabRoot).Records;
        }

        internal static MvcBindIndexDiscoveryResult Discover(
            string scriptRoot = MvcBindToolConfig.ScriptRoot,
            string prefabRoot = MvcBindToolConfig.LoadResourcesRoot)
        {
            var scripts = BuildScriptIndex(scriptRoot);
            var records = new List<MvcBindViewRecord>();
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { prefabRoot });

            foreach (var prefabPath in prefabGuids
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(MvcBindPathUtility.IsPrefabAssetPath)
                         .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var itemIndex = prefab != null ? prefab.GetComponent<ComponentItemIndex>() : null;
                if (itemIndex == null)
                {
                    continue;
                }

                records.Add(BuildRecord(prefabPath, itemIndex, scripts));
            }

            return new MvcBindIndexDiscoveryResult(records, prefabGuids.Length, 1);
        }

        internal static MvcBindViewRecord FindRecordForPrefab(string prefabPath)
        {
            var normalized = MvcBindPathUtility.NormalizeAssetPath(prefabPath);
            return BuildViewRecords().FirstOrDefault(record =>
                string.Equals(record.prefabPath, normalized, StringComparison.OrdinalIgnoreCase));
        }

        private static MvcBindViewRecord BuildRecord(
            string prefabPath,
            ComponentItemIndex itemIndex,
            ScriptIndex scripts)
        {
            var normalizedPrefabPath = MvcBindPathUtility.NormalizeAssetPath(prefabPath);
            var viewName = MvcBindPathUtility.ToViewClassName(normalizedPrefabPath);
            var record = new MvcBindViewRecord
            {
                viewName = viewName,
                address = MvcBindPathUtility.ToRuntimeAddress(normalizedPrefabPath),
                prefabPath = normalizedPrefabPath,
                hasPrefab = true
            };

            if (scripts.ViewScripts.TryGetValue(viewName, out var viewScript))
            {
                record.viewScriptPath = viewScript.Path;
                record.hasViewScript = true;
            }

            if (scripts.ComponentScripts.TryGetValue(viewName, out var componentScript))
            {
                record.componentScriptPath = componentScript.Path;
                record.hasComponentScript = true;
            }

            var errors = new List<string>();
            var moduleFromView = viewScript?.ModuleName ?? string.Empty;
            var moduleFromComponent = componentScript?.ModuleName ?? string.Empty;
            if (!string.IsNullOrEmpty(moduleFromView) &&
                !string.IsNullOrEmpty(moduleFromComponent) &&
                !string.Equals(moduleFromView, moduleFromComponent, StringComparison.Ordinal))
            {
                errors.Add($"Module 不一致：View={moduleFromView}，Component={moduleFromComponent}");
            }

            var generatedAddress = componentScript?.Address ?? viewScript?.Address ?? string.Empty;
            if (!string.IsNullOrEmpty(generatedAddress) &&
                !string.Equals(generatedAddress, record.address, StringComparison.Ordinal))
            {
                errors.Add($"Source 地址与 Prefab 不一致：{generatedAddress}");
            }

            record.moduleName = !string.IsNullOrEmpty(moduleFromView)
                ? moduleFromView
                : moduleFromComponent;
            if (string.IsNullOrEmpty(record.moduleName))
            {
                errors.Add("缺少可用的 [Module] 信息");
            }

            if (!record.hasViewScript)
            {
                errors.Add("缺少手写 View 脚本");
            }

            if (!record.hasComponentScript)
            {
                errors.Add("缺少生成的 ViewComponent 脚本");
            }

            ApplyModuleOutputLocation(record, componentScript?.Path ?? viewScript?.Path);
            ValidateBindingArrays(itemIndex, errors);
            record.isValid = errors.Count == 0;
            record.validationMessage = string.Join("；", errors);
            if (!record.isValid)
            {
                record.moduleName = MvcBindViewRecord.InvalidModuleName;
            }

            return record;
        }

        private static void ApplyModuleOutputLocation(MvcBindViewRecord record, string scriptPath)
        {
            if (string.IsNullOrEmpty(scriptPath) || string.IsNullOrEmpty(record.moduleName))
            {
                return;
            }

            var normalized = MvcBindPathUtility.NormalizeAssetPath(scriptPath);
            var suffix = $"/{record.viewName}/View/{Path.GetFileName(normalized)}";
            if (!normalized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var moduleOutputRoot = normalized.Substring(0, normalized.Length - suffix.Length);
            var defaultRoot = $"{MvcBindToolConfig.ModuleRoot}/{record.moduleName}";
            if (string.Equals(moduleOutputRoot, defaultRoot, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            record.usesCustomModuleOutputDirectory = true;
            record.moduleOutputDirectory = moduleOutputRoot;
        }

        private static void ValidateBindingArrays(ComponentItemIndex itemIndex, ICollection<string> errors)
        {
            var components = itemIndex.Components;
            var componentTypes = itemIndex.ComponentTypes;
            var bindingKeys = itemIndex.BindingKeys;
            var bindingMethods = itemIndex.BindingMethods;
            if (components == null || componentTypes == null || bindingKeys == null || bindingMethods == null)
            {
                errors.Add("ComponentItemIndex 绑定数组为空");
                return;
            }

            if (components.Length != componentTypes.Length ||
                components.Length != bindingKeys.Length ||
                components.Length != bindingMethods.Length)
            {
                errors.Add("ComponentItemIndex 绑定数组长度不一致");
            }

            if (components.Any(component => component == null))
            {
                errors.Add("ComponentItemIndex 含有丢失的组件引用");
            }
        }

        private static ScriptIndex BuildScriptIndex(string scriptRoot)
        {
            var result = new ScriptIndex();
            if (!Directory.Exists(scriptRoot))
            {
                return result;
            }

            foreach (var path in Directory.GetFiles(scriptRoot, "*.cs", SearchOption.AllDirectories))
            {
                var normalized = MvcBindPathUtility.NormalizeAssetPath(path);
                var fileName = Path.GetFileNameWithoutExtension(normalized);
                var isComponent = fileName.EndsWith("ViewComponent", StringComparison.Ordinal);
                var isView = !isComponent && fileName.EndsWith("View", StringComparison.Ordinal);
                if (!isComponent && !isView)
                {
                    continue;
                }

                var viewName = isComponent
                    ? fileName.Substring(0, fileName.Length - "Component".Length)
                    : fileName;
                var metadata = ReadMetadata(normalized);
                var target = isComponent ? result.ComponentScripts : result.ViewScripts;
                if (!target.ContainsKey(viewName))
                {
                    target.Add(viewName, metadata);
                }
            }

            return result;
        }

        private static ScriptMetadata ReadMetadata(string scriptPath)
        {
            var source = File.ReadAllText(scriptPath, Encoding.UTF8);
            return new ScriptMetadata
            {
                Path = scriptPath,
                ModuleName = MatchValue(source, @"\[(?:Module|ModuleAttribute)\(""([^""]*)""\)\]"),
                Address = MatchValue(source, @"\[(?:Source|SourceAttribute)\(""([^""]*)""\)\]")
            };
        }

        private static string MatchValue(string source, string pattern)
        {
            if (string.IsNullOrEmpty(source))
            {
                return string.Empty;
            }

            var match = Regex.Match(source, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private sealed class ScriptIndex
        {
            public readonly Dictionary<string, ScriptMetadata> ViewScripts =
                new Dictionary<string, ScriptMetadata>(StringComparer.Ordinal);
            public readonly Dictionary<string, ScriptMetadata> ComponentScripts =
                new Dictionary<string, ScriptMetadata>(StringComparer.Ordinal);
        }

        private sealed class ScriptMetadata
        {
            public string Path;
            public string ModuleName;
            public string Address;
        }
    }

    internal sealed class MvcBindIndexDiscoveryResult
    {
        internal MvcBindIndexDiscoveryResult(
            List<MvcBindViewRecord> records,
            int prefabCandidateCount,
            int scriptScanPasses)
        {
            Records = records;
            PrefabCandidateCount = prefabCandidateCount;
            ScriptScanPasses = scriptScanPasses;
        }

        internal List<MvcBindViewRecord> Records { get; }
        internal int PrefabCandidateCount { get; }
        internal int ScriptScanPasses { get; }
    }
}
