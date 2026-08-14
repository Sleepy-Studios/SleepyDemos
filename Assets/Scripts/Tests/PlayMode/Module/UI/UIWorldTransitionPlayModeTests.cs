using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class UIWorldTransitionPlayModeTests
    {
        private Action<View> onOpenHandler;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return UIManager.Instance.CloseAll().ToCoroutine();
            yield return UIManager.Instance.InitializeAsync().ToCoroutine();
            Scenario.Reset();
            ClearProviderIfApiExists();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (onOpenHandler != null)
            {
                UIManager.Instance.OnOpen -= onOpenHandler;
                onOpenHandler = null;
            }

            yield return UIManager.Instance.CloseAll().ToCoroutine();
            ClearProviderIfApiExists();
            Scenario.Reset();
        }

        [UnityTest]
        public IEnumerator Push_StagesWorldAndUiInParallelAroundCommit_AndResolvesEachTargetOnce()
        {
            Scenario.Register<WorldPageA>();
            Scenario.Register<WorldPageB>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);
            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), _ => { });

            Scenario.Events.Clear();
            provider.ResetObservations();
            var pageA = UIManager.Instance.Get<WorldPageA>();
            var pageBTransition = Scenario.GetUITransition<WorldPageB>();
            var pageATransition = Scenario.GetUITransition<WorldPageA>();
            pageATransition.BlockExit = true;
            pageBTransition.BlockEnter = true;
            provider.BlockExitFor = typeof(WorldPageA);
            provider.BlockEnterFor = typeof(WorldPageB);
            var pageBOpenCount = 0;
            onOpenHandler = openedView =>
            {
                if (openedView is WorldPageB)
                {
                    pageBOpenCount++;
                    Scenario.Events.Add("WorldPageB.open");
                }
            };
            UIManager.Instance.OnOpen += onOpenHandler;

            var showTask = UIManager.Instance.ShowAsync<WorldPageB>().Preserve();
            yield return null;

            Assert.That(Scenario.Events[0], Is.EqualTo("WorldPageB.load"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.ui.exit.start"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.world.exit.start"));
            Assert.That(Scenario.Events, Does.Not.Contain("WorldPageB.ui.enter.start"));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(pageA));

            pageATransition.CompleteExit();
            yield return null;
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(pageA),
                "World Exit 未完成时不得提交新栈。");

            provider.GetResolved<WorldPageA>().CompleteExit();
            yield return null;
            Assert.That(UIManager.Instance.GetStackTopView(), Is.TypeOf<WorldPageB>());
            Assert.That(Scenario.Events, Does.Contain("WorldPageB.ui.enter.start"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageB.world.enter.start"));

            pageBTransition.CompleteEnter();
            yield return null;
            Assert.That(showTask.Status, Is.EqualTo(UniTaskStatus.Pending),
                "UI Enter 完成但 World Enter 未完成时操作不得结束。");
            provider.GetResolved<WorldPageB>().CompleteEnter();

            UIOperationResult result = default;
            yield return Await(showTask, value => result = value);
            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.EqualTo(1));
            Assert.That(provider.ResolveCount(typeof(WorldPageB)), Is.EqualTo(1));
            Assert.That(MaxIndex("WorldPageA.ui.exit.done", "WorldPageA.world.exit.done"),
                Is.LessThan(MinIndex("WorldPageB.ui.enter.start", "WorldPageB.world.enter.start")));
            Assert.That(pageBOpenCount, Is.EqualTo(1));
            Assert.That(MaxIndex("WorldPageB.ui.enter.done", "WorldPageB.world.enter.done"),
                Is.LessThan(Scenario.Events.IndexOf("WorldPageB.open")));
        }

        [UnityTest]
        public IEnumerator NullProviderAndNullResolve_UseEmptyWorldTransition()
        {
            Scenario.Register<WorldPageA>();
            RegisterProvider(null);
            Assert.That(ReadProvider(), Is.Null);

            UIOperationResult first = default;
            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), value => first = value);
            Assert.That(first.Status, Is.EqualTo(UIOperationStatus.Succeeded));

            Scenario.Register<WorldPageB>();
            var provider = new FakeWorldTransitionProvider { ReturnNull = true };
            RegisterProvider(provider);
            UIOperationResult second = default;
            yield return Await(UIManager.Instance.ShowAsync<WorldPageB>(), value => second = value);

            Assert.That(second.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.EqualTo(1));
            Assert.That(provider.ResolveCount(typeof(WorldPageB)), Is.EqualTo(1));
            Assert.That(Scenario.Events, Has.None.Contains(".world."));
        }

        [UnityTest]
        public IEnumerator AnimatedFalse_CompletesUiAndWorldImmediatelyInBothDirections()
        {
            Scenario.Register<WorldPageA>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);

            yield return Await(
                UIManager.Instance.ShowAsync<WorldPageA>(new UIShowOptions(false)),
                _ => { });
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.ui.complete.Enter"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.world.complete.Enter"));
            Assert.That(Scenario.Events, Does.Not.Contain("WorldPageA.world.enter.start"));

            Scenario.Events.Clear();
            provider.ResetObservations();
            yield return Await(UIManager.Instance.CloseAsync<WorldPageA>(false), _ => { });
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.ui.complete.Exit"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.world.complete.Exit"));
            Assert.That(Scenario.Events, Does.Not.Contain("WorldPageA.world.exit.start"));
        }

        [UnityTest]
        public IEnumerator WorldEnterFailure_RollsBackWorldEndpointsAndPreservesOriginalException()
        {
            Scenario.Register<WorldPageA>();
            Scenario.Register<WorldPageB>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);
            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), _ => { });

            Scenario.Events.Clear();
            provider.ResetObservations();
            provider.ThrowEnterFor = typeof(WorldPageB);
            UIOperationResult result = default;
            yield return Await(UIManager.Instance.ShowAsync<WorldPageB>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.SameAs(provider.EnterException));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.TypeOf<WorldPageA>());
            Assert.That(UIManager.Instance.Get<WorldPageA>().State, Is.EqualTo(ViewState.Visible));
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.world.complete.Enter"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageB.world.complete.Exit"));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.EqualTo(1));
            Assert.That(provider.ResolveCount(typeof(WorldPageB)), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CancellationDuringWorldEnter_RollsBackWithoutHangingOrResolvingAgain()
        {
            Scenario.Register<WorldPageA>();
            Scenario.Register<WorldPageB>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);
            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), _ => { });

            Scenario.Events.Clear();
            provider.ResetObservations();
            provider.BlockEnterFor = typeof(WorldPageB);
            using var cancellation = new CancellationTokenSource();
            var task = UIManager.Instance.ShowAsync<WorldPageB>(default, cancellation.Token).Preserve();
            yield return null;
            Assert.That(Scenario.Events, Does.Contain("WorldPageB.world.enter.start"));

            cancellation.Cancel();
            UIOperationResult result = default;
            yield return Await(task, value => result = value);
            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.TypeOf<WorldPageA>());
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.world.complete.Enter"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageB.world.complete.Exit"));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.EqualTo(1));
            Assert.That(provider.ResolveCount(typeof(WorldPageB)), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Close_ResolvesExitingAndRevealedViewsOnce()
        {
            Scenario.Register<WorldPageA>();
            Scenario.Register<WorldPageB>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);
            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), _ => { });
            yield return Await(UIManager.Instance.ShowAsync<WorldPageB>(), _ => { });

            Scenario.Events.Clear();
            provider.ResetObservations();
            UIOperationResult result = default;
            yield return Await(UIManager.Instance.CloseAsync<WorldPageB>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(provider.ResolveCount(typeof(WorldPageB)), Is.EqualTo(1));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.EqualTo(1));
            Assert.That(Scenario.Events, Does.Contain("WorldPageB.world.exit.start"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.world.enter.start"));
        }

        [UnityTest]
        public IEnumerator ProviderResolveFailure_PreservesExceptionAndDoesNotRetry()
        {
            Scenario.Register<WorldPageA>();
            Scenario.Register<WorldPageB>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);
            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), _ => { });

            provider.ResetObservations();
            provider.ThrowResolveFor = typeof(WorldPageB);
            UIOperationResult result = default;
            yield return Await(UIManager.Instance.ShowAsync<WorldPageB>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.SameAs(provider.ResolveException));
            Assert.That(provider.ResolveCount(typeof(WorldPageB)), Is.EqualTo(1));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.TypeOf<WorldPageA>());
        }

        [UnityTest]
        public IEnumerator Replace_ResolvesOldExitAndNewEnterExactlyOnce()
        {
            Scenario.Register<WorldPageA>();
            Scenario.Register<WorldPageB>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);
            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), _ => { });

            Scenario.Events.Clear();
            provider.ResetObservations();
            UIOperationResult result = default;
            yield return Await(UIManager.Instance.ReplaceAsync<WorldPageB>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.EqualTo(1));
            Assert.That(provider.ResolveCount(typeof(WorldPageB)), Is.EqualTo(1));
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.world.exit.start"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageB.world.enter.start"));
        }

        [UnityTest]
        public IEnumerator Back_ResolvesTopExitAndRevealedEnterExactlyOnce()
        {
            Scenario.Register<WorldPageA>();
            Scenario.Register<WorldPageB>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);
            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), _ => { });
            yield return Await(UIManager.Instance.ShowAsync<WorldPageB>(), _ => { });

            Scenario.Events.Clear();
            provider.ResetObservations();
            UIOperationResult result = default;
            yield return Await(UIManager.Instance.BackAsync(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(result.Action, Is.EqualTo(UINavigationAction.Back));
            Assert.That(provider.ResolveCount(typeof(WorldPageB)), Is.EqualTo(1));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.EqualTo(1));
            Assert.That(Scenario.Events, Does.Contain("WorldPageB.world.exit.start"));
            Assert.That(Scenario.Events, Does.Contain("WorldPageA.world.enter.start"));
        }

        [UnityTest]
        public IEnumerator ShowWithoutHidingPrevious_DoesNotResolveOrRunPreviousExit()
        {
            Scenario.Register<WorldPageA>();
            Scenario.Register<WorldPageB>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);
            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), _ => { });

            Scenario.Events.Clear();
            provider.ResetObservations();
            UIOperationResult result = default;
            yield return Await(
                UIManager.Instance.ShowAsync<WorldPageB>(
                    new UIShowOptions(animated: true, hidePrevious: false)),
                value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.Zero);
            Assert.That(provider.ResolveCount(typeof(WorldPageB)), Is.EqualTo(1));
            Assert.That(Scenario.Events, Does.Not.Contain("WorldPageA.world.exit.start"));
            Assert.That(UIManager.Instance.Get<WorldPageA>().State, Is.EqualTo(ViewState.Visible));
        }

        [UnityTest]
        public IEnumerator PreloadAndCloseAll_DoNotResolveWorldTransitions()
        {
            Scenario.Register<WorldPageA>();
            var provider = new FakeWorldTransitionProvider();
            RegisterProvider(provider);

            UIOperationResult preloadResult = default;
            yield return Await(UIManager.Instance.PreloadAsync<WorldPageA>(null),
                value => preloadResult = value);
            Assert.That(preloadResult.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.Zero,
                "Preload 不改变正式表现，不应解析 World Transition。");

            yield return Await(UIManager.Instance.ShowAsync<WorldPageA>(), _ => { });
            provider.ResetObservations();
            UIOperationResult closeAllResult = default;
            yield return Await(UIManager.Instance.CloseAllAsync(), value => closeAllResult = value);

            Assert.That(closeAllResult.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(provider.ResolveCount(typeof(WorldPageA)), Is.Zero,
                "CloseAll 当前直接销毁全部 View，没有 Enter/Exit 阶段，不应虚构 World Transition。");
        }

        [UnityTest]
        public IEnumerator WorldFailure_WaitsForCanceledUiCleanupAndPreservesPrimaryException()
        {
            Scenario.Register<WorldPageB>();
            var uiTransition = Scenario.GetUITransition<WorldPageB>();
            uiTransition.WaitForEnterCancellation = true;
            var provider = new FakeWorldTransitionProvider
            {
                ThrowEnterFor = typeof(WorldPageB)
            };
            RegisterProvider(provider);

            var task = UIManager.Instance.ShowAsync<WorldPageB>().Preserve();
            yield return null;

            var cleanupStartedBeforeRelease = uiTransition.EnterCleanupStarted;
            var operationCompletedBeforeCleanup = task.Status != UniTaskStatus.Pending;
            var rollbackStartedBeforeCleanup =
                Scenario.Events.Contains("WorldPageB.world.complete.Exit");

            uiTransition.CompleteEnterCleanup();
            UIOperationResult result = default;
            yield return Await(task, value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.SameAs(provider.EnterException));
            Assert.That(cleanupStartedBeforeRelease, Is.True);
            Assert.That(operationCompletedBeforeCleanup, Is.False,
                "World 失败后必须等待已取消的 UI 成员完成 finally 收口。");
            Assert.That(rollbackStartedBeforeCleanup, Is.False,
                "UI 成员尚未收口时不得提前回滚 World 终态。");
            Assert.That(uiTransition.EnterCleanupFinished, Is.True);
            Assert.That(Scenario.Events.IndexOf("WorldPageB.ui.enter.cleanup.done"),
                Is.LessThan(Scenario.Events.IndexOf("WorldPageB.world.complete.Exit")));
        }

        [UnityTest]
        public IEnumerator UiFailure_WaitsForCanceledWorldCleanupAndPreservesPrimaryException()
        {
            Scenario.Register<WorldPageB>();
            var uiTransition = Scenario.GetUITransition<WorldPageB>();
            uiTransition.BlockEnter = true;
            uiTransition.EnterException = new InvalidOperationException("ui enter failed");
            var provider = new FakeWorldTransitionProvider
            {
                WaitForEnterCancellationFor = typeof(WorldPageB)
            };
            RegisterProvider(provider);

            var task = UIManager.Instance.ShowAsync<WorldPageB>().Preserve();
            yield return null;
            var worldTransition = provider.GetResolved<WorldPageB>();
            Assert.That(Scenario.Events, Does.Contain("WorldPageB.world.enter.start"));

            uiTransition.CompleteEnter();
            yield return null;
            var cleanupStartedBeforeRelease = worldTransition.EnterCleanupStarted;
            var operationCompletedBeforeCleanup = task.Status != UniTaskStatus.Pending;
            var rollbackStartedBeforeCleanup =
                Scenario.Events.Contains("WorldPageB.world.complete.Exit");

            worldTransition.CompleteEnterCleanup();
            UIOperationResult result = default;
            yield return Await(task, value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.SameAs(uiTransition.EnterException));
            Assert.That(cleanupStartedBeforeRelease, Is.True);
            Assert.That(operationCompletedBeforeCleanup, Is.False,
                "UI 失败后必须等待已取消的 World 成员完成 finally 收口。");
            Assert.That(rollbackStartedBeforeCleanup, Is.False);
            Assert.That(worldTransition.EnterCleanupFinished, Is.True);
            Assert.That(Scenario.Events.IndexOf("WorldPageB.world.enter.cleanup.done"),
                Is.LessThan(Scenario.Events.IndexOf("WorldPageB.world.complete.Exit")));
        }

        private static int MinIndex(string first, string second)
        {
            return Math.Min(Scenario.Events.IndexOf(first), Scenario.Events.IndexOf(second));
        }

        private static int MaxIndex(string first, string second)
        {
            return Math.Max(Scenario.Events.IndexOf(first), Scenario.Events.IndexOf(second));
        }

        private static IEnumerator Await(UniTask<UIOperationResult> task, Action<UIOperationResult> receive)
        {
            yield return task.ToCoroutine(receive);
        }

        private static void RegisterProvider(IUIWorldTransitionProvider provider)
        {
            UIManager.Instance.RegisterWorldTransitionProvider(provider);
        }

        private static IUIWorldTransitionProvider ReadProvider()
        {
            return UIManager.Instance.WorldTransitionProvider;
        }

        private static void ClearProviderIfApiExists()
        {
            UIManager.Instance.RegisterWorldTransitionProvider(null);
        }

        private sealed class WorldPageA : WorldTestPage { }
        private sealed class WorldPageB : WorldTestPage { }

        private abstract class WorldTestPage : View
        {
            private readonly TestLoader loader;

            protected WorldTestPage()
            {
                loader = Scenario.Take(GetType());
                Loader = loader;
            }

            public override string Address => GetType().Name;
            protected override IUITransition CreateUITransition() => loader.Transition;
        }

        private static class Scenario
        {
            private static readonly Dictionary<Type, Queue<TestLoader>> Loaders = new();
            private static readonly List<TestLoader> OwnedLoaders = new();
            internal static readonly List<string> Events = new();

            internal static void Register<T>() where T : View
            {
                var loader = new TestLoader(typeof(T));
                OwnedLoaders.Add(loader);
                if (!Loaders.TryGetValue(typeof(T), out var queue))
                {
                    queue = new Queue<TestLoader>();
                    Loaders.Add(typeof(T), queue);
                }

                queue.Enqueue(loader);
            }

            internal static TestLoader Take(Type type) => Loaders[type].Dequeue();

            internal static ControlledUITransition GetUITransition<T>() where T : View
            {
                foreach (var loader in OwnedLoaders)
                {
                    if (loader.ViewType == typeof(T))
                    {
                        return loader.Transition;
                    }
                }

                throw new InvalidOperationException($"未注册测试 Loader: {typeof(T)}");
            }

            internal static void Reset()
            {
                foreach (var loader in OwnedLoaders)
                {
                    loader.Dispose();
                }

                OwnedLoaders.Clear();
                Loaders.Clear();
                Events.Clear();
            }
        }

        private sealed class TestLoader : IResourceLoader
        {
            private GameObject instance;
            private bool released;

            internal TestLoader(Type viewType)
            {
                ViewType = viewType;
                Transition = new ControlledUITransition(viewType);
            }

            internal Type ViewType { get; }
            internal ControlledUITransition Transition { get; }

            public GameObject Instantiate(string address, Transform parent) =>
                Instantiate(address, parent, false);

            public GameObject Instantiate(string address, Transform parent, bool worldPositionStays)
            {
                instance = new GameObject(ViewType.Name, typeof(RectTransform));
                instance.transform.SetParent(parent, worldPositionStays);
                return instance;
            }

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent) =>
                InstantiateAsync(address, parent, false);

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool worldPositionStays)
            {
                Scenario.Events.Add($"{ViewType.Name}.load");
                return UniTask.FromResult(Instantiate(address, parent, worldPositionStays));
            }

            public T LoadAsset<T>(string address) where T : Object => null;
            public UniTask<T> LoadAssetAsync<T>(string address) where T : Object => UniTask.FromResult<T>(null);
            public void ReleaseAsset(Object asset) { }

            public void ReleaseInstance(GameObject target)
            {
                released = true;
                if (target != null)
                {
                    Object.Destroy(target);
                }
            }

            public void Dispose()
            {
                if (!released && instance != null)
                {
                    Object.Destroy(instance);
                }
            }
        }

        private sealed class ControlledUITransition : IUITransition
        {
            private readonly Type viewType;
            private UniTaskCompletionSource enterCompletion;
            private UniTaskCompletionSource exitCompletion;
            private UniTaskCompletionSource enterCleanupCompletion;

            internal ControlledUITransition(Type viewType)
            {
                this.viewType = viewType;
            }

            internal bool BlockEnter { get; set; }
            internal bool BlockExit { get; set; }
            internal bool WaitForEnterCancellation { get; set; }
            internal bool EnterCleanupStarted { get; private set; }
            internal bool EnterCleanupFinished { get; private set; }
            internal Exception EnterException { get; set; }
            public void Initialize(Transform root) { }

            public async UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                Scenario.Events.Add($"{viewType.Name}.ui.enter.start");
                try
                {
                    if (WaitForEnterCancellation)
                    {
                        enterCompletion = new UniTaskCompletionSource();
                        await enterCompletion.Task.AttachExternalCancellation(cancellationToken);
                    }
                    else if (BlockEnter)
                    {
                        enterCompletion = new UniTaskCompletionSource();
                        await enterCompletion.Task.AttachExternalCancellation(cancellationToken);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    if (EnterException != null)
                    {
                        throw EnterException;
                    }

                    Scenario.Events.Add($"{viewType.Name}.ui.enter.done");
                }
                finally
                {
                    if (WaitForEnterCancellation)
                    {
                        EnterCleanupStarted = true;
                        Scenario.Events.Add($"{viewType.Name}.ui.enter.cleanup.start");
                        enterCleanupCompletion = new UniTaskCompletionSource();
                        await enterCleanupCompletion.Task;
                        EnterCleanupFinished = true;
                        Scenario.Events.Add($"{viewType.Name}.ui.enter.cleanup.done");
                    }
                }
            }

            public async UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                Scenario.Events.Add($"{viewType.Name}.ui.exit.start");
                if (BlockExit)
                {
                    exitCompletion = new UniTaskCompletionSource();
                    await exitCompletion.Task.AttachExternalCancellation(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                Scenario.Events.Add($"{viewType.Name}.ui.exit.done");
            }

            public void CompleteImmediately(UITransitionDirection direction)
            {
                Scenario.Events.Add($"{viewType.Name}.ui.complete.{direction}");
            }

            internal void CompleteEnter() => enterCompletion.TrySetResult();
            internal void CompleteEnterCleanup() => enterCleanupCompletion?.TrySetResult();
            internal void CompleteExit() => exitCompletion.TrySetResult();
            public void Dispose() { }
        }

        private sealed class FakeWorldTransitionProvider : IUIWorldTransitionProvider
        {
            private readonly Dictionary<Type, int> resolveCounts = new();
            private readonly Dictionary<Type, ControlledWorldTransition> resolved = new();

            internal Type BlockEnterFor { get; set; }
            internal Type BlockExitFor { get; set; }
            internal Type ThrowEnterFor { get; set; }
            internal Type ThrowResolveFor { get; set; }
            internal Type WaitForEnterCancellationFor { get; set; }
            internal bool ReturnNull { get; set; }
            internal Exception EnterException { get; } = new InvalidOperationException("world enter failed");
            internal Exception ResolveException { get; } = new InvalidOperationException("world resolve failed");

            public IUIWorldTransition Resolve(View view)
            {
                var type = view.GetType();
                resolveCounts[type] = ResolveCount(type) + 1;
                if (type == ThrowResolveFor)
                {
                    throw ResolveException;
                }

                if (ReturnNull)
                {
                    return null;
                }

                var transition = new ControlledWorldTransition(
                    type,
                    type == BlockEnterFor,
                    type == BlockExitFor,
                    type == ThrowEnterFor ? EnterException : null,
                    type == WaitForEnterCancellationFor);
                resolved[type] = transition;
                return transition;
            }

            internal int ResolveCount(Type type) => resolveCounts.TryGetValue(type, out var count) ? count : 0;
            internal ControlledWorldTransition GetResolved<T>() where T : View => resolved[typeof(T)];

            internal void ResetObservations()
            {
                resolveCounts.Clear();
                resolved.Clear();
            }
        }

        private sealed class ControlledWorldTransition : IUIWorldTransition
        {
            private readonly Type viewType;
            private readonly bool blockEnter;
            private readonly bool blockExit;
            private readonly Exception enterException;
            private readonly bool waitForEnterCancellation;
            private UniTaskCompletionSource enterCompletion;
            private UniTaskCompletionSource exitCompletion;
            private UniTaskCompletionSource enterCleanupCompletion;

            internal ControlledWorldTransition(
                Type viewType,
                bool blockEnter,
                bool blockExit,
                Exception enterException,
                bool waitForEnterCancellation)
            {
                this.viewType = viewType;
                this.blockEnter = blockEnter;
                this.blockExit = blockExit;
                this.enterException = enterException;
                this.waitForEnterCancellation = waitForEnterCancellation;
            }

            internal bool EnterCleanupStarted { get; private set; }
            internal bool EnterCleanupFinished { get; private set; }

            public async UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                Scenario.Events.Add($"{viewType.Name}.world.enter.start");
                try
                {
                    if (enterException != null)
                    {
                        throw enterException;
                    }

                    if (waitForEnterCancellation || blockEnter)
                    {
                        enterCompletion = new UniTaskCompletionSource();
                        await enterCompletion.Task.AttachExternalCancellation(cancellationToken);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    Scenario.Events.Add($"{viewType.Name}.world.enter.done");
                }
                finally
                {
                    if (waitForEnterCancellation)
                    {
                        EnterCleanupStarted = true;
                        Scenario.Events.Add($"{viewType.Name}.world.enter.cleanup.start");
                        enterCleanupCompletion = new UniTaskCompletionSource();
                        await enterCleanupCompletion.Task;
                        EnterCleanupFinished = true;
                        Scenario.Events.Add($"{viewType.Name}.world.enter.cleanup.done");
                    }
                }
            }

            public async UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                Scenario.Events.Add($"{viewType.Name}.world.exit.start");
                if (blockExit)
                {
                    exitCompletion = new UniTaskCompletionSource();
                    await exitCompletion.Task.AttachExternalCancellation(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                Scenario.Events.Add($"{viewType.Name}.world.exit.done");
            }

            public void CompleteImmediately(UITransitionDirection direction)
            {
                Scenario.Events.Add($"{viewType.Name}.world.complete.{direction}");
            }

            internal void CompleteEnter() => enterCompletion.TrySetResult();
            internal void CompleteEnterCleanup() => enterCleanupCompletion?.TrySetResult();
            internal void CompleteExit() => exitCompletion.TrySetResult();
        }
    }
}
