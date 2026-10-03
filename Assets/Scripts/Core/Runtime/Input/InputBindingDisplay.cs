using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Core.Runtime.Inputs
{
    /// 提示从动作实际绑定解析；用途绑定也解析到选定手柄的真实物理键。
    public static class InputBindingDisplay
    {
        /// <summary>解析当前提示设备下的实际绑定。</summary>
        /// <param name="action">正在使用的动作，优先传入会话副本。</param>
        /// <param name="controlPath">对应设备内控制路径，用于查图标。</param>
        /// <returns>绑定显示文字；手机或未绑定时为空。</returns>
        public static string Get(InputAction action, out string controlPath)
        {
            controlPath = null;
            if (action == null || InputDeviceState.PromptKind == InputDeviceKind.Touch) return string.Empty;
            var device = InputDeviceState.PromptDevice;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isPartOfComposite) continue;
                if (binding.isComposite)
                {
                    var parts = new List<string>();
                    for (int j = i + 1; j < action.bindings.Count && action.bindings[j].isPartOfComposite; j++)
                    {
                        foreach (var control in action.controls)
                        {
                            if (!MatchesDevice(control, device) || !InputControlPath.Matches(action.bindings[j].effectivePath, control)) continue;
                            if (!parts.Contains(control.displayName)) parts.Add(control.displayName);
                        }
                    }
                    if (parts.Count > 0) return InputDeviceState.PromptKind == InputDeviceKind.KeyboardMouse
                        ? action.GetBindingDisplayString(i) : string.Join(" / ", parts);
                    continue;
                }
                foreach (var control in action.controls)
                {
                    if (!MatchesDevice(control, device)) continue;
                    if (!InputControlPath.Matches(binding.effectivePath, control)) continue;
                    controlPath = control.path.Substring(control.device.path.Length + 1);
                    return control.displayName;
                }
                // 无键盘硬件时仍可显示保存的绑定；手柄则必须匹配真实连接设备。
                if (InputDeviceState.PromptKind == InputDeviceKind.KeyboardMouse &&
                    (binding.effectivePath.StartsWith("<Keyboard>") || binding.effectivePath.StartsWith("<Mouse>")))
                    return action.GetBindingDisplayString(i, out _, out controlPath);
            }
            return string.Empty;
        }

        private static bool MatchesDevice(InputControl control, InputDevice device) =>
            InputDeviceState.PromptKind == InputDeviceKind.Gamepad ? control.device == device :
            control.device is Keyboard || control.device is Mouse;

        /// <summary>格式化动作提示文字，不写死设备按键。</summary>
        /// <param name="action">实际动作。</param>
        /// <param name="caption">业务名称。</param>
        /// <returns>例如 E 交互；触控仅显示交互。</returns>
        public static string Label(InputAction action, string caption)
        {
            string key = Get(action, out _);
            return string.IsNullOrEmpty(key) ? caption : key + " " + caption;
        }
    }
}
