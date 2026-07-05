using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Core.Editor.AssetNaming
{
    /// <summary>
    /// 目录驱动的命名校验：类型由「目录 + 扩展名 + Importer」决定，文件名只承载语义。
    /// 规则数据全部来自 <see cref="LoadResourcesNamingSpec"/>（SSOT）。
    /// </summary>
    public static class LoadResourcesAssetNamingRules
    {
        public const string LoadResourcesRoot = LoadResourcesNamingSpec.LoadResourcesRoot;

        private static readonly Regex DemoIdRegex =
            new Regex(LoadResourcesNamingSpec.DemoIdPattern, RegexOptions.Compiled);

        private static readonly Regex SemanticRegex =
            new Regex(LoadResourcesNamingSpec.SemanticPattern, RegexOptions.Compiled);

        private static readonly Regex LegalNameRegex =
            new Regex(LoadResourcesNamingSpec.LegalNamePattern, RegexOptions.Compiled);

        public static bool ShouldSkipAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith(LoadResourcesRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (assetPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var extension = Path.GetExtension(assetPath);
            if (string.IsNullOrEmpty(extension))
            {
                return true;
            }

            if (LoadResourcesNamingSpec.SkippedExtensions.Any(item => extension.Equals(item, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var relative = assetPath.Substring(LoadResourcesRoot.Length + 1);
            var topFolder = relative.Split('/')[0];
            if (LoadResourcesNamingSpec.SkippedTopFolders.Any(item => topFolder.Equals(item, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (relative.StartsWith("Fonts/Fallbacks/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var fileName = Path.GetFileName(assetPath);
            if (LoadResourcesNamingSpec.ExemptFileNames.Any(item => fileName.Equals(item, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 校验单个资产，返回所有命中的问题（含 Error / Warning / Info）。
        /// 空列表表示完全通过。
        /// </summary>
        public static IReadOnlyList<NamingIssue> Validate(string assetPath)
        {
            var issues = new List<NamingIssue>();
            if (ShouldSkipAssetPath(assetPath))
            {
                return issues;
            }

            var normalized = assetPath.Replace('\\', '/');
            var relative = normalized.Substring(LoadResourcesRoot.Length + 1);
            var segments = relative.Split('/');
            var fileName = segments[segments.Length - 1];
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var extension = (Path.GetExtension(fileName) ?? string.Empty).ToLowerInvariant();
            var dirSegments = segments.Take(segments.Length - 1).ToArray();

            // 基础合法性
            if (normalized.IndexOf('-') >= 0)
            {
                issues.Add(Error(assetPath, "路径或文件名不得包含短横线 '-'。"));
            }

            if (normalized.IndexOf(' ') >= 0)
            {
                issues.Add(Error(assetPath, "路径或文件名不得包含空格。"));
            }

            if (nameWithoutExtension.IndexOf('&') >= 0)
            {
                issues.Add(Error(assetPath, "已废除 '&' 类型前缀，文件名只保留纯语义命名（类型由目录与扩展名决定）。"));
            }

            if (string.IsNullOrEmpty(nameWithoutExtension))
            {
                issues.Add(Error(assetPath, "文件名为空。"));
                return issues;
            }

            if (!LegalNameRegex.IsMatch(nameWithoutExtension))
            {
                issues.Add(Error(assetPath, "文件名只允许字母、数字、下划线，且不能以特殊字符开头。"));
            }
            else if (char.IsDigit(nameWithoutExtension[0]))
            {
                issues.Add(Error(assetPath, "文件名不能以数字开头。"));
            }
            else if (!SemanticRegex.IsMatch(nameWithoutExtension))
            {
                issues.Add(Warning(assetPath, "建议使用 PascalCase 语义命名（分段用 _，变体用两位数字，如 Rock_01_BaseColor）。"));
            }

            // Demos：demo_id 校验
            if (dirSegments.Length > 0 && dirSegments[0].Equals("Demos", StringComparison.OrdinalIgnoreCase))
            {
                if (dirSegments.Length < 2)
                {
                    issues.Add(Error(assetPath, "Demos/ 根目录不得直接放资产，须落在 Demos/<demo_id>/ 下。"));
                }
                else if (!DemoIdRegex.IsMatch(dirSegments[1]))
                {
                    issues.Add(Error(assetPath, $"demo_id '{dirSegments[1]}' 非法，须匹配 {LoadResourcesNamingSpec.DemoIdPattern}（小写字母开头）。"));
                }
            }

            // 顶层目录白名单
            var topFolder = dirSegments.Length > 0 ? dirSegments[0] : string.Empty;
            if (!LoadResourcesNamingSpec.RegisteredTopFolders.Any(item => item.Equals(topFolder, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Error(assetPath, $"未登记的顶层目录 '{topFolder}'。新增顶层目录须同步 LoadResourcesNamingSpec 与文档。"));
                return issues;
            }

            // 目录矩阵匹配
            var rule = MatchRule(dirSegments);
            if (rule == null)
            {
                issues.Add(Error(assetPath, "资产所在目录未登记，须放入目录矩阵规定的子目录（见 docs/architecture/asset-naming.md）。"));
                return issues;
            }

            // 扩展名 ↔ 目录绑定
            if (!rule.Extensions.Any(item => item.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(Error(assetPath,
                    $"扩展名 '{extension}' 不允许出现在 {string.Join("/", rule.Segments)}（允许：{string.Join(" ", rule.Extensions)}）。"));
            }

            // Importer 类型（图片 Sprite / Default）
            AppendTextureImporterIssue(assetPath, extension, rule, issues);

            return issues;
        }

        /// <summary>返回该资产按目录矩阵应自动打的 Label；无匹配返回空数组。</summary>
        public static IReadOnlyList<string> GetLabels(string assetPath)
        {
            if (ShouldSkipAssetPath(assetPath))
            {
                return Array.Empty<string>();
            }

            var normalized = assetPath.Replace('\\', '/');
            var relative = normalized.Substring(LoadResourcesRoot.Length + 1);
            var segments = relative.Split('/');
            var dirSegments = segments.Take(segments.Length - 1).ToArray();
            var rule = MatchRule(dirSegments);
            return rule?.Labels ?? Array.Empty<string>();
        }

        private static FolderRule MatchRule(string[] dirSegments)
        {
            FolderRule best = null;
            foreach (var rule in LoadResourcesNamingSpec.FolderRules)
            {
                if (!IsPrefixMatch(rule.Segments, dirSegments))
                {
                    continue;
                }

                if (best == null ||
                    rule.Segments.Length > best.Segments.Length ||
                    (rule.Segments.Length == best.Segments.Length && GetSpecificity(rule) > GetSpecificity(best)))
                {
                    best = rule;
                }
            }

            return best;
        }

        private static int GetSpecificity(FolderRule rule)
        {
            return rule.Segments.Count(segment =>
                segment != LoadResourcesNamingSpec.DemoIdToken &&
                segment != LoadResourcesNamingSpec.UiModuleToken);
        }

        private static bool IsPrefixMatch(string[] ruleSegments, string[] dirSegments)
        {
            if (ruleSegments.Length > dirSegments.Length)
            {
                return false;
            }

            for (var i = 0; i < ruleSegments.Length; i++)
            {
                if (ruleSegments[i] == LoadResourcesNamingSpec.DemoIdToken)
                {
                    if (!DemoIdRegex.IsMatch(dirSegments[i]))
                    {
                        return false;
                    }

                    continue;
                }

                if (ruleSegments[i] == LoadResourcesNamingSpec.UiModuleToken)
                {
                    if (string.IsNullOrWhiteSpace(dirSegments[i]))
                    {
                        return false;
                    }

                    continue;
                }

                if (!ruleSegments[i].Equals(dirSegments[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static void AppendTextureImporterIssue(string assetPath, string extension, FolderRule rule, List<NamingIssue> issues)
        {
            if (rule.TextureKind == TextureKind.Any)
            {
                return;
            }

            var imageExtensions = new[]
            {
                ".png", ".jpg", ".jpeg", ".tga", ".psd", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".exr", ".hdr"
            };
            if (!imageExtensions.Contains(extension))
            {
                return;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            var isSprite = importer.textureType == TextureImporterType.Sprite;
            switch (rule.TextureKind)
            {
                case TextureKind.Sprite when !isSprite:
                    issues.Add(Error(assetPath, $"{string.Join("/", rule.Segments)} 下的图片须设为 Sprite (2D and UI)。"));
                    break;
                case TextureKind.Default when isSprite:
                    issues.Add(Error(assetPath, $"{string.Join("/", rule.Segments)} 下的图片须设为 Default（非 Sprite）。"));
                    break;
            }
        }

        private static NamingIssue Error(string path, string message)
        {
            return new NamingIssue(path, NamingSeverity.Error, message);
        }

        private static NamingIssue Warning(string path, string message)
        {
            return new NamingIssue(path, NamingSeverity.Warning, message);
        }
    }
}
