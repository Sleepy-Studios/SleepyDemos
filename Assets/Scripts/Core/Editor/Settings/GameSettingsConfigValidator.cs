using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Core.Editor.TextMeshPro;
using Core.Runtime.Localization;
using Core.Runtime.Presentation;
using Core.Runtime.Rendering;
using Core.Runtime.Settings;
using Core.Runtime.TimeControls;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Editor.Settings
{
    /// 正式公共配置的只读检查入口，不初始化运行时服务或生成字体。
    public static class GameSettingsConfigValidator
    {
        private const string ConfigPath = "Assets/" + GameSettingsService.ConfigAddress + ".asset";
        private const string CnCharactersPath = "Assets/LoadResources/Fonts/Fallbacks/Default_CN_Characters.txt";
        private const string EnCharactersPath = "Assets/LoadResources/Fonts/Fallbacks/Default_EN_Characters.txt";

        /// 读取正式配置及相关 SO，返回全部错误与缺字信息，不修改原资产。
        public static IReadOnlyList<string> ValidateAll()
        {
            var issues = new List<string>();
            var config = AssetDatabase.LoadAssetAtPath<GameSettingsConfig>(ConfigPath);
            if (config == null)
            {
                issues.Add("未找到正式公共配置：" + ConfigPath);
                return issues;
            }

            Check(config, config.Validate, issues);
            var validated = new HashSet<Object> { config };
            if (config.Language != null && config.Fonts != null)
            {
                ValidateCatalogs(config.Language, config.Fonts, issues);
            }

            var levels = new HashSet<GraphicsQualityLevel>();
            foreach (var preset in config.QualityPresets ?? Array.Empty<GraphicsQualityPreset>())
            {
                if (preset == null)
                {
                    issues.Add("公共配置存在空画质预设引用。");
                }
                else if (!levels.Add(preset.Level))
                {
                    issues.Add("公共配置存在重复画质档位：" + preset.Level);
                }

                if (preset != null && validated.Add(preset))
                {
                    Check(preset, preset.Validate, issues);
                }
            }

            foreach (var level in new[] { GraphicsQualityLevel.Low, GraphicsQualityLevel.Medium, GraphicsQualityLevel.High })
            {
                if (!levels.Contains(level))
                {
                    issues.Add("公共配置缺少画质档位：" + level);
                }
            }

            var folders = new[] { "Assets/LoadResources/Config/Settings", "Assets/LoadResources/Config/Presentation" }
                .Where(AssetDatabase.IsValidFolder).ToArray();
            if (folders.Length > 0)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", folders))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (asset == null || !validated.Add(asset))
                    {
                        continue;
                    }

                    switch (asset)
                    {
                        case GraphicsQualityPreset quality:
                            Check(quality, quality.Validate, issues);
                            break;
                        case PostProcessPreset post:
                            Check(post, post.Validate, issues);
                            break;
                        case CinematicPreset cinematic:
                            Check(cinematic, cinematic.Validate, issues);
                            break;
                        case GameTimePreset time:
                            Check(time, time.Validate, issues);
                            break;
                        case GameSettingsConfig other when other != config:
                            Check(other, other.Validate, issues);
                            break;
                    }
                }
            }

            return issues;
        }

        [MenuItem("Tools/SleepyDemos/Settings/Validate Global Settings Config")]
        public static void ValidateFromMenu()
        {
            var issues = ValidateAll();
            foreach (string issue in issues)
            {
                Debug.LogWarning("[GameSettingsConfig] " + issue);
            }

            if (issues.Count == 0)
            {
                Debug.Log("[GameSettingsConfig] 正式 SO、词表、字体映射、中英参数与字形覆盖检查通过（包含现有 fallback，不动态补字）。");
            }
            else
            {
                Debug.LogWarning($"[GameSettingsConfig] 共发现 {issues.Count} 项问题；未修改配置、字体或字符集。");
            }
        }

        private static void Check(Object asset, Action validate, List<string> issues)
        {
            try
            {
                validate();
            }
            catch (Exception exception)
            {
                issues.Add(AssetDatabase.GetAssetPath(asset) + "：" + exception.Message);
            }
        }

        private static void ValidateCatalogs(LanguageCatalog language, FontMappingCatalog fonts, List<string> issues)
        {
            IReadOnlyList<LanguageCatalog.Entry> words;
            IReadOnlyList<FontMappingCatalog.Entry> fontEntries;
            try
            {
                words = language.Entries;
                fontEntries = fonts.Entries;
            }
            catch (Exception exception)
            {
                issues.Add("词表或字体映射无法读取：" + exception.Message);
                return;
            }

            // public Configure 与内部 Validate 共用领域规则；仅操作立即销毁的配置副本。
            var languageCopy = Object.Instantiate(language);
            var fontsCopy = Object.Instantiate(fonts);
            languageCopy.hideFlags = HideFlags.HideAndDontSave;
            fontsCopy.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Check(language, () => languageCopy.Configure(words.ToArray()), issues);
                Check(fonts, () => fontsCopy.Configure(fontEntries.ToArray()), issues);
            }
            finally
            {
                Object.DestroyImmediate(languageCopy);
                Object.DestroyImmediate(fontsCopy);
            }

            var mappings = new Dictionary<string, FontMappingCatalog.Entry>(StringComparer.Ordinal);
            var fontIds = new HashSet<int> { 0 };
            foreach (var entry in fontEntries)
            {
                if (entry == null)
                {
                    continue;
                }

                string key = entry.FontId + ":" + entry.Language;
                fontIds.Add(entry.FontId);
                if (!mappings.TryAdd(key, entry))
                {
                    issues.Add("重复字体映射：" + key);
                }

                if (entry.Font != null && entry.Material != null && entry.Material.mainTexture != entry.Font.atlasTexture)
                {
                    issues.Add($"字体映射 {key} 的材质图集不匹配：{AssetDatabase.GetAssetPath(entry.Material)}");
                }
            }

            var wordKeys = new HashSet<string>(StringComparer.Ordinal);
            var glyphTexts = new Dictionary<TMP_FontAsset, StringBuilder>();
            foreach (var entry in words)
            {
                if (entry == null)
                {
                    continue;
                }

                fontIds.Add(entry.FontId);
                if (!wordKeys.Add(entry.Key ?? string.Empty))
                {
                    issues.Add("重复文案键：" + entry.Key);
                }

                try
                {
                    var cn = GetFormatIndices(entry.Chinese);
                    var en = GetFormatIndices(entry.English);
                    if (!cn.SetEquals(en))
                    {
                        issues.Add($"文案 {entry.Key} 中英占位参数不匹配：中文 [{string.Join(",", cn)}]，英文 [{string.Join(",", en)}]");
                    }
                }
                catch (FormatException exception)
                {
                    issues.Add("文案 " + entry.Key + " 的格式无效：" + exception.Message);
                }

                AppendGlyphText(entry.FontId, "zh-CN", entry.Chinese, mappings, glyphTexts);
                AppendGlyphText(entry.FontId, "en", entry.English, mappings, glyphTexts);
            }

            foreach (int id in fontIds.OrderBy(value => value))
            {
                foreach (string locale in new[] { "zh-CN", "en" })
                {
                    if (!mappings.ContainsKey(id + ":" + locale))
                    {
                        issues.Add($"字体编号 {id} 缺少 {locale} 映射，或文案引用了未定义编号。");
                    }
                }
            }

            var missingCharacters = new SortedSet<int>();
            foreach (var pair in glyphTexts)
            {
                string characters = TMPFontBuilderWindow.NormalizeCharacters(pair.Value.ToString());
                if (!pair.Key.HasCharacters(characters, out uint[] missing, searchFallbacks: true, tryAddCharacter: false))
                {
                    if (missing == null)
                    {
                        issues.Add("无法读取字体字符表：" + AssetDatabase.GetAssetPath(pair.Key));
                        continue;
                    }

                    foreach (uint character in missing)
                    {
                        missingCharacters.Add((int)character);
                    }

                    issues.Add("字体及现有 fallback 缺字：" + AssetDatabase.GetAssetPath(pair.Key)
                        + "；" + string.Concat(missing.Distinct().OrderBy(value => value).Select(value => char.ConvertFromUtf32((int)value))));
                }
            }

            if (missingCharacters.Count > 0)
            {
                string characters = string.Concat(missingCharacters.Select(char.ConvertFromUtf32));
                issues.Add("缺字合集：" + characters + "\n按语言补入公共字符集并更新原字体，不新增 Demo 字体：\n"
                    + CnCharactersPath + "\n" + EnCharactersPath);
            }
        }

        private static void AppendGlyphText(int fontId, string locale, string text,
            Dictionary<string, FontMappingCatalog.Entry> mappings, Dictionary<TMP_FontAsset, StringBuilder> glyphTexts)
        {
            if (!mappings.TryGetValue(fontId + ":" + locale, out var mapping) || mapping.Font == null)
            {
                return;
            }

            if (!glyphTexts.TryGetValue(mapping.Font, out var builder))
            {
                builder = new StringBuilder();
                glyphTexts.Add(mapping.Font, builder);
            }

            builder.Append(text);
        }

        internal static SortedSet<int> GetFormatIndices(string text)
        {
            if (text == null)
            {
                throw new FormatException("中英文案不能为空。");
            }

            var indices = new SortedSet<int>();
            for (int cursor = 0; cursor < text.Length; cursor++)
            {
                char current = text[cursor];
                if (current != '{' && current != '}')
                {
                    continue;
                }

                if (cursor + 1 < text.Length && text[cursor + 1] == current)
                {
                    cursor++;
                    continue;
                }

                if (current == '}')
                {
                    throw new FormatException("存在未转义的右花括号。");
                }

                int start = ++cursor;
                while (cursor < text.Length && text[cursor] >= '0' && text[cursor] <= '9')
                {
                    cursor++;
                }

                if (!int.TryParse(text.Substring(start, cursor - start), NumberStyles.None, CultureInfo.InvariantCulture, out int index))
                {
                    throw new FormatException("参数必须使用非负整数索引。");
                }

                indices.Add(index);
                SkipSpaces(text, ref cursor);
                if (cursor < text.Length && text[cursor] == ',')
                {
                    cursor++;
                    SkipSpaces(text, ref cursor);
                    if (cursor < text.Length && text[cursor] == '-')
                    {
                        cursor++;
                    }

                    int alignmentStart = cursor;
                    while (cursor < text.Length && text[cursor] >= '0' && text[cursor] <= '9')
                    {
                        cursor++;
                    }

                    if (cursor == alignmentStart)
                    {
                        throw new FormatException("参数对齐宽度无效。");
                    }

                    SkipSpaces(text, ref cursor);
                }

                if (cursor < text.Length && text[cursor] == ':')
                {
                    cursor++;
                    while (cursor < text.Length && text[cursor] != '}')
                    {
                        if (text[cursor] == '{')
                        {
                            if (cursor + 1 >= text.Length || text[cursor + 1] != '{')
                            {
                                throw new FormatException("格式说明中存在未转义的左花括号。");
                            }

                            cursor++;
                        }

                        cursor++;
                    }
                }

                if (cursor >= text.Length || text[cursor] != '}')
                {
                    throw new FormatException("参数缺少右花括号或格式无效。");
                }
            }

            return indices;
        }

        private static void SkipSpaces(string text, ref int cursor)
        {
            while (cursor < text.Length && text[cursor] == ' ')
            {
                cursor++;
            }
        }
    }
}
