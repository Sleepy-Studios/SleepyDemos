using System;
using Core.Runtime;
using Core.Runtime.Settings;

namespace Hotfix
{
    /// ViewTab 持有的独立分页，每次显示才绑定候选，隐藏时释放编辑会话。
    public abstract class SettingsPanelView : View
    {
        private GameSettingsSnapshot candidate;
        private Action edited;
        protected abstract SettingsPage Page { get; }
        public SettingsPage Editor => Page;

        /// <summary>存入宿主候选；未初始化时不访问控件，可见页立即重新绑定。</summary>
        /// <param name="value">同一宿主五个分页共享的独立候选，不是服务 Current 本体。</param>
        /// <param name="changed">用户修改后通知宿主。</param>
        public void SetData(GameSettingsSnapshot value, Action changed)
        {
            candidate = value ?? throw new ArgumentNullException(nameof(value));
            edited = changed;
            if (IsEnable)
            {
                Page.Release();
                Page.Bind(candidate, edited);
            }
        }

        protected override void OnShow()
        {
            Page.Release();
            Page.Bind(candidate, edited);
            base.OnShow();
        }

        protected override void OnHide()
        {
            Page.Release();
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            if (Page != null)
            {
                Page.Release();
            }
            base.OnDestroy();
        }
    }
}
