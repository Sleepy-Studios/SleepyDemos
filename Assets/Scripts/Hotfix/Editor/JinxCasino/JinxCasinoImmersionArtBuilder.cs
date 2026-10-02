using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.JinxCasino
{
    /// 样板模型独立材质与导入配置；不覆盖旧原型调色板或已有人工材质调整。
    public static class JinxCasinoImmersionArtBuilder
    {
        public const string Root = "Assets/LoadResources/Demos/jinx_casino/Art/Immersion";
        private static readonly string[] ModelNames = { "S1Slots", "S1Blackjack", "S1Levers", "S1Hall" };
        private static readonly string[] MaterialNames = { "InkBlue", "PlumRed", "CreamYellow", "Mint", "CopperGold", "FaceInk", "PaperLight",
            "color_blue", "color_pink", "color_mint", "color_gold", "color_violet" };
        private static readonly string[] Colors = { "14213B", "AD315D", "F4DC9A", "54C4AD", "BE8C50", "101623", "FFF1CB",
            "36A8FF", "FF6BA9", "56DFB0", "F5CA57", "A57AFF" };
        private static readonly float[] Metallic = { .04f, .08f, .04f, .08f, .88f, .05f, .04f, .12f, .12f, .12f, .12f, .12f };
        private static readonly float[] Roughness = { .73f, .46f, .62f, .46f, .27f, .46f, .62f, .46f, .46f, .46f, .46f, .46f };

        /// 配置四个新模型与独立URP材质，完成后由Unity Test Runner验证模型合同。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/沉浸样板/配置模型导入与材质")]
        public static void PrepareImports()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("模型导入须在编辑模式执行。");
            foreach (string name in ModelNames)
                if (!File.Exists(Root + "/Models/" + name + ".fbx")) throw new FileNotFoundException("缺少独立样板模型：" + name);
            Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh();
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("未找到项目URP Lit。");
            var palette = new Dictionary<string, Material>();
            for (int i = 0; i < MaterialNames.Length; i++)
            {
                string path = Root + "/Materials/" + MaterialNames[i] + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    ColorUtility.TryParseHtmlString("#" + Colors[i], out var color);
                    material = new Material(shader) { name = MaterialNames[i] };
                    material.SetColor("_BaseColor", color);
                    material.SetFloat("_Metallic", Metallic[i]);
                    material.SetFloat("_Smoothness", 1 - Roughness[i]);
                    AssetDatabase.CreateAsset(material, path);
                }
                palette.Add(MaterialNames[i], material);
            }
            foreach (string name in ModelNames)
            {
                string path = Root + "/Models/" + name + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null) throw new InvalidOperationException("模型导入失败：" + path);
                importer.globalScale = 1; importer.importCameras = false; importer.importLights = false;
                importer.importAnimation = false; importer.addCollider = false; importer.isReadable = false;
                foreach (var pair in palette)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), pair.Key), pair.Value);
                importer.SaveAndReimport();
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[JinxCasino] 四个独立样板模型已配置；等待Importer/目标坐标门禁，不代表场景视觉完成。");
        }
    }
}
