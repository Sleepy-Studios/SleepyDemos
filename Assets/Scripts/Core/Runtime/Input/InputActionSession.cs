using System;
using System.Collections.Generic;
using Core.Runtime.Settings;
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
        private GameplayInputSettings settings;
        private string appliedOverrides;

        /// 源资产首个动作 GUID，不依赖资源名称或路径。
        public string SourceId { get; }

        /// 当前会话输入参数的独立副本。
        public GameplayInputSettings Settings => settings.Copy();
        /// 当前会话拥有的动作副本，供提示绑定到同一真实动作。
        public InputActionAsset Asset { get; }
        /// 当前启用的业务上下文；关闭输入时为空。
        public InputActionMap Map { get; private set; }

        /// <summary>克隆宿主动作资产，不修改原资产启用状态。</summary>
        /// <param name="source">保存的动作资产。</param>
        public InputActionSession(InputActionAsset source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            SourceId = GetSourceId(source);
            InputDeviceState.Initialize();
            Asset = UnityEngine.Object.Instantiate(source);
            Asset.devices = source.devices;
            Asset.Disable();
            try
            {
                OnSettingsChanged(GameSettingsService.Current);
                GameSettingsService.Changed += OnSettingsChanged;
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>取得源动作资产的持久化标识；不能传没有动作的空资产。</summary>
        /// <param name="source">正式源动作资产，重命名不改变标识。</param>
        /// <returns>首个动作的稳定 GUID。</returns>
        public static string GetSourceId(InputActionAsset source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            foreach (var action in source)
            {
                return action.id.ToString();
            }
            throw new ArgumentException("输入资产必须包含至少一个动作。", nameof(source));
        }

        /// <summary>应用独立通用参数，不修改 Input System 全局处理器。</summary>
        /// <param name="value">有效的输入参数，调用后保留副本。</param>
        public void ApplySettings(GameplayInputSettings value)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(InputActionSession));
            }
            if (value == null || !value.IsValid)
            {
                throw new ArgumentException("输入配置无效。", nameof(value));
            }
            settings = value.Copy();
        }

        private void OnSettingsChanged(GameSettingsSnapshot value)
        {
            ApplySettings(value.Input);
            value.BindingOverrides.TryGetValue(SourceId, out var overrides);
            overrides ??= string.Empty;
            if (overrides == appliedOverrides)
            {
                return;
            }
            // 先在候选副本校验，损坏记录不能清掉运行中的绑定。
            using var candidate = new InputRebindSession(Asset, overrides);
            string candidateOverrides = candidate.ExportOverrides();
            if (candidateOverrides == Asset.SaveBindingOverridesAsJson())
            {
                appliedOverrides = overrides;
                return;
            }
            string mapName = Map?.name;
            var enabledActions = new List<InputAction>();
            foreach (var action in Asset)
            {
                if (action.enabled)
                {
                    enabledActions.Add(action);
                }
            }
            Asset.Disable();
            Asset.LoadBindingOverridesFromJson(candidateOverrides);
            appliedOverrides = overrides;
            if (mapName != null)
            {
                SetMap(mapName);
            }
            // Router 自己管理 Map，Demo 浮层也可能额外启用关闭快捷键；两者都要保留。
            foreach (var action in enabledActions)
            {
                if (!action.enabled)
                {
                    action.Enable();
                }
            }
        }

        /// <summary>切换 Map；旧按键和轴必须先回到中立，避免打开菜单后重复操作。</summary>
        /// <param name="name">Map 名称；null 表示关闭业务输入。</param>
        public void SetMap(string name)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(InputActionSession));
            }
            Asset.Disable();
            awaitingRelease.Clear();
            Map = name == null ? null : Asset.FindActionMap(name, true);
            if (Map == null)
            {
                return;
            }
            Map.Enable();
            foreach (var action in Map.actions)
            {
                if (HasInput(action))
                {
                    awaitingRelease.Add(action);
                }
            }
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
        /// <param name="deadzone">为空使用公共设置；显式值只覆盖本次读取。</param>
        /// <returns>归一化摇杆或原键盘组合值。</returns>
        public Vector2 ReadVector(string name, float? deadzone = null)
        {
            if (!Available(name, out var action))
            {
                return Vector2.zero;
            }
            return action.activeControl is StickControl stick
                ? GameplayInputMath.ApplyDeadzone(stick.ReadUnprocessedValue(), deadzone ?? settings.GamepadDeadzone, settings.GamepadMaximum)
                : action.ReadValue<Vector2>();
        }

        /// <summary>手柄单轴读取原始值后只应用一次死区，避免布局处理器与专属动作处理器叠加。</summary>
        /// <param name="name">标量动作名称。</param>
        /// <param name="deadzone">为空使用公共设置；显式轴死区范围 [0,1)。</param>
        public float ReadAxis(string name, float? deadzone = null)
        {
            float minimum = deadzone ?? settings.GamepadDeadzone;
            float maximum = deadzone.HasValue ? 1 : settings.GamepadMaximum;
            if (float.IsNaN(minimum) || float.IsInfinity(minimum) || minimum < 0 || minimum >= 1)
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
                return Mathf.Sign(value) * Mathf.Clamp01((Mathf.Abs(value) - minimum) / (maximum - minimum));
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
            if (disposed || action == null || !action.enabled)
            {
                return false;
            }
            if (!awaitingRelease.Contains(action))
            {
                return true;
            }
            if (!HasInput(action))
            {
                awaitingRelease.Remove(action);
            }
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
            if (disposed)
            {
                return;
            }
            disposed = true;
            GameSettingsService.Changed -= OnSettingsChanged;
            Asset.Disable();
            awaitingRelease.Clear();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(Asset);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(Asset);
            }
        }
    }
}
