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
            var catalogState = typeof(LanguageService).GetField("catalogs", BindingFlags.NonPublic | BindingFlags.Static);
            var originalCatalogs = catalogState.GetValue(null);
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
                target.text = "90°/s";
                LanguageService.SetFont(target, 1);
                Assert.That(target.text, Is.EqualTo("90°/s"), "动态数字单独换字体不应改文本");
                Assert.That(target.font, Is.SameAs(second));
                Assert.That(target.fontSharedMaterial, Is.SameAs(second.material));
                Assert.That(target.fontSize, Is.EqualTo(36));
                Assert.That(LanguageService.CurrentCulture.Name, Is.EqualTo("zh-CN"));
                Assert.Throws<ArgumentException>(() => LanguageService.SelectLanguage("ja"));
                Assert.That(LanguageService.CurrentLanguage, Is.EqualTo("zh-CN"));
            }
            finally
            {
                // 测试替换静态配置后恢复原服务，避免污染同一 Editor 的 Hub 或其它回归。
                textState.SetValue(null, originalTexts);
                fontState.SetValue(null, originalFonts);
                catalogState.SetValue(null, originalCatalogs);
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
        public void CatalogScopesShareRegistrationUntilLastLeaseAndPreserveLanguage()
        {
            WithInitializedService((common, fonts) =>
            {
                var demo = ScriptableObject.CreateInstance<LanguageCatalog>();
                IDisposable first = null;
                IDisposable second = null;
                try
                {
                    demo.Configure(new[] { new LanguageCatalog.Entry("demo", 0, "玩法", "Demo") });
                    LanguageService.SelectLanguage("en");
                    first = LanguageService.RegisterCatalog(demo);
                    second = LanguageService.RegisterCatalog(demo);
                    Assert.That(LanguageService.Get("demo"), Is.EqualTo("Demo"));
                    first.Dispose();
                    first.Dispose();
                    Assert.That(LanguageService.Get("demo"), Is.EqualTo("Demo"));
                    Assert.That(LanguageService.CurrentLanguage, Is.EqualTo("en"));
                    Assert.That(LanguageService.CurrentCulture.Name, Is.EqualTo("en-US"));
                    second.Dispose();
                    Assert.That(LanguageService.Get("demo"), Is.EqualTo("demo"));
                    Assert.That(LanguageService.Get("common"), Is.EqualTo("Common"));
                    using (LanguageService.RegisterCatalog(demo))
                    {
                        LanguageService.SelectLanguage("zh-CN");
                        Assert.That(LanguageService.Get("demo"), Is.EqualTo("玩法"));
                    }
                }
                finally
                {
                    first?.Dispose();
                    second?.Dispose();
                    Object.DestroyImmediate(demo);
                }
            });
        }

        [Test]
        public void CatalogRegistrationFailsAtomicallyAndOldLeasesCannotRemoveNewState()
        {
            WithInitializedService((common, fonts) =>
            {
                var demo = ScriptableObject.CreateInstance<LanguageCatalog>();
                var invalid = ScriptableObject.CreateInstance<LanguageCatalog>();
                IDisposable old = null;
                IDisposable current = null;
                try
                {
                    invalid.Configure(new[]
                    {
                        new LanguageCatalog.Entry("partial", 0, "临时", "Partial"),
                        new LanguageCatalog.Entry("common", 0, "冲突", "Conflict")
                    });
                    Assert.Throws<ArgumentException>(() => LanguageService.RegisterCatalog(invalid));
                    Assert.That(LanguageService.Get("partial"), Is.EqualTo("partial"));
                    Assert.That(LanguageService.Get("common"), Is.EqualTo("公共"));
                    invalid.Configure(new[]
                    {
                        new LanguageCatalog.Entry("partial", 0, "临时", "Partial"),
                        new LanguageCatalog.Entry("missing-font", 99, "缺字体", "Missing font")
                    });
                    Assert.Throws<ArgumentException>(() => LanguageService.RegisterCatalog(invalid));
                    Assert.That(LanguageService.Get("partial"), Is.EqualTo("partial"));

                    demo.Configure(new[] { new LanguageCatalog.Entry("demo", 0, "玩法", "Demo") });
                    old = LanguageService.RegisterCatalog(demo);
                    invalid.Configure(new[] { new LanguageCatalog.Entry("demo", 0, "冲突", "Conflict") });
                    Assert.Throws<ArgumentException>(() => LanguageService.RegisterCatalog(invalid));
                    LanguageService.Initialize(common, fonts, "zh-CN");
                    current = LanguageService.RegisterCatalog(demo);
                    old.Dispose();
                    Assert.That(LanguageService.Get("demo"), Is.EqualTo("玩法"));

                    typeof(LanguageService).GetMethod("Reset", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
                    LanguageService.Initialize(common, fonts, "en");
                    using (LanguageService.RegisterCatalog(demo))
                    {
                        current.Dispose();
                        Assert.That(LanguageService.Get("demo"), Is.EqualTo("Demo"));
                    }
                    Assert.That(LanguageService.Get("demo"), Is.EqualTo("demo"));
                }
                finally
                {
                    old?.Dispose();
                    current?.Dispose();
                    Object.DestroyImmediate(demo);
                    Object.DestroyImmediate(invalid);
                }
            });
        }

        private static void WithInitializedService(Action<LanguageCatalog, FontMappingCatalog> action)
        {
            var fields = new[] { "texts", "fonts", "languages", "catalogs" };
            var previous = new object[fields.Length];
            for (int i = 0; i < fields.Length; i++)
            {
                previous[i] = typeof(LanguageService).GetField(fields[i], BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            }
            string language = LanguageService.CurrentLanguage;
            var common = ScriptableObject.CreateInstance<LanguageCatalog>();
            var fonts = ScriptableObject.CreateInstance<FontMappingCatalog>();
            try
            {
                var chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset");
                var english = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/EN/BebasNeue_EN.asset");
                Assert.That(chinese, Is.Not.Null);
                Assert.That(english, Is.Not.Null);
                common.Configure(new[] { new LanguageCatalog.Entry("common", 0, "公共", "Common") });
                fonts.Configure(new[]
                {
                    new FontMappingCatalog.Entry(0, "zh-CN", chinese, chinese.material),
                    new FontMappingCatalog.Entry(0, "en", english, english.material)
                });
                LanguageService.Initialize(common, fonts, "zh-CN");
                action(common, fonts);
            }
            finally
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    typeof(LanguageService).GetField(fields[i], BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, previous[i]);
                }
                typeof(LanguageService).GetProperty(nameof(LanguageService.CurrentLanguage)).SetValue(null, language);
                Object.DestroyImmediate(common);
                Object.DestroyImmediate(fonts);
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
