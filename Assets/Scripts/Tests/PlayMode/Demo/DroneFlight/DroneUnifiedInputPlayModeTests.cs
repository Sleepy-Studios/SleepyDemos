#if UNITY_EDITOR
using System.Collections;
using Core.Runtime.Inputs;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class DroneUnifiedInputPlayModeTests
    {
        private GameObject drone;
        private Gamepad gamepad;
        private InputSettings originalSettings, testSettings;
        private DronePlayerInput input;
        private DroneFlightController controller;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            gamepad = InputSystem.AddDevice<Gamepad>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab");
            Assert.That(prefab, Is.Not.Null);
            drone = Object.Instantiate(prefab);
            input = drone.GetComponent<DronePlayerInput>(); controller = drone.GetComponent<DroneFlightController>();
            Assert.That(input.Session, Is.Not.Null, "正式资产必须保存动作引用，不能使用运行时兜底。");
            input.Session.Asset.devices = new InputDevice[] { gamepad };
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(drone);
            InputSystem.RemoveDevice(gamepad);
            InputSystem.settings = originalSettings; Object.Destroy(testSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourAxisGamepadViewModifierAndMenuReleaseGateShareRealActions()
        {
            var state = new GamepadState { leftStick = new Vector2(.5f, .75f), rightStick = new Vector2(-.5f, .25f) };
            InputSystem.QueueStateEvent(gamepad, state); yield return null; yield return null;
            var left = GameplayInputMath.ApplyDeadzone(state.leftStick, .2f, 1);
            var right = GameplayInputMath.ApplyDeadzone(state.rightStick, .2f, 1);
            Assert.That(controller.CurrentControlInput.Lift, Is.EqualTo(left.y).Within(.001f));
            Assert.That(controller.CurrentControlInput.Yaw, Is.EqualTo(left.x).Within(.001f));
            Assert.That(controller.CurrentControlInput.Forward, Is.EqualTo(right.y).Within(.001f));
            Assert.That(controller.CurrentControlInput.Right, Is.EqualTo(right.x).Within(.001f));
            state.leftTrigger = 1; InputSystem.QueueStateEvent(gamepad, state); yield return null;
            Assert.That(controller.CurrentControlInput.Forward, Is.Zero);
            Assert.That(controller.CurrentControlInput.Right, Is.Zero);
            Assert.That(input.CameraLook.sqrMagnitude, Is.GreaterThan(0));
            input.SetPanelOpen(true);
            Assert.That(controller.CurrentControlInput.Lift, Is.Zero);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            input.SetPanelOpen(false); yield return null;
            Assert.That(input.ResetProgress, Is.Zero, "菜单确认不能变成长按复位。");
            Assert.That(controller.IsArmed, Is.False);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            Assert.That(controller.IsArmed, Is.True, "松开后重新短按才执行解锁。");
            controller.SetArmed(false);
        }

        [UnityTest]
        public IEnumerator DebugDrawAndPanelUseIndependentRealBindings()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var observed = new System.Collections.Generic.List<string>();
            try
            {
                input.Session.Asset.devices = new InputDevice[] { keyboard, gamepad };
                input.PresentationRequested += observed.Add;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F2, Key.F3));
                yield return null;
                Assert.That(observed, Does.Contain("DebugDraw"));
                Assert.That(observed, Does.Contain("DebugPanel"));
                Assert.That(observed.Count, Is.EqualTo(2));
            }
            finally { input.PresentationRequested -= observed.Add; InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator SavedHudTouchPadsAreIndependentAndPanelDropsOldPointers()
        {
            var canvas = new GameObject("DroneTouchCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var events = EventSystem.current == null ? new GameObject("TouchEvents", typeof(EventSystem), typeof(InputSystemUIInputModule)) : null;
            var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHudView.prefab"), canvas.transform);
            var presenter = hud.GetComponent<DroneControlsPresenter>();
            try
            {
                presenter.Bind(input); InputDeviceState.NotifyTouch(); yield return null;
                var fields = new SerializedObject(presenter);
                var left = (TouchInputPad)fields.FindProperty("leftPad").objectReferenceValue;
                var right = (TouchInputPad)fields.FindProperty("rightPad").objectReferenceValue;
                Assert.That(left.gameObject.activeInHierarchy, Is.True);
                Assert.That(right.gameObject.activeInHierarchy, Is.True);
                var a = new PointerEventData(EventSystem.current) { pointerId = 11, position = new Vector2(100, 100) };
                var b = new PointerEventData(EventSystem.current) { pointerId = 12, position = new Vector2(900, 100) };
                left.OnPointerDown(a); right.OnPointerDown(b);
                a.position += new Vector2(0, 40); b.position += new Vector2(40, 0);
                left.OnDrag(a); right.OnDrag(b); yield return null; yield return null;
                Assert.That(controller.CurrentControlInput.Lift, Is.GreaterThan(0));
                Assert.That(controller.CurrentControlInput.Right, Is.GreaterThan(0));
                left.OnPointerUp(a); Assert.That(right.Move.x, Is.GreaterThan(0));
                input.SetPanelOpen(true); input.SetPanelOpen(false);
                right.OnDrag(b); yield return null; yield return null;
                Assert.That(controller.CurrentControlInput.Right, Is.Zero, "面板切换后旧指针不可继续飞行。");
                Assert.That(InputDeviceState.ActiveKind, Is.EqualTo(InputDeviceKind.Touch), "打开面板的静止鼠标不能抢走触控。");
                Assert.That(left.gameObject.activeInHierarchy, Is.True);
                yield return new WaitForEndOfFrame();
                string folder = System.IO.Path.GetFullPath("ValidationArtifacts~/refactor");
                System.IO.Directory.CreateDirectory(folder);
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "DroneTouch.png"), shot.EncodeToPNG()); Object.Destroy(shot);
                input.SetTouchArmHeld(true); yield return null;
                Assert.That(input.ResetProgress, Is.GreaterThan(0));
                presenter.enabled = false; yield return null;
                Assert.That(input.ResetProgress, Is.Zero);
                Assert.That(controller.IsArmed, Is.False, "HUD 隐藏释放保持输入，不能解释成短按解锁。");
            }
            finally { Object.Destroy(canvas); if (events != null) Object.Destroy(events); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator TouchKeepsFourAxesAndClosingInputClearsHeldCommands()
        {
            InputDeviceState.NotifyTouch();
            input.SetTouchFrame(new Vector2(.2f, .6f), new Vector2(-.4f, .7f));
            yield return null;
            Assert.That(controller.CurrentControlInput.Lift, Is.EqualTo(.6f).Within(.001f));
            Assert.That(controller.CurrentControlInput.Yaw, Is.EqualTo(.2f).Within(.001f));
            Assert.That(controller.CurrentControlInput.Forward, Is.EqualTo(.7f).Within(.001f));
            Assert.That(controller.CurrentControlInput.Right, Is.EqualTo(-.4f).Within(.001f));
            input.SetTouchLookMode(true); yield return null;
            Assert.That(controller.CurrentControlInput.Forward, Is.Zero);
            input.SetTouchArmHeld(true); yield return null;
            input.enabled = false; yield return null;
            input.enabled = true; yield return null;
            Assert.That(input.ResetProgress, Is.Zero);
            Assert.That(controller.IsArmed, Is.False, "隐藏或断开触控不能解释成短按解锁。");
        }
    }
}
#endif
