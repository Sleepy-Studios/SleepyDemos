using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    internal sealed class UIWorldTransitionTransaction
    {
        private readonly IUIWorldTransitionProvider provider;
        private readonly Dictionary<View, Entry> entries = new Dictionary<View, Entry>();

        internal UIWorldTransitionTransaction(IUIWorldTransitionProvider provider)
        {
            this.provider = provider;
        }

        internal void Resolve(View view, UITransitionDirection rollbackDirection)
        {
            if (view == null || entries.ContainsKey(view))
            {
                return;
            }

            var transition = provider?.Resolve(view) ?? EmptyUIWorldTransition.Instance;
            entries.Add(view, new Entry(transition, rollbackDirection));
        }

        internal async UniTask EnterAsync(
            View view,
            UITransitionContext context,
            CancellationToken cancellationToken)
        {
            var entry = GetEntry(view);
            cancellationToken.ThrowIfCancellationRequested();
            entry.Attempted = true;
            if (context.Animated)
            {
                await entry.Transition.EnterAsync(context, cancellationToken);
            }
            else
            {
                entry.Transition.CompleteImmediately(UITransitionDirection.Enter);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        internal async UniTask ExitAsync(
            View view,
            UITransitionContext context,
            CancellationToken cancellationToken)
        {
            var entry = GetEntry(view);
            cancellationToken.ThrowIfCancellationRequested();
            entry.Attempted = true;
            if (context.Animated)
            {
                await entry.Transition.ExitAsync(context, cancellationToken);
            }
            else
            {
                entry.Transition.CompleteImmediately(UITransitionDirection.Exit);
            }

            cancellationToken.ThrowIfCancellationRequested();
        }

        internal void Restore(Action<Exception> reportFailure)
        {
            foreach (var entry in entries.Values)
            {
                if (!entry.Attempted)
                {
                    continue;
                }

                try
                {
                    entry.Transition.CompleteImmediately(entry.RollbackDirection);
                }
                catch (Exception exception)
                {
                    reportFailure?.Invoke(exception);
                }
            }
        }

        private Entry GetEntry(View view)
        {
            if (view == null || !entries.TryGetValue(view, out var entry))
            {
                throw new InvalidOperationException("World Transition 必须在事务阶段开始前解析。");
            }

            return entry;
        }

        private sealed class Entry
        {
            internal Entry(IUIWorldTransition transition, UITransitionDirection rollbackDirection)
            {
                Transition = transition;
                RollbackDirection = rollbackDirection;
            }

            internal IUIWorldTransition Transition { get; }
            internal UITransitionDirection RollbackDirection { get; }
            internal bool Attempted { get; set; }
        }
    }
}
