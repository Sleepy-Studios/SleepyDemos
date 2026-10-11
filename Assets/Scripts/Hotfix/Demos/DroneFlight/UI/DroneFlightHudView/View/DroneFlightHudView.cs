using UnityEngine;
using UnityEngine.EventSystems;
using Cysharp.Threading.Tasks;
namespace Hotfix
{
    using Core.Runtime;
    using Core.Runtime.Inputs;
    using Core.Runtime.Settings;
    using DroneFlight;

    [Module("DroneFlight")]
    [UIBind("DroneFlightHudView")]
    public partial class DroneFlightHudView : View<DroneFlightViewData>
    {
        private UIProgressBar resetProgressBar;
        private DroneFlightData state;

        protected override void OnGameObjectInitialize()
        {
            base.OnGameObjectInitialize();
            InitializeControlsReferences();
            InitializeControls();
            Core.Runtime.Localization.LanguageService.SetText(TextMeshProUGUI_GraphicsSettingsLabel, "Settings.Graphics");
            resetProgressBar = UIProgressBar_ResetProgressFill;
            GameSettingsService.Changed += OnSettingsChanged;
            AddBinding(() => GameSettingsService.Changed -= OnSettingsChanged);
            InputDeviceState.Changed += RefreshTelemetryHints;
            AddBinding(() => InputDeviceState.Changed -= RefreshTelemetryHints);
            OnSettingsChanged(GameSettingsService.Current);
            BindData<DroneFlightData>(OnData);
            BindUpdate(UpdateControls);
        }

        /// <summary>交付会话数据；已显示的 HUD 立即切换遥测订阅。</summary>
        /// <param name="data">当前机体与输入会话。</param>
        /// <returns>当前 HUD。</returns>
        public override View<DroneFlightViewData> SetData(DroneFlightViewData data)
        {
            base.SetData(data);
            BindControls(data?.Input);
            return this;
        }

        private void OnData(DroneFlightData value)
        {
            state = value;
            RefreshControls();
            if (value.HasSnapshot) OnSnapshotChanged(value.Snapshot);
        }


        protected override void OnDestroy() { ReleaseInput(); input = null; base.OnDestroy(); }

        private void OnSnapshotChanged(DroneFlightUiSnapshot snapshot)
        {
            CanvasGroup_TelemetryRoot.alpha = snapshot.TelemetryVisible ? 1 : 0;
            CanvasGroup_TelemetryRoot.interactable = false;
            CanvasGroup_TelemetryRoot.blocksRaycasts = false;
            var hud = snapshot.Hud;
            TextMeshProUGUI_StatusText.text = $"<b>{DroneHudFormatter.FormatProfile(hud.Profile)}</b>\n<size=70%>{DroneHudPresentation.Status(hud)}</size>";
            TextMeshProUGUI_CameraText.text = DroneHudPresentation.Camera(hud);
            TextMeshProUGUI_HeightText.text = $"{hud.Height:F1} <size=65%>m</size>";
            TextMeshProUGUI_DistanceText.text = $"{hud.Distance:F1} <size=65%>m</size>";
            TextMeshProUGUI_HorizontalText.text = $"{hud.HorizontalSpeed:F1} <size=65%>m/s</size>";
            TextMeshProUGUI_VerticalText.text = $"{hud.VerticalSpeed:+0.0;-0.0;0.0} <size=65%>m/s</size>";
            TextMeshProUGUI_GearText.text = $"起落架  <color=#F4A23A>{DroneHudPresentation.Gear(hud.LandingGearState)}</color>";
            bool equipped = snapshot.Equipment.Kind != DroneEquipmentKind.None;
            RectTransform_EquipmentPanel.gameObject.SetActive(equipped);
            TextMeshProUGUI_EquipmentText.text = snapshot.EquipmentText;
            string equipmentControlsKey = DroneHudFormatter.EquipmentTouchControlsKey(snapshot.Equipment.Kind);
            if (equipmentControlsKey != null)
            {
                Core.Runtime.Localization.LanguageService.SetText(TextMeshProUGUI_EquipmentActions, equipmentControlsKey);
            }
            else
            {
                TextMeshProUGUI_EquipmentActions.text = string.Empty;
            }
            TextMeshProUGUI_WarningText.text = snapshot.WarningText;
            TextMeshProUGUI_WarningText.color = hud.OperationState == DroneFlightOperationState.Fault
                ? new UnityEngine.Color32(240, 77, 65, 255) : new UnityEngine.Color32(244, 162, 58, 255);
            RectTransform_WarningPanel.gameObject.SetActive(!string.IsNullOrEmpty(snapshot.WarningText));
            RectTransform_ResetPanel.gameObject.SetActive(snapshot.ResetProgress > 0);
            resetProgressBar.SetValue(snapshot.ResetProgress);
            TextMeshProUGUI_ResetProgressText.text = $"重新运行场景  {snapshot.ResetProgress * snapshot.ResetHoldSeconds:F1} / {snapshot.ResetHoldSeconds:F1} s  · 松开取消";
            RefreshTelemetryHints(snapshot);
            SetEquipment(snapshot.Equipment.Kind);
        }

