using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        private static void EnsureSharedChineseFallback()
        {
            // 通用Loading使用原共享字体；只补充独立共享fallback，不能让公共UI依赖某个Demo字库。
            const string primaryPath = "Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset";
            const string supplementPath = "Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CNSupplement.asset";
            var primary = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(primaryPath);
            if (primary == null) throw new InvalidOperationException("通用Loading中文字体不存在。");
            if (primary.HasCharacter('霉', true)) return;
            var supplement = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(supplementPath);
            if (supplement == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
                supplement = TMP_FontAsset.CreateFontAsset(source, 54, 5, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic, true);
                if (supplement == null || !supplement.TryAddCharacters("霉", out string missing) || !string.IsNullOrEmpty(missing))
                    throw new InvalidOperationException("通用中文补充字体缺少目标字形。");
                supplement.name = "HarmonyOS_CNSupplement";
                var material = supplement.material;
                var atlases = (Texture2D[])supplement.atlasTextures.Clone();
                supplement.material = null;
                AssetDatabase.CreateAsset(supplement, supplementPath);
                foreach (var atlas in atlases)
                {
                    atlas.name = "HarmonyOS_CNSupplement Atlas"; AssetDatabase.AddObjectToAsset(atlas, supplement);
                }
                material.name = "HarmonyOS_CNSupplement Material";
                material.SetTexture("_MainTex", atlases[0]); AssetDatabase.AddObjectToAsset(material, supplement);
                supplement.material = material; supplement.atlasTextures = atlases;
                EditorUtility.SetDirty(supplement); AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(supplementPath, ImportAssetOptions.ForceSynchronousImport);
                supplement = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(supplementPath);
            }
            if (supplement == null || supplement.material == null || supplement.atlasTexture == null ||
                supplement.material.GetTexture("_MainTex") != supplement.atlasTexture || !supplement.HasCharacter('霉'))
                throw new InvalidOperationException("通用中文补充字体没有有效的持久图集/材质。");
            primary.fallbackFontAssetTable ??= new List<TMP_FontAsset>();
            if (!primary.fallbackFontAssetTable.Contains(supplement)) primary.fallbackFontAssetTable.Add(supplement);
            primary.ReadFontAssetDefinition(); EditorUtility.SetDirty(primary); AssetDatabase.SaveAssets();
            if (!primary.HasCharacter('霉', true)) throw new InvalidOperationException("通用Loading字体没有解析到补充字形。");
        }
    }
}
