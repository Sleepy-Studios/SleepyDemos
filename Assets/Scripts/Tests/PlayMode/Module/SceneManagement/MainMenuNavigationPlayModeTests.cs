#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Core.Runtime;
using Core.Runtime.Inputs;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.SceneManagement;
using SleepyStudios.LoopScroll;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    /// 通过 InputSystem 指针、键盘和手柄验证 Hub 进入及回滚，不调用按钮监听器模拟操作。
    public sealed class MainMenuNavigationPlayModeTests
    {
        private GameSceneNavigator originalNavigator;
        private FailingSceneRuntime runtime;
        private Mouse mouse;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Touchscreen touchscreen;
        private InputSettings originalInputSettings;
        private InputSettings testInputSettings;

        [UnityTest, Timeout(180000)]
        public IEnumerator FailedEntryRestoresNewMenuAndAllowsRetry()
        {
            yield return PrepareHub();
            var firstMenu = UIManager.Instance.Get<MainMenuView>();
            // 直接使用保存的绑定，避免把 Prefab 的层级路径变成测试契约。
            var button = CardForScene(firstMenu, GameSceneId.JinxCasino);
            yield return null; yield return null;
            Vector2 position = ButtonPosition(button);
            yield return Click(position);
            Assert.That(runtime.LoadCount, Is.Zero, "点击卡片只切换预览，不能误进入场景。");
            position = ButtonPosition(Field<Button>(firstMenu, "Button_Start"));
            yield return Click(position);
            yield return Wait(() => runtime.LoadCount == 1 && firstMenu.State == ViewState.Destroyed, "Loading 替换旧 Hub", 15);
            yield return Click(position);
            Assert.That(runtime.LoadCount, Is.EqualTo(1), "加载期间重复点击不能再发起进入请求。");

            LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenuView\] 无法进入 JinxCasino：测试场景加载失败"));
            runtime.FailEntry();
            yield return Wait(IsStableHub, "加载失败恢复 Hub", 15);
            var restoredMenu = UIManager.Instance.Get<MainMenuView>();
            Assert.That(restoredMenu, Is.Not.SameAs(firstMenu));
            var feedback = Field<TMPro.TextMeshProUGUI>(restoredMenu, "TextMeshProUGUI_Status");
            Assert.That(feedback.text, Does.Contain("进入失败"));
            var feedbackSize = feedback.GetPreferredValues();
            Assert.That(feedbackSize.x, Is.LessThanOrEqualTo(feedback.rectTransform.rect.width), "失败提示应在已有控件内完整显示。");
            Assert.That(feedbackSize.y, Is.LessThanOrEqualTo(feedback.rectTransform.rect.height), "失败提示不能溢出遮住入口。");
            foreach (var card in VisibleCards(restoredMenu))
                Assert.That(card.Button.interactable, Is.True, "恢复后所有卡片可浏览；未开放限制由开始按钮承担。");

            // 新实例需要经过帧末 Canvas 注册/布局；随后仍验证真实射线并通过 InputSystem 点击。
            yield return null; yield return null;
            Assert.That(Field<TMPro.TextMeshProUGUI>(restoredMenu, "TextMeshProUGUI_Title").text, Is.EqualTo("倒霉蛋俱乐部"), "失败恢复应保留待重试玩法。");
            yield return Click(ButtonPosition(Field<Button>(restoredMenu, "Button_Start")));
            yield return Wait(() => runtime.LoadCount == 2, "新 Hub 可以实际点击重试", 15);
            LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenuView\] 无法进入 JinxCasino：测试场景加载失败"));
            runtime.FailEntry();
            yield return Wait(IsStableHub, "重试失败也能恢复", 15);
            yield return null; yield return null;
            Assert.That(CardForScene(UIManager.Instance.Get<MainMenuView>(), GameSceneId.JinxCasino).interactable, Is.True);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator KeyboardAndGamepadNavigateEnterAndRecoverWithoutRepeatingHeldSubmit()
        {
            yield return PrepareHub();
            var firstMenu = UIManager.Instance.Get<MainMenuView>();
            yield return WaitForInitialSelection(firstMenu);
            var firstSelection = EventSystem.current.currentSelectedGameObject;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.EqualTo(firstSelection), "方向键应移动真实焦点。");
            yield return Wait(() => SelectedCardIsVisible(), "键盘焦点滚入视口", 5);
            var keyboardTarget = SelectedScene(firstMenu);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            yield return Wait(() => runtime.LoadCount == 1 && firstMenu.State == ViewState.Destroyed, "Enter 进入当前选中 Demo", 15);
            AssertRequestedScene(keyboardTarget);
            ExpectEntryFailure(keyboardTarget);
            runtime.FailEntry();
            yield return Wait(IsStableHub, "键盘进入失败回滚", 15);

            var restoredMenu = UIManager.Instance.Get<MainMenuView>();
            yield return WaitForInitialSelection(restoredMenu);
            firstSelection = EventSystem.current.currentSelectedGameObject;
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            var backgroundPosition = new Vector2(Screen.width * .05f, Screen.height * .05f);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = backgroundPosition, phase = UnityEngine.InputSystem.TouchPhase.Began }); yield return null;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = backgroundPosition, phase = UnityEngine.InputSystem.TouchPhase.Ended }); yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null, "触屏点击空白应清除选中项，为切回手柄提供真实前置状态。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.EqualTo(firstSelection), "方向键应移动真实手柄焦点。");
            yield return Wait(() => SelectedCardIsVisible(), "手柄焦点滚入视口", 5);
            var gamepadTarget = SelectedScene(restoredMenu);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            yield return Wait(() => runtime.LoadCount == 2 && restoredMenu.State == ViewState.Destroyed, "A 进入当前选中 Demo", 15);
            AssertRequestedScene(gamepadTarget);
            ExpectEntryFailure(gamepadTarget);
            runtime.FailEntry();
            yield return Wait(IsStableHub, "按住 A 时恢复新 Hub", 15);
            yield return null; yield return null;
            Assert.That(EventSystem.current.sendNavigationEvents, Is.False, "恢复期间 A 未松开不能重新提交。");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
            Assert.That(runtime.LoadCount, Is.EqualTo(2));

            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            var latestMenu = UIManager.Instance.Get<MainMenuView>();
            yield return WaitForInitialSelection(latestMenu);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            yield return Wait(() => runtime.LoadCount == 3, "松开 A 后可明确再次进入", 15);
            AssertRequestedScene(gamepadTarget);
            ExpectEntryFailure(gamepadTarget);
            runtime.FailEntry();
            yield return Wait(IsStableHub, "再次进入结束后收口", 15);
            yield return WaitForInitialSelection(UIManager.Instance.Get<MainMenuView>());
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator GamepadScrollsRecycledCardsAndEntersCurrentIdentity()
        {
            yield return PrepareHub();
            var menu = UIManager.Instance.Get<MainMenuView>();
            var entries = Field<List<MainMenuDemoEntry>>(menu, "entries");
            var originals = entries.ToArray();
            var list = Field<LoopScrollView>(menu, "LoopScrollView_DemoList");
            // 放大真实集合以跨越虚拟化边界；不以物理 Cell 数量或固定布局作断言。
            entries.Clear();
            for (int i = 0; i < 40; i++)
            {
                var source = originals[i % originals.Length];
                entries.Add(new MainMenuDemoEntry(source.Key + ":" + i, source.Title + " " + i, source.Description, source.PreviewAddress, source.SceneId));
            }
            list.SetTotalCount(entries, getItemKey: item => ((MainMenuDemoEntry)item).Key);
            try
            {
                yield return WaitForInitialSelection(menu);
                for (int attempt = 0; attempt < 20 && list.GetVisibleCell(0) != null; attempt++)
                {
                    InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadRight)); yield return null;
                    InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                    yield return Wait(() => EventSystem.current.sendNavigationEvents && SelectedCardIsVisible(), "手柄滚动并重新建立有效焦点", 5);
                }
                Assert.That(list.GetVisibleCell(0), Is.Null, "操作应越过首行回收边界，而不是只在初始卡片之间导航。");
                // 未开放卡片现在也允许浏览；进入断言选择下一个已开放项目。
                while (!entries[EventSystem.current.currentSelectedGameObject.GetComponentInParent<LoopCell>().Context.Index].SceneId.HasValue)
                {
                    InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadRight)); yield return null;
                    InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
                }
                var selected = EventSystem.current.currentSelectedGameObject.GetComponent<LoopScrollMenuButton>();
                var selectedCell = selected.GetComponentInParent<LoopCell>();
                Assert.That(selectedCell.Context.IsCurrent, Is.True);
                Assert.That(list.ItemViews().TryGetItemView(selectedCell, out var item), Is.True);
                Assert.That(Field<TMPro.TextMeshProUGUI>(item, "TextMeshProUGUI_Title").text,
                    Is.EqualTo(entries[selectedCell.Context.Index].Title), "回收后显示必须对应当前业务数据。");
                var target = entries[selectedCell.Context.Index].SceneId.Value;
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                yield return Wait(() => runtime.LoadCount == 1 && menu.State == ViewState.Destroyed, "回收后的卡片实际进入", 15);
                AssertRequestedScene(target);
                ExpectEntryFailure(target);
                runtime.FailEntry();
                yield return Wait(IsStableHub, "回收后进入失败回滚", 15);
            }
            finally
            {
                entries.Clear(); entries.AddRange(originals);
                if (menu.State != ViewState.Destroyed) list.SetTotalCount(entries, getItemKey: item => ((MainMenuDemoEntry)item).Key);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SelectionSharesArtworkAndUnopenedDemoCannotEnter()
        {
            yield return PrepareHub();
            var menu = UIManager.Instance.Get<MainMenuView>();
            var button = CardForScene(menu, GameSceneId.BlockPorters);
            yield return Click(ButtonPosition(button));
            Assert.That(runtime.LoadCount, Is.Zero);
            Assert.That(Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_Title").text, Is.EqualTo("小小搬豆工"));
            var hero = Field<UIImageLoader>(menu, "UIImageLoader_Hero");
            var list = Field<LoopScrollView>(menu, "LoopScrollView_DemoList");
            yield return Wait(() => hero.TargetImage.sprite != null &&
                hero.TargetImage.sprite == list.GetVisibleCell(1)?.GetComponentInChildren<UIImageLoader>().TargetImage.sprite,
                "大预览与卡片复用同一个主体 Sprite", 10);
            var preview = list.GetVisibleCell(1).GetComponentInChildren<UIImageLoader>();
            Assert.That(hero.TargetImage.preserveAspect && preview.TargetImage.preserveAspect, Is.True);

            var entries = Field<List<MainMenuDemoEntry>>(menu, "entries");
            int unopened = entries.FindIndex(entry => !entry.SceneId.HasValue);
            list.ScrollToCell(unopened, ScrollAlignment.End);
            yield return null; yield return null;
            var unavailable = list.GetVisibleCell(unopened).GetComponentInChildren<LoopScrollMenuButton>();
            yield return Click(ButtonPosition(unavailable));
            Assert.That(Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_Title").text, Is.EqualTo("UI 交互展台"));
            Assert.That(Field<Button>(menu, "Button_Start").interactable, Is.False);
            Assert.That(Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_StartLabel").text, Is.EqualTo("未开放"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(runtime.LoadCount, Is.Zero, "未开放项目允许浏览，但提交不能进入。");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TouchSelectsPreviewBeforeExplicitStart()
        {
            yield return PrepareHub();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            InputDeviceState.Notify(touchscreen);
            yield return null; yield return null;
            var menu = UIManager.Instance.Get<MainMenuView>();
            yield return Tap(ButtonPosition(CardForScene(menu, GameSceneId.BlockPorters)));
            Assert.That(runtime.LoadCount, Is.Zero, "触控选择卡片不能直接进入场景。");
            Assert.That(Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_Title").text, Is.EqualTo("小小搬豆工"));
            yield return Tap(ButtonPosition(Field<Button>(menu, "Button_Start")));
            yield return Wait(() => runtime.LoadCount == 1, "触控开始按钮进入当前预览", 15);
            AssertRequestedScene(GameSceneId.BlockPorters);
            ExpectEntryFailure(GameSceneId.BlockPorters);
            runtime.FailEntry();
            yield return Wait(IsStableHub, "触控进入失败收口", 15);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SettingsEntryRestoresAfterClosingPanelAndLeavingHall()
        {
            yield return PrepareHub();
            var menu = UIManager.Instance.Get<MainMenuView>();
            var settings = UIManager.Instance.Get<DlssSettingsView>();
            Assert.That(settings, Is.Not.Null);
            var originalEntry = Field<Button>(settings, "Button_OpenButton");
            Assert.That(originalEntry.gameObject.activeSelf, Is.False, "大厅只显示自己的画质入口。");
            yield return Click(ButtonPosition(Field<Button>(menu, "Button_Settings")));
            yield return Wait(() => settings.IsSettingsPanelOpen, "打开已有画质面板", 5);
            yield return Click(ButtonPosition(Field<Button>(settings, "Button_CloseButton")));
            yield return Wait(() => !settings.IsSettingsPanelOpen && !originalEntry.gameObject.activeSelf,
                "关闭设置后恢复大厅入口作用域", 5);
            yield return UIManager.Instance.CloseAsync<MainMenuView>(animated: false).ToCoroutine();
            Assert.That(originalEntry.gameObject.activeSelf, Is.True, "离开大厅应释放入口抑制，不影响其他 Demo。");
            yield return UIManager.Instance.ShowAsync<MainMenuView>(new UIShowOptions(false)).ToCoroutine();
        }

        private IEnumerator Tap(Vector2 position)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Began }); yield return null;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Ended }); yield return null; yield return null;
        }

        private IEnumerator PrepareHub()
        {
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                Assert.That(startup, Is.Not.Null);
                yield return Wait(() => startup.isDone, "AppEntrance 启动", 90);
            }
            yield return Wait(IsStableHub, "Hub 稳定", 90);
            originalInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            originalNavigator = GameSceneNavigator.Instance;
            runtime = new FailingSceneRuntime();
            SetNavigator(new GameSceneNavigator(runtime, new GameSceneLoadingPresenter()));
            // 每项测试从正常新开页面开始，不能把上一项指针清焦点后的页面当首次显示。
            UIOperationResult closed = default;
            yield return UIManager.Instance.CloseAsync<MainMenuView>(animated: false).ToCoroutine(result => closed = result);
            Assert.That(closed.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            UIOperationResult shown = default;
            yield return UIManager.Instance.ShowAsync<MainMenuView>(new UIShowOptions(false)).ToCoroutine(result => shown = result);
            Assert.That(shown.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                if (gamepad != null && gamepad.added) InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                if (runtime?.HasPendingEntry == true)
                {
                    LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenuView\] 无法进入 .*：测试场景加载失败"));
                    runtime.FailEntry();
                    yield return Wait(IsStableHub, "清理待完成的进入请求", 15);
                }
            }
            finally
            {
                if (originalNavigator != null) SetNavigator(originalNavigator);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
                if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);
                if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
                if (testInputSettings != null) Object.Destroy(testInputSettings);
            }
        }

        private IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
        }

        private static Vector2 ButtonPosition(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera, rect.TransformPoint(rect.rect.center));
            Assert.That(position.x, Is.InRange(0, Screen.width));
            Assert.That(position.y, Is.InRange(0, Screen.height));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), "实际入口不能被其它 UI 遮挡。");
            return position;
        }

        private static void SetNavigator(GameSceneNavigator navigator) =>
            typeof(GameSceneNavigator).GetProperty(nameof(GameSceneNavigator.Instance)).SetValue(null, navigator);

        private static IEnumerator WaitForInitialSelection(MainMenuView menu) => Wait(() =>
            EventSystem.current.sendNavigationEvents && EventSystem.current.currentSelectedGameObject ==
                (Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_Status").text.Contains("进入失败")
                    ? Field<Button>(menu, "Button_Start").gameObject : CardForScene(menu, GameSceneId.DroneFlight).gameObject),
            "公共输入恢复 Hub 默认焦点", 5);

        private static GameSceneId SelectedScene(MainMenuView menu)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected?.GetComponent<LoopScrollMenuButton>(), Is.Not.Null, "焦点必须落在列表的真实按钮。");
            var cell = selected.GetComponentInParent<LoopCell>();
            Assert.That(cell.Context.IsCurrent, Is.True);
            var entry = Field<List<MainMenuDemoEntry>>(menu, "entries")[cell.Context.Index];
            Assert.That(entry.SceneId.HasValue, Is.True, "焦点必须落在当前绑定的已开放 Demo 卡片。");
            return entry.SceneId.Value;
        }

        private static IEnumerable<(Button Button, MainMenuDemoEntry Entry)> VisibleCards(MainMenuView menu)
        {
            var list = Field<LoopScrollView>(menu, "LoopScrollView_DemoList");
            var entries = Field<List<MainMenuDemoEntry>>(menu, "entries");
            for (int i = 0; i < list.Count; i++)
            {
                var cell = list.GetVisibleCell(i);
                if (cell != null && cell.Context.IsCurrent)
                    yield return (cell.GetComponentInChildren<LoopScrollMenuButton>(true), entries[i]);
            }
        }

        private static Button CardForScene(MainMenuView menu, GameSceneId target)
        {
            foreach (var card in VisibleCards(menu)) if (card.Entry.SceneId == target) return card.Button;
            Assert.Fail("目标 Demo 卡片未绑定：" + target);
            return null;
        }

        private static bool SelectedCardIsVisible()
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            var card = selected != null ? selected.GetComponent<LoopScrollMenuButton>() : null;
            if (card == null) return false;
            var list = card.GetComponentInParent<LoopScrollView>();
            var cell = card.GetComponentInParent<LoopCell>();
            if (list == null || cell == null || !cell.Context.IsCurrent) return false;
            var viewport = list.ScrollRect.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, cell.RectTransform);
            return bounds.min.y >= viewport.rect.yMin - 1 && bounds.max.y <= viewport.rect.yMax + 1 &&
                bounds.min.x >= viewport.rect.xMin - 1 && bounds.max.x <= viewport.rect.xMax + 1;
        }

        private void AssertRequestedScene(GameSceneId target)
        {
            Assert.That(GameSceneCatalog.TryGet(target, out var definition), Is.True);
            Assert.That(runtime.LastAddress, Is.EqualTo(definition.Address), "选中物件和实际进入目标必须一致。");
        }

        private static void ExpectEntryFailure(GameSceneId target) =>
            LogAssert.Expect(LogType.Error, new Regex($@"\[MainMenuView\] 无法进入 {target}：测试场景加载失败"));

        private static T Field<T>(object instance, string name) =>
            (T)instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(instance);

        private static bool IsStableHub() => GameSceneNavigator.Instance != null &&
            GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning &&
            UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible &&
            !UIRootManager.Instance.InteractionGate.IsBlocking && Object.FindFirstObjectByType<StartupLoadingView>() == null;

        private static IEnumerator Wait(Func<bool> predicate, string reason, float timeout)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, reason + "超时");
        }

        private sealed class FailingSceneRuntime : IGameSceneRuntime
        {
            private UniTaskCompletionSource<GameSceneRuntimeResult> pending;
            internal int LoadCount { get; private set; }
            internal string LastAddress { get; private set; }
            internal bool HasPendingEntry => pending != null;

            public async UniTask<GameSceneRuntimeResult> LoadAsync(string address, Action<float> onProgress)
            {
                LoadCount++;
                LastAddress = address;
                pending = new UniTaskCompletionSource<GameSceneRuntimeResult>();
                return await pending.Task;
            }

            internal void FailEntry()
            {
                var completion = pending;
                pending = null;
                completion.TrySetResult(GameSceneRuntimeResult.Failure("测试场景加载失败"));
            }

            public UniTask<GameSceneRuntimeResult> ReturnToHubAsync(Action<float> onProgress) =>
                UniTask.FromResult(GameSceneRuntimeResult.Success());
        }
    }
}
#endif
