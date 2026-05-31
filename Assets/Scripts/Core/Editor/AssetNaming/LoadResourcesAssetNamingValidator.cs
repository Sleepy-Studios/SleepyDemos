using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Core.Editor.AssetNaming
{
    public static class LoadResourcesAssetNamingValidator
    {
        public static IReadOnlyList<string> ValidateAll()
        {
            var errors = new List<string>();
            if (!AssetDatabase.IsValidFolder(LoadResourcesAssetNamingRules.LoadResourcesRoot))
            {
                errors.Add($"未找到目录: {LoadResourcesAssetNamingRules.LoadResourcesRoot}");
                return errors;
            }

            var root = LoadResourcesAssetNamingRules.LoadResourcesRoot;
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var assetPath = file.Replace('\\', '/');
                if (LoadResourcesAssetNamingRules.ShouldSkipAssetPath(assetPath))
                {
                    continue;
                }

                if (!LoadResourcesAssetNamingRules.TryValidate(assetPath, out var error))
                {
                    errors.Add($"{assetPath}: {error}");
                }
            }

            return errors;
        }

        [MenuItem("Tools/SleepyDemos/校验 LoadResources 资源命名")]
        public static void ValidateFromMenu()
        {
            var errors = ValidateAll();
            if (errors.Count == 0)
            {
                UnityEngine.Debug.Log("[AssetNaming] LoadResources 命名校验通过。");
                return;
            }

            foreach (var error in errors)
            {
                UnityEngine.Debug.LogError($"[AssetNaming] {error}");
            }

            UnityEngine.Debug.LogError($"[AssetNaming] 共 {errors.Count} 项错误。规则见 docs/architecture/asset-naming.md");
        }
    }
}
