using System;
using System.Collections.Generic;
using System.Linq;

namespace Core.Editor.AssetNaming
{
    /// <summary>
    /// 资源命名规则的单一数据源（SSOT）。
    /// 目录矩阵、扩展名表、语义正则、Label 映射、demo_id 规则集中在这里定义，
    /// Validator / Postprocessor / TMP 字体工具与文档速查表都以本文件为准。
    /// 改动命名规范时只改这里，再同步 docs/architecture/asset-naming.md。
    /// </summary>
    public static class LoadResourcesNamingSpec
    {
        public const string LoadResourcesRoot = "Assets/LoadResources";

        /// <summary>Demo 目录段在目录矩阵中的占位符，匹配任意合法 demo_id。</summary>
        public const string DemoIdToken = "{demo_id}";

        /// <summary>demo_id（Demos 下一级目录名）格式：小写字母开头 + 小写字母/数字/下划线。</summary>
        public const string DemoIdPattern = "^[a-z][a-z0-9_]*$";

        /// <summary>
        /// 语义文件名格式（不含扩展名）：PascalCase 主体，分段用 `_`，
        /// 每段要么是 PascalCase 单词，要么是两位数字变体（如 _01）。
        /// 例：MainMenuView、Rock_01_BaseColor、HarmonyOS_CN_Atlas。
        /// </summary>
        public const string SemanticPattern = "^[A-Z][A-Za-z0-9]*(_([A-Z][A-Za-z0-9]*|[0-9]{2}))*$";

        /// <summary>合法文件名字符集（Error 级）：只允许字母、数字、下划线。</summary>
        public const string LegalNamePattern = "^[A-Za-z0-9_]+$";

        public static readonly string[] SkippedTopFolders =
        {
            "Codes"
        };

        public static readonly string[] SkippedExtensions =
        {
            ".meta",
            ".cs",
            ".md",
            ".dll",
            ".asmdef",
            ".spriteatlasv2"
        };

        public static readonly string[] ExemptFileNames =
        {
            ".gitkeep"
        };

        private static readonly string[] ImageExtensions =
        {
            ".png", ".jpg", ".jpeg", ".tga", ".psd", ".gif", ".bmp", ".tif", ".tiff", ".webp", ".exr", ".hdr"
        };

        private static readonly string[] AudioExtensions =
        {
            ".wav", ".ogg", ".mp3", ".aiff", ".aif", ".flac"
        };

        private static readonly string[] ModelExtensions =
        {
            ".fbx", ".obj", ".blend", ".dae", ".3ds", ".dxf", ".skp"
        };

        private static readonly string[] ShaderExtensions =
        {
            ".shader", ".shadergraph", ".shadersubgraph", ".cginc", ".hlsl"
        };

        private static readonly string[] AnimationExtensions =
        {
            ".anim", ".controller", ".overridecontroller"
        };

