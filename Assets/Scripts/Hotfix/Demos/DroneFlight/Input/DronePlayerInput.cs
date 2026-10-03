using System;
using System.Collections.Generic;
using Core.Runtime.Inputs;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix.DroneFlight
{
    /// 四轴输入只转成飞控命令；资产、死区、设备和松键门闩使用 Core 实现。
    [DefaultExecutionOrder(-200), RequireComponent(typeof(DroneFlightController))]
    public sealed class DronePlayerInput : MonoBehaviour
    {
        [SerializeField] private DroneInputConfig config;
        [SerializeField] private InputActionAsset actions;
        private InputActionSession session;
        private DroneFlightController controller;
        private DroneCameraRig cameraRig;
        private DroneEquipmentHost equipment;
        private DroneLandingGearController landingGear;
        private Vector4 smoothedKeyboardInput;
        private Vector2 touchLeft, touchRight;
        private bool touchArmHeld, wasArmHeld;
        private static readonly string[] ImmediateCommands = { "Takeoff", "Landing", "ProfileCine", "ProfileNormal", "ProfileSport", "LandingGear", "Help", "DebugDraw", "DebugPanel", "CopyTelemetry" };
        private readonly HashSet<string> pendingCommands = new();
        private readonly HashSet<string> frameCommands = new();
        private DroneResetHoldTracker resetHoldTracker;
        internal event Action ReloadRequested;
        internal event Action ExitRequested;
        internal event Action ActivateRequested;
        internal event Action<bool> PanelChanged;
        internal event Action<bool> HelpChanged;
        internal event Action<bool> DebugChanged;
        internal event Action<string> PresentationRequested;
        internal InputActionSession Session => session;
        internal float ResetProgress => resetHoldTracker?.Progress ?? 0;
        internal float ResetHoldSeconds => config != null ? config.ResetHoldSeconds : 5;
        /// 操作面板打开时手动飞行输入被清空。
        public bool IsPanelOpen { get; private set; }
        /// 阅读帮助时独立阻止飞行，保留打开前的操作菜单状态。
        public bool IsHelpOpen { get; private set; }
        /// 调试页导航期间不向飞控提交手动输入。
        public bool IsDebugOpen { get; private set; }
        /// HUD 只在当前无人机受控且面板关闭时提交双摇杆。
        public bool AcceptsFlightInput => isActiveAndEnabled && !IsPanelOpen && !IsHelpOpen && !IsDebugOpen;
        internal Vector2 CameraLook { get; private set; }
        internal float LineInput { get; private set; }
        internal Vector2 AimPosition { get; private set; } = new(.5f, .5f);
        internal bool HasMouseAim => InputDeviceState.ActiveKind == InputDeviceKind.KeyboardMouse;
        internal float ZoomInput { get; private set; }

        private void Awake()
        {
            controller = GetComponent<DroneFlightController>();
            cameraRig = GetComponent<DroneCameraRig>();
            equipment = GetComponent<DroneEquipmentHost>();
            landingGear = GetComponent<DroneLandingGearController>();
            resetHoldTracker = new DroneResetHoldTracker(ResetHoldSeconds);
            if (actions != null) session = new InputActionSession(actions);
        }
        private void OnEnable() => session?.SetMap("Flight");
        private void OnDisable()
        {
            ResetBufferedInput(); SetHelpOpen(false); SetDebugOpen(false); SetPanelOpen(false);
            session?.SetMap("Waiting");
        }
        private void OnDestroy() => session?.Dispose();
        private void OnApplicationFocus(bool focus) { if (!focus) ResetBufferedInput(); }
        private void OnApplicationPause(bool paused) { if (paused) ResetBufferedInput(); }

        private void Update()
        {
            if (session == null || controller == null) return;
            if (IsHelpOpen)
            {
                if (session.Asset.FindAction("Flight/Help").WasPressedThisFrame()) PresentationRequested?.Invoke("Help");
                controller.SetControlInput(default);
                return;
            }
            if (IsDebugOpen)
            {
                foreach (string command in DebugCommands)
                    if (session.Asset.FindAction("Flight/" + command).WasPressedThisFrame()) PresentationRequested?.Invoke(command);
                controller.SetControlInput(default);
                return;
            }
            frameCommands.Clear(); foreach (var command in pendingCommands) frameCommands.Add(command); pendingCommands.Clear();
            if (Pressed("Panel")) SetPanelOpen(!IsPanelOpen);
            if (IsPanelOpen) { controller.SetControlInput(default); return; }
            bool armHeld = touchArmHeld || session.Held("ArmOrReset");
            if (armHeld && !wasArmHeld) resetHoldTracker.Begin();
            if (armHeld && resetHoldTracker.Step(Time.unscaledDeltaTime)) { ResetBufferedInput(); ReloadRequested?.Invoke(); }
            if (!armHeld && wasArmHeld && resetHoldTracker.Release() == DroneResetReleaseResult.ShortPress)
                controller.SetArmed(!controller.IsArmed);
            wasArmHeld = armHeld;

            Vector2 planar = session.ReadVector("Move");
            Vector2 vertical = session.ReadVector("VerticalYaw");
            bool padOrTouch = InputDeviceState.ActiveKind != InputDeviceKind.KeyboardMouse;
            var target = new Vector4(vertical.y, vertical.x, planar.y, planar.x);
            if (InputDeviceState.ActiveKind == InputDeviceKind.Touch) target = new Vector4(touchLeft.y, touchLeft.x, touchRight.y, touchRight.x);
            bool lookMode = session.Held("ViewModifier") || touchLookMode;
            CameraLook = session.ReadVector("CameraLook");
            if (lookMode) { CameraLook += InputDeviceState.ActiveKind == InputDeviceKind.Touch ? touchRight : session.ReadVector("PadLook"); target.z = target.w = 0; }
            if (!padOrTouch)
            {
                float rise = controller.InputRiseRate > 0 ? controller.InputRiseRate : config != null ? config.KeyboardFallbackRiseRate : 3;
                smoothedKeyboardInput = new Vector4(Step(smoothedKeyboardInput.x, target.x, rise), Step(smoothedKeyboardInput.y, target.y, rise),
                    Step(smoothedKeyboardInput.z, target.z, rise), Step(smoothedKeyboardInput.w, target.w, rise));
                target = smoothedKeyboardInput;
            }
            else { smoothedKeyboardInput = Vector4.zero; }
            controller.SetControlInput(DroneControlInput.Create(target.x, target.y, target.z, target.w));
            LineInput = session.Read<float>("Line") + touchLine;
            ZoomInput = session.Read<float>("Zoom");
            var point = session.Read<Vector2>("Point");
            if (HasMouseAim && Screen.width > 0 && Screen.height > 0)
                AimPosition = new Vector2(Mathf.Clamp01(point.x / Screen.width), Mathf.Clamp01(point.y / Screen.height));
            else if (lookMode) AimPosition = new Vector2(Mathf.Clamp01(AimPosition.x + CameraLook.x * Time.unscaledDeltaTime * .5f),
                Mathf.Clamp01(AimPosition.y + CameraLook.y * Time.unscaledDeltaTime * .5f));
            foreach (string command in ImmediateCommands)
                if (Pressed(command)) ExecuteImmediate(command);
        }
        private bool touchLookMode;
        private float touchLine;

        internal bool Pressed(string name) => !IsHelpOpen && !IsDebugOpen && (frameCommands.Contains(name) || session?.Pressed(name) == true);
        internal string Label(string name, string caption) => InputBindingDisplay.Label(session?.Asset.FindAction("Flight/" + name), caption);

        /// <summary>触控按钮与操作面板提交同一玩法命令，下一帧消费。</summary>
        /// <param name="command">保存的动作名称。</param>
        public void Execute(string command)
        {
            if (session?.Asset.FindAction(command) == null) throw new ArgumentException("未知无人机动作：" + command);
            if (IsHelpOpen) { if (command == "Help") PresentationRequested?.Invoke(command); return; }
            if (IsDebugOpen) { if (command is "DebugPanel" or "DebugDraw" or "CopyTelemetry") PresentationRequested?.Invoke(command); return; }
            if (command == "Panel") { SetPanelOpen(!IsPanelOpen); return; }
            if (IsPanelOpen || !isActiveAndEnabled) ExecuteImmediate(command); else pendingCommands.Add(command);
        }
        private void ExecuteImmediate(string command)
        {
            switch (command)
            {
                case "Activate": ActivateRequested?.Invoke(); break;
                case "Back": if (!isActiveAndEnabled) ExitRequested?.Invoke(); break;
                case "ArmOrReset": controller.SetArmed(!controller.IsArmed); break;
                case "Exit": ExitRequested?.Invoke(); break;
                case "Takeoff": controller.BeginAutomaticTakeoff(); break;
                case "Landing": controller.BeginAutomaticLanding(); break;
                case "ProfileCine": controller.SetResponseProfile(DroneResponseProfile.Cine); break;
                case "ProfileNormal": controller.SetResponseProfile(DroneResponseProfile.Normal); break;
                case "ProfileSport": controller.SetResponseProfile(DroneResponseProfile.Sport); break;
                case "LandingGear": landingGear?.Toggle(); break;
                case "ZoomIn": cameraRig?.AdjustFieldOfView(-5); break;
                case "ZoomOut": cameraRig?.AdjustFieldOfView(5); break;
                case "SwitchCamera": SwitchCamera(); break;
                case "Equipment": equipment?.PrimaryAction(); break;
                case "Aim": equipment?.ToggleAimMode(); break;
                case "Help": case "DebugDraw": case "DebugPanel": case "CopyTelemetry": PresentationRequested?.Invoke(command); break;
            }
        }
        internal void SwitchCamera()
        {
            if (cameraRig != null && cameraRig.Mode != DroneCameraMode.HarpoonAim)
                cameraRig.SetMode((DroneCameraMode)(((int)cameraRig.Mode + 1) % (int)DroneCameraMode.HarpoonAim));
        }
        /// <summary>打开操作面板时清空手动输入，保持飞控稳定逻辑。</summary>
        /// <param name="open">面板是否打开。</param>
        public void SetPanelOpen(bool open)
        {
            if (IsHelpOpen || IsDebugOpen) return;
            if (IsPanelOpen == open) return;
            IsPanelOpen = open; ResetBufferedInput();
            session?.SetMap(open ? "Menu" : isActiveAndEnabled ? "Flight" : "Waiting");
            PanelChanged?.Invoke(open);
        }
        /// <summary>帮助独占阅读输入；关闭后恢复来源菜单或飞行并等待旧按键释放。</summary>
        /// <param name="open">是否显示操作指南。</param>
        internal void SetHelpOpen(bool open)
        {
            if (IsHelpOpen == open) return;
            IsHelpOpen = open;
            ResetBufferedInput();
            RefreshOverlayMap();
            HelpChanged?.Invoke(open);
        }
        /// 调试导航与飞行使用相同输入会话，关闭后等待旧按键释放。
        internal void SetDebugOpen(bool open)
        {
            if (IsDebugOpen == open) return;
            IsDebugOpen = open;
            ResetBufferedInput(); RefreshOverlayMap();
            DebugChanged?.Invoke(open);
        }
        private void RefreshOverlayMap()
        {
            session?.SetMap(IsHelpOpen || IsDebugOpen || IsPanelOpen ? "Menu" : isActiveAndEnabled ? "Flight" : "Waiting");
            // 仅复用当前浮层自己的真实快捷键，其它业务动作继续关闭。
            if (IsHelpOpen) session?.Asset.FindAction("Flight/Help")?.Enable();
            if (IsDebugOpen)
                foreach (string command in DebugCommands) session?.Asset.FindAction("Flight/" + command)?.Enable();
        }
        private static readonly string[] DebugCommands = { "DebugPanel", "DebugDraw", "CopyTelemetry" };
        /// <summary>记录双摇杆，不模拟 Gamepad 设备。</summary>
        /// <param name="left">升降/偏航。</param>
        /// <param name="right">前后/左右，镜头模式下控制镜头。</param>
        public void SetTouchFrame(Vector2 left, Vector2 right) { touchLeft = left; touchRight = right; }
        /// <summary>触屏确认键的真实保持状态。</summary>
        /// <param name="held">是否按住。</param>
        public void SetTouchArmHeld(bool held) => touchArmHeld = held;
        /// <summary>切换手机右摇杆的镜头职责。</summary>
        /// <param name="value">是否控制镜头。</param>
        public void SetTouchLookMode(bool value) => touchLookMode = value;
        /// <summary>触控收放线的保持量。</summary>
        /// <param name="value">负数收线，正数放线，零停止。</param>
        public void SetTouchLine(float value) => touchLine = Mathf.Clamp(value, -1, 1);
        internal void ResetBufferedInput()
        {
            smoothedKeyboardInput = Vector4.zero; touchLeft = touchRight = CameraLook = Vector2.zero;
            LineInput = ZoomInput = touchLine = 0; touchArmHeld = wasArmHeld = false;
            resetHoldTracker?.Release(); frameCommands.Clear(); pendingCommands.Clear();
            controller?.SetControlInput(default);
        }
        private float Step(float current, float target, float rise)
            => Mathf.MoveTowards(current, target, Mathf.Max(0, Mathf.Approximately(target, 0) ? config != null ? config.KeyboardFallRate : 5 : rise) * Time.unscaledDeltaTime);
    }
}
