using System;
using System.Collections.Generic;
using Core.Runtime.Localization;
using Core.Runtime.Inputs;
using UnityEngine;

namespace Core.Runtime.Settings
{
    /// 公共设置制作配置；运行时读取副本，玩家修改只进入本机偏好。
    [CreateAssetMenu(menuName = "SleepyDemos/Settings/Game Settings Config")]
    public sealed class GameSettingsConfig : ScriptableObject
    {
        [SerializeField] private GameSettingsSnapshot defaults = new();
        [SerializeField] private SettingsOptions options = new();
        [SerializeField] private GraphicsQualityPreset[] qualityPresets = Array.Empty<GraphicsQualityPreset>();
        [SerializeField] private LanguageCatalog language;
        [SerializeField] private FontMappingCatalog fonts;
        [SerializeField] private InputSettingsCatalog inputs;

        /// 画质预设由显示服务只读使用。
        public IReadOnlyList<GraphicsQualityPreset> QualityPresets => qualityPresets;
        /// 模块词表。
        public LanguageCatalog Language => language;
        /// 各语言字体组。
        public FontMappingCatalog Fonts => fonts;
        /// 实际宿主动作资产与改键候选校验。
        public InputSettingsCatalog Inputs => inputs;

        /// 取得独立默认候选。
        public GameSettingsSnapshot GetDefaults() => defaults.Copy();
        /// 取得独立的控件范围，不让页面读写制作 SO。
        public SettingsOptions GetOptions() => options.Copy();

        /// 验证正式配置，缺失的资源不能用运行时临时资产掩盖。
        public void Validate()
        {
            if (defaults == null || options == null || language == null || fonts == null || inputs == null || qualityPresets == null)
            {
                throw new ArgumentException("公共设置配置或语言资源缺失。");
            }

            defaults.Validate();
            options.Validate();
            options.ValidateCandidate(defaults);
            inputs.Validate();
        }

        /// <summary>编辑器创建正式配置时设置资源引用，不用于保存玩家偏好。</summary>
        /// <param name="value">完整制作默认值。</param>
        /// <param name="languageCatalog">语言词表。</param>
        /// <param name="fontCatalog">字体映射。</param>
        /// <param name="presets">当前平台的质量预设。</param>
        /// <param name="inputCatalog">正式宿主动作资产目录。</param>
        public void Configure(GameSettingsSnapshot value, LanguageCatalog languageCatalog,
            FontMappingCatalog fontCatalog, GraphicsQualityPreset[] presets, InputSettingsCatalog inputCatalog)
        {
            defaults = value?.Copy();
            language = languageCatalog;
            fonts = fontCatalog;
            qualityPresets = presets ?? Array.Empty<GraphicsQualityPreset>();
            inputs = inputCatalog;
            Validate();
        }
    }
}
