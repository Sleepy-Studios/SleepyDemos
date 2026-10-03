using System;
using UnityEngine;

namespace Core.Runtime.Inputs
{
    /// 只保存使用中的图标；资源由宿主加载/序列化引用，不扫描 Resources。
    [CreateAssetMenu(menuName = "SleepyDemos/Input Glyph Catalog")]
    public sealed class InputGlyphCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public GamepadStyle style;
            public string controlPath;
            public Sprite sprite;
        }
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        /// <summary>查找已确认品牌对应的图标；未知设备不冒用品牌。</summary>
        /// <param name="style">真实设备图标风格。</param>
        /// <param name="path">设备内部控制路径。</param>
        /// <returns>图标或 null，调用方可显示绑定文字。</returns>
        public Sprite Get(GamepadStyle style, string path)
        {
            foreach (var entry in entries) if (entry.style == style && entry.controlPath == path) return entry.sprite;
            return null;
        }
    }
}
