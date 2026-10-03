using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Core.Runtime.Inputs
{
    public enum GamepadStyle { Generic, Xbox, PlayStation, Switch }

    /// 实际操作与提示设备分开；插入手柄只更新提示，不伪造操作。
    public static class InputDeviceState
    {
        private static bool initialized;
        private static IDisposable buttons;
        private static InputAction touchPress;
        private static readonly Dictionary<Gamepad, Vector4> previousSticks = new();
        private static Gamepad promptGamepad;

        /// 最近实际操作的设备类别，决定触控区与交互视觉。
        public static InputDeviceKind ActiveKind { get; private set; }
        /// 最近实际活动的设备；平台触控模拟时可以为空。
        public static InputDevice ActiveDevice { get; private set; }
        /// 提示优先使用当前可用手柄，独立于实际操作设备。
        public static InputDevice PromptDevice => promptGamepad != null ? promptGamepad : ActiveDevice;
        /// 提示设备的类别。
        public static InputDeviceKind PromptKind => promptGamepad != null ? InputDeviceKind.Gamepad : ActiveKind;
        /// 根据实际布局识别的图标风格。
        public static GamepadStyle PromptStyle => GetStyle(promptGamepad);
        /// 实际设备、提示设备或布局改变；订阅方只在此时重建表现。
        public static event Action Changed;
        /// 实际输入活动，供具有设备白名单的会话更新自己的来源。
        public static event Action<InputDevice> Activity;

        /// Core Root 与独立输入会话共用一次订阅。
        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            ActiveKind = Application.isMobilePlatform ? InputDeviceKind.Touch : InputDeviceKind.KeyboardMouse;
            ActiveDevice = Application.isMobilePlatform ? Touchscreen.current : Keyboard.current;
            buttons = InputSystem.onAnyButtonPress.Call(control => Notify(control.device));
            touchPress = new InputAction("IdentifyPhysicalTouch", InputActionType.PassThrough, "<Touchscreen>/touch*/press");
            touchPress.performed += OnTouch;
            touchPress.Enable();
            InputSystem.onAfterUpdate += OnAfterUpdate;
            InputSystem.onDeviceChange += OnDeviceChange;
            RefreshPrompt();
        }

        /// <summary>记录真实输入来源；静止设备与摇杆漂移不调用该入口。</summary>
        /// <param name="device">实际输入控制所属设备。</param>
        public static void Notify(InputDevice device)
        {
            Initialize();
            if (device == null || !device.added || !device.enabled) return;
            var kind = device is Gamepad ? InputDeviceKind.Gamepad : device is Touchscreen ? InputDeviceKind.Touch : InputDeviceKind.KeyboardMouse;
            if (!(device is Gamepad || device is Touchscreen || device is Keyboard || device is Mouse)) return;
            bool changed = ActiveDevice != device || ActiveKind != kind;
            ActiveDevice = device; ActiveKind = kind;
            if (device is Gamepad pad && promptGamepad != pad) { promptGamepad = pad; changed = true; }
            if (changed) Changed?.Invoke();
            Activity?.Invoke(device);
        }

        /// UI 触控区域报告真实手指活动，测试模拟指针时也能切换触屏视觉。
        public static void NotifyTouch()
        {
            Initialize();
            if (Touchscreen.current != null) { Notify(Touchscreen.current); return; }
            if (ActiveKind == InputDeviceKind.Touch) return;
            ActiveKind = InputDeviceKind.Touch; ActiveDevice = null; Changed?.Invoke();
        }

        /// <summary>仅根据 Input System 识别的布局分类，兼容设备不猜品牌。</summary>
        /// <param name="device">待识别手柄。</param>
        /// <returns>图标风格。</returns>
        public static GamepadStyle GetStyle(InputDevice device)
        {
            if (device == null) return GamepadStyle.Generic;
            string layout = device.layout;
            if (InputSystem.IsFirstLayoutBasedOnSecond(layout, "SwitchProControllerHID")) return GamepadStyle.Switch;
            if (InputSystem.IsFirstLayoutBasedOnSecond(layout, "DualShockGamepad")) return GamepadStyle.PlayStation;
            if (InputSystem.IsFirstLayoutBasedOnSecond(layout, "XInputController")) return GamepadStyle.Xbox;
            return GamepadStyle.Generic;
        }

        private static void OnTouch(InputAction.CallbackContext value)
        { if (value.ReadValue<float>() > 0) Notify(value.control.device); }

        private static void OnAfterUpdate()
        {
            if (Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0.01f) Notify(Mouse.current);
            foreach (var pad in Gamepad.all)
            {
                if (!pad.enabled) continue;
                var left = GameplayInputMath.ApplyDeadzone(pad.leftStick.ReadUnprocessedValue(), 0.2f, 1f);
                var right = GameplayInputMath.ApplyDeadzone(pad.rightStick.ReadUnprocessedValue(), 0.2f, 1f);
                var value = new Vector4(left.x, left.y, right.x, right.y);
                previousSticks.TryGetValue(pad, out var previous);
                previousSticks[pad] = value;
                if (value.sqrMagnitude > 0.0001f && (value - previous).sqrMagnitude > 0.0001f) Notify(pad);
            }
        }

        private static void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device == ActiveDevice && (!device.added || !device.enabled || change == InputDeviceChange.Disconnected))
            {
                // 最后一个手柄离线时提示立即回到可用平台入口，不保留已移除设备。
                ActiveDevice = Application.isMobilePlatform ? Touchscreen.current : Keyboard.current;
                ActiveKind = Application.isMobilePlatform ? InputDeviceKind.Touch : InputDeviceKind.KeyboardMouse;
            }
            if (device is Gamepad pad && (!pad.added || !pad.enabled || change == InputDeviceChange.Disconnected))
            {
                previousSticks.Remove(pad);
                if (promptGamepad == pad) promptGamepad = null;
            }
            RefreshPrompt();
            Changed?.Invoke(); // 键盘布局变化也会改变绑定文字。
        }

        private static void RefreshPrompt()
        {
            if (promptGamepad != null && promptGamepad.added && promptGamepad.enabled) return;
            promptGamepad = null;
            foreach (var pad in Gamepad.all) if (pad.added && pad.enabled) { promptGamepad = pad; break; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            buttons?.Dispose(); buttons = null;
            touchPress?.Dispose(); touchPress = null;
            InputSystem.onAfterUpdate -= OnAfterUpdate;
            InputSystem.onDeviceChange -= OnDeviceChange;
            previousSticks.Clear(); promptGamepad = null; ActiveDevice = null;
            Changed = null; Activity = null; initialized = false;
        }
    }
}
