using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Core.Editor.AssetNaming
{
    public static class LoadResourcesAssetNamingRules
    {
        public const string LoadResourcesRoot = "Assets/LoadResources";
        public const char PrefixSeparator = '&';

        private static readonly string[] SkippedTopFolders =
        {
            "Codes"
        };

        private static readonly string[] SkippedExtensions =
        {
            ".meta",
            ".cs",
            ".md",
            ".dll",
            ".asmdef",
            ".spriteatlasv2"
        };

        private static readonly string[] ExemptFileNames =
        {
            ".gitkeep"
        };

        public static readonly IReadOnlyList<string> RegisteredPrefixKeys = new[]
        {
            "font",
            "time",
            "json",
            "anim",
            "spr",
            "tex",
            "mat",
            "pfb",
            "mdl",
            "anc",
            "scn",
            "shd",
            "phy",
            "vid",
            "bgm",
            "sfx",
            "so",
            "txt"
        };

        private static readonly HashSet<string> DemosAllowedPrefixKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "spr", "tex", "mat", "pfb", "mdl", "anim", "anc", "scn", "so", "font",
            "sfx", "bgm", "shd", "phy", "vid", "time", "json", "txt"
        };

        private static readonly Dictionary<string, HashSet<string>> FolderPrefixRules =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "UI", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pfb" } },
                { "Art", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "spr", "tex", "mat", "mdl", "anim", "anc", "shd" } },
                { "Audio", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "sfx", "bgm" } },
                { "VFX", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "pfb", "tex", "mat", "shd" } },
                { "Scenes", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "scn" } },
                { "Config", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "so", "json", "txt" } },
                { "Fonts", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "font" } }
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

            if (!TryMatchPrefix(nameWithoutExtension, out var prefixKey, out var remainder))
            {
                error = $"未识别的类型前缀。允许的前缀见 docs/architecture/asset-naming.md（如 tex&、pfb&）。文件名: {fileName}";
                return false;
            }

            if (string.IsNullOrEmpty(remainder))
            {
                error = "前缀 '&' 后须有语义命名。";
                return false;
            }

            if (!IsPrefixAllowedForPath(assetPath, prefixKey))
            {
                error = $"前缀 '{prefixKey}{PrefixSeparator}' 不允许出现在当前目录: {assetPath}";
                return false;
            }

            if (!TryValidateExtensionBinding(assetPath, prefixKey, out error))
            {
                return false;
            }

            if (prefixKey.Equals("pfb", StringComparison.Ordinal) &&
                assetPath.IndexOf("/UI/", StringComparison.OrdinalIgnoreCase) >= 0 &&
                assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) &&
                !nameWithoutExtension.EndsWith("View", StringComparison.Ordinal))
            {
                error = "UI Prefab 文件名须以 View 结尾，例如 pfb&MainMenuView.prefab。";
                return false;
            }

            return true;
        }

        public static string GetSemanticName(string fileNameWithoutExtension)
        {
            if (string.IsNullOrEmpty(fileNameWithoutExtension))
            {
                return fileNameWithoutExtension;
            }

            var separatorIndex = fileNameWithoutExtension.IndexOf(PrefixSeparator);
            if (separatorIndex < 0 || separatorIndex >= fileNameWithoutExtension.Length - 1)
            {
                return fileNameWithoutExtension;
            }

            return fileNameWithoutExtension.Substring(separatorIndex + 1);
        }

        private static bool TryValidateExtensionBinding(string assetPath, string prefixKey, out string error)
        {
            error = null;
            var extension = Path.GetExtension(assetPath);
            if (string.IsNullOrEmpty(extension))
            {
                error = "缺少文件扩展名。";
                return false;
            }

            extension = extension.ToLowerInvariant();

            switch (prefixKey)
            {
                case "pfb":
                    return RequireExtension(extension, out error, ".prefab");
                case "mdl":
                    return RequireExtension(extension, out error, ".fbx", ".obj", ".blend", ".dae", ".3ds", ".dxf", ".skp");
                case "anim":
                    return RequireExtension(extension, out error, ".anim");
                case "anc":
                    return RequireExtension(extension, out error, ".controller", ".overridecontroller");
                case "scn":
                    return RequireExtension(extension, out error, ".unity");
                case "mat":
                    return RequireExtension(extension, out error, ".mat");
                case "font":
                    return RequireFontExtension(assetPath, extension, out error);
                case "so":
                    return RequireExtension(extension, out error, ".asset");
                case "json":
                    return RequireExtension(extension, out error, ".json");
                case "txt":
                    return RequireExtension(extension, out error, ".txt", ".bytes");
                case "sfx":
                case "bgm":
                    return RequireExtension(extension, out error, ".wav", ".ogg", ".mp3", ".aiff", ".aif", ".flac");
                case "shd":
                    return RequireExtension(extension, out error, ".shader", ".shadergraph", ".shadersubgraph", ".cginc", ".hlsl");
                case "phy":
                    return RequireExtension(extension, out error, ".physicmaterial");
                case "vid":
                    return RequireExtension(extension, out error, ".mp4", ".webm", ".mov", ".avi", ".asf");
                case "time":
                    return RequireExtension(extension, out error, ".playable");
                case "spr":
                case "tex":
                    return RequireExtension(extension, out error,
                        ".png", ".jpg", ".jpeg", ".tga", ".psd", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".exr", ".hdr");
                default:
                    error = $"未配置扩展名校验的前缀: {prefixKey}{PrefixSeparator}";
                    return false;
            }
        }

        private static bool RequireFontExtension(string assetPath, string extension, out string error)
        {
            if (extension.Equals(".ttf", StringComparison.Ordinal) ||
                extension.Equals(".otf", StringComparison.Ordinal))
            {
                error = null;
                return true;
            }

            if (extension.Equals(".asset", StringComparison.Ordinal))
            {
                if (assetPath.IndexOf("/Fonts/TMP_FontAssets/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = null;
                    return true;
                }

                error = "font& 的 .asset 仅用于 Fonts/TMP_FontAssets。";
                return false;
            }

            if (extension.Equals(".png", StringComparison.Ordinal))
            {
                if (assetPath.IndexOf("/Fonts/TMP_FontAssets/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = null;
                    return true;
                }

                error = "font& 的 .png 仅用于 Fonts/TMP_FontAssets 外部图集。";
                return false;
            }

            if (extension.Equals(".mat", StringComparison.Ordinal))
            {
                if (assetPath.IndexOf("/Fonts/Materials/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    error = null;
                    return true;
                }

                error = "font& 的 .mat 仅用于 Fonts/Materials。";
                return false;
            }

            error = "font& 仅用于 .ttf / .otf / TMP .asset / TMP 图集 .png / Fonts/Materials 下 .mat。";
            return false;
        }

        private static bool RequireExtension(string extension, out string error, params string[] allowed)
        {
            if (allowed.Any(item => extension.Equals(item, StringComparison.OrdinalIgnoreCase)))
            {
                error = null;
                return true;
            }

            error = $"扩展名 '{extension}' 与当前前缀不匹配。";
            return false;
        }

        private static bool TryMatchPrefix(string nameWithoutExtension, out string prefixKey, out string remainder)
        {
            prefixKey = null;
            remainder = null;

            var separatorIndex = nameWithoutExtension.IndexOf(PrefixSeparator);
            if (separatorIndex <= 0)
            {
                return false;
            }

            prefixKey = nameWithoutExtension.Substring(0, separatorIndex);
            if (!RegisteredPrefixKeys.Contains(prefixKey))
            {
                prefixKey = null;
                return false;
            }

            remainder = nameWithoutExtension.Substring(separatorIndex + 1);
            return true;
        }

        private static bool IsPrefixAllowedForPath(string assetPath, string prefixKey)
        {
            var relative = assetPath.Substring(LoadResourcesRoot.Length + 1);
            var parts = relative.Split('/');
            var topFolder = parts[0];

            if (topFolder.Equals("Demos", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length >= 3 &&
                    parts[2].Equals("Data", StringComparison.OrdinalIgnoreCase))
                {
                    return prefixKey.Equals("json", StringComparison.Ordinal) ||
                           prefixKey.Equals("txt", StringComparison.Ordinal) ||
                           prefixKey.Equals("so", StringComparison.Ordinal);
                }

                return DemosAllowedPrefixKeys.Contains(prefixKey);
            }

            if (topFolder.Equals("Fonts", StringComparison.OrdinalIgnoreCase))
            {
                return prefixKey.Equals("font", StringComparison.Ordinal);
            }

            if (!FolderPrefixRules.TryGetValue(topFolder, out var allowed))
            {
                return RegisteredPrefixKeys.Contains(prefixKey);
            }

            return allowed.Contains(prefixKey);
        }
    }
}