        private void RefreshTelemetryHints()
        {
            if (state?.HasSnapshot == true)
            {
                RefreshTelemetryHints(state.Snapshot);
            }
        }

        private void RefreshTelemetryHints(DroneFlightUiSnapshot snapshot)
        {
            bool touch = InputDeviceState.PromptKind == InputDeviceKind.Touch;
            bool pad = InputDeviceState.PromptKind == InputDeviceKind.Gamepad;
            bool physical = showInputPrompts && !touch;
            bool aiming = snapshot.Hud.CameraMode == DroneCameraMode.HarpoonAim;
            var kind = snapshot.Equipment.Kind;
            TextMeshProUGUI_EquipmentActions.gameObject.SetActive(touch && showInputPrompts);
            BindTelemetryPrompt("Camera", aiming ? "Aim" : "SwitchCamera", aiming ? "Input.Drone.ExitAim" : "Input.Drone.SwitchCamera", physical);
            BindTelemetryPrompt("Gear", "LandingGear", pad ? "Input.Drone.Panel" : "Input.Drone.GearToggle", physical);
            BindTelemetryPrompt("Aim", "Aim", "Input.Drone.Aim", physical && kind == DroneEquipmentKind.Harpoon);
            BindTelemetryPrompt("Equipment", "Equipment", kind == DroneEquipmentKind.Grapple ? "Input.Drone.GrappleToggle" : "Input.Drone.HarpoonAction",
                physical && kind != DroneEquipmentKind.None);
            BindTelemetryPrompt("Line", "Line", kind == DroneEquipmentKind.Grapple ? "Input.Drone.GrappleLine" : "Input.Drone.Line",
                physical && kind != DroneEquipmentKind.None);
            BindTelemetryPrompt("Reset", "ArmOrReset", "Input.Drone.HoldReset", physical && snapshot.ResetProgress > 0);
        }

        private void BindTelemetryPrompt(string key, string action, string captionKey, bool visible)
        {
            var prompt = InputPromptGroup_DroneFlightHudView.Get("Telemetry/" + key);
            var value = input?.Session?.Asset.FindAction("Flight/" + action);
            if (prompt.Action != value)
            {
                prompt.Bind(value);
            }
            prompt.SetCaptionKey(captionKey);
            prompt.SetBindingsVisible(showInputPrompts);
            prompt.gameObject.SetActive(visible);
        }

        protected override void OnHide() { ReleaseInput(); base.OnHide(); }
        private GameObject touchControls;
        private GameObject operationPanel;
        private UIMenuScope menuScope;
        private TouchInputPad leftPad;
        private TouchInputPad rightPad;
        private RectTransform leftThumb;
        private RectTransform rightThumb;
        private bool lastActive;
        private InputCommandButton[] buttons;
        private InputBindingPrompt[] prompts;
        private DronePlayerInput input;
        private DroneEquipmentKind equipmentKind;
        private GameObject menuSelection;
        private bool showInputPrompts = true;

