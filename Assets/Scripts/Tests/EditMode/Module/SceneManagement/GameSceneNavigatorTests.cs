using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Core.Runtime;
using Core.Runtime.Localization;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.SceneManagement;
using NUnit.Framework;
using TMPro;
using UnityEngine;

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
            Assert.That(GameSceneCatalog.TryGet(GameSceneId.DroneFlight, out var demo), Is.True);
            Assert.That(demo.Address, Is.EqualTo(GameSceneCatalog.DroneFlightAddress));
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
            var navigator = new GameSceneNavigator(runtime, presenter);

            var result = await navigator.SwitchAsync(GameSceneId.Hub);

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Ignored));
            Assert.That(runtime.LoadCount, Is.Zero);
            Assert.That(presenter.BeginCount, Is.Zero);
        }

        [Test]
        public async Task SwitchAsync_RuntimeFailure_RestoresSourceAndReturnsFailed()
        {
            var runtime = new FakeSceneRuntime
            {
                NextResult = GameSceneRuntimeResult.Failure("测试加载失败")
            };
            var presenter = new FakeLoadingPresenter();
            var navigator = new GameSceneNavigator(runtime, presenter);

            var result = await navigator.SwitchAsync(GameSceneId.DroneFlight);

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Failed));
            Assert.That(result.Error, Does.Contain("测试加载失败"));
            Assert.That(presenter.RestoredScene, Is.EqualTo(GameSceneId.Hub));
            Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.Hub));
        }

        [Test]
        public async Task SwitchAsync_RuntimeProgress_IsReportedMonotonically()
        {
            var runtime = new FakeSceneRuntime
            {
                ProgressValues = new[] { 0.8f, 0.2f, 1f }
            };
            var presenter = new FakeLoadingPresenter();
            var navigator = new GameSceneNavigator(runtime, presenter);

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
            var navigator = new GameSceneNavigator(runtime, new FakeLoadingPresenter());

            var first = navigator.SwitchAsync(GameSceneId.DroneFlight);
            await UniTask.Yield();
            var busy = await navigator.SwitchAsync(GameSceneId.DroneFlight);
            completion.TrySetResult(GameSceneRuntimeResult.Success());
            var completed = await first;

            Assert.That(busy.Status, Is.EqualTo(GameSceneSwitchStatus.Busy));
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
            var navigator = new GameSceneNavigator(runtime, presenter);
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

        [Test]
        public async Task ReloadCurrentAsync_Hub_IsIgnoredWithoutRuntimeWork()
        {
            var runtime = new FakeSceneRuntime();
            var navigator = new GameSceneNavigator(runtime, new FakeLoadingPresenter());

            var result = await navigator.ReloadCurrentAsync();

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Ignored));
            Assert.That(runtime.LoadCount, Is.Zero);
            Assert.That(runtime.ReturnCount, Is.Zero);
        }

        private sealed class FakeSceneRuntime : IGameSceneRuntime
        {
            internal GameSceneRuntimeResult NextResult { get; set; } = GameSceneRuntimeResult.Success();
            internal float[] ProgressValues { get; set; } = Array.Empty<float>();
            internal UniTaskCompletionSource<GameSceneRuntimeResult> PendingCompletion { get; set; }
            internal int LoadCount { get; private set; }
            internal int ReturnCount { get; private set; }

            public async UniTask<GameSceneRuntimeResult> LoadAsync(
                string address,
                Action<float> onProgress)
            {
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
                return UniTask.FromResult(NextResult);
            }
        }

        private sealed class FakeLoadingPresenter : IGameSceneLoadingPresenter
        {
            internal int BeginCount { get; private set; }
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
                return UniTask.FromResult<string>(null);
            }

            public UniTask RestoreAsync(GameSceneId source)
            {
                RestoredScene = source;
                return UniTask.CompletedTask;
            }
        }
    }
}
