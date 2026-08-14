using System.IO;
using System.Linq;
using Core.Runtime;
using Hotfix.DroneFlight;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tests.Demo
{
    /*
     * 测试说明：验证 DroneFlight 场景收集、Hub 入口、编辑器直启宿主、相机与 UI Loading 契约，防止场景接入链路退化。
     */
    public sealed class DroneFlightSceneNavigationTests
    {
        [Test]
        public void BuildSettings_OnlyContainsHubBootstrapScene()
        {
            var enabledScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            CollectionAssert.AreEqual(
                new[] { "Assets/Scenes/AppEntrance.unity" },
                enabledScenes);
        }

        [Test]
        public void DroneDemoScene_IsCollectedByDemosCollector()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null);
            var collectorSettings = File.ReadAllText(
                Path.Combine(projectRoot, "Assets/Settings/AssetBundleCollectorSetting.asset"));

            StringAssert.Contains("GroupName: Demos", collectorSettings);
            StringAssert.Contains("CollectPath: Assets/LoadResources/Demos", collectorSettings);
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(GameSceneCatalog.DroneFlightAddress),
                Is.Not.Null);
        }

        [Test]
        public void MainMenuPrefab_UsesGeneratedButtonCallbackWithoutLegacyLauncher()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/LoadResources/UI/Hall/MainMenuView.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Has.None.Matches<MonoBehaviour>(
                component => component.GetType().Name == "DroneFlightDemoLauncher"));
            var index = prefab.GetComponent<ComponentItemIndex>();
            Assert.That(index, Is.Not.Null);
            Assert.That(index.BindingMethods, Has.Some.Contains("OnDroneFlightButtonClick"));
        }

        [Test]
        public void CommonLoadingPrefab_ContainsGeneratedBindingContract()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/LoadResources/UI/Common/CommonLoading.prefab");

            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>(), Is.Null);
            var index = prefab.GetComponent<ComponentItemIndex>();
            Assert.That(index, Is.Not.Null);
            Assert.That(index.Components, Has.Length.EqualTo(7));
            Assert.That(index.ComponentTypes.Count(type => type == "TMPro.TextMeshProUGUI"), Is.EqualTo(5));
            Assert.That(index.ComponentTypes.Count(type => type == "UnityEngine.UI.Image"), Is.EqualTo(2));
        }

        [Test]
        public void DroneDemoScene_ContainsSingleMainCameraAndAudioListener()
        {
            var scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.DroneFlightAddress,
                OpenSceneMode.Additive);
            try
            {
                var cameras = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                    .Where(camera => camera.CompareTag("MainCamera"))
                    .ToArray();
                var listeners = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<AudioListener>(true))
                    .ToArray();

                Assert.That(cameras, Has.Length.EqualTo(1));
                Assert.That(listeners, Has.Length.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void DroneDemoScene_UsesStandaloneEditorBootstrapAndDoesNotPreplaceDrone()
        {
            var scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.DroneFlightAddress,
                OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var bootstraps = roots
                    .SelectMany(root => root.GetComponentsInChildren<DemoIslandEditorBootstrap>(true))
                    .ToArray();
                var coordinators = roots
                    .SelectMany(root => root.GetComponentsInChildren<DroneFlightSceneCoordinator>(true))
                    .ToArray();

                Assert.That(bootstraps, Has.Length.EqualTo(1));
                Assert.That(bootstraps[0].transform.parent, Is.Null,
                    "直启宿主必须独立为根对象，避免场景重载时把玩法协调器一起设为常驻。");
                Assert.That(coordinators, Has.Length.EqualTo(1));
                Assert.That(roots.SelectMany(root => root.GetComponentsInChildren<DroneFlightController>(true)),
                    Is.Empty,
                    "机型选择完成前不得预放活动无人机。");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
