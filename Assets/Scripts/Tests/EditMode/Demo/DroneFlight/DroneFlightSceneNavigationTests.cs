using System;
using System.IO;
using System.Linq;
using Core.Runtime;
using Hotfix.DroneFlight;
using Hotfix.DroneFlight.Adapters.SleepyDemos;
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
        private const string ArenaPrefabPath =
            "Assets/LoadResources/Demos/drone_flight/Prefabs/Environment/DroneFlightArena.prefab";

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

        [Test]
        public void IndustrialArena_UsesExactGroundAndBoundaryDimensions()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            var ground = RequireChild(prefab.transform, "Ground/GroundSurface").GetComponent<BoxCollider>();
            Assert.That(ground, Is.Not.Null);
            Assert.That(ground.size.x, Is.EqualTo(100f).Within(0.001f));
            Assert.That(ground.size.z, Is.EqualTo(100f).Within(0.001f));
            Assert.That(ground.transform.localPosition.y + ground.center.y + ground.size.y * 0.5f,
                Is.EqualTo(0f).Within(0.001f));

            var north = RequireChild(prefab.transform, "Boundary/NorthWall").GetComponent<BoxCollider>();
            var south = RequireChild(prefab.transform, "Boundary/SouthWall").GetComponent<BoxCollider>();
            var east = RequireChild(prefab.transform, "Boundary/EastWall").GetComponent<BoxCollider>();
            var west = RequireChild(prefab.transform, "Boundary/WestWall").GetComponent<BoxCollider>();

            Assert.That(new[] { north, south, east, west }, Has.All.Not.Null);
            Assert.That(new[] { north.size.y, south.size.y, east.size.y, west.size.y },
                Has.All.EqualTo(10f).Within(0.001f));
            Assert.That(north.transform.localPosition.z + north.center.z - north.size.z * 0.5f,
                Is.EqualTo(50f).Within(0.001f));
            Assert.That(south.transform.localPosition.z + south.center.z + south.size.z * 0.5f,
                Is.EqualTo(-50f).Within(0.001f));
            Assert.That(east.transform.localPosition.x + east.center.x - east.size.x * 0.5f,
                Is.EqualTo(50f).Within(0.001f));
            Assert.That(west.transform.localPosition.x + west.center.x + west.size.x * 0.5f,
                Is.EqualTo(-50f).Within(0.001f));
        }

        [Test]
        public void IndustrialArena_IsBakedAndContainsSixCourseGroups()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
            Assert.That(prefab, Is.Not.Null);

            var course = RequireChild(prefab.transform, "Course");
            for (var index = 1; index <= 6; index++)
            {
                Assert.That(course.Cast<Transform>().Any(child => child.name.StartsWith($"{index:00}_")),
                    Is.True,
                    $"训练路线缺少第 {index} 组障碍。");
            }

            Assert.That(prefab.GetComponentsInChildren<MeshCollider>(true), Is.Empty,
                "静态训练场使用独立 BoxCollider，不应让视觉 Mesh 承担碰撞。");
            Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true), Is.Empty,
                "烘焙后的训练场不得残留 ProBuilder 或其它运行时脚本组件。");

            var dependencies = AssetDatabase.GetDependencies(
                new[] { GameSceneCatalog.DroneFlightAddress, ArenaPrefabPath },
                true);
            Assert.That(dependencies, Has.None.Contains("com.unity.probuilder").IgnoreCase,
                "场景和训练场 Prefab 的递归依赖不得包含 ProBuilder 包资源。");
        }

        [Test]
        public void DroneDemoScene_PreservesCentralGameplayMarkers()
        {
            var scene = EditorSceneManager.OpenScene(
                GameSceneCatalog.DroneFlightAddress,
                OpenSceneMode.Additive);
            try
            {
                var transforms = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .ToArray();
                var spawnPoint = transforms.Single(transform => transform.name == "SpawnPoint");
                var dropZone = transforms.Single(transform => transform.name == "PayloadDropZone");

                Assert.That(spawnPoint.position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(spawnPoint.position.z, Is.EqualTo(0f).Within(0.001f));
                Assert.That(dropZone.position.x, Is.EqualTo(0f).Within(0.001f));
                Assert.That(dropZone.position.z, Is.EqualTo(4f).Within(0.001f));
                Assert.That(transforms.Count(transform => transform.name == "DroneFlightArena"), Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Transform RequireChild(Transform root, string path)
        {
            var child = root.Find(path);
            Assert.That(child, Is.Not.Null, $"训练场 Prefab 缺少节点：{path}");
            return child;
        }
    }
}
