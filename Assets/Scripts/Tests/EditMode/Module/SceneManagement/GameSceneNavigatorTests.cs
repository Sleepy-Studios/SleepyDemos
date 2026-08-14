using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class GameSceneNavigatorTests
    {
        [Test]
        public void Catalog_MapsBusinessSceneWithoutGivingHubAResourceAddress()
        {
            Assert.That(GameSceneCatalog.TryGet(GameSceneId.Hub, out var hub), Is.True);
            Assert.That(hub.IsHub, Is.True);
            Assert.That(hub.Address, Is.Null);
            Assert.That(GameSceneCatalog.TryGet(GameSceneId.DroneFlight, out var demo), Is.True);
            Assert.That(demo.Address, Is.EqualTo(GameSceneCatalog.DroneFlightAddress));
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

        [Test]
        public async Task ReloadCurrentAsync_Demo_UnloadsAndLoadsSameSceneAgain()
        {
            var runtime = new FakeSceneRuntime();
            var presenter = new FakeLoadingPresenter();
            var navigator = new GameSceneNavigator(runtime, presenter);
            await navigator.SwitchAsync(GameSceneId.DroneFlight);
            var loadCountBeforeReload = runtime.LoadCount;

            var result = await navigator.ReloadCurrentAsync();

            Assert.That(result.Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            Assert.That(navigator.CurrentScene, Is.EqualTo(GameSceneId.DroneFlight));
            Assert.That(runtime.ReturnCount, Is.EqualTo(1));
            Assert.That(runtime.LoadCount, Is.EqualTo(loadCountBeforeReload + 1));
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

            public UniTask<string> BeginAsync(GameSceneDefinition target)
            {
                BeginCount++;
                return UniTask.FromResult<string>(null);
            }

            public void SetProgress(float progress, string step, string description)
            {
                ProgressValues.Add(progress);
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
