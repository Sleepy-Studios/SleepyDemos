using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Core.Tests.UI
{
    public sealed class UIViewLifecyclePlayModeTests
    {
        private readonly List<GameObject> createdObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var createdObject in createdObjects)
            {
                if (createdObject != null)
                {
                    Object.Destroy(createdObject);
                }
            }

            createdObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LoadAsync_WhenResourceLoads_InitializesOnceAndKeepsRootHidden()
        {
            var events = new List<string>();
            var root = CreateObject("LoadedView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { AsyncResult = root };
            var transition = new FakeTransition(events);
            var view = new FakeView(loader, transition, events);

            bool loaded = false;
            yield return CaptureResult(InvokeLoadAsync(view, parent.transform, CancellationToken.None),
                result => loaded = result).ToCoroutine();

            Assert.That(loaded, Is.True);
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(root.activeSelf, Is.False);
            Assert.That(loader.InstantiateAsyncCount, Is.EqualTo(1));
            Assert.That(transition.InitializeCount, Is.EqualTo(1));
            Assert.That(GetUITransition(view), Is.SameAs(transition));
            Assert.That(events, Is.EqualTo(new[]
            {
                "View.Before:Loading",
                "View.Component:LoadedHidden",
                "Transition.Initialize",
                "View.GameObject:LoadedHidden"
            }));
        }

        [UnityTest]
        public IEnumerator LoadAsync_WhenResourceIsNull_ReturnsFalseAndDisposesLoader()
        {
            var loader = new FakeResourceLoader { AsyncResult = null };
            var view = new FakeView(loader, new FakeTransition(new List<string>()), new List<string>());
            var parent = CreateObject("ViewParent");
            bool loaded = true;

            yield return CaptureResult(InvokeLoadAsync(view, parent.transform, CancellationToken.None),
                result => loaded = result).ToCoroutine();
            Assert.That(view.State, Is.EqualTo(ViewState.Faulted));
            yield return InvokeDestroyAsync(view).ToCoroutine();

            Assert.That(loaded, Is.False);
            Assert.That(view.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(0));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DestroyAsync_WhileLoading_WaitsAndReleasesLateInstanceOnce()
        {
            var events = new List<string>();
            var loader = new FakeResourceLoader { DelayAsyncResult = true };
            var transition = new FakeTransition(events);
            var view = new FakeView(loader, transition, events);
            var parent = CreateObject("ViewParent");
            var lateRoot = CreateObject("LateView");

            var loadTask = InvokeLoadAsync(view, parent.transform, CancellationToken.None).Preserve();
            var destroyTask = InvokeDestroyAsync(view).Preserve();
            Assert.That(view.State, Is.EqualTo(ViewState.Destroying));

            loader.CompleteAsync(lateRoot);
            yield return loadTask.SuppressCancellationThrow().ToCoroutine();
            yield return destroyTask.ToCoroutine();

            Assert.That(view.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
            Assert.That(transition.InitializeCount, Is.EqualTo(0));
            Assert.That(transition.DisposeCount, Is.EqualTo(0));
            Assert.That(view.gameObject, Is.Null);
            Assert.That(view.transform, Is.Null);
        }

        [UnityTest]
        public IEnumerator EnterExitDestroy_UseStableTransitionAndExpectedStates()
        {
            var events = new List<string>();
            var root = CreateObject("TransitionView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { AsyncResult = root };
            var transition = new FakeTransition(events);
            var view = new FakeView(loader, transition, events);
            yield return InvokeLoadAsync(view, parent.transform, CancellationToken.None).ToCoroutine();

            var cachedTransition = GetUITransition(view);
            var enterContext = new UITransitionContext(1, UINavigationAction.Push, view, null, true);
            yield return InvokeTransitionAsync("EnterAsync", view, enterContext, CancellationToken.None).ToCoroutine();
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            Assert.That(root.activeSelf, Is.True);

            var exitContext = new UITransitionContext(2, UINavigationAction.Close, null, view, true);
            yield return InvokeTransitionAsync("ExitAsync", view, exitContext, CancellationToken.None).ToCoroutine();
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(root.activeSelf, Is.False);
            Assert.That(GetUITransition(view), Is.SameAs(cachedTransition));

            yield return InvokeDestroyAsync(view).ToCoroutine();

            Assert.That(transition.InitializeCount, Is.EqualTo(1));
            Assert.That(transition.EnterCount, Is.EqualTo(1));
            Assert.That(transition.ExitCount, Is.EqualTo(1));
            Assert.That(transition.DisposeCount, Is.EqualTo(1));
            Assert.That(events, Does.Contain("View.Show:Entering"));
            Assert.That(events, Does.Contain("Transition.Enter"));
            Assert.That(events, Does.Contain("Transition.Exit"));
            Assert.That(events, Does.Contain("View.Hide:Exiting"));
            Assert.That(events, Does.Contain("View.Destroy:Destroying"));
        }

        [UnityTest]
        public IEnumerator EnterAsync_WhenTransitionThrows_SetsFaultedAndRethrows()
        {
            var events = new List<string>();
            var root = CreateObject("EnterFailureView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { AsyncResult = root };
            var transition = new FakeTransition(events);
            var view = new FakeView(loader, transition, events);
            yield return view.LoadAsync(parent.transform, CancellationToken.None).ToCoroutine();
            var expected = new InvalidOperationException("Enter failed");
            transition.EnterException = expected;

            Exception actual = null;
            var context = new UITransitionContext(3, UINavigationAction.Push, view, null, true);
            yield return CaptureResult(
                CaptureException(InvokeTransitionAsync("EnterAsync", view, context, CancellationToken.None)),
                result => actual = result).ToCoroutine();

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(view.State, Is.EqualTo(ViewState.Faulted));
            yield return view.DestroyAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator ExitAsync_WhenTransitionThrows_SetsFaultedAndRethrows()
        {
            var events = new List<string>();
            var root = CreateObject("ExitFailureView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { AsyncResult = root };
            var transition = new FakeTransition(events);
            var view = new FakeView(loader, transition, events);
            yield return view.LoadAsync(parent.transform, CancellationToken.None).ToCoroutine();
            var enterContext = new UITransitionContext(4, UINavigationAction.Push, view, null, false);
            yield return InvokeTransitionAsync("EnterAsync", view, enterContext, CancellationToken.None).ToCoroutine();
            var expected = new InvalidOperationException("Exit failed");
            transition.ExitException = expected;

            Exception actual = null;
            var exitContext = new UITransitionContext(5, UINavigationAction.Close, null, view, true);
            yield return CaptureResult(
                CaptureException(InvokeTransitionAsync("ExitAsync", view, exitContext, CancellationToken.None)),
                result => actual = result).ToCoroutine();

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(view.State, Is.EqualTo(ViewState.Faulted));
            yield return view.DestroyAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LoadAsync_WhenInitializationThrows_CleansRegisteredResourcesBeforeCacheReplacement()
        {
            var cache = new UICache();
            var view = cache.GetOrCreateView<InitializationFailureView>();
            var root = CreateObject("InitializationFailureView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { AsyncResult = root };
            var events = new List<string>();
            var transition = new FakeTransition(events);
            var binding = new CountingBinding();
            var subView = new CacheTestView();
            var expected = new InvalidOperationException("Transition initialize failed");
            transition.InitializeException = expected;
            view.Configure(loader, transition, binding, subView);

            Exception actual = null;
            yield return CaptureResult(
                CaptureException(view.LoadAsync(parent.transform, CancellationToken.None)),
                result => actual = result).ToCoroutine();

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(view.State, Is.EqualTo(ViewState.Faulted));
            Assert.That(binding.DisposeCount, Is.EqualTo(1));
            Assert.That(subView.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(transition.InitializeCount, Is.EqualTo(1));
            Assert.That(transition.DisposeCount, Is.EqualTo(1));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));

            var replacement = cache.GetOrCreateView<InitializationFailureView>();
            Assert.That(replacement, Is.Not.SameAs(view));
            yield return view.DestroyAsync().ToCoroutine();
            Assert.That(binding.DisposeCount, Is.EqualTo(1));
            Assert.That(transition.DisposeCount, Is.EqualTo(1));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DestroyAsync_WhenCalledConcurrently_OverlapsAndReleasesOwnedResourcesOnce()
        {
            var events = new List<string>();
            var root = CreateObject("ParentDestroyView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { AsyncResult = root };
            var transition = new FakeTransition(events);
            var view = new FakeView(loader, transition, events);
            yield return view.LoadAsync(parent.transform, CancellationToken.None).ToCoroutine();

            var childEvents = new List<string>();
            var childLoader = new FakeResourceLoader { DelayAsyncResult = true };
            var childTransition = new FakeTransition(childEvents);
            var childView = new FakeView(childLoader, childTransition, childEvents);
            var childRoot = CreateObject("DelayedChildView");
            var childLoad = childView.LoadAsync(parent.transform, CancellationToken.None);
            view.AddSubView(childView);

            var first = view.DestroyAsync();
            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Pending));
            var second = view.DestroyAsync();
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));

            childLoader.CompleteAsync(childRoot);
            yield return childLoad.ToCoroutine();
            yield return UniTask.WhenAll(first, second).ToCoroutine();
            yield return view.DestroyAsync().ToCoroutine();

            Assert.That(view.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
            Assert.That(transition.DisposeCount, Is.EqualTo(1));
            Assert.That(childView.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(childLoader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(childLoader.DisposeCount, Is.EqualTo(1));
            Assert.That(childTransition.InitializeCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator LoadAsync_WhenCanceledAfterInstantiate_ReleasesInstanceAndRethrowsCancellation()
        {
            var events = new List<string>();
            var root = CreateObject("CanceledView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { DelayAsyncResult = true };
            var transition = new FakeTransition(events);
            var view = new FakeView(loader, transition, events);
            using var cancellation = new CancellationTokenSource();

            var exceptionTask = CaptureException(
                InvokeLoadAsync(view, parent.transform, cancellation.Token)).Preserve();
            cancellation.Cancel();
            loader.CompleteAsync(root);
            Exception exception = null;
            yield return CaptureResult(exceptionTask, result => exception = result).ToCoroutine();

            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            Assert.That(view.State, Is.EqualTo(ViewState.Faulted));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
            Assert.That(transition.InitializeCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator UICache_WhenCachedViewCannotBeReused_CreatesReplacement()
        {
            var cache = new UICache();
            var destroyed = cache.GetOrCreateView<CacheTestView>();
            yield return InvokeDestroyAsync(destroyed).ToCoroutine();
            var replacementAfterDestroy = cache.GetOrCreateView<CacheTestView>();

            Assert.That(replacementAfterDestroy, Is.Not.SameAs(destroyed));

            var loader = new FakeResourceLoader { AsyncResult = null };
            replacementAfterDestroy.Loader = loader;
            var parent = CreateObject("ViewParent");
            yield return InvokeLoadAsync(replacementAfterDestroy, parent.transform, CancellationToken.None).ToCoroutine();
            var replacementAfterFault = cache.GetOrCreateView<CacheTestView>();

            Assert.That(replacementAfterFault, Is.Not.SameAs(replacementAfterDestroy));
            Assert.That(replacementAfterFault.State, Is.EqualTo(ViewState.Created));
        }

        private GameObject CreateObject(string name)
        {
            var createdObject = new GameObject(name);
            createdObjects.Add(createdObject);
            return createdObject;
        }

        private static UniTask<bool> InvokeLoadAsync(
            View view,
            Transform parent,
            CancellationToken cancellationToken)
        {
            return view.LoadAsync(parent, cancellationToken);
        }

        private static UniTask InvokeTransitionAsync(
            string methodName,
            View view,
            UITransitionContext context,
            CancellationToken cancellationToken)
        {
            var method = typeof(View).GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(UITransitionContext), typeof(CancellationToken) },
                null);
            Assert.That(method, Is.Not.Null, $"View 应提供 {methodName}(UITransitionContext, CancellationToken) 入口。");
            return (UniTask)method.Invoke(view, new object[] { context, cancellationToken });
        }

        private static UniTask InvokeDestroyAsync(View view)
        {
            return view.DestroyAsync();
        }

        private static IUITransition GetUITransition(View view)
        {
            return view.UITransition;
        }

        private static async UniTask<Exception> CaptureException(UniTask<bool> task)
        {
            try
            {
                await task;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static async UniTask<Exception> CaptureException(UniTask task)
        {
            try
            {
                await task;
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static async UniTask CaptureResult<T>(UniTask<T> task, Action<T> capture)
        {
            capture(await task);
        }

        private sealed class FakeView : View
        {
            private readonly IUITransition transition;
            private readonly List<string> events;

            public FakeView(IResourceLoader loader, IUITransition transition, List<string> events)
            {
                Loader = loader;
                this.transition = transition;
                this.events = events;
            }

            public override string Address => "Fake/View";

            protected override IUITransition CreateUITransition()
            {
                return transition;
            }

            public override void OnBeforeInit()
            {
                events.Add($"View.Before:{State}");
            }

            protected override void InitComponent()
            {
                events.Add($"View.Component:{State}");
            }

            protected override void OnGameObjectInitialize()
            {
                events.Add($"View.GameObject:{State}");
            }

            protected override void OnShow()
            {
                events.Add($"View.Show:{State}");
            }

            protected override void OnHide()
            {
                events.Add($"View.Hide:{State}");
            }

            protected override void OnDestroy()
            {
                events.Add($"View.Destroy:{State}");
            }
        }

        private sealed class CacheTestView : View
        {
            public override string Address => "Fake/CacheView";
        }

        private sealed class InitializationFailureView : View
        {
            private IUITransition transition;
            private IDisposable binding;
            private View subView;

            public override string Address => "Fake/InitializationFailureView";

            public void Configure(
                IResourceLoader loader,
                IUITransition transition,
                IDisposable binding,
                View subView)
            {
                Loader = loader;
                this.transition = transition;
                this.binding = binding;
                this.subView = subView;
            }

            protected override IUITransition CreateUITransition()
            {
                return transition;
            }

            protected override void InitComponent()
            {
                AddBinding(binding);
                AddSubView(subView);
            }
        }

        private sealed class CountingBinding : IDisposable
        {
            public int DisposeCount { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
            }
        }

        private sealed class FakeTransition : IUITransition
        {
            private readonly List<string> events;

            public FakeTransition(List<string> events)
            {
                this.events = events;
            }

            public int InitializeCount { get; private set; }
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }
            public int DisposeCount { get; private set; }
            public Exception InitializeException { get; set; }
            public Exception EnterException { get; set; }
            public Exception ExitException { get; set; }

            public void Initialize(Transform root)
            {
                InitializeCount++;
                events.Add("Transition.Initialize");
                if (InitializeException != null)
                {
                    throw InitializeException;
                }
            }

            public UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnterCount++;
                events.Add("Transition.Enter");
                if (EnterException != null)
                {
                    throw EnterException;
                }

                return UniTask.CompletedTask;
            }

            public UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExitCount++;
                events.Add("Transition.Exit");
                if (ExitException != null)
                {
                    throw ExitException;
                }

                return UniTask.CompletedTask;
            }

            public void CompleteImmediately(UITransitionDirection direction)
            {
            }

            public void Dispose()
            {
                DisposeCount++;
                events.Add("Transition.Dispose");
            }
        }

        private sealed class FakeResourceLoader : IResourceLoader
        {
            private UniTaskCompletionSource<GameObject> completionSource;

            public GameObject AsyncResult { get; set; }
            public bool DelayAsyncResult { get; set; }
            public int InstantiateAsyncCount { get; private set; }
            public int ReleaseInstanceCount { get; private set; }
            public int DisposeCount { get; private set; }

            public GameObject Instantiate(string address, Transform parent)
            {
                return Instantiate(address, parent, false);
            }

            public GameObject Instantiate(string address, Transform parent, bool worldPositionStays)
            {
                if (AsyncResult != null)
                {
                    AsyncResult.transform.SetParent(parent, worldPositionStays);
                }

                return AsyncResult;
            }

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent)
            {
                return InstantiateAsync(address, parent, false);
            }

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool worldPositionStays)
            {
                InstantiateAsyncCount++;
                if (!DelayAsyncResult)
                {
                    if (AsyncResult != null)
                    {
                        AsyncResult.transform.SetParent(parent, worldPositionStays);
                    }

                    return UniTask.FromResult(AsyncResult);
                }

                completionSource = new UniTaskCompletionSource<GameObject>();
                return AwaitAndParent(completionSource.Task, parent, worldPositionStays);
            }

            public void CompleteAsync(GameObject result)
            {
                completionSource.TrySetResult(result);
            }

            public T LoadAsset<T>(string address) where T : Object
            {
                return null;
            }

            public UniTask<T> LoadAssetAsync<T>(string address) where T : Object
            {
                return UniTask.FromResult<T>(null);
            }

            public void ReleaseAsset(Object asset)
            {
            }

            public void ReleaseInstance(GameObject instance)
            {
                ReleaseInstanceCount++;
                if (instance != null)
                {
                    Object.Destroy(instance);
                }
            }

            public void Dispose()
            {
                DisposeCount++;
            }

            private static async UniTask<GameObject> AwaitAndParent(
                UniTask<GameObject> task,
                Transform parent,
                bool worldPositionStays)
            {
                var result = await task;
                if (result != null)
                {
                    result.transform.SetParent(parent, worldPositionStays);
                }

                return result;
            }
        }
    }
}
