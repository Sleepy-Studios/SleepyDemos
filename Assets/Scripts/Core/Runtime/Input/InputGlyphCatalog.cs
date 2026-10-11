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

        /// 键鼠和触屏图标；缺少预制图标的绑定使用空键帽内的实际显示文字。
        [Serializable]
        public struct DeviceEntry
        {
            public InputDeviceKind deviceKind;
            public string controlPath;
            public Sprite sprite;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        [SerializeField] private DeviceEntry[] deviceEntries = Array.Empty<DeviceEntry>();
        [SerializeField] private Sprite keyboardBlank;

        public Sprite KeyboardBlank => keyboardBlank;

        /// <summary>正式制作工具配置手柄和键鼠图标；保留已用资源，不扫描目录。</summary>
        /// <param name="gamepadEntries">真实布局风格和控制路径的图标。</param>
        /// <param name="otherEntries">键鼠与触屏专用路径图标。</param>
        /// <param name="blankKeycap">无法预制键名时显示文字的空白键帽。</param>
        public void Configure(Entry[] gamepadEntries, DeviceEntry[] otherEntries, Sprite blankKeycap)
        {
            entries = gamepadEntries == null ? Array.Empty<Entry>() : (Entry[])gamepadEntries.Clone();
            deviceEntries = otherEntries == null ? Array.Empty<DeviceEntry>() : (DeviceEntry[])otherEntries.Clone();
            keyboardBlank = blankKeycap;
        }

        /// <summary>按实际设备和控件路径取得图标。</summary>
        /// <param name="kind">真实提示设备类别。</param>
        /// <param name="style">已识别手柄品牌。</param>
        /// <param name="path">实际设备内控件路径。</param>
        /// <returns>专用图标；没有资源时由键帽文字展示真实绑定。</returns>
        public Sprite Get(InputDeviceKind kind, GamepadStyle style, string path)
        {
            if (kind == InputDeviceKind.Gamepad)
            {
                return Get(style, path);
            }
            foreach (var entry in deviceEntries)
            {
                if (entry.deviceKind == kind && entry.controlPath == path)
                {
                    return entry.sprite;
                }
            }
            return null;
        }

        /// <summary>查找已确认品牌对应的图标；未知设备不冒用品牌。</summary>
        /// <param name="style">真实设备图标风格。</param>
        /// <param name="path">设备内部控制路径。</param>
        /// <returns>图标或 null，调用方可显示绑定文字。</returns>
        public Sprite Get(GamepadStyle style, string path)
        {
            foreach (var entry in entries)
            {
                if (entry.style == style && entry.controlPath == path)
                {
                    return entry.sprite;
                }
            }
            return null;
        }
    }
}
