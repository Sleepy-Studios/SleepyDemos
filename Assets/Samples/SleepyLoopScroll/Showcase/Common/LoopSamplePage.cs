using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SleepyStudios.LoopScroll.Samples
{
    /// <summary>示例共享层级、文本和导航。每次进入子场景建立新的示例会话。</summary>
    public abstract class LoopSamplePage : MonoBehaviour
    {
        [SerializeField] private LoopSampleCatalog catalog;
        [SerializeField] private Font font;
        private readonly Dictionary<Text, Func<string>> localized = new Dictionary<Text, Func<string>>();
        private readonly List<Button> navigationButtons = new List<Button>();
        private Text status;
        private string statusKey;
        private object[] statusArguments = Array.Empty<object>();
        public bool IsNavigating { get; private set; }
        public bool IsReady { get; private set; }
        public LoopSampleCatalog Catalog => catalog;
        protected abstract string TitleKey { get; }
        protected abstract void BuildPage();

        public void ConfigureAssets(LoopSampleCatalog scenes, Font sampleFont) { catalog = scenes; font = sampleFont; }
        protected virtual void Start()
        {
            if (catalog == null) catalog = Resources.Load<LoopSampleCatalog>("SleepyLoopScrollSamples/Catalog");
            if (font == null) font = Resources.Load<Font>("SleepyLoopScrollSamples/NotoSansSC-Regular");
            if (font == null) throw new InvalidOperationException("Showcase 字体缺失，请重新导入完整示例。");
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(960, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem)); SceneManager.MoveGameObjectToScene(events, gameObject.scene);
                var input = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (input != null) events.AddComponent(input); else events.AddComponent<StandaloneInputModule>();
            }
            var background = Node("Background", transform, new Vector2(960, 720), Vector2.zero);
            background.anchorMin = Vector2.zero; background.anchorMax = Vector2.one; background.sizeDelta = Vector2.zero;
            background.gameObject.AddComponent<Image>().color = new Color(.055f, .075f, .12f);
            LocalizedLabel(TitleKey, transform, new Vector2(650, 50), new Vector2(-35, 310), 27).alignment = TextAnchor.MiddleCenter;
            status = Label("", transform, new Vector2(850, 55), new Vector2(0, -315), 17);
            status.alignment = TextAnchor.MiddleCenter;
            if (TitleKey != "menu") NavigationButton("back", new Vector2(-405, 310), new Vector2(115, 42), () => NavigateTo("menu"));
            LoopSampleLanguage.Changed += UpdateLanguage;
            BuildPage(); UpdateLanguage(); IsReady = true;
        }
        protected virtual void OnDestroy() { LoopSampleLanguage.Changed -= UpdateLanguage; }
        protected virtual void UpdateDataLanguage() { }
        private void UpdateLanguage()
        {
            foreach (var pair in localized) if (pair.Key != null) pair.Key.text = pair.Value();
            UpdateStatus(); UpdateDataLanguage();
        }
        protected void SetStatus(string key, params object[] arguments)
        { statusKey = key; statusArguments = arguments; UpdateStatus(); }
        private void UpdateStatus() { if (status != null && statusKey != null) status.text = LoopSampleLanguage.Get(statusKey, statusArguments); }
        protected RectTransform Node(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); node.SetParent(parent, false);
            node.sizeDelta = size; node.anchoredPosition = position; return node;
        }
        protected Text Label(string text, Transform parent, Vector2 size, Vector2 position, int fontSize = 18)
        {
            var label = Node("Label", parent, size, position).gameObject.AddComponent<Text>(); label.font = font;
            label.fontSize = fontSize; label.text = text; label.color = new Color(.85f, .91f, 1);
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false; return label;
        }
        protected Text LocalizedLabel(string key, Transform parent, Vector2 size, Vector2 position, int fontSize = 18)
        {
            var label = Label(LoopSampleLanguage.Get(key), parent, size, position, fontSize);
            localized.Add(label, () => LoopSampleLanguage.Get(key)); return label;
        }
        protected Button ActionButton(string key, Vector2 position, Action action, Vector2? size = null)
        {
            var rect = Node(key, transform, size ?? new Vector2(190, 42), position);
            rect.gameObject.AddComponent<Image>().color = new Color(.16f, .3f, .48f);
            var button = rect.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => { if (!IsNavigating) action(); });
            var label = LocalizedLabel(key, rect, rect.sizeDelta - new Vector2(10, 2), Vector2.zero, 17); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        protected Button NavigationButton(string key, Vector2 position, Vector2 size, Action action)
        { var button = ActionButton(key, position, action, size); navigationButtons.Add(button); return button; }
        public void NavigateTo(string id)
        { if (!IsNavigating) StartCoroutine(Navigate(id)); }
        private IEnumerator Navigate(string id)
        {
            var entry = catalog != null ? catalog.Find(id) : null;
            if (entry == null) { SetStatus("missingScene", id); yield break; }
            AsyncOperation operation = null; string error = null;
            IsNavigating = true; SetNavigationEnabled(false);
            try
            {
                var path = catalog.ResolvePath(entry);
#if UNITY_EDITOR
                if (UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path) == null)
                    error = LoopSampleLanguage.Get("missingScene", path);
                else operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                    new LoadSceneParameters(LoadSceneMode.Single));
