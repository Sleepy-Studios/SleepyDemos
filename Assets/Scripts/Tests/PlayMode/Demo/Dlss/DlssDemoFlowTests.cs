#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Core.Runtime;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using Core.Runtime.TimeControls;
using Core.Runtime.Rendering.Streamline;
using Hotfix;
using Hotfix.Dlss;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class DlssDemoFlowTests
    {
        private bool isUiTest;
        [UnityTest]
        public IEnumerator Controls_RebindingKeepsOneSubscriptionAndDisableReleasesHold()
        {
            isUiTest = true;
            if (!GameSettingsService.IsInitialized)
            {
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/AppEntrance.unity", LoadSceneMode.Single);
                yield return WaitUntil(() => GameSettingsService.IsInitialized, "正式公共偏好服务初始化");
            }
            yield return VerifyControlsPreferencesAsync().ToCoroutine();
        }

        private async UniTask VerifyControlsPreferencesAsync()
        {
            var previousSettings = GameSettingsService.Current;
            bool hadPreference = PlayerPrefs.HasKey(GameSettingsService.PreferenceKey);
            string preference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            var languageFields = new[] { "texts", "fonts", "languages", "<CurrentLanguage>k__BackingField" }
                .Select(name => typeof(LanguageService).GetField(name,
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)).ToArray();
            var previousLanguageValues = languageFields.Select(field => field.GetValue(null)).ToArray();
            var host = new GameObject("DlssControlsOwner"); host.SetActive(false);
            var owner = host.AddComponent<DlssDemoController>();
            var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/LoadResources/Demos/dlss/Data/Dlss.inputactions");
            var session = new Core.Runtime.Inputs.InputActionSession(asset);
            typeof(DlssDemoController).GetField("input", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(owner, session);
            var controls = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/dlss/Prefabs/UI/DlssControls.prefab"));
            var presenter = new DlssControlsView { Loader = new DroneFlightHudBindingPlayModeTests.OwnedObjectLoader() };
            var state = GlobalData.Add(new DlssData(owner));
            var buttons = controls.GetComponentsInChildren<Core.Runtime.Inputs.InputCommandButton>(true);
            try
            {
                var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameSettingsConfig>("Assets/" + GameSettingsService.ConfigAddress + ".asset");
                LanguageService.Initialize(config.Language, config.Fonts, "en");
                presenter.InitWithGameObject(controls);
                var index = controls.GetComponent<ComponentItemIndex>();
                Assert.That(index.Components, Has.None.Null);
                for (int i = 0; i < index.Components.Length; i++)
                {
                    Assert.That(index.ComponentTypes[i], Is.EqualTo(index.Components[i].GetType().FullName));
                    Assert.That(index.BindingKeys[i], Is.Not.Empty);
                }
                presenter.SetData(session.Asset); presenter.SetData(session.Asset);
                await presenter.ShowAsync(false);
                foreach (var prompt in controls.GetComponentsInChildren<Core.Runtime.Inputs.InputBindingPrompt>(true))
                {
                    string key = prompt.Action.name switch
                    {
                        "Sprint" => "Input.Sprint",
                        "Reset" => "Input.ResetView",
                        "Exit" => "Input.ReturnHub",
                        _ => throw new InvalidOperationException("测试收到未知正式观察动作。")
                    };
                    var caption = prompt.transform.Find("Caption").GetComponent<TMPro.TMP_Text>();
                    Assert.That(caption.text, Is.EqualTo(LanguageService.Get(key)), prompt.name);
                    Assert.That(caption.text, Does.Not.StartWith("Input."));
                    Assert.That(prompt.Action, Is.SameAs(session.Asset.FindAction(prompt.Action.id)), "底部与触控提示都读取同一生效副本。");
                }
                foreach (var button in buttons)
                    foreach (var name in new[] { "Clicked", "HoldChanged" })
                    {
                        var callbacks = typeof(Core.Runtime.Inputs.InputCommandButton).GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(button) as Delegate;
                        Assert.That(callbacks?.GetInvocationList().Length, Is.EqualTo(1), button.name + "/" + name);
                    }
                var hidden = GameSettingsService.Current;
                hidden.Accessibility.ShowInputPrompts = false;
                var applied = await GameSettingsService.ApplyAsync(hidden, _ => UniTask.FromResult(true));
                Assert.That(applied.Succeeded, Is.True, applied.Message);
                Core.Runtime.Inputs.InputDeviceState.NotifyTouch();
                await UniTask.Yield();
                foreach (var prompt in controls.GetComponentsInChildren<Core.Runtime.Inputs.InputBindingPrompt>(true))
                {
                    if (prompt.GetComponentInParent<Core.Runtime.Inputs.InputCommandButton>(true) == null)
                    {
                        Assert.That(prompt.gameObject.activeSelf, Is.False, "关闭偏好同时收起纯提示的键帽和说明。");
                    }
                    else
                    {
                        Assert.That(prompt.gameObject.activeInHierarchy, Is.True, "必需触控命令不能随纯提示一起隐藏。");
                        Assert.That(prompt.transform.Find("Caption").GetComponent<TMPro.TMP_Text>().text, Is.Not.Empty);
                    }
                }
                var sprint = System.Array.Find(buttons, button => button.Command == "Sprint");
                Assert.That(sprint, Is.Not.Null);
                sprint.OnPointerDown(new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { pointerId = 15 });
                Assert.That(presenter.Sprint, Is.True);
                await presenter.HideAsync(false);
                await UniTask.Yield();
                Assert.That(presenter.Sprint, Is.False);
                await presenter.ShowAsync(false);
                await UniTask.Yield();
                Assert.That(presenter.Sprint, Is.False);
                await presenter.DestroyAsync();
                await UniTask.Yield();
                foreach (var button in buttons)
                {
                    var callbacks = typeof(Core.Runtime.Inputs.InputCommandButton).GetField("Clicked", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(button) as Delegate;
                    Assert.That(callbacks, Is.Null, "销毁配对解除订阅");
                }
            }
            finally
            {
                try
                {
                    if (presenter.IsLoaded) await presenter.DestroyAsync();
                    state.Handler.Dispose();
                    GlobalData.Remove<DlssData>();
                    if (controls != null) Object.Destroy(controls);
                    Object.Destroy(host);
                }
                finally
                {
                    session.Dispose();
                    try
                    {
                        var restored = await GameSettingsService.ApplyAsync(previousSettings, _ => UniTask.FromResult(true));
                        Assert.That(restored.Succeeded, Is.True, restored.Message);
                    }
                    finally
                    {
                        if (hadPreference) PlayerPrefs.SetString(GameSettingsService.PreferenceKey, preference);
                        else PlayerPrefs.DeleteKey(GameSettingsService.PreferenceKey);
                        PlayerPrefs.Save();
                        for (int i = 0; i < languageFields.Length; i++) languageFields[i].SetValue(null, previousLanguageValues[i]);
                    }
                }
            }
            await UniTask.Yield();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FrozenObservationStopsWorldButKeepsResetAndExitAvailable()
        {
            isUiTest = true;
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                while (!startup.isDone) yield return null;
            }
            yield return WaitUntil(() => UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning, "Hub");
            Assert.That(UIManager.Instance.Get<HubSettingsView>(), Is.Null);
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Dlss).ToCoroutine();
            DlssDemoController owner = null;
            yield return WaitUntil(() => (owner = Object.FindFirstObjectByType<DlssDemoController>()) != null && owner.Actions != null && UIManager.Instance.Get<DlssControlsView>() != null, "观察控件");
            var controls = UIManager.Instance.Get<DlssControlsView>();
            Assert.That(controls.gameObject.GetComponentsInChildren<Core.Runtime.Inputs.InputBindingPrompt>(true)
                .Any(prompt => prompt.Action?.name == "Settings"), Is.False, "Demo 不保留公共设置操作提示。");
            var mode = StreamlineRuntime.RequestedMode;
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var moving = (Transform)typeof(DlssDemoController).GetField("movingObject", fields).GetValue(owner);
            var spinning = (Transform)typeof(DlssDemoController).GetField("spinningObject", fields).GetValue(owner);
            var camera = (Camera)typeof(DlssDemoController).GetField("worldCamera", fields).GetValue(owner);
            Assert.That(moving, Is.Not.Null);
            Assert.That(spinning, Is.Not.Null);
            Assert.That(camera, Is.Not.Null);
            var homePosition = (Vector3)typeof(DlssDemoController).GetField("homePosition", fields).GetValue(owner);
            var homeRotation = (Quaternion)typeof(DlssDemoController).GetField("homeRotation", fields).GetValue(owner);
            var previousInputSettings = InputSystem.settings;
            var inputSettings = Object.Instantiate(previousInputSettings);
            inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = inputSettings;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                // 隔离本用例的正式运行副本，原制作资产与公共存档不变。
                owner.Actions.Asset.devices = new InputDevice[] { keyboard };
                owner.Actions.Asset.RemoveAllBindingOverrides();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return WaitUntil(() => Vector3.Distance(camera.transform.position, homePosition) > .05f, "真实观察输入离开初始视角");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Quaternion frozenRotation;
                using (var time = GameTimeService.Push(GameTimeService.Scale))
                using (time.Freeze())
                {
                    // 等当前已采样的 Unity 帧结束，再观察真实动画消费者。
                    yield return new WaitForEndOfFrame();
                    Vector3 frozenMoving = moving.position;
                    frozenRotation = spinning.rotation;
                    Vector3 frozenCamera = camera.transform.position;
                    Quaternion frozenCameraRotation = camera.transform.rotation;
                    yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(moving.position, Is.EqualTo(frozenMoving));
                    Assert.That(Quaternion.Angle(spinning.rotation, frozenRotation), Is.LessThan(.001f));
                    Assert.That(camera.transform.position, Is.EqualTo(frozenCamera));
                    Assert.That(Quaternion.Angle(camera.transform.rotation, frozenCameraRotation), Is.LessThan(.001f));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                    yield return null;
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return new WaitForSecondsRealtime(.15f);
                    Assert.That(UIManager.Instance.Get<HubSettingsView>(), Is.Null, "旧 Esc 快捷键不能从 Demo 打开 Hub 公共设置。");
                    Assert.That(owner.Actions.Map.name, Is.EqualTo("Observe"));
                    Assert.That(controls.gameObject.activeInHierarchy, Is.True);
                    Assert.That(camera.transform.position, Is.EqualTo(frozenCamera));
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.R));
                    yield return null;
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return WaitUntil(() => Vector3.Distance(camera.transform.position, homePosition) < .001f
                        && Quaternion.Angle(camera.transform.rotation, homeRotation) < .001f, "冻结期间真实 Reset 恢复观察视角");
                    Assert.That(moving.position, Is.EqualTo(frozenMoving));
                    Assert.That(Quaternion.Angle(spinning.rotation, frozenRotation), Is.LessThan(.001f));
                }
                yield return WaitUntil(() => Quaternion.Angle(spinning.rotation, frozenRotation) > .1f, "释放冻结后真实场景动画恢复");
                Assert.That(StreamlineRuntime.RequestedMode, Is.EqualTo(mode));
                using (var time = GameTimeService.Push(GameTimeService.Scale))
                using (time.Freeze())
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Backspace));
                    yield return null;
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    yield return WaitUntil(() => owner == null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub
                        && !GameSceneNavigator.Instance.IsTransitioning, "冻结期间真实返回动作完成 Hub 导航");
                    Assert.That(UIManager.Instance.Get<DlssControlsView>(), Is.Null);
                    Assert.That(UIManager.Instance.Get<HubSettingsView>(), Is.Null);
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = previousInputSettings;
                Object.Destroy(inputSettings);
            }
        }

        [UnityTest]
        public IEnumerator EditorDirectStartupUsesSharedSettings()
        {
            if (!StreamlineRuntime.IsBackendSupported) Assert.Ignore("Windows Editor DX12/Vulkan required.");
            var load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/LoadResources/Demos/drone_flight/Scenes/Main.unity", new LoadSceneParameters(LoadSceneMode.Single));
            while (!load.isDone) yield return null;
            yield return WaitUntil(() => GameSceneNavigator.Instance?.IsEditorDirect == true, "Editor direct rendering service");
            Assert.That(UIManager.Instance.Get<HubSettingsView>(), Is.Null, "直启不自动加载 Hub 设置。");
            StreamlineRuntime.ApplyModeAsync(StreamlineDlssMode.Quality).Forget();
            yield return WaitUntil(() => StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "Editor direct Quality");
            Assert.AreSame(UIRootManager.Instance.BaseCamera, StreamlineRuntime.BoundCamera);
        }

        [UnityTest]
        public IEnumerator FormalStartupModesAndHubReturn()
        {
            if (!StreamlineRuntime.IsBackendSupported) Assert.Ignore("Requires Windows Editor DX12/Vulkan.");
            // Only the real startup shell is loaded directly. All Demo transitions use the Hub button/navigation.
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                while (!startup.isDone) yield return null;
            }
            yield return WaitUntil(() => GameSceneNavigator.Instance != null &&
                UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "Formal Hub startup");
            Assert.IsTrue(StreamlineRuntime.IsInitialized);
            string folder = Path.GetFullPath("Library/Streamline/evidence/demo/" + SystemInfo.graphicsDeviceType);
            Directory.CreateDirectory(folder);
            yield return new WaitForEndOfFrame();
            var hubScreenshot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, "Hub.png"), hubScreenshot.EncodeToPNG());
            Object.Destroy(hubScreenshot);
            StreamlineRuntime.ApplyModeAsync(null).Forget();
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Off");
            Assert.IsNull(StreamlineRuntime.RequestedMode);
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.DroneFlight).AsTask();
            yield return WaitUntil(() => travel.IsCompleted, "DroneFlight");
            Assert.That(travel.Result.Status, Is.Not.EqualTo(GameSceneSwitchStatus.Failed));
            yield return WaitUntil(() => UIManager.Instance.Get<DroneFlightVehicleSelectView>()?.State == ViewState.Visible, "Vehicle selection");
            Click(UIManager.Instance.Get<DroneFlightVehicleSelectView>(), "StartButton");
            yield return WaitUntil(() => UIManager.Instance.Get<DroneFlightHudView>()?.State == ViewState.Visible, "Flight HUD");
            StreamlineRuntime.ApplyModeAsync(StreamlineDlssMode.Quality).Forget();
            yield return WaitUntil(() => StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "DroneFlight Quality: " + StreamlineRuntime.Status);
            Assert.AreEqual(UIRootManager.Instance.BaseCamera, StreamlineRuntime.BoundCamera);
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Backspace));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.RemoveDevice(keyboard);
            yield return WaitUntil(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning && StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "Hub preserved Quality");
            yield return new WaitForEndOfFrame();
            int uiHosts = 0;
            foreach (var candidate in Camera.allCameras)
            {
                var candidateData = candidate.GetUniversalAdditionalCameraData();
                if (candidate.enabled && candidateData.renderType == CameraRenderType.Base && candidateData.cameraStack.Contains(UIRootManager.Instance.UICamera)) uiHosts++;
            }
            Assert.AreEqual(1, uiHosts, "UI lost its presentation camera after unloading the previous Demo.");
            StreamlineRuntime.ApplyModeAsync(null).Forget();
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Disable in returned Hub");
            Assert.IsTrue(UIRootManager.Instance.BaseCamera.enabled, "Hub camera stayed disabled after DLSS shutdown.");
            StreamlineRuntime.ApplyModeAsync(StreamlineDlssMode.Quality).Forget();
            yield return WaitUntil(() => StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "Re-enable in Hub");
            travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Dlss).AsTask();
            yield return WaitUntil(() => travel.IsCompleted && StreamlineRuntime.EffectiveMode == StreamlineDlssMode.Quality, "Lab inherited Quality");
            StreamlineDlssMode?[] modes = { null, StreamlineDlssMode.Balanced, StreamlineDlssMode.Performance, StreamlineDlssMode.UltraPerformance, StreamlineDlssMode.Dlaa, StreamlineDlssMode.Quality };
            foreach (var mode in modes)
            {
                StreamlineRuntime.ApplyModeAsync(mode).Forget();
                yield return WaitUntil(() => !StreamlineRuntime.IsBusy && StreamlineRuntime.EffectiveMode == mode, "Mode " + mode);
                yield return new WaitForEndOfFrame();
                var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(folder, (mode?.ToString() ?? "Off") + ".png"), screenshot.EncodeToPNG());
                var pixels = screenshot.GetPixels();
                float minimum = float.MaxValue, maximum = float.MinValue;
                for (int y = screenshot.height / 3; y < screenshot.height * 2 / 3; y += 9)
                for (int x = screenshot.width / 4; x < screenshot.width * 3 / 4; x += 9)
                {
                    float r = pixels[y * screenshot.width + x].r;
                    minimum = Mathf.Min(minimum, r); maximum = Mathf.Max(maximum, r);
                }
                Assert.Greater(maximum - minimum, 0.1f, "Blank world: " + StreamlineRuntime.Status);
                Object.Destroy(screenshot);
            }
            Assert.That(StreamlineRuntime.RequestedMode, Is.EqualTo(StreamlineDlssMode.Quality));
            var main = StreamlineRuntime.BoundCamera;
            Vector3 screen = main.WorldToScreenPoint(main.transform.position + main.transform.forward * 10);
            Assert.That(Vector2.Distance(screen, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), Is.LessThan(0.5f));
            Assert.That(Vector3.Dot(main.ScreenPointToRay(screen).direction, main.transform.forward), Is.GreaterThan(0.999f));
            yield return VerifyPostProcessing(main);
            using (var resize = new GameViewResolution(1280, 720))
            {
                yield return WaitUntil(() => StreamlineRuntime.OutputSize == new Vector2Int(1280, 720) && !StreamlineRuntime.IsBusy && StreamlineRuntime.EffectiveMode.HasValue, "Resize to 720p");
            }
            yield return null;
            yield return null;
            yield return WaitUntil(() => StreamlineRuntime.OutputSize == new Vector2Int(Screen.width, Screen.height) && !StreamlineRuntime.IsBusy && StreamlineRuntime.EffectiveMode.HasValue, "Restore resolution");
            double resumeAt = UnityEditor.EditorApplication.timeSinceStartup + 0.3;
            UnityEditor.EditorApplication.CallbackFunction resume = null;
            resume = () =>
            {
                if (UnityEditor.EditorApplication.timeSinceStartup < resumeAt) return;
                UnityEditor.EditorApplication.update -= resume;
                UnityEditor.EditorApplication.isPaused = false;
            };
            UnityEditor.EditorApplication.update += resume;
            UnityEditor.EditorApplication.isPaused = true;
            yield return null;
            int rendered = StreamlineCaptureFeature.Session.CapturedFrames;
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy && StreamlineRuntime.EffectiveMode.HasValue && StreamlineCaptureFeature.Session.CapturedFrames > rendered + 3, "Resume");
            StreamlineRuntime.ApplyModeAsync(null).Forget();
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Before unsupported camera");
            main.orthographic = true;
            StreamlineRuntime.ApplyModeAsync(StreamlineDlssMode.Quality).Forget();
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Unsupported camera fallback");
            Assert.IsNull(StreamlineRuntime.EffectiveMode);
            Assert.AreEqual(StreamlineDlssMode.Quality, StreamlineRuntime.RequestedMode);
            Assert.IsFalse(StreamlineRuntime.CanEnable(out _));
            main.orthographic = false;
            StreamlineRuntime.ApplyModeAsync(null).Forget();
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Disable");
            Assert.IsNull(StreamlineCaptureFeature.Session);
            Assert.IsFalse(StreamlineRuntime.RequiresRestart);
            travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub).AsTask();
            yield return WaitUntil(() => travel.IsCompleted, "Final Hub");
        }

        [UnityTearDown]
        public IEnumerator CloseDemoIfNeeded()
        {
            if (isUiTest) { isUiTest = false; yield break; }
            StreamlineRuntime.ApplyModeAsync(null).Forget();
            yield return WaitUntil(() => !StreamlineRuntime.IsBusy, "Cleanup");
            if (GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene != GameSceneId.Hub)
                {
                yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub).ToCoroutine();
                yield return WaitUntil(() => GameSceneNavigator.Instance?.IsEditorDirect == false && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "Cleanup Hub");
            }
        }

        private static void Click(View view, string name)
        {
            Assert.IsNotNull(view);
            foreach (var button in view.gameObject.GetComponentsInChildren<Button>(true))
            {
                if (button.name != name) continue;
                Assert.IsTrue(button.interactable, name + " is disabled.");
                button.onClick.Invoke();
                return;
            }
            Assert.Fail("Button not found: " + name);
        }

        private static IEnumerator VerifyPostProcessing(Camera camera)
        {
            yield return new WaitForEndOfFrame();
            float before = CaptureWorldMean();
            var data = camera.GetUniversalAdditionalCameraData();
            LayerMask previousMask = data.volumeLayerMask;
            var item = new GameObject("Temporary exposure validation") { layer = 31 };
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var exposure = profile.Add<ColorAdjustments>();
            exposure.postExposure.Override(-3);
            var volume = item.AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10000; volume.sharedProfile = profile;
            try
            {
                data.volumeLayerMask = 1 << 31;
                for (int frame = 0; frame < 8; frame++) yield return null;
                yield return new WaitForEndOfFrame();
                Assert.Less(CaptureWorldMean(), before * 0.65f, "Post exposure did not reach the presented world image.");
            }
            finally
            {
                data.volumeLayerMask = previousMask;
                Object.Destroy(item); Object.Destroy(exposure); Object.Destroy(profile);
            }
        }

        private static float CaptureWorldMean()
        {
            var screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            var pixels = screenshot.GetPixels();
            float total = 0; int count = 0;
            for (int y = screenshot.height / 4; y < screenshot.height / 2; y += 8)
            for (int x = screenshot.width / 4; x < screenshot.width / 2; x += 8)
            {
                total += pixels[y * screenshot.width + x].grayscale; count++;
            }
            Object.Destroy(screenshot);
            return total / count;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, string step)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 90;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(condition(), "Timed out: " + step + " / " + StreamlineRuntime.Status);
        }
    }
}
#endif
