using System;
using Core.Runtime.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.KagitKart
{
    /// 赛车专属动作映射；设备、门闩、触摸指针与菜单导航复用 Core。
    public sealed class KagitKartInput : IDisposable
    {
        private readonly InputActionSession session;
        private KagitKartControls touch;
        private bool touchLeft, touchRight;
        private bool pendingDrift, pendingItem;
        private bool driving;
        private bool disposed;
        private int capturedFrame = -1;
        private InputDevice activeDevice;
        private InputDeviceKind activeKind;

        public InputActionAsset Asset => session.Asset;
        public bool AutoAccelerate { get; set; }
        public bool IsTouch => InputDeviceState.ActiveKind == InputDeviceKind.Touch;
        public event Action DeviceChanged;
        public event Action InputCleared;

        /// <summary>使用正式保存的动作资产创建独立副本；默认仅启用全局快捷键。</summary>
        /// <param name="asset">Demo 动作模板，不修改其启用状态或绑定。</param>
        public KagitKartInput(InputActionAsset asset)
        {
            session = new InputActionSession(asset);
            session.SetMap("Global");
            activeDevice = InputDeviceState.ActiveDevice;
            activeKind = InputDeviceState.ActiveKind;
            InputDeviceState.Changed += OnDeviceChanged;
        }

        /// <summary>进入比赛启用驾驶，菜单与暂停只保留全局动作；保持输入必须先释放。</summary>
        /// <param name="enabled">是否可驾驶。</param>
        public void SetDriving(bool enabled)
        {
            driving = enabled;
            ClearPending();
            session.SetMap(enabled ? "Drive" : "Global");
            InputCleared?.Invoke();
        }

        /// 每个渲染帧调用，短按边沿保留到实际固定步读取，零模拟步帧也不丢操作。
        public void CaptureFrame()
        {
            if (disposed || !driving || capturedFrame == Time.frameCount)
            {
                return;
            }
            capturedFrame = Time.frameCount;
            if (activeKind == InputDeviceKind.Touch)
            {
                return;
            }
            bool pad = activeKind == InputDeviceKind.Gamepad;
            pendingDrift |= session.Pressed(pad ? "PadDrift" : "KeyDrift");
            pendingItem |= session.Pressed(pad ? "PadItem" : "KeyItem");
        }

        /// <summary>在固定步读取统一赛车命令；短按只在第一次读取时消费，按住仍持续交付。</summary>
        /// <param name="countdown">倒计时不使用自动油门，保留起步加速按键时机。</param>
        public KagitKartControls ReadControls(bool countdown)
        {
            if (disposed || !driving)
            {
                return default;
            }
            CaptureFrame();
            KagitKartControls result;
            if (activeKind == InputDeviceKind.Touch)
            {
                result = touch;
            }
            else
            {
                bool pad = activeKind == InputDeviceKind.Gamepad;
                float steer = session.ReadAxis(pad ? "PadSteer" : "KeySteer");
                if (activeKind == InputDeviceKind.Gamepad)
                {
                    if (session.Held("PadLeft"))
                    {
                        steer = -1;
                    }
                    if (session.Held("PadRight"))
                    {
                        steer = 1;
                    }
                }
                result = new KagitKartControls
                {
                    Steer = Mathf.Clamp(steer, -1, 1),
                    Throttle = Mathf.Clamp01(session.Read<float>(pad ? "PadThrottle" : "KeyThrottle")),
                    Brake = Mathf.Clamp01(session.Read<float>(pad ? "PadBrake" : "KeyBrake")),
                    Drift = session.Held(pad ? "PadDrift" : "KeyDrift"),
                    Item = session.Held(pad ? "PadItem" : "KeyItem"),
                    LookBack = session.Held(pad ? "PadLookBack" : "KeyLookBack")
                };
            }
            // 原作按命令合并触控与物理输入；显式显示触控区时鼠标也能操作保存的触控组件。
            if (Mathf.Abs(touch.Steer) > Mathf.Abs(result.Steer))
            {
                result.Steer = touch.Steer;
            }
            result.Throttle = Mathf.Max(result.Throttle, touch.Throttle);
            result.Brake = Mathf.Max(result.Brake, touch.Brake);
            result.Drift |= touch.Drift;
            result.Item |= touch.Item;
            result.LookBack |= touch.LookBack;
            result.Drift |= pendingDrift;
            result.Item |= pendingItem;
            pendingDrift = pendingItem = false;
            if (AutoAccelerate && !countdown && result.Brake < 0.5f)
            {
                result.Throttle = 1;
            }
            return result;
        }

        /// <summary>读取暂停、静音或计时重赛快捷键；菜单确认与返回仍由公共 UI 处理。</summary>
        /// <param name="command">Pause、Mute 或 Restart。</param>
        public bool Pressed(string command) => !disposed && session.Pressed(command);

        /// <summary>绑定到正式动作，提示组件根据其实际绑定显示图标与文字。</summary>
        /// <param name="command">Steer、Throttle、Brake、Drift、Item 或 LookBack。</param>
        /// <returns>对应提示设备的生效动作副本。</returns>
        public InputAction PromptAction(string command) => Asset.FindAction("Drive/" +
            (InputDeviceState.PromptKind == InputDeviceKind.Gamepad ? "Pad" : "Key") + command, true);

        /// <summary>接收公共 InputCommandButton 的保持回调，短按道具和漂移也锁存到固定步。</summary>
        /// <param name="command">Left、Right、Throttle、Brake、Drift、Item 或 LookBack；左右同时保持时回正。</param>
        /// <param name="held">指针是否保持。</param>
        public void SetTouchHold(string command, bool held)
        {
            if (disposed || !driving)
            {
                return;
            }
            switch (command)
            {
                case "Left":
                    touchLeft = held;
                    break;
                case "Right":
                    touchRight = held;
                    break;
                case "Throttle":
                    touch.Throttle = held ? 1 : 0;
                    break;
                case "Brake":
                    touch.Brake = held ? 1 : 0;
                    break;
                case "Drift":
                    pendingDrift |= held && !touch.Drift;
                    touch.Drift = held;
                    break;
                case "Item":
                    pendingItem |= held && !touch.Item;
                    touch.Item = held;
                    break;
                case "LookBack":
                    touch.LookBack = held;
                    break;
                default:
                    throw new ArgumentException("触控命令无效。", nameof(command));
            }
            touch.Steer = (touchRight ? 1 : 0) - (touchLeft ? 1 : 0);
        }

        /// 失焦、切后台及设备断连时清理保持状态并建立松键门闩，宿主另外冻结比赛时钟。
        public void ResetInput()
        {
            ClearPending();
            session.SetMap(driving ? "Drive" : "Global");
            InputCleared?.Invoke();
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            InputDeviceState.Changed -= OnDeviceChanged;
            ClearPending();
            InputCleared?.Invoke();
            InputCleared = null;
            DeviceChanged = null;
            session.Dispose();
        }

        private void OnDeviceChanged()
        {
            var nextDevice = InputDeviceState.ActiveDevice;
            var nextKind = InputDeviceState.ActiveKind;
            // 键盘和鼠标共用驾驶上下文，互切只更新提示，不释放驾驶命令。
            if (activeKind != nextKind || nextKind != InputDeviceKind.KeyboardMouse && activeDevice != nextDevice)
            {
                if (activeKind == InputDeviceKind.KeyboardMouse)
                {
                    session.RequireRelease(Keyboard.current);
                    session.RequireRelease(Mouse.current);
                }
                else
                {
                    session.RequireRelease(activeDevice);
                }
                ClearPending();
                InputCleared?.Invoke();
            }
            activeDevice = nextDevice;
            activeKind = nextKind;
            DeviceChanged?.Invoke();
        }

        private void ClearPending()
        {
            touch = default;
            touchLeft = touchRight = false;
            pendingDrift = pendingItem = false;
            capturedFrame = -1;
        }
    }
}
