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
    /// DLSS 体验场景的可重复装配入口。
    public static class DlssDemoBuilder
    {
        internal const string Root = "Assets/LoadResources/Demos/dlss";
        private const string MainMenuPath = "Assets/LoadResources/UI/Hall/MainMenuView.prefab";

        [MenuItem("Tools/SleepyDemos/DLSS/生成体验场景与Hub入口")]
        public static void BuildSceneAndHub()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请在非 Play 模式装配场景。");
            EnsureFolder(Root + "/Scenes");
            EnsureFolder(Root + "/Art/Materials");
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var main = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                main.tag = "MainCamera"; main.cullingMask = 0; main.depth = 0;
                main.clearFlags = CameraClearFlags.SolidColor; main.backgroundColor = Color.black;
                main.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                var world = main;
                world.enabled = true; world.cullingMask = 1 << 30;
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
                controller.Configure(world, moving.transform, spinner.transform);
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

    }
}
