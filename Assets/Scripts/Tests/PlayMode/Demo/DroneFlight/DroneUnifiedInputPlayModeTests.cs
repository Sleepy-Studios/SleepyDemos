#if UNITY_EDITOR
using System.Collections;
using Core.Runtime.Inputs;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
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
    /// 三端四轴输入、菜单/帮助松键门闩，以及保存态指南的设备提示和取消焦点。
    public sealed class DroneUnifiedInputPlayModeTests
    {
        private GameObject drone;
        private Gamepad gamepad;
        private InputSettings originalSettings, testSettings;
        private DronePlayerInput input;
        private DroneFlightController controller;
        private GameObject helpInstance, helpEvents;
        private DroneFlightHelpView helpTestView;
        private Keyboard helpKeyboard;

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
            if(helpTestView != null) yield return helpTestView.HideAsync(false).ToCoroutine();
            Object.Destroy(helpInstance);Object.Destroy(helpEvents);helpTestView=null;helpInstance=helpEvents=null;
            Object.Destroy(drone);
            if(gamepad != null) InputSystem.RemoveDevice(gamepad);
            if(helpKeyboard != null) InputSystem.RemoveDevice(helpKeyboard);helpKeyboard=null;
            InputSystem.settings = originalSettings; Object.Destroy(testSettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HelpPreservesSourceMenuAndRequiresReleaseBeforeFlight()
        {
            InputSystem.QueueStateEvent(gamepad,new GamepadState { leftStick=Vector2.up });
            yield return null;
            input.SetHelpOpen(true);
            Assert.That(input.IsPanelOpen,Is.False);
            Assert.That(input.AcceptsFlightInput,Is.False);
            Assert.That(controller.CurrentControlInput.Lift,Is.Zero);
            input.Execute("Exit");
            InputSystem.QueueStateEvent(gamepad,new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            input.SetHelpOpen(false);
            yield return null;
            Assert.That(controller.IsArmed,Is.False);
            Assert.That(input.ResetProgress,Is.Zero);
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;
            input.SetPanelOpen(true); input.SetHelpOpen(true); input.SetHelpOpen(false);
            Assert.That(input.IsPanelOpen,Is.True,"关闭帮助返回原操作菜单，不直接回飞行。");
            Assert.That(input.AcceptsFlightInput,Is.False);
            input.SetPanelOpen(false);
            Assert.That(input.AcceptsFlightInput,Is.True);
            input.SetDebugOpen(true);
            Assert.That(input.AcceptsFlightInput,Is.False,"调试菜单导航不能同时飞行。");
            input.Execute("ArmOrReset");
            Assert.That(controller.IsArmed,Is.False);
            input.SetDebugOpen(false);
        }

        [UnityTest]
        public IEnumerator HelpPrefabUsesMenuFocusCancelAndLiveDevicePrompts()
        {
            var events = helpEvents = EventSystem.current == null ? new GameObject("HelpEvents",typeof(EventSystem),typeof(InputSystemUIInputModule)) : null;
            var instance=helpInstance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHelpView.prefab"));
            var view=helpTestView=new DroneFlightHelpView();
            view.InitWithGameObject(instance);view.SetData(new DroneFlightViewData(null,"help-test",input));
            input.SetHelpOpen(true);
            yield return view.ShowAsync(false).ToCoroutine();
            for(int i=0;i<5;i++) yield return null;
            var index=instance.GetComponent<ComponentItemIndex>();
            var device=System.Array.Find(index.Components,x=>x.name=="Device") as TMPro.TextMeshProUGUI;
            Assert.That(device.text,Does.Contain("手柄"));
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(instance.transform),Is.True);
            int helpCommands=0;
            void Observe(string command) { if(command=="Help") helpCommands++; }
            input.PresentationRequested+=Observe;
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject,new BaseEventData(EventSystem.current),ExecuteEvents.cancelHandler);
            Assert.That(helpCommands,Is.EqualTo(1));
            input.PresentationRequested-=Observe;
            InputSystem.RemoveDevice(gamepad);gamepad=null;InputDeviceState.NotifyTouch();yield return null;
            Assert.That(device.text,Is.EqualTo("触屏"));
            var flight=System.Array.Find(index.Components,x=>x.name=="Flight") as TMPro.TextMeshProUGUI;
            Assert.That(flight.text,Does.Contain("左摇杆"));
            helpKeyboard=InputSystem.AddDevice<Keyboard>();InputDeviceState.Notify(helpKeyboard);yield return null;
            Assert.That(device.text,Is.EqualTo("键鼠"));
            Assert.That(flight.text,Does.Not.Contain("左摇杆"));
            yield return view.HideAsync(false).ToCoroutine();
            input.SetHelpOpen(false);Object.Destroy(instance);if(events!=null) Object.Destroy(events);
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
                var backButton = System.Array.Find(hud.GetComponentsInChildren<InputCommandButton>(true), button => button.Command == "Back");
                Assert.That(backButton != null && backButton.gameObject.activeInHierarchy, Is.True, "飞行期间必须保留触屏返回遥控等待的入口。");
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
                input.enabled = false; yield return null;
                input.enabled = true; yield return null;
                right.OnDrag(b); yield return null;
                Assert.That(controller.CurrentControlInput.Right, Is.Zero, "退出并重新进入遥控不能沿用旧手指。");
                input.SetPanelOpen(true);
                var operationPanel = (GameObject)fields.FindProperty("operationPanel").objectReferenceValue;
                var helpButton = System.Array.Find(operationPanel.GetComponentsInChildren<InputCommandButton>(true), button => button.Command == "Help");
                Assert.That(helpButton, Is.Not.Null);
                EventSystem.current.SetSelectedGameObject(helpButton.gameObject);
                input.SetHelpOpen(true);
                Assert.That(operationPanel.activeSelf, Is.False);
                input.SetHelpOpen(false);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(helpButton.gameObject), "关闭指南应恢复来源菜单的同一焦点。");
                input.SetPanelOpen(false);
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
