using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    // 先推进共享时钟和领域投影，再由默认顺序的场景演出消费本帧dt。
    [DefaultExecutionOrder(-500)]
    public sealed partial class JinxCasinoController
    {
        private readonly JinxCasinoPresentationClock presentationClock = new JinxCasinoPresentationClock();
        private bool presentationClockBound;

        /// 沉浸样板共享本机表现时间；旧原型返回null，保持原Time语义。
        public JinxCasinoPresentationClock PresentationClock
        {
            get
            {
                if (!UsesImmersion) return null;
                if (!presentationClockBound)
                {
                    presentationClock.BindPauseSource(() => IsImmersionPaused);
                    presentationClockBound = true;
                }
                return presentationClock;
            }
        }

        private void AdvanceImmersionPresentation() => PresentationClock?.Advance(Time.unscaledDeltaTime, Time.frameCount);
    }
}
