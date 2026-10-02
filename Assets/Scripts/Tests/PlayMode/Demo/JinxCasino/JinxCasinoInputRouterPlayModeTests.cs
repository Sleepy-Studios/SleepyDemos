using System.Collections;
using System.IO;
using Hotfix.JinxCasino.Adapters.Input;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 用真实InputAction和Core同类UI模块验证路由，不另起测试Runner，不模拟已执行的领域资金操作。
    public sealed class JinxCasinoInputRouterPlayModeTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private Touchscreen touchscreen;
        private RecordingCasinoGamepad gamepad;
        private InputActionAsset source;
        private JinxCasinoInputRouter router;
        private JinxCasinoMenuInputScope navigation;
        private GameObject ownedEventSystem;
        private GameObject ownedButton;
        private string layoutName;
        private InputSettings originalSettings;
        private InputSettings testSettings;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            // 合成输入不能依赖操作者当前聚焦哪个Editor窗口；只借用临时设置，不修改项目资产。
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            layoutName = "JinxCasinoRecordingGamepad" + System.Guid.NewGuid().ToString("N");
            InputSystem.RegisterLayout<RecordingCasinoGamepad>(layoutName);
            gamepad = (RecordingCasinoGamepad)InputSystem.AddDevice(layoutName);
            // 验证实际交付的动作配置，避免只测工厂而遗漏保存资产中的绑定差异。
            source = InputActionAsset.FromJson(File.ReadAllText("Assets/LoadResources/Demos/jinx_casino/Data/JinxCasinoImmersion.inputactions"));
            source.devices = new InputDevice[] { keyboard, mouse, gamepad, touchscreen };
            router = new JinxCasinoInputRouter(source);
            yield return null;
            router.ReadFrame(0.016f); router.ConsumeActions();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            navigation?.Dispose(); navigation = null; router?.Dispose(); router = null;
            if (ownedButton != null) Object.Destroy(ownedButton);
            if (ownedEventSystem != null) Object.Destroy(ownedEventSystem);
            if (source != null) Object.Destroy(source);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);
            if (layoutName != null) InputSystem.RemoveLayout(layoutName);
            if (originalSettings != null) InputSystem.settings = originalSettings;
            if (testSettings != null) Object.Destroy(testSettings);
            originalSettings = null; testSettings = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator CursorRelockDiscardsOnlyOneLookDeltaAndKeepsDiscreteInteraction()
        {
            router.DiscardNextLookDelta();
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(100, 60) });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            Assert.That(router.ReadFrame(.016f, pointerLocked: true).LookDegrees, Is.EqualTo(Vector2.zero));
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.Interact));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(20, 10) });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(router.ReadFrame(.016f, pointerLocked: true).LookDegrees.sqrMagnitude, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator ExplorationInteractCannotBecomeHeldTableConfirmOrNextGroup()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.Interact));
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.None));
            router.SetContext(JinxCasinoInputContext.Table); yield return null;
            router.ReadFrame(0.016f);
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.None));
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            // 与宿主Update一致逐帧采样：新启用的Button没有按下阶段，不保证发canceled回调。
            router.ReadFrame(0.016f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.Confirm));
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            router.SetContext(JinxCasinoInputContext.Exploration);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.Interact));
            router.SetContext(JinxCasinoInputContext.Table); yield return null;
            router.ReadFrame(0.016f);
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.None), "探索E不能在新桌面重复作为NextGroup。");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            router.ReadFrame(0.016f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.NextGroup));
            Assert.That(source.enabled, Is.False, "私有Map副本不能启用保存的源资产。");
        }

        [UnityTest]
        public IEnumerator TableHasHelpAndGroupsWhileTouchUsesSameValidatedActionQueue()
        {
            router.SetContext(JinxCasinoInputContext.Table); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.North).WithButton(GamepadButton.LeftShoulder).WithButton(GamepadButton.RightShoulder)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.Help | JinxCasinoInputActions.PreviousGroup | JinxCasinoInputActions.NextGroup));
            Assert.That(router.DeviceKind, Is.EqualTo(JinxCasinoInputDeviceKind.Gamepad));
            Assert.That(router.QueueTouchAction(JinxCasinoInputActions.Help), Is.True);
            Assert.That(router.DeviceKind, Is.EqualTo(JinxCasinoInputDeviceKind.Touch));
            Assert.That(router.QueueTouchAction(JinxCasinoInputActions.Interact), Is.False);
            Assert.That(router.QueueTouchAction(JinxCasinoInputActions.Confirm | JinxCasinoInputActions.Back), Is.False);
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.Help));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.Back), "桌面Esc只返回，不同时触发Pause。");
            router.SetContext(JinxCasinoInputContext.Menu);
            Assert.That(router.QueueTouchAction(JinxCasinoInputActions.Confirm), Is.False, "菜单不提供第二条Submit路径。");
        }

        [UnityTest]
        public IEnumerator RawGamepadDeadzoneAndMouseDeltaHaveDistinctRealActionSemantics()
        {
            var settings = new JinxCasinoInputSettings { GamepadDeadzone = 0.05f };
            router.ApplySettings(settings);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(0.1f, 0), rightStick = new Vector2(0.5f, 0) }); yield return null;
            var frame = router.ReadFrame(0.1f);
            Assert.That(frame.Move.x, Is.EqualTo(0.05f / 0.95f).Within(0.0001f), "不能叠加全局默认死区吞掉0.1输入。");
            Assert.That(frame.LookDegrees.x, Is.EqualTo(0.45f / 0.95f * 9).Within(0.0001f));
            Assert.That(frame.DeviceKind, Is.EqualTo(JinxCasinoInputDeviceKind.Gamepad));
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2, delta = new Vector2(10, 0) }); yield return null;
            var mouseFrame = router.ReadFrame(0.1f);
            Assert.That(mouseFrame.LookDegrees.x, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(mouseFrame.DeviceKind, Is.EqualTo(JinxCasinoInputDeviceKind.KeyboardMouse));
        }

        [UnityTest]
        public IEnumerator DeviceLossAndForegroundNeverAutomaticallyResumeAndRumbleStops()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            router.ConsumeActions(); Assert.That(router.PlayRumble(0.5f, 0.3f, 1), Is.True);
            Assert.That(gamepad.LowMotor, Is.EqualTo(0.5f));
            router.PauseState.SetApplicationFocus(false);
            Assert.That(gamepad.LowMotor, Is.Zero); Assert.That(gamepad.HighMotor, Is.Zero);
            router.SetTouchFrame(Vector2.right, Vector2.one, true);
            Assert.That(router.ReadFrame(0.016f).Move, Is.EqualTo(Vector2.zero));
            router.PauseState.SetApplicationFocus(true);
            Assert.That(router.PauseState.IsPaused, Is.True); Assert.That(router.PauseState.TryResume(), Is.True);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(router.DeviceKind, Is.EqualTo(JinxCasinoInputDeviceKind.Gamepad));
            InputSystem.RemoveDevice(gamepad);
            Assert.That(router.PauseState.IsPaused, Is.True); Assert.That(router.PauseState.TryResume(), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
            Assert.That(router.DeviceKind, Is.EqualTo(JinxCasinoInputDeviceKind.KeyboardMouse));
            Assert.That(router.PauseState.CanResume, Is.True); Assert.That(router.PauseState.IsPaused, Is.True);
            Assert.That(router.PauseState.TryResume(), Is.True);
            Assert.That(router.PlayRumble(1, 1, 1), Is.False, "键鼠替代后不能向已断连手柄发送新震动。");
        }

        [UnityTest]
        public IEnumerator CoreMenuOwnsExactlyOneSubmitAndTableUsesIndependentFocus()
        {
            var system = EventSystem.current;
            if (system == null)
            {
                ownedEventSystem = new GameObject("Casino input test EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                system = ownedEventSystem.GetComponent<EventSystem>();
            }
            Assert.That(system.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            yield return null;
            bool original = system.sendNavigationEvents;
            ownedButton = new GameObject("Casino input test saved-selection stand-in", typeof(RectTransform), typeof(Image), typeof(Button));
            int submits = 0; ownedButton.GetComponent<Button>().onClick.AddListener(() => submits++);
            navigation = new JinxCasinoMenuInputScope(system, router);
            router.SetContext(JinxCasinoInputContext.Table); navigation.SetContext(JinxCasinoInputContext.Table);
            system.SetSelectedGameObject(ownedButton);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.Confirm)); Assert.That(submits, Is.Zero);
            router.SetContext(JinxCasinoInputContext.Menu); navigation.SetContext(JinxCasinoInputContext.Menu, ownedButton);
            yield return null; navigation.Update();
            Assert.That(system.sendNavigationEvents, Is.False, "开菜单的A保持按住时不允许Core重复提交。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; navigation.Update(); yield return null;
            Assert.That(system.sendNavigationEvents, Is.True); Assert.That(system.currentSelectedGameObject, Is.EqualTo(ownedButton));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(submits, Is.EqualTo(1)); Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.None));
            navigation.Dispose(); navigation = null; Assert.That(system.sendNavigationEvents, Is.EqualTo(original));
        }

        [UnityTest]
        public IEnumerator RumbleExpiresAndDisposeStopsTheOwnedDevice()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(router.PlayRumble(0.5f, 0.25f, 0.1f), Is.True);
            router.ReadFrame(0.2f); Assert.That(gamepad.LowMotor, Is.Zero); Assert.That(gamepad.HighMotor, Is.Zero);
            Assert.That(router.PlayRumble(1, 1, 1), Is.True);
            router.Dispose(); router = null;
            Assert.That(gamepad.LowMotor, Is.Zero); Assert.That(gamepad.HighMotor, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisabledGamepadStopsMotorsAndRequiresExplicitResume()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            router.ReadFrame(0.016f);
            Assert.That(router.PlayRumble(0.5f, 0.25f, 1), Is.True);
            InputSystem.DisableDevice(gamepad); yield return null;
            Assert.That(gamepad.enabled, Is.False);
            Assert.That(gamepad.LowMotor, Is.Zero);
            Assert.That(gamepad.HighMotor, Is.Zero);
            Assert.That(router.PauseState.TryResume(), Is.False);
            InputSystem.EnableDevice(gamepad); yield return null;
            Assert.That(router.PauseState.IsPaused, Is.True);
            Assert.That(router.PauseState.TryResume(), Is.True);
        }

        [UnityTest]
        public IEnumerator ReturningToLockedExplorationDropsWarpDeltaThenUsesMouseWithoutRightHold()
        {
            router.SetContext(JinxCasinoInputContext.Table); yield return null;
            router.SetContext(JinxCasinoInputContext.Exploration);
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(400, 0) }); yield return null;
            Assert.That(router.ReadFrame(0.016f, 0.12f, true).LookDegrees, Is.EqualTo(Vector2.zero));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(10, 0) }); yield return null;
            Assert.That(router.ReadFrame(0.016f, 0.12f, true).LookDegrees.x, Is.EqualTo(1.2f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator TablePointerProvidesScreenPositionAndConsumesEachMousePressOnce()
        {
            router.SetContext(JinxCasinoInputContext.Table);
            yield return null; router.ReadFrame(0.016f);
            var position = new Vector2(310, 225);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return null;
            var frame = router.ReadFrame(0.016f);
            Assert.That(frame.PointerPosition, Is.EqualTo(position));
            Assert.That(frame.PointerPressed, Is.True);
            Assert.That(frame.PointerMoved, Is.True);
            Assert.That(router.ReadFrame(0.016f).PointerPressed, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return null;
            var keyboardNavigation = router.ReadFrame(0.016f);
            Assert.That(keyboardNavigation.TableNavigation.x, Is.GreaterThan(0));
            Assert.That(keyboardNavigation.PointerMoved, Is.False, "静止指针不得覆盖键盘导航。");
            Assert.That(router.ConsumeActions(), Is.EqualTo(JinxCasinoInputActions.None), "实体点击不得重复变成菜单/手柄确认。");
            router.SetContext(JinxCasinoInputContext.Menu);
            Assert.That(router.ReadFrame(0.016f).PointerPressed, Is.False);
            router.SetContext(JinxCasinoInputContext.Table);
            yield return null; router.ReadFrame(0.016f);
            Assert.That(router.ReadFrame(0.016f).PointerPressed, Is.False, "按住切回桌面不能重复点击。");
        }

        [UnityTest]
        public IEnumerator TouchscreenUsesSamePointerFrameAndPauseDiscardsPendingPress()
        {
            router.SetContext(JinxCasinoInputContext.Table);
            yield return null; router.ReadFrame(0.016f);
            var position = new Vector2(400, 250);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Began });
            yield return null;
            var frame = router.ReadFrame(0.016f);
            Assert.That(frame.PointerPosition, Is.EqualTo(position));
            Assert.That(frame.PointerPressed, Is.True);
            Assert.That(frame.DeviceKind, Is.EqualTo(JinxCasinoInputDeviceKind.Touch));
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Ended });
            yield return null; router.ReadFrame(0.016f);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 2, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Began });
            yield return null;
            router.PauseState.RequestPause(JinxCasinoPauseReason.User);
            Assert.That(router.ReadFrame(0.016f).PointerPressed, Is.False);
            Assert.That(router.PauseState.TryResume(), Is.True);
            Assert.That(router.ReadFrame(0.016f).PointerPressed, Is.False);
        }

        public sealed class RecordingCasinoGamepad : Gamepad
        {
            public float LowMotor { get; private set; }
            public float HighMotor { get; private set; }

            /// <summary>记录实际Router发出的电机命令，保持InputDevice与真实Action解析路径。</summary>
            /// <param name="lowFrequency">低频电机强度。</param>
            /// <param name="highFrequency">高频电机强度。</param>
            public override void SetMotorSpeeds(float lowFrequency, float highFrequency)
            {
                LowMotor = lowFrequency; HighMotor = highFrequency;
            }
        }
    }
}
