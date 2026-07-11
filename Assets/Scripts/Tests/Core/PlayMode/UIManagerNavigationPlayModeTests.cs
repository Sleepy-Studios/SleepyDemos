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
    public sealed class UIManagerNavigationPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return UIManager.Instance.CloseAll().ToCoroutine();
            yield return UIManager.Instance.InitializeAsync().ToCoroutine();
            TestViewRegistry.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return UIManager.Instance.CloseAll().ToCoroutine();
            TestViewRegistry.Reset();
        }

        [UnityTest]
        public IEnumerator CloseDuringLoad_DoesNotShowGhostView()
        {
            var loader = TestViewRegistry.Register<SlowPage>(delay: true);
            var showTask = UIManager.Instance.ShowAsync<SlowPage>();
            yield return null;

            var closeTask = UIManager.Instance.CloseAsync<SlowPage>();
            loader.Complete(new GameObject(nameof(SlowPage)));

            UIOperationResult showResult = default;
            UIOperationResult closeResult = default;
            yield return AwaitResult(showTask, result => showResult = result);
            yield return AwaitResult(closeTask, result => closeResult = result);

            Assert.That(showResult.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(closeResult.Status,
                Is.EqualTo(UIOperationStatus.Succeeded).Or.EqualTo(UIOperationStatus.Canceled));
            Assert.That(UIManager.Instance.Get<SlowPage>(), Is.Null);
            Assert.That(UIManager.Instance.StackCount, Is.Zero);
            Assert.That(loader.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DifferentPages_ShowInStrictFifoOrder()
        {
            var events = new List<string>();
            var firstLoader = TestViewRegistry.Register<FirstPage>(events, true);
            var secondLoader = TestViewRegistry.Register<SecondPage>(events, true);

            var firstTask = UIManager.Instance.ShowAsync<FirstPage>();
            var secondTask = UIManager.Instance.ShowAsync<SecondPage>();
            yield return null;
            Assert.That(events, Is.EqualTo(new[] { "FirstPage.load" }));

            firstLoader.Complete(new GameObject(nameof(FirstPage)));
            yield return null;
            yield return null;
            Assert.That(events, Does.Contain("FirstPage.enter"));
            Assert.That(events, Does.Contain("SecondPage.load"));
            Assert.That(events.IndexOf("FirstPage.enter"), Is.LessThan(events.IndexOf("SecondPage.load")));

            secondLoader.Complete(new GameObject(nameof(SecondPage)));
            yield return AwaitResult(firstTask, _ => { });
            yield return AwaitResult(secondTask, _ => { });
        }

        [UnityTest]
        public IEnumerator NewPageLoadFailure_RestoresOldPageAndStack()
        {
            TestViewRegistry.Register<FirstPage>();
            UIOperationResult firstResult = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), result => firstResult = result);
            var oldPage = UIManager.Instance.Get<FirstPage>();

            TestViewRegistry.Register<NullPage>(returnNull: true);
            UIOperationResult failedResult = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<NullPage>(), result => failedResult = result);

            Assert.That(firstResult.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(failedResult.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(oldPage.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(oldPage));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
            Assert.That(UIManager.Instance.Get<NullPage>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator DuplicateShow_StableSingleInstanceReturnsIgnored()
        {
            TestViewRegistry.Register<FirstPage>();
            UIOperationResult firstResult = default;
            UIOperationResult duplicateResult = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), result => firstResult = result);
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), result => duplicateResult = result);

            Assert.That(firstResult.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(duplicateResult.Status, Is.EqualTo(UIOperationStatus.Ignored));
            Assert.That(firstResult.View, Is.SameAs(duplicateResult.View));
            Assert.That(TestViewRegistry.Events, Is.EqualTo(new[] { "FirstPage.load", "FirstPage.enter" }));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReplaceSuccess_DestroysOldPage()
        {
            var oldLoader = TestViewRegistry.Register<FirstPage>();
            TestViewRegistry.Register<SecondPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ReplaceAsync<SecondPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(UIManager.Instance.Get<FirstPage>(), Is.Null);
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(result.View));
            Assert.That(oldLoader.ReleaseCount, Is.EqualTo(1));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReplaceFailure_RestoresOldPage()
        {
            TestViewRegistry.Register<FirstPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            var oldPage = UIManager.Instance.Get<FirstPage>();
            TestViewRegistry.Register<NullPage>(returnNull: true);

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ReplaceAsync<NullPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(oldPage.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(oldPage));
            Assert.That(UIManager.Instance.Get<NullPage>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator Back_ClosesModalBeforePageAndRevealsPage()
        {
            TestViewRegistry.Register<FirstPage>();
            TestViewRegistry.Register<TestModal>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            yield return AwaitResult(UIManager.Instance.ShowAsync<TestModal>(), _ => { });
            var page = UIManager.Instance.Get<FirstPage>();

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.BackAsync(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(result.View, Is.TypeOf<TestModal>());
            Assert.That(UIManager.Instance.Get<TestModal>(), Is.Null);
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(page));
            Assert.That(page.State, Is.EqualTo(ViewState.Visible));
        }

        [UnityTest]
        public IEnumerator PendingCancellation_HasNoSideEffects()
        {
            var firstLoader = TestViewRegistry.Register<FirstPage>(delay: true);
            TestViewRegistry.Register<SecondPage>();
            var firstTask = UIManager.Instance.ShowAsync<FirstPage>();
            using var cancellation = new CancellationTokenSource();
            var pendingTask = UIManager.Instance.ShowAsync<SecondPage>(cancellationToken: cancellation.Token);
            cancellation.Cancel();
            firstLoader.Complete(new GameObject(nameof(FirstPage)));

            UIOperationResult pendingResult = default;
            yield return AwaitResult(firstTask, _ => { });
            yield return AwaitResult(pendingTask, value => pendingResult = value);

            Assert.That(pendingResult.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(TestViewRegistry.Events, Does.Not.Contain("SecondPage.load"));
            Assert.That(UIManager.Instance.Get<SecondPage>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator CurrentCancellation_ReturnsCanceledAndCleansView()
        {
            var loader = TestViewRegistry.Register<SlowPage>(delay: true);
            using var cancellation = new CancellationTokenSource();
            var task = UIManager.Instance.ShowAsync<SlowPage>(cancellationToken: cancellation.Token);
            yield return null;
            cancellation.Cancel();
            loader.Complete(new GameObject(nameof(SlowPage)));

            UIOperationResult result = default;
            yield return AwaitResult(task, value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(UIManager.Instance.Get<SlowPage>(), Is.Null);
            Assert.That(UIManager.Instance.StackCount, Is.Zero);
            Assert.That(loader.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CloseAll_CancelsCurrentAndPendingThenCleansEverythingOnce()
        {
            var loader = TestViewRegistry.Register<SlowPage>(delay: true);
            TestViewRegistry.Register<SecondPage>();
            var currentTask = UIManager.Instance.ShowAsync<SlowPage>();
            var pendingTask = UIManager.Instance.ShowAsync<SecondPage>();
            yield return null;
            var closeAllTask = UIManager.Instance.CloseAllAsync();
            loader.Complete(new GameObject(nameof(SlowPage)));

            UIOperationResult current = default;
            UIOperationResult pending = default;
            yield return AwaitResult(currentTask, value => current = value);
            yield return AwaitResult(pendingTask, value => pending = value);
            yield return AwaitResult(closeAllTask, _ => { });

            Assert.That(current.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(pending.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(UIManager.Instance.StackCount, Is.Zero);
            Assert.That(UIManager.Instance.cacheStack.GetAllViews(), Is.Empty);
            Assert.That(loader.ReleaseCount, Is.EqualTo(1));
            Assert.That(TestViewRegistry.Events, Does.Not.Contain("SecondPage.load"));
        }

        [UnityTest]
        public IEnumerator EnterException_RollsBackAndRemovesFaultedView()
        {
            TestViewRegistry.Register<FirstPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            var oldPage = UIManager.Instance.Get<FirstPage>();
            TestViewRegistry.Register<SecondPage>(throwEnter: true);

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<SecondPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(UIManager.Instance.Get<SecondPage>(), Is.Null);
            Assert.That(oldPage.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(oldPage));
        }

        [UnityTest]
        public IEnumerator ExitException_RestoresOldPageAndReturnsFailed()
        {
            TestViewRegistry.Register<FirstPage>(throwExit: true);
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            var oldPage = UIManager.Instance.Get<FirstPage>();
            TestViewRegistry.Register<SecondPage>();

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<SecondPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.TypeOf<InvalidOperationException>());
            Assert.That(oldPage.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(oldPage));
            Assert.That(UIManager.Instance.Get<SecondPage>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator LegacyShow_InvokesBeginOpenOnce()
        {
            TestViewRegistry.Register<FirstPage>();
            var beginCount = 0;
            void CountBegin(View _) => beginCount++;
            UIManager.Instance.OnBeginOpen += CountBegin;

            UIManager.Instance.Show<FirstPage>();
            for (int i = 0; i < 10 && UIManager.Instance.Get<FirstPage>()?.State != ViewState.Visible; i++)
            {
                yield return null;
            }

            UIManager.Instance.OnBeginOpen -= CountBegin;
            Assert.That(beginCount, Is.EqualTo(1));
        }

        private static IEnumerator AwaitResult(UniTask<UIOperationResult> task, Action<UIOperationResult> receive)
        {
            yield return task.ToCoroutine(receive);
        }

        private abstract class NavigationTestPage : View
        {
            private readonly TestLoader loader;
            private readonly List<string> events;

            protected NavigationTestPage()
            {
                (loader, events) = TestViewRegistry.Take(GetType());
                Loader = loader;
            }

            public override string Address => GetType().Name;
            public override bool DestroyOnHide => true;

            protected override void OnShow()
            {
                events.Add($"{GetType().Name}.enter");
            }

            protected override IUITransition CreateUITransition() => loader.Transition;
        }

        private sealed class SlowPage : NavigationTestPage { }
        private sealed class FirstPage : NavigationTestPage { }
        private sealed class SecondPage : NavigationTestPage { }
        private sealed class NullPage : NavigationTestPage { }
        private sealed class TestModal : NavigationTestPage
        {
            public override UILayer Level => UILayer.Pop;
        }

        private static class TestViewRegistry
        {
            private static readonly Dictionary<Type, Queue<(TestLoader, List<string>)>> Entries = new();
            internal static readonly List<string> Events = new();

            internal static TestLoader Register<T>(
                List<string> events = null,
                bool delay = false,
                GameObject result = null,
                bool returnNull = false,
                bool throwEnter = false,
                bool throwExit = false)
                where T : View
            {
                events ??= Events;
                var loader = new TestLoader(
                    events,
                    typeof(T).Name,
                    delay,
                    returnNull ? null : result ?? (delay ? null : new GameObject(typeof(T).Name)),
                    throwEnter,
                    throwExit);
                if (!Entries.TryGetValue(typeof(T), out var queue))
                {
                    queue = new Queue<(TestLoader, List<string>)>();
                    Entries.Add(typeof(T), queue);
                }

                queue.Enqueue((loader, events));
                return loader;
            }

            internal static (TestLoader, List<string>) Take(Type type)
            {
                return Entries[type].Dequeue();
            }

            internal static void Reset()
            {
                Entries.Clear();
                Events.Clear();
            }
        }

        private sealed class TestLoader : IResourceLoader
        {
            private readonly List<string> events;
            private readonly string name;
            private readonly bool delay;
            private readonly GameObject result;
            private UniTaskCompletionSource<GameObject> completionSource;

            internal TestLoader(
                List<string> events,
                string name,
                bool delay,
                GameObject result,
                bool throwEnter,
                bool throwExit)
            {
                this.events = events;
                this.name = name;
                this.delay = delay;
                this.result = result;
                Transition = new TestTransition(throwEnter, throwExit);
            }

            internal int ReleaseCount { get; private set; }
            internal IUITransition Transition { get; }

            public GameObject Instantiate(string address, Transform parent) => Instantiate(address, parent, false);
            public GameObject Instantiate(string address, Transform parent, bool worldPositionStays) => result;

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent) =>
                InstantiateAsync(address, parent, false);

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool worldPositionStays)
            {
                events.Add($"{name}.load");
                if (!delay)
                {
                    if (result != null)
                    {
                        result.transform.SetParent(parent, worldPositionStays);
                    }

                    return UniTask.FromResult(result);
                }

                completionSource = new UniTaskCompletionSource<GameObject>();
                return AwaitAndParent(completionSource.Task, parent, worldPositionStays);
            }

            internal void Complete(GameObject instance) => completionSource.TrySetResult(instance);

            public T LoadAsset<T>(string address) where T : Object => null;
            public UniTask<T> LoadAssetAsync<T>(string address) where T : Object => UniTask.FromResult<T>(null);
            public void ReleaseAsset(Object asset) { }

            public void ReleaseInstance(GameObject instance)
            {
                ReleaseCount++;
                if (instance != null)
                {
                    Object.Destroy(instance);
                }
            }

            public void Dispose() { }

            private static async UniTask<GameObject> AwaitAndParent(
                UniTask<GameObject> task,
                Transform parent,
                bool worldPositionStays)
            {
                var instance = await task;
                if (instance != null)
                {
                    instance.transform.SetParent(parent, worldPositionStays);
                }

                return instance;
            }
        }

        private sealed class TestTransition : IUITransition
        {
            private readonly bool throwEnter;
            private readonly bool throwExit;

            internal TestTransition(bool throwEnter, bool throwExit)
            {
                this.throwEnter = throwEnter;
                this.throwExit = throwExit;
            }

            public void Initialize(Transform root) { }

            public UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return throwEnter
                    ? UniTask.FromException(new InvalidOperationException("enter failed"))
                    : UniTask.CompletedTask;
            }

            public UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return throwExit
                    ? UniTask.FromException(new InvalidOperationException("exit failed"))
                    : UniTask.CompletedTask;
            }

            public void CompleteImmediately(UITransitionDirection direction) { }
            public void Dispose() { }
        }
    }
}
