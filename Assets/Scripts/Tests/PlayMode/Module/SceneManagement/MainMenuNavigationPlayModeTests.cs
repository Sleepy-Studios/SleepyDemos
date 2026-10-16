#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Presentation;
using Core.Runtime.Rendering;
using Core.Runtime.Settings;
using Core.Runtime.TimeControls;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.SceneManagement;
using SleepyStudios.LoopScroll;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    /// 通过 InputSystem 指针、键盘和手柄验证 Hub 进入及回滚，不调用按钮监听器模拟操作。
    public sealed class MainMenuNavigationPlayModeTests
    {
        private GameSceneNavigator originalNavigator;
        private FailingSceneRuntime runtime;
        private Mouse mouse;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Touchscreen touchscreen;
        private InputSettings originalInputSettings;
        private InputSettings testInputSettings;
        private Tests.Demo.GameViewResolution resolution;

        [UnityTest, Timeout(180000)]
        public IEnumerator LoadingOccludesUpperLayersAndRestoresTheirAuthoredState()
        {
            yield return PrepareHub();
            var layers = new[] { UILayer.Foreground, UILayer.Pop, UILayer.Decorate, UILayer.Tip }
                .Select(layer => UIRootManager.Instance.GetRoot(layer)).ToArray();
            var original = layers.Select(root => (Canvas: root.GetComponent<Canvas>(), Enabled: root.GetComponent<Canvas>().enabled,
                Raycaster: root.GetComponent<GraphicRaycaster>(), RaycasterEnabled: root.GetComponent<GraphicRaycaster>().enabled)).ToArray();
            int tipOrder = layers[3].GetComponent<Canvas>().sortingOrder;
            var source = new GameObject("Loading Source Modal Witness", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            source.layer = layers[1].gameObject.layer;
            source.transform.SetParent(layers[1], false);
            source.GetComponent<CanvasGroup>().alpha = 0.37f;
            GameObject target = null;
            var presenter = new GameSceneLoadingPresenter();
            CommonTipsView sourceTip = null;
            try
            {
                yield return UIManager.Instance.PreloadAsync<CommonTipsView>().ToCoroutine();
                sourceTip = UIManager.Instance.Get<CommonTipsView>();
                var sourceGroup = sourceTip.gameObject.GetComponent<CanvasGroup>();
                sourceGroup.alpha = 0.37f;
                sourceGroup.interactable = true;
                sourceGroup.blocksRaycasts = false;
                // 作者关闭的层与射线不能被 Loading 收口强行打开。
                layers[0].GetComponent<Canvas>().enabled = false;
                layers[3].GetComponent<GraphicRaycaster>().enabled = false;
                var authored = layers.Select(root => (Canvas: root.GetComponent<Canvas>(), Enabled: root.GetComponent<Canvas>().enabled,
                    Raycaster: root.GetComponent<GraphicRaycaster>(), RaycasterEnabled: root.GetComponent<GraphicRaycaster>().enabled)).ToArray();
                Assert.That(GameSceneCatalog.TryGet(GameSceneId.Hub, out var hub), Is.True);
                foreach (bool restoreFailure in new[] { true, false })
                {
                    string error = null;
                    yield return presenter.BeginAsync(hub).ContinueWith(value => error = value).ToCoroutine();
                    Assert.That(error, Is.Null);
                    Assert.That(UIManager.Instance.Get<CommonLoadingView>().State, Is.EqualTo(ViewState.Visible));
                    Assert.That(UIRootManager.Instance.GetRoot(UILayer.Base).GetComponent<Canvas>().enabled, Is.True);
                    var loading = UIManager.Instance.Get<CommonLoadingView>();
                    Assert.That(loading.transform.parent, Is.SameAs(layers[3]));
                    Assert.That(loading.gameObject.GetComponent<Canvas>(), Is.Null, "Loading 复用公共层 Canvas。");
                    Assert.That(layers[3].GetComponent<Canvas>().sortingOrder, Is.GreaterThan(tipOrder));
                    for (int index = 0; index < authored.Length; index++)
                    {
                        Assert.That(authored[index].Canvas.enabled, Is.EqualTo(index == 3));
                        Assert.That(authored[index].Raycaster.enabled, Is.EqualTo(index == 3));
                    }
                    Assert.That(source.activeInHierarchy, Is.True, "遮蔽不能退出来源 UI 激活生命周期。");
                    Assert.That(source.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0.37f));
                    Assert.That(sourceGroup.interactable, Is.False);
                    Assert.That(sourceGroup.blocksRaycasts, Is.False);
                    Assert.That(sourceGroup.alpha, Is.EqualTo(0.37f));
                    if (!restoreFailure)
                    {
                        target = new GameObject("Loading Target Widget Witness", typeof(RectTransform), typeof(Image));
                        target.layer = layers[2].gameObject.layer;
                        target.transform.SetParent(layers[2], false);
                        Assert.That(target.activeInHierarchy, Is.True, "加载期间目标控件仍可初始化。");
                    }
                    if (restoreFailure) yield return presenter.RestoreAsync(GameSceneId.Hub).ToCoroutine();
                    else yield return presenter.CompleteAsync(GameSceneId.Hub).ContinueWith(value => error = value).ToCoroutine();
                    Assert.That(error, Is.Null);
                    Assert.That(layers[3].GetComponent<Canvas>().sortingOrder, Is.EqualTo(tipOrder));
                    Assert.That(source, Is.Not.Null, "失败恢复保留原实例。");
                    Assert.That(UIManager.Instance.Get<CommonTipsView>(), Is.SameAs(sourceTip));
                    Assert.That(sourceGroup.interactable, Is.True);
                    Assert.That(sourceGroup.blocksRaycasts, Is.False);
                    foreach (var layer in authored)
                    {
                        Assert.That(layer.Canvas.enabled, Is.EqualTo(layer.Enabled));
                        Assert.That(layer.Raycaster.enabled, Is.EqualTo(layer.RaycasterEnabled));
                    }
                }
                Assert.That(target.activeInHierarchy, Is.True);
            }
            finally
            {
                foreach (var layer in original)
                {
                    layer.Canvas.enabled = layer.Enabled;
                    layer.Raycaster.enabled = layer.RaycasterEnabled;
                }
                Object.Destroy(source);
                if (target != null) Object.Destroy(target);
                if (sourceTip != null) UIManager.Instance.CloseAsync(sourceTip, false).Forget();
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator LoadingCleanupAfterOfficialHubStartupRemovesOwnedInstance()
        {
            yield return PrepareHub();
            Assert.That(GameSceneCatalog.TryGet(GameSceneId.Hub, out var hub), Is.True);
            var presenter = new GameSceneLoadingPresenter();
            string error = null;
            int startedFrame = Time.frameCount;
            yield return presenter.BeginAsync(hub).ContinueWith(value => error = value).ToCoroutine();
            Assert.That(error, Is.Null);
            Assert.That(Time.frameCount, Is.GreaterThan(startedFrame));
            var loading = UIManager.Instance.Get<CommonLoadingView>();
            Assert.That(loading.State, Is.EqualTo(ViewState.Visible));

            yield return UIManager.Instance.OpenAsync<MainMenuView>(new UIOpenOptions(false)).ToCoroutine();
            yield return presenter.CompleteAsync(GameSceneId.Hub).ContinueWith(value => error = value).ToCoroutine();
            Assert.That(error, Is.Null);
            Assert.That(loading.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(UIManager.Instance.Get<CommonLoadingView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<MainMenuView>().State, Is.EqualTo(ViewState.Visible));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator CommonLoadingAndFeedbackFollowHubLanguageAndPreserveDiagnostics()
        {
            yield return PrepareHub();
            yield return VerifyLoadingAndFeedbackAsync().ToCoroutine();
        }

        private async UniTask VerifyLoadingAndFeedbackAsync()
        {
            var previous = GameSettingsService.Current;
            Func<UniTask<HubSettingsView>> reopen = () => UniTask.FromResult<HubSettingsView>(null);
            try
            {
                foreach (string language in new[] { "zh-CN", "en" })
                {
                    var next = GameSettingsService.Current;
                    next.Language = language;
                    var applied = await HubLanguageFlow.ApplyAsync(next, reopen);
                    Assert.That(applied.Succeeded, Is.True, applied.Message);
                    await UniTask.WaitUntil(IsStableHub).Timeout(TimeSpan.FromSeconds(15));
                    var navigator = GameSceneNavigator.Instance;
                    try
                    {
                        SetNavigator(null);
                        GlobalData.Dispatch(new MainMenuEnterAction(GameSceneId.WallSqueeze));
                        Assert.That(GlobalData.Get<MainMenuData>().FeedbackKey, Is.EqualTo("Hub.EntryNotReady"));
                        AssertMappedText(Field<TMPro.TextMeshProUGUI>(UIManager.Instance.Get<MainMenuView>(), "TextMeshProUGUI_Status"), "Hub.EntryNotReady");
                    }
                    finally
                    {
                        SetNavigator(navigator);
                    }

                    GlobalData.Dispatch(new MainMenuSelectAction(SceneIndex(GameSceneId.WallSqueeze)));
                    GlobalData.Dispatch(new MainMenuEnterAction(GameSceneId.WallSqueeze));
                    await UniTask.WaitUntil(() => runtime.HasPendingEntry).Timeout(TimeSpan.FromSeconds(15));
                    var loading = UIManager.Instance.Get<CommonLoadingView>();
                    Assert.That(loading, Is.Not.Null);
                    AssertMappedText(Field<TMPro.TextMeshProUGUI>(loading, "TextMeshProUGUI_Title"), "Hub.wall_squeeze.Title");
                    AssertMappedText(Field<TMPro.TextMeshProUGUI>(loading, "TextMeshProUGUI_Step"), "Loading.LoadScene");
                    AssertMappedText(Field<TMPro.TextMeshProUGUI>(loading, "TextMeshProUGUI_Description"), "Loading.LoadingScene", LanguageService.Get("Hub.wall_squeeze.Title"));
                    AssertMappedText(Field<TMPro.TextMeshProUGUI>(loading, "TextMeshProUGUI_ProgressText"), "Loading.Progress", 10);
                    loading.SetProgress(0.7f, "重新运行场景", "正在重新加载 DLSS", stepKey: "Loading.ReloadScene",
                        descriptionKey: "Loading.ReloadingScene", args: new object[] { LanguageService.Get("Hub.wall_squeeze.Title") });
                    AssertMappedText(Field<TMPro.TextMeshProUGUI>(loading, "TextMeshProUGUI_Description"), "Loading.ReloadingScene", LanguageService.Get("Hub.wall_squeeze.Title"));
                    loading.SetProgress(0.2f, "加载完成", "", stepKey: "Loading.Complete");
                    Assert.That(Field<TMPro.TextMeshProUGUI>(loading, "TextMeshProUGUI_Description").text, Is.Empty);
                    AssertMappedText(Field<TMPro.TextMeshProUGUI>(loading, "TextMeshProUGUI_ProgressText"), "Loading.Progress", 70);

                    ExpectEntryFailure(GameSceneId.WallSqueeze);
                    runtime.FailEntry();
                    await UniTask.WaitUntil(IsStableHub).Timeout(TimeSpan.FromSeconds(15));
                    var menu = UIManager.Instance.Get<MainMenuView>();
                    var feedback = Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_Status");
                    Assert.That(GlobalData.Get<MainMenuData>().FeedbackKey, Is.EqualTo("Hub.EntryFailed"));
                    Assert.That(GlobalData.Get<MainMenuData>().Feedback, Is.Null);
                    AssertMappedText(feedback, "Hub.EntryFailed");
                    GlobalData.Dispatch(new MainMenuFeedbackAction("Loader diagnostic: asset missing"));
                    Assert.That(GlobalData.Get<MainMenuData>().FeedbackKey, Is.Null);
                    Assert.That(feedback.text, Is.EqualTo("Loader diagnostic: asset missing"));
                    GlobalData.Dispatch(new MainMenuFeedbackAction(null));
                    Assert.That(GlobalData.Get<MainMenuData>().Feedback, Is.Null);
                    AssertMappedText(feedback, "Hub.Ready");
                }
            }
            finally
            {
                if (runtime.HasPendingEntry)
                {
                    ExpectEntryFailure(GameSceneId.WallSqueeze);
                    runtime.FailEntry();
                    await UniTask.WaitUntil(IsStableHub).Timeout(TimeSpan.FromSeconds(15));
                }

                await HubLanguageFlow.ApplyAsync(previous, reopen);
            }
        }

        private static void AssertMappedText(TMPro.TMP_Text target, string key, params object[] args)
        {
            Assert.That(target.text, Is.EqualTo(args.Length == 0 ? LanguageService.Get(key) : LanguageService.Format(key, args)));
            var config = GameSettingsService.Config;
            Assert.That(config, Is.Not.Null);
            int fontId = -1;
            foreach (var entry in config.Language.Entries)
            {
                if (entry.Key == key)
                {
                    fontId = entry.FontId;
                    break;
                }
            }

            Assert.That(fontId, Is.GreaterThanOrEqualTo(0), "正式词表必须包含测试文案键");
            foreach (var mapping in config.Fonts.Entries)
            {
                if (mapping.FontId == fontId && mapping.Language == LanguageService.CurrentLanguage)
                {
                    Assert.That(target.font, Is.SameAs(mapping.Font));
                    Assert.That(target.fontSharedMaterial, Is.SameAs(mapping.Material));
                    return;
                }
            }

            Assert.Fail("词条缺少当前语言字体映射：" + key);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HubLanguageRebuildPreservesSelectionAndRecoversAfterReopenFailure()
        {
            yield return PrepareHub();
            yield return VerifyLanguageRebuildAsync().ToCoroutine();
        }

        private static async UniTask VerifyLanguageRebuildAsync()
        {
            var previous = GameSettingsService.Current;
            var oldMenu = UIManager.Instance.Get<MainMenuView>();
            GlobalData.Dispatch(new MainMenuSelectAction(SceneIndex(GameSceneId.WallSqueeze)));
            Func<UniTask<HubSettingsView>> reopen = () => UniTask.FromResult<HubSettingsView>(null);
            try
            {
                var next = previous.Copy();
                next.Language = previous.Language == "en" ? "zh-CN" : "en";
                var result = await HubLanguageFlow.ApplyAsync(next, reopen);
                Assert.That(result.Succeeded, Is.True, result.Message);
                Assert.That(oldMenu.State, Is.EqualTo(ViewState.Destroyed));
                var rebuilt = UIManager.Instance.Get<MainMenuView>();
                Assert.That(rebuilt, Is.Not.SameAs(oldMenu));
                Assert.That(GlobalData.Get<MainMenuData>().SelectedIndex, Is.EqualTo(SceneIndex(GameSceneId.WallSqueeze)));
                Assert.That(Field<TMPro.TextMeshProUGUI>(rebuilt, "TextMeshProUGUI_Title").text,
                    Is.EqualTo(LanguageService.Get("Hub.wall_squeeze.Title")));

                var failedCandidate = next.Copy();
                failedCandidate.Language = previous.Language;
                int calls = 0;
                result = await HubLanguageFlow.ApplyAsync(failedCandidate, () => ++calls == 1
                    ? UniTask.FromException<HubSettingsView>(new InvalidOperationException("Expected reopen failure"))
                    : reopen());
                Assert.That(result.Succeeded, Is.False);
                Assert.That(GameSettingsService.Current.Language, Is.EqualTo(next.Language));
                Assert.That(LanguageService.CurrentLanguage, Is.EqualTo(next.Language));
                Assert.That(UIManager.Instance.Get<MainMenuView>().State, Is.EqualTo(ViewState.Visible));
                Assert.That(HubLanguageFlow.IsChanging, Is.False);
            }
            finally
            {
                await HubLanguageFlow.ApplyAsync(previous, reopen);
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HubLanguageDisplayCancelRestoresPreferencesSelectionAndVisibleHub()
        {
            if (!DisplaySettingsRuntime.SupportsDesktopDisplay)
            {
                Assert.Ignore("显示确认取消回归需要桌面显示设置支持。");
            }
            yield return PrepareHub();
            yield return VerifyLanguageDisplayCancelAsync().ToCoroutine();
        }

        private async UniTask VerifyLanguageDisplayCancelAsync()
        {
            var previous = GameSettingsService.Current;
            bool hadPreference = PlayerPrefs.HasKey(GameSettingsService.PreferenceKey);
            string preference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            int originalSelection = GlobalData.Get<MainMenuData>().SelectedIndex;
            var oldMenu = UIManager.Instance.Get<MainMenuView>();
            Func<UniTask<HubSettingsView>> reopen = () => UniTask.FromResult<HubSettingsView>(null);
            var next = previous.Copy();
            next.Language = previous.Language == "en" ? "zh-CN" : "en";
            next.Graphics.WindowMode = DisplaySettingsRuntime.Capture().WindowMode == (int)FullScreenMode.Windowed
                ? (int)FullScreenMode.FullScreenWindow
                : (int)FullScreenMode.Windowed;
            // 公共音频通知证明候选已实际应用，再由显示确认取消触发回滚。
            next.Audio.Master = AudioSettingsRuntime.GetGain(AudioBus.Music) > 0.5f ? 0.25f : 0.75f;
            next.Audio.Music = 1;
            next.Audio.Muted = false;
            Assert.DoesNotThrow(next.Validate);
            Assert.DoesNotThrow(() => GameSettingsService.Options.ValidateCandidate(next));
            Assert.DoesNotThrow(() => LanguageService.ValidateLanguage(next.Language));
            bool runtimeAppliedAfterClose = false;
            int reopenCount = 0;
            Action observeAudio = () => runtimeAppliedAfterClose |= oldMenu.State == ViewState.Destroyed
                && Mathf.Approximately(AudioSettingsRuntime.GetGain(AudioBus.Music), next.Audio.Master);
            AudioSettingsRuntime.Changed += observeAudio;
            try
            {
                GlobalData.Dispatch(new MainMenuSelectAction(SceneIndex(GameSceneId.WallSqueeze)));
                var result = await HubLanguageFlow.ApplyAsync(next, () =>
                {
                    reopenCount++;
                    return reopen();
                }, _ => UniTask.FromResult(false));
                Assert.That(result.Succeeded, Is.False);
                Assert.That(result.Canceled, Is.True);
                Assert.That(result.Message, Does.Contain("设置已取消"));
                Assert.That(runtimeAppliedAfterClose, Is.True);
                Assert.That(reopenCount, Is.EqualTo(1));
                Assert.That(oldMenu.State, Is.EqualTo(ViewState.Destroyed));
                Assert.That(PlayerPrefs.HasKey(GameSettingsService.PreferenceKey), Is.EqualTo(hadPreference));
                Assert.That(PlayerPrefs.GetString(GameSettingsService.PreferenceKey), Is.EqualTo(preference));
                Assert.That(GameSettingsService.Current.Language, Is.EqualTo(previous.Language));
                Assert.That(LanguageService.CurrentLanguage, Is.EqualTo(previous.Language));
                Assert.That(GameSettingsService.Current.Audio.Master, Is.EqualTo(previous.Audio.Master));
                Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Music), Is.EqualTo(previous.Audio.Muted ? 0 : previous.Audio.Master * previous.Audio.Music).Within(0.0001f));
                Assert.That(GlobalData.Get<MainMenuData>().SelectedIndex, Is.EqualTo(SceneIndex(GameSceneId.WallSqueeze)));
                Assert.That(UIManager.Instance.Get<MainMenuView>().State, Is.EqualTo(ViewState.Visible));
                Assert.That(UIManager.Instance.Get<MainMenuView>(), Is.Not.SameAs(oldMenu));
                Assert.That(HubLanguageFlow.IsChanging, Is.False);
                Assert.That(runtime.LoadCount, Is.Zero);
            }
            finally
            {
                AudioSettingsRuntime.Changed -= observeAudio;
                try
                {
                    await HubLanguageFlow.ApplyAsync(previous, reopen);
                    GlobalData.Dispatch(new MainMenuSelectAction(originalSelection));
                }
                finally
                {
                    RestoreSettingsPreference(hadPreference, preference);
                }
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HubLanguageAndDemoEntryRejectEachOtherWhileTheirTransactionsArePending()
        {
            yield return PrepareHub();
            yield return VerifyLanguageEntryExclusionAsync().ToCoroutine();
        }

        private async UniTask VerifyLanguageEntryExclusionAsync()
        {
            var previous = GameSettingsService.Current;
            bool hadPreference = PlayerPrefs.HasKey(GameSettingsService.PreferenceKey);
            string preference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            int originalSelection = GlobalData.Get<MainMenuData>().SelectedIndex;
            Func<UniTask<HubSettingsView>> reopen = () => UniTask.FromResult<HubSettingsView>(null);
            var gate = new UniTaskCompletionSource<HubSettingsView>();
            UniTask<SettingsApplyResult> languageTask = default;
            UniTask<GameSceneSwitchResult> entryTask = default;
            bool languageStarted = false;
            bool languageAwaited = false;
            bool entryStarted = false;
            bool entryAwaited = false;
            bool reopening = false;
            try
            {
                GlobalData.Dispatch(new MainMenuSelectAction(SceneIndex(GameSceneId.WallSqueeze)));
                var next = previous.Copy();
                next.Language = previous.Language == "en" ? "zh-CN" : "en";
                languageTask = HubLanguageFlow.ApplyAsync(next, () =>
                {
                    reopening = true;
                    return gate.Task;
                }).Preserve();
                languageStarted = true;
                await UniTask.WaitUntil(() => reopening).Timeout(TimeSpan.FromSeconds(15));
                Assert.That(HubLanguageFlow.IsChanging, Is.True);
                string pendingPreference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
                GlobalData.Dispatch(new MainMenuEnterAction(GameSceneId.WallSqueeze));
                Assert.That(runtime.LoadCount, Is.Zero, "语言页面重建等待中必须拒绝新的 Demo 进入命令。");
                Assert.That(GlobalData.Get<MainMenuData>().IsEntering, Is.False);
                var rejected = await HubLanguageFlow.ApplyAsync(previous, reopen);
                Assert.That(rejected.Succeeded, Is.False, "语言事务等待期间不能再启动另一场重建。");
                Assert.That(PlayerPrefs.GetString(GameSettingsService.PreferenceKey), Is.EqualTo(pendingPreference));
                Assert.That(HubLanguageFlow.IsChanging, Is.True);
                gate.TrySetResult(null);
                var languageResult = await languageTask;
                languageAwaited = true;
                Assert.That(languageResult.Succeeded, Is.True, languageResult.Message);
                Assert.That(HubLanguageFlow.IsChanging, Is.False);

                // 直接持有导航任务，清理也会 await；不留下 MainMenu 的 fire-and-forget 请求。
                entryTask = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.WallSqueeze).Preserve();
                entryStarted = true;
                await UniTask.WaitUntil(() => runtime.HasPendingEntry).Timeout(TimeSpan.FromSeconds(15));
                var loading = UIManager.Instance.Get<CommonLoadingView>();
                Assert.That(loading?.State, Is.EqualTo(ViewState.Visible));
                Assert.That(GameSceneNavigator.Instance.IsTransitioning, Is.True);
                string enteringPreference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
                rejected = await HubLanguageFlow.ApplyAsync(previous, reopen);
                Assert.That(rejected.Succeeded, Is.False, "待进入 Demo 期间不允许关闭 Loading 或切换语言。");
                Assert.That(GameSettingsService.Current.Language, Is.EqualTo(next.Language));
                Assert.That(LanguageService.CurrentLanguage, Is.EqualTo(next.Language));
                Assert.That(PlayerPrefs.GetString(GameSettingsService.PreferenceKey), Is.EqualTo(enteringPreference));
                Assert.That(UIManager.Instance.Get<CommonLoadingView>(), Is.SameAs(loading));
                Assert.That(loading.State, Is.EqualTo(ViewState.Visible));
                Assert.That(HubLanguageFlow.IsChanging, Is.False);
                GlobalData.Dispatch(new MainMenuEnterAction(GameSceneId.WallSqueeze));
                Assert.That(runtime.LoadCount, Is.EqualTo(1), "导航等待期间不能再启动第二次加载。");
                runtime.FailEntry();
                var entryResult = await entryTask;
                entryAwaited = true;
                Assert.That(entryResult.Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
                await UniTask.WaitUntil(IsStableHub).Timeout(TimeSpan.FromSeconds(15));
                Assert.That(GlobalData.Get<MainMenuData>().SelectedIndex, Is.EqualTo(SceneIndex(GameSceneId.WallSqueeze)));
            }
            finally
            {
                gate.TrySetResult(null);
                try
                {
                    try
                    {
                        if (languageStarted && !languageAwaited)
                        {
                            await languageTask;
                        }
                    }
                    finally
                    {
                        if (entryStarted && !entryAwaited)
                        {
                            if (runtime.HasPendingEntry)
                            {
                                runtime.FailEntry();
                            }
                            await entryTask;
                        }
                    }
                }
                finally
                {
                    try
                    {
                        await HubLanguageFlow.ApplyAsync(previous, reopen);
                        GlobalData.Dispatch(new MainMenuSelectAction(originalSelection));
                    }
                    finally
                    {
                        RestoreSettingsPreference(hadPreference, preference);
                    }
                }
            }
        }

        private static void RestoreSettingsPreference(bool existed, string value)
        {
            if (existed)
            {
                PlayerPrefs.SetString(GameSettingsService.PreferenceKey, value);
            }
            else
            {
                PlayerPrefs.DeleteKey(GameSettingsService.PreferenceKey);
            }
            PlayerPrefs.Save();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FailedEntryRestoresNewMenuAndAllowsRetry()
        {
            yield return PrepareHub();
            var firstMenu = UIManager.Instance.Get<MainMenuView>();
            // 直接使用保存的绑定，避免把 Prefab 的层级路径变成测试契约。
            yield return RevealCard(firstMenu, GameSceneId.WallSqueeze);
            var button = CardForScene(firstMenu, GameSceneId.WallSqueeze);
            yield return null; yield return null;
            Vector2 position = ButtonPosition(button);
            yield return Click(position);
            Assert.That(runtime.LoadCount, Is.Zero, "点击卡片只切换预览，不能误进入场景。");
            position = ButtonPosition(Field<Button>(firstMenu, "Button_Start"));
            yield return Click(position);
            yield return Wait(() => runtime.LoadCount == 1 && firstMenu.State == ViewState.Destroyed, "Loading 替换旧 Hub", 15);
            yield return Click(position);
            Assert.That(runtime.LoadCount, Is.EqualTo(1), "加载期间重复点击不能再发起进入请求。");

            LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenu\] 无法进入 WallSqueeze：测试场景加载失败"));
            runtime.FailEntry();
            yield return Wait(IsStableHub, "加载失败恢复 Hub", 15);
            var restoredMenu = UIManager.Instance.Get<MainMenuView>();
            Assert.That(restoredMenu, Is.Not.SameAs(firstMenu));
            var feedback = Field<TMPro.TextMeshProUGUI>(restoredMenu, "TextMeshProUGUI_Status");
            Assert.That(feedback.text, Is.EqualTo(LanguageService.Get("Hub.EntryFailed")));
            var feedbackSize = feedback.GetPreferredValues();
            Assert.That(feedbackSize.x, Is.LessThanOrEqualTo(feedback.rectTransform.rect.width), "失败提示应在已有控件内完整显示。");
            Assert.That(feedbackSize.y, Is.LessThanOrEqualTo(feedback.rectTransform.rect.height), "失败提示不能溢出遮住入口。");
            foreach (var card in VisibleCards(restoredMenu))
                Assert.That(card.Button.interactable, Is.True, "恢复后所有卡片可浏览；未开放限制由开始按钮承担。");

            // 新实例需要经过帧末 Canvas 注册/布局；随后仍验证真实射线并通过 InputSystem 点击。
            yield return null; yield return null;
            Assert.That(Field<TMPro.TextMeshProUGUI>(restoredMenu, "TextMeshProUGUI_Title").text, Is.EqualTo(LanguageService.Get("Hub.wall_squeeze.Title")), "失败恢复应保留待重试玩法。");
            yield return Click(ButtonPosition(Field<Button>(restoredMenu, "Button_Start")));
            yield return Wait(() => runtime.LoadCount == 2, "新 Hub 可以实际点击重试", 15);
            LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenu\] 无法进入 WallSqueeze：测试场景加载失败"));
            runtime.FailEntry();
            yield return Wait(IsStableHub, "重试失败也能恢复", 15);
            yield return null; yield return null;
            Assert.That(CardForScene(UIManager.Instance.Get<MainMenuView>(), GameSceneId.WallSqueeze).interactable, Is.True);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator KeyboardAndGamepadNavigateEnterAndRecoverWithoutRepeatingHeldSubmit()
        {
            yield return PrepareHub();
            var firstMenu = UIManager.Instance.Get<MainMenuView>();
            yield return WaitForInitialSelection(firstMenu);
            var firstSelection = EventSystem.current.currentSelectedGameObject;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.EqualTo(firstSelection), "方向键应移动真实焦点。");
            yield return Wait(() => SelectedCardIsVisible(), "键盘焦点滚入视口", 5);
            yield return MoveToOpenCard(firstMenu, false);
            var keyboardTarget = SelectedScene(firstMenu);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            yield return Wait(() => runtime.LoadCount == 1 && firstMenu.State == ViewState.Destroyed, "Enter 进入当前选中 Demo", 15);
            AssertRequestedScene(keyboardTarget);
            ExpectEntryFailure(keyboardTarget);
            runtime.FailEntry();
            yield return Wait(IsStableHub, "键盘进入失败回滚", 15);

            var restoredMenu = UIManager.Instance.Get<MainMenuView>();
            yield return WaitForInitialSelection(restoredMenu);
            firstSelection = EventSystem.current.currentSelectedGameObject;
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            var backgroundPosition = new Vector2(Screen.width * .05f, Screen.height * .05f);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = backgroundPosition, phase = UnityEngine.InputSystem.TouchPhase.Began }); yield return null;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = backgroundPosition, phase = UnityEngine.InputSystem.TouchPhase.Ended }); yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null, "触屏点击空白应清除选中项，为切回手柄提供真实前置状态。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Not.EqualTo(firstSelection), "方向键应移动真实手柄焦点。");
            yield return Wait(() => SelectedCardIsVisible(), "手柄焦点滚入视口", 5);
            yield return MoveToOpenCard(restoredMenu, true);
            var gamepadTarget = SelectedScene(restoredMenu);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            yield return Wait(() => runtime.LoadCount == 2 && restoredMenu.State == ViewState.Destroyed, "A 进入当前选中 Demo", 15);
            AssertRequestedScene(gamepadTarget);
            ExpectEntryFailure(gamepadTarget);
            runtime.FailEntry();
            yield return Wait(IsStableHub, "按住 A 时恢复新 Hub", 15);
            yield return null; yield return null;
            Assert.That(EventSystem.current.sendNavigationEvents, Is.False, "恢复期间 A 未松开不能重新提交。");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
            Assert.That(runtime.LoadCount, Is.EqualTo(2));

            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            var latestMenu = UIManager.Instance.Get<MainMenuView>();
            yield return WaitForInitialSelection(latestMenu);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            yield return Wait(() => runtime.LoadCount == 3, "松开 A 后可明确再次进入", 15);
            AssertRequestedScene(gamepadTarget);
            ExpectEntryFailure(gamepadTarget);
            runtime.FailEntry();
            yield return Wait(IsStableHub, "再次进入结束后收口", 15);
            yield return WaitForInitialSelection(UIManager.Instance.Get<MainMenuView>());
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator GamepadScrollsRecycledCardsAndEntersCurrentIdentity()
        {
            yield return PrepareHub();
            var menu = UIManager.Instance.Get<MainMenuView>();
            var entries = Field<List<MainMenuDemoEntry>>(menu, "entries");
            var originals = entries.ToArray();
            var list = Field<LoopScrollView>(menu, "LoopScrollView_DemoList");
            // 放大真实集合以跨越虚拟化边界；不以物理 Cell 数量或固定布局作断言。
            entries.Clear();
            for (int i = 0; i < 40; i++)
            {
                var source = originals[i % originals.Length];
                entries.Add(new MainMenuDemoEntry(source.Key + ":" + i, source.Title + " " + i, source.Description, source.PreviewAddress, source.SceneId));
            }
            list.SetTotalCount(entries, getItemKey: item => ((MainMenuDemoEntry)item).Key);
            try
            {
                yield return WaitForInitialSelection(menu);
                for (int attempt = 0; attempt < 20 && list.GetVisibleCell(0) != null; attempt++)
                {
                    InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadRight)); yield return null;
                    InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                    yield return Wait(() => EventSystem.current.sendNavigationEvents && SelectedCardIsVisible(), "手柄滚动并重新建立有效焦点", 5);
                }
                Assert.That(list.GetVisibleCell(0), Is.Null, "操作应越过首行回收边界，而不是只在初始卡片之间导航。");
                // 未开放卡片现在也允许浏览；进入断言选择下一个已开放项目。
                while (!entries[EventSystem.current.currentSelectedGameObject.GetComponentInParent<LoopCell>().Context.Index].SceneId.HasValue)
                {
                    InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadRight)); yield return null;
                    InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
                }
                var selected = EventSystem.current.currentSelectedGameObject.GetComponent<LoopScrollMenuButton>();
                var selectedCell = selected.GetComponentInParent<LoopCell>();
                Assert.That(selectedCell.Context.IsCurrent, Is.True);
                Assert.That(list.ItemViews().TryGetItemView(selectedCell, out var item), Is.True);
                Assert.That(Field<TMPro.TextMeshProUGUI>(item, "TextMeshProUGUI_Title").text,
                    Is.EqualTo(entries[selectedCell.Context.Index].Title), "回收后显示必须对应当前业务数据。");
                var target = entries[selectedCell.Context.Index].SceneId.Value;
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                yield return Wait(() => runtime.LoadCount == 1 && menu.State == ViewState.Destroyed, "回收后的卡片实际进入", 15);
                AssertRequestedScene(target);
                ExpectEntryFailure(target);
                runtime.FailEntry();
                yield return Wait(IsStableHub, "回收后进入失败回滚", 15);
            }
            finally
            {
                entries.Clear(); entries.AddRange(originals);
                var currentMenu = UIManager.Instance.Get<MainMenuView>();
                if (currentMenu != null && currentMenu.State != ViewState.Destroyed)
                {
                    Field<LoopScrollView>(currentMenu, "LoopScrollView_DemoList").SetTotalCount(entries,
                        getItemKey: item => ((MainMenuDemoEntry)item).Key);
                    GlobalData.Dispatch(new MainMenuSelectAction(0));
                }
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SelectionSharesArtworkAndUnopenedDemoCannotEnter()
        {
            yield return PrepareHub();
            var menu = UIManager.Instance.Get<MainMenuView>();
            yield return RevealCard(menu, GameSceneId.WallSqueeze);
            var button = CardForScene(menu, GameSceneId.WallSqueeze);
            yield return Click(ButtonPosition(button));
            Assert.That(runtime.LoadCount, Is.Zero);
            Assert.That(Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_Title").text, Is.EqualTo(LanguageService.Get("Hub.wall_squeeze.Title")));
            var selectedIndex = Field<List<MainMenuDemoEntry>>(menu, "entries").FindIndex(entry => entry.SceneId == GameSceneId.WallSqueeze);
            var hero = Field<UIImageLoader>(menu, "UIImageLoader_Hero");
            var list = Field<LoopScrollView>(menu, "LoopScrollView_DemoList");
            yield return Wait(() => hero.TargetImage.sprite != null &&
                hero.TargetImage.sprite == list.GetVisibleCell(selectedIndex)?.GetComponentInChildren<UIImageLoader>().TargetImage.sprite,
                "大预览与卡片复用同一个主体 Sprite", 10);
            var preview = list.GetVisibleCell(selectedIndex).GetComponentInChildren<UIImageLoader>();
            Assert.That(hero.TargetImage.preserveAspect && preview.TargetImage.preserveAspect, Is.True);

            var entries = Field<List<MainMenuDemoEntry>>(menu, "entries");
            int unopened = entries.FindIndex(entry => !entry.SceneId.HasValue);
            list.ScrollToCell(unopened, ScrollAlignment.End);
            yield return null; yield return null;
            var unavailable = list.GetVisibleCell(unopened).GetComponentInChildren<LoopScrollMenuButton>();
            yield return Click(ButtonPosition(unavailable));
            Assert.That(Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_Title").text, Is.EqualTo("UI 交互展台"));
            Assert.That(Field<Button>(menu, "Button_Start").interactable, Is.False);
            Assert.That(Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_Label").text, Is.EqualTo("未开放"));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(runtime.LoadCount, Is.Zero, "未开放项目允许浏览，但提交不能进入。");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TouchSelectsPreviewBeforeExplicitStart()
        {
            yield return PrepareHub();
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            InputDeviceState.Notify(touchscreen);
            yield return null; yield return null;
            var menu = UIManager.Instance.Get<MainMenuView>();
            yield return RevealCard(menu, GameSceneId.WallSqueeze);
            yield return Tap(ButtonPosition(CardForScene(menu, GameSceneId.WallSqueeze)));
            Assert.That(runtime.LoadCount, Is.Zero, "触控选择卡片不能直接进入场景。");
            Assert.That(Field<TMPro.TextMeshProUGUI>(menu, "TextMeshProUGUI_Title").text, Is.EqualTo(LanguageService.Get("Hub.wall_squeeze.Title")));
            yield return Tap(ButtonPosition(Field<Button>(menu, "Button_Start")));
            yield return Wait(() => runtime.LoadCount == 1, "触控开始按钮进入当前预览", 15);
            AssertRequestedScene(GameSceneId.WallSqueeze);
            ExpectEntryFailure(GameSceneId.WallSqueeze);
            runtime.FailEntry();
            yield return Wait(IsStableHub, "触控进入失败收口", 15);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SettingsEntryOpensNormalPopupAndClosesWithoutFloatingEntry()
        {
            yield return PrepareHub();
            var menu = UIManager.Instance.Get<MainMenuView>();
            Assert.That(UIManager.Instance.Get<HubSettingsView>(), Is.Null, "启动不创建常驻画面设置。");
            int count = UIManager.Instance.StackCount;
            var mode = Core.Runtime.Rendering.Streamline.StreamlineRuntime.RequestedMode;
            for (int close = 0; close < 3; close++)
            {
                var entry = Field<Button>(menu, "Button_Settings");
                Assert.That(ButtonPosition(entry).x, Is.LessThan(Screen.width * .33f), "画面设置归入左侧操作区。");
                Assert.That(ButtonPosition(entry).y, Is.LessThan(Screen.height * .65f), "不在右上角悬浮。");
                yield return Click(ButtonPosition(entry));
                yield return Wait(() => UIManager.Instance.Get<HubSettingsView>()?.State == ViewState.Visible, "按需打开Pop", 5);
                var settings = UIManager.Instance.Get<HubSettingsView>();
                Assert.That(settings.Level, Is.EqualTo(UILayer.Pop));
                Assert.That(settings.ViewMode, Is.EqualTo(UIViewMode.Modal));
                Assert.That(settings.Mask, Is.EqualTo(MaskType.CloseRaycast));
                Assert.That(settings.transform.Find("OpenButton"), Is.Null, "Prefab不再保存悬浮入口。");
                Assert.That(UIManager.Instance.StackCount, Is.EqualTo(count + 1));
                yield return Wait(() => EventSystem.current.sendNavigationEvents, "弹窗导航松键", 3);
                if (close == 0)
                {
                    System.IO.Directory.CreateDirectory("Library/GraphicsSettings");
                    ScreenCapture.CaptureScreenshot("Library/GraphicsSettings/Popup.png"); yield return null; yield return null;
                    yield return Click(ButtonPosition(settings.gameObject.GetComponentsInChildren<Button>(true).Single(button => button.name == "Close")));
                }
                else if (close == 1)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null; yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                }
                else yield return Click(new Vector2(Screen.width * .05f, Screen.height * .05f));
                yield return Wait(() => UIManager.Instance.Get<HubSettingsView>() == null, "关闭销毁普通弹窗", 5);
                Assert.That(settings.State, Is.EqualTo(ViewState.Destroyed));
                Assert.That(UIManager.Instance.StackCount, Is.EqualTo(count));
                Assert.That(Core.Runtime.Rendering.Streamline.StreamlineRuntime.RequestedMode, Is.EqualTo(mode), "关闭弹窗保留画质偏好。");
                yield return Wait(() => EventSystem.current.currentSelectedGameObject == entry.gameObject, "恢复原设置入口焦点", 3);
            }
            ScreenCapture.CaptureScreenshot("Library/GraphicsSettings/Hall.png"); yield return null; yield return null;
            yield return UIManager.Instance.CloseAsync<MainMenuView>(animated: false).ToCoroutine();
            Assert.That(UIManager.Instance.Get<HubSettingsView>(), Is.Null, "离开大厅不出现全局悬浮按钮。");
            yield return UIManager.Instance.OpenAsync<MainMenuView>(new UIOpenOptions(false)).ToCoroutine();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HubSettingsIndependentPagesKeepCandidatesLocalAndApplyOnlyOnce()
        {
            yield return PrepareHub();
            yield return VerifySettingsPagesAsync().ToCoroutine();
        }

        private async UniTask VerifySettingsPagesAsync()
        {
            var previous = GameSettingsService.Current;
            bool hadPreference = PlayerPrefs.HasKey(GameSettingsService.PreferenceKey);
            string preference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            int commits = 0;
            Action<GameSettingsSnapshot> observe = _ => commits++;
            GameSettingsService.Changed += observe;
            try
            {
                var menu = UIManager.Instance.Get<MainMenuView>();
                var entry = Field<Button>(menu, "Button_Settings");
                await Click(ButtonPosition(entry));
                await Wait(() => UIManager.Instance.Get<HubSettingsView>()?.State == ViewState.Visible, "打开正式设置宿主", 10);
                var settings = UIManager.Instance.Get<HubSettingsView>();
                var screen = settings;
                await Wait(() => Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView is GraphicsSettingsPanelView
                    && Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView.State == ViewState.Visible, "初选画质页懒加载", 10);
                var pages = Field<List<View>>(screen, "views").ToArray();
                foreach (var page in pages.Where(page => page is not GraphicsSettingsPanelView))
                {
                    Assert.That(page.State, Is.EqualTo(ViewState.Created), "主设置打开时不预加载其它独立 View。");
                    Assert.That(page.IsLoaded, Is.False);
                    Assert.That(page.gameObject, Is.Null);
                }
                var expected = new Dictionary<SettingsCategory, Type>
                {
                    { SettingsCategory.Graphics, typeof(GraphicsSettingsPanelView) },
                    { SettingsCategory.Audio, typeof(AudioSettingsPanelView) },
                    { SettingsCategory.Input, typeof(InputSettingsPanelView) },
                    { SettingsCategory.Language, typeof(LanguageSettingsPanelView) },
                    { SettingsCategory.Accessibility, typeof(AccessibilitySettingsPanelView) }
                };
                foreach (var item in expected)
                {
                    await SelectSettingsPageAsync(screen, item.Key);
                    var selected = Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView;
                    Assert.That(selected, Is.InstanceOf(item.Value), "分类必须显示独立 View，而不是本地对象显隐替代页生命周期。");
                    foreach (var page in pages)
                    {
                        if (ReferenceEquals(page, selected))
                        {
                            Assert.That(page.State, Is.EqualTo(ViewState.Visible));
                        }
                        else if (page.IsLoaded)
                        {
                            Assert.That(page.State, Is.EqualTo(ViewState.LoadedHidden));
                            Assert.That(page.gameObject, Is.Not.SameAs(selected.gameObject));
                            Assert.That(page.gameObject.activeSelf, Is.False);
                        }
                        else
                        {
                            Assert.That(page.State, Is.EqualTo(ViewState.Created), "未选择分页保持未加载。");
                            Assert.That(page.gameObject, Is.Null);
                        }
                    }
                }

                await SelectSettingsPageAsync(screen, SettingsCategory.Accessibility);
                var accessibility = (SettingsPanelView)Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView;
                System.IO.Directory.CreateDirectory("Library/GraphicsSettings");
                ScreenCapture.CaptureScreenshot("Library/GraphicsSettings/Accessibility.png");
                await UniTask.Yield();
                var shake = (Button)accessibility.Rows.Single(row => row.Key == "CameraShake").Control;
                int shakeClicks = 0;
                shake.onClick.AddListener(() => shakeClicks++);
                await Click(ButtonPosition(shake));
                Assert.That(shakeClicks, Is.EqualTo(1), "开关应收到一次真实指针点击。");
                Assert.That(shake.GetComponent<UIBtnSwitch>().IsOn, Is.EqualTo(!previous.Accessibility.CameraShake), "真实点击必须更新基础开关状态。");
                Assert.That(screen.Capture().Accessibility.CameraShake, Is.EqualTo(!previous.Accessibility.CameraShake));
                await SelectSettingsPageAsync(screen, SettingsCategory.Audio);
                float edited = await EditMasterVolumeAsync(screen, previous.Audio.Master);
                Assert.That(screen.Capture().Audio.Master, Is.EqualTo(edited).Within(0.0001f));
                Assert.That(GameSettingsService.Current.Audio.Master, Is.EqualTo(previous.Audio.Master));
                Assert.That(GameSettingsService.Current.Accessibility.CameraShake, Is.EqualTo(previous.Accessibility.CameraShake));
                Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Music), Is.EqualTo(previous.Audio.Muted ? 0 : previous.Audio.Master * previous.Audio.Music).Within(0.0001f));
                await Click(ButtonPosition(Field<Button>(screen, "Button_Reset")));
                Assert.That(JsonUtility.ToJson(screen.Capture().Audio), Is.EqualTo(JsonUtility.ToJson(GameSettingsService.GetDefaults().Audio)));
                Assert.That(screen.Capture().Accessibility.CameraShake, Is.EqualTo(!previous.Accessibility.CameraShake), "恢复本类默认不能清掉其它分页候选。");
                Assert.That(GameSettingsService.Current.Audio.Master, Is.EqualTo(previous.Audio.Master), "恢复默认仍只修改候选。");
                await Click(ButtonPosition(Field<Button>(screen, "Button_Cancel")));
                await Wait(() => settings.State == ViewState.Destroyed, "取消设置并等待子页回收", 10);
                Assert.That(pages.All(page => page.State == ViewState.Destroyed), Is.True, "父宿主关闭返回前必须完成所有子 View 销毁。");
                Assert.That(GameSettingsService.Current.Accessibility.CameraShake, Is.EqualTo(previous.Accessibility.CameraShake));
                Assert.That(PlayerPrefs.HasKey(GameSettingsService.PreferenceKey), Is.EqualTo(hadPreference));
                Assert.That(PlayerPrefs.GetString(GameSettingsService.PreferenceKey), Is.EqualTo(preference));
                Assert.That(commits, Is.Zero);

                await Click(ButtonPosition(entry));
                await Wait(() => UIManager.Instance.Get<HubSettingsView>()?.State == ViewState.Visible, "重新打开独立候选", 10);
                settings = UIManager.Instance.Get<HubSettingsView>();
                screen = settings;
                pages = Field<List<View>>(screen, "views").ToArray();
                await SelectSettingsPageAsync(screen, SettingsCategory.Audio);
                edited = await EditMasterVolumeAsync(screen, previous.Audio.Master);
                int version = Field<int>(GlobalData.Get<SettingsData>(), "Version");
                var apply = Field<Button>(screen, "Button_Apply");
                var position = ButtonPosition(apply);
                await Click(position);
                await Click(position);
                await Wait(() => !SettingsFlow.IsSubmitting && !apply.interactable, "完成唯一设置提交", 15);
                Assert.That(Field<int>(GlobalData.Get<SettingsData>(), "Version"), Is.EqualTo(version + 1), "重复点击不能再次启动提交。");
                Assert.That(commits, Is.EqualTo(1));
                Assert.That(GameSettingsService.Current.Audio.Master, Is.EqualTo(edited).Within(0.0001f));
                var saved = LocalDataManager.LoadData<GameSettingsSnapshot>(GameSettingsService.PreferenceKey);
                Assert.That(saved, Is.Not.Null);
                Assert.That(saved.Audio.Master, Is.EqualTo(edited).Within(0.0001f), "通过正式页面完成本机保存。");
                AssertMappedText(Field<TMPro.TMP_Text>(screen, "TextMeshProUGUI_Feedback"), "Settings.Applied");
                await Click(ButtonPosition(Field<Button>(screen, "Button_Close")));
                await Wait(() => settings.State == ViewState.Destroyed, "应用后关闭宿主", 10);
                Assert.That(pages.All(page => page.State == ViewState.Destroyed), Is.True);
                Assert.That(GameSettingsService.Current.Audio.Master, Is.EqualTo(edited).Within(0.0001f));
            }
            finally
            {
                GameSettingsService.Changed -= observe;
                try
                {
                    await UIManager.Instance.CloseAsync<HubSettingsView>(animated: false);
                    await UIManager.Instance.CloseAsync<DisplayConfirmView>(animated: false);
                    await Wait(() => !SettingsFlow.IsSubmitting, "收口设置事务", 15);
                    var restored = await GameSettingsService.ApplyAsync(previous, _ => UniTask.FromResult(true));
                    Assert.That(restored.Succeeded, Is.True, restored.Message);
                }
                finally
                {
                    RestoreSettingsPreference(hadPreference, preference);
                }
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator InputSettingsPageOnlyEditsCommonParametersAndPreservesSavedBindings()
        {
            yield return PrepareHub();
            yield return VerifyCommonInputSettingsAsync().ToCoroutine();
        }

        private async UniTask VerifyCommonInputSettingsAsync()
        {
            var previous = GameSettingsService.Current;
            bool hadPreference = PlayerPrefs.HasKey(GameSettingsService.PreferenceKey);
            string preference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            var source = ScriptableObject.CreateInstance<InputActionAsset>();
            source.AddActionMap("Gameplay").AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            InputActionSession running = null;
            try
            {
                using var rebind = new InputRebindSession(source);
                rebind.SetBinding("Gameplay/Jump", 0, "<Keyboard>/f12");
                var committed = await GameSettingsService.ApplyBindingOverridesAsync(source, rebind.ExportOverrides());
                Assert.That(committed.Succeeded, Is.True, committed.Message);
                string savedOverrides = GameSettingsService.Current.BindingOverrides[rebind.SourceId];
                running = new InputActionSession(source);
                var menu = UIManager.Instance.Get<MainMenuView>();
                var entry = Field<Button>(menu, "Button_Settings");
                // 提交完成续体先于大厅的下一轮 LastPostLateUpdate；等待入口退出提交期间的禁用态。
                await Wait(() => !SettingsFlow.IsSubmitting && entry.isActiveAndEnabled && entry.IsInteractable(), "提交后恢复设置入口", 5);
                await Click(ButtonPosition(entry));
                await Wait(() => UIManager.Instance.Get<HubSettingsView>()?.State == ViewState.Visible, "打开正式设置宿主", 10);
                var screen = UIManager.Instance.Get<HubSettingsView>();
                await Wait(() => Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView is GraphicsSettingsPanelView
                    && Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView.State == ViewState.Visible,
                    "初选画质页懒加载", 10);
                await SelectSettingsPageAsync(screen, SettingsCategory.Input);
                var page = (InputSettingsPanelView)Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView;
                Assert.That(page.Rows.Select(row => row.Key), Is.EquivalentTo(new[]
                {
                    "GamepadStyle", "MouseLookMultiplier", "TouchLookMultiplier", "GamepadLookMultiplier",
                    "GamepadLookDegreesPerSecond", "GamepadDeadzone", "GamepadMaximum", "RumbleStrength",
                    "GamepadInvertY", "RumbleEnabled"
                }), "公共输入页只保留通用参数，不集中引用或编辑Demo动作。");
                var styleRow = page.Rows.Single(row => row.Key == "GamepadStyle");
                int selected = (int)GameSettingsService.CurrentGamepadStyle == 1 ? 1 : 0;
                var appliedStyle = GameSettingsService.CurrentGamepadStyle;
                await Click(ButtonPosition((Button)styleRow.Control));
                var choices = Field<UITab>(styleRow, "options");
                await Wait(() => choices.Items.Count == 3, "图标选项显示", 5);
                await Click(ButtonPosition(choices.Items[selected].GetComponent<Button>()));
                Assert.That(screen.Capture().Input.GamepadStyle, Is.EqualTo((GamepadStyle)(selected + 1)));
                Assert.That(GameSettingsService.CurrentGamepadStyle, Is.EqualTo(appliedStyle), "编辑图标仍只修改页面候选。");
                await Click(ButtonPosition(Field<Button>(screen, "Button_Reset")));
                Assert.That(JsonUtility.ToJson(screen.Capture().Input), Is.EqualTo(JsonUtility.ToJson(GameSettingsService.GetDefaults().Input)));
                Assert.That(screen.Capture().BindingOverrides[rebind.SourceId], Is.EqualTo(savedOverrides), "恢复公共输入参数不清除宿主改键。");
                int version = Field<int>(GlobalData.Get<SettingsData>(), "Version");
                await Click(ButtonPosition(Field<Button>(screen, "Button_Apply")));
                await Wait(() => !SettingsFlow.IsSubmitting && Field<int>(GlobalData.Get<SettingsData>(), "Version") == version + 1,
                    "应用公共输入参数", 15);
                Assert.That(GameSettingsService.Current.BindingOverrides[rebind.SourceId], Is.EqualTo(savedOverrides));
                Assert.That(LocalDataManager.LoadData<GameSettingsSnapshot>(GameSettingsService.PreferenceKey).BindingOverrides[rebind.SourceId], Is.EqualTo(savedOverrides));
                Assert.That(running.Asset.FindAction("Gameplay/Jump", true).bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f12"));
            }
            finally
            {
                try
                {
                    await UIManager.Instance.CloseAsync<HubSettingsView>(animated: false);
                    await Wait(() => !SettingsFlow.IsSubmitting, "收口输入参数事务", 15);
                    running?.Dispose();
                    var restored = await GameSettingsService.ApplyAsync(previous, _ => UniTask.FromResult(true));
                    Assert.That(restored.Succeeded, Is.True, restored.Message);
                }
                finally
                {
                    running?.Dispose();
                    UnityEngine.Object.Destroy(source);
                    RestoreSettingsPreference(hadPreference, preference);
                }
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator HubSettingsLanguagePageRebuildsRealViewsFontsAndMenuFocus()
        {
            yield return PrepareHub();
            yield return VerifySettingsLanguagePageAsync().ToCoroutine();
        }

        private async UniTask VerifySettingsLanguagePageAsync()
        {
            var previous = GameSettingsService.Current;
            bool hadPreference = PlayerPrefs.HasKey(GameSettingsService.PreferenceKey);
            string preference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            int originalSelection = GlobalData.Get<MainMenuData>().SelectedIndex;
            Func<UniTask<HubSettingsView>> reopen = () => UniTask.FromResult<HubSettingsView>(null);
            try
            {
                var oldMenu = UIManager.Instance.Get<MainMenuView>();
                await RevealCard(oldMenu, GameSceneId.WallSqueeze);
                await Click(ButtonPosition(CardForScene(oldMenu, GameSceneId.WallSqueeze)));
                Assert.That(GlobalData.Get<MainMenuData>().SelectedIndex, Is.EqualTo(SceneIndex(GameSceneId.WallSqueeze)));
                await Click(ButtonPosition(Field<Button>(oldMenu, "Button_Settings")));
                await Wait(() => UIManager.Instance.Get<HubSettingsView>()?.State == ViewState.Visible, "打开真实语言分页宿主", 10);
                foreach (string language in new[] { previous.Language == "en" ? "zh-CN" : "en", previous.Language })
                {
                    var settings = UIManager.Instance.Get<HubSettingsView>();
                    var screen = settings;
                    var pages = Field<List<View>>(screen, "views").ToArray();
                    await SelectSettingsPageAsync(screen, SettingsCategory.Language);
                    var page = (LanguageSettingsPanelView)Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView;
                    var choices = Field<UITab>(page, "UITab_Language_choices");
                    int nextIndex = LanguageService.Languages.Select((item, index) => (item, index)).Single(value => value.item.Id == language).index;
                    string liveLanguage = GameSettingsService.Current.Language;
                    await Click(ButtonPosition(choices.Items[nextIndex].GetComponent<Button>()));
                    Assert.That(screen.Capture().Language, Is.EqualTo(language));
                    Assert.That(GameSettingsService.Current.Language, Is.EqualTo(liveLanguage));
                    Assert.That(LanguageService.CurrentLanguage, Is.EqualTo(liveLanguage));
                    await Click(ButtonPosition(Field<Button>(screen, "Button_Apply")));
                    await Wait(() => !HubLanguageFlow.IsChanging && UIManager.Instance.Get<HubSettingsView>()?.State == ViewState.Visible
                        && UIManager.Instance.Get<HubSettingsView>() != settings, "语言应用并重建真实设置页", 15);
                    Assert.That(settings.State, Is.EqualTo(ViewState.Destroyed));
                    Assert.That(pages.All(item => item.State == ViewState.Destroyed), Is.True, "语言重建不得遗留旧语言子 View。");
                    Assert.That(oldMenu.State, Is.EqualTo(ViewState.Destroyed));
                    Assert.That(GameSettingsService.Current.Language, Is.EqualTo(language));
                    Assert.That(LanguageService.CurrentLanguage, Is.EqualTo(language));
                    Assert.That(GlobalData.Get<MainMenuData>().SelectedIndex, Is.EqualTo(SceneIndex(GameSceneId.WallSqueeze)));
                    oldMenu = UIManager.Instance.Get<MainMenuView>();
                    var rebuilt = UIManager.Instance.Get<HubSettingsView>();
                    screen = rebuilt;
                    Assert.That(screen.Category, Is.EqualTo(SettingsCategory.Language));
                    AssertMappedText(Field<TMPro.TMP_Text>(screen, "TextMeshProUGUI_Title"), "Settings.Title");
                    AssertMappedText(Field<TMPro.TMP_Text>(screen, "TextMeshProUGUI_Category_title"), "Settings.Language");
                    AssertMappedText(Field<TMPro.TMP_Text>(screen, "TextMeshProUGUI_Help"), "Settings.LanguageHelp");
                    AssertMappedText(Field<TMPro.TMP_Text>(screen, "TextMeshProUGUI_Label1"), "Common.Apply");
                    AssertMappedText(Field<TMPro.TMP_Text>(screen, "TextMeshProUGUI_Feedback"), "Settings.Applied");
                    page = (LanguageSettingsPanelView)Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView;
                    await Wait(() => page?.State == ViewState.Visible, "重建后的语言分页完成异步加载", 10);
                    choices = Field<UITab>(page, "UITab_Language_choices");
                    var cnFont = GameSettingsService.Config.Fonts.Entries.Single(item => item.FontId == 0 && item.Language == "zh-CN");
                    foreach (var choice in choices.Items)
                    {
                        var text = choice.GetComponentInChildren<TMPro.TMP_Text>(true);
                        Assert.That(text.font, Is.SameAs(cnFont.Font), "本语言名称混排复用含中文的公共制作字体。");
                        Assert.That(text.fontSharedMaterial, Is.SameAs(cnFont.Material));
                    }
                    InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
                    await UniTask.Yield();
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    await Wait(() => EventSystem.current.currentSelectedGameObject != null
                        && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(rebuilt.transform), "重建后恢复新设置页菜单焦点", 5);
                    Assert.That(EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>().IsInteractable(), Is.True);
                }
                var current = UIManager.Instance.Get<HubSettingsView>();
                var currentScreen = current;
                await Click(ButtonPosition(Field<Button>(currentScreen, "Button_Cancel")));
                await Wait(() => current.State == ViewState.Destroyed, "关闭还原语言后的设置页", 10);
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.LeftShoulder));
                await UniTask.Yield();
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                await Wait(() => EventSystem.current.currentSelectedGameObject != null
                    && EventSystem.current.currentSelectedGameObject.transform.IsChildOf(oldMenu.transform), "返回新 Hub 的有效菜单焦点", 5);
                Assert.That(GameSettingsService.Current.Language, Is.EqualTo(previous.Language));
                Assert.That(GlobalData.Get<MainMenuData>().SelectedIndex, Is.EqualTo(SceneIndex(GameSceneId.WallSqueeze)));
            }
            finally
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                try
                {
                    await UIManager.Instance.CloseAsync<HubSettingsView>(animated: false);
                    await UIManager.Instance.CloseAsync<DisplayConfirmView>(animated: false);
                    await Wait(() => !SettingsFlow.IsSubmitting, "收口语言设置事务", 15);
                    var restored = await HubLanguageFlow.ApplyAsync(previous, reopen);
                    Assert.That(restored.Succeeded, Is.True, restored.Message);
                    GlobalData.Dispatch(new MainMenuSelectAction(originalSelection));
                }
                finally
                {
                    RestoreSettingsPreference(hadPreference, preference);
                }
            }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator DisplayConfirmationUsesRealButtonsAndTimesOutWhileFrozen()
        {
            yield return PrepareHub();
            yield return VerifyDisplayConfirmationAsync().ToCoroutine();
        }

        private async UniTask VerifyDisplayConfirmationAsync()
        {
            var previous = GameSettingsService.Current;
            bool hadPreference = PlayerPrefs.HasKey(GameSettingsService.PreferenceKey);
            string preference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            try
            {
                using var clock = GameTimeService.Push(GameTimeService.Scale);
                using var freeze = clock.Freeze();
                await SettingsFlow.OpenAsync();
                await UniTask.Yield();
                await UniTask.Yield();
                var settings = UIManager.Instance.Get<HubSettingsView>();
                var screen = settings;
                await SelectSettingsPageAsync(screen, SettingsCategory.Graphics);
                var modes = new[] { -1, (int)FullScreenMode.Windowed, (int)FullScreenMode.FullScreenWindow, (int)FullScreenMode.ExclusiveFullScreen };
                for (int decision = 0; decision < 3; decision++)
                {
                    var before = GameSettingsService.Current;
                    string beforePreference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
                    var page = (SettingsPanelView)Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView;
                    var row = page.Rows.Single(value => value.Key == "WindowMode");
                    var choices = Field<UITab>(row, "options");
                    int choice = Array.FindIndex(modes, value => value >= 0 && value != (int)Screen.fullScreenMode
                        && value != before.Graphics.WindowMode);
                    Assert.That(choice, Is.GreaterThan(0));
                    await Click(ButtonPosition((Button)row.Control));
                    await Click(ButtonPosition(choices.Items[choice].GetComponent<Button>()));
                    Assert.That(screen.Capture().Graphics.WindowMode, Is.EqualTo(modes[choice]));
                    await Click(ButtonPosition(Field<Button>(screen, "Button_Apply")));
                    DisplayConfirmView confirm = null;
                    await Wait(() => (confirm = UIManager.Instance.Get<DisplayConfirmView>())?.IsEnable == true,
                        "正式十五秒显示确认", 10);
                    var confirmation = Field<DisplayConfirmScreen>(confirm, "DisplayConfirmScreen_Screen");
                    var request = Field<DisplayConfirmationRequest>(confirmation, "request");
                    Assert.That(request.Deadline - Time.realtimeSinceStartupAsDouble, Is.InRange(10, 15));
                    Assert.That(settings.State, Is.EqualTo(ViewState.Visible));
                    Assert.That(GameTimeService.Scale, Is.Zero);
                    if (decision == 0)
                    {
                        System.IO.Directory.CreateDirectory("Library/GraphicsSettings");
                        ScreenCapture.CaptureScreenshot("Library/GraphicsSettings/DisplayConfirm.png");
                        await UniTask.Yield();
                        await Click(ButtonPosition(Field<Button>(confirmation, "keep")));
                    }
                    else if (decision == 1)
                    {
                        await Click(ButtonPosition(Field<Button>(confirmation, "revert")));
                    }
                    await Wait(() => confirm.State == ViewState.Destroyed && !SettingsFlow.IsSubmitting,
                        decision == 2 ? "冻结期间十五秒超时恢复" : "确认结果回到设置页", 20);
                    if (decision == 0)
                    {
                        Assert.That(GameSettingsService.Current.Graphics.WindowMode, Is.EqualTo(modes[choice]));
                        Assert.That(LocalDataManager.LoadData<GameSettingsSnapshot>(GameSettingsService.PreferenceKey).Graphics.WindowMode,
                            Is.EqualTo(modes[choice]));
                    }
                    else
                    {
                        Assert.That(JsonUtility.ToJson(GameSettingsService.Current), Is.EqualTo(JsonUtility.ToJson(before)));
                        Assert.That(PlayerPrefs.GetString(GameSettingsService.PreferenceKey), Is.EqualTo(beforePreference));
                    }
                    Assert.That(GameTimeService.Scale, Is.Zero);
                }
            }
            finally
            {
                await UIManager.Instance.CloseAsync<DisplayConfirmView>(animated: false);
                await UIManager.Instance.CloseAsync<HubSettingsView>(animated: false);
                await Wait(() => !SettingsFlow.IsSubmitting, "结束显示确认事务", 20);
                try { Assert.That((await GameSettingsService.ApplyAsync(previous, _ => UniTask.FromResult(true))).Succeeded, Is.True); }
                finally { RestoreSettingsPreference(hadPreference, preference); }
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PresentationEntryUsesRealControlsAndRestoresWorldAfterFrozenCancellation()
        {
            yield return PrepareHub();
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1920, 1200), new Vector2Int(3440, 1440) })
            {
                using var viewport = new Tests.Demo.GameViewResolution(size.x, size.y);
                yield return Wait(() => Screen.width == size.x && Screen.height == size.y, "电影演出目标画幅", 5);
                yield return VerifyPresentationEntryAsync().ToCoroutine();
            }
        }

        private async UniTask VerifyPresentationEntryAsync()
        {
            var previous = GameSettingsService.Current;
            bool hadPreference = PlayerPrefs.HasKey(GameSettingsService.PreferenceKey);
            string preference = PlayerPrefs.GetString(GameSettingsService.PreferenceKey);
            var hubCamera = UIRootManager.Instance.BaseCamera;
            var listener = hubCamera.GetComponent<AudioListener>();
            bool cameraEnabled = hubCamera.enabled;
            bool listenerEnabled = listener != null && listener.enabled;
            float originalScale = GameTimeService.Scale;
            PresentationLabView lab = null;
            try
            {
                var options = previous.Copy();
                options.Accessibility.ReducedMotion = ReducedMotionMode.Off;
                options.Graphics.AllowedEffects = PostProcessEffects.All;
                var applied = await GameSettingsService.ApplyAsync(options, confirmDisplay: _ => UniTask.FromResult(true));
                Assert.That(applied.Succeeded, Is.True, applied.Message);
                var menu = UIManager.Instance.Get<MainMenuView>();
                await Wait(() => Field<Button>(menu, "Button_Effects").interactable, "设置事务结束后恢复体验入口", 5);
                await Click(ButtonPosition(Field<Button>(menu, "Button_Effects")));
                await Wait(() => (lab = UIManager.Instance.Get<PresentationLabView>())?.IsEnable == true
                    && Field<PresentationLabBindings>(lab, "PresentationLabBindings_Lab").Play.interactable, "正式效果体验入口", 15);
                var bindings = Field<PresentationLabBindings>(lab, "PresentationLabBindings_Lab");
                var world = Object.FindFirstObjectByType<PresentationWorld>();
                var overlay = UIManager.Instance.Get<CinematicOverlayView>();
                var presenter = Field<CinematicPresenter>(overlay, "CinematicPresenter_Overlay");
                Assert.That(presenter.IsInitialized, Is.True);
                Assert.That(UIRootManager.Instance.BaseCamera, Is.SameAs(world.WorldCamera));
                Assert.That(hubCamera.enabled, Is.False);
                if (listener != null) Assert.That(listener.enabled, Is.False);
                System.IO.Directory.CreateDirectory("Library/GraphicsSettings");

                await Click(ButtonPosition(bindings.Tabs.UiTab.Items[0].GetComponent<Button>()));
                int initialEffects = PostProcessRuntime.ActiveScopeCount;
                await Click(ButtonPosition((Button)Field<UISettingRow>(bindings.Effects[0], "toggleRow").Control));
                Assert.That(PostProcessRuntime.ActiveScopeCount, Is.EqualTo(initialEffects + 1));
                ScreenCapture.CaptureScreenshot("Library/GraphicsSettings/Lab-Post.png");
                await UniTask.Yield();
                await Click(ButtonPosition(bindings.Stop));
                await Wait(() => PostProcessRuntime.ActiveScopeCount == initialEffects && bindings.Play.interactable, "停止画面体验作用域", 5);

                var parameterScroll = bindings.Effects[0].GetComponentInParent<ScrollRect>();
                parameterScroll.verticalNormalizedPosition = 0.4f;
                parameterScroll.velocity = new Vector2(0, 50);
                await Click(ButtonPosition(bindings.Tabs.UiTab.Items[2].GetComponent<Button>()));
                await UniTask.Yield();
                Assert.That(parameterScroll.verticalNormalizedPosition, Is.EqualTo(1).Within(0.001f),
                    "切换体验分类后应显示首项，不沿用画面效果表单的滚动偏移。");
                Assert.That(parameterScroll.velocity, Is.EqualTo(Vector2.zero), "旧分类的滚动惯性不能带入新分类。");

                var styleDropdown = Field<UISettingRow>(bindings.CinematicPanel, "styleRow").Control.GetComponent<UIDropdown>();
                await Click(ButtonPosition(styleDropdown.GetComponent<Button>()));
                var styleOptions = Field<UITab>(styleDropdown, "tabView");
                var popupRect = (RectTransform)styleOptions.transform;
                var popupBackground = styleOptions.GetComponent<Image>();
                Assert.That(popupBackground, Is.Not.Null, "公共下拉浮层不能让后面的字段从选项间距透出。");
                Assert.That(popupBackground.color.a, Is.EqualTo(1));
                var ownerRect = (RectTransform)styleDropdown.transform;
                float popupTop = ownerRect.InverseTransformPoint(popupRect.TransformPoint(new Vector3(0, popupRect.rect.yMax))).y;
                Assert.That(popupTop, Is.LessThanOrEqualTo(ownerRect.rect.yMin),
                    "有足够空间时，增长后的浮层应从控件下方展开，不能顶到上方分类按钮。");
                foreach (var item in styleOptions.Items)
                {
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(popupRect, item.transform);
                    Assert.That(bounds.min.y, Is.GreaterThanOrEqualTo(popupRect.rect.yMin - 0.01f));
                    Assert.That(bounds.max.y, Is.LessThanOrEqualTo(popupRect.rect.yMax + 0.01f),
                        "下拉背景须随真实选项数量增长，不能只覆盖制作模板的前几项。");
                }
                await Click(ButtonPosition(styleOptions.Items[styleDropdown.CurrentIndex].GetComponent<Button>()));

                await Click(ButtonPosition(bindings.Tabs.UiTab.Items[1].GetComponent<Button>()));
                await Click(ButtonPosition(bindings.Play));
                Assert.That(GameTimeService.Scale, Is.EqualTo(0.25f).Within(0.001f));
                ScreenCapture.CaptureScreenshot("Library/GraphicsSettings/Lab-Time.png");
                await UniTask.Yield();
                await Click(ButtonPosition(bindings.Stop));
                await Wait(() => Mathf.Approximately(GameTimeService.Scale, originalScale) && bindings.Play.interactable, "停止时间体验作用域", 5);

                await Click(ButtonPosition(bindings.Tabs.UiTab.Items[2].GetComponent<Button>()));
                float originalHudAlpha = bindings.PreviewHud.alpha;
                bool originalHudInteractable = bindings.PreviewHud.interactable;
                bool originalHudRaycasts = bindings.PreviewHud.blocksRaycasts;
                await Click(ButtonPosition(bindings.Play));
                await Wait(() => !presenter.IsTransitioning && !presenter.IsTitlePlaying && bindings.Play.interactable,
                    "电影演出正常完成并保持黑边", 8);
                Assert.That(presenter.IsLetterboxVisible, Is.True);
                Assert.That(bindings.PreviewHud.alpha, Is.Zero.Within(0.001f));
                Assert.That(bindings.PreviewHud.interactable, Is.False);
                Assert.That(bindings.PreviewHud.blocksRaycasts, Is.False);
                ScreenCapture.CaptureScreenshot($"Library/GraphicsSettings/Lab-Letterbox-{Screen.width}x{Screen.height}.png");
                await UniTask.Yield();
                await Click(ButtonPosition(bindings.Stop));
                await Wait(() => !presenter.IsLetterboxVisible && bindings.Play.interactable, "正常收回黑边", 5);
                Assert.That(bindings.PreviewHud.alpha, Is.EqualTo(originalHudAlpha).Within(0.001f));
                Assert.That(bindings.PreviewHud.interactable, Is.EqualTo(originalHudInteractable));
                Assert.That(bindings.PreviewHud.blocksRaycasts, Is.EqualTo(originalHudRaycasts));

                using (var clock = GameTimeService.Push(originalScale))
                using (clock.Freeze())
                {
                    float worldTime = Field<float>(world, "elapsed");
                    string uiClock = bindings.Clock.text;
                    await UniTask.Delay(TimeSpan.FromSeconds(1.1), ignoreTimeScale: true);
                    Assert.That(bindings.Clock.text, Is.Not.EqualTo(uiClock));
                    Assert.That(Field<float>(world, "elapsed"), Is.EqualTo(worldTime));
                    await Click(ButtonPosition(bindings.Tabs.UiTab.Items[2].GetComponent<Button>()));
                    await Click(ButtonPosition(bindings.Play));
                    var curtain = Field<Image>(presenter, "curtain");
                    await Wait(() => presenter.IsLetterboxVisible && presenter.IsTransitioning
                        && Field<CanvasGroup>(presenter, "transitionGroup").blocksRaycasts
                        && curtain.fillAmount > 0.1f,
                        "冻结中播放正式黑幕与黑边", 5);
                    ScreenCapture.CaptureScreenshot("Library/GraphicsSettings/Lab-Cinematic.png");
                    await UniTask.Yield();
                    await Click(ButtonPosition(bindings.Stop));
                    await Wait(() => !presenter.IsLetterboxVisible && !presenter.IsTransitioning
                        && !presenter.IsTitlePlaying && bindings.Play.interactable, "取消并恢复电影通道", 5);
                    Assert.That(bindings.PreviewHud.alpha, Is.EqualTo(1).Within(0.001f));
                    Assert.That(Field<CanvasGroup>(presenter, "transitionGroup").alpha, Is.Zero);
                    Assert.That(Field<CanvasGroup>(presenter, "titleGroup").alpha, Is.Zero);
                    await Click(ButtonPosition(bindings.Close));
                    await Wait(() => lab.State == ViewState.Destroyed && UIManager.Instance.Get<MainMenuView>()?.IsEnable == true,
                        "关闭冻结体验并恢复Hub", 10);
                    Assert.That(GameTimeService.Scale, Is.Zero, "退出只能释放体验页自己的时间租约。");
                }
                Assert.That(UIManager.Instance.Get<CinematicOverlayView>(), Is.Null);
                Assert.That(UIRootManager.Instance.BaseCamera, Is.SameAs(hubCamera));
                Assert.That(hubCamera.enabled, Is.EqualTo(cameraEnabled));
                if (listener != null) Assert.That(listener.enabled, Is.EqualTo(listenerEnabled));
                Assert.That(GameTimeService.Scale, Is.EqualTo(originalScale));
                await UniTask.Yield();
                Assert.That(world == null, Is.True, "归还实例而不是隐藏体验世界留在场景里。");
            }
            finally
            {
                if (lab != null && lab.IsLoaded) await PresentationLabFlow.CloseAsync(lab);
                try
                {
                    var restored = await GameSettingsService.ApplyAsync(previous, confirmDisplay: _ => UniTask.FromResult(true));
                    Assert.That(restored.Succeeded, Is.True, restored.Message);
                }
                finally { RestoreSettingsPreference(hadPreference, preference); }
            }
        }

        private async UniTask SelectSettingsPageAsync(HubSettingsView screen, SettingsCategory category)
        {
            var tab = Field<ViewTab>(screen, "ViewTab_Editor_controls");
            await Click(ButtonPosition(tab.UiTab.Items[(int)category].GetComponent<Button>()));
            await Wait(() => screen.Category == category && tab.CurrentClickView?.GetType().Name == category + "SettingsPanelView"
                && tab.CurrentClickView.State == ViewState.Visible, "懒加载并显示独立设置分类 " + category, 10);
        }

        private async UniTask<float> EditMasterVolumeAsync(HubSettingsView screen, float original)
        {
            var page = (SettingsPanelView)Field<ViewTab>(screen, "ViewTab_Editor_controls").CurrentClickView;
            var slider = (Slider)page.Rows.Single(row => row.Key == "Master").Control;
            Canvas.ForceUpdateCanvases();
            var track = (RectTransform)slider.handleRect.parent;
            float normalized = original < 0.5f ? 0.8f : 0.2f;
            var local = new Vector2(Mathf.Lerp(track.rect.xMin, track.rect.xMax, normalized), track.rect.center.y);
            var position = RectTransformUtility.WorldToScreenPoint(slider.GetComponentInParent<Canvas>().worldCamera, track.TransformPoint(local));
            await Click(position);
            Assert.That(Mathf.Abs(slider.value - original), Is.GreaterThan(0.1f), "真实滑条指针输入必须改变候选。");
            return slider.value;
        }

        private IEnumerator Tap(Vector2 position)
        {
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Began }); yield return null;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = position,
                phase = UnityEngine.InputSystem.TouchPhase.Ended }); yield return null; yield return null;
        }

        private IEnumerator PrepareHub()
        {
            // 固定在项目最低支持尺寸，避免 Editor 窗口大小改变交互验收范围。
            resolution = new Tests.Demo.GameViewResolution(1920, 1080);
            yield return Wait(() => Screen.width == 1920 && Screen.height == 1080, "最低支持分辨率", 5);
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                Assert.That(startup, Is.Not.Null);
                yield return Wait(() => startup.isDone, "AppEntrance 启动", 90);
            }
            if (GameSceneNavigator.Instance?.CurrentScene == GameSceneId.Hub && UIManager.Instance?.Get<MainMenuView>() == null)
            {
                yield return UIManager.Instance.OpenAsync<MainMenuView>(new UIOpenOptions(false)).ToCoroutine();
            }
            yield return Wait(IsStableHub, "Hub 稳定", 90);
            originalInputSettings = InputSystem.settings;
            testInputSettings = Object.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            mouse = InputSystem.AddDevice<Mouse>();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(mouse, new MouseState());
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            originalNavigator = GameSceneNavigator.Instance;
            runtime = new FailingSceneRuntime();
            SetNavigator(new GameSceneNavigator(runtime, new GameSceneLoadingPresenter()));
            // 每项测试从正常新开页面开始，不能把上一项指针清焦点后的页面当首次显示。
            yield return UIManager.Instance.CloseAsync<MainMenuView>(animated: false).ToCoroutine();
            MainMenuView shown = null;
            yield return UIManager.Instance.OpenAsync<MainMenuView>(new UIOpenOptions(false)).ToCoroutine(view => shown = view);
            Assert.That(shown.State, Is.EqualTo(ViewState.Visible));
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                if (gamepad != null && gamepad.added) InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null;
                if (runtime?.HasPendingEntry == true)
                {
                    LogAssert.Expect(LogType.Error, new Regex(@"\[MainMenu\] 无法进入 .*：测试场景加载失败"));
                    runtime.FailEntry();
                    yield return Wait(IsStableHub, "清理待完成的进入请求", 15);
                }
                // Test Runner 退出 PlayMode 前按页面生命周期释放输入循环。
                yield return UIManager.Instance.CloseAsync<MainMenuView>(animated: false).ToCoroutine();
            }
            finally
            {
                if (originalNavigator != null) SetNavigator(originalNavigator);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
                if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);
                if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
                if (testInputSettings != null) Object.Destroy(testInputSettings);
                resolution?.Dispose();
                resolution = null;
            }
        }

        private IEnumerator Click(Vector2 position)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }); yield return null; yield return null;
        }

        private static Vector2 ButtonPosition(Button button)
        {
            Assert.That(button.isActiveAndEnabled && button.interactable, Is.True,
                $"{button.name}: activeSelf={button.gameObject.activeSelf}, activeHierarchy={button.gameObject.activeInHierarchy}, enabled={button.enabled}, interactable={button.interactable}");
            Assert.That(button.IsInteractable(), Is.True, "父 CanvasGroup 不能禁用当前可操作按钮。");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var position = RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera, rect.TransformPoint(rect.rect.center));
            // 桌面鼠标使用整像素坐标，避免投影的分数像素落在原生矩形命中的精度空隙。
            position = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
            Assert.That(position.x, Is.InRange(0, Screen.width));
            Assert.That(position.y, Is.InRange(0, Screen.height));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button),
                "实际入口不能被其它 UI 遮挡。命中=" + hits[0].gameObject.name + "; 目标=" + button.name);
            return position;
        }

        private static void SetNavigator(GameSceneNavigator navigator) =>
            typeof(GameSceneNavigator).GetProperty(nameof(GameSceneNavigator.Instance)).SetValue(null, navigator);

        private static IEnumerator WaitForInitialSelection(MainMenuView menu) => Wait(() =>
            EventSystem.current.sendNavigationEvents && EventSystem.current.currentSelectedGameObject ==
                (!string.IsNullOrEmpty(GlobalData.Get<MainMenuData>().FeedbackKey) || !string.IsNullOrEmpty(GlobalData.Get<MainMenuData>().Feedback)
                    ? Field<Button>(menu, "Button_Start").gameObject : CardForScene(menu, GameSceneId.DroneFlight).gameObject),
            "公共输入恢复 Hub 默认焦点", 5);

        private static GameSceneId SelectedScene(MainMenuView menu)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            Assert.That(selected?.GetComponent<LoopScrollMenuButton>(), Is.Not.Null, "焦点必须落在列表的真实按钮。");
            var cell = selected.GetComponentInParent<LoopCell>();
            Assert.That(cell.Context.IsCurrent, Is.True);
            var entry = Field<List<MainMenuDemoEntry>>(menu, "entries")[cell.Context.Index];
            Assert.That(entry.SceneId.HasValue, Is.True, "焦点必须落在当前绑定的已开放 Demo 卡片。");
            return entry.SceneId.Value;
        }

        private static IEnumerable<(Button Button, MainMenuDemoEntry Entry)> VisibleCards(MainMenuView menu)
        {
            var list = Field<LoopScrollView>(menu, "LoopScrollView_DemoList");
            var entries = Field<List<MainMenuDemoEntry>>(menu, "entries");
            for (int i = 0; i < list.Count; i++)
            {
                var cell = list.GetVisibleCell(i);
                if (cell != null && cell.Context.IsCurrent)
                    yield return (cell.GetComponentInChildren<LoopScrollMenuButton>(true), entries[i]);
            }
        }

        private static int SceneIndex(GameSceneId target)
        {
            int index = GlobalData.Get<MainMenuData>().Entries.FindIndex(entry => entry.SceneId == target);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "测试需要已登记的实际Demo。");
            return index;
        }

        private static IEnumerator RevealCard(MainMenuView menu, GameSceneId target)
        {
            var entries = Field<List<MainMenuDemoEntry>>(menu, "entries");
            int index = entries.FindIndex(entry => entry.SceneId == target);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            var list = Field<LoopScrollView>(menu, "LoopScrollView_DemoList");
            list.ScrollToCell(index, ScrollAlignment.Center);
            yield return Wait(() => list.GetVisibleCell(index)?.Context.IsCurrent == true, "目标卡片实际进入视口", 5);
            yield return null;
        }

        private IEnumerator MoveToOpenCard(MainMenuView menu, bool useGamepad)
        {
            var entries = Field<List<MainMenuDemoEntry>>(menu, "entries");
            for (int attempt = 0; attempt < entries.Count; attempt++)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                var cell = selected != null ? selected.GetComponentInParent<LoopCell>() : null;
                if (cell != null && cell.Context.IsCurrent && entries[cell.Context.Index].SceneId.HasValue)
                {
                    yield break;
                }
                if (useGamepad)
                {
                    InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadRight));
                    yield return null;
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                }
                else
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
                    yield return null;
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                }
                yield return null;
                yield return null;
                yield return Wait(SelectedCardIsVisible, "跳过未开放卡片后恢复真实焦点", 5);
            }
            Assert.Fail("没有通过真实导航到达已开放Demo。");
        }

        private static Button CardForScene(MainMenuView menu, GameSceneId target)
        {
            foreach (var card in VisibleCards(menu)) if (card.Entry.SceneId == target) return card.Button;
            Assert.Fail("目标 Demo 卡片未绑定：" + target);
            return null;
        }

        private static bool SelectedCardIsVisible()
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            var card = selected != null ? selected.GetComponent<LoopScrollMenuButton>() : null;
            if (card == null) return false;
            var list = card.GetComponentInParent<LoopScrollView>();
            var cell = card.GetComponentInParent<LoopCell>();
            if (list == null || cell == null || !cell.Context.IsCurrent) return false;
            var viewport = list.ScrollRect.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, cell.RectTransform);
            return bounds.min.y >= viewport.rect.yMin - 1 && bounds.max.y <= viewport.rect.yMax + 1 &&
                bounds.min.x >= viewport.rect.xMin - 1 && bounds.max.x <= viewport.rect.xMax + 1;
        }

        private void AssertRequestedScene(GameSceneId target)
        {
            Assert.That(GameSceneCatalog.TryGet(target, out var definition), Is.True);
            Assert.That(runtime.LastAddress, Is.EqualTo(definition.Address), "选中物件和实际进入目标必须一致。");
        }

        private static void ExpectEntryFailure(GameSceneId target) =>
            LogAssert.Expect(LogType.Error, new Regex($@"\[MainMenu\] 无法进入 {target}：测试场景加载失败"));

        private static T Field<T>(object instance, string name)
        {
            for (var type = instance.GetType(); type != null; type = type.BaseType)
            {
                var field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return (T)field.GetValue(instance);
            }
            Assert.Fail("测试对象缺少正式字段：" + name);
            return default;
        }

        private static bool IsStableHub() => GameSceneNavigator.Instance != null &&
            GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning &&
            UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible &&
            !UIRootManager.Instance.InteractionGate.IsBlocking && Object.FindFirstObjectByType<StartupLoadingView>() == null;

        private static IEnumerator Wait(Func<bool> predicate, string reason, float timeout)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, reason + "超时");
        }

        private sealed class FailingSceneRuntime : IGameSceneRuntime
        {
            private UniTaskCompletionSource<GameSceneRuntimeResult> pending;
            internal int LoadCount { get; private set; }
            internal string LastAddress { get; private set; }
            internal bool HasPendingEntry => pending != null;

            public async UniTask<GameSceneRuntimeResult> LoadAsync(string address, Action<float> onProgress)
            {
                LoadCount++;
                LastAddress = address;
                pending = new UniTaskCompletionSource<GameSceneRuntimeResult>();
                return await pending.Task;
            }

            internal void FailEntry()
            {
                var completion = pending;
                pending = null;
                completion.TrySetResult(GameSceneRuntimeResult.Failure("测试场景加载失败"));
            }

            public UniTask<GameSceneRuntimeResult> ReturnToHubAsync(Action<float> onProgress) =>
                UniTask.FromResult(GameSceneRuntimeResult.Success());
        }
    }
}
#endif