        private void OnSettingsChanged(GameSettingsSnapshot value)
        {
            showInputPrompts = value.Accessibility.ShowInputPrompts;
            foreach (var prompt in prompts)
            {
                prompt.SetBindingsVisible(showInputPrompts);
                if (prompt.GetComponentInParent<InputCommandButton>() == null)
                {
                    prompt.gameObject.SetActive(showInputPrompts);
                }
            }
            if (state?.HasSnapshot == true)
            {
                OnSnapshotChanged(state.Snapshot);
            }
        }

        /// <summary>绑定当前实例并释放旧会话的输入订阅。</summary>
        /// <param name="value">当前无人机输入。</param>
        private void BindControls(DronePlayerInput value)
        {
            ReleaseInput(); input = value;
            foreach (var prompt in prompts)
            {
                prompt.BindAsset(input?.Session?.Asset);
                prompt.SetBindingsVisible(showInputPrompts);
            }
            RefreshControls();
            RefreshTelemetryHints();
        }

        /// 释放本次绑定；不能将被取消的长按当作解锁短按。


        /// <summary>只显示已安装装备支持的按钮。</summary>
        /// <param name="kind">当前装备类型。</param>
        public void SetEquipment(DroneEquipmentKind kind)
        {
            equipmentKind = kind;
            if (buttons == null) return;
            foreach (var button in buttons)
            {
                bool show = button.Command switch
                {
                    "Equipment" or "ReelIn" or "ReelOut" => kind != DroneEquipmentKind.None,
                    "Aim" => kind == DroneEquipmentKind.Harpoon,
                    "Activate" => input != null && !input.isActiveAndEnabled,
                    "ArmOrReset" or "ViewModifier" => input != null && input.isActiveAndEnabled,
                    _ => true
                };
                button.gameObject.SetActive(show);
            }
        }

        private void UpdateControls()
        {
            bool active = input != null && input.isActiveAndEnabled;
            if (active != lastActive) { lastActive = active; ReleaseInput(); SetEquipment(equipmentKind); }
            if (leftThumb != null) leftThumb.anchoredPosition = leftPad.Move * 48f;
            if (rightThumb != null) rightThumb.anchoredPosition = rightPad.Move * 48f;
            if (input != null && input.AcceptsFlightInput)
                input.SetTouchFrame(leftPad.Move, rightPad.Move);
        }
        private void ReleaseInput()
        {
            leftPad?.ResetInput(); rightPad?.ResetInput(); input?.ResetBufferedInput();
        }
        private void RefreshControls()
        {
            ReleaseInput();
            bool overlay = state?.ControlsSuppressed == true;
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (overlay && operationPanel.activeInHierarchy && events?.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(operationPanel.transform)) menuSelection = events.currentSelectedGameObject;
            operationPanel.SetActive(input != null && state?.PanelVisible == true && !overlay);
            touchControls.SetActive(state?.DeviceKind == InputDeviceKind.Touch && !overlay && input != null && state?.PanelVisible != true);
            SetEquipment(equipmentKind);
            if (!overlay && operationPanel.activeInHierarchy && menuSelection != null && menuSelection.activeInHierarchy) { events?.SetSelectedGameObject(menuSelection); menuSelection = null; }
        }


