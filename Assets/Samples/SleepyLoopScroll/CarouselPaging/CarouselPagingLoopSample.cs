using System;
using System.Collections;
using System.Collections.Generic;
using SleepyStudios.LoopScroll;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SleepyStudios.LoopScroll.Samples.CarouselPaging
{
    public sealed class CarouselPagingLoopSample : MonoBehaviour
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
            Label("Sleepy Loop Scroll  /  CAROUSEL + PAGING", transform, new Vector2(880, 50), new Vector2(0, 310), 27);
            status = Label("Looping gallery / independent paging request state / nested drag routing", transform, new Vector2(880, 40), new Vector2(0, -315), 17);
            var pages = new List<string> { "OCEAN\nPage 1", "ISLAND\nPage 2", "SKY\nPage 3" };
            var gallery = MakeList("Gallery", new Vector2(840, 180), new Vector2(0, 130), LoopLayout.Horizontal);
            var carousel = gallery.gameObject.AddComponent<LoopCarouselController>(); carousel.Configure(300, 3, .2f); gallery.SetData<string, LoopCell>(pages, Bind, null, item => item);
            carousel.PageChanged += page => status.text = "Carousel page: " + (page + 1) + " / 3";
            for (var i = 0; i < 20; i++) items.Add("Loaded entry " + i);
            list = MakeList("Paged list", new Vector2(840, 260), new Vector2(0, -125), LoopLayout.Vertical); list.SetData<string, LoopCell>(items, Bind, null, item => item);
            var paging = list.gameObject.AddComponent<LoopPagingTrigger>(); paging.Configure(false, true); paging.LoadRequested += boundary => StartCoroutine(LoadPage(paging, boundary));
            gallery.ScrollRect.viewport.gameObject.AddComponent<NestedScrollRouter>().Configure(gallery.ScrollRect, list.ScrollRect);
            Button("Previous page", new Vector2(-205, 250), carousel.Previous); Button("Next page", new Vector2(0, 250), carousel.Next); Button("More entries", new Vector2(205, 250), () => list.ScrollTo(items.Count - 1, ScrollAlignment.End));
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
        private IEnumerator LoadPage(LoopPagingTrigger paging, PagingBoundary boundary)
        {
            status.text = "Loading next page..."; yield return new WaitForSecondsRealtime(.5f);
            var count = items.Count; for (var i = 0; i < 10; i++) items.Add("Loaded entry " + (count + i));
            list.Append(10); paging.Complete(boundary, items.Count < 100); status.text = "Loaded " + items.Count + " entries";
        }
    }
}