using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    internal sealed class UINavigationCoordinator : IDisposable
    {
        private readonly object stateGate = new object();
        private readonly Queue<QueuedUIOperation> operations = new Queue<QueuedUIOperation>();
        private readonly Func<QueuedUIOperation, CancellationToken, UniTask<UIOperationResult>> execute;
        private CancellationTokenSource currentCancellation;
        private QueuedUIOperation current;
        private long nextOperationId;
        private bool isPumping;
        private bool disposed;

        internal UINavigationCoordinator(
            Func<QueuedUIOperation, CancellationToken, UniTask<UIOperationResult>> execute)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        internal bool HasCloseAllBarrier
        {
            get
            {
                lock (stateGate)
                {
                    return HasCloseAllBarrierLocked();
                }
            }
        }

        internal UniTask<UIOperationResult> Enqueue(
            UINavigationAction action,
            Type targetType,
            bool animated,
            CancellationToken callerCancellation,
            Action<View> configure = null,
            View targetView = null)
        {
            return Enqueue(
                action,
                targetType,
                animated,
                callerCancellation,
                out _,
                configure,
                targetView);
        }

        internal UniTask<UIOperationResult> Enqueue(
            UINavigationAction action,
            Type targetType,
            bool animated,
            CancellationToken callerCancellation,
            out bool closeAllBarrier,
            Action<View> configure = null,
            View targetView = null)
        {
            QueuedUIOperation operation;
            List<QueuedUIOperation> canceledPending = null;
            CancellationTokenSource cancellationToRequest = null;
            var startPump = false;
            var rejectDisposed = false;

            lock (stateGate)
            {
                closeAllBarrier = HasCloseAllBarrierLocked();
                operation = new QueuedUIOperation(
                    ++nextOperationId,
                    action,
                    targetType,
                    animated,
                    callerCancellation,
                    configure,
                    targetView);
                if (disposed)
                {
                    operation.State = QueuedUIOperationState.Completed;
                    rejectDisposed = true;
                }
                else
                {
                    if (action == UINavigationAction.CloseAll)
                    {
                        canceledPending = CancelAndDrainPendingLocked();
                        cancellationToRequest = currentCancellation;
                    }
                    else if (IsReverseOfCurrentLocked(operation))
                    {
                        cancellationToRequest = currentCancellation;
                    }

                    operation.State = QueuedUIOperationState.Pending;
                    operations.Enqueue(operation);
                    if (!isPumping)
                    {
                        isPumping = true;
                        startPump = true;
                    }
                }
            }

            CompleteCanceled(canceledPending);
            RequestCancellation(cancellationToRequest);
            if (rejectDisposed)
            {
                CompleteCanceled(operation);
            }
            else
            {
                RegisterPendingCancellation(operation);
            }

            if (startPump)
            {
                PumpAsync().Forget();
            }

            return operation.Completion.Task;
        }

        private bool HasCloseAllBarrierLocked()
        {
            if (current?.Action == UINavigationAction.CloseAll)
            {
                return true;
            }

            foreach (var operation in operations)
            {
                if (operation.State == QueuedUIOperationState.Pending &&
                    operation.Action == UINavigationAction.CloseAll)
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            List<QueuedUIOperation> canceledPending;
            CancellationTokenSource cancellationToRequest;
            lock (stateGate)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                canceledPending = CancelAndDrainPendingLocked();
                cancellationToRequest = currentCancellation;
            }

            RequestCancellation(cancellationToRequest);
            CompleteCanceled(canceledPending);
        }

        private async UniTaskVoid PumpAsync()
        {
            while (true)
            {
                QueuedUIOperation operation;
                CancellationTokenSource operationCancellation;
                CancellationTokenRegistration registration;
                lock (stateGate)
                {
                    operation = DequeueNextPendingLocked();
                    if (operation == null)
                    {
                        current = null;
                        currentCancellation = null;
                        isPumping = false;
                        return;
                    }

                    operation.State = QueuedUIOperationState.Current;
                    registration = TakeRegistrationLocked(operation);
                    operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                        operation.CallerCancellation);
                    current = operation;
                    currentCancellation = operationCancellation;
                }

                registration.Dispose();
                UIOperationResult result;
                try
                {
                    await UniTask.SwitchToMainThread();
                    operationCancellation.Token.ThrowIfCancellationRequested();
                    result = await execute(operation, operationCancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    result = UIOperationResult.Canceled(
                        operation.OperationId,
                        operation.Action,
                        null);
                }
                catch (Exception exception)
                {
                    result = UIOperationResult.Failed(
                        operation.OperationId,
                        operation.Action,
                        null,
                        exception);
                }
                finally
                {
                    lock (stateGate)
                    {
                        operation.State = QueuedUIOperationState.Completed;
                        if (ReferenceEquals(current, operation))
                        {
                            current = null;
                            currentCancellation = null;
                        }
                    }

                    operationCancellation.Dispose();
                }

                operation.Completion.TrySetResult(result);
            }
        }

        private void RegisterPendingCancellation(QueuedUIOperation operation)
        {
            if (!operation.CallerCancellation.CanBeCanceled)
            {
                return;
            }

            var registration = operation.CallerCancellation.Register(
                () => CancelPending(operation));
            var disposeImmediately = false;
            lock (stateGate)
            {
                if (operation.State == QueuedUIOperationState.Pending)
                {
                    operation.CancellationRegistration = registration;
                    operation.HasCancellationRegistration = true;
                }
                else
                {
                    disposeImmediately = true;
                }
            }

            if (disposeImmediately)
            {
                registration.Dispose();
            }
        }

        private void CancelPending(QueuedUIOperation operation)
        {
            CancellationTokenRegistration registration = default;
            var shouldComplete = false;
            lock (stateGate)
            {
                if (operation.State == QueuedUIOperationState.Pending)
                {
                    operation.State = QueuedUIOperationState.Completed;
                    registration = TakeRegistrationLocked(operation);
                    shouldComplete = true;
                }
            }

            if (!shouldComplete)
            {
                return;
            }

            operation.Completion.TrySetResult(UIOperationResult.Canceled(
                operation.OperationId,
                operation.Action,
                null));
            registration.Dispose();
        }

        private List<QueuedUIOperation> CancelAndDrainPendingLocked()
        {
            List<QueuedUIOperation> canceled = null;
            while (operations.Count > 0)
            {
                var operation = operations.Dequeue();
                if (operation.State != QueuedUIOperationState.Pending)
                {
                    continue;
                }

                operation.State = QueuedUIOperationState.Completed;
                canceled ??= new List<QueuedUIOperation>();
                canceled.Add(operation);
            }

            return canceled;
        }

        private QueuedUIOperation DequeueNextPendingLocked()
        {
            while (operations.Count > 0)
            {
                var operation = operations.Dequeue();
                if (operation.State == QueuedUIOperationState.Pending)
                {
                    return operation;
                }
            }

            return null;
        }

        private CancellationTokenRegistration TakeRegistrationLocked(QueuedUIOperation operation)
        {
            if (!operation.HasCancellationRegistration)
            {
                return default;
            }

            operation.HasCancellationRegistration = false;
            var registration = operation.CancellationRegistration;
            operation.CancellationRegistration = default;
            return registration;
        }

        private void CompleteCanceled(List<QueuedUIOperation> operationsToCancel)
        {
            if (operationsToCancel == null)
            {
                return;
            }

            foreach (var operation in operationsToCancel)
            {
                CompleteCanceled(operation);
            }
        }

        private void CompleteCanceled(QueuedUIOperation operation)
        {
            CancellationTokenRegistration registration;
            lock (stateGate)
            {
                registration = TakeRegistrationLocked(operation);
            }

            operation.Completion.TrySetResult(UIOperationResult.Canceled(
                operation.OperationId,
                operation.Action,
                null));
            registration.Dispose();
        }

        private bool IsReverseOfCurrentLocked(QueuedUIOperation operation)
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

        private static void RequestCancellation(CancellationTokenSource cancellation)
        {
            try
            {
                cancellation?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }

    internal enum QueuedUIOperationState
    {
        Created,
        Pending,
        Current,
        Completed
    }

    internal sealed class QueuedUIOperation
    {
        internal QueuedUIOperation(
            long operationId,
            UINavigationAction action,
            Type targetType,
            bool animated,
            CancellationToken callerCancellation,
            Action<View> configure,
            View targetView)
        {
            OperationId = operationId;
            Action = action;
            TargetType = targetType;
            Animated = animated;
            CallerCancellation = callerCancellation;
            Configure = configure;
            TargetView = targetView;
            Completion = new UniTaskCompletionSource<UIOperationResult>();
        }

        internal long OperationId { get; }
        internal UINavigationAction Action { get; }
        internal Type TargetType { get; }
        internal bool Animated { get; }
        internal CancellationToken CallerCancellation { get; }
        internal Action<View> Configure { get; }
        internal View TargetView { get; }
        internal UniTaskCompletionSource<UIOperationResult> Completion { get; }
        internal QueuedUIOperationState State { get; set; }
        internal CancellationTokenRegistration CancellationRegistration { get; set; }
        internal bool HasCancellationRegistration { get; set; }
    }
}
