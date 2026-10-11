#if UNITY_EDITOR
using System.Collections;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using Core.Runtime.TimeControls;
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

        private DroneFlightData data;

        private DroneFlightController controller;

        private GameObject helpInstance, helpEvents;

        private DroneFlightHelpView helpTestView;

        private Keyboard helpKeyboard;
        private GameSettingsConfig languageConfig;
        private System.Reflection.FieldInfo[] languageFields;
        private object[] languageValues;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            languageConfig = AssetDatabase.LoadAssetAtPath<GameSettingsConfig>("Assets/" + GameSettingsService.ConfigAddress + ".asset");
            Assert.That(languageConfig, Is.Not.Null);
            var languageState = new[] { "texts", "fonts", "languages", "<CurrentLanguage>k__BackingField" };
            languageFields = new System.Reflection.FieldInfo[languageState.Length];
            languageValues = new object[languageState.Length];
            for (int i = 0; i < languageState.Length; i++)
            {
                languageFields[i] = typeof(LanguageService).GetField(languageState[i],
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                Assert.That(languageFields[i], Is.Not.Null);
                languageValues[i] = languageFields[i].GetValue(null);
            }
            // 玩法夹具没有走正式启动；使用正式配置初始化，不在生产 HUD 中添加兜底。
            LanguageService.Initialize(languageConfig.Language, languageConfig.Fonts, LanguageService.CurrentLanguage ?? "zh-CN");
            originalSettings = InputSystem.settings;
            testSettings = Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            gamepad = InputSystem.AddDevice<Gamepad>();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab");
            Assert.That(prefab, Is.Not.Null);
            drone = Object.Instantiate(prefab);
            input = drone.GetComponent<DronePlayerInput>();
            controller = drone.GetComponent<DroneFlightController>();
            data = GlobalData.Add<DroneFlightData>();
            data.Handler.AttachInput(input);
            Assert.That(input.Session, Is.Not.Null, "正式资产必须保存动作引用，不能使用运行时兜底。");
            input.Session.Asset.devices = new InputDevice[]
            {
                gamepad
            };
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (helpTestView != null)
                yield return helpTestView.HideAsync(false).ToCoroutine();
            Object.Destroy(helpInstance);
            Object.Destroy(helpEvents);
            helpTestView = null;
            helpInstance = helpEvents = null;
            data.Handler.Dispose();
            GlobalData.Remove<DroneFlightData>();
            Object.Destroy(drone);
            if (gamepad != null)
                InputSystem.RemoveDevice(gamepad);
            if (helpKeyboard != null)
                InputSystem.RemoveDevice(helpKeyboard);
            helpKeyboard = null;
            InputSystem.settings = originalSettings;
            Object.Destroy(testSettings);
            for (int i = 0; i < languageFields.Length; i++)
            {
                languageFields[i].SetValue(null, languageValues[i]);
            }
            languageConfig = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator GlobalFreezeRejectsFlightCommandsButKeepsPanelAndExitAvailable()
        {
            int exits = 0;
            System.Action onExit = () => exits++;
            input.ExitRequested += onExit;
            using var freeze = GameTimeService.Push(0);
            try
            {
                input.Execute("ArmOrReset");
                input.Execute("Takeoff");
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
                yield return null;
                Assert.That(input.AcceptsFlightInput, Is.False);
                Assert.That(controller.IsArmed, Is.False);
                Assert.That(controller.CurrentControlInput.Lift, Is.Zero);
                input.Execute("Exit");
                Assert.That(exits, Is.EqualTo(1));
                input.Execute("Panel");
                Assert.That(input.IsPanelOpen, Is.True);
            }
            finally
            {
                input.ExitRequested -= onExit;
                input.SetPanelOpen(false);
            }
        }

        [UnityTest]
        public IEnumerator HelpPreservesSourceMenuAndRequiresReleaseBeforeFlight()
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
            yield return null;
            input.SetHelpOpen(true);
            Assert.That(input.IsPanelOpen, Is.False);
            Assert.That(input.AcceptsFlightInput, Is.False);
            Assert.That(controller.CurrentControlInput.Lift, Is.Zero);
            input.Execute("Exit");
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            input.SetHelpOpen(false);
            yield return null;
            Assert.That(controller.IsArmed, Is.False);
            Assert.That(input.ResetProgress, Is.Zero);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            input.SetPanelOpen(true);
            input.SetHelpOpen(true);
            input.SetHelpOpen(false);
            Assert.That(input.IsPanelOpen, Is.True, "关闭帮助返回原操作菜单，不直接回飞行。");
            Assert.That(input.AcceptsFlightInput, Is.False);
            input.SetPanelOpen(false);
            Assert.That(input.AcceptsFlightInput, Is.True);
            input.SetDebugOpen(true);
            Assert.That(input.AcceptsFlightInput, Is.False, "调试菜单导航不能同时飞行。");
            input.Execute("ArmOrReset");
            Assert.That(controller.IsArmed, Is.False);
            input.SetDebugOpen(false);
        }

        [UnityTest]
        public IEnumerator HelpPrefabUsesMenuFocusCancelAndLiveDevicePrompts()
        {
            var events = helpEvents = EventSystem.current == null ? new GameObject("HelpEvents", typeof(EventSystem), typeof(InputSystemUIInputModule)) : null;
            var instance = helpInstance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHelpView.prefab"));
            var view = helpTestView = new DroneFlightHelpView();
            view.InitWithGameObject(instance);
            view.SetData(new DroneFlightViewData(null, "help-test", input));
            GlobalData.Dispatch(new DroneFlightHelpResultAction(data, true));
            yield return view.ShowAsync(false).ToCoroutine();
            for (int i = 0; i < 5; i++)
                yield return null;
            var index = instance.GetComponent<ComponentItemIndex>();
            var device = System.Array.Find(index.Components, x => x.name == "Device") as TMPro.TextMeshProUGUI;
            Assert.That(device.text, Does.Contain("键鼠"), "连接手柄不能抢占提示，必须有实际操作。");
            var group = instance.GetComponent<InputPromptGroup>();
            Assert.That(group.Get("SwitchCamera").transform.Find("Caption").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo(LanguageService.Get("Input.Drone.SwitchCamera")));
            Assert.That(group.Get("ResetHold").transform.Find("Caption").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo(LanguageService.Format("Input.Drone.ResetHoldDuration", input.ResetHoldSeconds)));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            Assert.That(device.text, Does.Contain("手柄"));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.Null);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(instance.transform), Is.True);
            ExecuteEvents.Execute(EventSystem.current.currentSelectedGameObject, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
            Assert.That(data.HelpRequested, Is.False, "公共Cancel提交帮助关闭Action。");
            InputSystem.RemoveDevice(gamepad);
            gamepad = null;
            InputDeviceState.NotifyTouch();
            yield return null;
            Assert.That(device.text, Is.EqualTo("触屏"));
            var flight = System.Array.Find(index.Components, x => x.name == "Flight") as TMPro.TextMeshProUGUI;
            Assert.That(flight.text, Is.EqualTo(LanguageService.Get("Input.Drone.TouchFlight")));
            helpKeyboard = InputSystem.AddDevice<Keyboard>();
            InputDeviceState.Notify(helpKeyboard);
            yield return null;
            Assert.That(device.text, Is.EqualTo("键鼠"));
            Assert.That(flight.text, Is.EqualTo(LanguageService.Get("Input.Drone.FlightAxes")));
            yield return view.HideAsync(false).ToCoroutine();
            input.SetHelpOpen(false);
            Object.Destroy(instance);
            if (events != null)
                Object.Destroy(events);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FourAxisGamepadViewModifierAndMenuReleaseGateShareRealActions()
        {
            var state = new GamepadState
            {
                leftStick = new Vector2(.5f, .75f),
                rightStick = new Vector2(-.5f, .25f)
            };
            InputSystem.QueueStateEvent(gamepad, state);
            yield return null;
            yield return null;
            var left = GameplayInputMath.ApplyDeadzone(state.leftStick, .2f, 1);
            var right = GameplayInputMath.ApplyDeadzone(state.rightStick, .2f, 1);
            Assert.That(controller.CurrentControlInput.Lift, Is.EqualTo(left.y).Within(.001f));
            Assert.That(controller.CurrentControlInput.Yaw, Is.EqualTo(left.x).Within(.001f));
            Assert.That(controller.CurrentControlInput.Forward, Is.EqualTo(right.y).Within(.001f));
            Assert.That(controller.CurrentControlInput.Right, Is.EqualTo(right.x).Within(.001f));
            state.leftTrigger = 1;
            InputSystem.QueueStateEvent(gamepad, state);
            yield return null;
            Assert.That(controller.CurrentControlInput.Forward, Is.Zero);
            Assert.That(controller.CurrentControlInput.Right, Is.Zero);
            Assert.That(input.CameraLook.sqrMagnitude, Is.GreaterThan(0));
            input.SetPanelOpen(true);
            Assert.That(controller.CurrentControlInput.Lift, Is.Zero);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            input.SetPanelOpen(false);
            yield return null;
            Assert.That(input.ResetProgress, Is.Zero, "菜单确认不能变成长按复位。");
            Assert.That(controller.IsArmed, Is.False);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
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
                input.Session.Asset.devices = new InputDevice[]
                {
                    keyboard,
                    gamepad
                };
                input.PresentationRequested += observed.Add;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F2, Key.F3));
                yield return null;
                Assert.That(observed, Does.Contain("DebugDraw"));
                Assert.That(observed, Does.Contain("DebugPanel"));
                Assert.That(observed.Count, Is.EqualTo(2));
            }
            finally
            {
                input.PresentationRequested -= observed.Add;
                InputSystem.RemoveDevice(keyboard);
            }
        }

        [UnityTest]
        public IEnumerator HudTelemetryPromptsUseSessionBindingsAndFollowEquipmentAndActualDevice()
        {
            helpKeyboard = InputSystem.AddDevice<Keyboard>();
            input.Session.Asset.devices = new InputDevice[] { helpKeyboard, gamepad };
            InputDeviceState.Notify(helpKeyboard);
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHudView.prefab"));
            var view = new DroneFlightHudView { Loader = new DroneFlightHudBindingPlayModeTests.OwnedObjectLoader() };
            try
            {
                // 模拟从英文 Hub 进入 Demo；结束时恢复服务原配置，绝不向在场页面广播语言切换。
                LanguageService.SelectLanguage("en");
                view.InitWithGameObject(instance);
                view.SetData(new DroneFlightViewData(null, data.SessionId, input));
                yield return view.ShowAsync(false).ToCoroutine();
                var group = instance.GetComponent<InputPromptGroup>();
                foreach (var item in new (string Key, string English)[]
                {
                    ("Touch/ArmOrReset", "Arm / hold to reset"),
                    ("Touch/Aim", "Aim"),
                    ("Touch/ReelIn", "Reel in"),
                    ("Touch/ReelOut", "Pay out"),
                    ("Touch/Activate", "Enter remote control"),
                    ("Touch/Equipment", "Equipment action"),
                    ("Touch/ViewModifier", "Camera modifier"),
                    ("Touch/Panel", "Operation panel"),
                    ("Operations/Takeoff", "Auto takeoff"),
                    ("Operations/Landing", "Auto landing"),
                    ("Operations/ProfileCine", "Cinematic mode"),
                    ("Operations/ProfileNormal", "Normal mode"),
                    ("Operations/ProfileSport", "Sport mode"),
                    ("Operations/ZoomIn", "Widen view"),
                    ("Operations/ZoomOut", "Narrow view"),
                    ("Operations/DebugDraw", "Thrust vectors"),
                    ("Operations/Panel", "Close panel"),
                    ("Hud/Panel", "Operation panel")
                })
                {
                    var caption = group.Get(item.Key).transform.Find("Caption").GetComponent<TMPro.TMP_Text>();
                    Assert.That(caption.text, Is.EqualTo(item.English), item.Key + " 应保留该行操作语义，不能被会话绑定清除文案键。");
                }
                var camera = group.Get("Telemetry/Camera");
                var aim = group.Get("Telemetry/Aim");
                var equipment = group.Get("Telemetry/Equipment");
                var line = group.Get("Telemetry/Line");
                var reset = group.Get("Telemetry/Reset");
                var gear = group.Get("Telemetry/Gear");
                var hud = new DroneHudSnapshot(DroneFlightOperationState.Flying, DroneResponseProfile.Normal,
                    true, 0, 12.5f, 3, -1, 7, false, DroneCameraMode.ThirdPerson, 0, 0, 60);
                var harpoon = new DroneEquipmentSnapshot(DroneEquipmentKind.Harpoon, DroneEquipmentState.Ready,
                    "待发射", 1, 0, 0, 0, 0, 0, true, Vector3.forward, Vector3.zero);
                GlobalData.Dispatch(new DroneFlightTelemetryAction(data.SessionId,
                    new DroneFlightUiSnapshot(hud, harpoon, "渔叉待发射", "", default, .4f, 5, true)));
                Assert.That(camera.Action, Is.SameAs(input.Session.Asset.FindAction("Flight/SwitchCamera")));
                Assert.That(gear.Action, Is.SameAs(input.Session.Asset.FindAction("Flight/LandingGear")));
                Assert.That(aim.Action, Is.SameAs(input.Session.Asset.FindAction("Flight/Aim")));
                Assert.That(equipment.Action, Is.SameAs(input.Session.Asset.FindAction("Flight/Equipment")));
                Assert.That(line.Action, Is.SameAs(input.Session.Asset.FindAction("Flight/Line")));
                Assert.That(reset.Action, Is.SameAs(input.Session.Asset.FindAction("Flight/ArmOrReset")));
                Assert.That(aim.gameObject.activeInHierarchy && line.gameObject.activeInHierarchy && reset.gameObject.activeInHierarchy, Is.True);
                Assert.That(camera.GetComponent<Button>(), Is.Null, "遥测区域只显示提示，不提交玩法命令。");
                var resetCaption = reset.transform.Find("Caption").GetComponent<TMPro.TMP_Text>();
                object[] durationArguments = { 5f };
                reset.SetCaptionKey("Input.Drone.ResetHoldDuration", durationArguments);
                durationArguments[0] = 9f;
                reset.Refresh();
                Assert.That(resetCaption.text, Is.EqualTo("Hold 5 seconds to restart the scene"), "完整句式保存参数副本，不随调用者数组改变。");
                reset.SetCaption("Author caption");
                Assert.That(resetCaption.text, Is.EqualTo("Author caption"));
                reset.SetCaptionKey("Input.Drone.HoldReset");
                Assert.That(resetCaption.text, Is.EqualTo("Hold to reset"), "切换静态词条后清理旧格式化参数。");
                var cameraCaption = camera.transform.Find("Caption").GetComponent<TMPro.TMP_Text>();
                Assert.That(cameraCaption.text, Is.EqualTo("Switch camera"));
                foreach (var mapping in languageConfig.Fonts.Entries)
                {
                    if (mapping.FontId == 0 && mapping.Language == "en")
                    {
                        Assert.That(cameraCaption.font, Is.SameAs(mapping.Font));
                        Assert.That(cameraCaption.fontSharedMaterial, Is.SameAs(mapping.Material));
                        Assert.That(resetCaption.font, Is.SameAs(mapping.Font));
                        Assert.That(resetCaption.fontSharedMaterial, Is.SameAs(mapping.Material));
                    }
                }
                Assert.That(equipment.transform.Find("Caption").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Fire / detach & recover"));
                var icon = camera.transform.Find("Glyphs/Glyph0/Icon0").GetComponent<Image>();
                Assert.That(icon.sprite, Is.Not.Null);
                Assert.That(icon.sprite.name, Is.EqualTo("KeyC"), "切换镜头使用实际 Kenney 键盘图标。");
                int keyboardBinding = -1;
                for (int i = 0; i < camera.Action.bindings.Count; i++)
                    if (camera.Action.bindings[i].effectivePath == "<Keyboard>/c") keyboardBinding = i;
                Assert.That(keyboardBinding, Is.GreaterThanOrEqualTo(0));
                camera.Action.ApplyBindingOverride(keyboardBinding, "<Keyboard>/y");
                Assert.That(icon.sprite.name, Is.EqualTo("KeyY"), "改键后遥测提示读取同一会话副本。");
                var index = instance.GetComponent<ComponentItemIndex>();
                var actions = System.Array.Find(index.Components, item => item.name == "EquipmentActions") as TMPro.TMP_Text;
                Assert.That(actions.gameObject.activeSelf, Is.False, "物理提示出现时不重复显示旧操作正文。");
                var resetText = System.Array.Find(index.Components, item => item.name == "ResetProgressText") as TMPro.TMP_Text;
                Assert.That(resetText.text, Does.Contain("2.0 / 5.0 s"));
                InputDeviceState.NotifyTouch();
                Assert.That(camera.gameObject.activeSelf, Is.False);
                Assert.That(equipment.gameObject.activeSelf, Is.False);
                Assert.That(actions.gameObject.activeSelf, Is.True);
                Assert.That(actions.text, Is.EqualTo("Belly aim · Fire / detach & recover · Reel in / pay out"));
                InputDeviceState.Notify(gamepad);
                Assert.That(camera.gameObject.activeSelf, Is.True);
                Assert.That(actions.gameObject.activeSelf, Is.False);
                Assert.That(cameraCaption.text, Is.EqualTo("Switch camera"));
                Assert.That(gear.transform.Find("Caption").GetComponent<TMPro.TMP_Text>().text, Is.EqualTo("Operation panel"));
                var aimedHud = new DroneHudSnapshot(DroneFlightOperationState.Flying, DroneResponseProfile.Normal,
                    true, 0, 12.5f, 3, -1, 7, false, DroneCameraMode.HarpoonAim, 0, 0, 60);
                GlobalData.Dispatch(new DroneFlightTelemetryAction(data.SessionId,
                    new DroneFlightUiSnapshot(aimedHud, harpoon, "渔叉待发射", "", default, 0, 5, true)));
                Assert.That(camera.Action, Is.SameAs(input.Session.Asset.FindAction("Flight/Aim")));
                Assert.That(cameraCaption.text, Is.EqualTo("Exit aim"));
                Assert.That(reset.gameObject.activeSelf, Is.False);
                GlobalData.Dispatch(new DroneFlightTelemetryAction(data.SessionId,
                    new DroneFlightUiSnapshot(hud, default, "", "", default, 0, 5, true)));
                Assert.That(aim.gameObject.activeSelf || equipment.gameObject.activeSelf || line.gameObject.activeSelf, Is.False,
                    "纯无人机不显示附加装备动作。");
            }
            finally
            {
                view.DestroyAsync().Forget();
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedHudTouchPadsAreIndependentAndPanelDropsOldPointers()
        {
            var canvas = new GameObject("DroneTouchCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var events = EventSystem.current == null ? new GameObject("TouchEvents", typeof(EventSystem), typeof(InputSystemUIInputModule)) : null;
            var hud = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/drone_flight/Prefabs/UI/DroneFlightHudView.prefab"), canvas.transform);
            var view = new DroneFlightHudView
            {
                Loader = new DroneFlightHudBindingPlayModeTests.OwnedObjectLoader()
            };
            view.InitWithGameObject(hud);
            view.SetData(new DroneFlightViewData(null, data.SessionId, input));
            try
            {
                yield return view.ShowAsync(false).ToCoroutine();
                InputDeviceState.NotifyTouch();
                yield return null;
                var backButton = System.Array.Find(hud.GetComponentsInChildren<InputCommandButton>(true), button => button.Command == "Back");
                Assert.That(backButton != null && backButton.gameObject.activeInHierarchy, Is.True, "飞行期间必须保留触屏返回遥控等待的入口。");
                var index = hud.GetComponent<ComponentItemIndex>();
                var left = System.Array.Find(index.Components, item => item is TouchInputPad && item.name == "LeftStick") as TouchInputPad;
                var right = System.Array.Find(index.Components, item => item is TouchInputPad && item.name == "RightStick") as TouchInputPad;
                Assert.That(left.gameObject.activeInHierarchy, Is.True);
                Assert.That(right.gameObject.activeInHierarchy, Is.True);
                var a = new PointerEventData(EventSystem.current)
                {
                    pointerId = 11,
                    position = new Vector2(100, 100)
                };
                var b = new PointerEventData(EventSystem.current)
                {
                    pointerId = 12,
                    position = new Vector2(900, 100)
                };
                left.OnPointerDown(a);
                right.OnPointerDown(b);
                a.position += new Vector2(0, 40);
                b.position += new Vector2(40, 0);
                left.OnDrag(a);
                right.OnDrag(b);
                yield return null;
                yield return null;
                Assert.That(controller.CurrentControlInput.Lift, Is.GreaterThan(0));
                Assert.That(controller.CurrentControlInput.Right, Is.GreaterThan(0));
                left.OnPointerUp(a);
                Assert.That(right.Move.x, Is.GreaterThan(0));
                input.enabled = false;
                yield return null;
                input.enabled = true;
                yield return null;
                right.OnDrag(b);
                yield return null;
                Assert.That(controller.CurrentControlInput.Right, Is.Zero, "退出并重新进入遥控不能沿用旧手指。");
                input.SetPanelOpen(true);
                var operationPanel = System.Array.Find(index.Components, item => item is RectTransform && item.name == "OperationPanel").gameObject;
                var helpButton = System.Array.Find(operationPanel.GetComponentsInChildren<InputCommandButton>(true), button => button.Command == "Help");
                Assert.That(helpButton, Is.Not.Null);
                EventSystem.current.SetSelectedGameObject(helpButton.gameObject);
                input.SetHelpOpen(true);
                Assert.That(operationPanel.activeSelf, Is.False);
                input.SetHelpOpen(false);
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(helpButton.gameObject), "关闭指南应恢复来源菜单的同一焦点。");
                input.SetPanelOpen(false);
                right.OnDrag(b);
                yield return null;
                yield return null;
                Assert.That(controller.CurrentControlInput.Right, Is.Zero, "面板切换后旧指针不可继续飞行。");
                Assert.That(InputDeviceState.ActiveKind, Is.EqualTo(InputDeviceKind.Touch), "打开面板的静止鼠标不能抢走触控。");
                Assert.That(left.gameObject.activeInHierarchy, Is.True);
                yield return new WaitForEndOfFrame();
                string folder = System.IO.Path.GetFullPath("ValidationArtifacts~/refactor");
                System.IO.Directory.CreateDirectory(folder);
                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "DroneTouch.png"), shot.EncodeToPNG());
                Object.Destroy(shot);
                input.SetTouchArmHeld(true);
                yield return null;
                Assert.That(input.ResetProgress, Is.GreaterThan(0));
                yield return view.HideAsync(false).ToCoroutine();
                yield return null;
                Assert.That(input.ResetProgress, Is.Zero);
                Assert.That(controller.IsArmed, Is.False, "HUD 隐藏释放保持输入，不能解释成短按解锁。");
            }
            finally
            {
                view.DestroyAsync().Forget();
                Object.Destroy(canvas);
                if (events != null)
                    Object.Destroy(events);
            }

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
            input.SetTouchLookMode(true);
            yield return null;
            Assert.That(controller.CurrentControlInput.Forward, Is.Zero);
            input.SetTouchArmHeld(true);
            yield return null;
            input.enabled = false;
            yield return null;
            input.enabled = true;
            yield return null;
            Assert.That(input.ResetProgress, Is.Zero);
            Assert.That(controller.IsArmed, Is.False, "隐藏或断开触控不能解释成短按解锁。");
        }
    }
}
#endif
