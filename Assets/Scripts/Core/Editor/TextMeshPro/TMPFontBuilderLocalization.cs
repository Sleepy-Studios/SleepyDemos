using System.Collections.Generic;

namespace Core.Editor.TextMeshPro
{
    internal enum TMPFontBuilderEditorLanguage
    {
        Chinese,
        English
    }

    internal enum TMPFontBuilderText
    {
        WindowTitle,
        Presets,
        Preset,
        PresetName,
        AddPresetTooltip,
        RemovePresetTooltip,
        LoadPreset,
        SavePreset,
        Source,
        FontFile,
        OutputDirectory,
        OutputDirectoryTooltip,
        Characters,
        CharacterSetText,
        InlineCharacters,
        Fallback,
        FallbackFont,
        Generation,
        SamplingPointSize,
        AtlasPadding,
        AtlasSize,
        ExportExternalAtlas,
        AstcPlatformSettings,
        BuildFontAsset,
        LoadedPreset,
        SavedPreset,
        AddedPreset,
        RemovedPreset,
        CannotRemoveLastPreset,
        EmptyPresetName,
        DuplicatePresetName,
        EmptyOutputDirectory,
        InvalidOutputDirectory,
        InvalidPreset,
        UnsavedChangesTitle,
        UnsavedChangesMessage,
        Save,
        Discard,
        Cancel,
        DeletePresetTitle,
        DeletePresetMessage,
        Delete,
        FallbackIgnored
    }

    internal static class TMPFontBuilderLocalization
    {
        private static readonly Dictionary<TMPFontBuilderText, string[]> Texts = new Dictionary<TMPFontBuilderText, string[]>
        {
            { TMPFontBuilderText.WindowTitle, new[] { "TMP 字体生成器", "TMP Font Builder" } },
            { TMPFontBuilderText.Presets, new[] { "预设", "Presets" } },
            { TMPFontBuilderText.Preset, new[] { "当前预设", "Preset" } },
            { TMPFontBuilderText.PresetName, new[] { "预设名称", "Preset Name" } },
            { TMPFontBuilderText.AddPresetTooltip, new[] { "复制当前参数并新增预设", "Duplicate current settings as a new preset" } },
            { TMPFontBuilderText.RemovePresetTooltip, new[] { "删除当前预设", "Remove the selected preset" } },
            { TMPFontBuilderText.LoadPreset, new[] { "加载预设", "Load Preset" } },
            { TMPFontBuilderText.SavePreset, new[] { "保存预设", "Save Preset" } },
            { TMPFontBuilderText.Source, new[] { "源字体", "Source" } },
            { TMPFontBuilderText.FontFile, new[] { "字体文件", "Font File" } },
            { TMPFontBuilderText.OutputDirectory, new[] { "输出目录", "Output Directory" } },
            { TMPFontBuilderText.OutputDirectoryTooltip, new[] { "当前预设生成到 TMP_FontAssets 下的子目录，例如 Japanese 或 Arabic", "Subfolder under TMP_FontAssets for this preset, such as Japanese or Arabic" } },
            { TMPFontBuilderText.Characters, new[] { "字符", "Characters" } },
            { TMPFontBuilderText.CharacterSetText, new[] { "字符集文本", "Character Set Text" } },
            { TMPFontBuilderText.InlineCharacters, new[] { "内联字符", "Inline Characters" } },
            { TMPFontBuilderText.Fallback, new[] { "回退字体", "Fallback" } },
            { TMPFontBuilderText.FallbackFont, new[] { "回退字体资产", "Fallback Font" } },
            { TMPFontBuilderText.Generation, new[] { "生成参数", "Generation" } },
            { TMPFontBuilderText.SamplingPointSize, new[] { "采样字号", "Sampling Point Size" } },
            { TMPFontBuilderText.AtlasPadding, new[] { "图集间距", "Atlas Padding" } },
            { TMPFontBuilderText.AtlasSize, new[] { "图集尺寸", "Atlas Size" } },
            { TMPFontBuilderText.ExportExternalAtlas, new[] { "导出外部图集", "Export External Atlas" } },
            { TMPFontBuilderText.AstcPlatformSettings, new[] { "应用 ASTC 平台设置", "ASTC Platform Settings" } },
            { TMPFontBuilderText.BuildFontAsset, new[] { "生成 TMP 字体资产", "Build TMP Font Asset" } },
            { TMPFontBuilderText.LoadedPreset, new[] { "已加载预设：{0}", "Loaded preset: {0}" } },
            { TMPFontBuilderText.SavedPreset, new[] { "已保存预设：{0}", "Saved preset: {0}" } },
            { TMPFontBuilderText.AddedPreset, new[] { "已新增预设：{0}", "Added preset: {0}" } },
            { TMPFontBuilderText.RemovedPreset, new[] { "已删除预设：{0}", "Removed preset: {0}" } },
            { TMPFontBuilderText.CannotRemoveLastPreset, new[] { "至少需要保留一套预设。", "At least one preset must remain." } },
            { TMPFontBuilderText.EmptyPresetName, new[] { "预设名称不能为空。", "Preset name cannot be empty." } },
            { TMPFontBuilderText.DuplicatePresetName, new[] { "预设名称不能重复。", "Preset name must be unique." } },
            { TMPFontBuilderText.EmptyOutputDirectory, new[] { "输出目录不能为空。", "Output directory cannot be empty." } },
            { TMPFontBuilderText.InvalidOutputDirectory, new[] { "输出目录只能是单个合法文件夹名称。", "Output directory must be one valid folder name." } },
            { TMPFontBuilderText.InvalidPreset, new[] { "当前预设无效，请重新选择。", "The selected preset is invalid. Select it again." } },
            { TMPFontBuilderText.UnsavedChangesTitle, new[] { "未保存的预设修改", "Unsaved Preset Changes" } },
            { TMPFontBuilderText.UnsavedChangesMessage, new[] { "预设“{0}”包含未保存的修改。", "Preset \"{0}\" has unsaved changes." } },
            { TMPFontBuilderText.Save, new[] { "保存", "Save" } },
            { TMPFontBuilderText.Discard, new[] { "放弃", "Discard" } },
            { TMPFontBuilderText.Cancel, new[] { "取消", "Cancel" } },
            { TMPFontBuilderText.DeletePresetTitle, new[] { "删除预设", "Delete Preset" } },
            { TMPFontBuilderText.DeletePresetMessage, new[] { "确定删除预设“{0}”吗？", "Delete preset \"{0}\"?" } },
            { TMPFontBuilderText.Delete, new[] { "删除", "Delete" } },
            { TMPFontBuilderText.FallbackIgnored, new[] { "当前回退字体指向正在生成的同一份字体，已自动忽略。", "The fallback points to the font being generated and was ignored." } }
        };

        internal static string Get(TMPFontBuilderText key, TMPFontBuilderEditorLanguage language)
        {
            return Texts.TryGetValue(key, out var values) ? values[(int)language] : key.ToString();
        }

        internal static string Format(TMPFontBuilderText key, TMPFontBuilderEditorLanguage language, params object[] args)
        {
            return string.Format(Get(key, language), args);
        }

        internal static bool HasTranslation(TMPFontBuilderText key, TMPFontBuilderEditorLanguage language)
        {
            return Texts.TryGetValue(key, out var values)
                && values.Length > (int)language
                && !string.IsNullOrWhiteSpace(values[(int)language]);
        }
    }
}
