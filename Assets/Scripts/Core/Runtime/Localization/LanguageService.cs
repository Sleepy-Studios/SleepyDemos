using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace Core.Runtime.Localization
{
    /// 一次性文本与字体解析；只有 Hub 设置流程选择语言，不订阅或扫描 UI。
    public static class LanguageService
    {
        private static Dictionary<string, LanguageCatalog.Entry> texts;
        private static Dictionary<int, Dictionary<string, FontMappingCatalog.Entry>> fonts;
        private static IReadOnlyList<LanguageCatalog.LanguageDefinition> languages;

        public static string CurrentLanguage { get; private set; }
        public static bool IsInitialized => texts != null;
        /// 支持语言的名称和格式文化，供 Hub 选择；不让 View 读取制作 SO。
        public static IReadOnlyList<LanguageCatalog.LanguageDefinition> Languages => languages;

        /// <summary>使用已加载的正式配置初始化；校验失败时保留旧服务状态。</summary>
        /// <param name="catalog">正式中英文文案表。</param>
        /// <param name="fontCatalog">每个字体编号的中英文映射，必须包含编号零。</param>
        /// <param name="language">首次语言 zh-CN 或 en。</param>
        public static void Initialize(LanguageCatalog catalog, FontMappingCatalog fontCatalog, string language)
        {
            if (catalog == null || fontCatalog == null)
            {
                throw new ArgumentNullException(catalog == null ? nameof(catalog) : nameof(fontCatalog));
            }

            ValidateLanguage(language);
            catalog.Validate();
            fontCatalog.Validate();
            var nextTexts = new Dictionary<string, LanguageCatalog.Entry>(StringComparer.Ordinal);
            var nextFonts = new Dictionary<int, Dictionary<string, FontMappingCatalog.Entry>>();
            foreach (var entry in fontCatalog.Entries)
            {
                if (!nextFonts.TryGetValue(entry.FontId, out var variants))
                {
                    variants = new Dictionary<string, FontMappingCatalog.Entry>(StringComparer.Ordinal);
                    nextFonts.Add(entry.FontId, variants);
                }
                variants.Add(entry.Language, entry);
            }

            RequireFont(nextFonts, 0);
            foreach (int id in nextFonts.Keys)
            {
                RequireFont(nextFonts, id);
            }
            foreach (var entry in catalog.Entries)
            {
                RequireFont(nextFonts, entry.FontId);
                nextTexts.Add(entry.Key, entry);
            }

            texts = nextTexts;
            fonts = nextFonts;
            languages = catalog.Languages;
            CurrentLanguage = language;
        }

        /// <summary>拒绝当前未制作的语言，不改变已选择状态。</summary>
        /// <param name="language">当前支持 zh-CN 和 en。</param>
        public static void ValidateLanguage(string language)
        {
            if (!IsSupported(language))
            {
                throw new ArgumentException("当前仅支持 zh-CN 和 en。", nameof(language));
            }
        }

        /// <summary>Hub 重建 UI 前选择已加载语言，不通知已存在的 Demo 页面。</summary>
        /// <param name="language">已制作的语言标识。</param>
        public static void SelectLanguage(string language)
        {
            EnsureInitialized();
            ValidateLanguage(language);
            CurrentLanguage = language;
        }

        /// <summary>读取完整文案，缺失键返回键本身供制作排查。</summary>
        /// <param name="key">稳定文案键。</param>
        /// <returns>当前语言文案。</returns>
        public static string Get(string key)
        {
            EnsureInitialized();
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("文案键不能为空。", nameof(key));
            }
            return texts.TryGetValue(key, out var entry) ? GetValue(entry) : key;
        }

        /// <summary>按当前语言文化格式化完整文案。</summary>
        /// <param name="key">稳定文案键。</param>
        /// <param name="args">完整句式中的占位参数。</param>
        /// <returns>格式化后的当前语言文案。</returns>
        public static string Format(string key, params object[] args)
        {
            string text = Get(key);
            foreach (var item in languages)
            {
                if (item.Id == CurrentLanguage)
                {
                    return string.Format(CultureInfo.GetCultureInfo(item.Culture), text, args ?? Array.Empty<object>());
                }
            }
            throw new InvalidOperationException("当前语言缺少格式文化。");
        }

        /// <summary>一次性应用文案、字体及对应材质，不改变字号、颜色或对齐。</summary>
        /// <param name="target">当前页面已有 TMP 文本。</param>
        /// <param name="key">文案表中的稳定键。</param>
        /// <param name="fontIdOverride">为空使用词条 FontId；缺失词条使用零。</param>
        /// <param name="args">完整句式中的参数；为空不做格式化。</param>
        public static void SetText(TMP_Text target, string key, int? fontIdOverride = null, params object[] args)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }
            string value = args != null && args.Length > 0 ? Format(key, args) : Get(key);
            int id = fontIdOverride ?? (texts.TryGetValue(key, out var entry) ? entry.FontId : 0);
            if (!fonts.TryGetValue(id, out var variants) || !variants.TryGetValue(CurrentLanguage, out var mapping))
            {
                throw new ArgumentException("字体编号没有当前语言映射。", nameof(fontIdOverride));
            }
            target.font = mapping.Font;
            target.fontSharedMaterial = mapping.Material;
            target.text = value;
        }

        internal static bool IsSupported(string language) => language == "zh-CN" || language == "en";

        private static string GetValue(LanguageCatalog.Entry entry)
        {
            return CurrentLanguage == "en" ? entry.English : entry.Chinese;
        }

        private static void RequireFont(Dictionary<int, Dictionary<string, FontMappingCatalog.Entry>> values, int id)
        {
            if (!values.TryGetValue(id, out var variants) || !variants.ContainsKey("zh-CN") || !variants.ContainsKey("en"))
            {
                throw new ArgumentException("每个使用中的字体编号必须配置中英文映射：" + id);
            }
        }

        private static void EnsureInitialized()
        {
            if (!IsInitialized)
            {
                throw new InvalidOperationException("语言服务尚未使用正式配置初始化。");
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            texts = null;
            fonts = null;
            CurrentLanguage = null;
            languages = null;
        }
    }
}
