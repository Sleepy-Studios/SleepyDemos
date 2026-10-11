using System.Reflection;
using Core.Editor.TextMeshPro;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Tests.Module
{
    public sealed class SharedFontAssetTests
    {
        [Test]
        public void EnglishSymbolsReusePublicChineseFallbackWithoutCycle()
        {
            var chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset");
            var english = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/EN/BebasNeue_EN.asset");
            Assert.That(chinese.HasCharacter('←'), Is.True, "左箭头由现有公共中文字库直接提供。");
            Assert.That(english.fallbackFontAssetTable, Does.Contain(chinese));
            Assert.That(english.HasCharacter('←', searchFallbacks: true), Is.True);
            Assert.That(chinese.fallbackFontAssetTable.Contains(english), Is.False, "不能形成中英相互回退的循环。");
        }

        [Test]
        public void UpdatingExistingFontImmediatelyRefreshesCharacterLookup()
        {
            const string path = "Assets/SharedFontCacheRegressionTemporary.asset";
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/LoadResources/Fonts/Source/CN/HarmonyOS_CN.ttf");
            TMP_FontAsset first = null;
            TMP_FontAsset updated = null;
            try
            {
                Assert.That(AssetDatabase.LoadMainAssetAtPath(path), Is.Null);
                first = TMP_FontAsset.CreateFontAsset(source, 36, 5, GlyphRenderMode.SDFAA, 128, 128, AtlasPopulationMode.Dynamic, false);
                Assert.That(first.TryAddCharacters("A"), Is.True);
                first.name = "SharedFontCacheRegressionTemporary";
                AssetDatabase.CreateAsset(first, path);
                AssetDatabase.AddObjectToAsset(first.material, first);
                AssetDatabase.AddObjectToAsset(first.atlasTexture, first);
                Assert.That(first.HasCharacter('A'), Is.True);
                Assert.That(first.HasCharacter('B'), Is.False);
                updated = TMP_FontAsset.CreateFontAsset(source, 36, 5, GlyphRenderMode.SDFAA, 128, 128, AtlasPopulationMode.Dynamic, false);
                Assert.That(updated.TryAddCharacters("AB"), Is.True);
                var save = typeof(TMPFontBuilderWindow).GetMethod("SaveFontAsset", BindingFlags.Static | BindingFlags.NonPublic);
                var result = (TMP_FontAsset)save.Invoke(null, new object[] { updated, path, false });
                Assert.That(result, Is.SameAs(first));
                Assert.That(result.HasCharacter('B'), Is.True);
            }
            finally
            {
                if (updated != null)
                {
                    // 图集已转入持久化目标，临时字体销毁时不能再销毁它。
                    updated.atlasTextures = System.Array.Empty<Texture2D>();
                    Object.DestroyImmediate(updated.material);
                    Object.DestroyImmediate(updated);
                }
                if (first != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }
        }

        [TestCase("CN", "HarmonyOS_CN")]
        [TestCase("EN", "BebasNeue_EN")]
        public void SharedFontContainsItsMaintainedCharacterSet(string language, string name)
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/LoadResources/Fonts/TMP_FontAssets/" + language + "/" + name + ".asset");
            var characters = AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>(
                "Assets/LoadResources/Fonts/Fallbacks/Default_" + language + "_Characters.txt");
            Assert.That(font, Is.Not.Null);
            Assert.That(characters, Is.Not.Null);
            foreach (var character in characters.text)
            {
                if (!char.IsControl(character))
                {
                    Assert.That(font.HasCharacter((int)character), Is.True, "U+" + ((int)character).ToString("X4"));
                }
            }
        }
    }
}
