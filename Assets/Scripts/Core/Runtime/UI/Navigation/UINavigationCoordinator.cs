using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    internal sealed class UINavigationCoordinator : IDisposable
    {
        private readonly Queue<QueuedUIOperation> operations = new Queue<QueuedUIOperation>();
        private readonly Func<QueuedUIOperation, CancellationToken, UniTask<UIOperationResult>> execute;
        private CancellationTokenSource lifetimeCancellation = new CancellationTokenSource();
        private CancellationTokenSource currentCancellation;
        private QueuedUIOperation current;
        private long nextOperationId;
        private bool isPumping;

        internal UINavigationCoordinator(
            Func<QueuedUIOperation, CancellationToken, UniTask<UIOperationResult>> execute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        internal UniTask<UIOperationResult> Enqueue(
            UINavigationAction action,
            Type targetType,
            bool animated,
            CancellationToken callerCancellation)
        {
            var operation = new QueuedUIOperation(
                Interlocked.Increment(ref nextOperationId),
                action,
                targetType,
                animated,
                callerCancellation);

            if (action == UINavigationAction.CloseAll)
            {
                currentCancellation?.Cancel();
                while (operations.Count > 0)
                {
                    var pending = operations.Dequeue();
                    pending.Completion.TrySetResult(UIOperationResult.Canceled(
                        pending.OperationId,
                        pending.Action,
                        null));
                }
            }
            else if (IsReverseOfCurrent(operation))
            {
                currentCancellation?.Cancel();
            }

            operations.Enqueue(operation);
            if (!isPumping)
            {
                PumpAsync().Forget();
            }

            return operation.Completion.Task;
        }

        public void Dispose()
        {
            lifetimeCancellation.Cancel();
            currentCancellation?.Cancel();
            lifetimeCancellation.Dispose();
            currentCancellation?.Dispose();
        }

        private async UniTaskVoid PumpAsync()
        {
            isPumping = true;
            try
            {
                while (operations.Count > 0)
                {
                    current = operations.Dequeue();
                    if (current.CallerCancellation.IsCancellationRequested)
                    {
                        current.Completion.TrySetResult(UIOperationResult.Canceled(
                            current.OperationId,
                            current.Action,
                            null));
                        current = null;
                        continue;
                    }

                    currentCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        lifetimeCancellation.Token,
                        current.CallerCancellation);
                    try
                    {
                        var result = await execute(current, currentCancellation.Token);
                        current.Completion.TrySetResult(result);
                    }
                    catch (OperationCanceledException)
                    {
                        current.Completion.TrySetResult(UIOperationResult.Canceled(
                            current.OperationId,
                            current.Action,
                            null));
                    }
                    catch (Exception exception)
                    {
                        current.Completion.TrySetResult(UIOperationResult.Failed(
                            current.OperationId,
                            current.Action,
                            null,
                            exception));
                    }
                    finally
                    {
                        currentCancellation.Dispose();
                        currentCancellation = null;
                        current = null;
                    }
                }
            }
            finally
            {
                isPumping = false;
                if (operations.Count > 0)
                {
                    PumpAsync().Forget();
                }
            }
        }

        private bool IsReverseOfCurrent(QueuedUIOperation operation)
        {
            if (current?.TargetType == null || current.TargetType != operation.TargetType)
            {
                return false;
            }

            var currentShows = current.Action == UINavigationAction.Push ||
                               current.Action == UINavigationAction.Replace;
            var nextCloses = operation.Action == UINavigationAction.Close;
            var currentCloses = current.Action == UINavigationAction.Close;
            var nextShows = operation.Action == UINavigationAction.Push ||
                            operation.Action == UINavigationAction.Replace;
            return (currentShows && nextCloses) || (currentCloses && nextShows);
        }
    }

    internal sealed class QueuedUIOperation
    {
        internal QueuedUIOperation(
            long operationId,
            UINavigationAction action,
            Type targetType,
            bool animated,
            CancellationToken callerCancellation)
        {
            OperationId = operationId;
            Action = action;
            TargetType = targetType;
            Animated = animated;
            CallerCancellation = callerCancellation;
            Completion = new UniTaskCompletionSource<UIOperationResult>();
        }

        internal long OperationId { get; }
        internal UINavigationAction Action { get; }
        internal Type TargetType { get; }
        internal bool Animated { get; }
        internal CancellationToken CallerCancellation { get; }
        internal UniTaskCompletionSource<UIOperationResult> Completion { get; }
    }
}
