using System;
using System.IO;
using Core.Editor.Hotfix;
using NUnit.Framework;
using UnityEditor;
using YooAsset.Editor;

namespace Tests.Module
{
    public sealed class HotfixAssemblyDefinitionFilterTests
    {
        [Test]
        public void CopyAssemblies_ProfileRoundTrip_PreservesGuidAndExistingMeta()
        {
            string root = "Assets/AssemblySync-" + Guid.NewGuid().ToString("N");
            string source = Path.Combine(Path.GetTempPath(), "SleepyDemos-AssemblySync-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(source);
            Directory.CreateDirectory(root);
            string keptMeta = "fileFormatVersion: 2\nguid: 1234567890abcdef1234567890abcdef\nTextScriptImporter:\n  externalObjects: {}\n  userData: keep\n  assetBundleName: \n  assetBundleVariant: \n";
            try
            {
                File.WriteAllBytes(Path.Combine(source, "Drone.dll"), new byte[] { 1, 2, 3 });
                File.WriteAllBytes(Path.Combine(source, "Kart.dll"), new byte[] { 4, 5, 6 });
                File.WriteAllBytes(root + "/Kart.dll.bytes", new byte[] { 0 });
                File.WriteAllText(root + "/Kart.dll.bytes.meta", keptMeta);
                HotfixAssemblyBuild.CopyAssemblies(source, root, new[] { "Drone.dll", "Kart.dll" });
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string guid = AssetDatabase.AssetPathToGUID(root + "/Drone.dll.bytes");
                Assert.That(guid, Is.Not.Empty);
                string originalMeta = File.ReadAllText(root + "/Kart.dll.bytes.meta");

                HotfixAssemblyBuild.CopyAssemblies(source, root, new[] { "Kart.dll" });
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Assert.That(File.Exists(root + "/Drone.dll.bytes"), Is.False);
                Assert.That(File.Exists(root + "/Drone.dll.bytes.meta"), Is.False);
                HotfixAssemblyBuild.CopyAssemblies(source, root, new[] { "Drone.dll", "Kart.dll" });
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Assert.That(AssetDatabase.AssetPathToGUID(root + "/Drone.dll.bytes"), Is.EqualTo(guid));
                Assert.That(File.ReadAllBytes(root + "/Drone.dll.bytes"), Is.EqualTo(new byte[] { 1, 2, 3 }));
                Assert.That(File.ReadAllText(root + "/Kart.dll.bytes.meta"), Is.EqualTo(originalMeta));
            }
            finally
            {
                if (!AssetDatabase.DeleteAsset(root) && Directory.Exists(root))
                {
                    foreach (string file in Directory.GetFiles(root)) File.Delete(file);
                    Directory.Delete(root);
                    File.Delete(root + ".meta");
                }
                File.Delete(Path.Combine(source, "Drone.dll"));
                File.Delete(Path.Combine(source, "Kart.dll"));
                Directory.Delete(source);
            }
        }

        [Test]
        public void TryGetDllNameFromJson_RuntimeAssembly_ReturnsDllName()
        {
            const string json = "{\"name\":\"Hotfix\",\"references\":[\"Core.Runtime\"]}";

            var accepted = HotfixAssemblyDefinitionFilter.TryGetDllNameFromJson(json, out var dllName);

            Assert.That(accepted, Is.True);
            Assert.That(dllName, Is.EqualTo("Hotfix.dll"));
        }

        [Test]
        public void TryGetDllNameFromFile_HotfixAssembly_ReturnsDllName()
        {
            var accepted = HotfixAssemblyDefinitionFilter.TryGetDllNameFromFile(
                "Assets/Scripts/Hotfix/Hotfix.asmdef",
                out var dllName);

            Assert.That(accepted, Is.True);
            Assert.That(dllName, Is.EqualTo("Hotfix.dll"));
        }

        [Test]
        public void TryGetDllNameFromJson_TestAssemblyReference_ReturnsFalse()
        {
            const string json =
                "{\"name\":\"Core.Tests\",\"optionalUnityReferences\":[\"TestAssemblies\"]}";

            AssertRejected(json);
        }

        [Test]
        public void TryGetDllNameFromJson_TestsSuffix_ReturnsFalse()
        {
            AssertRejected("{\"name\":\"Hotfix.Tests\"}");
        }

        [Test]
        public void TryGetDllNameFromJson_EditorOnlyAssembly_ReturnsFalse()
        {
            const string json = "{\"name\":\"Tools\",\"includePlatforms\":[\"Editor\"]}";

            AssertRejected(json);
        }

        [TestCase("not json")]
        [TestCase("{}")]
        [TestCase("{\"name\":\"\"}")]
        public void TryGetDllNameFromJson_InvalidOrMissingName_ReturnsFalse(string json)
        {
            AssertRejected(json);
        }

        [TestCase("Meadow")]
        [TestCase("Bosphorus")]
        [TestCase("Glacier")]
        [TestCase("Desk")]
        public void ReleaseDemoPackRule_ChapterAssetsShareOwnBundle(string chapter)
        {
            IBundlePackRule rule = new ReleaseDemoPackRule();
            const string root = "Assets/LoadResources/Demos/kagit_kart/";
            string expected = new BundlePackRuleResult(root + "Chapters/" + chapter, "bundle").GetBundleName("DefaultPackage", true);
            foreach (string path in new[]
            {
                "Data/Tracks/" + chapter + ".json", "Data/AiPlans/" + chapter + ".json",
                "Data/Worlds/" + chapter + ".asset", "Prefabs/Tracks/" + chapter + ".prefab",
                "Data/TrackMeshes/" + chapter + "/Shape00.asset",
                "Art/Materials/Tracks/" + chapter + "/Print00.mat",
                "Art/Textures/Tracks/" + chapter + "/Print00.png"
            })
            {
                var data = new BundlePackRuleData(root + path, "Assets/LoadResources/Demos", "Demos", string.Empty);
                Assert.That(rule.GetPackRuleResult(data).GetBundleName("DefaultPackage", true), Is.EqualTo(expected), path);
            }
            string other = chapter == "Meadow" ? "Desk" : "Meadow";
            var otherData = new BundlePackRuleData(root + "Prefabs/Tracks/" + other + ".prefab", "Assets/LoadResources/Demos", "Demos", string.Empty);
            Assert.That(rule.GetPackRuleResult(otherData).GetBundleName("DefaultPackage", true), Is.Not.EqualTo(expected));
        }

        [Test]
        public void ReleaseDemoPackRule_MusicClipsAreSeparateAndDroneKeepsDirectoryPacking()
        {
            IBundlePackRule rule = new ReleaseDemoPackRule();
            const string music = "Assets/LoadResources/Demos/kagit_kart/Audio/SFX/Music/";
            var start = new BundlePackRuleData(music + "Music_Meadow_Start.ogg", "Assets/LoadResources/Demos", "Demos", string.Empty);
            var loop = new BundlePackRuleData(music + "Music_Meadow_Loop.ogg", "Assets/LoadResources/Demos", "Demos", string.Empty);
            Assert.That(rule.GetPackRuleResult(start).GetBundleName("DefaultPackage", true),
                Is.Not.EqualTo(rule.GetPackRuleResult(loop).GetBundleName("DefaultPackage", true)));
            var drone = new BundlePackRuleData("Assets/LoadResources/Demos/drone_flight/Prefabs/Drone.prefab", "Assets/LoadResources/Demos", "Demos", string.Empty);
            Assert.That(rule.GetPackRuleResult(drone).GetBundleName("DefaultPackage", true),
                Is.EqualTo(((IBundlePackRule)new PackDirectory()).GetPackRuleResult(drone).GetBundleName("DefaultPackage", true)));
        }

        private static void AssertRejected(string json)
        {
            var accepted = HotfixAssemblyDefinitionFilter.TryGetDllNameFromJson(json, out var dllName);

            Assert.That(accepted, Is.False);
            Assert.That(dllName, Is.Empty);
        }
    }
}
