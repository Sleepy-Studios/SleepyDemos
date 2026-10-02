using System;

namespace Hotfix.JinxCasino.Adapters
{
    /// Demo单机表现时间；暂停不积累真实时间，恢复首帧不补算后台耗时。
    public sealed class JinxCasinoPresentationClock
    {
        private Func<bool> pauseSource;
        private bool paused;
        private bool skipNextDelta = true;
        private int lastFrame = -1;
        private float timeSeconds;
        private float deltaSeconds;

        /// 该Demo已经实际演出的累计秒数。
        public float TimeSeconds { get { SynchronizePause(); return timeSeconds; } }
        /// 暂停状态即时读取宿主，组件Update先于宿主也不会继续移动。
        public bool IsPaused { get { SynchronizePause(); return paused; } }

        /// <summary>绑定本Demo的暂停来源；不读取全局timeScale。</summary>
        /// <param name="source">宿主输入暂停与本机模态暂停的合并状态，null使用显式SetPaused。</param>
        public void BindPauseSource(Func<bool> source) { pauseSource = source; SynchronizePause(); }

        /// <summary>更新显式暂停，状态切换幂等；恢复第一帧丢弃dt。</summary>
        /// <param name="value">是否冻结当前演出剩余时间。</param>
        public void SetPaused(bool value)
        {
            if (paused == value) return;
            paused = value; deltaSeconds = 0; skipNextDelta = true;
        }

        /// <summary>宿主每帧仅推进一次表现时间，暂停期间保持累计时间。</summary>
        /// <param name="seconds">本帧真实dt；恢复帧可能包含后台时间，因此不计入。</param>
        /// <param name="frame">Unity帧号，用于避免重复推进和消费上一帧位移。</param>
        public void Advance(float seconds, int frame)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            SynchronizePause();
            if (lastFrame == frame) return;
            lastFrame = frame; deltaSeconds = 0;
            if (paused) return;
            if (skipNextDelta) { skipNextDelta = false; return; }
            deltaSeconds = seconds; timeSeconds += seconds;
        }

        /// <summary>只返回已经推进的当前帧位移时间；Update执行顺序不同也不会消费上一帧dt。</summary>
        /// <param name="frame">消费组件当前Unity帧号。</param>
        public float GetDeltaSeconds(int frame)
        { SynchronizePause(); return !paused && lastFrame == frame ? deltaSeconds : 0; }

        private void SynchronizePause() { if (pauseSource != null) SetPaused(pauseSource()); }
    }
}
