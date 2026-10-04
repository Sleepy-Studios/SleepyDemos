#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SleepyStudios.LoopScroll;
using SleepyStudios.LoopScroll.Samples;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.Demo
{
    public sealed class LoopScrollShowcaseTests
    {
        private int previousLanguage;
        private bool hadPreference;
        private LoopSampleCatalog catalog;
        [UnitySetUp]
        public IEnumerator OpenMenu()
        {
            hadPreference = PlayerPrefs.HasKey(LoopSampleLanguage.PreferenceKey);
            previousLanguage = PlayerPrefs.GetInt(LoopSampleLanguage.PreferenceKey);
            PlayerPrefs.DeleteKey(LoopSampleLanguage.PreferenceKey);
            catalog = Resources.Load<LoopSampleCatalog>("SleepyLoopScrollSamples/Catalog");
            Assert.That(catalog, Is.Not.Null);
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                catalog.ResolvePath(catalog.Find("menu")), new LoadSceneParameters(LoadSceneMode.Single));
            yield return Ready();
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            var page = UnityEngine.Object.FindObjectOfType<LoopSamplePage>();
            if (page != null)
            {
                var scene = page.gameObject.scene;
                var empty = SceneManager.CreateScene("LoopShowcaseTestCleanup");
                SceneManager.SetActiveScene(empty); yield return SceneManager.UnloadSceneAsync(scene);
            }
            if (hadPreference) PlayerPrefs.SetInt(LoopSampleLanguage.PreferenceKey, previousLanguage);
            else PlayerPrefs.DeleteKey(LoopSampleLanguage.PreferenceKey);
            PlayerPrefs.Save();
        }
        private static IEnumerator Ready()
        {
            var deadline = Time.realtimeSinceStartup + 15;
            while (Time.realtimeSinceStartup < deadline)
            {
                var page = UnityEngine.Object.FindObjectOfType<LoopSamplePage>();
                if (page != null && page.IsReady) { yield return null; yield break; }
                yield return null;
            }
            Assert.Fail("Showcase did not become ready");
        }
        private static Button Button(string name)
            => UnityEngine.Object.FindObjectsOfType<Button>().Single(button => button.gameObject.name == name);
        private static IEnumerator Enter(string id)
        {
            var source = UnityEngine.Object.FindObjectOfType<LoopSamplePage>();
            Button("Navigate:" + id).onClick.Invoke();
            Assert.That(source.IsNavigating, Is.True);
            source.NavigateTo(id); // 同一帧重复请求应被去重。
            yield return WaitForReplacement(source);
        }
        private static IEnumerator Back()
        {
            var source = UnityEngine.Object.FindObjectOfType<LoopSamplePage>();
            Button("back").onClick.Invoke(); yield return WaitForReplacement(source);
        }
        private static IEnumerator WaitForReplacement(LoopSamplePage source)
        {
            var deadline = Time.realtimeSinceStartup + 15;
            while (source != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(source == null, Is.True); yield return Ready();
        }
        private static void AssertOneSession()
        {
            Assert.That(UnityEngine.Object.FindObjectsOfType<LoopSamplePage>().Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsOfType<EventSystem>().Length, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsOfType<Camera>().Length, Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator DynamicControls_RealPointerSubmitDisableAndReopenRestoreStates()
        {
            var original = UnityEngine.InputSystem.InputSystem.settings;
            var settings = UnityEngine.Object.Instantiate(original);
            settings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            UnityEngine.InputSystem.InputSystem.settings = settings;
            var mouse = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            var keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            try
            {
                yield return null; yield return null;
                var button = Button("language"); var interaction = button.GetComponent<Core.Runtime.UIStateInteraction>();
                Assert.That(interaction, Is.Not.Null);
                var rect = (RectTransform)button.transform;
                Vector2 point = RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera, rect.TransformPoint(rect.rect.center));
                Core.Runtime.Inputs.InputDeviceState.Notify(mouse);
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = point });
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Hover"));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = point }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Pressed")); Assert.That(rect.localScale.x, Is.EqualTo(.96f).Within(.001f));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState { position = point });
                yield return null; yield return null;
                Assert.That(LoopSampleLanguage.IsEnglish, Is.True);
                EventSystem.current.SetSelectedGameObject(button.gameObject); Core.Runtime.Inputs.InputDeviceState.Notify(keyboard);
                yield return null; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Focused"));
                Assert.That(button.transform.Find("InteractionFeedback").GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Enter));
                yield return null; yield return null;
                UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(LoopSampleLanguage.IsEnglish, Is.False);
                button.interactable = false; yield return null;
                Assert.That(interaction.InteractionState, Is.EqualTo("Disabled"));
                Assert.That(button.transform.Find("InteractionFeedback").GetComponent<CanvasGroup>().alpha, Is.Zero);
                button.gameObject.SetActive(false); button.gameObject.SetActive(true); button.interactable = true;
                yield return null; yield return null;
                Assert.That(rect.localScale, Is.EqualTo(Vector3.one));
                yield return Capture("shared-ui-states");
            }
            finally
            {
                UnityEngine.InputSystem.InputSystem.RemoveDevice(mouse); UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);
                UnityEngine.InputSystem.InputSystem.settings = original; UnityEngine.Object.Destroy(settings);
            }
        }

        [UnityTest]
        public IEnumerator MainMenuTraversesEveryEntryAndReturns()
        {
            Assert.That(LoopSampleLanguage.IsEnglish, Is.False);
            foreach (var entry in catalog.Entries)
            {
                if (entry.id == "menu") continue;
                yield return Enter(entry.id); AssertOneSession();
                Assert.That(Button("back").interactable, Is.True);
                yield return Capture("zh-" + entry.id);
                yield return Back(); AssertOneSession();
                Assert.That(UnityEngine.Object.FindObjectsOfType<LoopScrollView>().Length, Is.Zero);
            }
            yield return Capture("zh-menu");
        }
        [UnityTest]
        public IEnumerator TenRoundTripsRejectDuplicatesAndRecoverMissingEntry()
        {
            for (var i = 0; i < 10; i++)
            { yield return Enter("multi"); yield return Back(); AssertOneSession(); Assert.That(UnityEngine.Object.FindObjectsOfType<Canvas>().Length, Is.EqualTo(1)); }
            var menu = UnityEngine.Object.FindObjectOfType<LoopSamplePage>(); menu.NavigateTo("missing-entry");
            Assert.That(menu.IsNavigating, Is.False); Assert.That(Button("Navigate:basic").interactable, Is.True);
            Assert.That(UnityEngine.Object.FindObjectsOfType<Text>().Any(text => text.text.Contains("找不到示例场景")), Is.True);
            var entry = catalog.Find("basic"); var originalPath = entry.scenePath;
            try
            {
                entry.scenePath = "MissingScene.unity"; menu.NavigateTo("basic");
                Assert.That(menu.IsNavigating, Is.False); Assert.That(Button("Navigate:basic").interactable, Is.True);
                Assert.That(UnityEngine.Object.FindObjectsOfType<Text>().Any(text => text.text.Contains("场景加载失败")), Is.True);
            }
            finally { entry.scenePath = originalPath; }
            yield return Enter("chat"); yield return Back();
        }
        [UnityTest]
        public IEnumerator LanguageSwitchPreservesSelectionChatAndCarouselIdentity()
        {
            yield return Enter("multi");
            var list = UnityEngine.Object.FindObjectOfType<LoopScrollView>(); list.ScrollToCell(50);
            var selection = list.GetComponent<LoopSelectionController>();
            selection.SetSelected(list.GetItemKey(50), true);
            var firstKey = list.GetItemKey(list.VisibleRange.First); var offset = list.Offset;
            LoopSampleLanguage.SetEnglish(true);
            Assert.That(list.GetItemKey(list.VisibleRange.First), Is.EqualTo(firstKey));
            Assert.That(list.Offset, Is.EqualTo(offset).Within(1)); CollectionAssert.Contains(selection.SelectedKeys, "50");
            yield return Capture("en-multi"); yield return Back();
            Assert.That(LoopSampleLanguage.IsEnglish, Is.True);
            yield return Enter("chat");
            list = UnityEngine.Object.FindObjectOfType<LoopScrollView>(); list.ScrollToCell(10);
            var chat = list.GetComponent<LoopChatController>(); Button("newMessage").onClick.Invoke();
            firstKey = list.GetItemKey(list.VisibleRange.First); var unread = chat.UnreadCount;
            LoopSampleLanguage.SetEnglish(false); yield return null; yield return null;
            Assert.That(list.GetItemKey(list.VisibleRange.First), Is.EqualTo(firstKey));
            Assert.That(chat.UnreadCount, Is.EqualTo(unread)); Assert.That(unread, Is.EqualTo(1));
            Button("history").onClick.Invoke(); yield return null;
            Assert.That(list.GetItemKey(list.VisibleRange.First), Is.EqualTo(firstKey));
            Button("latest").onClick.Invoke(); yield return null; Assert.That(chat.UnreadCount, Is.Zero);
            yield return Back(); yield return Enter("carousel");
            var carousel = UnityEngine.Object.FindObjectOfType<LoopCarouselController>();
            carousel.Configure(300, 0, .05f); Button("next").onClick.Invoke();
            yield return new WaitForSecondsRealtime(.15f); var page = carousel.CurrentPage;
            LoopSampleLanguage.SetEnglish(true); yield return null;
            Assert.That(carousel.CurrentPage, Is.EqualTo(page));
            Button("more").onClick.Invoke(); yield return new WaitForSecondsRealtime(.6f);
            Assert.That(UnityEngine.Object.FindObjectsOfType<LoopScrollView>().Single(view => !view.IsLooping).Count, Is.GreaterThanOrEqualTo(30));
            yield return Capture("en-carousel"); yield return Back(); yield return Capture("en-menu");
            Button("language").onClick.Invoke(); Assert.That(LoopSampleLanguage.IsEnglish, Is.False);
        }
        [UnityTest]
        public IEnumerator DirectChildReturnsAndChineseFontCoversDisplayedCharacters()
        {
            yield return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                catalog.ResolvePath(catalog.Find("chat")), new LoadSceneParameters(LoadSceneMode.Single));
            yield return Ready(); AssertOneSession();
            foreach (var text in UnityEngine.Object.FindObjectsOfType<Text>())
                foreach (var character in text.text)
                    if (!char.IsWhiteSpace(character)) Assert.That(text.font.HasCharacter(character), Is.True, "Missing glyph: " + character);
            yield return Back(); Assert.That(Button("Navigate:chat"), Is.Not.Null);
        }
        [UnityTest]
        public IEnumerator BasicControlsAndNativeDragChangeVisibleRange()
        {
            yield return Enter("basic"); Button("horizontal").onClick.Invoke(); yield return null;
            var list = UnityEngine.Object.FindObjectOfType<LoopScrollView>(); Assert.That(list.IsVertical, Is.False);
            Button("goto50000").onClick.Invoke(); yield return new WaitForSecondsRealtime(.35f);
            Assert.That(list.VisibleRange.First, Is.GreaterThan(49000));
            Button("vertical").onClick.Invoke(); yield return null;
            list = UnityEngine.Object.FindObjectOfType<LoopScrollView>();
            Canvas.ForceUpdateCanvases();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, list.ScrollRect.viewport.position) };
            var before = list.Offset;
            ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position += Vector2.up * 150;
            ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(list.Offset, Is.GreaterThan(before + 10)); yield return Back();
        }
        [UnityTest]
        public IEnumerator MvcOffsetCompletionManualCancelDragAndLanguageKeepControlsAvailable()
        {
            yield return Enter("mvc");
            var list = UnityEngine.Object.FindObjectOfType<LoopScrollView>();
            Button("offsetPositive").onClick.Invoke();
            Assert.That(list.IsAnimating, Is.True);
            Button("cancelScroll").onClick.Invoke();
            Assert.That(list.IsAnimating, Is.False);
            Assert.That(UnityEngine.Object.FindObjectsOfType<Text>().Any(text => text.text == "定位已取消 · 手动取消"), Is.True);
            Button("offsetPositive").onClick.Invoke();
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(UnityEngine.Object.FindObjectsOfType<Text>().Any(text => text.text == "定位完成"), Is.True);
            var target = list.GetVisibleCell(500).RectTransform;
            var line = (list.ViewportLength - target.rect.height) * .5f;
            Assert.That(-target.anchoredPosition.y - list.Offset, Is.EqualTo(line + 60).Within(1));
            yield return Capture("zh-mvc-offset-completed");
            Button("offsetNegative").onClick.Invoke();
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, list.ScrollRect.viewport.position) };
            ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.beginDragHandler);
            pointer.position += Vector2.up * 40;
            ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(list.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(UnityEngine.Object.FindObjectsOfType<Text>().Any(text => text.text == "定位已取消 · 开始拖动"), Is.True);
            yield return Capture("zh-mvc-drag-canceled");
            LoopSampleLanguage.SetEnglish(true);
            Assert.That(UnityEngine.Object.FindObjectsOfType<Text>().Any(text => text.text == "Scroll canceled · Drag started"), Is.True);
            foreach (var key in new[] { "goto500", "offsetPositive", "offsetNegative", "cancelScroll", "reset", "refresh" })
                Assert.That(Button(key).interactable, Is.True);
            Button("offsetNegative").onClick.Invoke(); yield return new WaitForSecondsRealtime(1f);
            target = list.GetVisibleCell(500).RectTransform;
            Assert.That(-target.anchoredPosition.y - list.Offset, Is.EqualTo(line - 60).Within(1));
            yield return Capture("en-mvc-negative-completed"); yield return Back();
        }
        private static IEnumerator Capture(string name)
        {
            var projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var parent = Directory.GetParent(projectRoot).FullName;
            var target = Path.Combine(parent, "SleepyLoopScroll", "ValidationArtifacts~", "showcase");
            Directory.CreateDirectory(target);
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(target, Application.unityVersion.Split('.')[0] + "-" + name + ".png"));
            yield return null; yield return null;
        }
        [UnityTest]
        public IEnumerator MenuRemainsReadableAcrossLandscapePortraitAndWindowSizes()
        {
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(720, 1280), new Vector2Int(1920, 1080) })
            {
                using (var scope = new GameViewSizeScope(size))
                {
                    for (var frame = 0; frame < 6; frame++) yield return null;
                    Assert.That(Screen.width, Is.EqualTo(size.x)); Assert.That(Screen.height, Is.EqualTo(size.y));
                    Canvas.ForceUpdateCanvases();
                    foreach (var button in UnityEngine.Object.FindObjectsOfType<Button>())
                    {
                        var corners = new Vector3[4]; button.GetComponent<RectTransform>().GetWorldCorners(corners);
                        foreach (var corner in corners)
                        {
                            var point = RectTransformUtility.WorldToScreenPoint(null, corner);
                            Assert.That(point.x, Is.InRange(0f, (float)Screen.width));
                            Assert.That(point.y, Is.InRange(0f, (float)Screen.height));
                        }
                    }
                    foreach (var text in UnityEngine.Object.FindObjectsOfType<Text>())
                    {
                        Assert.That(text.preferredWidth, Is.LessThanOrEqualTo(text.rectTransform.rect.width + 1), text.text);
                        Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 1), text.text);
                    }
                    yield return Capture("menu-" + size.x + "x" + size.y);
                }
                yield return null;
            }
        }
        // 只在 Test Runner 内临时设置 Game View，结束后恢复原选择并移除本次尺寸；不保存 Editor 偏好。
        private sealed class GameViewSizeScope : IDisposable
        {
            private readonly UnityEditor.EditorWindow window;
            private readonly PropertyInfo selection;
            private readonly object group;
            private readonly int original;
            private readonly int temporary;
            public GameViewSizeScope(Vector2Int size)
            {
                var assembly = typeof(UnityEditor.Editor).Assembly;
                var sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
                var singleton = typeof(UnityEditor.ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizes = singleton.GetProperty("instance").GetValue(null);
                group = sizesType.GetProperty("currentGroup").GetValue(sizes);
                temporary = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
                var enumType = assembly.GetType("UnityEditor.GameViewSizeType", true);
                var sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
                var definition = Activator.CreateInstance(sizeType, Enum.Parse(enumType, "FixedResolution"), size.x, size.y, "Loop Scroll validation");
                window = UnityEditor.EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView", true));
                selection = window.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                original = (int)selection.GetValue(window);
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { definition });
                selection.SetValue(window, temporary); window.Repaint();
            }
            public void Dispose()
            {
                selection.SetValue(window, original);
                group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { temporary }); window.Repaint();
            }
        }
    }
}
#endif
