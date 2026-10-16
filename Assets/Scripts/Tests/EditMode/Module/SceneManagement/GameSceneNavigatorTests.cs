using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Core.Runtime;
using Core.Runtime.Localization;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.AppDelegate;
using Hotfix.SceneManagement;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Module
{
    public sealed class GameSceneNavigatorTests
    {
        [Test]
        public void LoadingBeforeLanguageInitializationPreservesRawTextAndMonotonicProgress()
        {
            var textState = typeof(LanguageService).GetField("texts", BindingFlags.NonPublic | BindingFlags.Static);
            var previousTexts = textState.GetValue(null);
            var owner = new GameObject("Raw loading before language", typeof(RectTransform));
            var view = new CommonLoadingView();
            var title = CreateLoadingText("Title", "TextMeshProUGUI_Title", owner.transform, view);
            var step = CreateLoadingText("Step", "TextMeshProUGUI_Step", owner.transform, view);
            var description = CreateLoadingText("Description", "TextMeshProUGUI_Description", owner.transform, view);
            var percent = CreateLoadingText("Percent", "TextMeshProUGUI_ProgressText", owner.transform, view);
            try
            {
                textState.SetValue(null, null);
                view.SetTitle("正在加载", "Loading.Title");
                view.SetProgress(0.6f, "加载场景", "原始描述", stepKey: "Loading.LoadScene", descriptionKey: "Loading.LoadingScene", args: new object[] { "demo" });
                view.SetProgress(0.2f, "切换场景", "", stepKey: "Loading.SwitchScene");
                Assert.That(title.text, Is.EqualTo("正在加载"));
                Assert.That(step.text, Is.EqualTo("切换场景"));
                Assert.That(description.text, Is.Empty);
                Assert.That(percent.text, Is.EqualTo("60%"));
                view.ResetProgress();
                Assert.That(percent.text, Is.EqualTo("0%"));
            }
            finally
            {
                textState.SetValue(null, previousTexts);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void MainMenuDiagnosticFeedbackClearsKeyAndResetClearsBothChannels()
        {
            var data = new MainMenuData { FeedbackKey = "Hub.EntryFailed" };
            var handler = new MainMenuHandler();
            handler.Init(data);
            handler.ReduceAny(new MainMenuFeedbackAction("Loader diagnostic: asset missing"));
            Assert.That(data.Feedback, Is.EqualTo("Loader diagnostic: asset missing"));
            Assert.That(data.FeedbackKey, Is.Null);
            data.FeedbackKey = "Hub.EntryNotReady";
            data.ClearData();
            Assert.That(data.Feedback, Is.Null);
            Assert.That(data.FeedbackKey, Is.Null);
        }

        private static TextMeshProUGUI CreateLoadingText(string name, string field, Transform parent, CommonLoadingView view)
        {
            var owner = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            owner.transform.SetParent(parent, false);
            var text = owner.GetComponent<TextMeshProUGUI>();
            typeof(CommonLoadingView).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, text);
            return text;
        }

        [Test]
        public void Catalog_DirectEntryResolvesAllRegisteredDemoPaths()
        {
            foreach (GameSceneId scene in Enum.GetValues(typeof(GameSceneId)))
            {
                if (scene == GameSceneId.Hub)
                {
                    continue;
                }
                Assert.That(GameSceneCatalog.TryGet(scene, out var definition), Is.True);
                Assert.That(GameSceneCatalog.TryFindSceneId(definition.Address, out var resolved), Is.True);
                Assert.That(resolved, Is.EqualTo(scene));
            }
            foreach (var entry in new MainMenuData().Entries)
            {
                if (entry.SceneId.HasValue)
                {
                    Assert.That(GameSceneCatalog.TryGet(entry.SceneId.Value, out _), Is.True, entry.Key);
                }
            }
            Assert.That(GameSceneCatalog.TryFindSceneId("Assets/Scenes/AppEntrance.unity", out _), Is.False);
        }

        [Test]
        public void Catalog_MapsBusinessSceneWithoutGivingHubAResourceAddress()
        {
            Assert.That(GameSceneCatalog.TryGet(GameSceneId.Hub, out var hub), Is.True);
            Assert.That(hub.IsHub, Is.True);
            Assert.That(hub.Address, Is.Null);
            Assert.That(hub.LanguageCatalogAddress, Is.Null);
            Assert.That(GameSceneCatalog.TryGet(GameSceneId.DroneFlight, out var demo), Is.True);
            Assert.That(demo.Address, Is.EqualTo(GameSceneCatalog.DroneFlightAddress));
            Assert.That(demo.LanguageCatalogAddress, Is.EqualTo("LoadResources/Demos/drone_flight/Data/LanguageCatalog"));
            Assert.That(demo.DisplayNameKey, Is.EqualTo("Hub.drone_flight.Title"));
            Assert.That(hub.DisplayNameKey, Is.EqualTo("Scene.Hub"));
            Assert.That(GameSceneCatalog.TryGet(GameSceneId.Dlss, out var dlss), Is.True);
            Assert.That(dlss.Address, Is.EqualTo(GameSceneCatalog.DlssAddress));
            Assert.That(dlss.IsHub, Is.False);
        }

        [Test]
        public async Task SwitchAsync_CurrentScene_ReturnsIgnoredWithoutOpeningLoading()
        {
            var runtime = new FakeSceneRuntime();
            var presenter = new FakeLoadingPresenter();
            var navigator = CreateNavigator(runtime, presenter, () => UniTask.CompletedTask);

            var result = await navigator.SwitchAsync(GameSceneId.Hub);

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Ignored));
            Assert.That(runtime.LoadCount, Is.Zero);
            Assert.That(presenter.BeginCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task SwitchAsync_RuntimeFailure_RestoresSourceAndReturnsFailed(bool restoreFails)
        {
            var runtime = new FakeSceneRuntime
            {
                NextResult = GameSceneRuntimeResult.Failure("测试加载失败")
            };
            var presenter = new FakeLoadingPresenter { RestoreError = restoreFails ? "测试恢复失败" : null };
            var navigator = CreateNavigator(runtime, presenter, () => UniTask.CompletedTask);

            var result = await navigator.SwitchAsync(GameSceneId.DroneFlight);

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
            Assert.That(result.Error, Does.Contain("测试加载失败"));
            Assert.That(presenter.RestoredScene, Is.EqualTo(GameSceneId.Hub));
            Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.Hub));
            Assert.That(navigator.IsTransitioning, Is.False);
            if (restoreFails)
            {
                Assert.That(result.Error, Does.Contain("测试恢复失败"));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task CompletedScene_UiFailure_RestoresCommittedSceneAndAllowsRetry(bool reload)
        {
            var runtime = new FakeSceneRuntime();
            var presenter = new FakeLoadingPresenter();
            var navigator = CreateNavigator(runtime, presenter, () => UniTask.CompletedTask);
            if (reload)
            {
                await navigator.SwitchAsync(GameSceneId.DroneFlight);
            }

            presenter.CompleteError = "测试收口失败";
            var result = reload
                ? await navigator.ReloadCurrentAsync()
                : await navigator.SwitchAsync(GameSceneId.DroneFlight);

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
            Assert.That(result.Error, Does.Contain("测试收口失败"));
            Assert.That(presenter.RestoredScene, Is.EqualTo(GameSceneId.DroneFlight));
            Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.DroneFlight));
            Assert.That(navigator.IsTransitioning, Is.False);
            presenter.CompleteError = null;
            Assert.That((await navigator.SwitchAsync(GameSceneId.Hub)).Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task PreparationFailure_UnloadsCommittedSceneBeforeRestoringHub(bool reload, bool recoveryFails)
        {
            var runtime = new FakeSceneRuntime();
            var presenter = new FakeLoadingPresenter();
            bool failPreparation = false;
            GameSceneNavigator navigator = null;
            navigator = CreateNavigator(runtime, presenter, () =>
            {
                Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.DroneFlight));
                Assert.That(navigator.IsTransitioning, Is.True);
                return failPreparation
                    ? UniTask.FromException(new InvalidOperationException("测试准备失败"))
                    : UniTask.CompletedTask;
            });
            if (reload)
            {
                await navigator.SwitchAsync(GameSceneId.DroneFlight);
            }
            failPreparation = true;
            // 重载的第一次 Return 是正常卸载来源，第二次才是准备失败恢复。
            runtime.FailReturnNumber = recoveryFails ? (reload ? 2 : 1) : 0;
            LogAssert.Expect(LogType.Exception, new Regex("测试准备失败"));

            var result = reload
                ? await navigator.ReloadCurrentAsync()
                : await navigator.SwitchAsync(GameSceneId.DroneFlight);

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
            Assert.That(result.Error, Does.Contain("测试准备失败"));
            Assert.That(runtime.ReturnCount, Is.EqualTo(reload ? 2 : 1));
            var actualScene = recoveryFails ? GameSceneId.DroneFlight : GameSceneId.Hub;
            Assert.That(navigator.CurrentScene, Is.EqualTo(actualScene));
            Assert.That(presenter.RestoredScene, Is.EqualTo(actualScene));
            Assert.That(navigator.IsTransitioning, Is.False);
            if (recoveryFails)
            {
                Assert.That(result.Error, Does.Contain("测试卸载失败"));
                runtime.FailReturnNumber = 0;
                Assert.That((await navigator.SwitchAsync(GameSceneId.Hub)).Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            }
            failPreparation = false;
            Assert.That((await navigator.SwitchAsync(GameSceneId.DroneFlight)).Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
        }

        [Test]
        public async Task EditorDirectPreparationFailure_AfterOfficialHandoff_DoesNotOpenHubThroughOldPresenter()
        {
            var runtime = new FakeSceneRuntime();
            var presenter = new FakeLoadingPresenter();
            var navigator = CreateNavigator(runtime, presenter,
                () => UniTask.FromException(new InvalidOperationException("测试直启准备失败")));
            typeof(GameSceneNavigator).GetField("isEditorDirect", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(navigator, true);
            var instanceField = typeof(GameSceneNavigator).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
            var previousInstance = instanceField.GetValue(null);
            instanceField.SetValue(null, navigator);
            runtime.AfterReturn = GameSceneNavigator.ReleaseEditorDirect;
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("测试直启准备失败"));
                var result = await navigator.SwitchAsync(GameSceneId.DroneFlight);

                Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
                Assert.That(result.Error, Does.Contain("测试直启准备失败"));
                Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.Hub));
                Assert.That(GameSceneNavigator.Instance, Is.Null);
                Assert.That(presenter.RestoredScene, Is.EqualTo(GameSceneId.DroneFlight), "旧 Presenter 清理 Loading/遮挡，但不能按 Hub 抢建页面。");
                Assert.That(navigator.IsTransitioning, Is.False);
            }
            finally
            {
                instanceField.SetValue(null, previousInstance);
            }
        }

        [Test]
        public async Task SwitchAsync_LoadFailureFromDemo_PreservesSourceWithoutUnloadingIt()
        {
            var runtime = new FakeSceneRuntime();
            var presenter = new FakeLoadingPresenter();
            var navigator = CreateNavigator(runtime, presenter, () => UniTask.CompletedTask);
            await navigator.SwitchAsync(GameSceneId.DroneFlight);
            runtime.NextResult = GameSceneRuntimeResult.Failure("测试加载失败");

            var result = await navigator.SwitchAsync(GameSceneId.WallSqueeze);

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
            Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.DroneFlight));
            Assert.That(presenter.RestoredScene, Is.EqualTo(GameSceneId.DroneFlight));
            Assert.That(runtime.ReturnCount, Is.Zero);
            Assert.That(navigator.IsTransitioning, Is.False);
        }

        [Test]
        public async Task SwitchAsync_RuntimeProgress_IsReportedMonotonically()
        {
            var runtime = new FakeSceneRuntime
            {
                ProgressValues = new[] { 0.8f, 0.2f, 1f }
            };
            var presenter = new FakeLoadingPresenter();
            var navigator = CreateNavigator(runtime, presenter, () => UniTask.CompletedTask);

            var result = await navigator.SwitchAsync(GameSceneId.DroneFlight);

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            Assert.That(presenter.StepKeys, Does.Contain("Loading.LoadScene"));
            Assert.That(presenter.DescriptionKeys, Does.Contain("Loading.LoadingScene"));
            Assert.That(presenter.DescriptionKeys, Does.Contain("Loading.Initialize"));
            Assert.That(GameSceneCatalog.TryGet(GameSceneId.DroneFlight, out var definition), Is.True);
            string name = LanguageService.IsInitialized ? LanguageService.Get(definition.DisplayNameKey) : definition.DisplayName;
            Assert.That(presenter.DescriptionArguments[0], Is.EqualTo(new object[] { name }));
            for (var index = 1; index < presenter.ProgressValues.Count; index++)
            {
                Assert.That(
                    presenter.ProgressValues[index],
                    Is.GreaterThanOrEqualTo(presenter.ProgressValues[index - 1]));
            }
        }

        [Test]
        public async Task SwitchAsync_WhileRequestIsPending_ReturnsBusy()
        {
            var completion = new UniTaskCompletionSource<GameSceneRuntimeResult>();
            var runtime = new FakeSceneRuntime { PendingCompletion = completion };
            var navigator = CreateNavigator(runtime, new FakeLoadingPresenter(), () => UniTask.CompletedTask);

            var first = navigator.SwitchAsync(GameSceneId.DroneFlight);
            await UniTask.Yield();
            var busy = await navigator.SwitchAsync(GameSceneId.DroneFlight);
            var reloadBusy = await navigator.ReloadCurrentAsync();
            completion.TrySetResult(GameSceneRuntimeResult.Success());
            var completed = await first;

            Assert.That(busy.Status, Is.EqualTo(GameSceneSwitchStatus.Busy));
            Assert.That(reloadBusy.Status, Is.EqualTo(GameSceneSwitchStatus.Busy));
            Assert.That(completed.Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
        }

        [TestCase(true)]
        [TestCase(false)]
        public async Task ReloadCurrentAsync_Demo_UnloadsAndLoadsSameSceneAgain(bool reportsLoadProgress)
        {
            var runtime = new FakeSceneRuntime
            {
                ProgressValues = reportsLoadProgress ? new[] { 0f, 1f } : Array.Empty<float>()
            };
            var presenter = new FakeLoadingPresenter();
            var navigator = CreateNavigator(runtime, presenter, () => UniTask.CompletedTask);
            await navigator.SwitchAsync(GameSceneId.DroneFlight);
            var loadCountBeforeReload = runtime.LoadCount;

            var result = await navigator.ReloadCurrentAsync();

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.DroneFlight));
            Assert.That(runtime.ReturnCount, Is.EqualTo(1));
            Assert.That(runtime.LoadCount, Is.EqualTo(loadCountBeforeReload + 1));
            Assert.That(presenter.StepKeys, Does.Contain("Loading.ReloadScene"));
            Assert.That(presenter.DescriptionKeys, Does.Contain("Loading.UnloadingScene"));
            Assert.That(presenter.DescriptionKeys, Does.Contain("Loading.ReloadingScene"));

            await navigator.SwitchAsync(GameSceneId.Hub);
            Assert.That(presenter.StepKeys, Does.Contain("Loading.UnloadScene"));
            Assert.That(presenter.DescriptionKeys, Does.Contain("Loading.ReturnHub"));
        }

        [TestCase(GameSceneId.DroneFlight)]
        [TestCase(GameSceneId.WallSqueeze)]
        [TestCase(GameSceneId.KagitKart)]
        public async Task Standalone_ReturnHubReloadsOwnSceneAndRejectsOtherDemos(GameSceneId ownScene)
        {
            var runtime = new FakeSceneRuntime();
            var presenter = new FakeLoadingPresenter();
            var navigator = CreateNavigator(runtime, presenter, () => UniTask.CompletedTask, ownScene);
            Assert.That((await navigator.SwitchAsync(ownScene)).Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            var otherScene = ownScene == GameSceneId.DroneFlight ? GameSceneId.WallSqueeze : GameSceneId.DroneFlight;
            int beginCount = presenter.BeginCount;

            Assert.That((await navigator.SwitchAsync(otherScene)).Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
            Assert.That(presenter.BeginCount, Is.EqualTo(beginCount));
            Assert.That(runtime.LoadCount, Is.EqualTo(1));
            var returned = await navigator.SwitchAsync(GameSceneId.Hub);

            Assert.That(returned.Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            Assert.That(navigator.CurrentScene, Is.EqualTo(ownScene));
            Assert.That(runtime.ReturnCount, Is.EqualTo(1));
            Assert.That(runtime.LoadCount, Is.EqualTo(2));
            Assert.That(navigator.IsTransitioning, Is.False);
        }

        [Test]
        public async Task Standalone_PreparationFailureReturnsToRestrictedHubAndAllowsOwnRetry()
        {
            bool failPreparation = true;
            var navigator = CreateNavigator(new FakeSceneRuntime(), new FakeLoadingPresenter(),
                () => failPreparation ? UniTask.FromException(new InvalidOperationException("独立场景准备失败")) : UniTask.CompletedTask,
                GameSceneId.KagitKart);
            LogAssert.Expect(LogType.Exception, new Regex("独立场景准备失败"));

            Assert.That((await navigator.SwitchAsync(GameSceneId.KagitKart)).Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
            Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.Hub));
            var menu = new MainMenuData();
            MainMenuView.RestrictStandaloneEntries(menu, navigator.StandaloneScene);
            Assert.That(menu.Entries.Count, Is.EqualTo(1));
            Assert.That(menu.Entries[0].SceneId, Is.EqualTo(GameSceneId.KagitKart));
            Assert.That(menu.Entries[0].IsSelected, Is.True);
            Assert.That(menu.SelectedIndex, Is.Zero);
            Assert.That((await navigator.SwitchAsync(GameSceneId.WallSqueeze)).Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
            failPreparation = false;
            Assert.That((await navigator.SwitchAsync(menu.Entries[0].SceneId.Value)).Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
        }

        [Test]
        public async Task HubDiagnosticInitialScenePreservesNormalHubPolicyAndEntries()
        {
            var standaloneScene = HotfixEntry.ResolveStandaloneScene("hub", GameSceneId.Dlss);
            Assert.That(standaloneScene, Is.Null);
            Assert.That(HotfixEntry.ResolveStandaloneScene("kagit_kart", GameSceneId.KagitKart), Is.EqualTo(GameSceneId.KagitKart));
            var menu = new MainMenuData();
            int count = menu.Entries.Count;
            MainMenuView.RestrictStandaloneEntries(menu, standaloneScene);
            Assert.That(menu.Entries.Count, Is.EqualTo(count));
            var runtime = new FakeSceneRuntime();
            var navigator = CreateNavigator(runtime, new FakeLoadingPresenter(), () => UniTask.CompletedTask, standaloneScene);

            Assert.That((await navigator.SwitchAsync(GameSceneId.Dlss)).Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            Assert.That((await navigator.SwitchAsync(GameSceneId.Hub)).Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.Hub));
            Assert.That(runtime.LoadCount, Is.EqualTo(1));
            Assert.That(runtime.ReturnCount, Is.EqualTo(1));
        }

        [Test]
        public async Task ReloadCurrentAsync_Hub_IsIgnoredWithoutRuntimeWork()
        {
            var runtime = new FakeSceneRuntime();
            var navigator = CreateNavigator(runtime, new FakeLoadingPresenter(), () => UniTask.CompletedTask);

            var result = await navigator.ReloadCurrentAsync();

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Ignored));
            Assert.That(runtime.LoadCount, Is.Zero);
            Assert.That(runtime.ReturnCount, Is.Zero);
        }

        [Test]
        public async Task CatalogLease_IsReadyBeforeSceneLoadAndPreservesSourceOnTargetFailure()
        {
            var leases = new List<TrackingCatalogLease>();
            var runtime = new FakeSceneRuntime();
            var navigator = CreateNavigator(runtime, new FakeLoadingPresenter(), () => UniTask.CompletedTask,
                loadContext: definition =>
                {
                    Assert.That(definition.LanguageCatalogAddress, Is.Not.Null);
                    var lease = new TrackingCatalogLease();
                    leases.Add(lease);
                    return UniTask.FromResult(new GameSceneLanguageScope(lease));
                });
            runtime.BeforeLoad = () => Assert.That(leases[leases.Count - 1].Disposed, Is.False,
                "场景加载以及其 Awake/OnEnable 之前词表租约已存在");
            try
            {
                Assert.That((await navigator.SwitchAsync(GameSceneId.DroneFlight)).Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
                runtime.NextResult = GameSceneRuntimeResult.Failure("目标场景加载失败");
                Assert.That((await navigator.SwitchAsync(GameSceneId.WallSqueeze)).Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
                Assert.That(leases[0].Disposed, Is.False, "来源未卸载，保留来源词表");
                Assert.That(leases[1].Disposed, Is.True, "失败目标的词表配对释放");
                runtime.NextResult = GameSceneRuntimeResult.Success();
                await navigator.SwitchAsync(GameSceneId.WallSqueeze);
                Assert.That(leases[0].Disposed, Is.True);
                Assert.That(leases[2].Disposed, Is.False);
                await navigator.SwitchAsync(GameSceneId.Hub);
                Assert.That(leases[2].Disposed, Is.True);
            }
            finally
            {
                var context = (GameSceneLanguageScope)typeof(GameSceneNavigator)
                    .GetField("currentContext", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(navigator);
                context?.Dispose();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task PreparationFailure_CatalogLeaseSurvivesUntilActualSceneUnload(bool recoveryFails)
        {
            var runtime = new FakeSceneRuntime { FailReturnNumber = recoveryFails ? 1 : 0 };
            var lease = new TrackingCatalogLease();
            var context = new GameSceneLanguageScope(lease);
            var navigator = CreateNavigator(runtime, new FakeLoadingPresenter(),
                () => UniTask.FromException(new InvalidOperationException("私有词表生命周期准备失败")),
                loadContext: _ => UniTask.FromResult(context));
            try
            {
                LogAssert.Expect(LogType.Exception, new Regex("私有词表生命周期准备失败"));
                Assert.That((await navigator.SwitchAsync(GameSceneId.KagitKart)).Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
                Assert.That(lease.Disposed, Is.EqualTo(!recoveryFails));
                if (recoveryFails)
                {
                    runtime.FailReturnNumber = 0;
                    await navigator.SwitchAsync(GameSceneId.Hub);
                    Assert.That(lease.Disposed, Is.True);
                }
            }
            finally
            {
                context.Dispose();
            }
        }

        private sealed class TrackingCatalogLease : IDisposable
        {
            internal bool Disposed { get; private set; }

            public void Dispose()
            {
                Disposed = true;
            }
        }

        private static GameSceneNavigator CreateNavigator(IGameSceneRuntime runtime, IGameSceneLoadingPresenter presenter,
            Func<UniTask> prepareScene = null, GameSceneId? standaloneScene = null,
            Func<GameSceneDefinition, UniTask<GameSceneLanguageScope>> loadContext = null)
        {
            return new GameSceneNavigator(runtime, presenter, prepareScene, standaloneScene,
                loadContext ?? (_ => UniTask.FromResult<GameSceneLanguageScope>(null)));
        }

        private sealed class FakeSceneRuntime : IGameSceneRuntime
        {
            internal GameSceneRuntimeResult NextResult { get; set; } = GameSceneRuntimeResult.Success();
            internal float[] ProgressValues { get; set; } = Array.Empty<float>();
            internal UniTaskCompletionSource<GameSceneRuntimeResult> PendingCompletion { get; set; }
            internal int LoadCount { get; private set; }
            internal int ReturnCount { get; private set; }
            internal int FailReturnNumber { get; set; }
            internal Action AfterReturn { get; set; }
            internal Action BeforeLoad { get; set; }

            public async UniTask<GameSceneRuntimeResult> LoadAsync(
                string address,
                Action<float> onProgress)
            {
                BeforeLoad?.Invoke();
                LoadCount++;
                foreach (var progress in ProgressValues)
                {
                    onProgress?.Invoke(progress);
                }

                return PendingCompletion == null ? NextResult : await PendingCompletion.Task;
            }

            public UniTask<GameSceneRuntimeResult> ReturnToHubAsync(Action<float> onProgress)
            {
                ReturnCount++;
                onProgress?.Invoke(1f);
                AfterReturn?.Invoke();
                return UniTask.FromResult(ReturnCount == FailReturnNumber
                    ? GameSceneRuntimeResult.Failure("测试卸载失败")
                    : NextResult);
            }
        }

        private sealed class FakeLoadingPresenter : IGameSceneLoadingPresenter
        {
            internal int BeginCount { get; private set; }
            internal string CompleteError { get; set; }
            internal string RestoreError { get; set; }
            internal GameSceneId? RestoredScene { get; private set; }
            internal List<float> ProgressValues { get; } = new List<float>();
            internal List<string> StepKeys { get; } = new();
            internal List<string> DescriptionKeys { get; } = new();
            internal List<object[]> DescriptionArguments { get; } = new();

            public UniTask<string> BeginAsync(GameSceneDefinition target)
            {
                BeginCount++;
                return UniTask.FromResult<string>(null);
            }

            public void SetProgress(float progress, string step, string description, string stepKey, string descriptionKey, params object[] args)
            {
                ProgressValues.Add(progress);
                StepKeys.Add(stepKey);
                DescriptionKeys.Add(descriptionKey);
                DescriptionArguments.Add((object[])args.Clone());
            }

            public UniTask<string> CompleteAsync(GameSceneId target)
            {
                ProgressValues.Add(1f);
                return UniTask.FromResult(CompleteError);
            }

            public UniTask RestoreAsync(GameSceneId source)
            {
                RestoredScene = source;
                if (RestoreError != null)
                {
                    throw new InvalidOperationException(RestoreError);
                }
                return UniTask.CompletedTask;
            }
        }
    }
}
