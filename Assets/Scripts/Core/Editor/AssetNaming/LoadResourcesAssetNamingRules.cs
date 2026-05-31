using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Core.Editor.AssetNaming
{
    public static class LoadResourcesAssetNamingRules
    {
        public const string LoadResourcesRoot = "Assets/LoadResources";

        private static readonly string[] SkippedTopFolders =
        {
            "Codes",
            "Fallbacks"
        };

        private static readonly string[] SkippedExtensions =
        {
            ".meta",
            ".cs",
            ".md",
            ".json",
            ".txt",
            ".bytes",
            ".dll",
            ".asmdef",
            ".spriteatlasv2"
        };

        private static readonly string[] ExemptFileNames =
        {
            ".gitkeep"
        };

        private static readonly Regex VariantSegmentRegex = new Regex(@"^\d{2}$", RegexOptions.Compiled);
        private static readonly Regex PascalSegmentRegex = new Regex(@"^[A-Z][a-zA-Z0-9]*$", RegexOptions.Compiled);

        public static readonly IReadOnlyList<string> RegisteredPrefixes = new[]
        {
            "fonttmp_",
            "mati_",
            "tex_",
            "mat_",
            "pfb_",
            "ui_",
            "scn_",
            "mdl_",
            "anim_",
            "anc_",
            "vfx_",
            "sfx_",
            "bgm_",
            "font_",
            "atl_",
            "sk_",
            "so_"
        };

        private static readonly Dictionary<string, HashSet<string>> FolderPrefixRules =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "UI", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ui_" } },
                { "Art", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "tex_", "mat_", "mati_", "mdl_", "sk_", "anim_", "anc_", "atl_" } },
                { "Audio", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sfx_", "bgm_" } },
                { "VFX", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "vfx_", "tex_", "mat_", "mati_" } },
                { "Scenes", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scn_" } },
                { "Config", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "so_" } },
                { "Fonts", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "font_", "fonttmp_" } }
            };

        private static readonly HashSet<string> DemosAllowedPrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pfb_", "scn_", "tex_", "mat_", "mati_", "mdl_", "sk_", "anim_", "anc_", "vfx_", "sfx_", "bgm_", "so_", "atl_"
        };

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

            if (SkippedExtensions.Any(item => extension.Equals(item, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var relative = assetPath.Substring(LoadResourcesRoot.Length + 1);
            var topFolder = relative.Split('/')[0];
            if (SkippedTopFolders.Any(item => topFolder.Equals(item, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (relative.StartsWith("Fonts/Fallbacks/", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var fileName = Path.GetFileName(assetPath);
            if (ExemptFileNames.Any(item => fileName.Equals(item, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return false;
        }

        public static bool TryValidate(string assetPath, out string error)
        {
            error = null;

            if (ShouldSkipAssetPath(assetPath))
            {
                return true;
            }

            if (assetPath.IndexOf('-', StringComparison.Ordinal) >= 0)
            {
                error = "路径或文件名不得包含短横线 '-'。";
                return false;
            }

            var fileName = Path.GetFileName(assetPath);
            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            if (string.IsNullOrEmpty(nameWithoutExtension))
            {
                error = "文件名为空。";
                return false;
            }

            if (!TryMatchPrefix(nameWithoutExtension, out var prefix, out var remainder))
            {
                error = $"未识别的类型前缀。允许的前缀见 docs/architecture/asset-naming.md（如 tex_、pfb_、ui_）。文件名: {fileName}";
                return false;
            }

            if (!IsPrefixAllowedForPath(assetPath, prefix))
            {
                error = $"前缀 '{prefix}' 不允许出现在当前目录: {assetPath}";
                return false;
            }

            if (prefix.Equals("ui_", StringComparison.Ordinal))
            {
                if (!nameWithoutExtension.EndsWith("View", StringComparison.Ordinal))
                {
                    error = "UI Prefab 文件名须以 View 结尾，例如 ui_MainMenuView.prefab。";
                    return false;
                }
            }

            if (prefix.Equals("anc_", StringComparison.Ordinal) &&
                !assetPath.EndsWith(".controller", StringComparison.OrdinalIgnoreCase))
            {
                error = "anc_ 前缀仅用于 .controller 文件。";
                return false;
            }

            if (prefix.Equals("anim_", StringComparison.Ordinal) &&
                !assetPath.EndsWith(".anim", StringComparison.OrdinalIgnoreCase))
            {
                error = "anim_ 前缀仅用于 .anim 文件。";
                return false;
            }

            if (string.IsNullOrEmpty(remainder))
            {
                error = "前缀后至少需要一个 PascalCase 语义段。";
                return false;
            }

            var segments = remainder.Split('_');
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                if (string.IsNullOrEmpty(segment))
                {
                    error = "存在空的下划线分段。";
                    return false;
                }

                if (VariantSegmentRegex.IsMatch(segment))
                {
                    continue;
                }

                if (!PascalSegmentRegex.IsMatch(segment))
                {
                    error = $"语义段 '{segment}' 须为 PascalCase（首字母大写，仅字母数字）。";
                    return false;
                }
            }

            return true;
        }

        private static bool TryMatchPrefix(string nameWithoutExtension, out string prefix, out string remainder)
        {
            prefix = null;
            remainder = null;

            foreach (var candidate in RegisteredPrefixes.OrderByDescending(item => item.Length))
            {
                if (!nameWithoutExtension.StartsWith(candidate, StringComparison.Ordinal))
                {
                    continue;
                }

                prefix = candidate;
                remainder = nameWithoutExtension.Substring(candidate.Length);
                return true;
            }

            return false;
        }

        private static bool IsPrefixAllowedForPath(string assetPath, string prefix)
        {
            var relative = assetPath.Substring(LoadResourcesRoot.Length + 1);
            var parts = relative.Split('/');
            var topFolder = parts[0];

            if (topFolder.Equals("Demos", StringComparison.OrdinalIgnoreCase))
            {
                return DemosAllowedPrefixes.Contains(prefix);
            }

            if (topFolder.Equals("Fonts", StringComparison.OrdinalIgnoreCase))
            {
                if (relative.StartsWith("Fonts/Source/", StringComparison.OrdinalIgnoreCase))
                {
                    return prefix.Equals("font_", StringComparison.Ordinal);
                }

                if (relative.StartsWith("Fonts/TMP_FontAssets/", StringComparison.OrdinalIgnoreCase) ||
                    relative.StartsWith("Fonts/Materials/", StringComparison.OrdinalIgnoreCase))
                {
                    return prefix.Equals("fonttmp_", StringComparison.Ordinal);
                }

                return FolderPrefixRules["Fonts"].Contains(prefix);
            }

            if (!FolderPrefixRules.TryGetValue(topFolder, out var allowed))
            {
                return RegisteredPrefixes.Contains(prefix);
            }

            return allowed.Contains(prefix);
        }
    }
}
