using System;
using System.Collections;
using System.Collections.Generic;
using SleepyStudios.LoopScroll;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SleepyStudios.LoopScroll.Samples.MultiType
{
    public sealed class MultiTypeLoopSample : MonoBehaviour
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
            Label("Sleepy Loop Scroll  /  MULTIPLE TYPES", transform, new Vector2(880, 50), new Vector2(0, 310), 27);
            status = Label("Separate prefab pools / stable selection / click any row", transform, new Vector2(880, 40), new Vector2(0, -315), 17);
            for (var i = 0; i < 200; i++) items.Add((i % 2 == 0 ? "Equipment  " : "Consumable  ") + i);
            list = MakeList("Mixed", new Vector2(840, 480), new Vector2(0, -10), LoopLayout.Vertical, false, new[] { Template(new Color(.13f, .24f, .36f)), Template(new Color(.2f, .17f, .32f)) });
            list.SetDataSource(new MixedSource(items)); var selection = list.gameObject.AddComponent<LoopSelectionController>(); selection.MultiSelect = true;
            selection.CellSelectionChanged += (cell, selected) => cell.GetComponent<Image>().color = selected ? new Color(.1f, .55f, .5f) : cell.Context.Index % 2 == 0 ? new Color(.13f, .24f, .36f) : new Color(.2f, .17f, .32f);
            selection.SelectionChanged += () => status.text = "Selected: " + selection.SelectedKeys.Count;
            Button("Clear selection", new Vector2(-205, 250), selection.ClearSelection); Button("Refresh visible", new Vector2(0, 250), list.RefreshVisible); Button("Go to 150", new Vector2(205, 250), () => list.ScrollTo(150, ScrollAlignment.Center));
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
        private sealed class MixedSource : ILoopDataSource
        {
            private readonly List<string> data; public MixedSource(List<string> data) { this.data = data; }
            public int Count => data.Count; public bool HasStableKeys => true;
            public string GetItemKey(int index) => data[index]; public int GetCellType(int index) => index % 2;
            public float GetEstimatedSize(int index, float crossAxisSize) => 52;
            public void BindCell(LoopCell cell, int index, CellBindContext context) { Bind(cell, data[index], context); }
            public void UnbindCell(LoopCell cell, CellBindContext context) { }
        }
    }
}