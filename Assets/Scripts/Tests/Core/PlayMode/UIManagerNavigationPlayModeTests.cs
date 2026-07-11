using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
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
            Assert.That(closeResult.Status, Is.EqualTo(UIOperationStatus.Succeeded));
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

        [UnityTest]
        public IEnumerator EnterFailure_RestoresCompletePresentationSnapshotAndNames()
        {
            TestViewRegistry.Register<SnapshotPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<SnapshotPage>(), _ => { });
            var oldPage = UIManager.Instance.Get<SnapshotPage>();
            var root = UIRootManager.Instance.GetRoot(UILayer.Base);
            var beforeSibling = new GameObject("BeforeSibling").transform;
            beforeSibling.SetParent(root, false);
            oldPage.transform.SetSiblingIndex(0);
            beforeSibling.SetSiblingIndex(1);

            var mask = UIRootManager.Instance.Mask;
            mask.transform.SetParent(root, false);
            mask.transform.SetSiblingIndex(1);
            mask.transform.localScale = new Vector3(0.3f, 0.4f, 0.5f);
            mask.transform.localPosition = new Vector3(7f, 8f, 9f);
            var button = mask.GetComponent<Button>();
            button.interactable = false;
            var expectedViewParent = oldPage.transform.parent;
            var expectedViewSibling = oldPage.transform.GetSiblingIndex();
            var expectedViewActive = oldPage.gameObject.activeSelf;
            var expectedMaskParent = mask.transform.parent;
            var expectedMaskSibling = mask.transform.GetSiblingIndex();
            var expectedMaskScale = mask.transform.localScale;
            var expectedMaskPosition = mask.transform.localPosition;
            var expectedCurrentName = UIManager.Instance.CurrentUIName;
            var expectedLastName = UIManager.Instance.LastCloseName;

            TestViewRegistry.Register<SecondPage>(throwEnter: true);
            yield return AwaitResult(UIManager.Instance.ShowAsync<SecondPage>(), _ => { });

            Assert.That(oldPage.transform.parent, Is.SameAs(expectedViewParent));
            Assert.That(oldPage.transform.GetSiblingIndex(), Is.EqualTo(expectedViewSibling));
            Assert.That(oldPage.gameObject.activeSelf, Is.EqualTo(expectedViewActive));
            Assert.That(mask.transform.parent, Is.SameAs(expectedMaskParent));
            Assert.That(mask.transform.GetSiblingIndex(), Is.EqualTo(expectedMaskSibling));
            Assert.That(mask.transform.localScale, Is.EqualTo(expectedMaskScale));
            Assert.That(mask.transform.localPosition, Is.EqualTo(expectedMaskPosition));
            Assert.That(button.interactable, Is.False);
            Assert.That(UIManager.Instance.CurrentUIName, Is.EqualTo(expectedCurrentName));
            Assert.That(UIManager.Instance.LastCloseName, Is.EqualTo(expectedLastName));
            Object.Destroy(beforeSibling.gameObject);
        }

        [UnityTest]
        public IEnumerator ReshowFaultedNonTopPage_RemovesItAndKeepsOldTopVisible()
        {
            TestViewRegistry.Register<FirstPage>(throwEnterOnCall: 2);
            TestViewRegistry.Register<SecondPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            yield return AwaitResult(UIManager.Instance.ShowAsync<SecondPage>(), _ => { });
            var top = UIManager.Instance.Get<SecondPage>();

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(UIManager.Instance.Get<FirstPage>(), Is.Null);
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(top));
            Assert.That(top.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CloseFaultedNonTopPage_RemovesItAndKeepsTopVisible()
        {
            TestViewRegistry.Register<FirstPage>(throwExitOnCall: 2);
            TestViewRegistry.Register<SecondPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            yield return AwaitResult(UIManager.Instance.ShowAsync<SecondPage>(), _ => { });
            var nonTop = UIManager.Instance.Get<FirstPage>();
            var top = UIManager.Instance.Get<SecondPage>();
            nonTop.RestoreVisibleAfterNavigationFailure();

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.CloseAsync<FirstPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(UIManager.Instance.Get<FirstPage>(), Is.Null);
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(top));
            Assert.That(top.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PrimaryEnterException_IsPreservedWhenDestroyAndRestoreAlsoThrow()
        {
            TestViewRegistry.Register<FirstPage>(throwComplete: true);
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            var loader = TestViewRegistry.Register<DestroyThrowPage>(throwEnter: true);

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: destroy failed");
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: restore failed");
            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<DestroyThrowPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.SameAs(loader.Transition.EnterException));
            Assert.That(UIManager.Instance.Get<DestroyThrowPage>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator EventHandlerExceptions_DoNotRollbackCommittedOpenOrClose()
        {
            TestViewRegistry.Register<FirstPage>();
            void ThrowOpen(View _) => throw new InvalidOperationException("open handler failed");
            UIManager.Instance.OnOpen += ThrowOpen;
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: open handler failed");
            UIOperationResult openResult = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), value => openResult = value);
            UIManager.Instance.OnOpen -= ThrowOpen;

            Assert.That(openResult.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(UIManager.Instance.CurrentUIName, Is.EqualTo(nameof(FirstPage)));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));

            void ThrowClose(View _) => throw new InvalidOperationException("close handler failed");
            UIManager.Instance.OnClose += ThrowClose;
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: close handler failed");
            UIOperationResult closeResult = default;
            yield return AwaitResult(UIManager.Instance.CloseAsync<FirstPage>(), value => closeResult = value);
            UIManager.Instance.OnClose -= ThrowClose;

            Assert.That(closeResult.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(UIManager.Instance.CurrentUIName, Is.Null);
            Assert.That(UIManager.Instance.StackCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CloseAll_WhenOneDestroyThrows_ContinuesAndClearsAllState()
        {
            TestViewRegistry.Register<DestroyThrowPage>();
            var laterLoader = TestViewRegistry.Register<SecondPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<DestroyThrowPage>(), _ => { });
            yield return AwaitResult(UIManager.Instance.ShowAsync<SecondPage>(), _ => { });
            UIRootManager.Instance.Mask.transform.localScale = Vector3.one;

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.CloseAllAsync(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.TypeOf<AggregateException>());
            Assert.That(result.Exception.InnerException.Message, Is.EqualTo("destroy failed"));
            Assert.That(laterLoader.ReleaseCount, Is.EqualTo(1));
            Assert.That(UIManager.Instance.cacheStack.GetAllViews(), Is.Empty);
            Assert.That(UIManager.Instance.StackCount, Is.Zero);
            Assert.That(UIRootManager.Instance.Mask.transform.localScale, Is.EqualTo(Vector3.zero));
            Assert.That(UIManager.Instance.CurrentUIName, Is.Null);
            Assert.That(UIManager.Instance.LastCloseName, Is.Null);
        }

        [UnityTest]
        public IEnumerator ReplaceDestroyOnHideFalse_RemovesOldReferenceButKeepsHiddenCache()
        {
            TestViewRegistry.Register<PersistentPage>();
            TestViewRegistry.Register<SecondPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<PersistentPage>(), _ => { });
            var oldPage = UIManager.Instance.Get<PersistentPage>();
            Assert.That(oldPage.Reference, Is.EqualTo(1));

            yield return AwaitResult(UIManager.Instance.ReplaceAsync<SecondPage>(), _ => { });

            Assert.That(oldPage.Reference, Is.Zero);
            Assert.That(oldPage.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(UIManager.Instance.Get<PersistentPage>(), Is.SameAs(oldPage));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.TypeOf<SecondPage>());
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ConsecutiveLegacyDataShow_ConfiguresEachOperationInFifoOrder()
        {
            var loader = TestViewRegistry.Register<DataPage>(delay: true);
            UIManager.Instance.Show<DataPage, string>("one");
            UIManager.Instance.Show<DataPage, string>("two");
            yield return null;

            var beforeCompletion = TestViewRegistry.Events.ToArray();
            loader.Complete(new GameObject(nameof(DataPage)));
            for (int i = 0; i < 60 && TestViewRegistry.Events.Count < 4; i++)
            {
                yield return null;
            }

            Assert.That(beforeCompletion, Is.EqualTo(new[] { "data:one", "DataPage.load" }));
            Assert.That(TestViewRegistry.Events,
                Is.EqualTo(new[] { "data:one", "DataPage.load", "show:one", "data:two" }));
        }

        [UnityTest]
        public IEnumerator PreloadData_WaitsForEarlierNavigationThenLoadsHiddenWithoutStackMutation()
        {
            var slowLoader = TestViewRegistry.Register<SlowPage>(delay: true);
            TestViewRegistry.Register<DataPage>();
            var showTask = UIManager.Instance.ShowAsync<SlowPage>();
            var preloadTask = UIManager.Instance.Preload<DataPage, string>("preload");
            yield return null;

            var configuredTooEarly = TestViewRegistry.Events.Contains("data:preload");
            var loadedTooEarly = TestViewRegistry.Events.Contains("DataPage.load");

            slowLoader.Complete(new GameObject(nameof(SlowPage)));
            yield return AwaitResult(showTask, _ => { });
            yield return preloadTask.ToCoroutine();

            var preloaded = UIManager.Instance.Get<DataPage>();
            Assert.That(configuredTooEarly, Is.False);
            Assert.That(loadedTooEarly, Is.False);
            Assert.That(TestViewRegistry.Events, Does.Contain("data:preload"));
            Assert.That(preloaded.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ClosePostCommitDestroyFailure_DoesNotRestoreDestroyedGhost()
        {
            TestViewRegistry.Register<FirstPage>();
            TestViewRegistry.Register<DestroyThrowPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            var oldPage = UIManager.Instance.Get<FirstPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<DestroyThrowPage>(), _ => { });

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: destroy failed");
            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.CloseAsync<DestroyThrowPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(UIManager.Instance.Get<DestroyThrowPage>(), Is.Null);
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(oldPage));
            Assert.That(oldPage.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator ReplacePostCommitDestroyFailure_KeepsNewPageAndRemovesOld()
        {
            TestViewRegistry.Register<DestroyThrowPage>();
            TestViewRegistry.Register<SecondPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<DestroyThrowPage>(), _ => { });

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: destroy failed");
            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ReplaceAsync<SecondPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(UIManager.Instance.Get<DestroyThrowPage>(), Is.Null);
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(result.View));
            Assert.That(result.View.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator PreloadConfigureFailure_PreservesPrimaryWhenCleanupThrowsAndRemovesCache()
        {
            TestViewRegistry.Register<ConfigureDestroyThrowPage>();
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: destroy failed");

            UIOperationResult result = default;
            yield return AwaitResult(
                UIManager.Instance.PreloadAsync<ConfigureDestroyThrowPage>(
                    target => target.SetData("bad")),
                value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.SameAs(ConfigureDestroyThrowPage.PrimaryException));
            Assert.That(UIManager.Instance.Get<ConfigureDestroyThrowPage>(), Is.Null);
            Assert.That(UIManager.Instance.StackCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CacheOnlyCloseDestroyFailure_ReturnsPrimaryAndAlwaysRemovesCache()
        {
            TestViewRegistry.Register<DestroyThrowPage>();
            yield return UIManager.Instance.Preload<DestroyThrowPage>().ToCoroutine();
            var cached = UIManager.Instance.Get<DestroyThrowPage>();

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.CloseAsync<DestroyThrowPage>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.View, Is.SameAs(cached));
            Assert.That(result.Exception.Message, Is.EqualTo("destroy failed"));
            Assert.That(UIManager.Instance.Get<DestroyThrowPage>(), Is.Null);
            Assert.That(UIManager.Instance.StackCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CloseAllCallerCancellation_StillCleansEverythingAndReturnsCanceled()
        {
            var loader = TestViewRegistry.Register<SlowPage>(delay: true);
            var view = UIManager.Instance.cacheStack.GetOrCreateView<SlowPage>();
            var loadTask = view.LoadAsync(
                UIRootManager.Instance.GetRoot(view.Level), CancellationToken.None)
                .SuppressCancellationThrow();
            yield return null;
            using var cancellation = new CancellationTokenSource();
            var closeAllTask = UIManager.Instance.CloseAllAsync(cancellation.Token);
            yield return null;
            cancellation.Cancel();
            loader.Complete(new GameObject(nameof(SlowPage)));
            yield return loadTask.ToCoroutine();

            UIOperationResult result = default;
            yield return AwaitResult(closeAllTask, value => result = value);
            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(UIManager.Instance.cacheStack.GetAllViews(), Is.Empty);
            Assert.That(UIManager.Instance.StackCount, Is.Zero);
            Assert.That(loader.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SecondCloseAll_CancelsFirstAfterCleanupWithoutDoubleRelease()
        {
            var loader = TestViewRegistry.Register<SlowPage>(delay: true);
            var view = UIManager.Instance.cacheStack.GetOrCreateView<SlowPage>();
            var loadTask = view.LoadAsync(
                UIRootManager.Instance.GetRoot(view.Level), CancellationToken.None)
                .SuppressCancellationThrow();
            yield return null;
            var firstTask = UIManager.Instance.CloseAllAsync();
            yield return null;
            var secondTask = UIManager.Instance.CloseAllAsync();
            loader.Complete(new GameObject(nameof(SlowPage)));
            yield return loadTask.ToCoroutine();

            UIOperationResult first = default;
            UIOperationResult second = default;
            yield return AwaitResult(firstTask, value => first = value);
            yield return AwaitResult(secondTask, value => second = value);
            Assert.That(first.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(second.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(loader.ReleaseCount, Is.EqualTo(1));
            Assert.That(UIManager.Instance.cacheStack.GetAllViews(), Is.Empty);
            Assert.That(UIManager.Instance.StackCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator LegacyShowDuringCloseAllBarrier_ReturnsNullThenCreatesFreshVisibleView()
        {
            var slowLoader = TestViewRegistry.Register<SlowPage>(delay: true);
            var firstLaterLoader = TestViewRegistry.Register<SecondPage>();
            var unusedLoader = TestViewRegistry.Register<SecondPage>();
            var showTask = UIManager.Instance.ShowAsync<SlowPage>();
            yield return null;
            var closeAllTask = UIManager.Instance.CloseAllAsync();

            Assert.That(UIManager.Instance.HasCloseAllBarrier, Is.True);
            var legacyReturned = UIManager.Instance.Show<SecondPage>();
            slowLoader.Complete(new GameObject(nameof(SlowPage)));
            yield return AwaitResult(showTask, _ => { });
            yield return AwaitResult(closeAllTask, _ => { });
            for (int i = 0; i < 60 && UIManager.Instance.Get<SecondPage>()?.State != ViewState.Visible; i++)
            {
                yield return null;
            }

            Assert.That(legacyReturned, Is.Null);
            Assert.That(UIManager.Instance.Get<SecondPage>()?.State, Is.EqualTo(ViewState.Visible));
            Assert.That(firstLaterLoader.InstantiateCount, Is.EqualTo(1));
            Assert.That(unusedLoader.InstantiateCount, Is.Zero);
            Assert.That(slowLoader.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LegacySingleDataShowDuringCloseAllBarrier_ReturnsNullAndConfiguresFreshView()
        {
            var slowLoader = TestViewRegistry.Register<SlowPage>(delay: true);
            var actualLoader = TestViewRegistry.Register<DataPage>();
            var unusedLoader = TestViewRegistry.Register<DataPage>();
            var showTask = UIManager.Instance.ShowAsync<SlowPage>();
            yield return null;
            var closeAllTask = UIManager.Instance.CloseAllAsync();

            var returned = UIManager.Instance.Show<DataPage, string>("one");
            var configuredEarly = TestViewRegistry.Events.Contains("data:one");
            var loadedEarly = TestViewRegistry.Events.Contains("DataPage.load");
            slowLoader.Complete(new GameObject(nameof(SlowPage)));
            yield return AwaitResult(showTask, _ => { });
            yield return AwaitResult(closeAllTask, _ => { });
            for (int i = 0; i < 60 && UIManager.Instance.Get<DataPage>()?.State != ViewState.Visible; i++)
            {
                yield return null;
            }

            var actual = UIManager.Instance.Get<DataPage>();
            Assert.That(returned, Is.Null);
            Assert.That(configuredEarly, Is.False);
            Assert.That(loadedEarly, Is.False);
            Assert.That(actual?.State, Is.EqualTo(ViewState.Visible));
            Assert.That(TestViewRegistry.Events, Does.Contain("show:one"));
            Assert.That(actualLoader.InstantiateCount, Is.EqualTo(1));
            Assert.That(unusedLoader.InstantiateCount, Is.Zero);
            Assert.That(slowLoader.ReleaseCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LegacyDoubleDataShowDuringCloseAllBarrier_ReturnsNullAndConfiguresFreshView()
        {
            var slowLoader = TestViewRegistry.Register<SlowPage>(delay: true);
            var actualLoader = TestViewRegistry.Register<DoubleDataPage>();
            var unusedLoader = TestViewRegistry.Register<DoubleDataPage>();
            var showTask = UIManager.Instance.ShowAsync<SlowPage>();
            yield return null;
            var closeAllTask = UIManager.Instance.CloseAllAsync();

            var returned = UIManager.Instance.Show<DoubleDataPage, string, int>("two", 2);
            var configuredEarly = TestViewRegistry.Events.Contains("data:two:2");
            var loadedEarly = TestViewRegistry.Events.Contains("DoubleDataPage.load");
            slowLoader.Complete(new GameObject(nameof(SlowPage)));
            yield return AwaitResult(showTask, _ => { });
            yield return AwaitResult(closeAllTask, _ => { });
            for (int i = 0; i < 60 && UIManager.Instance.Get<DoubleDataPage>()?.State != ViewState.Visible; i++)
            {
                yield return null;
            }

            var actual = UIManager.Instance.Get<DoubleDataPage>();
            Assert.That(returned, Is.Null);
            Assert.That(configuredEarly, Is.False);
            Assert.That(loadedEarly, Is.False);
            Assert.That(actual?.State, Is.EqualTo(ViewState.Visible));
            Assert.That(TestViewRegistry.Events, Does.Contain("show:two:2"));
            Assert.That(actualLoader.InstantiateCount, Is.EqualTo(1));
            Assert.That(unusedLoader.InstantiateCount, Is.Zero);
            Assert.That(slowLoader.ReleaseCount, Is.EqualTo(1));
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
        private sealed class SnapshotPage : NavigationTestPage
        {
            public override MaskType Mask => MaskType.CloseRaycast;
        }

        private sealed class PersistentPage : NavigationTestPage
        {
            public override bool DestroyOnHide => false;
        }

        private sealed class DataPage : View<string>
        {
            private readonly List<string> events;

            public DataPage()
            {
                var entry = TestViewRegistry.Take(GetType());
                Loader = entry.Item1;
                events = entry.Item2;
            }

            public override string Address => nameof(DataPage);

            public override View<string> SetData(string data)
            {
                events.Add($"data:{data}");
                return base.SetData(data);
            }

            protected override void OnShow()
            {
                events.Add($"show:{params1}");
            }
        }

        private sealed class DoubleDataPage : View<string, int>
        {
            private readonly List<string> events;

            public DoubleDataPage()
            {
                var entry = TestViewRegistry.Take(GetType());
                Loader = entry.Item1;
                events = entry.Item2;
            }

            public override string Address => nameof(DoubleDataPage);

            public override View<string, int> SetData(string data1, int data2)
            {
                events.Add($"data:{data1}:{data2}");
                return base.SetData(data1, data2);
            }

            protected override void OnShow()
            {
                events.Add($"show:{params1}:{params2}");
            }
        }

        private sealed class DestroyThrowPage : NavigationTestPage
        {
            protected override void OnDestroy()
            {
                throw new InvalidOperationException("destroy failed");
            }
        }

        private sealed class ConfigureDestroyThrowPage : View<string>
        {
            internal static readonly Exception PrimaryException =
                new InvalidOperationException("configure failed");

            public ConfigureDestroyThrowPage()
            {
                var entry = TestViewRegistry.Take(GetType());
                Loader = entry.Item1;
            }

            public override string Address => nameof(ConfigureDestroyThrowPage);

            public override View<string> SetData(string data)
            {
                throw PrimaryException;
            }

            protected override void OnDestroy()
            {
                throw new InvalidOperationException("destroy failed");
            }
        }
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
                bool throwExit = false,
                int throwEnterOnCall = 0,
                int throwExitOnCall = 0,
                bool throwComplete = false)
                where T : View
            {
                events ??= Events;
                var loader = new TestLoader(
                    events,
                    typeof(T).Name,
                    delay,
                    returnNull ? null : result ?? (delay ? null : new GameObject(typeof(T).Name)),
                    throwEnter,
                    throwExit,
                    throwEnterOnCall,
                    throwExitOnCall,
                    throwComplete);
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
                bool throwExit,
                int throwEnterOnCall,
                int throwExitOnCall,
                bool throwComplete)
            {
                this.events = events;
                this.name = name;
                this.delay = delay;
                this.result = result;
                Transition = new TestTransition(
                    throwEnter,
                    throwExit,
                    throwEnterOnCall,
                    throwExitOnCall,
                    throwComplete);
            }

            internal int ReleaseCount { get; private set; }
            internal int InstantiateCount { get; private set; }
            internal TestTransition Transition { get; }

            public GameObject Instantiate(string address, Transform parent) => Instantiate(address, parent, false);
            public GameObject Instantiate(string address, Transform parent, bool worldPositionStays) => result;

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent) =>
                InstantiateAsync(address, parent, false);

            public UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool worldPositionStays)
            {
                InstantiateCount++;
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
            private readonly int throwEnterOnCall;
            private readonly int throwExitOnCall;
            private readonly bool throwComplete;
            private int enterCalls;
            private int exitCalls;

            internal TestTransition(
                bool throwEnter,
                bool throwExit,
                int throwEnterOnCall,
                int throwExitOnCall,
                bool throwComplete)
            {
                this.throwEnter = throwEnter;
                this.throwExit = throwExit;
                this.throwEnterOnCall = throwEnterOnCall;
                this.throwExitOnCall = throwExitOnCall;
                this.throwComplete = throwComplete;
                EnterException = new InvalidOperationException("enter failed");
            }

            internal Exception EnterException { get; }

            public void Initialize(Transform root) { }

            public UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                enterCalls++;
                return throwEnter || throwEnterOnCall == enterCalls
                    ? UniTask.FromException(EnterException)
                    : UniTask.CompletedTask;
            }

            public UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                exitCalls++;
                return throwExit || throwExitOnCall == exitCalls
                    ? UniTask.FromException(new InvalidOperationException("exit failed"))
                    : UniTask.CompletedTask;
            }

            public void CompleteImmediately(UITransitionDirection direction)
            {
                if (throwComplete)
                {
                    throw new InvalidOperationException("restore failed");
                }
            }
            public void Dispose() { }
        }
    }
}
