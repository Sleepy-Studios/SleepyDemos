#if UNITY_EDITOR
using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Core.Runtime;
using Core.Runtime.Inputs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class UIComponentLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator DefaultAsyncTab_ReinitializeOnlyCompletesLatestRequest()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Tab/Tab.prefab"));
            try
            {
                var tab = root.GetComponent<UITab>();
                int oldCompleted = 0, newCompleted = 0;
                tab.Init(new[] { "A", "B", "C" }, action: () => oldCompleted++);
                Assert.That(oldCompleted, Is.Zero);
                tab.Init(new[] { "D", "E" }, initIndex: 1, action: () => newCompleted++);
                for (int i = 0; i < 10 && newCompleted == 0; i++) yield return null;
                Assert.That(oldCompleted, Is.Zero);
                Assert.That(newCompleted, Is.EqualTo(1));
                Assert.That(tab.Index, Is.EqualTo(1));
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator DefaultAsyncList_ReinitializeThenDisableStopsOldCallbacks()
        {
            var root = new GameObject("List", typeof(RectTransform), typeof(ViewList));
            var prefab = new GameObject("Item", typeof(RectTransform));
            try
            {
                var list = root.GetComponent<ViewList>();
                typeof(ViewList).GetField("prefab", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(list, prefab);
                int oldCount = 0, latestCount = 0;
                list.Init<View, int>(new[] { 1, 2, 3 }, (_, __, ___) => oldCount++);
                Assert.That(oldCount, Is.EqualTo(1), "默认异步逐帧交付，不应同步处理整表。");
                list.Init<View, int>(new[] { 4, 5 }, (_, __, ___) => latestCount++);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(oldCount, Is.EqualTo(1));
                Assert.That(latestCount, Is.EqualTo(2));
                list.Init<View, int>(new[] { 6, 7, 8 }, (_, __, ___) => latestCount++);
                root.SetActive(false);
                yield return null; yield return null;
                Assert.That(latestCount, Is.EqualTo(3));
                root.SetActive(true);
                int synchronousCount = 0;
                list.Init<View, int>(new[] { 9, 10 }, (_, __, ___) => synchronousCount++, isAsync: false);
                Assert.That(synchronousCount, Is.EqualTo(2));
            }
            finally { Object.Destroy(root); Object.Destroy(prefab); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator Accordion_DefaultAsyncReplacementAndDisableDoNotNotifyOldRequest()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Tab/AccordionTab.prefab"));
            try
            {
                var tab = root.GetComponent<AccordionTab>();
                var data = new[] { new AccordionTabData { Desc = "A" }, new AccordionTabData { Desc = "B" } };
                int oldCount = 0, latestCount = 0;
                tab.Init(data, action: () => oldCount++);
                tab.Init(data, initLeafIndex: 1, action: () => latestCount++);
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(oldCount, Is.Zero);
                Assert.That(latestCount, Is.EqualTo(1));
                Assert.That(tab.Index, Is.EqualTo(1));
                tab.Init(data, action: () => oldCount++);
                root.SetActive(false);
                yield return null; yield return null;
                Assert.That(oldCount, Is.Zero);
                root.SetActive(true);
                tab.Init(data, action: () => latestCount++, isAsync: false);
                Assert.That(latestCount, Is.EqualTo(2));
            }
            finally { Object.Destroy(root); }
        }

        [UnityTest]
        public IEnumerator ImageDefaultAsync_ReplacementAndDestructionDiscardLateResults()
        {
            var root = new GameObject("Image", typeof(RectTransform), typeof(Image), typeof(UIImageLoader));
            var loader = new DeferredImageLoader();
            var first = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            var second = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.zero);
            try
            {
                var component = root.GetComponent<UIImageLoader>();
                typeof(UIImageLoader).GetField("loader", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(component, loader);
                component.SetImage("first", false);
                component.SetImage("second", false);
                Assert.That(loader.Requests.Count, Is.EqualTo(2));
                loader.Requests[1].TrySetResult(second);
                loader.Requests[0].TrySetResult(first);
                yield return null;
                Assert.That(root.GetComponent<Image>().sprite, Is.SameAs(second));
                Assert.That(loader.Released, Does.Contain(first));
                component.SetImage("late", false);
                Object.Destroy(root);
                yield return null;
                loader.Requests[2].TrySetResult(first);
                yield return null;
                Assert.That(loader.Disposed, Is.True);
                Assert.That(loader.Released, Does.Contain(second));
                Assert.That(loader.Released.FindAll(x => x == first).Count, Is.EqualTo(2));
            }
            finally { if (root != null) Object.Destroy(root); Object.Destroy(first); Object.Destroy(second); }
        }

        private sealed class DeferredImageLoader : IResourceLoader
        {
            public readonly List<UniTaskCompletionSource<Sprite>> Requests = new List<UniTaskCompletionSource<Sprite>>();
            public readonly List<Object> Released = new List<Object>();
            public bool Disposed;
            public GameObject Instantiate(string address, Transform parent) => throw new NotSupportedException();
            public GameObject Instantiate(string address, Transform parent, bool stays) => throw new NotSupportedException();
            public UniTask<GameObject> InstantiateAsync(string address, Transform parent) => throw new NotSupportedException();
            public UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool stays) => throw new NotSupportedException();
            public T LoadAsset<T>(string address) where T : Object => throw new NotSupportedException();
            public async UniTask<T> LoadAssetAsync<T>(string address) where T : Object
            {
                var source = new UniTaskCompletionSource<Sprite>();
                Requests.Add(source);
                return await source.Task as T;
            }
            public void ReleaseAsset(Object asset) => Released.Add(asset);
            public void ReleaseInstance(GameObject instance) => Object.Destroy(instance);
            public void Dispose() => Disposed = true;
        }

        [UnityTest]
        public IEnumerator ViewTab_LateLoadCannotShowOldSelectionOrClearNewInitialization()
        {
            var root = new GameObject("ViewTabs", typeof(RectTransform), typeof(ViewTab));
            var firstLoader = new DeferredViewLoader();
            var secondLoader = new DeferredViewLoader();
            var thirdLoader = new DeferredViewLoader();
            var first = new DeferredView { Loader = firstLoader };
            var second = new DeferredView { Loader = secondLoader };
            var third = new DeferredView { Loader = thirdLoader };
            try
            {
                var tabs = root.GetComponent<ViewTab>();
                tabs.Init(views: new List<View> { first, second }, enableAnimation: false);
                tabs.Select(1);
                firstLoader.Complete(root.transform);
                yield return null;
                secondLoader.Complete(root.transform);
                yield return null;
                Assert.That(first.State, Is.EqualTo(ViewState.LoadedHidden));
                Assert.That(second.State, Is.EqualTo(ViewState.Visible));
                tabs.Init(views: new List<View> { first, second }, enableAnimation: false);
                yield return null;
                Assert.That(first.State, Is.EqualTo(ViewState.Visible));
                Assert.That(second.State, Is.EqualTo(ViewState.LoadedHidden));
                tabs.Init(views: new List<View> { third }, enableAnimation: false);
                thirdLoader.Complete(root.transform);
                yield return null; yield return null;
                Assert.That(tabs.CurrentClickView, Is.SameAs(third));
                Assert.That(third.State, Is.EqualTo(ViewState.Visible));
                Assert.That(first.State, Is.EqualTo(ViewState.Destroyed));
                Assert.That(second.State, Is.EqualTo(ViewState.Destroyed));
            }
            finally { Object.Destroy(root); }
            yield return null; yield return null;
            Assert.That(third.State, Is.EqualTo(ViewState.Destroyed));
        }

        [UnityTest]
        public IEnumerator Dropdown_DefaultAsyncCompletesWhileCollapsed()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Dropdown/Dropdown.prefab"));
            try
            {
                var dropdown = root.GetComponent<UIDropdown>();
                dropdown.SetData(new[] { "A", "B", "C" }, _ => { }, selectedIndex: 2);
                for (int i = 0; i < 6; i++) yield return null;
                var tab = root.GetComponentInChildren<UITab>(true);
                Assert.That(tab.Index, Is.EqualTo(2));
                Assert.That(tab.Items[2].activeSelf, Is.True);
            }
            finally { Object.Destroy(root); }
        }

        private sealed class DeferredView : View
        {
            public override string Address => "Tests/DeferredView";
            public override bool EnableOnInit => false;
        }

        private sealed class DeferredViewLoader : IResourceLoader
        {
            private readonly UniTaskCompletionSource<GameObject> completion = new UniTaskCompletionSource<GameObject>();
            public void Complete(Transform parent)
            {
                var instance = new GameObject("LoadedView", typeof(RectTransform));
                instance.transform.SetParent(parent, false);
                completion.TrySetResult(instance);
            }
            public GameObject Instantiate(string address, Transform parent) => throw new NotSupportedException();
            public GameObject Instantiate(string address, Transform parent, bool stays) => throw new NotSupportedException();
            public UniTask<GameObject> InstantiateAsync(string address, Transform parent) => completion.Task;
            public UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool stays) => completion.Task;
            public T LoadAsset<T>(string address) where T : Object => throw new NotSupportedException();
            public UniTask<T> LoadAssetAsync<T>(string address) where T : Object => throw new NotSupportedException();
            public void ReleaseAsset(Object asset) { }
            public void ReleaseInstance(GameObject instance) { if (instance != null) Object.Destroy(instance); }
            public void Dispose() { }
        }

        [UnityTest]
        public IEnumerator CanceledPointerHoldDoesNotConsumeLaterSubmit()
        {
            var events = new GameObject("CommandEvents", typeof(EventSystem));
            var root = new GameObject("Command", typeof(RectTransform), typeof(Button), typeof(InputCommandButton));
            var command = root.GetComponent<InputCommandButton>();
            try
            {
                var fields = new SerializedObject(command);
                fields.FindProperty("hold").boolValue = true; fields.ApplyModifiedPropertiesWithoutUndo();
                int clicks = 0, releases = 0;
                command.Clicked += _ => clicks++;
                command.HoldChanged += (_, held) => { if (!held) releases++; };
                var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 4 };
                command.OnPointerDown(pointer); command.OnPointerUp(pointer);
                root.GetComponent<Button>().onClick.Invoke();
                Assert.That(clicks, Is.Zero, "同帧指针 Click 不能再提交一次保持命令。");
                yield return null;
                root.GetComponent<Button>().OnSubmit(new BaseEventData(events.GetComponent<EventSystem>()));
                Assert.That(clicks, Is.EqualTo(1), "拖出取消不能留下吞掉下一次确认的标记。");
                Assert.That(releases, Is.EqualTo(1));
            }
            finally { Object.Destroy(root); Object.Destroy(events); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LongTextShrinksAndShortTextRestoresExplicitDesignSize()
        {
            var canvas = new GameObject("TextCanvas", typeof(Canvas));
            var root = new GameObject("AutoFit", typeof(RectTransform)); root.transform.SetParent(canvas.transform);
            root.AddComponent<TextMeshProUGUI>();
            try
            {
                var text = root.GetComponent<TextMeshProUGUI>();
                text.font = TMP_Settings.defaultFontAsset;
                text.fontSizeMin = 10; text.fontSize = 36;
                var fit = root.AddComponent<TMPAutoFitLayoutElement>();
                fit.SetDesignFontSize(36); fit.SetMaxWidth(100); fit.SetMaxHeight(40);
                text.text = "A very long sentence that requires several lines inside a small box.";
                fit.RefreshLayout(); text.ForceMeshUpdate(); yield return null;
                Assert.That(text.fontSize, Is.LessThan(36));
                text.text = "A"; fit.RefreshLayout(); text.ForceMeshUpdate();
                Assert.That(text.fontSize, Is.EqualTo(36).Within(.01f));
                Assert.That(fit.DesignFontSize, Is.EqualTo(36));
            }
            finally { Object.Destroy(canvas); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseMinusOnePointerCannotReenterAndDisableReleasesOwnership()
        {
            var events = new GameObject("PressFixtureEvents", typeof(EventSystem));
            var first = new GameObject("First", typeof(RectTransform), typeof(PressButton));
            var second = new GameObject("Second", typeof(RectTransform), typeof(PressButton));
            var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = -1 };
            try
            {
                int down = 0;
                var source = first.GetComponent<PressButton>(); source.OnMouseDown.AddListener(() => down++);
                source.OnPointerDown(pointer); source.OnPointerDown(pointer);
                Assert.That(down, Is.EqualTo(1), "同一个鼠标按下不能重复进入。");
                second.GetComponent<PressButton>().OnPointerDown(pointer);
                Assert.That(second.GetComponent<PressButton>().IsPressed, Is.False);
                first.SetActive(false);
                second.GetComponent<PressButton>().OnPointerDown(pointer);
                Assert.That(second.GetComponent<PressButton>().IsPressed, Is.True, "禁用时释放合法的 -1 指针所有权。");
                second.GetComponent<PressButton>().OnPointerUp(pointer);
            }
            finally { Object.Destroy(first); Object.Destroy(second); Object.Destroy(events); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenAsyncTabDoesNotNotifyAndCanInitializeAgainSynchronously()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Tab/Tab.prefab");
            var root = Object.Instantiate(prefab);
            var tab = root.GetComponent<UITab>();
            try
            {
                int completed = 0;
                tab.Init(new[] { "一", "二", "三", "四" }, action: () => completed++, isAsync: true);
                root.SetActive(false);
                yield return null; yield return null;
                Assert.That(completed, Is.Zero, "隐藏后旧异步任务不能交付完成回调。");
                root.SetActive(true);
                tab.Init(new[] { "新的 A", "新的 B" }, initIndex: 1, action: () => completed++, isAsync: false);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(tab.Index, Is.EqualTo(1));
                Assert.That(tab.Items[0].activeSelf, Is.True);
                Assert.That(tab.Items[1].activeSelf, Is.True);
                for (int i = 2; i < tab.Count; i++) Assert.That(tab.Items[i].activeSelf, Is.False);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
#endif
