using System;
using System.IO;
using System.Linq;
using Hotfix.BlockPorters;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.BlockPorters
{
    public static class BlockPortersWorkbenchIO
    {
        public const string SettingsRoot = "Assets/Settings/BlockPorters";
        public const string DataRoot = "Assets/LoadResources/Demos/block_porters/Data";
        public const string CatalogPath = DataRoot + "/LevelCatalog.asset";

        /// <summary>拷贝 PNG/JPG 原图到编辑资料目录并创建可继续编辑的配方。</summary>
        /// <param name="path">项目内或外部图片的绝对/相对文件路径。</param>
        public static BlockPortersRecipe Import(string path)
        {
            var texture = CopySource(path);
            Directory.CreateDirectory(SettingsRoot + "/Recipes"); AssetDatabase.Refresh();
            var recipe = ScriptableObject.CreateInstance<BlockPortersRecipe>(); recipe.SetSource(texture);
            AssetDatabase.CreateAsset(recipe, AssetDatabase.GenerateUniqueAssetPath(SettingsRoot + "/Recipes/" + Path.GetFileNameWithoutExtension(path) + ".asset"));
            AssetDatabase.SaveAssets(); return recipe;
        }

        /// <summary>保存原图副本；资料目录内已有图片直接复用，其他输入不会改动原文件。</summary>
        /// <param name="path">PNG/JPG 的项目路径或外部文件路径。</param>
        public static Texture2D CopySource(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg") throw new ArgumentException("只支持 PNG/JPG。");
            if (path.Replace('\\', '/').StartsWith(SettingsRoot + "/Sources/", StringComparison.Ordinal))
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException("原图不存在。");
            Directory.CreateDirectory(SettingsRoot + "/Sources"); AssetDatabase.Refresh();
            string target = AssetDatabase.GenerateUniqueAssetPath(SettingsRoot + "/Sources/" + Path.GetFileName(path));
            File.Copy(path, target); AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(target) ?? throw new InvalidOperationException("图片导入失败。");
        }

        /// <summary>直接从源文件解码像素，不更改已有 TextureImporter 设置。</summary>
        /// <param name="source">本地图片资产。</param>
        /// <param name="width">实际文件宽度。</param>
        /// <param name="height">实际文件高度。</param>
        public static Color32[] ReadPixels(Texture2D source, out int width, out int height)
        {
            if (source == null) throw new ArgumentException("请先选择原图。");
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!decoded.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)))) throw new InvalidOperationException("源图解码失败。");
                width = decoded.width; height = decoded.height; return decoded.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        /// <summary>只导出当前版本的已验证难度候选；更新原资产保留 GUID，并加入关卡集。</summary>
        /// <param name="recipe">编辑配方。</param>
        /// <param name="analysis">通过共享规则验证的参考解和难度报告。</param>
        /// <param name="revision">分析时配方版本，过期直接拒绝。</param>
        /// <param name="path">新资产必须在 Demo Data 下，已有导出沿用原路径。</param>
        /// <param name="catalog">关卡集，可为空以读取正式默认资产。</param>
        public static BlockPortersLevel Export(BlockPortersRecipe recipe, PorterAnalysis analysis, int revision, string path, BlockPortersLevelCatalog catalog = null)
        {
            if (recipe.Revision != revision || analysis == null || !BlockPortersLevelGenerator.Matches(recipe.Settings.Difficulty, analysis))
                throw new InvalidOperationException("当前配方未通过可解性和目标难度验证。");
            var data = recipe.CreateData();
            var scheduler = new BlockPortersScheduler(new BlockPortersSession(data));
            foreach (int column in analysis.Solution) { if (!scheduler.Dispatch(column)) throw new InvalidOperationException("参考解派队失败。"); scheduler.Settle(); }
            if (scheduler.Session.Status != BlockPortersStatus.Won) throw new InvalidOperationException("参考解未通关。");
            catalog ??= AssetDatabase.LoadAssetAtPath<BlockPortersLevelCatalog>(CatalogPath);
            if (catalog == null) throw new InvalidOperationException("正式关卡集不存在。");
            var level = recipe.ExportedLevel;
            if (level != null) path = AssetDatabase.GetAssetPath(level);
            if (!path.StartsWith(DataRoot + "/", StringComparison.Ordinal) || path.Contains("..") || Path.GetExtension(path) != ".asset")
                throw new ArgumentException("导出路径必须位于本 Demo 的 Data 目录。");
            if (level == null) level = AssetDatabase.LoadAssetAtPath<BlockPortersLevel>(path);
            bool isNew = level == null;
            if (isNew) level = ScriptableObject.CreateInstance<BlockPortersLevel>();
            else Undo.RecordObject(level, "更新搬砖关卡");
            level.Configure(recipe.DisplayName, data, recipe.Palette, analysis.Solution); level.SetColorLabels(recipe.Labels);
            if (isNew) AssetDatabase.CreateAsset(level, path); else EditorUtility.SetDirty(level);
            Undo.RecordObject(recipe, "保存导出引用"); recipe.SetExportedLevel(level); EditorUtility.SetDirty(recipe);
            if (!catalog.Levels.Contains(level)) { Undo.RecordObject(catalog, "增加搬砖关卡"); catalog.Configure(catalog.Levels.Concat(new[] { level }).ToArray()); EditorUtility.SetDirty(catalog); }
            AssetDatabase.SaveAssets(); return level;
        }
    }
}
