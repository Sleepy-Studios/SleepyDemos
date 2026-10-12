using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Settings;
using UnityEngine;

namespace Hotfix
{
    /// 独立设置分页共享候选生命周期；控件与编辑规则由具体 View 直接持有。
    public abstract class SettingsPanelView : View
    {
        protected GameSettingsSnapshot Candidate { get; private set; }
        protected Action Edited { get; private set; }
        protected Transform PopupRoot { get; private set; }
        public IReadOnlyList<UISettingRow> Rows { get; private set; }

        /// <summary>在首次加载前存入共享候选；可见分页立即重新回显。</summary>
        /// <param name="value">Hub 五个分页共享的独立候选。</param>
        /// <param name="changed">用户编辑后通知宿主。</param>
        /// <param name="popupRoot">下拉选项逃出滚动裁剪的宿主挂载根。</param>
        public void SetData(GameSettingsSnapshot value, Action changed, Transform popupRoot)
        {
            Candidate = value ?? throw new ArgumentNullException(nameof(value));
            Edited = changed;
            PopupRoot = popupRoot;
            if (IsEnable)
            {
                Release();
                Refresh();
            }
        }

        protected void InitializeRows(params UISettingRow[] rows)
        {
            foreach (var row in rows)
            {
                if (row == null)
                {
                    throw new InvalidOperationException("设置分页缺少正式控件引用。");
                }
                if (row.Control != null && row.Control.TryGetComponent<UIDropdown>(out var dropdown))
                {
                    dropdown.SetPopupRoot(PopupRoot);
                }
            }
            Rows = rows;
        }

        public abstract void Refresh();
        public virtual void Release() { }

        protected void Change(Action mutation)
        {
            mutation();
            Edited?.Invoke();
        }

        protected override void OnShow()
        {
            Release();
            Refresh();
            base.OnShow();
        }

        protected override void OnHide()
        {
            Release();
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            if (Rows != null)
            {
                Release();
            }
            base.OnDestroy();
        }
    }
}
