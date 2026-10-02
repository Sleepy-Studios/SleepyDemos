using System.Collections;
using Core.Runtime.Inputs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    /// 用真实InputAction和Core同类UI模块验证路由，不另起测试Runner，不模拟已执行的领域资金操作。
    public sealed class GameplayInputRouterTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private Touchscreen touchscreen;
        private RecordingInputGamepad gamepad;
        private InputActionAsset source;
        private GameplayInputRouter router;
        private MenuInputScope navigation;
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
            layoutName = "RecordingInputGamepad" + System.Guid.NewGuid().ToString("N");
            InputSystem.RegisterLayout<RecordingInputGamepad>(layoutName);
            gamepad = (RecordingInputGamepad)InputSystem.AddDevice(layoutName);
            // 公共模块使用独立测试动作；Demo入口回归另外验证实际保存资产。
            source = CreateTestActions();
            source.devices = new InputDevice[] { keyboard, mouse, gamepad, touchscreen };
            router = new GameplayInputRouter(source);
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
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.Interact));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(20, 10) });
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(router.ReadFrame(.016f, pointerLocked: true).LookDegrees.sqrMagnitude, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator ExplorationInteractCannotBecomeHeldTableConfirmOrNextGroup()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.Interact));
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.None));
            router.SetContext(GameplayInputContext.Interaction); yield return null;
            router.ReadFrame(0.016f);
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.None));
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            // 与宿主Update一致逐帧采样：新启用的Button没有按下阶段，不保证发canceled回调。
            router.ReadFrame(0.016f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.Confirm));
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            router.SetContext(GameplayInputContext.Gameplay);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.Interact));
            router.SetContext(GameplayInputContext.Interaction); yield return null;
            router.ReadFrame(0.016f);
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.None), "探索E不能在新桌面重复作为NextGroup。");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            router.ReadFrame(0.016f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.NextGroup));
            Assert.That(source.enabled, Is.False, "私有Map副本不能启用保存的源资产。");
        }

        [UnityTest]
        public IEnumerator TableHasHelpAndGroupsWhileTouchUsesSameValidatedActionQueue()
        {
            router.SetContext(GameplayInputContext.Interaction); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.North).WithButton(GamepadButton.LeftShoulder).WithButton(GamepadButton.RightShoulder)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.Help | GameplayInputActions.PreviousGroup | GameplayInputActions.NextGroup));
            Assert.That(router.DeviceKind, Is.EqualTo(InputDeviceKind.Gamepad));
            Assert.That(router.QueueTouchAction(GameplayInputActions.Help), Is.True);
            Assert.That(router.DeviceKind, Is.EqualTo(InputDeviceKind.Touch));
            Assert.That(router.QueueTouchAction(GameplayInputActions.Interact), Is.False);
            Assert.That(router.QueueTouchAction(GameplayInputActions.Confirm | GameplayInputActions.Back), Is.False);
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.Help));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.Back), "桌面Esc只返回，不同时触发Pause。");
            router.SetContext(GameplayInputContext.Menu);
            Assert.That(router.QueueTouchAction(GameplayInputActions.Confirm), Is.False, "菜单不提供第二条Submit路径。");
        }

        [UnityTest]
        public IEnumerator RawGamepadDeadzoneAndMouseDeltaHaveDistinctRealActionSemantics()
        {
            var settings = new GameplayInputSettings { GamepadDeadzone = 0.05f };
            router.ApplySettings(settings);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(0.1f, 0), rightStick = new Vector2(0.5f, 0) }); yield return null;
            var frame = router.ReadFrame(0.1f);
            Assert.That(frame.Move.x, Is.EqualTo(0.05f / 0.95f).Within(0.0001f), "不能叠加全局默认死区吞掉0.1输入。");
            Assert.That(frame.LookDegrees.x, Is.EqualTo(0.45f / 0.95f * 9).Within(0.0001f));
            Assert.That(frame.DeviceKind, Is.EqualTo(InputDeviceKind.Gamepad));
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2, delta = new Vector2(10, 0) }); yield return null;
            var mouseFrame = router.ReadFrame(0.1f);
            Assert.That(mouseFrame.LookDegrees.x, Is.EqualTo(1.2f).Within(0.0001f));
            Assert.That(mouseFrame.DeviceKind, Is.EqualTo(InputDeviceKind.KeyboardMouse));
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
            Assert.That(router.DeviceKind, Is.EqualTo(InputDeviceKind.Gamepad));
            InputSystem.RemoveDevice(gamepad);
            Assert.That(router.PauseState.IsPaused, Is.True); Assert.That(router.PauseState.TryResume(), Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
            Assert.That(router.DeviceKind, Is.EqualTo(InputDeviceKind.KeyboardMouse));
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
            navigation = new MenuInputScope(system, router);
            router.SetContext(GameplayInputContext.Interaction); navigation.SetContext(GameplayInputContext.Interaction);
            system.SetSelectedGameObject(ownedButton);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.Confirm)); Assert.That(submits, Is.Zero);
            router.SetContext(GameplayInputContext.Menu); navigation.SetContext(GameplayInputContext.Menu, ownedButton);
            yield return null; navigation.Update();
            Assert.That(system.sendNavigationEvents, Is.False, "开菜单的A保持按住时不允许Core重复提交。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; navigation.Update(); yield return null;
            Assert.That(system.sendNavigationEvents, Is.True); Assert.That(system.currentSelectedGameObject, Is.EqualTo(ownedButton));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            Assert.That(submits, Is.EqualTo(1)); Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.None));
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
            router.SetContext(GameplayInputContext.Interaction); yield return null;
            router.SetContext(GameplayInputContext.Gameplay);
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(400, 0) }); yield return null;
            Assert.That(router.ReadFrame(0.016f, 0.12f, true).LookDegrees, Is.EqualTo(Vector2.zero));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(10, 0) }); yield return null;
            Assert.That(router.ReadFrame(0.016f, 0.12f, true).LookDegrees.x, Is.EqualTo(1.2f).Within(0.0001f));
        }

        [UnityTest]
        public IEnumerator TablePointerProvidesScreenPositionAndConsumesEachMousePressOnce()
        {
            router.SetContext(GameplayInputContext.Interaction);
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
            Assert.That(keyboardNavigation.InteractionNavigation.x, Is.GreaterThan(0));
            Assert.That(keyboardNavigation.PointerMoved, Is.False, "静止指针不得覆盖键盘导航。");
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.None), "实体点击不得重复变成菜单/手柄确认。");
            router.SetContext(GameplayInputContext.Menu);
            Assert.That(router.ReadFrame(0.016f).PointerPressed, Is.False);
            router.SetContext(GameplayInputContext.Interaction);
            yield return null; router.ReadFrame(0.016f);
            Assert.That(router.ReadFrame(0.016f).PointerPressed, Is.False, "按住切回桌面不能重复点击。");
        }

        [UnityTest]
        public IEnumerator TouchscreenUsesSamePointerFrameAndPauseDiscardsPendingPress()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return null; router.ReadFrame(.016f);
            Assert.That(router.DeviceKind, Is.EqualTo(InputDeviceKind.Gamepad));
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 9, position = new Vector2(300, 200),
                phase = UnityEngine.InputSystem.TouchPhase.Began });
            yield return null;
            Assert.That(router.ReadFrame(.016f).DeviceKind, Is.EqualTo(InputDeviceKind.Touch), "未绑定触屏玩法动作的探索态也能找回触控入口。");
            Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.None), "识别触摸不能额外提交玩法操作。");
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 9, position = new Vector2(300, 200),
                phase = UnityEngine.InputSystem.TouchPhase.Ended });
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            router.SetContext(GameplayInputContext.Interaction);
            yield return null; router.ReadFrame(0.016f);
            var position = new Vector2(400, 250);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Began });
            yield return null;
            var frame = router.ReadFrame(0.016f);
            Assert.That(frame.PointerPosition, Is.EqualTo(position));
            Assert.That(frame.PointerPressed, Is.True);
            Assert.That(frame.DeviceKind, Is.EqualTo(InputDeviceKind.Touch));
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Ended });
            yield return null; router.ReadFrame(0.016f);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 2, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Began });
            yield return null;
            router.PauseState.RequestPause(LocalPauseReason.User);
            Assert.That(router.ReadFrame(0.016f).PointerPressed, Is.False);
            Assert.That(router.PauseState.TryResume(), Is.True);
            Assert.That(router.ReadFrame(0.016f).PointerPressed, Is.False);
        }

        public sealed class RecordingInputGamepad : Gamepad
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
        // 独立测试夹具，仅驻留内存，由TearDown销毁，不生成生产资产。
        private static InputActionAsset CreateTestActions()
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            asset.name = "TestGameplayInput";
            var exploration = asset.AddActionMap("Gameplay");
            AddKeyboardVector(exploration.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2"), true);
            exploration.AddAction("PadMove", InputActionType.Value, "<Gamepad>/leftStick", expectedControlLayout: "Vector2");
            exploration.AddAction("MouseLook", InputActionType.Value, "<Mouse>/delta", expectedControlLayout: "Vector2");
            AddButton(exploration, "LookHold", "<Mouse>/rightButton");
            exploration.AddAction("PadLook", InputActionType.Value, "<Gamepad>/rightStick", expectedControlLayout: "Vector2");
            AddButton(exploration, "Interact", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            AddButton(exploration, "Pause", "<Keyboard>/escape", "<Gamepad>/start");
            var table = asset.AddActionMap("Interaction");
            table.AddAction("Point", InputActionType.PassThrough, "<Pointer>/position", expectedControlLayout: "Vector2");
            AddButton(table, "Click", "<Pointer>/press");
            AddKeyboardVector(table.AddAction("Navigate", InputActionType.Value, expectedControlLayout: "Vector2"), false);
            var padNavigate = table.AddAction("PadNavigate", InputActionType.Value, expectedControlLayout: "Vector2");
            padNavigate.AddBinding("<Gamepad>/leftStick"); padNavigate.AddBinding("<Gamepad>/dpad");
            AddButton(table, "Confirm", "<Keyboard>/enter", "<Keyboard>/space", "<Gamepad>/buttonSouth");
            AddButton(table, "Back", "<Keyboard>/escape", "<Gamepad>/buttonEast");
            AddButton(table, "Secondary", "<Keyboard>/r", "<Gamepad>/buttonWest");
            AddButton(table, "Help", "<Keyboard>/h", "<Gamepad>/buttonNorth");
            AddButton(table, "PreviousGroup", "<Keyboard>/q", "<Gamepad>/leftShoulder");
            AddButton(table, "NextGroup", "<Keyboard>/e", "<Gamepad>/rightShoulder");
            AddButton(table, "Pause", "<Gamepad>/start");
            var menu = asset.AddActionMap("Menu");
            // Menu的Submit/Cancel/Navigate由Core现有InputSystemUIInputModule唯一处理。
            AddButton(menu, "Pause", "<Gamepad>/start");
            return asset;
        }

        private static void AddButton(InputActionMap map, string name, params string[] paths)
        {
            var action = map.AddAction(name, InputActionType.Button, interactions: "Press(behavior=0)", expectedControlLayout: "Button");
            foreach (string path in paths) action.AddBinding(path);
        }

        private static void AddKeyboardVector(InputAction action, bool wasd)
        {
            var binding = action.AddCompositeBinding("2DVector");
            binding.With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            if (wasd) binding.With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
        }

    }
}
