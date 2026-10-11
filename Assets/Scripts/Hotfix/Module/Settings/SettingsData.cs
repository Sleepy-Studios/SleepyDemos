using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Settings;

namespace Hotfix
{
    /// 公共设置页面状态，已保存偏好始终由 Core 服务持有。
    public sealed class SettingsData : IData
    {
        public List<IHandler> Handlers { get; } = new() { new SettingsHandler() };

        /// 当前独立候选源。
        public GameSettingsSnapshot Current => GameSettingsService.Current;
        /// 本模块正在等待应用事务。
        public bool IsApplying { get; internal set; }
        /// 最近一次提交反馈。
        public string Feedback { get; internal set; }
        internal int Version;

        /// 使旧异步结果失效，不改变已保存偏好。
        public void ClearData()
        {
            Version++;
            IsApplying = false;
            Feedback = null;
        }
    }
}
