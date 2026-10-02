#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.SceneManagement;
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
            var button = Field<Button>(firstMenu, "Button_JinxCasinoButton");
            yield return null; yield return null;
            Vector2 position = ButtonPosition(button);
            yield return Click(position);
            yield return Wait(() => runtime.LoadCount == 1 && firstMenu.State == ViewState.Destroyed, "Loading 替换旧 Hub", 15);
            yield return Click(position);
            Assert.That(runtime.LoadCount, Is.EqualTo(1), "加载期间重复点击不能再发起进入请求。");

            LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenuView\] 无法进入 JinxCasino：测试场景加载失败"));
            runtime.FailEntry();
            yield return Wait(IsStableHub, "加载失败恢复 Hub", 15);
            var restoredMenu = UIManager.Instance.Get<MainMenuView>();
            Assert.That(restoredMenu, Is.Not.SameAs(firstMenu));
            var feedback = Field<TMPro.TextMeshProUGUI>(restoredMenu, "TextMeshProUGUI_Title");
            Assert.That(feedback.text, Does.Contain("进入失败"));
            var feedbackSize = feedback.GetPreferredValues();
            Assert.That(feedbackSize.x, Is.LessThanOrEqualTo(feedback.rectTransform.rect.width), "失败提示应在已有控件内完整显示。");
            Assert.That(feedbackSize.y, Is.LessThanOrEqualTo(feedback.rectTransform.rect.height), "失败提示不能溢出遮住入口。");
            Assert.That(Field<Button>(restoredMenu, "Button_UIFrameworkValidationButton").interactable, Is.False);
            foreach (string field in new[] { "Button_DroneFlightButton", "Button_DlssButton", "Button_BlockPortersButton", "Button_JinxCasinoButton" })
                Assert.That(Field<Button>(restoredMenu, field).interactable, Is.True, field);

            // 新实例需要经过帧末 Canvas 注册/布局；随后仍验证真实射线并通过 InputSystem 点击。
            yield return null; yield return null;
            yield return Click(ButtonPosition(Field<Button>(restoredMenu, "Button_JinxCasinoButton")));
            yield return Wait(() => runtime.LoadCount == 2, "新 Hub 可以实际点击重试", 15);
            LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenuView\] 无法进入 JinxCasino：测试场景加载失败"));
            runtime.FailEntry();
            yield return Wait(IsStableHub, "重试失败也能恢复", 15);
            Assert.That(Field<Button>(UIManager.Instance.Get<MainMenuView>(), "Button_JinxCasinoButton").interactable, Is.True);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator KeyboardAndGamepadNavigateEnterAndRecoverWithoutRepeatingHeldSubmit()
        {
            yield return PrepareHub();
            var firstMenu = UIManager.Instance.Get<MainMenuView>();
            yield return WaitForInitialSelection(firstMenu);
            var firstSelection = EventSystem.current.currentSelectedGameObject;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.EqualTo(firstSelection), "方向键应移动真实焦点。");
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
            AssertRequestedScene(GameSceneId.DroneFlight);
            ExpectEntryFailure(GameSceneId.DroneFlight);
            runtime.FailEntry();
            yield return Wait(IsStableHub, "再次进入结束后收口", 15);
            yield return WaitForInitialSelection(UIManager.Instance.Get<MainMenuView>());
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
            EventSystem.current.sendNavigationEvents && EventSystem.current.currentSelectedGameObject == Field<Button>(menu, "Button_DroneFlightButton").gameObject,
            "公共输入恢复 Hub 默认焦点", 5);

        private static GameSceneId SelectedScene(MainMenuView menu)
        {
            foreach (var entry in new[]
            {
                ("Button_DroneFlightButton", GameSceneId.DroneFlight), ("Button_DlssButton", GameSceneId.Dlss),
                ("Button_BlockPortersButton", GameSceneId.BlockPorters), ("Button_JinxCasinoButton", GameSceneId.JinxCasino)
            })
                if (EventSystem.current.currentSelectedGameObject == Field<Button>(menu, entry.Item1).gameObject) return entry.Item2;
            Assert.Fail("焦点必须落在已开放的 Demo 入口。");
            return default;
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