        /// <summary>
        /// 目录矩阵：类型由「目录 + 扩展名 + Importer」决定，文件名只承载语义。
        /// 资产必须落在某条规则对应目录（或其子目录）下；否则视为目录未登记。
        /// </summary>
        public static readonly IReadOnlyList<FolderRule> FolderRules = new List<FolderRule>
        {
            // UI
            new FolderRule("UI/Views", new[] { ".prefab" })
                { RequireViewSuffix = true, Labels = new[] { "ui", "view" }, Description = "UI 视图预制体（MvcBind 专用）" },
            new FolderRule("UI/Widgets", new[] { ".prefab" })
                { Labels = new[] { "ui", "widget" }, Description = "可复用 UI 控件预制体" },
            new FolderRule("UI/Atlas", Concat(new[] { ".spriteatlas" }, ImageExtensions))
                { TextureKind = TextureKind.Sprite, Labels = new[] { "ui", "atlas" }, Description = "UI 图集与切图（Sprite）" },

            // Art
            new FolderRule("Art/Textures", ImageExtensions)
                { TextureKind = TextureKind.Default, Labels = new[] { "art", "texture" }, Description = "材质贴图（Default）" },
            new FolderRule("Art/Materials", new[] { ".mat" })
                { Labels = new[] { "art", "material" }, Description = "材质" },
            new FolderRule("Art/Models", ModelExtensions)
                { Labels = new[] { "art", "model" }, Description = "模型" },
            new FolderRule("Art/Animations", AnimationExtensions)
                { Labels = new[] { "art", "anim" }, Description = "动画 Clip 与 Animator Controller" },
            new FolderRule("Art/Shaders", ShaderExtensions)
                { Labels = new[] { "art", "shader" }, Description = "Shader / ShaderGraph" },

            // Audio
            new FolderRule("Audio/SFX", AudioExtensions)
                { Labels = new[] { "audio", "sfx" }, Description = "音效" },
            new FolderRule("Audio/BGM", AudioExtensions)
                { Labels = new[] { "audio", "bgm" }, Description = "背景音乐" },

            // VFX
            new FolderRule("VFX", Concat(new[] { ".prefab", ".vfx", ".mat" }, Concat(ImageExtensions, ShaderExtensions)))
                { TextureKind = TextureKind.Default, Labels = new[] { "vfx" }, Description = "特效预制体及其私有材质/贴图/Shader" },

            // Scenes
            new FolderRule("Scenes", new[] { ".unity" })
                { Labels = new[] { "scene" }, Description = "可加载场景" },

            // Config
            new FolderRule("Config", new[] { ".asset", ".json", ".bytes" })
                { Labels = new[] { "config" }, Description = "ScriptableObject / JSON / 二进制配置" },

            // Fonts
            new FolderRule("Fonts/Source", new[] { ".ttf", ".otf" })
                { Labels = new[] { "font" }, Description = "源字体" },
            new FolderRule("Fonts/TMP_FontAssets", new[] { ".asset", ".png" })
                { TextureKind = TextureKind.Default, Labels = new[] { "font" }, Description = "TMP Font Asset 与外部图集" },
            new FolderRule("Fonts/Materials", new[] { ".mat" })
                { Labels = new[] { "font" }, Description = "字体材质" },

            // Demos（{demo_id} 占位符匹配任意合法 demo_id）
            new FolderRule("Demos/{demo_id}/Scenes", new[] { ".unity" })
                { Labels = new[] { "demo", "scene" }, Description = "Demo 可加载场景" },
            new FolderRule("Demos/{demo_id}/Prefabs", new[] { ".prefab" })
                { Labels = new[] { "demo", "prefab" }, Description = "Demo 玩法预制体" },
            new FolderRule("Demos/{demo_id}/Art",
                Concat(new[] { ".mat" }, Concat(ImageExtensions, Concat(ModelExtensions, Concat(AnimationExtensions, ShaderExtensions)))))
                { Labels = new[] { "demo", "art" }, Description = "Demo 私有美术" },
            new FolderRule("Demos/{demo_id}/Data", new[] { ".json", ".bytes", ".txt", ".asset" })
                { Labels = new[] { "demo", "data" }, Description = "Demo 数据表 / 配置" },
            new FolderRule("Demos/{demo_id}/VFX",
                Concat(new[] { ".prefab", ".vfx", ".mat" }, Concat(ImageExtensions, ShaderExtensions)))
                { TextureKind = TextureKind.Default, Labels = new[] { "demo", "vfx" }, Description = "Demo 私有特效" },
        };

        /// <summary>目录矩阵中登记过的顶层目录白名单（含被跳过的顶层目录）。</summary>
        public static readonly IReadOnlyCollection<string> RegisteredTopFolders =
            FolderRules.Select(rule => rule.Segments[0])
                .Concat(SkippedTopFolders)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        /// <summary>由命名系统托管的 Unity Asset Label；移动资产时会移除旧托管标签并写入新托管标签。</summary>
        public static readonly IReadOnlyCollection<string> ManagedLabels =
            FolderRules.SelectMany(rule => rule.Labels)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        private static string[] Concat(string[] left, string[] right)
        {
            return left.Concat(right).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    public enum NamingSeverity
    {
        Error,
        Warning,
        Info
    }

    public enum TextureKind
    {
        Any,
        Sprite,
        Default
    }

    public readonly struct NamingIssue
    {
        public readonly string AssetPath;
        public readonly NamingSeverity Severity;
        public readonly string Message;

        public NamingIssue(string assetPath, NamingSeverity severity, string message)
        {
            AssetPath = assetPath;
            Severity = severity;
            Message = message;
        }

        public override string ToString()
        {
            return $"{AssetPath}: {Message}";
        }
    }

    public sealed class FolderRule
    {
        public FolderRule(string relativeFolder, string[] extensions)
        {
            Segments = relativeFolder.Split('/');
            Extensions = extensions;
        }

        /// <summary>相对 LoadResources 的目录分段，可含 {demo_id} 占位符。</summary>
        public string[] Segments { get; }

        /// <summary>允许的扩展名（含点，小写）。</summary>
        public string[] Extensions { get; }

        /// <summary>是否要求语义名以 View 结尾（UI 视图）。</summary>
        public bool RequireViewSuffix { get; set; }

        /// <summary>图片类资产期望的 TextureImporter 类型。</summary>
        public TextureKind TextureKind { get; set; } = TextureKind.Any;

        /// <summary>导入时自动打的 Label（替代旧的文件名前缀搜索）。</summary>
        public string[] Labels { get; set; } = Array.Empty<string>();

        public string Description { get; set; } = string.Empty;
    }
}
