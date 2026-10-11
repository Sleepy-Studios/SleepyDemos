using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Runtime.Inputs
{
    /// 控制设置页可编辑的正式动作资产，保留各 Demo 的动作边界。
    [CreateAssetMenu(menuName = "SleepyDemos/Settings/Input Settings Catalog")]
    public sealed class InputSettingsCatalog : ScriptableObject
    {
        /// 一个宿主的设置入口，文案键与动作资产独立于显示名称。
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string displayKey;
            [SerializeField] private InputActionAsset asset;

            public string DisplayKey => displayKey;
            public InputActionAsset Asset => asset;
            public string SourceId => InputActionSession.GetSourceId(asset);

            /// <summary>配置一个控制分页的正式入口。</summary>
            /// <param name="displayKey">宿主名称的公共文案键。</param>
            /// <param name="asset">正式源动作资产，不从目录动态扫描。</param>
            public Entry(string displayKey, InputActionAsset asset)
            {
                this.displayKey = displayKey;
                this.asset = asset;
            }
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => Array.AsReadOnly(entries);

        /// <summary>由制作工具显式配置可编辑入口。</summary>
        /// <param name="values">各宿主的正式动作资产，不保存调用方数组引用。</param>
        public void Configure(Entry[] values)
        {
            ValidateEntries(values);
            entries = (Entry[])values.Clone();
        }

        /// 验证正式入口与稳定动作身份。
        public void Validate() => ValidateEntries(entries);

        /// <summary>在设置保存边界验证覆盖记录，未知资产或破坏固定菜单的记录拒绝提交。</summary>
        /// <param name="overrides">按源资产 SourceId 索引的候选 override JSON。</param>
        public void ValidateOverrides(IReadOnlyDictionary<string, string> overrides)
        {
            if (overrides == null)
            {
                throw new ArgumentNullException(nameof(overrides));
            }
            Validate();
            foreach (var record in overrides)
            {
                Entry match = null;
                foreach (var entry in entries)
                {
                    if (entry.SourceId == record.Key)
                    {
                        match = entry;
                        break;
                    }
                }
                if (match == null || record.Value == null || record.Value.Length > 65536)
                {
                    throw new ArgumentException("按键覆盖记录不属于正式输入配置。");
                }
                using var candidate = new InputRebindSession(match.Asset, record.Value);
                candidate.ExportOverrides();
            }
        }

        private static void ValidateEntries(Entry[] values)
        {
            if (values == null || values.Length == 0)
            {
                throw new ArgumentException("输入设置目录必须显式配置正式动作资产。");
            }
            var sources = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in values)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.DisplayKey) || entry.Asset == null || !sources.Add(entry.SourceId))
                {
                    throw new ArgumentException("输入设置目录包含无效或重复入口。");
                }
            }
        }
    }
}
