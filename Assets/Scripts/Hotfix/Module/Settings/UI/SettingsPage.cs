using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Settings;
using UnityEngine;

namespace Hotfix
{
    /// 一个独立设置分页的制作引用与候选生命周期，不保存玩家数据。
    public abstract class SettingsPage : MonoBehaviour
    {
        [SerializeField] private UISettingRow[] rows = Array.Empty<UISettingRow>();
        private readonly Dictionary<string, UISettingRow> fields = new();
        protected GameSettingsSnapshot Candidate { get; private set; }
        protected Action Edited { get; private set; }
        public IReadOnlyList<UISettingRow> Rows => rows;

        /// <summary>绑定本次打开页面的候选；分页不得持有制作默认值作为运行时对象。</summary>
        /// <param name="candidate">设置页拥有的独立快照。</param>
        /// <param name="edited">用户编辑后通知父页刷新帮助与候选状态。</param>
        public void Bind(GameSettingsSnapshot candidate, Action edited)
        {
            Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
            Edited = edited;
            fields.Clear();
            foreach (var row in rows)
            {
                if (row == null || string.IsNullOrWhiteSpace(row.Key) || !fields.TryAdd(row.Key, row))
                    throw new InvalidOperationException("设置分页存在空引用或重复字段。");
            }
            Refresh();
        }

        public abstract void Refresh();
        public virtual void Release() { }
        protected UISettingRow Row(string key) => fields[key];

        protected void Change(Action mutation)
        {
            mutation();
            Edited?.Invoke();
        }
    }
}
