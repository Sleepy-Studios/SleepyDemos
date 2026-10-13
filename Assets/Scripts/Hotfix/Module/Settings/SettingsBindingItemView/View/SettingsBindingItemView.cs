using System;
using Core.Runtime;
using Core.Runtime.Inputs;
using UnityEngine.InputSystem;

namespace Hotfix
{
    [Module("Settings")]
    [UIBind("SettingsBindingItemView")]
    public sealed partial class SettingsBindingItemView : ItemView
    {
        /// <summary>刷新一个循环列表物理条目，不把 Cell 上下文放进业务数据。</summary>
        public void SetData(InputAction action, int bindingIndex, string captionKey, string context, InputDeviceKind device,
            bool editable, Action rebind, Action reset)
            => InputSettingsBindingRow_Row.Bind(action, bindingIndex, captionKey, context, device, editable, rebind, reset);

        public void Clear() => InputSettingsBindingRow_Row.Clear();

        public void SetGamepadStyle(GamepadStyle style) => InputSettingsBindingRow_Row.SetGamepadStyle(style);
    }
}