#else
                if (!Application.CanStreamedLevelBeLoaded(path)) error = LoopSampleLanguage.Get("missingBuild", path);
                else operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
#endif
            }
            catch (Exception exception) { error = exception.Message; }
            if (operation == null)
            { IsNavigating = false; SetNavigationEnabled(true); SetStatus("loadError", error ?? entry.scenePath); yield break; }
            while (!operation.isDone) yield return null;
            // Single 加载销毁来源会话，目标页面有自己独立的导航状态和事件订阅。
            IsNavigating = false; SetNavigationEnabled(true);
        }
        private void SetNavigationEnabled(bool enabled)
        { for (var i = 0; i < navigationButtons.Count; i++) if (navigationButtons[i] != null) navigationButtons[i].interactable = enabled; }
        protected LoopCell Template(Color color, float height = 48)
        {
            var rect = Node("CellTemplate", transform, new Vector2(800, height), Vector2.zero);
            rect.gameObject.AddComponent<Image>().color = color;
            var label = Label("", rect, new Vector2(770, height - 8), Vector2.zero);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(14, 4); label.rectTransform.offsetMax = new Vector2(-14, -4);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var cell = rect.gameObject.AddComponent<LoopCell>(); rect.gameObject.SetActive(false); return cell;
        }
        protected LoopScrollView MakeList(string name, Vector2 size, Vector2 position, LoopLayout mode, bool dynamic = false, LoopCell[] prefabs = null)
        {
            var root = Node(name, transform, size, position); root.gameObject.AddComponent<Image>().color = new Color(.09f, .13f, .2f);
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Node("Viewport", root, size, Vector2.zero); viewport.gameObject.AddComponent<Image>().color = new Color(.09f, .13f, .2f);
            viewport.gameObject.AddComponent<RectMask2D>(); var content = Node("Content", viewport, size, Vector2.zero);
            scroll.viewport = viewport; scroll.content = content; var view = root.gameObject.AddComponent<LoopScrollView>();
            if (prefabs == null) prefabs = new[] { Template(new Color(.12f, .21f, .32f)) };
            var entries = new LoopCellPrefab[prefabs.Length];
            for (var i = 0; i < entries.Length; i++) entries[i] = new LoopCellPrefab { Type = i, Prefab = prefabs[i], Prewarm = 32 };
            view.Configure(scroll, entries, mode, new Vector2(mode == LoopLayout.Horizontal ? 300 : 180, 52), dynamic); return view;
        }
        protected static void Bind(LoopCell cell, LoopSampleItem item, CellBindContext context)
        { cell.GetComponentInChildren<Text>(true).text = item.Display; }
    }
}
