using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Core.Runtime.Localization
{
    /// 按字体编号和语言选择制作字体与材质；多个编号允许共用同一资产。
    [CreateAssetMenu(menuName = "SleepyDemos/Settings/Font Mapping Catalog")]
    public sealed class FontMappingCatalog : ScriptableObject
    {
        /// 一种语言下的一份字体及对应材质预设。
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private int fontId;
            [SerializeField] private string language;
            [SerializeField] private TMP_FontAsset font;
            [SerializeField] private Material material;

            public int FontId => fontId;
            public string Language => language;
            public TMP_FontAsset Font => font;
            public Material Material => material;

            /// <summary>配置字体编号的目标语言资产，不按字体名称推断。</summary>
            /// <param name="fontId">稳定非负编号。</param>
            /// <param name="language">zh-CN 或 en。</param>
            /// <param name="font">已加载的正式 TMP 字体。</param>
            /// <param name="material">引用目标字体图集的正式材质预设。</param>
            public Entry(int fontId, string language, TMP_FontAsset font, Material material)
            {
                this.fontId = fontId;
                this.language = language;
                this.font = font;
                this.material = material;
            }
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => Array.AsReadOnly(entries);

        /// <summary>显式配置字体映射，校验图集与材质及重复映射。</summary>
        /// <param name="values">目标映射，不保存调用方数组引用。</param>
        public void Configure(Entry[] values)
        {
            Validate(values);
            entries = (Entry[])values.Clone();
        }

        internal void Validate() => Validate(entries);

        private static void Validate(Entry[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in values)
            {
                if (entry == null || entry.FontId < 0 || !LanguageService.IsSupported(entry.Language)
                    || entry.Font == null || entry.Material == null || entry.Font.atlasTexture == null
                    || entry.Material.mainTexture != entry.Font.atlasTexture
                    || !keys.Add(entry.FontId + ":" + entry.Language))
                {
                    throw new ArgumentException("字体映射无效，材质必须引用目标字体的图集。");
                }
            }
        }
    }
}