        private void ClosePanel() => GlobalData.Dispatch(new DroneFlightControlAction("ClosePanel"));
        private void OnCommand(string name) => GlobalData.Dispatch(new DroneFlightControlAction(name));
        private void OnGraphicsSettingsClick() => SettingsFlow.OpenAsync(Hotfix.SceneManagement.GameSceneId.DroneFlight).Forget();
        private void OnHold(string name, bool held) => GlobalData.Dispatch(new DroneFlightHoldAction(name, held));
        private void InitializeControls()
        {
            foreach (var button in buttons) { this.RegisterInputCommandButton(button, OnCommand); this.RegisterInputCommandHold(button, OnHold); }
            if (menuScope != null) { menuScope.Canceled += ClosePanel; AddBinding(() => { if (menuScope != null) menuScope.Canceled -= ClosePanel; }); }
        }
        private void InitializeControlsReferences()
        {
            touchControls = RectTransform_TouchControls.gameObject;
            operationPanel = RectTransform_OperationPanel.gameObject;
            menuScope = UIMenuScope_OperationPanel;
            leftPad = TouchInputPad_LeftStick;
            rightPad = TouchInputPad_RightStick;
            leftThumb = RectTransform_Thumb;
            rightThumb = RectTransform_Thumb1;
            buttons = new Core.Runtime.Inputs.InputCommandButton[] { InputCommandButton_ArmOrReset, InputCommandButton_Equipment, InputCommandButton_Aim, InputCommandButton_ViewModifier, InputCommandButton_ReelIn, InputCommandButton_ReelOut, InputCommandButton_Activate, InputCommandButton_Back, InputCommandButton_Panel, InputCommandButton_Takeoff, InputCommandButton_Landing, InputCommandButton_ProfileCine, InputCommandButton_ProfileNormal, InputCommandButton_ProfileSport, InputCommandButton_LandingGear, InputCommandButton_SwitchCamera, InputCommandButton_ZoomIn, InputCommandButton_ZoomOut, InputCommandButton_Help, InputCommandButton_DebugDraw, InputCommandButton_DebugPanel, InputCommandButton_CopyTelemetry, InputCommandButton_Exit, InputCommandButton_Panel1, InputCommandButton_OpenOperations, InputCommandButton_OpenHelp };
            var group = InputPromptGroup_DroneFlightHudView;
            if (group == null)
            {
                throw new System.InvalidOperationException("正式无人机 HUD 提示组尚未制作。");
            }
            InputBindingPrompt Prompt(string key, string captionKey)
            {
                var prompt = group.Get(key);
                prompt.SetCaptionKey(captionKey);
                return prompt;
            }
            prompts = new[]
            {
                Prompt("Touch/ArmOrReset", "Input.Drone.TouchArmOrReset"),
                Prompt("Touch/Equipment", "Input.Drone.Equipment"),
                Prompt("Touch/Aim", "Input.Drone.TouchAim"),
                Prompt("Touch/ViewModifier", "Input.Drone.ViewModifier"),
                Prompt("Touch/ReelIn", "Input.Drone.ReelIn"),
                Prompt("Touch/ReelOut", "Input.Drone.PayOut"),
                Prompt("Touch/Activate", "Input.Drone.EnterRemote"),
                Prompt("Touch/Back", "Input.Back"),
                Prompt("Touch/Panel", "Input.Drone.Panel"),
                Prompt("Operations/Takeoff", "Input.Drone.Takeoff"),
                Prompt("Operations/Landing", "Input.Drone.Landing"),
                Prompt("Operations/ProfileCine", "Input.Drone.ProfileCineMode"),
                Prompt("Operations/ProfileNormal", "Input.Drone.ProfileNormalMode"),
                Prompt("Operations/ProfileSport", "Input.Drone.ProfileSportMode"),
                Prompt("Operations/LandingGear", "Input.Drone.LandingGear"),
                Prompt("Operations/SwitchCamera", "Input.Drone.SwitchCamera"),
                Prompt("Operations/ZoomIn", "Input.Drone.ExpandView"),
                Prompt("Operations/ZoomOut", "Input.Drone.NarrowView"),
                Prompt("Operations/Help", "Input.Help"),
                Prompt("Operations/DebugDraw", "Input.Drone.ThrustVectors"),
                Prompt("Operations/DebugPanel", "Input.Drone.DebugPanel"),
                Prompt("Operations/CopyTelemetry", "Input.Drone.CopyTelemetry"),
                Prompt("Operations/Exit", "Input.ReturnHub"),
                Prompt("Operations/Panel", "Input.Drone.ClosePanel"),
                Prompt("Menu/Submit", "Input.Confirm"),
                Prompt("Menu/Cancel", "Input.Back"),
                Prompt("Hud/Panel", "Input.Drone.Panel"),
                Prompt("Hud/Help", "Input.Help")
            };
            foreach (string key in new[] { "Camera", "Gear", "Aim", "Equipment", "Line", "Reset" })
            {
                group.Get("Telemetry/" + key);
            }
        }
    }
}
