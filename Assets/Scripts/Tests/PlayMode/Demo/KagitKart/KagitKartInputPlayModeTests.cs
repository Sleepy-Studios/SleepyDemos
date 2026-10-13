#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Core.Runtime.Inputs;
using Core.Runtime.Settings;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
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
    public sealed class KagitKartInputPlayModeTests
    {
        private const string AssetPath = "Assets/LoadResources/Demos/kagit_kart/Data/PlayerInput.asset";
        private InputActionAsset source;
        private KagitKartInput input;
        private InputDevice device;
        private Touchscreen touchDevice;
        private InputSettings originalSettings;
        private InputSettings testSettings;
        private InputDevice[] existingDevices;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            // 全局设备识别也参与测试，真实鼠标和键盘不能抢占模拟设备。
            existingDevices = InputSystem.devices.Where(value => value.enabled).ToArray();
            foreach (var value in existingDevices)
            {
                InputSystem.DisableDevice(value);
            }
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            input?.Dispose();
            if (device != null && device.added)
            {
                InputSystem.RemoveDevice(device);
            }
            if (touchDevice != null && touchDevice.added)
            {
                InputSystem.RemoveDevice(touchDevice);
            }
            if (source != null)
            {
                source.devices = null;
            }
            foreach (var value in existingDevices)
            {
                if (value.added)
                {
                    InputSystem.EnableDevice(value);
                }
            }
            InputSystem.settings = originalSettings;
            Object.Destroy(testSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedKeyboardBindingsDriveAllCommandsAndPause()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Initialize(keyboard);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.Space, Key.E, Key.C));
            yield return null;
            yield return null;
            var controls = input.ReadControls(false);
            Assert.That(controls.Steer, Is.EqualTo(1));
            Assert.That(controls.Throttle, Is.EqualTo(1));
            Assert.That(controls.Drift, Is.True);
            Assert.That(controls.Item, Is.True);
            Assert.That(controls.LookBack, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow, Key.DownArrow, Key.LeftShift, Key.X, Key.P));
            yield return null;
            controls = input.ReadControls(false);
            Assert.That(controls.Steer, Is.EqualTo(-1));
            Assert.That(controls.Brake, Is.EqualTo(1));
            Assert.That(controls.Throttle, Is.Zero);
            Assert.That(controls.Drift, Is.True);
            Assert.That(controls.Item, Is.True);
            Assert.That(input.Pressed("Pause"), Is.True);
            Assert.That(source.enabled, Is.False);
            Assert.That(input.PromptAction("Throttle").actionMap.asset, Is.SameAs(input.Asset));
        }

        [UnityTest]
        public IEnumerator MouseActivityKeepsHeldKeyboardControlsAndPendingShortPress()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            touchDevice = InputSystem.AddDevice<Touchscreen>();
            Initialize(keyboard);
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.Space, Key.E));
                yield return null;
                yield return null;
                input.CaptureFrame();
                InputDeviceState.Notify(mouse);
                var controls = input.ReadControls(false);
                Assert.That(controls.Throttle, Is.EqualTo(1));
                Assert.That(controls.Steer, Is.EqualTo(1));
                Assert.That(controls.Drift && controls.Item, Is.True);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
                yield return null;
                input.CaptureFrame();
                InputDeviceState.Notify(mouse);
                controls = input.ReadControls(false);
                Assert.That(controls.Throttle, Is.EqualTo(1), "鼠标返回键盘、键盘返回鼠标都不能锁住持续驾驶。");
                Assert.That(controls.Steer, Is.EqualTo(1));

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.E));
                yield return null;
                input.CaptureFrame();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
                yield return null;
                InputDeviceState.Notify(mouse);
                Assert.That(input.ReadControls(false).Item, Is.True, "无模拟步时缓存的短按不能被鼠标活动清掉。");
                Assert.That(input.ReadControls(false).Item, Is.False);

                InputDeviceState.NotifyTouch();
                InputDeviceState.Notify(keyboard);
                controls = input.ReadControls(false);
                Assert.That(controls.Throttle, Is.Zero, "离开键鼠上下文时，即使最后活动的是鼠标，也要锁住旧键盘保持输入。");
                Assert.That(controls.Steer, Is.Zero);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D, Key.E));
                yield return null;
                controls = input.ReadControls(false);
                Assert.That(controls.Throttle, Is.EqualTo(1));
                Assert.That(controls.Steer, Is.EqualTo(1));
                Assert.That(controls.Item, Is.True);
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator GamepadUsesSharedScalarDeadzoneAndDpadPriority()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            Initialize(pad);
            var settings = GameSettingsService.Current.Input;
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.1f, 1), rightTrigger = 0.65f });
            yield return null;
            yield return null;
            var controls = input.ReadControls(false);
            Assert.That(controls.Steer, Is.Zero);
            Assert.That(controls.Throttle, Is.EqualTo(0.65f).Within(0.0001));
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.2f, 0), rightTrigger = 0.65f });
            yield return null;
            Assert.That(input.ReadControls(false).Steer, Is.EqualTo(Mathf.Clamp01((0.2f - settings.GamepadDeadzone)
                / (settings.GamepadMaximum - settings.GamepadDeadzone))).Within(0.0001));
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.6f, 1), rightTrigger = 0.65f, leftTrigger = 0.2f }
                .WithButton(GamepadButton.RightShoulder).WithButton(GamepadButton.West).WithButton(GamepadButton.North));
            yield return null;
            controls = input.ReadControls(false);
            Assert.That(controls.Steer, Is.EqualTo(Mathf.Clamp01((0.6f - settings.GamepadDeadzone)
                / (settings.GamepadMaximum - settings.GamepadDeadzone))).Within(0.0001));
            InputBindingDisplay.Get(input.PromptAction("Steer"), out string glyphPath);
            Assert.That(glyphPath, Is.EqualTo("leftStick"));
            Assert.That(controls.Drift && controls.Item && controls.LookBack, Is.True);
            Assert.That(controls.Brake, Is.EqualTo(0.2f).Within(0.0001));
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = Vector2.right }
                .WithButton(GamepadButton.DpadLeft).WithButton(GamepadButton.South).WithButton(GamepadButton.East)
                .WithButton(GamepadButton.LeftShoulder).WithButton(GamepadButton.Start));
            yield return null;
            controls = input.ReadControls(false);
            Assert.That(controls.Steer, Is.EqualTo(-1));
            Assert.That(controls.Throttle, Is.EqualTo(1));
            Assert.That(controls.Brake, Is.EqualTo(1));
            Assert.That(controls.Item, Is.True);
            Assert.That(input.Pressed("Pause"), Is.True);
        }

        [UnityTest]
        public IEnumerator ShortTouchPressSurvivesZeroStepFrameAndIsConsumedOnlyOnce()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Initialize(screen);
            InputDeviceState.NotifyTouch();
            input.SetTouchHold("Item", true);
            input.SetTouchHold("Drift", true);
            input.SetTouchHold("Item", false);
            input.SetTouchHold("Drift", false);
            input.CaptureFrame();
            yield return null;
            input.CaptureFrame();
            var first = input.ReadControls(false);
            Assert.That(first.Item && first.Drift, Is.True);
            var second = input.ReadControls(false);
            Assert.That(second.Item || second.Drift, Is.False);
        }

        [UnityTest]
        public IEnumerator AutoThrottleKeepsCountdownManualAndAllowsBraking()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Initialize(screen);
            InputDeviceState.NotifyTouch();
            Assert.That(input.AutoAccelerate, Is.False);
            input.AutoAccelerate = true;
            Assert.That(input.ReadControls(true).Throttle, Is.Zero);
            Assert.That(input.ReadControls(false).Throttle, Is.EqualTo(1));
            input.SetTouchHold("Brake", true);
            var controls = input.ReadControls(false);
            Assert.That(controls.Brake, Is.EqualTo(1));
            Assert.That(controls.Throttle, Is.Zero);
            input.SetTouchHold("Brake", false);
            Assert.That(input.ReadControls(false).Throttle, Is.EqualTo(1));
            yield return null;
        }

        [UnityTest]
        public IEnumerator FixedStepSessionConsumesQuickItemOnlyAfterAnActualStep()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Initialize(screen);
            InputDeviceState.NotifyTouch();
            const string root = "Assets/LoadResources/Demos/kagit_kart/Data/";
            var catalog = KagitKartSourceCatalog.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "SourceCatalog.json").text);
            var track = KagitKartTrackData.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "Tracks/Meadow.json").text);
            var plan = KagitKartAiPlan.Parse(AssetDatabase.LoadAssetAtPath<TextAsset>(root + "AiPlans/Meadow.json").text, track);
            using var race = new KagitKartSession(catalog, track, plan, KagitKartRaceMode.TimeTrial, "tilki", 120, 12345);
            for (int i = 0; i < 360; i++)
            {
                race.AdvanceFrame(KagitKartSimulationClock.Step, default);
            }
            input.SetTouchHold("Item", true);
            input.SetTouchHold("Item", false);
            int reads = 0;
            Func<KagitKartControls> read = () =>
            {
                reads++;
                return input.ReadControls(race.Race.State == KagitKartRaceState.Countdown);
            };
            Assert.That(race.AdvanceInputFrame(KagitKartSimulationClock.Step * 0.25, read), Is.Zero);
            Assert.That(reads, Is.Zero);
            yield return null;
            Assert.That(race.AdvanceInputFrame(KagitKartSimulationClock.Step * 3, read), Is.EqualTo(3));
            Assert.That(reads, Is.EqualTo(3));
            Assert.That(race.Player.ItemCount, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator GameplayReopenRequiresReleaseAndDeviceSwitchClearsOldTouch()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            touchDevice = InputSystem.AddDevice<Touchscreen>();
            Initialize(keyboard);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.Space, Key.E));
            yield return null;
            yield return null;
            Assert.That(input.ReadControls(false).Throttle, Is.EqualTo(1));
            input.SetDriving(false);
            input.SetDriving(true);
            yield return null;
            var controls = input.ReadControls(false);
            Assert.That(controls.Throttle, Is.Zero);
            Assert.That(controls.Drift || controls.Item, Is.False);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            // 松键帧没有模拟步，也必须解除门闩。
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            Assert.That(input.ReadControls(false).Throttle, Is.EqualTo(1));
            InputDeviceState.NotifyTouch();
            input.SetTouchHold("Throttle", true);
            input.SetTouchHold("Drift", true);
            input.SetTouchHold("Left", true);
            Assert.That(input.ReadControls(false).Steer, Is.EqualTo(-1));
            InputDeviceState.Notify(keyboard);
            controls = input.ReadControls(false);
            Assert.That(controls.Drift, Is.False);
            Assert.That(controls.Steer, Is.Zero);
            Assert.That(controls.Throttle, Is.Zero, "重新选择键盘不能恢复切换前一直按住的油门。");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.E));
            yield return null;
            controls = input.ReadControls(false);
            Assert.That(controls.Item, Is.True, "新按键不应被旧设备的松键门闩吞掉。");
            Assert.That(controls.Throttle, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InputSystemThreeTouchesSteerAccelerateAndDriftTogether()
        {
            var screen = InputSystem.AddDevice<Touchscreen>();
            Initialize(screen);
            var original = EventSystem.current;
            var root = new GameObject("KagitKartTouchInputTest");
            var events = new GameObject("Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(root.transform);
            var system = events.GetComponent<EventSystem>();
            var module = events.GetComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
            module.actionsAsset.devices = new InputDevice[] { screen };
            EventSystem.current = system;
            var canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            canvas.transform.SetParent(root.transform);
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 32000;
            var rightObject = Rect(canvas.transform, "Right", new Vector2(0.25f, 0.3f));
            var leftObject = Rect(canvas.transform, "Left", new Vector2(0.25f, 0.65f));
            BindHold(rightObject, "Right");
            BindHold(leftObject, "Left");
            var gasObject = Rect(canvas.transform, "Throttle", new Vector2(0.65f, 0.3f));
            var driftObject = Rect(canvas.transform, "Drift", new Vector2(0.85f, 0.3f));
            BindHold(gasObject, "Throttle");
            BindHold(driftObject, "Drift");
            try
            {
                yield return null;
                Vector2 steerPoint = RectTransformUtility.WorldToScreenPoint(null, rightObject.transform.position);
                Vector2 leftPoint = RectTransformUtility.WorldToScreenPoint(null, leftObject.transform.position);
                Vector2 gasPoint = RectTransformUtility.WorldToScreenPoint(null, gasObject.transform.position);
                Vector2 driftPoint = RectTransformUtility.WorldToScreenPoint(null, driftObject.transform.position);
                Touch(screen, 1, UnityEngine.InputSystem.TouchPhase.Began, steerPoint);
                Touch(screen, 2, UnityEngine.InputSystem.TouchPhase.Began, gasPoint);
                Touch(screen, 3, UnityEngine.InputSystem.TouchPhase.Began, driftPoint);
                yield return null;
                yield return null;
                var controls = input.ReadControls(false);
                Assert.That(controls.Steer, Is.EqualTo(1));
                Assert.That(controls.Throttle, Is.EqualTo(1));
                Assert.That(controls.Drift, Is.True);
                Touch(screen, 2, UnityEngine.InputSystem.TouchPhase.Ended, gasPoint);
                yield return null;
                yield return null;
                controls = input.ReadControls(false);
                Assert.That(controls.Throttle, Is.Zero);
                Assert.That(controls.Drift, Is.True);
                Assert.That(controls.Steer, Is.EqualTo(1));
                Touch(screen, 4, UnityEngine.InputSystem.TouchPhase.Began, leftPoint);
                yield return null;
                yield return null;
                Assert.That(input.ReadControls(false).Steer, Is.Zero, "左右两指同时保持时回正。");
                Touch(screen, 1, UnityEngine.InputSystem.TouchPhase.Ended, steerPoint);
                yield return null;
                yield return null;
                Assert.That(input.ReadControls(false).Steer, Is.EqualTo(-1), "松开右指后保留左转。");
                Vector2 outside = new Vector2(Screen.width * 0.5f, Screen.height * 0.85f);
                Touch(screen, 4, UnityEngine.InputSystem.TouchPhase.Moved, outside);
                yield return null;
                yield return null;
                Assert.That(input.ReadControls(false).Steer, Is.EqualTo(-1), "拖出仍由原按钮持有，避免触点小偏移中断转向。");
                Touch(screen, 4, UnityEngine.InputSystem.TouchPhase.Ended, outside);
                yield return null;
                yield return null;
                Assert.That(input.ReadControls(false).Steer, Is.Zero, "按钮外抬手同样释放原指针。");
                Touch(screen, 5, UnityEngine.InputSystem.TouchPhase.Began, steerPoint);
                yield return null;
                yield return null;
                Assert.That(input.ReadControls(false).Steer, Is.EqualTo(1));
                rightObject.SendMessage("OnApplicationFocus", false);
                Assert.That(input.ReadControls(false).Steer, Is.Zero, "失焦释放保持转向。");
                Touch(screen, 5, UnityEngine.InputSystem.TouchPhase.Ended, steerPoint);
                yield return null;
                Touch(screen, 6, UnityEngine.InputSystem.TouchPhase.Began, steerPoint);
                yield return null;
                yield return null;
                rightObject.SendMessage("OnApplicationPause", true);
                Assert.That(input.ReadControls(false).Steer, Is.Zero, "挂起释放保持转向。");
                Touch(screen, 6, UnityEngine.InputSystem.TouchPhase.Ended, steerPoint);
                yield return null;
                Touch(screen, 7, UnityEngine.InputSystem.TouchPhase.Began, leftPoint);
                yield return null;
                yield return null;
                Assert.That(input.ReadControls(false).Steer, Is.EqualTo(-1));
                leftObject.SetActive(false);
                driftObject.SetActive(false);
                controls = input.ReadControls(false);
                Assert.That(controls.Steer, Is.Zero);
                Assert.That(controls.Drift, Is.False);
                rightObject.GetComponent<InputCommandButton>().OnPointerDown(new PointerEventData(system) { pointerId = 91 });
                Assert.That(input.ReadControls(false).Steer, Is.EqualTo(1));
                input.SetDriving(false);
                canvas.SetActive(false);
                input.SetDriving(true);
                canvas.SetActive(true);
                Assert.That(input.ReadControls(false).Steer, Is.Zero, "暂停恢复不能重新使用未松开的旧指针。");
            }
            finally
            {
                module.actionsAsset.devices = null;
                if (original != null && original.isActiveAndEnabled)
                {
                    EventSystem.current = original;
                }
                Object.Destroy(root);
            }
        }

        private void Initialize(InputDevice value)
        {
            device = value;
            source = AssetDatabase.LoadAssetAtPath<InputActionAsset>(AssetPath);
            Assert.That(source, Is.Not.Null);
            source.devices = new InputDevice[] { device };
            input = new KagitKartInput(source);
            InputDeviceState.Notify(device);
            input.SetDriving(true);
        }

        private static GameObject Rect(Transform parent, string name, Vector2 anchor)
        {
            var result = new GameObject(name, typeof(RectTransform), typeof(Image));
            result.transform.SetParent(parent, false);
            var rect = result.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.sizeDelta = new Vector2(180, 100);
            return result;
        }

        private void BindHold(GameObject obj, string command)
        {
            var button = obj.AddComponent<InputCommandButton>();
            button.Configure(command, true);
            button.HoldChanged += input.SetTouchHold;
        }

        private static void Touch(Touchscreen screen, int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = position });
        }
        [UnityTest]
        public IEnumerator VisibleTouchCommandsMergeWithPhysicalInputAndReleaseCleanly()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            Initialize(keyboard);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            yield return null;
            input.SetTouchHold("Left", true);
            input.SetTouchHold("Brake", true);
            input.SetTouchHold("Item", true);
            var mixed = input.ReadControls(false);
            Assert.That(mixed.Steer, Is.EqualTo(-1));
            Assert.That(mixed.Throttle, Is.EqualTo(1), "触控刹车不丢键盘油门，规则层决定加减速。");
            Assert.That(mixed.Brake, Is.EqualTo(1));
            Assert.That(mixed.Item, Is.True);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            yield return null;
            Assert.That(input.ReadControls(false).Steer, Is.EqualTo(1), "原作采用绝对值较大的转向命令。");
            input.SetTouchHold("Left", false);
            input.SetTouchHold("Brake", false);
            input.SetTouchHold("Item", false);
            var released = input.ReadControls(false);
            Assert.That(released.Brake, Is.Zero);
            Assert.That(released.Item, Is.False);
            input.SetDriving(false);
            Assert.That(input.ReadControls(false).Throttle, Is.Zero);
        }


    }
}
#endif
