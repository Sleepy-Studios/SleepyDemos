using System;
using System.Linq;
using Core.Editor.MvcBind;
using Core.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Module
{
    public sealed class MvcBindIndexDiscoveryTests
    {
        private const string TemporaryRoot = "Assets/__MvcBindIndexDiscoveryTests";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TemporaryRoot))
            {
                AssetDatabase.DeleteAsset(TemporaryRoot);
            }
        }

        [Test]
        public void Discover_ProjectIndex_UsesPrefabsAndFindsDroneFlightCustomDirectoryViews()
        {
            var result = MvcBindIndexDiscovery.Discover();

            Assert.That(result.ScriptScanPasses, Is.EqualTo(1));
            Assert.That(result.PrefabCandidateCount, Is.GreaterThanOrEqualTo(result.Records.Count));
            Assert.That(result.Records.All(record => record.hasPrefab), Is.True);

            var droneRecords = result.Records
                .Where(record => record.viewName.StartsWith("DroneFlight", StringComparison.Ordinal))
                .ToArray();
            Assert.That(droneRecords.Select(record => record.viewName), Is.EquivalentTo(new[]
            {
                "DroneFlightDebugView",
                "DroneFlightHudView",
                "DroneFlightVehicleSelectView"
            }));
            Assert.That(droneRecords.All(record => record.isValid), Is.True);
            Assert.That(droneRecords.All(record => record.moduleName == "DroneFlight"), Is.True);
        }

        [Test]
        public void Discover_PrefabWithoutScripts_GroupsRecordAsInvalidBinding()
        {
            CreateTemporaryIndexedPrefab("MissingGeneratedScriptsView");

            var records = MvcBindIndexDiscovery.BuildViewRecords(MvcBindToolConfig.ScriptRoot, TemporaryRoot);

            Assert.That(records, Has.Count.EqualTo(1));
            Assert.That(records[0].isValid, Is.False);
            Assert.That(records[0].moduleName, Is.EqualTo(MvcBindViewRecord.InvalidModuleName));
            StringAssert.Contains("缺少手写 View 脚本", records[0].validationMessage);
            StringAssert.Contains("缺少生成的 ViewComponent 脚本", records[0].validationMessage);
        }

        [Test]
        public void ApplyPrefabGenerationLocation_DroneFlightHud_UsesCustomDirectory()
        {
            var record = MvcBindIndexDiscovery.BuildViewRecords()
                .Single(item => item.viewName == "DroneFlightHudView");
            var settings = new MvcBindSettings();
            settings.ApplyPrefabPath(record.prefabPath);

            MvcBindWindow.ApplyPrefabGenerationLocation(settings, record);

            Assert.That(settings.moduleName, Is.EqualTo("DroneFlight"));
            Assert.That(settings.useCustomModuleOutputDirectory, Is.True);
            Assert.That(
                settings.customModuleOutputDirectory,
                Is.EqualTo("Assets/Scripts/Hotfix/Demos/DroneFlight/Adapters/UI"));
            Assert.That(
                settings.outputFolder,
                Is.EqualTo(
                    "Assets/Scripts/Hotfix/Demos/DroneFlight/Adapters/UI/DroneFlightHudView/View"));
        }

        [Test]
        public void ApplyPrefabGenerationLocation_DefaultModuleView_DoesNotEnableCustomDirectory()
        {
            var record = MvcBindIndexDiscovery.BuildViewRecords()
                .Single(item => item.viewName == "MainMenuView");
            var settings = new MvcBindSettings();
            settings.ApplyPrefabPath(record.prefabPath);

            MvcBindWindow.ApplyPrefabGenerationLocation(settings, record);

            Assert.That(settings.moduleName, Is.EqualTo("Main"));
            Assert.That(settings.useCustomModuleOutputDirectory, Is.False);
            Assert.That(
                settings.outputFolder,
                Is.EqualTo("Assets/Scripts/Hotfix/Module/Main/MainMenuView/View"));
        }

        private static void CreateTemporaryIndexedPrefab(string viewName)
        {
            AssetDatabase.CreateFolder("Assets", "__MvcBindIndexDiscoveryTests");
            var root = new GameObject(viewName, typeof(RectTransform), typeof(ComponentItemIndex));
            try
            {
                var index = root.GetComponent<ComponentItemIndex>();
                index.Components = Array.Empty<Component>();
                index.ComponentTypes = Array.Empty<string>();
                index.BindingKeys = Array.Empty<string>();
                index.BindingMethods = Array.Empty<string>();
                PrefabUtility.SaveAsPrefabAsset(root, $"{TemporaryRoot}/{viewName}.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
