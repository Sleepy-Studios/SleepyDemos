#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.TimeControls;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.SceneManagement;
using Hotfix.WallSqueeze;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class WallSqueezeRuntimeTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private Touchscreen touch;
        private InputSettings originalSettings;
        private InputSettings temporarySettings;
        private InputActionAsset template;
        private WallSqueezeInput input;
        private Camera camera;
        private GameObject ownedCanvas;
        private GameObject ownedEventSystem;
        private EventSystem originalEventSystem;
        private WallSqueezeSimulation simulation;
        private WallSqueezeWorld world;
        private readonly List<GraphicRaycaster> suspendedRaycasters = new();
        private readonly List<Object> temporaryRuleAssets = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalSettings = InputSystem.settings;
            temporarySettings = Object.Instantiate(originalSettings);
            temporarySettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            temporarySettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = temporarySettings;
            // 单元输入用例隔离已经打开的 Hub 画布，仅本用例的 UI 阻挡参与射线。
            foreach (var raycaster in Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (raycaster.enabled)
                {
                    suspendedRaycasters.Add(raycaster);
                    raycaster.enabled = false;
                }
            }
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            touch = InputSystem.AddDevice<Touchscreen>();
            template = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/LoadResources/Demos/wall_squeeze/Data/PlayerInput.asset"));
            template.devices = new InputDevice[] { keyboard, mouse, gamepad, touch };
            camera = new GameObject("WallSqueezeTestCamera", typeof(Camera)).GetComponent<Camera>();
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 5.9f;
            camera.transform.position = new Vector3(8, 4.5f, -10);
            simulation = new WallSqueezeSimulation(
                AssetDatabase.LoadAssetAtPath<WallSqueezeSettings>("Assets/LoadResources/Demos/wall_squeeze/Data/Settings.asset"),
                AssetDatabase.LoadAssetAtPath<WallSqueezeLevel>("Assets/LoadResources/Demos/wall_squeeze/Data/Level2.asset"));
            input = new WallSqueezeInput(template, camera);
            yield return null;
            Read();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            input?.Dispose();
            input = null;
            if (world != null && GameSceneNavigator.Instance?.CurrentScene == GameSceneId.WallSqueeze)
            {
                world.Dispatch(WallSqueezeCommand.Hub);
                yield return Wait(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "清理返回 Hub");
            }
            if (ownedCanvas != null)
            {
                Object.Destroy(ownedCanvas);
            }
            if (ownedEventSystem != null)
            {
                Object.Destroy(ownedEventSystem);
            }
            if (originalEventSystem != null)
            {
                originalEventSystem.enabled = true;
            }
            foreach (var raycaster in suspendedRaycasters)
            {
                if (raycaster != null)
                {
                    raycaster.enabled = true;
                }
            }
            suspendedRaycasters.Clear();
            if (camera != null)
            {
                Object.Destroy(camera.gameObject);
            }
            Object.Destroy(template);
            foreach (var asset in temporaryRuleAssets)
            {
                Object.Destroy(asset);
            }
            temporaryRuleAssets.Clear();
            foreach (var device in new InputDevice[] { keyboard, mouse, gamepad, touch })
            {
                if (device != null && device.added)
                {
                    InputSystem.RemoveDevice(device);
                }
            }
            InputSystem.settings = originalSettings;
            Object.Destroy(temporarySettings);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SavedKeyboardAndGamepadBindingsKeepAxisAndStickMagnitude()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            Read();
            Assert.That(input.Selected, Is.EqualTo(1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Read();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
            yield return null;
            input.Read(simulation, .1f, 4, out int wall, out float target);
            Assert.That(wall, Is.EqualTo(1));
            Assert.That(target, Is.EqualTo(6.4f).Within(.001f));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Read();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
            yield return null;
            Read();
            Assert.That(input.Selected, Is.Zero);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            Read();
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.6f, .9f) });
            yield return null;
            input.Read(simulation, .1f, 4, out wall, out target);
            Assert.That(wall, Is.Zero);
            Assert.That(target - simulation.Walls[0].Center.x, Is.InRange(.05f, .399f));
            Assert.That(input.Router.DeviceKind, Is.EqualTo(InputDeviceKind.Gamepad));
            InputSystem.RemoveDevice(gamepad);
            Assert.That(input.Router.PauseState.IsPaused, Is.True);
            Assert.That(input.HeldWall, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator KeyboardEscapePausesGameplayOnceAndMenuCancelDoesNotSubmitPause()
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            Assert.That(input.Read(simulation, .1f, 4, out _, out _), Is.True, "保存的 Esc/Back 在玩法上下文请求暂停。");
            Assert.That(input.Read(simulation, .1f, 4, out _, out _), Is.False, "同一次按键只提交一次暂停。");
            Assert.That(input.Router.ConsumeActions(), Is.EqualTo(GameplayInputActions.None));
            input.Router.SetContext(GameplayInputContext.Menu);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Read();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter, Key.Escape));
            yield return null;
            Assert.That(input.Read(simulation, .1f, 4, out int wall, out _), Is.False, "菜单确认/返回归公共 UI，不能再触发玩法暂停。");
            Assert.That(wall, Is.EqualTo(-1));
            Assert.That(input.Router.ConsumeActions(), Is.EqualTo(GameplayInputActions.None));
        }

        [UnityTest]
        public IEnumerator GlobalFreezeSuppressesWallSelectionAndMovementWhilePauseStillWorks()
        {
            Read();
            int selected = input.Selected;
            using var freeze = GameTimeService.Push(0);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            input.Read(simulation, .1f, 4, out int wall, out _);
            Assert.That(input.Selected, Is.EqualTo(selected));
            Assert.That(wall, Is.EqualTo(-1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Read();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            Assert.That(input.Read(simulation, .1f, 4, out wall, out _), Is.True);
            Assert.That(wall, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator SelectionAndPointerSkipFixedWallsAndNoMovableWallsAreSafe()
        {
            var level = Object.Instantiate(AssetDatabase.LoadAssetAtPath<WallSqueezeLevel>("Assets/LoadResources/Demos/wall_squeeze/Data/Level2.asset"));
            temporaryRuleAssets.Add(level);
            level.Walls[0].IsFixed = true;
            level.FixedWalls = new[]
            {
                new WallSqueezeWallLayout { Axis = 0, Center = new Vector2(15.5f, 8), Size = new Vector2(.24f, 1) }
            };
            simulation = new WallSqueezeSimulation(
                AssetDatabase.LoadAssetAtPath<WallSqueezeSettings>("Assets/LoadResources/Demos/wall_squeeze/Data/Settings.asset"), level);
            Read();
            Assert.That(input.Selected, Is.EqualTo(1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q));
            yield return null;
            Read();
            Assert.That(input.Selected, Is.EqualTo(1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Read();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null;
            Read();
            Assert.That(input.Selected, Is.EqualTo(1));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Read();
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightShoulder));
            yield return null;
            Read();
            Assert.That(input.Selected, Is.EqualTo(1));
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            Read();
            Vector2 point = camera.WorldToScreenPoint(simulation.Walls[0].Center);
            // 首次设备切换先松键，再按住固定墙中央，确保门闩不掩盖命中断言。
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            Read();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            input.Read(simulation, .1f, 4, out int wall, out _);
            Assert.That(input.HeldWall, Is.EqualTo(-1));
            Assert.That(wall, Is.EqualTo(-1));
            Assert.That(input.Selected, Is.EqualTo(1));
            // 运行时若暂时没有可操作墙，也不能索引 -1 或把固定墙发成命令。
            simulation.Walls[1].IsFixed = true;
            input.Read(simulation, .1f, 4, out wall, out _);
            Assert.That(input.Selected, Is.EqualTo(-1));
            Assert.That(wall, Is.EqualTo(-1));
        }

        [UnityTest]
        public IEnumerator UiBlocksMousePressAndPauseRequiresReleaseBeforeDraggingAgain()
        {
            originalEventSystem = EventSystem.current;
            if (originalEventSystem != null)
            {
                originalEventSystem.enabled = false;
            }
            ownedEventSystem = new GameObject("WallSqueezeTestEvents", typeof(EventSystem));
            ownedCanvas = new GameObject("WallSqueezeTestCanvas", typeof(Canvas), typeof(GraphicRaycaster));
            ownedCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(Image));
            blocker.transform.SetParent(ownedCanvas.transform, false);
            Vector2 point = camera.WorldToScreenPoint(simulation.Walls[0].Center);
            var rect = (RectTransform)blocker.transform;
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = point;
            rect.sizeDelta = Vector2.one * 100;
            Canvas.ForceUpdateCanvases();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            Read();
            Assert.That(input.HeldWall, Is.EqualTo(-1));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            Read();
            blocker.SetActive(false);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            Read();
            Assert.That(input.HeldWall, Is.Zero);
            input.Router.PauseState.SetApplicationFocus(false);
            Assert.That(input.HeldWall, Is.EqualTo(-1));
            input.Router.PauseState.SetApplicationFocus(true);
            Assert.That(input.Router.PauseState.IsPaused, Is.True);
            Assert.That(input.Router.PauseState.TryResume(), Is.True);
            Read();
            Assert.That(input.HeldWall, Is.EqualTo(-1));
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            Read();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point, buttons = 1 });
            yield return null;
            Read();
            Assert.That(input.HeldWall, Is.Zero);
        }

        [UnityTest]
        public IEnumerator TouchCapturesOneFingerAndSecondFingerCannotTakeOver()
        {
            Vector2 point = camera.WorldToScreenPoint(simulation.Walls[0].Center);
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, position = point, phase = UnityEngine.InputSystem.TouchPhase.Began });
            yield return null;
            Read();
            Assert.That(input.HeldWall, Is.Zero);
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 2, position = camera.WorldToScreenPoint(simulation.Walls[1].Center), phase = UnityEngine.InputSystem.TouchPhase.Began });
            yield return null;
            Read();
            Assert.That(input.HeldWall, Is.Zero);
            InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, position = point, phase = UnityEngine.InputSystem.TouchPhase.Ended });
            yield return null;
            Read();
            Assert.That(input.HeldWall, Is.EqualTo(-1));
            Read();
            Assert.That(input.HeldWall, Is.EqualTo(-1));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HubRoundTripRapidPauseRetryAndResultLifecycle()
        {
            input.Dispose();
            input = null;
            if (GameSceneNavigator.Instance == null)
            {
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/AppEntrance.unity", LoadSceneMode.Single);
            }
            yield return Wait(() => GameSceneNavigator.Instance != null && !GameSceneNavigator.Instance.IsTransitioning, "启动 Hub");
            if (GameSceneNavigator.Instance.CurrentScene != GameSceneId.Hub)
            {
                yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub).ToCoroutine();
            }
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.WallSqueeze).ToCoroutine();
            yield return Wait(() => (world = Object.FindAnyObjectByType<WallSqueezeWorld>()) != null && UIManager.Instance.Get<WallSqueezeHudView>()?.State == ViewState.Visible, "显示真实 HUD");
            var hudPanel = UIManager.Instance.Get<WallSqueezeHudView>().gameObject.GetComponent<WallSqueezeHudPanel>();
            var touchHint = (TMPro.TMP_Text)typeof(WallSqueezeHudPanel).GetField("hint",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(hudPanel);
            string previousLanguage = LanguageService.CurrentLanguage;
            try
            {
                InputDeviceState.NotifyTouch();
                LanguageService.SelectLanguage("en");
                hudPanel.Render();
                Assert.That(touchHint.gameObject.activeSelf, Is.True);
                Assert.That(touchHint.text, Is.EqualTo("Drag a wall with one finger · Protect the blue residents"));
                LanguageService.SelectLanguage("zh-CN");
                hudPanel.Render();
                Assert.That(touchHint.text, Is.EqualTo("单指拖墙 · 保护蓝色住户"));
            }
            finally
            {
                LanguageService.SelectLanguage(previousLanguage);
                InputDeviceState.Notify(keyboard);
                hudPanel.Render();
            }
            Assert.That(world.GetComponent<WallSqueezePresentation>(), Is.Not.Null);
            Assert.That(world.Simulation.Remaining, Is.EqualTo(4));
            var wallShape = world.transform.Find("Walls").GetChild(0).Find("Shape").GetComponent<SpriteRenderer>();
            var bodyShape = world.transform.Find("Bodies").GetChild(0).Find("Shape").GetComponent<SpriteRenderer>();
            Assert.That(wallShape.bounds.size.x, Is.EqualTo(.24f).Within(.001f));
            Assert.That(wallShape.bounds.size.y, Is.EqualTo(9).Within(.001f));
            Assert.That(bodyShape.bounds.size.x, Is.EqualTo(.8f).Within(.001f));
            Assert.That(bodyShape.bounds.size.y, Is.EqualTo(.8f).Within(.001f));
            float frozenElapsed;
            using (var time = GameTimeService.Push(GameTimeService.Scale))
            using (time.Freeze())
            {
                yield return null;
                frozenElapsed = world.Simulation.ElapsedSeconds;
                Vector2 frozenWall = world.Simulation.Walls[0].Center;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(world.Simulation.ElapsedSeconds, Is.EqualTo(frozenElapsed), "全局冻结停止真实 World.Update 的玩法时钟。");
                Assert.That(world.Simulation.Walls[0].Center, Is.EqualTo(frozenWall));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Wait(() => UIManager.Instance.Get<WallSqueezeMenuView>()?.State == ViewState.Visible, "冻结期间键盘暂停入口");
                var frozenMenu = UIManager.Instance.Get<WallSqueezeMenuView>();
                var selection = frozenMenu.gameObject.GetComponent<WallSqueezeMenuPanel>().FirstSelection;
                Assert.That(selection.GetComponent<Button>().interactable, Is.True);
                EventSystem.current.SetSelectedGameObject(selection);
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Wait(() => !world.Paused && UIManager.Instance.Get<WallSqueezeMenuView>()?.IsEnable != true
                    && world.Input.Router.Context == GameplayInputContext.Interaction, "冻结期间菜单继续入口");
                Assert.That(world.Simulation.ElapsedSeconds, Is.EqualTo(frozenElapsed));
                Assert.That(world.Simulation.Walls[0].Center, Is.EqualTo(frozenWall));
            }
            yield return Wait(() => world.Simulation.ElapsedSeconds > frozenElapsed, "释放冻结后真实玩法时钟恢复");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return null;
            yield return null;
            world.Dispatch(WallSqueezeCommand.Retry);
            yield return null;
            yield return null;
            Assert.That(world.Simulation.Walls[0].Center.x, Is.EqualTo(8).Within(.001f), "游玩中重试必须等待旧移动键释放");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            world.Dispatch(WallSqueezeCommand.Pause);
            world.Dispatch(WallSqueezeCommand.Continue);
            yield return Wait(() => !world.Paused && UIManager.Instance.Get<WallSqueezeMenuView>()?.IsEnable != true && world.Input.Router.Context == GameplayInputContext.Interaction, "快速暂停继续收口");
            world.Dispatch(WallSqueezeCommand.Pause);
            yield return Wait(() => UIManager.Instance.Get<WallSqueezeMenuView>()?.State == ViewState.Visible, "暂停 Modal");
            Vector2 before = world.Simulation.Bodies[0].Center;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(world.Simulation.Bodies[0].Center, Is.EqualTo(before));
            var menu = UIManager.Instance.Get<WallSqueezeMenuView>();
            Assert.That(Array.Find(menu.gameObject.GetComponentsInChildren<Button>(true), button => button.name == "Graphics"), Is.Null,
                "Demo 暂停目录只保留玩法菜单，公共设置入口属于 Hub。");
            Assert.That(UIManager.Instance.Get<WallSqueezeMenuView>(), Is.SameAs(menu));
            Assert.That(menu.State, Is.EqualTo(ViewState.Visible));
            Assert.That(world.Paused, Is.True);
            world.Dispatch(WallSqueezeCommand.Retry);
            yield return Wait(() => !world.Paused && world.Input.Router.Context == GameplayInputContext.Interaction, "暂停后重试");
            world.Simulation.Advance(2.2f, 0, 15.65f);
            yield return null;
            yield return Wait(() => UIManager.Instance.Get<WallSqueezeMenuView>()?.State == ViewState.Visible, "胜利 Modal");
            Assert.That(world.Simulation.Result, Is.EqualTo(WallSqueezeResult.Won));
            world.Dispatch(WallSqueezeCommand.Next);
            yield return Wait(() => world.LevelIndex == 1 && world.Input.Router.Context == GameplayInputContext.Interaction, "下一关");
            world.Dispatch(WallSqueezeCommand.Hub);
            yield return Wait(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "返回 Hub");
            Assert.That(Object.FindAnyObjectByType<WallSqueezeWorld>(), Is.Null);
            Assert.That(GlobalData.Get<WallSqueezeData>(), Is.Null);
            Assert.That(UIManager.Instance.Get<WallSqueezeHudView>()?.IsEnable, Is.Not.True);
            Assert.That(UIManager.Instance.Get<WallSqueezeMenuView>()?.IsEnable, Is.Not.True);
        }

        private void Read() => input.Read(simulation, .016f, 4, out _, out _);

        [UnityTest, Timeout(240000)]
        public IEnumerator PublicSceneReloadAndSwitchReleaseOwnedWidgetsAndModals()
        {
            input.Dispose();
            input = null;
            if (GameSceneNavigator.Instance == null)
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/AppEntrance.unity", LoadSceneMode.Single);
            yield return Wait(() => GameSceneNavigator.Instance != null && !GameSceneNavigator.Instance.IsTransitioning, "正式导航稳定");
            if (GameSceneNavigator.Instance.CurrentScene != GameSceneId.Hub)
                yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub).ToCoroutine();
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.WallSqueeze).ToCoroutine();
            yield return Wait(() => (world = Object.FindAnyObjectByType<WallSqueezeWorld>()) != null &&
                UIManager.Instance.Get<WallSqueezeHudView>()?.State == ViewState.Visible, "旧 Wall HUD");
            var oldWorld = world;
            var oldHud = UIManager.Instance.Get<WallSqueezeHudView>();
            world.Dispatch(WallSqueezeCommand.Pause);
            yield return Wait(() => UIManager.Instance.Get<WallSqueezeMenuView>()?.State == ViewState.Visible, "旧 Wall Modal");
            var oldModal = UIManager.Instance.Get<WallSqueezeMenuView>();
            GameSceneSwitchResult result = default;
            yield return GameSceneNavigator.Instance.ReloadCurrentAsync().ContinueWith(value => result = value).ToCoroutine();
            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded), result.Error);
            yield return Wait(() => (world = Object.FindAnyObjectByType<WallSqueezeWorld>()) != null && world != oldWorld &&
                UIManager.Instance.Get<WallSqueezeHudView>()?.State == ViewState.Visible, "新 Wall HUD");
            Assert.That(oldHud.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(oldModal.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(UIManager.Instance.Get<WallSqueezeHudView>(), Is.Not.SameAs(oldHud));
            Assert.That(UIManager.Instance.Get<WallSqueezeMenuView>(), Is.Null);

            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart).ContinueWith(value => result = value).ToCoroutine();
            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded), result.Error);
            yield return Wait(() => UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "纸艺封面");
            Assert.That(UIManager.Instance.Get<WallSqueezeHudView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<WallSqueezeMenuView>(), Is.Null);
            var kart = Object.FindAnyObjectByType<Hotfix.KagitKart.KagitKartWorld>();
            kart.Dispatch(Hotfix.KagitKart.KagitKartCommand.Settings);
            yield return Wait(() => UIManager.Instance.Get<KagitKartSettingsView>()?.State == ViewState.Visible, "纸艺 owned Modal");
            var kartModal = UIManager.Instance.Get<KagitKartSettingsView>();
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub).ContinueWith(value => result = value).ToCoroutine();
            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded), result.Error);
            yield return Wait(() => UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "Hub 页面");
            Assert.That(kartModal.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(UIManager.Instance.Get<KagitKartSettingsView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<KagitKartTitleView>(), Is.Null);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator DemoNavigationFailureAndBusyPreserveSourceViewsAndCommands()
        {
            input.Dispose();
            input = null;
            if (GameSceneNavigator.Instance == null)
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/AppEntrance.unity", LoadSceneMode.Single);
            yield return Wait(() => GameSceneNavigator.Instance != null && !GameSceneNavigator.Instance.IsTransitioning, "正式启动稳定");
            var official = GameSceneNavigator.Instance;
            if (official.CurrentScene != GameSceneId.Hub) yield return official.SwitchAsync(GameSceneId.Hub).ToCoroutine();
            foreach (var scene in new[] { GameSceneId.WallSqueeze, GameSceneId.KagitKart })
            {
                yield return official.SwitchAsync(scene).ToCoroutine();
                Hotfix.KagitKart.KagitKartWorld kart = null;
                View source;
                if (scene == GameSceneId.WallSqueeze)
                {
                    yield return Wait(() => (world = Object.FindAnyObjectByType<WallSqueezeWorld>()) != null &&
                        UIManager.Instance.Get<WallSqueezeHudView>()?.State == ViewState.Visible, "Wall 来源 HUD");
                    world.Dispatch(WallSqueezeCommand.Pause);
                    yield return Wait(() => UIManager.Instance.Get<WallSqueezeMenuView>()?.State == ViewState.Visible, "Wall 来源 Modal");
                    source = UIManager.Instance.Get<WallSqueezeMenuView>();
                }
                else
                {
                    yield return Wait(() => (kart = Object.FindAnyObjectByType<Hotfix.KagitKart.KagitKartWorld>()) != null &&
                        !kart.Busy && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "Kart 来源 Page");
                    source = UIManager.Instance.Get<KagitKartTitleView>();
                }
                var faults = new PendingFailureSceneRuntime();
                var navigation = new GameSceneNavigator(faults, new GameSceneLoadingPresenter());
                // 故障注入沿用已经真实进入的来源标识，断言只检查公共 View/玩法契约。
                typeof(GameSceneNavigator).GetProperty(nameof(GameSceneNavigator.CurrentScene)).SetValue(navigation, scene);
                typeof(GameSceneNavigator).GetProperty(nameof(GameSceneNavigator.Instance)).SetValue(null, navigation);
                try
                {
                    foreach (bool reload in new[] { true, false })
                    {
                        var pending = (reload ? navigation.ReloadCurrentAsync() : navigation.SwitchAsync(GameSceneId.Dlss)).Preserve();
                        yield return Wait(() => faults.Pending, "真实 Loading 后进入可控资源故障点");
                        Assert.That(UIManager.Instance.Get<CommonLoadingView>().State, Is.EqualTo(ViewState.Visible));
                        Assert.That(source.State, Is.EqualTo(ViewState.Visible), "Loading 不触发来源 OnHide/Destroy。");
                        if (kart != null)
                        {
                            kart.Dispatch(Hotfix.KagitKart.KagitKartCommand.Hub);
                            Assert.That(kart.Busy, Is.False, "忙碌导航拒绝返回命令，不能留下 exiting。");
                        }
                        else world.Dispatch(WallSqueezeCommand.Hub);
                        faults.Fail();
                        GameSceneSwitchResult result = default;
                        yield return pending.ContinueWith(value => result = value).ToCoroutine();
                        Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
                        Assert.That(source.State, Is.EqualTo(ViewState.Visible));
                        Assert.That(UIManager.Instance.cacheStack.GetView(source.GetType()), Is.SameAs(source));
                        Assert.That(UIManager.Instance.Get<CommonLoadingView>(), Is.Null);
                    }
                    // 普通退出失败也必须保留原绑定，不能用重建菜单掩盖来源销毁。
                    if (kart != null) kart.Dispatch(Hotfix.KagitKart.KagitKartCommand.Hub);
                    else world.Dispatch(WallSqueezeCommand.Hub);
                    yield return Wait(() => faults.Pending, "返回 Hub 故障点");
                    LogAssert.Expect(kart != null ? LogType.Exception : LogType.Error,
                        new System.Text.RegularExpressions.Regex("导航来源保留测试失败"));
                    faults.Fail();
                    yield return Wait(() => !navigation.IsTransitioning && source.State == ViewState.Visible && (kart == null || !kart.Busy), "返回失败恢复来源");
                    Assert.That(UIManager.Instance.cacheStack.GetView(source.GetType()), Is.SameAs(source));
                    if (kart != null)
                    {
                        var open = source.gameObject.GetComponentsInChildren<Button>(true).Single(button => button.name == "Open");
                        open.onClick.Invoke();
                        yield return Wait(() => !kart.Busy && UIManager.Instance.Get<KagitKartModeView>()?.State == ViewState.Visible, "失败后原 Page 仍绑定 World");
                    }
                    else
                    {
                        world.Dispatch(WallSqueezeCommand.Continue);
                        yield return Wait(() => !world.Paused && UIManager.Instance.Get<WallSqueezeMenuView>() == null, "失败后恢复 Wall 命令");
                    }
                }
                finally
                {
                    if (faults.Pending) faults.Fail();
                    typeof(GameSceneNavigator).GetProperty(nameof(GameSceneNavigator.Instance)).SetValue(null, official);
                }
                yield return official.SwitchAsync(GameSceneId.Hub).ToCoroutine();
            }
        }

        private sealed class PendingFailureSceneRuntime : IGameSceneRuntime
        {
            private UniTaskCompletionSource<GameSceneRuntimeResult> pending;
            public bool Pending => pending != null;
            public UniTask<GameSceneRuntimeResult> LoadAsync(string address, Action<float> onProgress) => Begin();
            public UniTask<GameSceneRuntimeResult> ReturnToHubAsync(Action<float> onProgress) => Begin();
            private UniTask<GameSceneRuntimeResult> Begin()
            {
                pending = new UniTaskCompletionSource<GameSceneRuntimeResult>();
                return pending.Task;
            }
            public void Fail()
            {
                var completion = pending;
                pending = null;
                completion.TrySetResult(GameSceneRuntimeResult.Failure("导航来源保留测试失败"));
            }
        }

        private static IEnumerator Wait(Func<bool> condition, string description)
        {
            float deadline = Time.realtimeSinceStartup + 45;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(condition(), Is.True, description);
        }
    }
}
#endif
