using System;
using System.Collections;
using System.Collections.Generic;
using SleepyStudios.LoopScroll;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SleepyStudios.LoopScroll.Samples.Basic
{
    public sealed class BasicLoopSample : MonoBehaviour
    {
        private readonly List<string> items = new List<string>();
        private LoopScrollView list;
        private Canvas canvas;
        private Text status;
        private int serial = 1000;
        private void Start()
        {
            canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(960, 720); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            if (FindObjectOfType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem)); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(events, gameObject.scene);
                var inputModule = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
                if (inputModule != null) events.AddComponent(inputModule); else events.AddComponent<StandaloneInputModule>();
            }
            var background = Node("Background", transform, new Vector2(960, 720), Vector2.zero); background.gameObject.AddComponent<Image>().color = new Color(.055f, .075f, .12f);
            Label("Sleepy Loop Scroll  /  BASIC LISTS", transform, new Vector2(880, 50), new Vector2(0, 310), 27);
            status = Label("100,000 entries / vertical, horizontal and fixed grid", transform, new Vector2(880, 40), new Vector2(0, -315), 17);
            for (var i = 0; i < 100000; i++) items.Add("Entry " + i);
            var vertical = MakeList("Vertical", new Vector2(840, 480), new Vector2(0, -10), LoopLayout.Vertical);
            var horizontal = MakeList("Horizontal", new Vector2(840, 480), new Vector2(0, -10), LoopLayout.Horizontal);
            var grid = MakeList("Grid", new Vector2(840, 480), new Vector2(0, -10), LoopLayout.VerticalGrid);
            vertical.SetData<string, LoopCell>(items, Bind, null, item => item); horizontal.SetData<string, LoopCell>(items, Bind, null, item => item); grid.SetData<string, LoopCell>(items, Bind, null, item => item);
            horizontal.gameObject.SetActive(false); grid.gameObject.SetActive(false); list = vertical;
            Action<LoopScrollView> choose = view => { vertical.gameObject.SetActive(view == vertical); horizontal.gameObject.SetActive(view == horizontal); grid.gameObject.SetActive(view == grid); list = view; };
            Button("Vertical", new Vector2(-310, 250), () => choose(vertical)); Button("Horizontal", new Vector2(-105, 250), () => choose(horizontal)); Button("Grid", new Vector2(100, 250), () => choose(grid)); Button("Go to 50,000", new Vector2(305, 250), () => list.ScrollTo(50000, ScrollAlignment.Center, new ScrollAnimation(.25f)));
        }
        private static RectTransform Node(string name, Transform parent, Vector2 size, Vector2 position)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        private static Font FontAsset()
        {
return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        private static Text Label(string text, Transform parent, Vector2 size, Vector2 position, int fontSize = 18)
        {
            var label = Node("Label", parent, size, position).gameObject.AddComponent<Text>(); label.font = FontAsset(); label.fontSize = fontSize; label.text = text; label.color = new Color(.85f, .91f, 1); label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false; return label;
        }
        private void Button(string title, Vector2 position, Action action)
        {
            var rect = Node(title, transform, new Vector2(180, 42), position); rect.gameObject.AddComponent<Image>().color = new Color(.16f, .3f, .48f);
            var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(() => action());
            var text = Label(title, rect, new Vector2(170, 40), Vector2.zero, 17); text.alignment = TextAnchor.MiddleCenter;
        }
        private LoopCell Template(Color color, float height = 48)
        {
            var rect = Node("CellTemplate", transform, new Vector2(800, height), Vector2.zero); rect.gameObject.AddComponent<Image>().color = color;
            var label = Label("", rect, new Vector2(770, height - 8), Vector2.zero);
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = new Vector2(14, 4); label.rectTransform.offsetMax = new Vector2(-14, -4);
            var element = rect.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = height;
            var cell = rect.gameObject.AddComponent<LoopCell>(); rect.gameObject.SetActive(false); return cell;
        }
        private LoopScrollView MakeList(string name, Vector2 size, Vector2 position, LoopLayout mode, bool dynamic = false, LoopCell[] prefabs = null)
        {
            var root = Node(name, transform, size, position); root.gameObject.AddComponent<Image>().color = new Color(.09f, .13f, .2f);
            var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.movementType = ScrollRect.MovementType.Clamped;
            var viewport = Node("Viewport", root, size, Vector2.zero); viewport.gameObject.AddComponent<Image>().color = new Color(.09f, .13f, .2f); viewport.gameObject.AddComponent<RectMask2D>();
            var content = Node("Content", viewport, size, Vector2.zero); scroll.viewport = viewport; scroll.content = content;
            var view = root.gameObject.AddComponent<LoopScrollView>();
            if (prefabs == null) prefabs = new[] { Template(new Color(.12f, .21f, .32f)) };
            var entries = new LoopCellPrefab[prefabs.Length]; for (var i = 0; i < entries.Length; i++) entries[i] = new LoopCellPrefab { Type = i, Prefab = prefabs[i], Prewarm = 32 };
            view.Configure(scroll, entries, mode, new Vector2(mode == LoopLayout.Horizontal ? 300 : 180, 52), dynamic); return view;
        }
        private static void Bind(LoopCell cell, string item, CellBindContext context) { cell.GetComponentInChildren<Text>(true).text = item; }

    }
}
