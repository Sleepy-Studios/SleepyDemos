using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Hotfix.BlockPorters;
using Hotfix.Editor.BlockPorters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Demo
{
    public sealed class BlockPortersBatchTests
    {
        [UnityTest]
        public IEnumerator BatchFailureContinuesAndCancelPreventsLateExports()
        {
            const string catalogPath = BlockPortersWorkbenchIO.DataRoot + "/BatchTestCatalog.asset";
            string[] names = { "BatchTestA", "BatchTestB", "BatchTestCanceled" };
            foreach (string name in names)
            {
                Assert.That(AssetDatabase.LoadMainAssetAtPath(BlockPortersWorkbenchIO.SettingsRoot + "/Sources/" + name + ".png"), Is.Null);
                Assert.That(AssetDatabase.LoadMainAssetAtPath(BlockPortersWorkbenchIO.SettingsRoot + "/Recipes/" + name + ".asset"), Is.Null);
                Assert.That(AssetDatabase.LoadMainAssetAtPath(BlockPortersWorkbenchIO.DataRoot + "/" + name + ".asset"), Is.Null);
            }
            var seedRecipe = ScriptableObject.CreateInstance<BlockPortersRecipe>();
            seedRecipe.Settings.Width = seedRecipe.Settings.Height = 2; seedRecipe.Settings.Difficulty = PorterDifficulty.Easy;
            seedRecipe.Settings.Candidates = 1; seedRecipe.Settings.PolicyRuns = 0;
            var catalog = ScriptableObject.CreateInstance<BlockPortersLevelCatalog>();
            Assert.That(AssetDatabase.LoadMainAssetAtPath(catalogPath), Is.Null); AssetDatabase.CreateAsset(catalog, catalogPath);
            var window = ScriptableObject.CreateInstance<BlockPortersLevelEditorWindow>();
            Directory.CreateDirectory("Library/BlockPorters/BatchTests");
            var texture = new Texture2D(2, 2); texture.SetPixels(new[] { Color.red, Color.red, Color.red, Color.red }); texture.Apply();
            byte[] png = texture.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(texture);
            foreach (string name in names) File.WriteAllBytes("Library/BlockPorters/BatchTests/" + name + ".png", png);
            try
            {
                Set(window, "recipe", seedRecipe); Set(window, "catalog", catalog); window.Show();
                Invoke(window, "BeginBatch", new string[] { "Library/BlockPorters/BatchTests/BatchTestA.png", "Library/BlockPorters/BatchTests/Missing.png", "Library/BlockPorters/BatchTests/BatchTestB.png" });
                yield return WaitDone(window);
                Assert.That(catalog.Levels.Length, Is.EqualTo(2));
                var report = (System.Collections.Generic.List<string>)Get(window, "batchReport");
                Assert.That(report.Count, Is.EqualTo(3)); Assert.That(report.Count(line => line.Contains("失败")), Is.EqualTo(1));
                Assert.That(report.Single(line => line.Contains("失败")), Does.Contain("Missing.png"));
                Invoke(window, "BeginBatch", new string[] { "Library/BlockPorters/BatchTests/BatchTestCanceled.png" });
                Set(window, "isCanceling", true); ((System.Threading.CancellationTokenSource)Get(window, "cancellation")).Cancel();
                yield return WaitDone(window);
                Assert.That(catalog.Levels.Length, Is.EqualTo(2));
                Assert.That(AssetDatabase.LoadMainAssetAtPath(BlockPortersWorkbenchIO.DataRoot + "/BatchTestCanceled.asset"), Is.Null);
            }
            finally
            {
                window.Close(); UnityEngine.Object.DestroyImmediate(seedRecipe);
                foreach (string name in names)
                {
                    AssetDatabase.DeleteAsset(BlockPortersWorkbenchIO.DataRoot + "/" + name + ".asset");
                    AssetDatabase.DeleteAsset(BlockPortersWorkbenchIO.SettingsRoot + "/Recipes/" + name + ".asset");
                    AssetDatabase.DeleteAsset(BlockPortersWorkbenchIO.SettingsRoot + "/Sources/" + name + ".png");
                }
                AssetDatabase.DeleteAsset(catalogPath);
            }
        }
        private static IEnumerator WaitDone(BlockPortersLevelEditorWindow window)
        {
            double deadline = EditorApplication.timeSinceStartup + 60;
            while (Get(window, "work") != null || (bool)Get(window, "isBatch"))
            { Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline)); yield return null; }
        }
        private static void Invoke(object owner, string method, object parameter) => owner.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, new[] { parameter });
        private static object Get(object owner, string field) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    }
}
