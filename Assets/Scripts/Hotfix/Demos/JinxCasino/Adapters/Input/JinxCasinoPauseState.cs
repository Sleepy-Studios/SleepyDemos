using System;

namespace Hotfix.JinxCasino.Adapters.Input
{
    /// 单机暂停门闩。恢复焦点或重连只解除物理阻塞，必须再次明确确认继续。
    public sealed class JinxCasinoPauseState
    {
        private JinxCasinoPauseReason blockers;
        private JinxCasinoPauseReason reasons;
        private bool resumeRequired;

        /// 宿主在暂停时停止领域Advance/演出时钟，不使用全局Time.timeScale。
        public bool IsPaused => resumeRequired || blockers != JinxCasinoPauseReason.None;
        public bool CanResume => resumeRequired && blockers == JinxCasinoPauseReason.None;
        public JinxCasinoPauseReason Reasons => reasons;
        public JinxCasinoPauseReason BlockingReasons => blockers;
        /// 仅实际门闩或阻塞改变时通知；不以每帧刷新替代状态事件。
        public event Action Changed;

        /// <summary>用户明确打开暂停；背景/失焦/断连由各自状态方法报告。</summary>
        /// <param name="reason">一个或多个已定义的非零原因，不接受未知位。</param>
        public void RequestPause(JinxCasinoPauseReason reason)
        {
            const JinxCasinoPauseReason valid = JinxCasinoPauseReason.User | JinxCasinoPauseReason.FocusLost
                | JinxCasinoPauseReason.Background | JinxCasinoPauseReason.GamepadDisconnected;
            if (reason == JinxCasinoPauseReason.None || (reason & ~valid) != 0) throw new ArgumentOutOfRangeException(nameof(reason));
            bool changed = !resumeRequired || (reasons & reason) != reason;
            resumeRequired = true; reasons |= reason;
            var physical = reason & ~JinxCasinoPauseReason.User;
            changed |= (blockers & physical) != physical; blockers |= physical;
            if (changed) Changed?.Invoke();
        }

        /// <summary>接收Controller.OnApplicationFocus；重新聚焦保留确认继续门闩。</summary>
        /// <param name="focused">应用当前是否拥有焦点。</param>
        public void SetApplicationFocus(bool focused) => SetPhysicalState(JinxCasinoPauseReason.FocusLost, !focused);

        /// <summary>接收Controller.OnApplicationPause；切回前台不自动继续。</summary>
        /// <param name="paused">应用是否在后台或被系统暂停。</param>
        public void SetApplicationPaused(bool paused) => SetPhysicalState(JinxCasinoPauseReason.Background, paused);

        /// <summary>报告所用手柄断连或恢复；显式切换为键鼠/触屏也可解除手柄物理阻塞。</summary>
        /// <param name="available">可用手柄恢复，或玩家实际操作其它设备选择替代输入时为true。</param>
        public void SetGamepadAvailable(bool available) => SetPhysicalState(JinxCasinoPauseReason.GamepadDisconnected, !available);

        /// 用户明确确认继续；仍失焦、在后台或未选择替代输入时拒绝，不吞掉暂停原因。
        public bool TryResume()
        {
            if (!CanResume) return false;
            resumeRequired = false; reasons = JinxCasinoPauseReason.None; Changed?.Invoke(); return true;
        }

        private void SetPhysicalState(JinxCasinoPauseReason reason, bool blocked)
        {
            if (blocked) { RequestPause(reason); return; }
            if ((blockers & reason) == 0) return;
            blockers &= ~reason; Changed?.Invoke();
        }
    }
}
