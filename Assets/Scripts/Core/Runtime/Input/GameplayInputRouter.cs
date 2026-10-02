using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;

namespace Core.Runtime.Inputs
{
    /// 宿主独立的物理输入边界。独占ActionAsset副本，不修改全局资产或Core UI ActionMap。
    public sealed class GameplayInputRouter : IDisposable
    {
        private readonly InputActionAsset asset;
        private readonly InputActionMap gameplayMap;
        private readonly InputActionMap interactionMap;
        private readonly InputActionMap menu;
        private readonly IDisposable buttonSubscription;
        private readonly HashSet<InputAction> awaitingRelease = new HashSet<InputAction>();
        private readonly Dictionary<InputControl, Vector2> previousPadValues = new Dictionary<InputControl, Vector2>();
        private InputActionMap activeMap;
        private GameplayInputSettings settings;
        private GameplayInputActions pending;
        private bool pendingPointerPress;
        private bool pendingPointerMove;
        private bool hasPointerPosition;
        private Vector2 previousPointerPosition;
        private Vector2 touchMove;
        private Vector2 touchLook;
        private bool continuousSuppressed;
        private bool skipLookDelta;
        private bool disposed;
        private Gamepad activeGamepad;
        private Gamepad rumbleGamepad;
        private float rumbleRemaining;
        private GameplayInputContext context;
        private InputDeviceKind deviceKind;

        /// 暂停门闩，宿主必须据此冻结领域与场景演出，而不是仅挡移动。
        public LocalPauseState PauseState { get; } = new LocalPauseState();
        public GameplayInputContext Context => context;
        public InputDeviceKind DeviceKind => deviceKind;
        public GameplayInputSettings Settings => settings.Copy();
        /// 更换提示图标或按钮说明；Gamepad遵循标准Xbox A/B/X/Y映射，不排斥兼容手柄。
        public event Action<InputDeviceKind> DeviceChanged;

        /// <summary>克隆已保存的宿主资产并启用Gameplay；源资产保持原状态，Dispose释放副本和订阅。</summary>
        /// <param name="source">宿主保存的动作资产；克隆并只启用指定上下文，不改原资产。</param>
        /// <param name="inputSettings">为空使用默认参数，非法有限值或版本抛出异常。</param>
        /// <param name="gameplayMapName">移动、视角和交互入口所在Map，默认Gameplay。</param>
        /// <param name="interactionMapName">物件选择和确认动作所在Map，默认Interaction。</param>
        /// <param name="menuMapName">菜单Pause动作所在Map，公共UI独立处理导航。</param>
        public GameplayInputRouter(InputActionAsset source, GameplayInputSettings inputSettings = null,
            string gameplayMapName = "Gameplay", string interactionMapName = "Interaction", string menuMapName = "Menu")
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            settings = (inputSettings ?? new GameplayInputSettings()).Copy();
            if (!settings.IsValid) throw new ArgumentException("输入配置无效。", nameof(inputSettings));
            ValidateAsset(source, gameplayMapName, interactionMapName, menuMapName);
            asset = UnityEngine.Object.Instantiate(source);
            // devices是运行时属性，Instantiate不依赖它被序列化。测试与限定控制器可在源资产设置白名单。
            asset.devices = source.devices;
            gameplayMap = asset.FindActionMap(gameplayMapName, true);
            interactionMap = asset.FindActionMap(interactionMapName, true);
            menu = asset.FindActionMap(menuMapName, true);
            foreach (var map in new[] { gameplayMap, interactionMap, menu })
                foreach (var action in map.actions) { action.performed += OnPerformed; action.canceled += OnCanceled; }
            asset.Disable();
            PauseState.Changed += OnPauseChanged;
            InputSystem.onDeviceChange += OnDeviceChange;
            buttonSubscription = InputSystem.onAnyButtonPress.Call(OnAnyButtonPress);
            ActivateContext(GameplayInputContext.Gameplay);
        }

