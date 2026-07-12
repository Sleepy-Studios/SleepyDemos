using System;
using System.Collections;
using System.Reflection;
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
    public sealed class UITransitionPlayModeTests
    {
        private GameObject root;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (root != null)
            {
                Object.Destroy(root);
            }

            yield return null;
        }

        [Test]
        public void View_DefaultTransitionFactory_UsesFadeScaleTransition()
        {
            var transition = new DefaultTransitionView().CreateTransition();

            Assert.That(transition, Is.TypeOf<FadeScaleUITransition>());
            transition.Dispose();
        }

        [UnityTest]
        public IEnumerator FadeScale_EnterAndExit_ReachesExpectedVisualStates()
        {
            root = new GameObject("FadeScaleRoot", typeof(RectTransform));
            var transition = CreateTransition(0.01f, 0.95f);
            transition.Initialize(root.transform);
            var canvasGroup = root.GetComponent<CanvasGroup>();

            yield return transition.EnterAsync(default, CancellationToken.None).ToCoroutine();

            Assert.That(canvasGroup, Is.Not.Null);
            Assert.That(canvasGroup.alpha, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Vector3.Distance(root.transform.localScale, Vector3.one), Is.LessThan(0.001f));

            yield return transition.ExitAsync(default, CancellationToken.None).ToCoroutine();

            Assert.That(canvasGroup.alpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(Vector3.Distance(root.transform.localScale, Vector3.one * 0.95f), Is.LessThan(0.001f));
            transition.Dispose();
        }

        [UnityTest]
        public IEnumerator FadeScale_CancelThenCompleteImmediately_ProducesDeterministicStates()
        {
            root = new GameObject("CancelableFadeScaleRoot", typeof(RectTransform));
            var transition = CreateTransition(1f, 0.9f);
            transition.Initialize(root.transform);
            using var cancellation = new CancellationTokenSource();
            var task = CaptureException(transition.EnterAsync(default, cancellation.Token)).Preserve();

            yield return null;
            cancellation.Cancel();
            Exception exception = null;
            yield return Capture(task, value => exception = value).ToCoroutine();

            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            transition.CompleteImmediately(UITransitionDirection.Enter);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(Vector3.Distance(root.transform.localScale, Vector3.one), Is.LessThan(0.001f));

            transition.CompleteImmediately(UITransitionDirection.Exit);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f));
            Assert.That(Vector3.Distance(root.transform.localScale, Vector3.one * 0.9f), Is.LessThan(0.001f));
            transition.Dispose();
        }

        [UnityTest]
        public IEnumerator FadeScale_CancelRacingZeroDurationCompletion_StillReturnsCanceled()
        {
            root = new GameObject("RacingFadeScaleRoot", typeof(RectTransform));
            var transition = CreateTransition(0.01f, 0.95f);
            transition.Initialize(root.transform);
            using var cancellation = new CancellationTokenSource();
            var task = CaptureException(transition.EnterAsync(default, cancellation.Token)).Preserve();
            var sequence = typeof(FadeScaleUITransition).GetField(
                "activeTween",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(transition);
            Assert.That(sequence, Is.Not.Null);
            var callbackField = sequence.GetType().GetField("onComplete");
            Assert.That(callbackField, Is.Not.Null);
            var complete = (Delegate)callbackField.GetValue(sequence);
            Action racingCompletion = () =>
            {
                cancellation.Cancel();
                complete?.DynamicInvoke();
            };
            callbackField.SetValue(sequence, Delegate.CreateDelegate(
                callbackField.FieldType,
                racingCompletion.Target,
                racingCompletion.Method));

            Exception exception = null;
            yield return Capture(task, value => exception = value).ToCoroutine();

            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            transition.Dispose();
        }

        [UnityTest]
        public IEnumerator FadeScale_DisposeDuringTween_CompletesAwaitWithoutHanging()
        {
            root = new GameObject("DisposedFadeScaleRoot", typeof(RectTransform));
            var transition = CreateTransition(1f, 0.95f);
            transition.Initialize(root.transform);
            var task = CaptureException(transition.EnterAsync(default, CancellationToken.None)).Preserve();

            yield return null;
            transition.Dispose();
            yield return null;

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Exception exception = null;
            yield return Capture(task, value => exception = value).ToCoroutine();
            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
        }

        [UnityTest]
        public IEnumerator FadeScale_RootDestroyed_KillsTweenAndCompletesAwait()
        {
            root = new GameObject("DestroyedFadeScaleRoot", typeof(RectTransform));
            var transition = CreateTransition(1f, 0.95f);
            transition.Initialize(root.transform);
            var task = CaptureException(transition.EnterAsync(default, CancellationToken.None)).Preserve();

            yield return null;
            Object.Destroy(root);
            root = null;
            yield return null;

            Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Exception exception = null;
            yield return Capture(task, value => exception = value).ToCoroutine();
            Assert.That(exception, Is.TypeOf<OperationCanceledException>());
            transition.Dispose();
        }

        [UnityTest]
        public IEnumerator FadeScale_Reentry_CancelsOldAwaitAndCompletesNewTransition()
        {
            root = new GameObject("ReenteredFadeScaleRoot", typeof(RectTransform));
            var transition = CreateTransition(0.05f, 0.95f);
            transition.Initialize(root.transform);
            var oldTask = CaptureException(
                transition.EnterAsync(default, CancellationToken.None)).Preserve();

            yield return null;
            var newTask = CaptureException(
                transition.ExitAsync(default, CancellationToken.None)).Preserve();
            Exception oldException = null;
            Exception newException = null;
            yield return Capture(oldTask, value => oldException = value).ToCoroutine();
            yield return Capture(newTask, value => newException = value).ToCoroutine();

            Assert.That(oldException, Is.TypeOf<OperationCanceledException>());
            Assert.That(newException, Is.Null);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(0f).Within(0.001f));
            Assert.That(Vector3.Distance(root.transform.localScale, Vector3.one * 0.95f),
                Is.LessThan(0.001f));
            transition.Dispose();
        }

        [Test]
        public void InteractionGate_NestedAcquireReleaseAndReinitialize_TracksCountAndGraphic()
        {
            root = new GameObject("GateRoot", typeof(RectTransform));
            var first = root.AddComponent<Image>();
            var gate = new UIInteractionGate();
            gate.Initialize(first);

            Assert.That(gate.IsBlocking, Is.False);
            Assert.That(first.raycastTarget, Is.False);
            gate.Acquire();
            gate.Acquire();
            Assert.That(gate.Count, Is.EqualTo(2));
            Assert.That(first.raycastTarget, Is.True);
            gate.Release();
            Assert.That(first.raycastTarget, Is.True);

            var secondRoot = new GameObject("SecondGateRoot", typeof(RectTransform));
            var second = secondRoot.AddComponent<Image>();
            gate.Initialize(second);
            Assert.That(first.raycastTarget, Is.False);
            Assert.That(second.raycastTarget, Is.True);
            gate.Release();
            Assert.That(second.raycastTarget, Is.False);
            Object.Destroy(secondRoot);
        }

        [Test]
        public void InteractionGate_ExtraRelease_LogsErrorAndClampsAtZero()
        {
            var gate = new UIInteractionGate();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("UIInteractionGate"));

            gate.Release();

            Assert.That(gate.Count, Is.Zero);
        }

        private static IUITransition CreateTransition(float duration, float hiddenScale)
        {
            return new FadeScaleUITransition(duration, hiddenScale);
        }

        private sealed class DefaultTransitionView : View
        {
            public IUITransition CreateTransition()
            {
                return CreateUITransition();
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

        private static async UniTask Capture<T>(UniTask<T> task, Action<T> capture)
        {
            capture(await task);
        }
    }
}
