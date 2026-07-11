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
