using System;
using Hotfix.DroneFlight;
using Hotfix.DroneFlight.Adapters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.DroneFlight
{
    /// <summary>确定性创建可直接运行和迁移的无人机捕鱼 MVP 场景及贝塞尔路径 Prefab。</summary>
    internal static class DroneFishingMvpSceneBuilder
    {
        private const string DemoRoot = "Assets/LoadResources/Demos/drone_flight";
        private const string MissionPrefabFolder = DemoRoot + "/Prefabs/Mission";
        private const string MaterialFolder = DemoRoot + "/Art/Materials";
        private const string PathPrefabPath = MissionPrefabFolder + "/DroneFishingBezierRoute.prefab";
        private const string ScenePath = DemoRoot + "/Scenes/FishingBurstMvp.unity";
        private const string HarpoonPrefabPath = DemoRoot + "/Prefabs/DroneHarpoonVariant.prefab";
        private const string FishingConfigPath = DemoRoot + "/Data/DroneFishingMissionConfig.asset";

        [MenuItem("Tools/SleepyDemos/DroneFlight/Build Fishing MVP Scene")]
        public static void BuildAndOpen()
        {
            EnsureFolder(MissionPrefabFolder);
            var pathPrefab = BuildPathPrefab();
            var harpoonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HarpoonPrefabPath);
            if (harpoonPrefab == null)
            {
                throw new InvalidOperationException($"找不到渔叉无人机 Prefab：{HarpoonPrefabPath}");
            }

            var fishingConfig = AssetDatabase.LoadAssetAtPath<DroneFishingMissionConfig>(FishingConfigPath);
            if (fishingConfig == null)
            {
                throw new InvalidOperationException($"找不到捕鱼演出配置：{FishingConfigPath}");
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "FishingBurstMvp";
            BuildLighting();
            var camera = BuildCamera(out var cameraTracker);
            cameraTracker.Configure(fishingConfig);
            BuildEnvironment(out var fishBody, out var fishCollider);
            var path = PrefabUtility.InstantiatePrefab(pathPrefab) as GameObject;
            if (path == null)
            {
                throw new InvalidOperationException("无法实例化捕鱼贝塞尔路径 Prefab。");
            }

            path.name = "DroneFishingBezierRoute";
            var missionPath = path.GetComponent<DroneBezierMissionPath>();
            var endHoverPoint = new GameObject("EndHoverPoint").transform;
            endHoverPoint.position = camera.transform.TransformPoint(-3f, -1f, 6f);
            endHoverPoint.rotation = Quaternion.LookRotation(camera.transform.position - endHoverPoint.position, Vector3.up);

            BuildUi(
                out var qtePanel,
                out var launchButton,
                out var completedPanel,
                out var completedReplayButton,
                out var failedPanel,
                out var failedReplayButton);
            var coordinator = new GameObject("DroneFishingMission").AddComponent<DroneFishingMissionCoordinator>();
            coordinator.Configure(
                camera,
                cameraTracker,
                missionPath,
                harpoonPrefab,
                endHoverPoint,
                fishBody,
                fishCollider,
                qtePanel,
                launchButton,
                completedPanel,
                completedReplayButton,
                failedPanel,
                failedReplayButton);
            coordinator.Configure(fishingConfig);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException($"保存捕鱼 MVP 场景失败：{ScenePath}");
            }

            AssetDatabase.SetLabels(pathPrefab, new[] { "demo", "prefab" });
            AssetDatabase.SetLabels(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), new[] { "demo", "scene" });
            AssetDatabase.SaveAssets();
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Debug.Log($"[DroneFishingMvp] 已生成并打开场景：{ScenePath}");
        }

        private static GameObject BuildPathPrefab()
        {
            var root = new GameObject("DroneFishingBezierRoute");
            try
            {
                var path = root.AddComponent<DroneBezierMissionPath>();
                var entryRoot = CreateChild(root.transform, "Entry");
                var orbitRoot = CreateChild(root.transform, "Orbit");
                var diveRoot = CreateChild(root.transform, "Dive");

                var entryStart = CreatePoint(entryRoot, "Entry_Start", new Vector3(-12f, 3f, 0f));
                var entryHandle = CreatePoint(entryRoot, "Entry_HandleOut", new Vector3(-9f, 3f, -2f));
                var orbitEntryHandle = CreatePoint(entryRoot, "Entry_HandleIn", new Vector3(-6f, 3f, -4f));

                const float radius = 4f;
                var handle = radius * 0.5522848f;
                var orbit0 = CreatePoint(orbitRoot, "Orbit_Anchor_0", new Vector3(-radius, 3f, 0f));
                var orbit0Out = CreatePoint(orbitRoot, "Orbit_0_HandleOut", new Vector3(-radius, 3f, -handle));
                var orbit1In = CreatePoint(orbitRoot, "Orbit_1_HandleIn", new Vector3(-handle, 3f, -radius));
                var orbit1 = CreatePoint(orbitRoot, "Orbit_Anchor_1", new Vector3(0f, 3f, -radius));
                var orbit1Out = CreatePoint(orbitRoot, "Orbit_1_HandleOut", new Vector3(handle, 3f, -radius));
                var orbit2In = CreatePoint(orbitRoot, "Orbit_2_HandleIn", new Vector3(radius, 3f, -handle));
                var orbit2 = CreatePoint(orbitRoot, "Orbit_Anchor_2", new Vector3(radius, 3f, 0f));
                var orbit2Out = CreatePoint(orbitRoot, "Orbit_2_HandleOut", new Vector3(radius, 3f, handle));
                var orbit3In = CreatePoint(orbitRoot, "Orbit_3_HandleIn", new Vector3(handle, 3f, radius));
                var orbit3 = CreatePoint(orbitRoot, "Orbit_Anchor_3", new Vector3(0f, 3f, radius));
                var orbit3Out = CreatePoint(orbitRoot, "Orbit_3_HandleOut", new Vector3(-handle, 3f, radius));
                var orbit0In = CreatePoint(orbitRoot, "Orbit_0_HandleIn", new Vector3(-radius, 3f, handle));

                var diveHandleOut = CreatePoint(diveRoot, "Dive_HandleOut", new Vector3(-3f, 2.5f, 0f));
                var diveHandleIn = CreatePoint(diveRoot, "Dive_HandleIn", new Vector3(-1f, 1.5f, 0f));
                var diveEnd = CreatePoint(diveRoot, "Dive_End", new Vector3(0f, 1.5f, 0f));

                path.Configure(
                    new[] { CreateSegment(entryStart, entryHandle, orbitEntryHandle, orbit0) },
                    new[]
                    {
                        CreateSegment(orbit0, orbit0Out, orbit1In, orbit1),
                        CreateSegment(orbit1, orbit1Out, orbit2In, orbit2),
                        CreateSegment(orbit2, orbit2Out, orbit3In, orbit3),
                        CreateSegment(orbit3, orbit3Out, orbit0In, orbit0)
                    },
                    new[] { CreateSegment(orbit0, diveHandleOut, diveHandleIn, diveEnd) });
                return PrefabUtility.SaveAsPrefabAsset(root, PathPrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static DroneBezierSegment CreateSegment(
            Transform start,
            Transform startHandle,
            Transform endHandle,
            Transform end)
        {
            var segment = new DroneBezierSegment();
            segment.Configure(start, startHandle, endHandle, end);
            return segment;
        }

        private static void BuildLighting()
        {
            var light = new GameObject("Directional Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.38f, 0.48f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.18f, 0.24f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.08f, 0.1f);
        }

        private static Camera BuildCamera(out DroneCinematicCameraTracker tracker)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 7f, -14f);
            cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.zero - cameraObject.transform.position, Vector3.up);
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 200f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.07f, 0.11f);
            tracker = cameraObject.AddComponent<DroneCinematicCameraTracker>();
            tracker.CaptureInitialPose();
            return camera;
        }

        private static void BuildEnvironment(out Rigidbody fishBody, out Collider fishCollider)
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "WaterSurface_Y0";
            water.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            water.transform.localScale = new Vector3(1.2f, 1f, 1.2f);
            Object.DestroyImmediate(water.GetComponent<Collider>());
            water.GetComponent<Renderer>().sharedMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/FishingWaterMvp.mat",
                new Color(0.05f, 0.48f, 0.72f, 0.32f),
                true);

            var fish = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fish.name = "FishPayloadCube_YMinus5";
            fish.transform.position = new Vector3(0f, -5f, 0f);
            fish.transform.localScale = new Vector3(1.2f, 0.55f, 0.5f);
            fish.GetComponent<Renderer>().sharedMaterial = CreateOrUpdateMaterial(
                MaterialFolder + "/FishingTargetMvp.mat",
                new Color(1f, 0.38f, 0.08f, 1f),
                false);
            fishCollider = fish.GetComponent<Collider>();
            fishBody = fish.AddComponent<Rigidbody>();
            fishBody.mass = 0.35f;
            fishBody.useGravity = false;
            fishBody.isKinematic = true;
            fishBody.constraints = RigidbodyConstraints.FreezeAll;
        }

        private static Material CreateOrUpdateMaterial(string path, Color color, bool transparent)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(transparent
                    ? "Universal Render Pipeline/Unlit"
                    : "Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }

            else
            {
                material.shader = Shader.Find(transparent
                    ? "Universal Render Pipeline/Unlit"
                    : "Universal Render Pipeline/Lit") ?? material.shader;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = 3000;
            }
            else
            {
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_ZWrite", 1f);
                material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = -1;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildUi(
            out GameObject qtePanel,
            out Button launchButton,
            out GameObject completedPanel,
            out Button completedReplayButton,
            out GameObject failedPanel,
            out Button failedReplayButton)
        {
            var canvasObject = new GameObject(
                "FishingMvpCanvas",
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            qtePanel = CreatePanel(canvasObject.transform, "QteSuccessPop", new Color(0.03f, 0.08f, 0.12f, 0.92f));
            CreateText(qtePanel.transform, "QteTitle", "无人机上鱼演出", new Vector2(0f, 70f), 34);
            launchButton = CreateButton(qtePanel.transform, "LaunchDroneButton", "无人机飞出", new Vector2(0f, -40f));

            completedPanel = CreatePanel(canvasObject.transform, "MissionCompletedPop", new Color(0.03f, 0.14f, 0.08f, 0.94f));
            CreateText(completedPanel.transform, "CompletedTitle", "捕鱼流程完成", new Vector2(0f, 70f), 36);
            completedReplayButton = CreateButton(completedPanel.transform, "CompletedReplayButton", "重新播放", new Vector2(0f, -40f));
            completedPanel.SetActive(false);

            failedPanel = CreatePanel(canvasObject.transform, "MissionFailedPop", new Color(0.2f, 0.04f, 0.04f, 0.94f));
            CreateText(failedPanel.transform, "FailedTitle", "流程未完成，请检查 Console", new Vector2(0f, 70f), 32);
            failedReplayButton = CreateButton(failedPanel.transform, "FailedReplayButton", "重新播放", new Vector2(0f, -40f));
            failedPanel.SetActive(false);

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem));
            var inputModuleType = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputModuleType != null)
            {
                eventSystem.AddComponent(inputModuleType);
            }
            else
            {
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }

        private static GameObject CreatePanel(Transform parent, string name, Color color)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(620f, 280f);
            panel.GetComponent<Image>().color = color;
            return panel;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 position)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(260f, 72f);
            buttonObject.GetComponent<Image>().color = new Color(0.12f, 0.55f, 0.85f, 1f);
            CreateText(buttonObject.transform, "Label", label, Vector2.zero, 28, true);
            return buttonObject.GetComponent<Button>();
        }

        private static void CreateText(
            Transform parent,
            string name,
            string content,
            Vector2 position,
            int fontSize,
            bool stretch = false)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            if (stretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            else
            {
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = position;
                rect.sizeDelta = new Vector2(560f, 70f);
            }

            var text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(parent, false);
            return child;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 localPosition)
        {
            var point = CreateChild(parent, name);
            point.localPosition = localPosition;
            return point;
        }

        private static void EnsureFolder(string folder)
        {
            var current = "Assets";
            foreach (var segment in folder.Substring("Assets/".Length).Split('/'))
            {
                var next = $"{current}/{segment}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segment);
                }

                current = next;
            }
        }
    }
}
