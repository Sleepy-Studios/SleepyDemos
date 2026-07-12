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

namespace Core.Tests.UI
{
    public sealed class UIWorldTransitionPlayModeTests
    {
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

            internal ControlledUITransition(Type viewType)
            {
                this.viewType = viewType;
            }

            internal bool BlockEnter { get; set; }
            internal bool BlockExit { get; set; }
            public void Initialize(Transform root) { }

            public async UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                Scenario.Events.Add($"{viewType.Name}.ui.enter.start");
                if (BlockEnter)
                {
                    enterCompletion = new UniTaskCompletionSource();
                    await enterCompletion.Task.AttachExternalCancellation(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                Scenario.Events.Add($"{viewType.Name}.ui.enter.done");
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
                    type == ThrowEnterFor ? EnterException : null);
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
            private UniTaskCompletionSource enterCompletion;
            private UniTaskCompletionSource exitCompletion;

            internal ControlledWorldTransition(
                Type viewType,
                bool blockEnter,
                bool blockExit,
                Exception enterException)
            {
                this.viewType = viewType;
                this.blockEnter = blockEnter;
                this.blockExit = blockExit;
                this.enterException = enterException;
            }

            public async UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                Scenario.Events.Add($"{viewType.Name}.world.enter.start");
                if (enterException != null)
                {
                    throw enterException;
                }

                if (blockEnter)
                {
                    enterCompletion = new UniTaskCompletionSource();
                    await enterCompletion.Task.AttachExternalCancellation(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                Scenario.Events.Add($"{viewType.Name}.world.enter.done");
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
            internal void CompleteExit() => exitCompletion.TrySetResult();
        }
    }
}
