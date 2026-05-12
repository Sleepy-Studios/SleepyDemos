using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public interface IUIAnimation
    {
        void Init(Transform root);
        UniTask Show();
        UniTask Hide();
    }

    public interface ICameraAnimation
    {
        UniTask Show(View view);
        UniTask Hide(View view);
    }

    public sealed class EmptyUIAnimation : IUIAnimation
    {
        public void Init(Transform root)
        {
        }

        public UniTask Show()
        {
            return UniTask.CompletedTask;
        }

        public UniTask Hide()
        {
            return UniTask.CompletedTask;
        }
    }
}
