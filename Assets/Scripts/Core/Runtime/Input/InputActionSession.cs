using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Core.Runtime.Inputs
{
    /// 动作资产副本与上下文门闩；业务直接消费自己的动作，不向 Core 增加玩法枚举。
    public sealed class InputActionSession : IDisposable
    {
        private readonly HashSet<InputAction> awaitingRelease = new();
        private bool disposed;
        /// 当前会话拥有的动作副本，供提示绑定到同一真实动作。
        public InputActionAsset Asset { get; }
        /// 当前启用的业务上下文；关闭输入时为空。
        public InputActionMap Map { get; private set; }

        /// <summary>克隆宿主动作资产，不修改原资产启用状态。</summary>
        /// <param name="source">保存的动作资产。</param>
        public InputActionSession(InputActionAsset source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            InputDeviceState.Initialize();
            Asset = UnityEngine.Object.Instantiate(source);
            Asset.devices = source.devices;
            Asset.Disable();
        }

        /// <summary>切换 Map；旧按键和轴必须先回到中立，避免打开菜单后重复操作。</summary>
        /// <param name="name">Map 名称；null 表示关闭业务输入。</param>
        public void SetMap(string name)
        {
            if (disposed) throw new ObjectDisposedException(nameof(InputActionSession));
            Asset.Disable(); awaitingRelease.Clear();
            Map = name == null ? null : Asset.FindActionMap(name, true);
            if (Map == null) return;
            Map.Enable();
            foreach (var action in Map.actions) if (HasInput(action)) awaitingRelease.Add(action);
        }

        /// <summary>读取本帧离散动作边沿，不自行提交 UI。</summary>
        /// <param name="name">当前 Map 中的动作名称。</param>
        /// <returns>本帧首次真实按下。</returns>
        public bool Pressed(string name) => Available(name, out var action) && action.WasPressedThisFrame();
        /// <summary>读取按钮保持状态。</summary>
        /// <param name="name">动作名称。</param>
        /// <returns>是否按住且通过松键门闩。</returns>
        public bool Held(string name) => Available(name, out var action) && action.IsPressed();
        /// <summary>读取连续轴；被门闩屏蔽时返回零。</summary>
        /// <typeparam name="T">动作值类型。</typeparam>
        /// <param name="name">动作名称。</param>
        /// <returns>连续输入。</returns>
        public T Read<T>(string name) where T : struct => Available(name, out var action) ? action.ReadValue<T>() : default;

        /// <summary>摇杆读取原始值后只应用一次公共死区；键盘组合轴保持原定义。</summary>
        /// <param name="name">二维动作名称。</param>
        /// <param name="deadzone">径向死区。</param>
        /// <returns>归一化摇杆或原键盘组合值。</returns>
        public Vector2 ReadVector(string name, float deadzone = .2f)
        {
            if (!Available(name, out var action)) return Vector2.zero;
            return action.activeControl is StickControl stick
                ? GameplayInputMath.ApplyDeadzone(stick.ReadUnprocessedValue(), deadzone, 1)
                : action.ReadValue<Vector2>();
        }

        /// <summary>手柄单轴读取原始值后只应用一次死区，避免布局处理器与专属动作处理器叠加。</summary>
        /// <param name="name">标量动作名称。</param>
        /// <param name="deadzone">轴死区，范围 [0,1)。</param>
        public float ReadAxis(string name, float deadzone = .2f)
        {
            if (float.IsNaN(deadzone) || float.IsInfinity(deadzone) || deadzone < 0 || deadzone >= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(deadzone));
            }
            if (!Available(name, out var action))
            {
                return 0;
            }
            if (action.activeControl is AxisControl axis && axis.device is Gamepad)
            {
                float value = axis.ReadUnprocessedValue();
                return Mathf.Sign(value) * Mathf.Clamp01((Mathf.Abs(value) - deadzone) / (1 - deadzone));
            }
            return action.ReadValue<float>();
        }

        /// <summary>实际设备切换时只锁住旧设备仍保持的动作，不吞掉新设备的首次操作。</summary>
        /// <param name="device">离开的输入设备；为空时不改变动作。</param>
        public void RequireRelease(InputDevice device)
        {
            if (device == null || Map == null || disposed)
            {
                return;
            }
            foreach (var action in Map.actions)
            {
                foreach (var control in action.controls)
                {
                    if (control.device == device && HasInput(control, action.type))
                    {
                        awaitingRelease.Add(action);
                        break;
                    }
                }
            }
        }

        private bool Available(string name, out InputAction action)
        {
            action = Map?.FindAction(name);
            if (disposed || action == null || !action.enabled) return false;
            if (!awaitingRelease.Contains(action)) return true;
            if (!HasInput(action)) awaitingRelease.Remove(action);
            return false;
        }

        private static bool HasInput(InputAction action)
        {
            foreach (var control in action.controls)
            {
                if (HasInput(control, action.type))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HasInput(InputControl control, InputActionType type)
        {
            if (control is ButtonControl button && button.isPressed)
            {
                return true;
            }
            // 屏幕位置是绝对坐标，不要求鼠标回到屏幕原点。
            if (control.device is Gamepad && control is Vector2Control vector && vector.ReadValue().sqrMagnitude > 0.04f)
            {
                return true;
            }
            if (control.device is Gamepad && control is AxisControl axis)
            {
                float neutral = control is ButtonControl && type == InputActionType.Value ? 0.01f : 0.2f;
                return Mathf.Abs(axis.ReadValue()) > neutral;
            }
            return false;
        }

        /// 释放副本和回调，重复调用无副作用。
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Asset.Disable(); awaitingRelease.Clear();
            if (Application.isPlaying) UnityEngine.Object.Destroy(Asset); else UnityEngine.Object.DestroyImmediate(Asset);
        }
    }
}
