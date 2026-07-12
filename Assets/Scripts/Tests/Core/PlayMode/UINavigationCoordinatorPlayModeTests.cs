using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Core.Tests.UI
{
    public sealed class UINavigationCoordinatorPlayModeTests
    {
        [UnityTest]
        public IEnumerator ConcurrentEnqueue_UsesSinglePumpAndOperationIdFifo()
        {
            var executionOrder = new List<long>();
            var concurrency = 0;
            var maxConcurrency = 0;
            var mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var coordinator = new UINavigationCoordinator(async (operation, _) =>
            {
                Assert.That(Thread.CurrentThread.ManagedThreadId, Is.EqualTo(mainThreadId));
                concurrency++;
                maxConcurrency = Math.Max(maxConcurrency, concurrency);
                executionOrder.Add(operation.OperationId);
                await UniTask.Yield();
                concurrency--;
                return UIOperationResult.Canceled(operation.OperationId, operation.Action, null);
            });

            var workers = new[]
            {
                Task.Run(() => EnqueueMany(coordinator, typeof(FirstMarker), 10)),
                Task.Run(() => EnqueueMany(coordinator, typeof(SecondMarker), 10))
            };
            var enqueueCompletion = Task.WhenAll(workers);
            while (!enqueueCompletion.IsCompleted)
            {
                yield return null;
            }

            var tasks = enqueueCompletion.Result.SelectMany(value => value).ToArray();
            var executionCompletion = Task.WhenAll(tasks);
            while (!executionCompletion.IsCompleted)
            {
                yield return null;
            }

            Assert.That(maxConcurrency, Is.EqualTo(1));
            Assert.That(executionOrder, Is.EqualTo(Enumerable.Range(1, 20).Select(value => (long)value)));
            coordinator.Dispose();
        }

        [UnityTest]
        public IEnumerator PendingCallerCancellation_CompletesBeforeCurrentAndHasNoSideEffects()
        {
            var currentGate = new UniTaskCompletionSource();
            var pendingExecutions = 0;
            var coordinator = new UINavigationCoordinator(async (operation, token) =>
            {
                if (operation.TargetType == typeof(FirstMarker))
                {
                    await currentGate.Task.AttachExternalCancellation(token);
                }
                else
                {
                    pendingExecutions++;
                }

                return UIOperationResult.Canceled(operation.OperationId, operation.Action, null);
            });

            var current = coordinator.Enqueue(
                UINavigationAction.Push, typeof(FirstMarker), true, CancellationToken.None);
            yield return null;
            using var cancellation = new CancellationTokenSource();
            var pending = coordinator.Enqueue(
                UINavigationAction.Push, typeof(SecondMarker), true, cancellation.Token);
            cancellation.Cancel();
            yield return null;

            Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(current.Status, Is.EqualTo(UniTaskStatus.Pending));
            Assert.That(pendingExecutions, Is.Zero);

            currentGate.TrySetResult();
            yield return current.ToCoroutine();
            UIOperationResult pendingResult = default;
            yield return pending.ToCoroutine(value => pendingResult = value);
            Assert.That(pendingResult.Status, Is.EqualTo(UIOperationStatus.Canceled));
            coordinator.Dispose();
        }

        [UnityTest]
        public IEnumerator Dispose_CompletesCurrentAndPendingCanceledWithoutDuplicateCallback()
        {
            var coordinator = new UINavigationCoordinator(async (operation, token) =>
            {
                await UniTask.Never(token);
                return UIOperationResult.Canceled(operation.OperationId, operation.Action, null);
            });
            var current = coordinator.Enqueue(
                UINavigationAction.Push, typeof(FirstMarker), true, CancellationToken.None);
            yield return null;
            using var cancellation = new CancellationTokenSource();
            var pending = coordinator.Enqueue(
                UINavigationAction.Push, typeof(SecondMarker), true, cancellation.Token);

            coordinator.Dispose();
            cancellation.Cancel();
            yield return null;
            yield return null;

            Assert.That(current.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            Assert.That(pending.Status, Is.EqualTo(UniTaskStatus.Succeeded));
            UIOperationResult currentResult = default;
            UIOperationResult pendingResult = default;
            yield return current.ToCoroutine(value => currentResult = value);
            yield return pending.ToCoroutine(value => pendingResult = value);
            Assert.That(currentResult.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(pendingResult.Status, Is.EqualTo(UIOperationStatus.Canceled));
        }

        [UnityTest]
        public IEnumerator InteractionGate_AcquiresForNavigationAndSkipsPreload()
        {
            var gateRoot = new GameObject("CoordinatorGate", typeof(RectTransform), typeof(Image));
            var gateImage = gateRoot.GetComponent<Image>();
            var gate = new UIInteractionGate();
            gate.Initialize(gateImage);
            var observedBlockingExecutions = 0;
            var coordinator = new UINavigationCoordinator((operation, token) =>
            {
                if (operation.Action == UINavigationAction.Preload)
                {
                    Assert.That(gate.Count, Is.Zero);
                }
                else
                {
                    Assert.That(gate.Count, Is.EqualTo(1));
                    observedBlockingExecutions++;
                }

                return UniTask.FromResult(UIOperationResult.Succeeded(
                    operation.OperationId,
                    operation.Action,
                    null));
            }, gate);

            yield return coordinator.Enqueue(
                UINavigationAction.Push, typeof(FirstMarker), true, CancellationToken.None).ToCoroutine();
            Assert.That(gate.Count, Is.Zero);
            yield return coordinator.Enqueue(
                UINavigationAction.Preload, typeof(SecondMarker), false, CancellationToken.None).ToCoroutine();
            Assert.That(gate.Count, Is.Zero);
            Assert.That(observedBlockingExecutions, Is.EqualTo(1));

            coordinator.Dispose();
            UnityEngine.Object.Destroy(gateRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InteractionGate_ReleasesWhenExecutorFailsOrCancels()
        {
            var gateRoot = new GameObject("CoordinatorFailureGate", typeof(RectTransform), typeof(Image));
            var gate = new UIInteractionGate();
            gate.Initialize(gateRoot.GetComponent<Image>());
            var coordinator = new UINavigationCoordinator((operation, token) =>
            {
                Assert.That(gate.Count, Is.EqualTo(1));
                if (operation.TargetType == typeof(FirstMarker))
                {
                    throw new InvalidOperationException("Expected executor failure");
                }

                throw new OperationCanceledException(token);
            }, gate);

            UIOperationResult failed = default;
            yield return coordinator.Enqueue(
                    UINavigationAction.Push, typeof(FirstMarker), true, CancellationToken.None)
                .ToCoroutine(value => failed = value);
            Assert.That(failed.Status, Is.EqualTo(UIOperationStatus.Failed));
            Assert.That(gate.Count, Is.Zero);

            UIOperationResult canceled = default;
            yield return coordinator.Enqueue(
                    UINavigationAction.Push, typeof(SecondMarker), true, CancellationToken.None)
                .ToCoroutine(value => canceled = value);
            Assert.That(canceled.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(gate.Count, Is.Zero);

            coordinator.Dispose();
            UnityEngine.Object.Destroy(gateRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InteractionGate_WhenExecutorCompletesOnWorker_ReleasesOnMainThread()
        {
            var gateRoot = new GameObject("CoordinatorWorkerGate", typeof(RectTransform), typeof(Image));
            var gate = new UIInteractionGate();
            gate.Initialize(gateRoot.GetComponent<Image>());
            var executorReached = new UniTaskCompletionSource();
            var workerCompletion = new UniTaskCompletionSource<UIOperationResult>();
            var coordinator = new UINavigationCoordinator((operation, token) =>
            {
                executorReached.TrySetResult();
                return workerCompletion.Task;
            }, gate);

            UIOperationResult result = default;
            var operationTask = coordinator.Enqueue(
                UINavigationAction.Push, typeof(FirstMarker), true, CancellationToken.None);
            yield return executorReached.Task.ToCoroutine();
            var worker = Task.Run(() => workerCompletion.TrySetResult(UIOperationResult.Canceled(
                1,
                UINavigationAction.Push,
                null)));
            while (!worker.IsCompleted)
            {
                yield return null;
            }

            yield return operationTask.ToCoroutine(value => result = value);

            Assert.That(result.Exception, Is.Null, result.Exception?.ToString());
            Assert.That(result.Status, Is.EqualTo(UIOperationStatus.Canceled));
            Assert.That(gate.Count, Is.Zero);
            Assert.That(gateRoot.GetComponent<Image>().raycastTarget, Is.False);
            coordinator.Dispose();
            UnityEngine.Object.Destroy(gateRoot);
            yield return null;
        }

        private static Task<UIOperationResult>[] EnqueueMany(
            UINavigationCoordinator coordinator,
            Type targetType,
            int count)
        {
            var tasks = new Task<UIOperationResult>[count];
            for (int i = 0; i < count; i++)
            {
                tasks[i] = coordinator.Enqueue(
                    UINavigationAction.Push,
                    targetType,
                    true,
                    CancellationToken.None).AsTask();
            }

            return tasks;
        }

        private sealed class FirstMarker { }
        private sealed class SecondMarker { }
    }
}
