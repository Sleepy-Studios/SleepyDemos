using System.IO;
using Core.Editor.MvcBind;
using Core.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Module
{
    public sealed class MvcBindCustomOutputDirectoryTests
    {
        [Test]
        public void ToOutputFolder_WhenCustomDirectoryDisabled_PreservesDefaultConvention()
        {
            var settings = CreateSettings();

            var output = MvcBindPathUtility.ToOutputFolder(settings);

            Assert.That(
                output,
                Is.EqualTo("Assets/Scripts/Hotfix/Module/DroneFlight/DroneFlightHudView/View"));
        }

        [Test]
        public void ToOutputFolder_WhenCustomDirectoryEnabled_DoesNotAppendModuleAgain()
        {
            var settings = CreateSettings();
            settings.useCustomModuleOutputDirectory = true;
            settings.customModuleOutputDirectory =
                "Assets/Scripts/Hotfix/Demos/DroneFlight/Adapters/SleepyDemos/UI";

            var output = MvcBindPathUtility.ToOutputFolder(settings);

            Assert.That(
                output,
                Is.EqualTo(
                    "Assets/Scripts/Hotfix/Demos/DroneFlight/Adapters/SleepyDemos/UI/DroneFlightHudView/View"));
            StringAssert.DoesNotContain("UI/DroneFlight/", output);
        }

        [Test]
        public void ApplyPrefabPath_WhenCustomDirectoryEnabled_UsesPrefabNameAndKeepsCustomDirectory()
        {
            var settings = CreateSettings();
            settings.useCustomModuleOutputDirectory = true;
            settings.customModuleOutputDirectory =
                "Assets/Scripts/Hotfix/Demos/DroneFlight/Adapters/SleepyDemos/UI";

            var applied = settings.ApplyPrefabPath(
                "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightDebugView.prefab");

            Assert.That(applied, Is.True);
            Assert.That(settings.viewName, Is.EqualTo("DroneFlightDebugView"));
            Assert.That(
                settings.outputFolder,
                Is.EqualTo(
                    "Assets/Scripts/Hotfix/Demos/DroneFlight/Adapters/SleepyDemos/UI/DroneFlightDebugView/View"));
        }

        [TestCase("")]
        [TestCase("Packages/DroneFlight")]
        [TestCase("C:/Temp/DroneFlight")]
        [TestCase("Assets/../Packages/DroneFlight")]
        public void IsValidCustomModuleOutputDirectory_WhenOutsideAssets_ReturnsFalse(string path)
        {
            Assert.That(MvcBindPathUtility.IsValidCustomModuleOutputDirectory(path), Is.False);
        }

        [Test]
        public void TryConvertToAssetFolder_WhenProjectAssetsChild_ReturnsProjectRelativePath()
        {
            var absolutePath = Path.Combine(Application.dataPath, "Scripts", "Hotfix");

            var converted = MvcBindWindow.TryConvertToAssetFolder(absolutePath, out var assetFolder);

            Assert.That(converted, Is.True);
            Assert.That(assetFolder, Is.EqualTo("Assets/Scripts/Hotfix"));
        }

        [Test]
        public void TryConvertToAssetFolder_WhenOutsideProjectAssets_ReturnsFalse()
        {
            var outsidePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library"));

            var converted = MvcBindWindow.TryConvertToAssetFolder(outsidePath, out var assetFolder);

            Assert.That(converted, Is.False);
            Assert.That(assetFolder, Is.Empty);
        }

        [Test]
        public void GenerateAndBind_WhenModuleIsEmpty_FailsBeforeChangingPrefab()
        {
            var target = new GameObject("MissingModuleView", typeof(RectTransform));
            try
            {
                var settings = new MvcBindSettings
                {
                    prefabPath = "Assets/LoadResources/UI/MissingModuleView.prefab",
                    viewName = "MissingModuleView",
                    moduleName = string.Empty,
                    useCustomModuleOutputDirectory = true,
                    customModuleOutputDirectory = "Assets/Scripts/Hotfix/Demos"
                };
                var node = new MvcBindNode
                {
                    name = target.name,
                    path = target.name,
                    gameObject = target,
                    selectedComponentType = typeof(RectTransform)
                };

                var success = MvcBindComponentWindowBridge.GenerateAndBind(
                    target,
                    settings,
                    new[] { node },
                    false,
                    out _,
                    out var message);

                Assert.That(success, Is.False);
                Assert.That(target.GetComponent<ComponentItemIndex>(), Is.Null);
                StringAssert.Contains("必须填写 Module", message);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        private static MvcBindSettings CreateSettings()
        {
            return new MvcBindSettings
            {
                moduleName = "DroneFlight",
                viewName = "DroneFlightHudView"
            };
        }
    }
}
