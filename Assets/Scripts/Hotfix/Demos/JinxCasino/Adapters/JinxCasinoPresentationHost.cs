using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    // 先推进共享时钟和领域投影，再由默认顺序的场景演出消费本帧dt。
    [DefaultExecutionOrder(-500)]
    public sealed partial class JinxCasinoController
    {
        private readonly JinxCasinoPresentationClock presentationClock = new JinxCasinoPresentationClock();
        private bool presentationClockBound;

        /// 单机场景共享本机表现时间，菜单暂停同时冻结物件反馈。
        public JinxCasinoPresentationClock PresentationClock
        {
            get
            {
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
