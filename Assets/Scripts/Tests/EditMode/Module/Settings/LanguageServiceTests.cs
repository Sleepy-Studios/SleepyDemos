using System;
using System.Reflection;
using Core.Runtime.Localization;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class LanguageServiceTests
    {
        [Test]
        public void FontIdsRestoreDistinctChineseFontsAfterSharedEnglishAndPreserveTextStyle()
        {
            var textState = typeof(LanguageService).GetField("texts", BindingFlags.NonPublic | BindingFlags.Static);
            var fontState = typeof(LanguageService).GetField("fonts", BindingFlags.NonPublic | BindingFlags.Static);
            var originalTexts = textState.GetValue(null);
            var originalFonts = fontState.GetValue(null);
            string originalLanguage = LanguageService.CurrentLanguage;
            var chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset");
            var english = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/EN/BebasNeue_EN.asset");
            Assert.That(chinese, Is.Not.Null);
            Assert.That(english, Is.Not.Null);
            var second = Object.Instantiate(chinese);
            var third = Object.Instantiate(chinese);
            var catalog = ScriptableObject.CreateInstance<LanguageCatalog>();
            var fonts = ScriptableObject.CreateInstance<FontMappingCatalog>();
            var languageState = typeof(LanguageService).GetField("languages", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            var originalLanguages = languageState.GetValue(null);
            var root = new GameObject("LanguageText", typeof(RectTransform), typeof(TextMeshProUGUI));
            try
            {
                catalog.Configure(new[]
                {
                    new LanguageCatalog.Entry("first", 0, "标题 {0}", "Title {0}"),
                    new LanguageCatalog.Entry("second", 1, "正文", "Body"),
                    new LanguageCatalog.Entry("third", 2, "推荐体验", "Recommended")
                });
                fonts.Configure(new[]
                {
                    new FontMappingCatalog.Entry(0, "zh-CN", chinese, chinese.material),
                    new FontMappingCatalog.Entry(1, "zh-CN", second, second.material),
                    new FontMappingCatalog.Entry(2, "zh-CN", third, third.material),
                    new FontMappingCatalog.Entry(0, "en", english, english.material),
                    new FontMappingCatalog.Entry(1, "en", english, english.material),
                    new FontMappingCatalog.Entry(2, "en", english, english.material)
                });
                LanguageService.Initialize(catalog, fonts, "zh-CN");
                var target = root.GetComponent<TextMeshProUGUI>();
                target.fontSize = 36;
                target.color = Color.cyan;
                target.alignment = TextAlignmentOptions.BottomRight;
                LanguageService.SetText(target, "second");
                Assert.That(target.font, Is.SameAs(second));
                LanguageService.SetText(target, "third");
                Assert.That(target.font, Is.SameAs(third));
                LanguageService.SelectLanguage("en");
                LanguageService.SetText(target, "first", null, 3);
                Assert.That(target.text, Is.EqualTo("Title 3"));
                Assert.That(target.font, Is.SameAs(english));
                Assert.That(target.fontSharedMaterial, Is.SameAs(english.material));
                LanguageService.SetText(target, "second");
                Assert.That(target.font, Is.SameAs(english));
                LanguageService.SetText(target, "third");
                Assert.That(target.font, Is.SameAs(english));
                LanguageService.SelectLanguage("zh-CN");
                LanguageService.SetText(target, "third");
                Assert.That(target.font, Is.SameAs(third));
                LanguageService.SetText(target, "second");
                Assert.That(target.font, Is.SameAs(second));
                LanguageService.SetText(target, "second", 0);
                Assert.That(target.font, Is.SameAs(chinese), "显式零覆盖有效，不能当成空值");
                LanguageService.SetText(target, "first", null, 4);
                Assert.That(target.font, Is.SameAs(chinese));
                Assert.That(target.text, Is.EqualTo("标题 4"));
                Assert.That(target.fontSize, Is.EqualTo(36));
                Assert.That(target.color, Is.EqualTo(Color.cyan));
                Assert.That(target.alignment, Is.EqualTo(TextAlignmentOptions.BottomRight));
                Assert.Throws<ArgumentException>(() => LanguageService.SelectLanguage("ja"));
                Assert.That(LanguageService.CurrentLanguage, Is.EqualTo("zh-CN"));
            }
            finally
            {
                // 测试替换静态配置后恢复原服务，避免污染同一 Editor 的 Hub 或其它回归。
                textState.SetValue(null, originalTexts);
                fontState.SetValue(null, originalFonts);
                languageState.SetValue(null, originalLanguages);
                typeof(LanguageService).GetProperty(nameof(LanguageService.CurrentLanguage)).SetValue(null, originalLanguage);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(fonts);
                // TMP 克隆仍引用正式图集，析构前解除共享引用，不能由测试销毁资产。
                second.atlasTextures = Array.Empty<Texture2D>();
                second.material = null;
                Object.DestroyImmediate(second);
                third.atlasTextures = Array.Empty<Texture2D>();
                third.material = null;
                Object.DestroyImmediate(third);
            }
        }

        [Test]
        public void CatalogRejectsDuplicateKeysAndMismatchedFontAtlas()
        {
            var catalog = ScriptableObject.CreateInstance<LanguageCatalog>();
            var fonts = ScriptableObject.CreateInstance<FontMappingCatalog>();
            try
            {
                var entry = new LanguageCatalog.Entry("duplicate", 0, "文案", "Text");
                Assert.Throws<ArgumentException>(() => catalog.Configure(new[] { entry, entry }));
                Assert.Throws<ArgumentException>(() => fonts.Configure(new[]
                {
                    new FontMappingCatalog.Entry(0, "zh-CN", null, null)
                }));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(fonts);
            }
        }
    }
}
