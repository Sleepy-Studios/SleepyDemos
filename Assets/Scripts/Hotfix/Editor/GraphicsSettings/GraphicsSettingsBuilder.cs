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

namespace Hotfix.Editor.GraphicsSettings
{
    public static class GraphicsSettingsBuilder
    {
        internal const string PanelPath = "Assets/LoadResources/UI/GraphicsSettings/DlssSettingsView.prefab";
        private const string MainMenuPath = "Assets/LoadResources/UI/Hall/MainMenuView.prefab";
        [MenuItem("Tools/SleepyDemos/DLSS/生成设置面板")]
        public static void BuildPanel()
        {
            EnsureFolder("Assets/LoadResources/UI/GraphicsSettings");
            TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<GameObject>(MainMenuPath)?.GetComponentInChildren<TextMeshProUGUI>(true)?.font;
            if (font == null) font = TMP_Settings.defaultFontAsset;
            var root = new GameObject("DlssSettingsView", typeof(RectTransform));
            SetRect(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            try
            {
                var panel = Rect(root.transform, "SettingsPanel", new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(350, 740));
                panel.pivot = new Vector2(1, 0.5f);
                panel.gameObject.AddComponent<Image>().color = new Color(0.025f, 0.045f, 0.075f, 0.97f);
                PanelLabel(panel, font, "PanelTitle", "画质设置", 30, 28, 48);
                PanelLabel(panel, font, "DeviceText", "正在读取设备…", 15, 82, 60);
                PanelLabel(panel, font, "ModeText", "当前：关闭", 18, 138, 54);
                string[] names = { "OffButton", "QualityButton", "BalancedButton", "PerformanceButton", "UltraPerformanceButton", "DlaaButton" };
                string[] labels = { "关闭 · 原生分辨率", "Quality · 质量", "Balanced · 均衡", "Performance · 性能", "Ultra Performance · 超高性能", "DLAA · 原生抗锯齿" };
                for (int index = 0; index < names.Length; ++index)
                    Button(panel, font, names[index], labels[index], 198 + index * 52, 44);
                PanelLabel(panel, font, "ResolutionText", "输入 —  →  输出 —", 17, 520, 48);
                PanelLabel(panel, font, "StatusText", "准备场景…", 16, 575, 70);
                var opener = Button(root.transform, font, "OpenButton", "画质设置", 24, 40);
                SetRect(opener.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-224, -24), new Vector2(200, 42));
                Button(panel, font, "CloseButton", "关闭设置", 692, 34);
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
                    prefabPath = PanelPath, moduleName = "GraphicsSettings", viewName = "DlssSettingsView", namespaceName = "Hotfix",
                    address = MvcBindPathUtility.ToRuntimeAddress(PanelPath), viewType = ViewType.View,
                    layer = UILayer.Decorate, viewMode = UIViewMode.Widget, mask = MaskType.None,
                    isHotfix = true, isAsync = true, enableOnInit = true, destroyOnHide = true,
                    uiTransitionType = typeof(EmptyUITransition).FullName,
                    useCustomModuleOutputDirectory = true,
                    customModuleOutputDirectory = "Assets/Scripts/Hotfix/Module/GraphicsSettings"
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

        private static UnityEngine.UI.Button Button(Transform panel, TMP_FontAsset font, string name, string value, float top, float height)
        {
            var rect = Rect(panel, name, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -top), new Vector2(302, height));
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.09f, 0.15f, 0.23f, 1);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var text = Label(rect, font, "Label", value, 18);
            SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-28, 0));
            return rect.GetComponent<UnityEngine.UI.Button>();
        }
    }
}
