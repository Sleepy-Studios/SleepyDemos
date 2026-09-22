using System;
using System.IO;
using Core.Editor.MvcBind;
using Core.Runtime;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.Dlss
{
    /// DLSS 体验场景与设置面板的可重复装配入口。
    public static class DlssDemoBuilder
    {
        internal const string Root = "Assets/LoadResources/Demos/dlss";
        internal const string PanelPath = Root + "/Prefabs/DlssSettingsView.prefab";
        private const string MainMenuPath = "Assets/LoadResources/UI/Hall/MainMenuView.prefab";

        [MenuItem("Tools/SleepyDemos/DLSS/配置Windows图形后端")]
        public static void ConfigureWindowsGraphics()
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,
                new[] { GraphicsDeviceType.Direct3D12, GraphicsDeviceType.Vulkan });
            AssetDatabase.SaveAssets();
            Debug.Log("[DLSS Demo] Windows 默认使用 D3D12，保留 Vulkan。当前 Editor 后端在重启后生效。");
        }

        [MenuItem("Tools/SleepyDemos/DLSS/生成体验场景与Hub入口")]
        public static void BuildSceneAndHub()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请在非 Play 模式装配场景。");
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Data");
            EnsureFolder(Root + "/Art/Materials");
            var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (source == null) throw new InvalidOperationException("当前项目不是 URP。");
            var sourceData = new SerializedObject(source).FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue as ScriptableRendererData;
            const string rendererPath = Root + "/Data/DlssRenderer.asset";
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (renderer == null)
            {
                renderer = Object.Instantiate(sourceData);
                renderer.name = "DlssRenderer";
                renderer.rendererFeatures.Clear();
                AssetDatabase.CreateAsset(renderer, rendererPath);
                var capture = ScriptableObject.CreateInstance<Core.Runtime.Rendering.Streamline.StreamlineCaptureFeature>();
                capture.name = "Streamline capture";
                AssetDatabase.AddObjectToAsset(capture, renderer);
                renderer.rendererFeatures.Add(capture);
                renderer.SetDirty();
            }
            const string pipelinePath = Root + "/Data/DlssPipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = Object.Instantiate(source);
                pipeline.name = "DlssPipeline";
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            var pipelineData = new SerializedObject(pipeline);
            var renderers = pipelineData.FindProperty("m_RendererDataList");
            renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            pipelineData.FindProperty("m_DefaultRendererIndex").intValue = 0;
            pipelineData.ApplyModifiedPropertiesWithoutUndo();
            pipeline.renderScale = 1;
            pipeline.msaaSampleCount = 1;
            pipeline.upscalingFilter = UpscalingFilterSelection.Linear;
            pipeline.supportsHDR = true;
            pipeline.supportsCameraDepthTexture = true;
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(renderer);

            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var main = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                main.tag = "MainCamera"; main.cullingMask = 0; main.depth = 0;
                main.clearFlags = CameraClearFlags.SolidColor; main.backgroundColor = Color.black;
                main.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var world = new GameObject("WorldCamera", typeof(Camera)).GetComponent<Camera>();
                world.enabled = false; world.depth = -20; world.cullingMask = 1 << 30;
                world.clearFlags = CameraClearFlags.SolidColor; world.backgroundColor = new Color(0.035f, 0.055f, 0.085f);
                world.allowHDR = true; world.nearClipPlane = 0.1f; world.farClipPlane = 150; world.fieldOfView = 60;
                world.transform.SetPositionAndRotation(new Vector3(0, 2.5f, -8), Quaternion.LookRotation(new Vector3(0, -1.1f, 12)));
                main.transform.SetPositionAndRotation(world.transform.position, world.transform.rotation);
                var worldData = world.GetUniversalAdditionalCameraData();
                worldData.volumeLayerMask = 0;
                worldData.requiresDepthTexture = true;
                var light = new GameObject("Sun", typeof(Light)).GetComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.6f; light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(45, -28, 0);
                RenderSettings.ambientMode = AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.3f, 0.35f, 0.42f);
                RenderSettings.skybox = null;
                var dark = Material("FloorDark", new Color(0.11f, 0.17f, 0.22f));
                var pale = Material("FloorLight", new Color(0.45f, 0.52f, 0.58f));
                Primitive("Ground", PrimitiveType.Plane, new Vector3(0, -0.04f, 5), new Vector3(3, 1, 3), dark);
                for (int z = 0; z < 12; ++z)
                for (int x = 0; x < 12; ++x)
                    Primitive("Tile_" + x + "_" + z, PrimitiveType.Cube, new Vector3(x - 5.5f, -0.01f, z), new Vector3(0.96f, 0.02f, 0.96f), ((x + z) & 1) == 0 ? pale : dark);
                var moving = Primitive("MovingCube", PrimitiveType.Cube, new Vector3(-1.6f, 0.8f, 3), new Vector3(1.1f, 1.6f, 1.1f), Material("Copper", new Color(0.85f, 0.24f, 0.08f)));
                var spinner = Primitive("RotatingCube", PrimitiveType.Cube, new Vector3(1.2f, 1.2f, 5), Vector3.one * 1.5f, Material("Teal", new Color(0.05f, 0.65f, 0.5f)));
                var lineMaterial = Material("FineLines", new Color(0.95f, 0.8f, 0.3f));
                for (int index = 0; index < 30; ++index)
                    Primitive("FineLine_" + index, PrimitiveType.Cube, new Vector3(-4.5f + index * 0.3f, 1.5f, 9), new Vector3(0.025f, 3, 0.025f), lineMaterial);
                var glass = Material("Glass", new Color(0.08f, 0.65f, 0.9f, 0.38f));
                glass.SetFloat("_Surface", 1); glass.SetFloat("_ZWrite", 0);
                glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                glass.SetFloat("_Cull", (float)CullMode.Off); glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                glass.renderQueue = (int)RenderQueue.Transparent;
                EditorUtility.SetDirty(glass);
                Primitive("GlassPanel", PrimitiveType.Quad, new Vector3(3, 1.5f, 4), new Vector3(2, 2.5f, 1), glass);
                var controller = new GameObject("DlssDemoController").AddComponent<Hotfix.Dlss.DlssDemoController>();
                controller.Configure(world, pipeline, moving.transform, spinner.transform);
                EditorSceneManager.SaveScene(scene, Root + "/Scenes/Main.unity");
            }
            finally
            {
                SceneManager.SetActiveScene(previous);
                EditorSceneManager.CloseScene(scene, true);
            }
            AddHubButton();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DLSS Demo] 体验场景与 Hub 按钮已生成。请从 AppEntrance 启动。");
        }

        private static Material Material(string name, Color color)
        {
            string path = Root + "/Art/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.SetColor("_BaseColor", color); material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name; item.layer = 30;
            item.transform.position = position; item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            var collider = item.GetComponent<Collider>(); if (collider != null) Object.DestroyImmediate(collider);
            return item;
        }

        private static void AddHubButton()
        {
            var root = PrefabUtility.LoadPrefabContents(MainMenuPath);
            try
            {
                Button template = null, target = null;
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    if (button.name == "DroneFlightButton") template = button;
                    if (button.name == "DlssButton") target = button;
                }
                if (template == null) throw new InvalidOperationException("Hub 缺少现有 Demo 按钮模板。");
                if (target == null)
                {
                    target = Object.Instantiate(template, template.transform.parent);
                    target.name = "DlssButton";
                    target.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
                    target.GetComponent<RectTransform>().anchoredPosition = template.GetComponent<RectTransform>().anchoredPosition + Vector2.down * 90;
                }
                var tmpLabel = target.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmpLabel != null) tmpLabel.text = "DLSS 实验室";
                else
                {
                    var legacyLabel = target.GetComponentInChildren<Text>(true);
                    if (legacyLabel == null) throw new InvalidOperationException("Hub 按钮模板没有文本组件。");
                    legacyLabel.text = "DLSS 实验室";
                }
                var nodes = MvcPrefabScanner.Scan(root);
                MvcBindComponentWindowBridge.RestoreComponentChoices(root, nodes);
                foreach (var node in nodes) if (node.name == "DlssButton") Select(node, typeof(Button), "OnDlssButtonClick");
                var settings = new MvcBindSettings
                {
                    prefabPath = MainMenuPath, moduleName = "Main", viewName = "MainMenuView", namespaceName = "Hotfix",
                    address = MvcBindPathUtility.ToRuntimeAddress(MainMenuPath), viewType = ViewType.View,
                    layer = UILayer.Base, viewMode = UIViewMode.Page, mask = MaskType.None,
                    isHotfix = true, isAsync = true, enableOnInit = true, destroyOnHide = true
                };
                settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings);
                if (!MvcBindComponentWindowBridge.GenerateAndBind(root, settings, nodes, false, out _, out string message)) throw new InvalidOperationException(message);
                PrefabUtility.SaveAsPrefabAsset(root, MainMenuPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        [MenuItem("Tools/SleepyDemos/DLSS/生成设置面板")]
        public static void BuildPanel()
        {
            EnsureFolder(Root + "/Prefabs");
            EnsureFolder(Root + "/Art/Materials");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPath)?.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
            if (font == null) font = TMP_Settings.defaultFontAsset;
            var root = new GameObject("DlssSettingsView", typeof(RectTransform));
            SetRect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            try
            {
                var display = Rect(root.transform, "WorldImage", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var raw = display.gameObject.AddComponent<RawImage>();
                raw.raycastTarget = false;
                var shader = Shader.Find("SleepyDemos/DlssPresentation");
                if (shader != null)
                {
                    const string materialPath = Root + "/Art/Materials/Presentation.mat";
                    var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
                    raw.material = material;
                }
                var heading = Label(root.transform, font, "Heading", "DLSS 实验室", 36);
                SetRect(heading.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(36, -32), new Vector2(500, 56));
                var help = Label(root.transform, font, "Help", "右键拖动视角 · WASD 移动 · R 重置", 18);
                SetRect(help.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(36, 30), new Vector2(700, 40));
                help.rectTransform.pivot = Vector2.zero;
                var panel = Rect(root.transform, "SettingsPanel", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(350, 740));
                panel.pivot = new Vector2(1, 0.5f);
                panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.075f, 0.97f);
                PanelLabel(panel, font, "PanelTitle", "画质设置", 30, 28, 48);
                PanelLabel(panel, font, "DeviceText", "正在读取设备…", 15, 82, 60);
                PanelLabel(panel, font, "ModeText", "当前：关闭", 22, 150, 34);
                string[] names = { "OffButton", "QualityButton", "BalancedButton", "PerformanceButton", "UltraPerformanceButton", "DlaaButton" };
                string[] labels = { "关闭 · 原生分辨率", "Quality · 质量", "Balanced · 均衡", "Performance · 性能", "Ultra Performance · 超高性能", "DLAA · 原生抗锯齿" };
                for (int index = 0; index < names.Length; ++index)
                    Button(panel, font, names[index], labels[index], 198 + index * 52, 44);
                PanelLabel(panel, font, "ResolutionText", "输入 —  →  输出 —", 17, 520, 48);
                PanelLabel(panel, font, "StatusText", "准备场景…", 16, 575, 70);
                Button(panel, font, "ResetCameraButton", "重置视角", 650, 34);
                Button(panel, font, "BackButton", "返回 Hub", 692, 34);
                PrefabUtility.SaveAsPrefabAsset(root, PanelPath);
            }
            finally { Object.DestroyImmediate(root); }
            var contents = PrefabUtility.LoadPrefabContents(PanelPath);
            try
            {
                var nodes = MvcPrefabScanner.Scan(contents);
                foreach (var node in nodes)
                {
                    if (node.componentTypes.Contains(typeof(Button))) Select(node, typeof(Button), "On" + node.name + "Click");
                    else if (node.name == "WorldImage") Select(node, typeof(RawImage));
                    else if (node.name == "DeviceText" || node.name == "ModeText" || node.name == "ResolutionText" || node.name == "StatusText") Select(node, typeof(TextMeshProUGUI));
                }
                var settings = new MvcBindSettings
                {
                    prefabPath = PanelPath, moduleName = "Dlss", viewName = "DlssSettingsView", namespaceName = "Hotfix",
                    address = MvcBindPathUtility.ToRuntimeAddress(PanelPath), viewType = ViewType.View,
                    layer = UILayer.Base, viewMode = UIViewMode.Page, mask = MaskType.None,
                    isHotfix = true, isAsync = true, enableOnInit = true, destroyOnHide = true,
                    uiTransitionType = typeof(EmptyUITransition).FullName,
                    useCustomModuleOutputDirectory = true,
                    customModuleOutputDirectory = "Assets/Scripts/Hotfix/Demos/Dlss/Adapters/UI"
                };
                settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings);
                if (!MvcBindComponentWindowBridge.GenerateAndBind(contents, settings, nodes, false, out _, out string message))
                    throw new InvalidOperationException(message);
                PrefabUtility.SaveAsPrefabAsset(contents, PanelPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[DLSS Demo] 设置面板与 MvcBind 绑定已生成。");
        }

        private static void Select(MvcBindNode node, Type type, string callback = null)
        {
            node.selectedComponentType = type;
            node.selectedComponentTypeName = type.FullName;
            node.selectedComponentTypes.Clear();
            node.selectedMethodNames.Clear();
            node.selectedMethodNamesByComponentTypeName.Clear();
            if (callback != null) node.selectedMethodNames.Add(callback);
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            SetRect(rect, min, max, position, size);
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }

        private static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string name, string value, float size)
        {
            var rect = Rect(parent, name, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(300, 40));
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font; text.text = value; text.fontSize = size;
            text.color = new Color(0.9f, 0.94f, 1); text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        private static void PanelLabel(Transform panel, TMP_FontAsset font, string name, string value, float size, float top, float height)
        {
            var text = Label(panel, font, name, value, size);
            SetRect(text.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -top), new Vector2(302, height));
        }

        private static void Button(Transform panel, TMP_FontAsset font, string name, string value, float top, float height)
        {
            var rect = Rect(panel, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -top), new Vector2(302, height));
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.09f, 0.15f, 0.23f, 1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var text = Label(rect, font, "Label", value, 18);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-28, 0));
        }
    }
}
