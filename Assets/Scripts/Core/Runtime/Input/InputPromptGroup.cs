using System;
using UnityEngine;

namespace Core.Runtime.Inputs
{
    /// 保存页面多行提示的显式引用；动作、说明与显隐仍由对应页面负责。
    public sealed class InputPromptGroup : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public InputBindingPrompt prompt;
            public GameObject rowRoot;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>制作工具将稳定行标识与嵌套提示组件绑定；不扫描页面层级。</summary>
        /// <param name="values">唯一的非空标识与组件引用。</param>
        public void Configure(Entry[] values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in values)
            {
                if (string.IsNullOrWhiteSpace(entry.key) || entry.prompt == null || !keys.Add(entry.key))
                {
                    throw new ArgumentException("提示组包含缺失或重复的行绑定。", nameof(values));
                }
            }
            entries = (Entry[])values.Clone();
        }

        /// <summary>读取已保存的提示行；缺失是制作错误，不临时创建替代 UI。</summary>
        /// <param name="key">制作时保存的稳定行标识。</param>
        /// <returns>正式嵌套提示组件。</returns>
        public InputBindingPrompt Get(string key)
        {
            foreach (var entry in entries)
            {
                if (entry.key == key && entry.prompt != null)
                {
                    return entry.prompt;
                }
            }
            throw new InvalidOperationException("正式提示行未绑定：" + key);
        }

        /// <summary>收起或显示保存的提示区域，避免触屏说明与物理提示同时占布局空间。</summary>
        /// <param name="visible">物理提示是否可见。</param>
        public void SetVisible(bool visible)
        {
            foreach (var entry in entries)
            {
                if (entry.rowRoot != null)
                {
                    entry.rowRoot.SetActive(visible);
                }
                entry.prompt.gameObject.SetActive(visible);
            }
        }
    }
}
