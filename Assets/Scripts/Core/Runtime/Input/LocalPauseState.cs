using System;

namespace Core.Runtime.Inputs
{
    /// 单机暂停门闩。恢复焦点或重连只解除物理阻塞，必须再次明确确认继续。
    public sealed class LocalPauseState
    {
        private LocalPauseReason blockers;
        private LocalPauseReason reasons;
        private bool resumeRequired;

        /// 宿主在暂停时停止领域Advance/演出时钟，不使用全局Time.timeScale。
        public bool IsPaused => resumeRequired || blockers != LocalPauseReason.None;
        public bool CanResume => resumeRequired && blockers == LocalPauseReason.None;
        public LocalPauseReason Reasons => reasons;
        public LocalPauseReason BlockingReasons => blockers;
        /// 仅实际门闩或阻塞改变时通知；不以每帧刷新替代状态事件。
        public event Action Changed;

        /// <summary>用户明确打开暂停；背景/失焦/断连由各自状态方法报告。</summary>
        /// <param name="reason">一个或多个已定义的非零原因，不接受未知位。</param>
        public void RequestPause(LocalPauseReason reason)
        {
            const LocalPauseReason valid = LocalPauseReason.User | LocalPauseReason.FocusLost
                | LocalPauseReason.Background | LocalPauseReason.GamepadDisconnected;
            if (reason == LocalPauseReason.None || (reason & ~valid) != 0) throw new ArgumentOutOfRangeException(nameof(reason));
            bool changed = !resumeRequired || (reasons & reason) != reason;
            resumeRequired = true; reasons |= reason;
            var physical = reason & ~LocalPauseReason.User;
            changed |= (blockers & physical) != physical; blockers |= physical;
            if (changed) Changed?.Invoke();
        }

        /// <summary>接收Controller.OnApplicationFocus；重新聚焦保留确认继续门闩。</summary>
        /// <param name="focused">应用当前是否拥有焦点。</param>
        public void SetApplicationFocus(bool focused) => SetPhysicalState(LocalPauseReason.FocusLost, !focused);

        /// <summary>接收Controller.OnApplicationPause；切回前台不自动继续。</summary>
        /// <param name="paused">应用是否在后台或被系统暂停。</param>
        public void SetApplicationPaused(bool paused) => SetPhysicalState(LocalPauseReason.Background, paused);

        /// <summary>报告所用手柄断连或恢复；显式切换为键鼠/触屏也可解除手柄物理阻塞。</summary>
        /// <param name="available">可用手柄恢复，或玩家实际操作其它设备选择替代输入时为true。</param>
        public void SetGamepadAvailable(bool available) => SetPhysicalState(LocalPauseReason.GamepadDisconnected, !available);

        /// 用户明确确认继续；仍失焦、在后台或未选择替代输入时拒绝，不吞掉暂停原因。
        public bool TryResume()
        {
            if (!CanResume) return false;
            resumeRequired = false; reasons = LocalPauseReason.None; Changed?.Invoke(); return true;
        }

        private void SetPhysicalState(LocalPauseReason reason, bool blocked)
        {
            if (blocked) { RequestPause(reason); return; }
            if ((blockers & reason) == 0) return;
            blockers &= ~reason; Changed?.Invoke();
        }
    }
}