        /// <summary>更换输入上下文并清除旧边沿；仍按住的按钮必须释放再按，避免Interact转成Confirm。</summary>
        /// <param name="value">Gameplay用于移动与视角，Interaction用于物件操作，Menu仅交给现有UI模块导航。</param>
        public void SetContext(GameplayInputContext value)
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(typeof(GameplayInputContext), value)) throw new ArgumentOutOfRangeException(nameof(value));
            if (context == value && activeMap != null) return;
            ActivateContext(value);
        }

        /// <summary>明确应用经校验的预览或已保存参数；关闭震动立即停止本输入会话所发震动。</summary>
        /// <param name="value">独立有限值副本，坏值拒绝且保留当前参数。</param>
        public void ApplySettings(GameplayInputSettings value)
        {
            ThrowIfDisposed();
            if (value == null || !value.IsValid) throw new ArgumentException("输入配置无效。", nameof(value));
            settings = value.Copy();
            if (!settings.RumbleEnabled || settings.RumbleStrength == 0) StopRumble();
        }

        /// <summary>菜单作用域报告Core UI模块的实际导航或指针设备，只更换提示，不再次提交UI动作。</summary>
        /// <param name="device">实际InputAction回调中的设备；不在本资产白名单内或非Menu上下文时忽略。</param>
        public void NotifyMenuDevice(InputDevice device)
        {
            if (disposed) return;
            if (context == GameplayInputContext.Menu && device != null && IsAllowedDevice(device)) RecordDevice(device);
        }

        /// <summary>每个宿主Update调用一次；暂停时始终返回零连续量，鼠标与触控不乘帧时长。</summary>
        /// <param name="unscaledDeltaSeconds">非负有限的未缩放帧时长，长帧不隐式改变领域时钟。</param>
        /// <param name="mouseDegreesPerPixel">既有GameSettings.LookSensitivity默认0.12。</param>
        /// <param name="pointerLocked">宿主已在探索中锁定光标时为true，允许无需右键转向；默认保留旧右键语义。</param>
        public GameplayInputFrame ReadFrame(float unscaledDeltaSeconds, float mouseDegreesPerPixel = 0.12f, bool pointerLocked = false)
        {
            ThrowIfDisposed();
            if (!Finite(unscaledDeltaSeconds) || unscaledDeltaSeconds < 0 || !Finite(mouseDegreesPerPixel) || mouseDegreesPerPixel < 0)
                throw new ArgumentOutOfRangeException(nameof(unscaledDeltaSeconds));
            TickRumble(unscaledDeltaSeconds);
            RefreshReleasedButtons();
            Vector2 keyboardMove = context == GameplayInputContext.Gameplay ? gameplayMap["Move"].ReadValue<Vector2>() : Vector2.zero;
            Vector2 padMove = context == GameplayInputContext.Gameplay ? ReadPadVector(gameplayMap["PadMove"]) : Vector2.zero;
            Vector2 padLook = context == GameplayInputContext.Gameplay ? ReadPadVector(gameplayMap["PadLook"]) : Vector2.zero;
            Vector2 navigation = context == GameplayInputContext.Interaction ? interactionMap["Navigate"].ReadValue<Vector2>() + ReadPadVector(interactionMap["PadNavigate"]) : Vector2.zero;
            Vector2 mouse = context == GameplayInputContext.Gameplay && (pointerLocked || !awaitingRelease.Contains(gameplayMap["LookHold"])
                && gameplayMap["LookHold"].IsPressed()) ? gameplayMap["MouseLook"].ReadValue<Vector2>() : Vector2.zero;
            Vector2 touchDelta = touchLook; touchLook = Vector2.zero;
            if (mouse.sqrMagnitude > 0.01f)
                SetDevice(InputDeviceKind.KeyboardMouse);
            if (continuousSuppressed && keyboardMove.sqrMagnitude + padMove.sqrMagnitude + padLook.sqrMagnitude + navigation.sqrMagnitude + touchMove.sqrMagnitude < 0.0001f)
                continuousSuppressed = false;
            var frame = new GameplayInputFrame { DeviceKind = deviceKind, IsPaused = PauseState.IsPaused };
            bool pointerPressed = pendingPointerPress; pendingPointerPress = false;
            bool pointerMoved = pendingPointerMove; pendingPointerMove = false;
            if (PauseState.IsPaused || continuousSuppressed) return frame;
            // 模态关闭/光标重新锁定可能产生warp增量，第一次恢复采样只丢弃增量，不吞真实离散按键。
            if (skipLookDelta) { mouse = touchDelta = Vector2.zero; skipLookDelta = false; }
            if (context == GameplayInputContext.Gameplay)
            {
                frame.Move = Vector2.ClampMagnitude(keyboardMove + padMove + touchMove, 1);
                frame.LookDegrees = GameplayInputMath.LookDegrees(mouse, touchDelta, padLook, unscaledDeltaSeconds, mouseDegreesPerPixel, settings);
            }
            else if (context == GameplayInputContext.Interaction)
            {
                frame.InteractionNavigation = Vector2.ClampMagnitude(navigation, 1);
                frame.PointerPosition = interactionMap["Point"].ReadValue<Vector2>();
                frame.PointerPressed = pointerPressed;
                frame.PointerMoved = pointerMoved;
            }
            return frame;
        }

        /// 读取并清空离散动作。每次物理按下最多交付一次；Menu从不交付Confirm/Back/导航。
        public GameplayInputActions ConsumeActions()
        {
            ThrowIfDisposed();
            var value = pending; pending = GameplayInputActions.None;
            return PauseState.IsPaused ? value & GameplayInputActions.Pause : value;
        }

        /// 光标重新锁定时丢弃下一次鼠标/触屏增量，避免系统居中造成视角跳跃。
        public void DiscardNextLookDelta() => skipLookDelta = true;

        /// <summary>传入已有TouchPad读数；切上下文或暂停会清空，宿主也应ResetInput释放旧pointer。</summary>
        /// <param name="move">归一化移动量，直到下一次更新保持。</param>
        /// <param name="lookDelta">720p参考像素的一次增量，ReadFrame后清空。</param>
        /// <param name="hadActivity">真实指针按下或拖动时为true，用于切换设备提示。</param>
        public void SetTouchFrame(Vector2 move, Vector2 lookDelta, bool hadActivity)
        {
            ThrowIfDisposed();
            if (!Finite(move.x) || !Finite(move.y) || !Finite(lookDelta.x) || !Finite(lookDelta.y)) throw new ArgumentException("触屏输入必须为有限值。");
            if (hadActivity) SetDevice(InputDeviceKind.Touch);
            if (context != GameplayInputContext.Gameplay || PauseState.IsPaused) { touchMove = touchLook = Vector2.zero; return; }
            touchMove = Vector2.ClampMagnitude(move, 1); touchLook += lookDelta;
        }

        /// <summary>触屏虚拟按钮或物理桌面指针统一注入一个离散动作，不代为执行按钮或领域资金操作。</summary>
        /// <param name="action">本上下文允许的单一动作，组合或未定义位拒绝。</param>
        /// <returns>本次输入入队成功；UI菜单Submit/Cancel必须继续由Core模块处理。</returns>
        public bool QueueTouchAction(GameplayInputActions action)
        {
            ThrowIfDisposed();
            int numeric = (int)action;
            if (numeric <= 0 || (numeric & (numeric - 1)) != 0 || !Allowed(action) || PauseState.IsPaused && action != GameplayInputActions.Pause) return false;
            SetDevice(InputDeviceKind.Touch); pending |= action; return true;
        }

        /// <summary>播放可选短震动；仅作用于最近实际使用的手柄，暂停/断连/设备切换/Dispose停止。</summary>
        /// <param name="lowFrequency">0至1的低频电机强度。</param>
        /// <param name="highFrequency">0至1的高频电机强度。</param>
        /// <param name="durationSeconds">大于0且不超过2秒，读取帧时推进未缩放计时。</param>
        public bool PlayRumble(float lowFrequency, float highFrequency, float durationSeconds)
        {
            ThrowIfDisposed();
            if (!Finite(lowFrequency) || !Finite(highFrequency) || lowFrequency < 0 || lowFrequency > 1 || highFrequency < 0 || highFrequency > 1
                || !Finite(durationSeconds) || durationSeconds <= 0 || durationSeconds > 2) throw new ArgumentOutOfRangeException(nameof(durationSeconds));
            if (!settings.RumbleEnabled || settings.RumbleStrength == 0 || PauseState.IsPaused || deviceKind != InputDeviceKind.Gamepad
                || activeGamepad == null || !activeGamepad.added || !activeGamepad.enabled) return false;
            StopRumble(); rumbleGamepad = activeGamepad; rumbleRemaining = durationSeconds;
            rumbleGamepad.SetMotorSpeeds(lowFrequency * settings.RumbleStrength, highFrequency * settings.RumbleStrength); return true;
        }

        /// 停止自己所发震动；不调用InputSystem.ResetHaptics影响其它Demo或设备。
        public void StopRumble()
        {
            // InputSystem先标记disabled再通知；逻辑禁用仍可发送归零，不能留下已开启的电机。
            if (rumbleGamepad != null && rumbleGamepad.added) rumbleGamepad.SetMotorSpeeds(0, 0);
            rumbleGamepad = null; rumbleRemaining = 0;
        }

        /// 取消所有输入订阅与私有资产，重复调用无副作用。
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; StopRumble(); buttonSubscription.Dispose();
            InputSystem.onDeviceChange -= OnDeviceChange; PauseState.Changed -= OnPauseChanged;
            foreach (var map in new[] { gameplayMap, interactionMap, menu })
                foreach (var action in map.actions) { action.performed -= OnPerformed; action.canceled -= OnCanceled; }
            asset.Disable(); awaitingRelease.Clear(); previousPadValues.Clear(); pending = GameplayInputActions.None;
            if (Application.isPlaying) UnityEngine.Object.Destroy(asset); else UnityEngine.Object.DestroyImmediate(asset);
        }

        private void ActivateContext(GameplayInputContext value)
        {
            asset.Disable(); context = value; pending = GameplayInputActions.None; pendingPointerPress = false; pendingPointerMove = false;
            hasPointerPosition = false;
            touchMove = touchLook = Vector2.zero; awaitingRelease.Clear(); continuousSuppressed = true; skipLookDelta = true;
            activeMap = value == GameplayInputContext.Gameplay ? gameplayMap : value == GameplayInputContext.Interaction ? interactionMap : menu;
            CaptureHeldButtons(); activeMap.Enable();
        }

        private void OnPerformed(InputAction.CallbackContext callback)
        {
            if (disposed || callback.action.actionMap != activeMap || awaitingRelease.Contains(callback.action)) return;
            if (callback.action.name == "Point")
            {
                Vector2 position = callback.ReadValue<Vector2>();
                if (!hasPointerPosition || (position - previousPointerPosition).sqrMagnitude >= 4)
                {
                    pendingPointerMove = true;
                    RecordDevice(callback.control.device);
                }
                previousPointerPosition = position; hasPointerPosition = true;
                return;
            }
            if (callback.action.type != InputActionType.Button) return;
            RecordDevice(callback.control.device);
            if (callback.action.name == "Click")
            {
                if (!PauseState.IsPaused) pendingPointerPress = true;
                return;
            }
            if (TryAction(callback.action.name, out var action) && Allowed(action) && (!PauseState.IsPaused || action == GameplayInputActions.Pause)) pending |= action;
        }

        private void OnCanceled(InputAction.CallbackContext callback) => awaitingRelease.Remove(callback.action);
        private void OnAnyButtonPress(InputControl control) { if (!disposed && IsAllowedDevice(control.device)) RecordDevice(control.device); }

        private void RecordDevice(InputDevice device)
        {
            if (device is Gamepad gamepad) { activeGamepad = gamepad; SetDevice(InputDeviceKind.Gamepad); }
            else if (device is Touchscreen) SetDevice(InputDeviceKind.Touch);
            else if (device is Keyboard || device is Mouse) SetDevice(InputDeviceKind.KeyboardMouse);
        }

        private void SetDevice(InputDeviceKind value)
        {
            if (value != InputDeviceKind.Gamepad) PauseState.SetGamepadAvailable(true);
            if (deviceKind == value) return;
            if (value != InputDeviceKind.Gamepad) StopRumble();
            deviceKind = value; DeviceChanged?.Invoke(value);
        }

        private Vector2 ReadPadVector(InputAction action)
        {
            Vector2 strongest = Vector2.zero; Gamepad changedSource = null;
            foreach (var control in action.controls)
            {
                if (!(control.device is Gamepad gamepad) || !gamepad.added || !gamepad.enabled) continue;
                Vector2 value;
                // 绕过设备布局自带stickDeadzone，再应用本输入会话参数一次；不能把死区处理重复叠加。
                if (control is StickControl stick) value = GameplayInputMath.ApplyDeadzone(stick.ReadUnprocessedValue(), settings.GamepadDeadzone, settings.GamepadMaximum);
                else if (control is Vector2Control vector) value = vector.ReadValue();
                else continue;
                previousPadValues.TryGetValue(control, out Vector2 previous);
                if (value.sqrMagnitude > 0.0001f && (value - previous).sqrMagnitude > 0.0001f) changedSource = gamepad;
                previousPadValues[control] = value;
                if (value.sqrMagnitude <= strongest.sqrMagnitude) continue;
                strongest = value;
            }
            // 持续按住的摇杆不抢回刚切换的键鼠提示；只有实际变化且越过死区才更换设备。
            if (changedSource != null) { activeGamepad = changedSource; SetDevice(InputDeviceKind.Gamepad); }
            return strongest;
        }

        private bool IsAllowedDevice(InputDevice device)
        {
            var allowed = asset.devices;
            if (!allowed.HasValue) return true;
            foreach (var candidate in allowed.Value) if (candidate == device) return true;
            return false;
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (!(device is Gamepad gamepad)) return;
            bool unavailable = change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected || change == InputDeviceChange.Disabled;
            if (unavailable && device == activeGamepad && deviceKind == InputDeviceKind.Gamepad)
                PauseState.SetGamepadAvailable(false);
            else if (IsAllowedDevice(device) && (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected || change == InputDeviceChange.Enabled))
            {
                if (device == activeGamepad && PauseState.IsPaused) gamepad.SetMotorSpeeds(0, 0);
                PauseState.SetGamepadAvailable(true);
            }
        }

        private void OnPauseChanged()
        {
            pending = GameplayInputActions.None; pendingPointerPress = false; pendingPointerMove = false; touchMove = touchLook = Vector2.zero;
            continuousSuppressed = true; skipLookDelta = true; CaptureHeldButtons(); StopRumble();
        }

        private void CaptureHeldButtons()
        {
            if (activeMap == null) return;
            foreach (var action in activeMap.actions) if (action.type == InputActionType.Button && HasPressedControl(action)) awaitingRelease.Add(action);
        }

        private void RefreshReleasedButtons()
        {
            awaitingRelease.RemoveWhere(action => !HasPressedControl(action));
        }

        private static bool HasPressedControl(InputAction action)
        {
            foreach (var control in action.controls) if (control is ButtonControl button && button.isPressed) return true;
            return false;
        }

        private bool Allowed(GameplayInputActions action) => action == GameplayInputActions.Pause ||
            context == GameplayInputContext.Gameplay && action == GameplayInputActions.Interact ||
            context == GameplayInputContext.Interaction && (action == GameplayInputActions.Confirm || action == GameplayInputActions.Back
                || action == GameplayInputActions.Secondary || action == GameplayInputActions.Help
                || action == GameplayInputActions.PreviousGroup || action == GameplayInputActions.NextGroup);

        private static bool TryAction(string name, out GameplayInputActions action)
            => Enum.TryParse(name, out action) && action != GameplayInputActions.None;
        private void TickRumble(float delta) { if (rumbleGamepad != null && (rumbleRemaining -= delta) <= 0) StopRumble(); }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(GameplayInputRouter)); }

        private static void ValidateAsset(InputActionAsset value, string gameplay, string interaction, string menu)
        {
            if (string.IsNullOrWhiteSpace(gameplay) || string.IsNullOrWhiteSpace(interaction) || string.IsNullOrWhiteSpace(menu)
                || gameplay == interaction || gameplay == menu || interaction == menu)
                throw new ArgumentException("输入上下文必须使用三个不同的有效Map名称。");
            foreach (string name in new[] { "Move", "PadMove", "MouseLook", "LookHold", "PadLook", "Interact", "Pause" })
                value.FindActionMap(gameplay, true).FindAction(name, true);
            foreach (string name in new[] { "Point", "Click", "Navigate", "PadNavigate", "Confirm", "Back", "Secondary", "Help", "PreviousGroup", "NextGroup", "Pause" })
                value.FindActionMap(interaction, true).FindAction(name, true);
            var menuMap = value.FindActionMap(menu, true); menuMap.FindAction("Pause", true);
            if (menuMap.actions.Count != 1)
                throw new ArgumentException("菜单Map只提供Pause；Submit/Cancel/Navigate由公共UI模块处理。", nameof(value));
        }
    }
}
