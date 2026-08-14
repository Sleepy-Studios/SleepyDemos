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
        public IEnumerator WidgetAboveModal_DoesNotHideOrMoveTopModalMask()
        {
            TestViewRegistry.Register<FirstPage>();
            TestViewRegistry.Register<MaskedModal>();
            TestViewRegistry.Register<TestWidget>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            yield return AwaitResult(UIManager.Instance.ShowAsync<MaskedModal>(), _ => { });
            var modal = UIManager.Instance.Get<MaskedModal>();
            var mask = UIRootManager.Instance.Mask;
            var expectedParent = modal.transform.parent;
            var expectedSibling = mask.transform.GetSiblingIndex();
            var expectedColor = mask.color;

            yield return AwaitResult(UIManager.Instance.ShowAsync<TestWidget>(), _ => { });

            Assert.That(mask.transform.parent, Is.SameAs(expectedParent));
            Assert.That(mask.transform.GetSiblingIndex(), Is.EqualTo(expectedSibling));
            Assert.That(mask.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(mask.color, Is.EqualTo(expectedColor));
            Assert.That(mask.color.a, Is.EqualTo(expectedColor.a));

            yield return AwaitResult(UIManager.Instance.CloseAsync<TestWidget>(), _ => { });

            Assert.That(mask.transform.parent, Is.SameAs(expectedParent));
            Assert.That(mask.transform.GetSiblingIndex(), Is.EqualTo(expectedSibling));
            Assert.That(mask.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(mask.color, Is.EqualTo(expectedColor));
        }

        [UnityTest]
        public IEnumerator TipWidgetNavigation_KeepsInteractionGateAboveViewContent()
        {
            TestViewRegistry.Register<TipWidget>();

            yield return AwaitResult(UIManager.Instance.ShowAsync<TipWidget>(), _ => { });

            var tipLayer = UIRootManager.Instance.GetRoot(UILayer.Tip);
            var content = tipLayer.Find("TipContent");
            var gate = tipLayer.Find("InteractionGate");
            var view = UIManager.Instance.Get<TipWidget>();
            Assert.That(content, Is.Not.Null);
            Assert.That(gate, Is.Not.Null);
            Assert.That(view.transform.parent, Is.SameAs(content));
            Assert.That(gate.GetSiblingIndex(), Is.EqualTo(tipLayer.childCount - 1));
            Assert.That(gate.GetSiblingIndex(), Is.GreaterThan(content.GetSiblingIndex()));
        }

        [UnityTest]
        public IEnumerator DefaultFade_AnimatedFalse_UsesImmediateEnterAndExitStates()
        {
            TestViewRegistry.Register<DefaultFadePage>();
            yield return AwaitResult(
                UIManager.Instance.ShowAsync<DefaultFadePage>(new UIShowOptions(false)),
                _ => { });
            var view = UIManager.Instance.Get<DefaultFadePage>();
            var canvasGroup = view.transform.GetComponent<CanvasGroup>();

            Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
            Assert.That(Vector3.Distance(view.transform.localScale, Vector3.one), Is.LessThan(0.001f));

            yield return AwaitResult(
                UIManager.Instance.CloseAsync<DefaultFadePage>(animated: false),
                _ => { });

            Assert.That(view.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(canvasGroup.alpha, Is.EqualTo(0f));
            Assert.That(Vector3.Distance(view.transform.localScale, Vector3.one * 0.95f),
                Is.LessThan(0.001f));
        }

        [UnityTest]
        public IEnumerator Rollback_RealDefaultTransitionsMatchVisibleAndHiddenSnapshotStates()
        {
            TestViewRegistry.Register<DefaultFadePage>();
            TestViewRegistry.Register<DefaultFadeWidget>();
            yield return AwaitResult(
                UIManager.Instance.ShowAsync<DefaultFadePage>(new UIShowOptions(false)),
                _ => { });
            yield return UIManager.Instance.Preload<DefaultFadeWidget>().ToCoroutine();
            var visible = UIManager.Instance.Get<DefaultFadePage>();
            var hidden = UIManager.Instance.Get<DefaultFadeWidget>();
            hidden.UITransition.CompleteImmediately(UITransitionDirection.Enter);

            TestViewRegistry.Register<SecondPage>(throwEnter: true);
            UIOperationResult result = default;
            yield return AwaitResult(
                UIManager.Instance.ShowAsync<SecondPage>(),
                value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(visible.State, Is.EqualTo(ViewState.Visible));
            Assert.That(visible.transform.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(Vector3.Distance(visible.transform.localScale, Vector3.one), Is.LessThan(0.001f));
            Assert.That(hidden.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(hidden.transform.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
            Assert.That(Vector3.Distance(hidden.transform.localScale, Vector3.one * 0.95f),
                Is.LessThan(0.001f));
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
            var root = UIRootManager.Instance.GetRoot(UILayer.Pop);
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
            var expectedViewActive = oldPage.gameObject.activeSelf;
            var expectedMaskColor = mask.color;
            var expectedCurrentName = UIManager.Instance.CurrentUIName;
            var expectedLastName = UIManager.Instance.LastCloseName;

            TestViewRegistry.Register<SecondPage>(throwEnter: true);
            yield return AwaitResult(UIManager.Instance.ShowAsync<SecondPage>(), _ => { });

            Assert.That(oldPage.transform.parent, Is.SameAs(expectedViewParent));
            Assert.That(oldPage.gameObject.activeSelf, Is.EqualTo(expectedViewActive));
            Assert.That(mask.transform.parent, Is.SameAs(oldPage.transform.parent));
            Assert.That(mask.transform.GetSiblingIndex(), Is.LessThan(oldPage.transform.GetSiblingIndex()));
            Assert.That(mask.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(mask.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(mask.color, Is.EqualTo(expectedMaskColor));
            Assert.That(button.interactable, Is.True);
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

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: restore failed");
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: destroy failed");
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
        public IEnumerator TypedShowAsync_ConfiguresDataBeforeLoadAndOnShow()
        {
            TestViewRegistry.Register<DataPage>();

            UIOperationResult result = default;
            yield return AwaitResult(
                UIManager.Instance.ShowAsync<DataPage, string>("typed-data"),
                value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(TestViewRegistry.Events, Is.EqualTo(new[]
            {
                "data:typed-data", "DataPage.load", "show:typed-data"
            }));
        }

        [UnityTest]
        public IEnumerator CloseExpectedInstance_DoesNotCloseNewSameTypeSessionView()
        {
            TestViewRegistry.Register<FirstPage>();
            UIOperationResult firstShow = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), value => firstShow = value);
            var oldSessionView = firstShow.View;
            yield return AwaitResult(UIManager.Instance.CloseAsync(oldSessionView, false), _ => { });

            TestViewRegistry.Register<FirstPage>();
            UIOperationResult secondShow = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), value => secondShow = value);
            UIOperationResult staleClose = default;
            yield return AwaitResult(
                UIManager.Instance.CloseAsync(oldSessionView, false),
                value => staleClose = value);

            Assert.That(staleClose.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(UIManager.Instance.Get<FirstPage>(), Is.SameAs(secondShow.View));
            Assert.That(secondShow.View.State, Is.EqualTo(ViewState.Visible));
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
            Assert.That(second.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(second.View, Is.Null);
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

        [UnityTest]
        public IEnumerator ShowHidePreviousFalse_KeepsOldPageVisibleAndCloseDoesNotReenterIt()
        {
            TestViewRegistry.Register<FirstPage>();
            TestViewRegistry.Register<SecondPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            var oldPage = UIManager.Instance.Get<FirstPage>();

            UIManager.Instance.Show<SecondPage>(false);
            for (int i = 0; i < 30 && UIManager.Instance.Get<SecondPage>()?.State != ViewState.Visible; i++)
            {
                yield return null;
            }

            Assert.That(oldPage.State, Is.EqualTo(ViewState.Visible));
            yield return AwaitResult(UIManager.Instance.CloseAsync<SecondPage>(), _ => { });
            Assert.That(oldPage.State, Is.EqualTo(ViewState.Visible));
            Assert.That(TestViewRegistry.Events.Count(value => value == "FirstPage.enter"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LegacySyncShow_ConstructsOutsideCoordinatorGate()
        {
            LockProbePage.Reset();
            TestViewRegistry.Register<LockProbePage>();

            UIManager.Instance.Show<LockProbePage>();
            yield return null;

            Assert.That(LockProbePage.CoordinatorGateWasAvailable, Is.True);
        }

        [UnityTest]
        public IEnumerator LegacySyncShowFromWorker_ReturnsNullAndConstructsOnMainThread()
        {
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            WorkerPage.Reset();
            TestViewRegistry.Register<WorkerPage>();
            var workerTask = System.Threading.Tasks.Task.Run(
                () => UIManager.Instance.Show<WorkerPage>());
            while (!workerTask.IsCompleted)
            {
                yield return null;
            }

            for (int i = 0; i < 30 && WorkerPage.ConstructorThreadId == 0; i++)
            {
                yield return null;
            }

            Assert.That(workerTask.Result, Is.Null);
            Assert.That(WorkerPage.ConstructorThreadId, Is.EqualTo(mainThreadId));
        }

        [UnityTest]
        public IEnumerator LegacySyncShowDuringCurrentCacheOnlyClose_RejectsDestroyingCandidate()
        {
            RacePage.Reset();
            var oldLoader = TestViewRegistry.Register<RacePage>();
            var old = UIManager.Instance.cacheStack.GetOrCreateView<RacePage>();
            var childLoader = TestViewRegistry.Register<DelayedChildPage>(delay: true);
            var child = new DelayedChildPage();
            var childLoad = child.LoadAsync(
                    UIRootManager.Instance.GetRoot(child.Level), CancellationToken.None)
                .SuppressCancellationThrow();
            yield return null;
            old.AddSubView(child);
            var newLoader = TestViewRegistry.Register<RacePage>();
            var closeTask = UIManager.Instance.CloseAsync<RacePage>();
            yield return null;
            Assert.That(old.State, Is.EqualTo(ViewState.Destroying));

            var returned = UIManager.Instance.Show<RacePage>();
            childLoader.Complete(new GameObject(nameof(DelayedChildPage)));
            yield return childLoad.ToCoroutine();
            yield return AwaitResult(closeTask, _ => { });
            for (int i = 0; i < 60 && UIManager.Instance.Get<RacePage>()?.State != ViewState.Visible; i++)
            {
                yield return null;
            }

            var actual = UIManager.Instance.Get<RacePage>();
            Assert.That(returned, Is.Null);
            Assert.That(old.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(actual, Is.Not.Null.And.Not.SameAs(old));
            Assert.That(actual.State, Is.EqualTo(ViewState.Visible));
            Assert.That(RacePage.Instances.Count, Is.EqualTo(2));
            Assert.That(oldLoader.DisposeCount, Is.EqualTo(1));
            Assert.That(newLoader.InstantiateCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LegacySyncShowBehindPendingClose_RejectsCachedCandidate()
        {
            RacePage.Reset();
            var oldLoader = TestViewRegistry.Register<RacePage>();
            var old = UIManager.Instance.cacheStack.GetOrCreateView<RacePage>();
            var slowLoader = TestViewRegistry.Register<SlowPage>(delay: true);
            var slowShow = UIManager.Instance.ShowAsync<SlowPage>();
            yield return null;
            var closeTask = UIManager.Instance.CloseAsync<RacePage>();
            var newLoader = TestViewRegistry.Register<RacePage>();

            var returned = UIManager.Instance.Show<RacePage>();
            slowLoader.Complete(new GameObject(nameof(SlowPage)));
            yield return AwaitResult(slowShow, _ => { });
            yield return AwaitResult(closeTask, _ => { });
            for (int i = 0; i < 60 && UIManager.Instance.Get<RacePage>()?.State != ViewState.Visible; i++)
            {
                yield return null;
            }

            var actual = UIManager.Instance.Get<RacePage>();
            Assert.That(returned, Is.Null);
            Assert.That(old.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(actual, Is.Not.Null.And.Not.SameAs(old));
            Assert.That(actual.State, Is.EqualTo(ViewState.Visible));
            Assert.That(RacePage.Instances.Count, Is.EqualTo(2));
            Assert.That(oldLoader.DisposeCount, Is.EqualTo(1));
            Assert.That(newLoader.InstantiateCount, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator LegacyShowSynchronousEnterFailure_ReturnsBoundDestroyedInstanceWithoutRecreation()
        {
            SyncFailPage.Reset();
            TestViewRegistry.Register<SyncFailPage>(throwEnter: true);
            TestViewRegistry.Register<SyncFailPage>();

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: enter failed");
            var returned = UIManager.Instance.Show<SyncFailPage>();
            yield return null;

            Assert.That(SyncFailPage.Instances.Count, Is.EqualTo(1));
            Assert.That(returned, Is.SameAs(SyncFailPage.Instances[0]));
            Assert.That(returned.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(UIManager.Instance.Get<SyncFailPage>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator ReplaceModalFailure_DestroysAndRemovesCreatedTarget()
        {
            TestViewRegistry.Register<FirstPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            var old = UIManager.Instance.Get<FirstPage>();
            TestViewRegistry.Register<TestModal>();

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ReplaceAsync<TestModal>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.View.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(UIManager.Instance.Get<TestModal>(), Is.Null);
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(old));
        }

        [UnityTest]
        public IEnumerator ReplaceExistingModalFailure_PreservesInstanceStackAndPresentation()
        {
            TestViewRegistry.Register<FirstPage>();
            TestViewRegistry.Register<MaskedModal>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            yield return AwaitResult(UIManager.Instance.ShowAsync<MaskedModal>(), _ => { });
            var page = UIManager.Instance.Get<FirstPage>();
            var modal = UIManager.Instance.Get<MaskedModal>();
            var pageState = page.State;
            var mask = UIRootManager.Instance.Mask;
            var button = mask.GetComponent<Button>();
            mask.transform.localScale = new Vector3(0.2f, 0.3f, 0.4f);
            var expectedMaskParent = mask.transform.parent;
            var expectedMaskColor = mask.color;
            var expectedCurrentName = UIManager.Instance.CurrentUIName;
            var expectedLastName = UIManager.Instance.LastCloseName;

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ReplaceAsync<MaskedModal>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.View, Is.SameAs(modal));
            Assert.That(UIManager.Instance.Get<MaskedModal>(), Is.SameAs(modal));
            Assert.That(modal.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(modal));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(2));
            Assert.That(page.State, Is.EqualTo(pageState));
            Assert.That(UIManager.Instance.CurrentUIName, Is.EqualTo(expectedCurrentName));
            Assert.That(UIManager.Instance.LastCloseName, Is.EqualTo(expectedLastName));
            Assert.That(mask.transform.parent, Is.SameAs(expectedMaskParent));
            Assert.That(mask.transform.GetSiblingIndex(), Is.LessThan(modal.transform.GetSiblingIndex()));
            Assert.That(mask.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(mask.transform.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(mask.color, Is.EqualTo(expectedMaskColor));
            Assert.That(button.interactable, Is.True);
        }

        [UnityTest]
        public IEnumerator ReplaceWidgetFailure_DestroysAndRemovesCreatedTarget()
        {
            TestViewRegistry.Register<FirstPage>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            var old = UIManager.Instance.Get<FirstPage>();
            TestViewRegistry.Register<TestWidget>();

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ReplaceAsync<TestWidget>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.View.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(UIManager.Instance.Get<TestWidget>(), Is.Null);
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(old));
        }

        [UnityTest]
        public IEnumerator ReplacePreloadedWidgetFailure_PreservesExistingHiddenInstance()
        {
            TestViewRegistry.Register<FirstPage>();
            TestViewRegistry.Register<TestWidget>();
            yield return AwaitResult(UIManager.Instance.ShowAsync<FirstPage>(), _ => { });
            yield return UIManager.Instance.Preload<TestWidget>().ToCoroutine();
            var page = UIManager.Instance.Get<FirstPage>();
            var widget = UIManager.Instance.Get<TestWidget>();
            var expectedCurrentName = UIManager.Instance.CurrentUIName;
            var expectedLastName = UIManager.Instance.LastCloseName;

            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ReplaceAsync<TestWidget>(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.View, Is.SameAs(widget));
            Assert.That(UIManager.Instance.Get<TestWidget>(), Is.SameAs(widget));
            Assert.That(widget.State, Is.EqualTo(ViewState.LoadedHidden));
            Assert.That(UIManager.Instance.GetStackTopView(), Is.SameAs(page));
            Assert.That(page.State, Is.EqualTo(ViewState.Visible));
            Assert.That(UIManager.Instance.StackCount, Is.EqualTo(1));
            Assert.That(UIManager.Instance.CurrentUIName, Is.EqualTo(expectedCurrentName));
            Assert.That(UIManager.Instance.LastCloseName, Is.EqualTo(expectedLastName));
        }

        [UnityTest]
        public IEnumerator OnBeforeOpenSubscribers_AwaitSequentiallyBeforeEnter()
        {
            var gate = new UniTaskCompletionSource();
            var events = TestViewRegistry.Events;
            TestViewRegistry.Register<SecondPage>();
            async UniTask First(View _)
            {
                events.Add("hook1.start");
                await gate.Task;
                events.Add("hook1.end");
            }
            UniTask Second(View _)
            {
                events.Add("hook2");
                return UniTask.CompletedTask;
            }
            UIManager.Instance.OnBeforeOpen += First;
            UIManager.Instance.OnBeforeOpen += Second;
            var task = UIManager.Instance.ShowAsync<SecondPage>();
            yield return null;
            var beforeRelease = events.ToArray();
            gate.TrySetResult();
            yield return AwaitResult(task, _ => { });
            UIManager.Instance.OnBeforeOpen -= First;
            UIManager.Instance.OnBeforeOpen -= Second;

            Assert.That(beforeRelease, Is.EqualTo(new[] { "SecondPage.load", "hook1.start" }));
            Assert.That(events, Is.EqualTo(new[]
                { "SecondPage.load", "hook1.start", "hook1.end", "hook2", "SecondPage.enter" }));
        }

        [UnityTest]
        public IEnumerator OnBeforeOpenFirstFailure_StopsLaterSubscriberAndPreservesOriginal()
        {
            var exception = new InvalidOperationException("hook failed");
            var secondCalled = false;
            TestViewRegistry.Register<SecondPage>();
            UniTask First(View _) => UniTask.FromException(exception);
            UniTask Second(View _)
            {
                secondCalled = true;
                return UniTask.CompletedTask;
            }
            UIManager.Instance.OnBeforeOpen += First;
            UIManager.Instance.OnBeforeOpen += Second;
            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.ShowAsync<SecondPage>(), value => result = value);
            UIManager.Instance.OnBeforeOpen -= First;
            UIManager.Instance.OnBeforeOpen -= Second;

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(result.Exception, Is.SameAs(exception));
            Assert.That(secondCalled, Is.False);
            Assert.That(UIManager.Instance.Get<SecondPage>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator OnBeforeOpenCancellation_StopsLaterSubscriberAndEnter()
        {
            using var cancellation = new CancellationTokenSource();
            var gate = new UniTaskCompletionSource();
            var secondCalled = false;
            TestViewRegistry.Register<SecondPage>();
            async UniTask First(View _)
            {
                await gate.Task;
            }
            UniTask Second(View _)
            {
                secondCalled = true;
                return UniTask.CompletedTask;
            }
            UIManager.Instance.OnBeforeOpen += First;
            UIManager.Instance.OnBeforeOpen += Second;
            var task = UIManager.Instance.ShowAsync<SecondPage>(cancellationToken: cancellation.Token);
            yield return null;
            cancellation.Cancel();
            gate.TrySetResult();
            UIOperationResult result = default;
            yield return AwaitResult(task, value => result = value);
            UIManager.Instance.OnBeforeOpen -= First;
            UIManager.Instance.OnBeforeOpen -= Second;

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(secondCalled, Is.False);
            Assert.That(TestViewRegistry.Events, Does.Not.Contain("SecondPage.enter"));
            Assert.That(UIManager.Instance.Get<SecondPage>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator EmptyCloseAll_ReturnsSucceededWithNullView()
        {
            UIOperationResult result = default;
            yield return AwaitResult(UIManager.Instance.CloseAllAsync(), value => result = value);

            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            Assert.That(result.View, Is.Null);
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
            public override UILayer Level => UILayer.Pop;
            public override MaskType Mask => MaskType.CloseRaycast;
        }

        private sealed class PersistentPage : NavigationTestPage
        {
            public override bool DestroyOnHide => false;
        }

        private sealed class SyncFailPage : NavigationTestPage
        {
            internal static readonly List<SyncFailPage> Instances = new();

            public SyncFailPage()
            {
                Instances.Add(this);
            }

            internal static void Reset()
            {
                Instances.Clear();
            }
        }

        private sealed class LockProbePage : NavigationTestPage
        {
            internal static bool CoordinatorGateWasAvailable { get; private set; }

            public LockProbePage()
            {
                var completed = new ManualResetEventSlim();
                System.Threading.Tasks.Task.Run(() =>
                {
                    _ = UIManager.Instance.HasCloseAllBarrier;
                    completed.Set();
                });
                CoordinatorGateWasAvailable = completed.Wait(500);
            }

            internal static void Reset() => CoordinatorGateWasAvailable = false;
        }

        private sealed class WorkerPage : NavigationTestPage
        {
            internal static int ConstructorThreadId { get; private set; }

            public WorkerPage()
            {
                ConstructorThreadId = Thread.CurrentThread.ManagedThreadId;
            }

            internal static void Reset() => ConstructorThreadId = 0;
        }

        private sealed class RacePage : NavigationTestPage
        {
            internal static readonly List<RacePage> Instances = new();

            public RacePage()
            {
                Instances.Add(this);
            }

            internal static void Reset() => Instances.Clear();
        }

        private sealed class DelayedChildPage : NavigationTestPage { }

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

        private sealed class MaskedModal : NavigationTestPage
        {
            public override UILayer Level => UILayer.Pop;
            public override MaskType Mask => MaskType.CloseRaycast;
        }

        private sealed class TestWidget : NavigationTestPage
        {
            public override UILayer Level => UILayer.Decorate;
        }

        private sealed class TipWidget : NavigationTestPage
        {
            public override UILayer Level => UILayer.Tip;
        }

        private class DefaultFadePage : View
        {
            public DefaultFadePage()
            {
                var entry = TestViewRegistry.Take(GetType());
                Loader = entry.Item1;
            }

            public override string Address => GetType().Name;
            public override bool DestroyOnHide => false;
        }

        private sealed class DefaultFadeWidget : DefaultFadePage
        {
            public override UILayer Level => UILayer.Decorate;
        }

        private static class TestViewRegistry
        {
            private static readonly Dictionary<Type, Queue<(TestLoader, List<string>)>> Entries = new();
            private static readonly List<TestLoader> Loaders = new();
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
                Loaders.Add(loader);
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
                foreach (var loader in Loaders)
                {
                    loader.Dispose();
                }

                Loaders.Clear();
                Entries.Clear();
                Events.Clear();
            }
        }

        private sealed class TestLoader : IResourceLoader
        {
            private readonly List<string> events;
            private readonly string name;
            private readonly bool delay;
            private GameObject result;
            private UniTaskCompletionSource<GameObject> completionSource;
            private bool released;
            private bool disposed;

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
            internal int DisposeCount { get; private set; }
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

            internal void Complete(GameObject instance)
            {
                result = instance;
                completionSource.TrySetResult(instance);
            }

            public T LoadAsset<T>(string address) where T : Object => null;
            public UniTask<T> LoadAssetAsync<T>(string address) where T : Object => UniTask.FromResult<T>(null);
            public void ReleaseAsset(Object asset) { }

            public void ReleaseInstance(GameObject instance)
            {
                ReleaseCount++;
                released = true;
                if (instance != null)
                {
                    Object.Destroy(instance);
                }
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                DisposeCount++;
                if (!released && result != null)
                {
                    Object.Destroy(result);
                }
            }

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
            internal Exception ExitException { get; } = new InvalidOperationException("exit failed");

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
                    ? UniTask.FromException(ExitException)
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
