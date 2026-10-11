using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Runtime.Localization
{
    /// 文案制作表；字体编号与文案键保持稳定，语言切换不改变资源身份。
    [CreateAssetMenu(menuName = "SleepyDemos/Settings/Language Catalog")]
    public sealed class LanguageCatalog : ScriptableObject
    {
        /// 支持语言的制作名称和数字格式文化；首版词表制作中英两列。
        [Serializable]
        public sealed class LanguageDefinition
        {
            [SerializeField] private string id;
            [SerializeField] private string displayName;
            [SerializeField] private string culture;

            public string Id => id;
            public string DisplayName => displayName;
            public string Culture => culture;

            /// <summary>配置支持语言的本地显示名及数字格式。</summary>
            /// <param name="id">稳定语言标识，首版为 zh-CN 或 en。</param>
            /// <param name="displayName">用本语言表达的选择项名称。</param>
            /// <param name="culture">CultureInfo 可识别的格式文化名称。</param>
            public LanguageDefinition(string id, string displayName, string culture)
            {
                this.id = id;
                this.displayName = displayName;
                this.culture = culture;
            }
        }

        [SerializeField] private LanguageDefinition[] languages =
        {
            new("zh-CN", "简体中文", "zh-CN"), new("en", "English", "en-US")
        };
        /// 一条公共文案，FontId 为零时使用公共默认字体。
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string key;
            [SerializeField] private int fontId;
            [SerializeField, TextArea] private string chinese;
            [SerializeField, TextArea] private string english;

            public string Key => key;
            public int FontId => fontId;
            public string Chinese => chinese;
            public string English => english;

            /// <summary>建立正式文案配置，字体编号默认零。</summary>
            /// <param name="key">稳定文案键。</param>
            /// <param name="fontId">稳定字体编号，非负。</param>
            /// <param name="chinese">简体中文完整文案。</param>
            /// <param name="english">英文完整文案。</param>
            public Entry(string key, int fontId, string chinese, string english)
            {
                this.key = key;
                this.fontId = fontId;
                this.chinese = chinese;
                this.english = english;
            }
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => Array.AsReadOnly(entries);
        public IReadOnlyList<LanguageDefinition> Languages => Array.AsReadOnly(languages);

        /// <summary>由制作工具显式配置词条；拒绝重复键和不完整翻译。</summary>
        /// <param name="values">正式词条，不保存调用方数组引用。</param>
        public void Configure(Entry[] values)
        {
            Validate(values);
            entries = (Entry[])values.Clone();
        }

        internal void Validate()
        {
            Validate(entries);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in languages)
            {
                if (item == null || !LanguageService.IsSupported(item.Id)
                    || string.IsNullOrWhiteSpace(item.DisplayName) || !ids.Add(item.Id))
                {
                    throw new ArgumentException("语言定义无效或重复。");
                }
                System.Globalization.CultureInfo.GetCultureInfo(item.Culture);
            }
            if (!ids.Contains("zh-CN") || !ids.Contains("en"))
            {
                throw new ArgumentException("首版语言配置必须包含中英定义。");
            }
        }

        private static void Validate(Entry[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in values)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.FontId < 0
                    || entry.Chinese == null || entry.English == null || !keys.Add(entry.Key))
                {
                    throw new ArgumentException("文案表包含无效或重复词条。");
                }
            }
        }
    }
}
