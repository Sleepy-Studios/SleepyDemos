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
            string prefix = activeKind == InputDeviceKind.Gamepad ? "Pad" : "Key";
            pendingDrift |= session.Pressed(prefix + "Drift");
            pendingItem |= session.Pressed(prefix + "Item");
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
                string prefix = activeKind == InputDeviceKind.Gamepad ? "Pad" : "Key";
                float steer = session.ReadAxis(prefix + "Steer", 0.18f);
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
                    Throttle = Mathf.Clamp01(session.Read<float>(prefix + "Throttle")),
                    Brake = Mathf.Clamp01(session.Read<float>(prefix + "Brake")),
                    Drift = session.Held(prefix + "Drift"),
                    Item = session.Held(prefix + "Item"),
                    LookBack = session.Held(prefix + "LookBack")
                };
            }
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

        /// <summary>接收公共触控滑条的归一化横轴。</summary>
        /// <param name="steer">有限的转向值，内部钳制到 [-1,1]。</param>
        public void SetTouchSteer(float steer)
        {
            if (float.IsNaN(steer) || float.IsInfinity(steer))
            {
                throw new ArgumentOutOfRangeException(nameof(steer));
            }
            if (driving && !disposed)
            {
                touch.Steer = Mathf.Clamp(steer, -1, 1);
            }
        }

        /// <summary>接收公共 InputCommandButton 的保持回调，短按道具和漂移也锁存到固定步。</summary>
        /// <param name="command">Throttle、Brake、Drift、Item 或 LookBack。</param>
        /// <param name="held">指针是否保持。</param>
        public void SetTouchHold(string command, bool held)
        {
            if (disposed || !driving)
            {
                return;
            }
            switch (command)
            {
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
            if (activeDevice != InputDeviceState.ActiveDevice || activeKind != InputDeviceState.ActiveKind)
            {
                session.RequireRelease(activeDevice);
                activeDevice = InputDeviceState.ActiveDevice;
                activeKind = InputDeviceState.ActiveKind;
                ClearPending();
                InputCleared?.Invoke();
            }
            DeviceChanged?.Invoke();
        }

        private void ClearPending()
        {
            touch = default;
            pendingDrift = pendingItem = false;
            capturedFrame = -1;
        }
    }
}
