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
        private readonly IUIInteractionGate interactionGate;
        private CancellationTokenSource currentCancellation;
        private QueuedUIOperation current;
        private long nextOperationId;
        private bool isPumping;
        private bool disposed;

        internal UINavigationCoordinator(
            Func<QueuedUIOperation, CancellationToken, UniTask<UIOperationResult>> execute,
            IUIInteractionGate interactionGate = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.interactionGate = interactionGate;
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
            View targetView = null,
            bool hidePrevious = true)
        {
            return EnqueueCore(
                action,
                targetType,
                animated,
                callerCancellation,
                out _,
                out _,
                configure,
                targetView,
                hidePrevious,
                false);
        }

        internal UniTask<UIOperationResult> Enqueue(
            UINavigationAction action,
            Type targetType,
            bool animated,
            CancellationToken callerCancellation,
            out bool closeAllBarrier,
            Action<View> configure = null,
            View targetView = null,
            bool hidePrevious = true)
        {
            return EnqueueCore(
                action,
                targetType,
                animated,
                callerCancellation,
                out closeAllBarrier,
                out _,
                configure,
                targetView,
                hidePrevious,
                false);
        }

        internal UniTask<UIOperationResult> EnqueueLegacyShow(
            Type targetType,
            bool animated,
            out bool closeAllBarrier,
            out bool candidateAdopted,
            Action<View> configure,
            View candidate,
            bool hidePrevious = true)
        {
            return EnqueueCore(
                UINavigationAction.Push,
                targetType,
                animated,
                CancellationToken.None,
                out closeAllBarrier,
                out candidateAdopted,
                configure,
                candidate,
                hidePrevious,
                true);
        }

        private UniTask<UIOperationResult> EnqueueCore(
            UINavigationAction action,
            Type targetType,
            bool animated,
            CancellationToken callerCancellation,
            out bool closeAllBarrier,
            out bool targetAdopted,
            Action<View> configure,
            View targetView,
            bool hidePrevious,
            bool requireBarrierFreeAdoption)
        {
            QueuedUIOperation operation;
            List<QueuedUIOperation> canceledPending = null;
            CancellationTokenSource cancellationToRequest = null;
            var startPump = false;
            var rejectDisposed = false;

            lock (stateGate)
            {
                closeAllBarrier = HasCloseAllBarrierLocked();
                var hasEarlierDestructiveOperation = requireBarrierFreeAdoption &&
                                                    HasDestructiveOperationLocked(targetType);
                targetAdopted = targetView != null &&
                                (!requireBarrierFreeAdoption ||
                                 (!closeAllBarrier && !hasEarlierDestructiveOperation && !disposed));
                operation = new QueuedUIOperation(
                    ++nextOperationId,
                    action,
                    targetType,
                    animated,
                    callerCancellation,
                    configure,
                    targetAdopted ? targetView : null,
                    hidePrevious);
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

        private bool HasDestructiveOperationLocked(Type targetType)
        {
            if (IsDestructiveOperation(current, targetType))
            {
                return true;
            }

            foreach (var operation in operations)
            {
                if (operation.State == QueuedUIOperationState.Pending &&
                    IsDestructiveOperation(operation, targetType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsDestructiveOperation(QueuedUIOperation operation, Type targetType)
        {
            if (operation == null)
            {
                return false;
            }

            return operation.Action == UINavigationAction.CloseAll ||
                   operation.Action == UINavigationAction.Back ||
                   operation.Action == UINavigationAction.Replace ||
                   (operation.Action == UINavigationAction.Close && operation.TargetType == targetType);
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
                var gateAcquired = false;
                try
                {
                    await UniTask.SwitchToMainThread();
                    operationCancellation.Token.ThrowIfCancellationRequested();
                    if (operation.Action != UINavigationAction.Preload && interactionGate != null)
                    {
                        interactionGate.Acquire();
                        gateAcquired = true;
                    }

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
                    await UniTask.SwitchToMainThread();
                    if (gateAcquired)
                    {
                        try
                        {
                            interactionGate.Release();
                        }
                        catch (Exception exception)
                        {
                            UnityEngine.Debug.LogException(exception);
                        }
                    }

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
            View targetView,
            bool hidePrevious)
        {
            OperationId = operationId;
            Action = action;
            TargetType = targetType;
            Animated = animated;
            CallerCancellation = callerCancellation;
            Configure = configure;
            TargetView = targetView;
            HidePrevious = hidePrevious;
            Completion = new UniTaskCompletionSource<UIOperationResult>();
        }

        internal long OperationId { get; }
        internal UINavigationAction Action { get; }
        internal Type TargetType { get; }
        internal bool Animated { get; }
        internal CancellationToken CallerCancellation { get; }
        internal Action<View> Configure { get; }
        internal View TargetView { get; }
        internal bool HidePrevious { get; }
        internal UniTaskCompletionSource<UIOperationResult> Completion { get; }
        internal QueuedUIOperationState State { get; set; }
        internal CancellationTokenRegistration CancellationRegistration { get; set; }
        internal bool HasCancellationRegistration { get; set; }
    }
}
