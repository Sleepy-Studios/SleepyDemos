using System.Collections;
using System.Linq;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Tests.Module
{
    public sealed class GameSettingsServiceTests
    {
        private bool hadPreferences;
        private string preferences;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadPreferences = PlayerPrefs.HasKey(GameSettingsService.PreferenceKey);
            preferences = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            GameSettingsService.Shutdown();
            yield return null;
            PlayerPrefs.DeleteKey(GameSettingsService.PreferenceKey);
            if (!ResourceServices.Default.IsInitialized)
            {
                yield return ResourceServices.Default.InitializeAsync(ResourceInitializeOptions.Default).ToCoroutine();
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameSettingsService.Shutdown();
            Restore(GameSettingsService.PreferenceKey, hadPreferences, preferences);
            PlayerPrefs.Save();
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedInitializationAndSavedCandidateDoNotModifyConfig()
        {
            var first = GameSettingsService.InitializeAsync();
            var second = GameSettingsService.InitializeAsync();
            yield return UniTask.WhenAll(first, second).ToCoroutine();
            var defaults = GameSettingsService.Config.GetDefaults();
            var next = GameSettingsService.Current;
            next.Audio.Master = 0.37f;
            var inputEntry = GameSettingsService.Config.Inputs.Entries.Single(entry => entry.DisplayKey == "Input.WallSqueeze");
            using var router = new GameplayInputRouter(inputEntry.Asset);
            using var rebind = new InputRebindSession(inputEntry.Asset);
            var action = rebind.Asset.FindAction("Interaction/NextGroup", true);
            int bindingIndex = action.bindings.ToList().FindIndex(binding => binding.effectivePath.StartsWith("<Keyboard>"));
            rebind.SetBinding(action.actionMap.name + "/" + action.name, bindingIndex, "<Keyboard>/f12");
            next.BindingOverrides[rebind.SourceId] = rebind.ExportOverrides();
            next.Input.GamepadDeadzone = 0.32f;
            next.Input.GamepadStyle = GamepadStyle.PlayStation;
            SettingsApplyResult result = default;
            yield return GameSettingsService.ApplyAsync(next).ContinueWith(value => result = value).ToCoroutine();
            Assert.That(result.Succeeded, Is.True, result.Message);
            next.Audio.Master = 0;
            Assert.That(GameSettingsService.Current.Audio.Master, Is.EqualTo(0.37f));
            Assert.That(GameSettingsService.Config.GetDefaults().Audio.Master, Is.EqualTo(defaults.Audio.Master));
            Assert.That(LocalDataManager.LoadData<GameSettingsSnapshot>(GameSettingsService.PreferenceKey).Audio.Master, Is.EqualTo(0.37f));
            Assert.That(router.Settings.GamepadDeadzone, Is.EqualTo(0.32f));
            Assert.That(GameSettingsService.CurrentGamepadStyle, Is.EqualTo(GamepadStyle.PlayStation));
            var effective = router.FindAction("Interaction/NextGroup");
            Assert.That(effective.bindings[bindingIndex].effectivePath, Is.EqualTo("<Keyboard>/f12"));
            Assert.That(router.FindAction("Gameplay/Move").enabled, Is.True, "统一提交不能关闭Router已启用的Map");

            GameSettingsService.Shutdown();
            yield return GameSettingsService.InitializeAsync().ToCoroutine();
            yield return null;
            Assert.That(GameSettingsService.IsInitialized, Is.True, "旧驱动延迟销毁不能关闭新初始化");
            Assert.That(GameSettingsService.Current.Audio.Master, Is.EqualTo(0.37f));
            Assert.That(GameSettingsService.CurrentGamepadStyle, Is.EqualTo(GamepadStyle.PlayStation));
        }

        [UnityTest]
        public IEnumerator LegacyControllerIconsUseXboxWithoutDiscardingOtherPreferences()
        {
            yield return GameSettingsService.InitializeAsync().ToCoroutine();
            var saved = GameSettingsService.Current;
            saved.Audio.Master = 0.37f;
            saved.Input.GamepadDeadzone = 0.31f;
            saved.Input.GamepadStyle = GamepadStyle.Xbox;
            LocalDataManager.SaveData(GameSettingsService.PreferenceKey, saved);
            string json = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            foreach (bool missing in new[] { true, false })
            {
                GameSettingsService.Shutdown();
                string legacyJson = missing
                    ? json.Replace("\"GamepadStyle\":1", "\"LegacyGamepadStyle\":1")
                    : json.Replace("\"GamepadStyle\":1", "\"GamepadStyle\":0");
                PlayerPrefs.SetString(GameSettingsService.PreferenceKey, legacyJson);
                yield return GameSettingsService.InitializeAsync().ToCoroutine();
                Assert.That(GameSettingsService.CurrentGamepadStyle, Is.EqualTo(GamepadStyle.Xbox));
                Assert.That(GameSettingsService.Current.Audio.Master, Is.EqualTo(0.37f));
                Assert.That(GameSettingsService.Current.Input.GamepadDeadzone, Is.EqualTo(0.31f));
            }
        }

        [UnityTest]
        public IEnumerator RebindingHeldRouterButtonRequiresReleaseAndParameterChangesKeepPendingInput()
        {
            yield return GameSettingsService.InitializeAsync().ToCoroutine();
            var entry = GameSettingsService.Config.Inputs.Entries.Single(value => value.DisplayKey == "Input.WallSqueeze");
            var originalInputSettings = InputSystem.settings;
            var testInputSettings = Object.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = testInputSettings;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var source = Object.Instantiate(entry.Asset);
            source.devices = new InputDevice[] { keyboard };
            using var router = new GameplayInputRouter(source);
            using var candidate = new InputRebindSession(entry.Asset);
            try
            {
                router.SetContext(GameplayInputContext.Interaction);
                yield return null;
                router.ReadFrame(0.016f);
                var action = candidate.Asset.FindAction("Interaction/NextGroup", true);
                int index = action.bindings.ToList().FindIndex(binding => binding.effectivePath.StartsWith("<Keyboard>"));
                candidate.SetBinding("Interaction/NextGroup", index, "<Keyboard>/f12");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q, Key.F12));
                yield return null;
                // Q 的旧动作尚未消费，新 F12 在设置生效前已经保持按住。
                var settings = GameSettingsService.Current;
                settings.BindingOverrides[candidate.SourceId] = candidate.ExportOverrides();
                SettingsApplyResult result = default;
                yield return GameSettingsService.ApplyAsync(settings).ContinueWith(value => result = value).ToCoroutine();
                Assert.That(result.Succeeded, Is.True, result.Message);
                yield return null;
                router.ReadFrame(0.016f);
                Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.None), "改键必须清掉旧 pending，按住的新绑定也不能立即提交。");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                router.ReadFrame(0.016f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F12));
                yield return null;
                Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.NextGroup), "新键松开后再次按下才允许提交。");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                router.ReadFrame(0.016f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F12, Key.RightArrow));
                yield return null;
                Assert.That(router.ReadFrame(0.016f).InteractionNavigation.x, Is.GreaterThan(0));
                settings = GameSettingsService.Current;
                settings.Audio.Master = 0.45f;
                settings.Input.GamepadDeadzone = 0.31f;
                settings.BindingOverrides[candidate.SourceId] = "\n" + settings.BindingOverrides[candidate.SourceId] + "\n";
                yield return GameSettingsService.ApplyAsync(settings).ContinueWith(value => result = value).ToCoroutine();
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(router.ConsumeActions(), Is.EqualTo(GameplayInputActions.NextGroup), "仅音量/输入参数变更不能清除已发生的玩法输入。");
                Assert.That(router.ReadFrame(0.016f).InteractionNavigation.x, Is.GreaterThan(0), "参数提交不能要求持续方向输入重新归零。");
            }
            finally
            {
                router.Dispose();
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings = originalInputSettings;
                Object.Destroy(testInputSettings);
                Object.Destroy(source);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidSaveFallsBackAndDemoCannotChangeLanguage()
        {
            PlayerPrefs.SetString(GameSettingsService.PreferenceKey, "{broken");
            yield return GameSettingsService.InitializeAsync().ToCoroutine();
            Assert.That(GameSettingsService.Feedback, Is.Null);
            Assert.That(GameSettingsService.Current.Language, Is.EqualTo("zh-CN"));
            var next = GameSettingsService.Current;
            next.Language = "en";
            SettingsApplyResult result = default;
            yield return GameSettingsService.ApplyAsync(next).ContinueWith(value => result = value).ToCoroutine();
            Assert.That(result.Succeeded, Is.False);
            Assert.That(LanguageService.CurrentLanguage, Is.EqualTo("zh-CN"));
            Assert.That(PlayerPrefs.GetString(GameSettingsService.PreferenceKey), Is.EqualTo("{broken"));
            GameSettingsService.Shutdown();
            LocalDataManager.SaveData(GameSettingsService.PreferenceKey, new GameSettingsSnapshot { Language = "unsupported" });
            yield return GameSettingsService.InitializeAsync().ToCoroutine();
            Assert.That(GameSettingsService.Current.Language, Is.EqualTo("zh-CN"), "未知语言记录回退默认，不阻断启动");
        }

        [UnityTest]
        public IEnumerator DisplayCancelRestoresCompleteCandidate()
        {
            yield return GameSettingsService.InitializeAsync().ToCoroutine();
            LocalDataManager.SaveData(GameSettingsService.PreferenceKey, GameSettingsService.Current);
            var previous = GameSettingsService.Current;
            string saved = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            var actual = DisplaySettingsRuntime.Capture();
            var next = previous.Copy();
            next.Graphics.WindowMode = actual.WindowMode == (int)FullScreenMode.Windowed
                ? (int)FullScreenMode.FullScreenWindow : (int)FullScreenMode.Windowed;
            next.Audio.Master = 0.25f;
            bool confirmed = false;
            SettingsApplyResult result = default;
            yield return GameSettingsService.ApplyAsync(next, _ =>
            {
                confirmed = true;
                return UniTask.FromResult(false);
            }).ContinueWith(value => result = value).ToCoroutine();
            Assert.That(confirmed, Is.EqualTo(DisplaySettingsRuntime.SupportsDesktopDisplay));
            Assert.That(result.Canceled, Is.True);
            Assert.That(GameSettingsService.Current.Audio.Master, Is.EqualTo(previous.Audio.Master));
            Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Music), Is.EqualTo(previous.Audio.Master * previous.Audio.Music));
            Assert.That(PlayerPrefs.GetString(GameSettingsService.PreferenceKey), Is.EqualTo(saved));
            float started = Time.realtimeSinceStartup;
            var pending = GameSettingsService.ApplyAsync(next, token => UniTask.Never<bool>(token)).AsTask();
            Assert.That(GameSettingsService.IsApplying, Is.True);
            yield return GameSettingsService.ApplyAsync(previous).ContinueWith(value => result = value).ToCoroutine();
            Assert.That(result.Succeeded, Is.False, "显示确认期间禁止重复提交");
            while (!pending.IsCompleted)
            {
                yield return null;
            }
            Assert.That(pending.Result.Canceled, Is.True, "未确认15秒后恢复旧显示");
            Assert.That(Time.realtimeSinceStartup - started, Is.InRange(14.5f, 18f));
            Assert.That(PlayerPrefs.GetString(GameSettingsService.PreferenceKey), Is.EqualTo(saved));
        }

        private static void Restore(string key, bool existed, string value)
        {
            if (existed)
            {
                PlayerPrefs.SetString(key, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(key);
            }
        }
    }
}
