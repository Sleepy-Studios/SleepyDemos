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
        /// <param name="deviceKind">操作说明页可显式预览设备；为空跟随最近实际设备。</param>
        /// <param name="compositePart">只显示指定复合子绑定；为空显示完整组合。</param>
        /// <param name="bindingIndex">可选的精确单绑定索引；复合动作只接受子绑定索引。</param>
        public static IReadOnlyList<Part> GetParts(InputAction action, out string separator,
            InputDeviceKind? deviceKind = null, string compositePart = null, int? bindingIndex = null)
        {
            separator = " / ";
            var result = new List<Part>();
            var kind = deviceKind ?? InputDeviceState.PromptKind;
            if (action == null || kind == InputDeviceKind.Touch)
            {
                return result;
            }
            var device = ResolveDevice(kind);
            if (bindingIndex.HasValue)
            {
                int index = bindingIndex.Value;
                if (index < 0 || index >= action.bindings.Count)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(bindingIndex));
                }
                if (action.bindings[index].isComposite)
                {
                    throw new System.ArgumentException("精确提示只能选择单绑定或复合子绑定。", nameof(bindingIndex));
                }
                AddPart(action, action.bindings[index], kind, device, result);
                return result;
            }
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
                    var selectedParts = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                    for (int j = i + 1; j < action.bindings.Count && action.bindings[j].isPartOfComposite; j++)
                    {
                        var part = action.bindings[j];
                        if (!selectedParts.Contains(part.name) && (string.IsNullOrEmpty(compositePart)
                            || string.Equals(part.name, compositePart, System.StringComparison.OrdinalIgnoreCase)))
                        {
                            int before = result.Count;
                            AddPart(action, part, kind, device, result);
                            if (result.Count > before)
                            {
                                selectedParts.Add(part.name);
                            }
                        }
                    }
                }
                else
                {
                    if (string.IsNullOrEmpty(compositePart))
                    {
                        AddPart(action, binding, kind, device, result);
                    }
                }
                if (result.Count > 0)
                {
                    return result;
                }
            }
            return result;
        }

        private static void AddPart(InputAction action, InputBinding binding, InputDeviceKind kind, InputDevice device, List<Part> result)
        {
            foreach (var control in action.controls)
            {
                if (!MatchesDevice(control, kind, device) || !InputControlPath.Matches(binding.effectivePath, control))
                {
                    continue;
                }
                var display = control is AxisControl && control.parent is StickControl ? control.parent : control;
                string path = display.path.Substring(display.device.path.Length + 1);
                if (!result.Exists(part => part.ControlPath == path))
                {
                    result.Add(new Part(display.displayName, path));
                }
                return;
            }
            if (TryPreviewUsage(binding.effectivePath, kind, device, out var usagePart))
            {
                result.Add(usagePart);
                return;
            }
            // 操作说明可预览未连接设备的制作绑定；未识别的手柄不猜测品牌。
            if (kind == InputDeviceKind.KeyboardMouse
                && (binding.effectivePath.StartsWith("<Keyboard>") || binding.effectivePath.StartsWith("<Mouse>"))
                || kind == InputDeviceKind.Gamepad && binding.effectivePath.StartsWith("<Gamepad>"))
            {
                result.Add(new Part(InputControlPath.ToHumanReadableString(binding.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice),
                    binding.effectivePath.Substring(binding.effectivePath.IndexOf('/') + 1)));
            }
        }

        private static bool TryPreviewUsage(string path, InputDeviceKind kind, InputDevice device, out Part part)
        {
            part = default;
            int slash = path.IndexOf('/');
            if (slash < 0)
            {
                return false;
            }
            string control = path.Substring(slash + 1);
            if (control.Length < 3 || control[0] != '{' || control[control.Length - 1] != '}')
            {
                return false;
            }
            bool wildcard = path.StartsWith("*/", System.StringComparison.Ordinal);
            bool matches = kind == InputDeviceKind.Gamepad ? path.StartsWith("<Gamepad>/", System.StringComparison.Ordinal)
                : path.StartsWith("<Keyboard>/", System.StringComparison.Ordinal);
            if (!wildcard && !matches)
            {
                return false;
            }
            string usage = control.Substring(1, control.Length - 2);
            string layoutName = kind == InputDeviceKind.Gamepad ? device?.layout ?? "Gamepad" : "Keyboard";
            var layout = InputSystem.LoadLayout(layoutName);
            if (layout == null)
            {
                return false;
            }
            foreach (var item in layout.controls)
            {
                foreach (var value in item.usages)
                {
                    if (!string.Equals(value.ToString(), usage, System.StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    string resolvedPath = item.name.ToString();
                    string text = InputControlPath.ToHumanReadableString("<" + layoutName + ">/" + resolvedPath,
                        InputControlPath.HumanReadableStringOptions.OmitDevice);
                    part = new Part(text, resolvedPath);
                    return true;
                }
            }
            return false;
        }

        /// <summary>解析当前提示设备下的实际绑定。</summary>
        /// <param name="action">正在使用的动作，优先传入会话副本。</param>
        /// <param name="controlPath">对应设备内控制路径，用于查图标。</param>
        /// <returns>绑定显示文字；手机或未绑定时为空。</returns>
        /// <param name="deviceKind">可选的说明页设备预览。</param>
        /// <param name="compositePart">可选的复合子绑定名。</param>
        /// <param name="bindingIndex">可选的精确单绑定索引。</param>
        public static string Get(InputAction action, out string controlPath,
            InputDeviceKind? deviceKind = null, string compositePart = null, int? bindingIndex = null)
        {
            var parts = GetParts(action, out var separator, deviceKind, compositePart, bindingIndex);
            controlPath = parts.Count == 1 ? parts[0].ControlPath : null;
            var names = new string[parts.Count];
            for (int i = 0; i < parts.Count; i++)
            {
                names[i] = parts[i].Text;
            }
            return string.Join(separator, names);
        }

        internal static InputDevice ResolveDevice(InputDeviceKind kind) => kind == InputDeviceState.PromptKind
            ? InputDeviceState.PromptDevice : kind == InputDeviceKind.Gamepad ? Gamepad.current : Keyboard.current;

        private static bool MatchesDevice(InputControl control, InputDeviceKind kind, InputDevice device) =>
            kind == InputDeviceKind.Gamepad ? control.device is Gamepad && (device == null || control.device == device) :
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
