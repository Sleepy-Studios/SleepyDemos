#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using Core.Runtime.Inputs;
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

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            input?.Dispose();
            if (device != null && device.added)
            {
                InputSystem.RemoveDevice(device);
            }
            if (source != null)
            {
                source.devices = null;
            }
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
        public IEnumerator GamepadUsesOriginalScalarDeadzoneAndDpadPriority()
        {
            var pad = InputSystem.AddDevice<Gamepad>();
            Initialize(pad);
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.1f, 1), rightTrigger = 0.65f });
            yield return null;
            yield return null;
            var controls = input.ReadControls(false);
            Assert.That(controls.Steer, Is.Zero);
            Assert.That(controls.Throttle, Is.EqualTo(0.65f).Within(0.0001));
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.2f, 0), rightTrigger = 0.65f });
            yield return null;
            Assert.That(input.ReadControls(false).Steer, Is.EqualTo((0.2f - 0.18f) / 0.82f).Within(0.0001));
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0.6f, 1), rightTrigger = 0.65f, leftTrigger = 0.2f }
                .WithButton(GamepadButton.RightShoulder).WithButton(GamepadButton.West).WithButton(GamepadButton.North));
            yield return null;
            controls = input.ReadControls(false);
            Assert.That(controls.Steer, Is.EqualTo((0.6f - 0.18f) / 0.82f).Within(0.0001));
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
            input.ReadControls(false);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return null;
            Assert.That(input.ReadControls(false).Throttle, Is.EqualTo(1));
            InputDeviceState.NotifyTouch();
            input.SetTouchHold("Throttle", true);
            input.SetTouchHold("Drift", true);
            input.SetTouchSteer(-0.8f);
            Assert.That(input.ReadControls(false).Steer, Is.EqualTo(-0.8f));
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
            var sliderObject = Rect(canvas.transform, "Steer", new Vector2(0.25f, 0.3f));
            var slider = sliderObject.AddComponent<TouchInputPad>();
            slider.ConfigureHorizontal(96);
            var gasObject = Rect(canvas.transform, "Throttle", new Vector2(0.65f, 0.3f));
            var driftObject = Rect(canvas.transform, "Drift", new Vector2(0.85f, 0.3f));
            BindHold(gasObject, "Throttle");
            BindHold(driftObject, "Drift");
            try
            {
                yield return null;
                Vector2 steerPoint = RectTransformUtility.WorldToScreenPoint(null, sliderObject.transform.position);
                Vector2 gasPoint = RectTransformUtility.WorldToScreenPoint(null, gasObject.transform.position);
                Vector2 driftPoint = RectTransformUtility.WorldToScreenPoint(null, driftObject.transform.position);
                Touch(screen, 1, UnityEngine.InputSystem.TouchPhase.Began, steerPoint);
                Touch(screen, 2, UnityEngine.InputSystem.TouchPhase.Began, gasPoint);
                Touch(screen, 3, UnityEngine.InputSystem.TouchPhase.Began, driftPoint);
                yield return null;
                yield return null;
                float travel = 96 * Screen.height / 720f;
                Touch(screen, 1, UnityEngine.InputSystem.TouchPhase.Moved, steerPoint + new Vector2(travel * 0.5f, 0));
                yield return null;
                yield return null;
                input.SetTouchSteer(slider.Move.x);
                var controls = input.ReadControls(false);
                Assert.That(controls.Steer, Is.EqualTo(0.5f).Within(0.02));
                Assert.That(controls.Throttle, Is.EqualTo(1));
                Assert.That(controls.Drift, Is.True);
                Touch(screen, 2, UnityEngine.InputSystem.TouchPhase.Ended, gasPoint);
                yield return null;
                yield return null;
                controls = input.ReadControls(false);
                Assert.That(controls.Throttle, Is.Zero);
                Assert.That(controls.Drift, Is.True);
                Assert.That(controls.Steer, Is.EqualTo(0.5f).Within(0.02));
                sliderObject.SetActive(false);
                driftObject.SetActive(false);
                input.SetTouchSteer(slider.Move.x);
                controls = input.ReadControls(false);
                Assert.That(controls.Steer, Is.Zero);
                Assert.That(controls.Drift, Is.False);
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
            typeof(InputCommandButton).GetField("command", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(button, command);
            typeof(InputCommandButton).GetField("hold", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(button, true);
            button.HoldChanged += input.SetTouchHold;
        }

        private static void Touch(Touchscreen screen, int id, UnityEngine.InputSystem.TouchPhase phase, Vector2 position)
        {
            InputSystem.QueueStateEvent(screen, new TouchState { touchId = id, phase = phase, position = position });
        }
    }
}
#endif
