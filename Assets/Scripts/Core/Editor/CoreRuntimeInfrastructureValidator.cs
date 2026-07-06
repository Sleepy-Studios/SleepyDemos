using System;
using System.Collections.Generic;
using System.IO;
using Core.Runtime;
using UnityEditor;
using UnityEngine;

namespace Core.Editor
{
    public static class CoreRuntimeInfrastructureValidator
    {
        private static readonly string[] ForbiddenOuterRuntimeTerms =
        {
            "YooAssetResourceSystem",
            "YooAssetResourceService",
            "YooAssetResourceLoader",
            "ResourcePackage",
            "AssetHandle",
            "YooAssetPlayMode",
            "Addressables",
            "Game.Main",
            "UnityEngine.UI.LoopScrollRect",
            "MultiTypeViewList",
            "SuperScrollView",
            "GTask",
            "GAsync",
            "GalaDebugger",
            "ListPool"
        };

        private static readonly string[] OuterRuntimeScanRoots =
        {
            "Assets/Scripts/Core/Runtime/UI",
            "Assets/Scripts/Core/Runtime/Components",
            "Assets/Scripts/Core/Runtime/Startup",
            "Assets/Scripts/Core/Runtime/HotUpdate",
            "Assets/Scripts/Hotfix"
        };

        [MenuItem("Tools/SleepyDemos/Validate Core Runtime Infrastructure")]
        public static void Validate()
        {
            ValidateInternal(exitEditor: false);
        }

        public static void ValidateForBatchMode()
        {
            ValidateInternal(exitEditor: true);
        }

        private static void ValidateInternal(bool exitEditor)
        {
            var errors = new List<string>();
            var warnings = new List<string>();

            ValidateResourceService(errors);
            ValidateUIComponents(errors);
            ValidateForbiddenReferences(errors);
            ValidateDocs(errors);

            if (errors.Count > 0)
            {
                Debug.LogError(BuildReport("Core Runtime Infrastructure 校验失败", errors, warnings));
                if (exitEditor)
                {
                    EditorApplication.Exit(1);
                }

                return;
            }

            Debug.Log(BuildReport("Core Runtime Infrastructure 校验通过", errors, warnings));
            if (exitEditor)
            {
                EditorApplication.Exit(0);
            }
        }

        private static void ValidateResourceService(List<string> errors)
        {
            var service = ResourceServices.Default;
            if (service == null)
            {
                errors.Add("ResourceServices.Default 为空。");
                return;
            }

            if (service.CreateLoader() is not IResourceLoader loader)
            {
                errors.Add("ResourceServices.Default.CreateLoader() 未返回 IResourceLoader。");
                return;
            }

            loader.Dispose();

            var normalized = service.NormalizeAddress("LoadResources/UI\\Views\\TestView");
            if (normalized != "LoadResources/UI/Views/TestView")
            {
                errors.Add($"资源地址标准化失败：期望 LoadResources/UI/Views/TestView，实际 {normalized}。");
            }
        }

        private static void ValidateUIComponents(List<string> errors)
        {
            RequireComponentType<UITab>(errors);
            RequireComponentType<ViewList>(errors);
            RequireComponentType<UIBtnSwitch>(errors);
            RequireComponentType<UIDropdown>(errors);
            RequireComponentType<UIState>(errors);
            RequireComponentType<ViewTab>(errors);
            RequireComponentType<AccordionTab>(errors);
            RequireComponentType<AccordionViewTab>(errors);
            RequireComponentType<UIImageLoader>(errors);
            RequireComponentType<LoopVerticalScrollRect>(errors);
            RequireComponentType<LoopHorizontalScrollRect>(errors);
            RequireComponentType<LoopGridView>(errors);
            RequireComponentType<LoopStaggeredGridView>(errors);
        }

        private static void RequireComponentType<T>(List<string> errors)
        {
            if (typeof(T) == null)
            {
                errors.Add($"缺少组件类型：{typeof(T).Name}");
            }
        }

        private static void ValidateForbiddenReferences(List<string> errors)
        {
            foreach (var root in OuterRuntimeScanRoots)
            {
                var fullRoot = Path.GetFullPath(root);
                if (!Directory.Exists(fullRoot))
                {
                    continue;
                }

                foreach (var file in Directory.GetFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
                {
                    if (NormalizePath(file).Contains("/Components/LoopScroll/", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var text = File.ReadAllText(file);
                    foreach (var term in ForbiddenOuterRuntimeTerms)
                    {
                        if (text.Contains(term, StringComparison.Ordinal))
                        {
                            errors.Add($"{NormalizePath(file)} 不应直接引用 {term}。");
                        }
                    }
                }
            }
        }

        private static void ValidateDocs(List<string> errors)
        {
            RequireFile("docs/modules/resource-runtime.md", errors);
            RequireFile("docs/modules/ui-runtime.md", errors);
            RequireFile("docs/runbooks/use-core-ui-components.md", errors);
            RequireFile("docs/architecture/startup-flow.md", errors);
            RequireFile("docs/architecture/hotfix-boundary.md", errors);
        }

        private static void RequireFile(string path, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add($"缺少文档：{path}");
            }
        }

        private static string BuildReport(string title, List<string> errors, List<string> warnings)
        {
            var lines = new List<string> { $"[SleepyDemos] {title}" };
            if (errors.Count > 0)
            {
                lines.Add("Errors:");
                lines.AddRange(errors);
            }

            if (warnings.Count > 0)
            {
                lines.Add("Warnings:");
                lines.AddRange(warnings);
            }

            if (errors.Count == 0 && warnings.Count == 0)
            {
                lines.Add("资源抽象、基础 UI 组件、外层引用约束和文档入口均通过。");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static string NormalizePath(string file)
        {
            return file.Replace('\\', '/');
        }
    }
}
