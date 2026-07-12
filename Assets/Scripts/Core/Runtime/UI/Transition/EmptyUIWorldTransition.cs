using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    internal sealed class EmptyUIWorldTransition : IUIWorldTransition
    {
        internal static readonly EmptyUIWorldTransition Instance = new EmptyUIWorldTransition();

        private EmptyUIWorldTransition()
        {
        }

        public UniTask EnterAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public UniTask ExitAsync(UITransitionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return UniTask.CompletedTask;
        }

        public void CompleteImmediately(UITransitionDirection direction)
        {
        }
    }
}
