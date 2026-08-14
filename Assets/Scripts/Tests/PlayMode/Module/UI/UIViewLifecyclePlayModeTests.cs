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

namespace Tests.Module
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
        public IEnumerator LoadAsync_WhenOnBeforeInitReenters_ReusesSingleOperation()
        {
            var events = new List<string>();
            var root = CreateObject("ReentrantLoadView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { AsyncResult = root };
            var transition = new FakeTransition(events);
            var view = new ReentrantLoadView(loader, transition, events, parent.transform);

            bool loaded = false;
            yield return CaptureResult(
                view.LoadAsync(parent.transform, CancellationToken.None),
                result => loaded = result).ToCoroutine();
            bool reenteredLoaded = false;
            yield return CaptureResult(
                view.ReenteredLoad,
                result => reenteredLoaded = result).ToCoroutine();

            Assert.That(loaded, Is.True);
            Assert.That(reenteredLoaded, Is.True);
            Assert.That(loader.InstantiateAsyncCount, Is.EqualTo(1));
            Assert.That(transition.InitializeCount, Is.EqualTo(1));
            yield return view.DestroyAsync().ToCoroutine();
        }

        [UnityTest]
        public IEnumerator LoadAsync_WhenOnBeforeInitReentersCanceledWaiter_OuterLoadStillSucceeds()
        {
            var events = new List<string>();
            var root = CreateObject("CanceledReentrantLoadView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { AsyncResult = root };
            var transition = new FakeTransition(events);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            var view = new CanceledReentrantLoadView(
                loader,
                transition,
                events,
                parent.transform,
                canceled.Token);

            var outer = CaptureException(
                view.LoadAsync(parent.transform, CancellationToken.None)).Preserve();
            Exception outerException = null;
            yield return CaptureResult(outer, result => outerException = result).ToCoroutine();
            Exception innerException = null;
            yield return CaptureResult(
                CaptureException(view.ReenteredLoad),
                result => innerException = result).ToCoroutine();
            yield return null;

            Assert.That(outerException, Is.Null);
            Assert.That(innerException, Is.TypeOf<OperationCanceledException>());
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(loader.InstantiateAsyncCount, Is.EqualTo(1));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(0));
            Assert.That(loader.DisposeCount, Is.EqualTo(0));
            LogAssert.NoUnexpectedReceived();
            yield return view.DestroyAsync().ToCoroutine();
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
        public IEnumerator EnterExit_WhenAnimationDisabled_CompletesTransitionImmediately()
        {
            var events = new List<string>();
            var root = CreateObject("ImmediateTransitionView");
            var parent = CreateObject("ViewParent");
            var transition = new FakeTransition(events);
            var view = new FakeView(
                new FakeResourceLoader { AsyncResult = root },
                transition,
                events);
            yield return view.LoadAsync(parent.transform, CancellationToken.None).ToCoroutine();

            yield return view.EnterAsync(
                new UITransitionContext(20, UINavigationAction.Push, view, null, false),
                CancellationToken.None).ToCoroutine();
            yield return view.ExitAsync(
                new UITransitionContext(21, UINavigationAction.Close, null, view, false),
                CancellationToken.None).ToCoroutine();

            Assert.That(transition.CompleteImmediatelyCount, Is.EqualTo(2));
            Assert.That(transition.LastImmediateDirection, Is.EqualTo(UITransitionDirection.Exit));
            yield return view.DestroyAsync().ToCoroutine();
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

            var sameFaultedView = cache.GetOrCreateView<InitializationFailureView>();
            Assert.That(sameFaultedView, Is.SameAs(view));
            yield return view.DestroyAsync().ToCoroutine();
            var replacement = cache.GetOrCreateView<InitializationFailureView>();
            Assert.That(replacement, Is.Not.SameAs(view));
            Assert.That(binding.DisposeCount, Is.EqualTo(1));
            Assert.That(transition.DisposeCount, Is.EqualTo(1));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator InitWithGameObject_WhenFailureCleanupIsPending_DestroyWaitsSameCleanupOperation()
        {
            var parentView = new InitializationFailureView();
            var parentRoot = CreateObject("SynchronousFailureParent");
            var parentLoader = new FakeResourceLoader();
            var parentTransition = new FakeTransition(new List<string>());
            var parentBinding = new CountingBinding();
            var expected = new InvalidOperationException("Synchronous transition initialize failed");
            parentTransition.InitializeException = expected;

            var childLoader = new FakeResourceLoader { DelayAsyncResult = true };
            var childTransition = new FakeTransition(new List<string>());
            var childView = new FakeView(childLoader, childTransition, new List<string>());
            var childBinding = new CountingBinding();
            childView.AddBinding(childBinding);
            var childParent = CreateObject("DelayedChildParent");
            var childRoot = CreateObject("DelayedOwnedChild");
            var childLoad = CaptureException(
                childView.LoadAsync(childParent.transform, CancellationToken.None)).Preserve();
            parentView.Configure(parentLoader, parentTransition, parentBinding, childView);

            Exception actual = null;
            try
            {
                parentView.InitWithGameObject(parentRoot);
            }
            catch (Exception exception)
            {
                actual = exception;
            }

            Assert.That(actual, Is.SameAs(expected));
            Assert.That(parentView.State, Is.EqualTo(ViewState.Faulted));
            var destroy = parentView.DestroyAsync();
            Assert.That(destroy.Status, Is.EqualTo(UniTaskStatus.Pending));

            childLoader.CompleteAsync(childRoot);
            Exception childLoadException = null;
            yield return CaptureResult(childLoad, result => childLoadException = result).ToCoroutine();
            yield return destroy.ToCoroutine();
            yield return null;

            Assert.That(parentView.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(parentBinding.DisposeCount, Is.EqualTo(1));
            Assert.That(parentTransition.InitializeCount, Is.EqualTo(1));
            Assert.That(parentTransition.DisposeCount, Is.EqualTo(1));
            Assert.That(parentLoader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(parentLoader.DisposeCount, Is.EqualTo(1));
            Assert.That(childView.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(childLoadException, Is.TypeOf<OperationCanceledException>());
            Assert.That(childBinding.DisposeCount, Is.EqualTo(1));
            Assert.That(childTransition.InitializeCount, Is.EqualTo(0));
            Assert.That(childLoader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(childLoader.DisposeCount, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator InitWithGameObject_WhenBindingReentersDestroy_ReusesPublishedCleanupLatch()
        {
            var view = new InitializationFailureView();
            var root = CreateObject("SynchronousCleanupReentryView");
            var loader = new FakeResourceLoader();
            var transition = new FakeTransition(new List<string>());
            var binding = new ReentrantDestroyBinding(view);
            var expected = new InvalidOperationException("Synchronous cleanup reentry");
            transition.InitializeException = expected;
            view.Configure(loader, transition, binding, null);

            Exception actual = null;
            try
            {
                view.InitWithGameObject(root);
            }
            catch (Exception exception)
            {
                actual = exception;
            }

            Assert.That(actual, Is.SameAs(expected));
            yield return binding.ReenteredDestroy.ToCoroutine();
            yield return null;

            Assert.That(binding.DisposeCount, Is.EqualTo(1));
            Assert.That(view.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(transition.DisposeCount, Is.EqualTo(1));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
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
            var childLoad = CaptureException(
                childView.LoadAsync(parent.transform, CancellationToken.None)).Preserve();
            view.AddSubView(childView);

            var first = view.DestroyAsync();
            Assert.That(first.Status, Is.EqualTo(UniTaskStatus.Pending));
            var second = view.DestroyAsync();
            Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));

            childLoader.CompleteAsync(childRoot);
            Exception childLoadException = null;
            yield return CaptureResult(childLoad, result => childLoadException = result).ToCoroutine();
            yield return UniTask.WhenAll(first, second).ToCoroutine();
            yield return view.DestroyAsync().ToCoroutine();

            Assert.That(view.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
            Assert.That(transition.DisposeCount, Is.EqualTo(1));
            Assert.That(childView.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(childLoadException, Is.TypeOf<OperationCanceledException>());
            Assert.That(childLoader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(childLoader.DisposeCount, Is.EqualTo(1));
            Assert.That(childTransition.InitializeCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator DestroyAsync_WhenOnDestroyReenters_ReusesLatchAndKeepsLegacyHookOrder()
        {
            var events = new List<string>();
            var root = CreateObject("ReentrantDestroyView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { AsyncResult = root };
            var transition = new FakeTransition(events);
            var view = new ReentrantDestroyView(loader, transition, events);
            yield return view.LoadAsync(parent.transform, CancellationToken.None).ToCoroutine();

            yield return view.DestroyAsync().ToCoroutine();
            yield return view.ReenteredDestroy.ToCoroutine();

            Assert.That(view.OnDestroyCount, Is.EqualTo(1));
            Assert.That(view.HadOwnedResourcesDuringOnDestroy, Is.True);
            Assert.That(view.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
            Assert.That(transition.DisposeCount, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
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
            yield return null;
            var canceledPromptly = exceptionTask.Status == UniTaskStatus.Succeeded;
            loader.CompleteAsync(root);
            Exception exception = null;
            yield return CaptureResult(exceptionTask, result => exception = result).ToCoroutine();
            yield return null;

            Assert.That(canceledPromptly, Is.True);
            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            Assert.That(view.State, Is.EqualTo(ViewState.Faulted));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(1));
            Assert.That(loader.DisposeCount, Is.EqualTo(1));
            Assert.That(transition.InitializeCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator LoadAsync_WhenOneOfTwoWaitersCancels_OtherWaiterStillCompletesSharedLoad()
        {
            var events = new List<string>();
            var root = CreateObject("SharedLoadView");
            var parent = CreateObject("ViewParent");
            var loader = new FakeResourceLoader { DelayAsyncResult = true };
            var transition = new FakeTransition(events);
            var view = new FakeView(loader, transition, events);
            using var firstCancellation = new CancellationTokenSource();

            var first = CaptureException(
                view.LoadAsync(parent.transform, firstCancellation.Token)).Preserve();
            var second = CaptureException(
                view.LoadAsync(parent.transform, CancellationToken.None)).Preserve();
            firstCancellation.Cancel();
            yield return null;

            var firstCanceledPromptly = first.Status == UniTaskStatus.Succeeded;
            var secondWasPending = second.Status == UniTaskStatus.Pending;
            loader.CompleteAsync(root);
            Exception firstException = null;
            yield return CaptureResult(first, result => firstException = result).ToCoroutine();
            Exception secondException = null;
            yield return CaptureResult(second, result => secondException = result).ToCoroutine();

            Assert.That(firstCanceledPromptly, Is.True);
            Assert.That(secondWasPending, Is.True);
            Assert.That(firstException, Is.TypeOf<OperationCanceledException>());
            Assert.That(secondException, Is.Null);
            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(loader.InstantiateAsyncCount, Is.EqualTo(1));
            Assert.That(loader.ReleaseInstanceCount, Is.EqualTo(0));
            Assert.That(loader.DisposeCount, Is.EqualTo(0));
            yield return view.DestroyAsync().ToCoroutine();
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
            var sameFaultedView = cache.GetOrCreateView<CacheTestView>();

            Assert.That(sameFaultedView, Is.SameAs(replacementAfterDestroy));
            yield return replacementAfterDestroy.DestroyAsync().ToCoroutine();
            var replacementAfterFaultDestroy = cache.GetOrCreateView<CacheTestView>();
            Assert.That(replacementAfterFaultDestroy, Is.Not.SameAs(replacementAfterDestroy));
            Assert.That(replacementAfterFaultDestroy.State, Is.EqualTo(ViewState.Created));
        }

        [Test]
        public void AddSubView_WhenAddingSelf_ThrowsArgumentException()
        {
            var view = new CacheTestView();

            Assert.Throws<ArgumentException>(() => view.AddSubView(view));
        }

        [Test]
        public void AddSubView_WhenAddingTwoNodeCycle_ThrowsAndDestroyCompletes()
        {
            var first = new CacheTestView();
            var second = new CacheTestView();
            first.AddSubView(second);

            Assert.Throws<InvalidOperationException>(() => second.AddSubView(first));
            var destroy = first.DestroyAsync();
            Assert.That(destroy.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(first.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(second.State, Is.EqualTo(ViewState.Destroyed));
        }

        [Test]
        public void AddSubView_WhenAddingThreeNodeCycle_ThrowsAndDuplicateRemainsIdempotent()
        {
            var first = new CacheTestView();
            var second = new CacheTestView();
            var third = new CacheTestView();
            first.AddSubView(second);
            first.AddSubView(second);
            second.AddSubView(third);

            Assert.Throws<InvalidOperationException>(() => third.AddSubView(first));
            var destroy = first.DestroyAsync();
            Assert.That(destroy.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(first.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(second.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(third.State, Is.EqualTo(ViewState.Destroyed));
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

        private class FakeView : View
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

        private sealed class ReentrantLoadView : FakeView
        {
            private readonly Transform parent;
            private bool reentered;

            public ReentrantLoadView(
                IResourceLoader loader,
                IUITransition transition,
                List<string> events,
                Transform parent)
                : base(loader, transition, events)
            {
                this.parent = parent;
            }

            public UniTask<bool> ReenteredLoad { get; private set; }

            public override void OnBeforeInit()
            {
                base.OnBeforeInit();
                if (reentered)
                {
                    return;
                }

                reentered = true;
                ReenteredLoad = LoadAsync(parent, CancellationToken.None);
            }
        }

        private sealed class CanceledReentrantLoadView : FakeView
        {
            private readonly Transform parent;
            private readonly CancellationToken canceledToken;
            private bool reentered;

            public CanceledReentrantLoadView(
                IResourceLoader loader,
                IUITransition transition,
                List<string> events,
                Transform parent,
                CancellationToken canceledToken)
                : base(loader, transition, events)
            {
                this.parent = parent;
                this.canceledToken = canceledToken;
            }

            public UniTask<bool> ReenteredLoad { get; private set; }

            public override void OnBeforeInit()
            {
                base.OnBeforeInit();
                if (reentered)
                {
                    return;
                }

                reentered = true;
                ReenteredLoad = LoadAsync(parent, canceledToken);
            }
        }

        private sealed class ReentrantDestroyView : FakeView
        {
            private bool reentered;

            public ReentrantDestroyView(
                IResourceLoader loader,
                IUITransition transition,
                List<string> events)
                : base(loader, transition, events)
            {
            }

            public int OnDestroyCount { get; private set; }
            public bool HadOwnedResourcesDuringOnDestroy { get; private set; }
            public UniTask ReenteredDestroy { get; private set; }

            protected override void OnDestroy()
            {
                OnDestroyCount++;
                HadOwnedResourcesDuringOnDestroy = gameObject != null && UITransition != null;
                base.OnDestroy();
                if (reentered)
                {
                    return;
                }

                reentered = true;
                ReenteredDestroy = DestroyAsync();
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

        private sealed class ReentrantDestroyBinding : IDisposable
        {
            private readonly View owner;

            public ReentrantDestroyBinding(View owner)
            {
                this.owner = owner;
            }

            public int DisposeCount { get; private set; }
            public UniTask ReenteredDestroy { get; private set; }

            public void Dispose()
            {
                DisposeCount++;
                ReenteredDestroy = owner.DestroyAsync();
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
            public int CompleteImmediatelyCount { get; private set; }
            public UITransitionDirection LastImmediateDirection { get; private set; }
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
                CompleteImmediatelyCount++;
                LastImmediateDirection = direction;
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
