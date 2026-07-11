using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    internal sealed class UILegacyAnimationTransaction
    {
        private readonly List<Attempt> attempts = new List<Attempt>();
        private bool compensated;

        internal void Record(View view, UILegacyAnimationStage stage)
        {
            for (int i = 0; i < attempts.Count; i++)
            {
                if (ReferenceEquals(attempts[i].View, view) && attempts[i].Stage == stage)
                {
                    return;
                }
            }

            attempts.Add(new Attempt(view, stage));
        }

        internal bool HasExitAttempt(View view)
        {
            for (int i = 0; i < attempts.Count; i++)
            {
                if (ReferenceEquals(attempts[i].View, view) &&
                    (attempts[i].Stage == UILegacyAnimationStage.CameraHide ||
                     attempts[i].Stage == UILegacyAnimationStage.UIHide))
                {
                    return true;
                }
            }

            return false;
        }

        internal async UniTask CompensateAsync(Action<Exception> reportFailure)
        {
            if (compensated)
            {
                return;
            }

            compensated = true;
            for (int i = attempts.Count - 1; i >= 0; i--)
            {
                try
                {
                    await CompensateAsync(attempts[i]);
                }
                catch (Exception exception)
                {
                    reportFailure?.Invoke(exception);
                }
            }
        }

        private static UniTask CompensateAsync(Attempt attempt)
        {
            return attempt.Stage switch
            {
                UILegacyAnimationStage.CameraShow => attempt.View.CameraAnimation.Hide(attempt.View),
                UILegacyAnimationStage.UIShow => attempt.View.UIAnimation.Hide(),
                UILegacyAnimationStage.CameraHide => attempt.View.CameraAnimation.Show(attempt.View),
                UILegacyAnimationStage.UIHide => attempt.View.UIAnimation.Show(),
                _ => UniTask.CompletedTask
            };
        }

        private readonly struct Attempt
        {
            internal Attempt(View view, UILegacyAnimationStage stage)
            {
                View = view;
                Stage = stage;
            }

            internal View View { get; }
            internal UILegacyAnimationStage Stage { get; }
        }
    }

    internal enum UILegacyAnimationStage
    {
        CameraShow,
        UIShow,
        CameraHide,
        UIHide
    }
}
