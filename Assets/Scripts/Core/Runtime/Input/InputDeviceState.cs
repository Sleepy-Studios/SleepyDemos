using System;
using System.Collections.Generic;
using Core.Runtime.Settings;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Core.Runtime.Inputs
{
    public enum GamepadStyle { Xbox = 1, PlayStation = 2, Switch = 3 }

    /// 设备提示跟随实际操作；连接手柄不抢占提示或伪造输入。
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
        /// 提示跟随最近真实操作；连接手柄本身不改变键鼠提示。
        public static InputDevice PromptDevice => ActiveKind == InputDeviceKind.Gamepad ? promptGamepad : ActiveDevice;
        /// 提示设备的类别。
        public static InputDeviceKind PromptKind => ActiveKind;
        /// 玩家已应用的图标风格；实际设备仍负责物理控件与用途解析。
        public static GamepadStyle PromptStyle => GameSettingsService.CurrentGamepadStyle;
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
