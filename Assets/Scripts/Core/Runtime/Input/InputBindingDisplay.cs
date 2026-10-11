using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Core.Runtime.Inputs
{
    /// 提示从动作实际绑定解析；用途绑定也解析到选定手柄的真实物理键。
    public static class InputBindingDisplay
    {
        /// 一个实际控件的图标路径与显示名，绑定组可由多个键帽组成。
        public readonly struct Part
        {
            public string Text { get; }
            public string ControlPath { get; }

            /// <summary>建立实际绑定显示项。</summary>
            /// <param name="text">当前键盘布局或设备显示名。</param>
            /// <param name="controlPath">设备内路径，空值表示文字复合说明。</param>
            public Part(string text, string controlPath)
            {
                Text = text;
                ControlPath = controlPath;
            }
        }

        /// <summary>解析当前设备的一组实际绑定，复合子绑定按资产顺序返回。</summary>
        /// <param name="action">实际会话动作。</param>
        /// <param name="separator">方向组合为斜线，修饰键组合为加号。</param>
        /// <returns>可用于键帽组的显示项，触屏无物理按键时为空。</returns>
        public static IReadOnlyList<Part> GetParts(InputAction action, out string separator)
        {
            separator = " / ";
            var result = new List<Part>();
            if (action == null || InputDeviceState.PromptKind == InputDeviceKind.Touch)
            {
                return result;
            }
            var device = InputDeviceState.PromptDevice;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isPartOfComposite)
                {
                    continue;
                }
                if (binding.isComposite)
                {
                    separator = binding.effectivePath.IndexOf("Modifier", System.StringComparison.OrdinalIgnoreCase) >= 0 ? " + " : " / ";
                    for (int j = i + 1; j < action.bindings.Count && action.bindings[j].isPartOfComposite; j++)
                    {
                        AddPart(action, action.bindings[j], device, result);
                    }
                }
                else
                {
                    AddPart(action, binding, device, result);
                }
                if (result.Count > 0)
                {
                    return result;
                }
            }
            return result;
        }

        private static void AddPart(InputAction action, InputBinding binding, InputDevice device, List<Part> result)
        {
            foreach (var control in action.controls)
            {
                if (!MatchesDevice(control, device) || !InputControlPath.Matches(binding.effectivePath, control))
                {
                    continue;
                }
                var display = control is AxisControl && control.parent is StickControl ? control.parent : control;
                string path = display.path.Substring(display.device.path.Length + 1);
                result.Add(new Part(display.displayName, path));
                return;
            }
            // 未连接键盘时仍能根据制作绑定显示键名，不猜测手柄品牌。
            if (InputDeviceState.PromptKind == InputDeviceKind.KeyboardMouse
                && (binding.effectivePath.StartsWith("<Keyboard>") || binding.effectivePath.StartsWith("<Mouse>")))
            {
                result.Add(new Part(InputControlPath.ToHumanReadableString(binding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice),
                    binding.effectivePath.Substring(binding.effectivePath.IndexOf('/') + 1)));
            }
        }

        /// <summary>解析当前提示设备下的实际绑定。</summary>
        /// <param name="action">正在使用的动作，优先传入会话副本。</param>
        /// <param name="controlPath">对应设备内控制路径，用于查图标。</param>
        /// <returns>绑定显示文字；手机或未绑定时为空。</returns>
        public static string Get(InputAction action, out string controlPath)
        {
            var parts = GetParts(action, out var separator);
            controlPath = parts.Count == 1 ? parts[0].ControlPath : null;
            var names = new string[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                names[i] = parts[i].Text;
            }
            return string.Join(separator, names);
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
