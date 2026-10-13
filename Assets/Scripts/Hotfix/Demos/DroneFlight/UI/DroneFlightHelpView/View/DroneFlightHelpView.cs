using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using Hotfix.DroneFlight;
using UnityEngine.InputSystem;

namespace Hotfix
{
    /// 按当前输入设备与装备显示操作说明，不提交飞行或装备命令。
    [Module("DroneFlight")]
    [UIBind("DroneFlightHelpView")]
    public partial class DroneFlightHelpView : View<DroneFlightViewData>
    {
        /// <summary>交付会话数据；已显示的指南立即更新装备和设备说明。</summary>
        /// <param name="data">当前机体与输入会话。</param>
        /// <returns>当前指南。</returns>
        public override View<DroneFlightViewData> SetData(DroneFlightViewData data)
        {
            base.SetData(data);
            if (State == ViewState.Visible)
            {
                Refresh();
            }
            return this;
        }

        protected override void OnGameObjectInitialize()
        {
            BindData<DroneFlightData>(_ => Refresh());
            Button_Close.onClick.AddListener(Close);
            Button_Return.onClick.AddListener(Close);
            UIMenuScope_DroneFlightHelpView.Canceled += Close;
            GameSettingsService.Changed += OnSettingsChanged;
            AddBinding(() => GameSettingsService.Changed -= OnSettingsChanged);
        }

        protected override void OnShow()
        {
            base.OnShow();
            InputDeviceState.Changed += Refresh;
            InputSystem.onActionChange += OnActionChange;
            Refresh();
            ScrollRect_Guide.verticalNormalizedPosition = 1;
        }

        protected override void OnHide()
        {
            ReleasePresentation();
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            ReleasePresentation();
            base.OnDestroy();
        }

        private void ReleasePresentation()
        {
            InputDeviceState.Changed -= Refresh;
            InputSystem.onActionChange -= OnActionChange;
        }

        private void OnSettingsChanged(GameSettingsSnapshot value) => Refresh();
        private void OnActionChange(object target, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
            {
                Refresh();
            }
        }

        private void Close() => GlobalData.Dispatch(new DroneFlightControlAction("Help"));

        private void BindPrompt(string key, string action, string captionKey, bool showBindings, params object[] args)
        {
            var prompt = InputPromptGroup_DroneFlightHelpView.Get(key);
            prompt.Bind(params1?.Input?.Session?.Asset.FindAction("Flight/" + action));
            prompt.SetCaptionKey(captionKey, args);
            prompt.SetBindingsVisible(showBindings);
        }

        private void Refresh()
        {
            bool touch = InputDeviceState.PromptKind == InputDeviceKind.Touch;
            bool pad = InputDeviceState.PromptKind == InputDeviceKind.Gamepad;
            bool physical = !touch;
            bool showBindings = GameSettingsService.Current.Accessibility.ShowInputPrompts;
            var group = InputPromptGroup_DroneFlightHelpView;
            group.SetVisible(physical);
            TextMeshProUGUI_Device.text = touch ? "触屏" : pad ? "手柄 · " + InputDeviceState.PromptStyle : "键鼠";
            TextMeshProUGUI_Flight.gameObject.SetActive(touch);
            LanguageService.SetText(TextMeshProUGUI_Flight, touch ? "Input.Drone.TouchFlight" : "Input.Drone.FlightAxes");
            TextMeshProUGUI_Camera.gameObject.SetActive(touch);
            LanguageService.SetText(TextMeshProUGUI_Camera, "Input.Drone.TouchCamera");
            TextMeshProUGUI_Takeoff.gameObject.SetActive(touch);
            LanguageService.SetText(TextMeshProUGUI_Takeoff, "Input.Drone.TouchFlightCommands");
            BindPrompt("Move", "Move", "Input.Drone.HorizontalMove", showBindings);
            BindPrompt("VerticalYaw", "VerticalYaw", "Input.Drone.VerticalYaw", showBindings);
            BindPrompt("SwitchCamera", "SwitchCamera", "Input.Drone.SwitchCamera", showBindings);
            BindPrompt("CameraLook", pad ? "PadLook" : "CameraLook", pad ? "Input.Drone.CameraLookPad" : "Input.Drone.CameraLook", showBindings);
            BindPrompt("ViewModifier", "ViewModifier", "Input.Drone.HoldViewModifier", showBindings);
            group.Get("ViewModifier").gameObject.SetActive(physical && pad);
            BindPrompt("Zoom", "Zoom", pad ? "Input.Drone.ZoomPanel" : "Input.Drone.Zoom", showBindings);
            BindPrompt("LandingGear", "LandingGear", pad ? "Input.Drone.GearPanel" : "Input.Drone.LandingGear", showBindings);
            BindPrompt("ArmOrReset", "ArmOrReset", "Input.Drone.ArmOrReset", showBindings);
            BindPrompt("Takeoff", "Takeoff", pad ? "Input.Drone.TakeoffPanel" : "Input.Drone.Takeoff", showBindings);
            BindPrompt("Landing", "Landing", pad ? "Input.Drone.LandingPanel" : "Input.Drone.Landing", showBindings);
            BindPrompt("ProfileCine", "ProfileCine", pad ? "Input.Drone.ProfileCinePanel" : "Input.Drone.ProfileCine", showBindings);
            BindPrompt("ProfileNormal", "ProfileNormal", pad ? "Input.Drone.ProfileNormalPanel" : "Input.Drone.ProfileNormal", showBindings);
            BindPrompt("ProfileSport", "ProfileSport", pad ? "Input.Drone.ProfileSportPanel" : "Input.Drone.ProfileSport", showBindings);
            var kind = params1?.TelemetrySource?.Current.Equipment.Kind ?? DroneEquipmentKind.None;
            TextMeshProUGUI_Equipment.gameObject.SetActive(touch || kind == DroneEquipmentKind.None);
            if (kind is DroneEquipmentKind.Grapple or DroneEquipmentKind.Harpoon)
            {
                LanguageService.SetText(TextMeshProUGUI_Equipment,
                    kind == DroneEquipmentKind.Grapple ? "Input.Drone.TouchGrapple" : "Input.Drone.TouchHarpoon");
            }
            else
            {
                LanguageService.SetText(TextMeshProUGUI_Equipment, "Input.Drone.NoEquipmentHelp");
            }
            BindPrompt("Aim", "Aim", pad ? "Input.Drone.AimPad" : "Input.Drone.AimMouse", showBindings);
            BindPrompt("Equipment", "Equipment", kind == DroneEquipmentKind.Grapple ? "Input.Drone.GrappleToggle" : "Input.Drone.HarpoonAction", showBindings);
            BindPrompt("Line", "Line", "Input.Drone.Line", showBindings);
            group.Get("Aim").gameObject.SetActive(physical && kind == DroneEquipmentKind.Harpoon);
            group.Get("Equipment").gameObject.SetActive(physical && kind != DroneEquipmentKind.None);
            group.Get("Line").gameObject.SetActive(physical && kind != DroneEquipmentKind.None);
            float seconds = params1?.Input?.ResetHoldSeconds ?? 5;
            TextMeshProUGUI_Reset.gameObject.SetActive(touch);
            LanguageService.SetText(TextMeshProUGUI_Reset, "Input.Drone.TouchResetHoldDuration", args: new object[] { seconds });
            BindPrompt("ResetHold", "ArmOrReset", "Input.Drone.ResetHoldDuration", showBindings, seconds);
            LanguageService.SetText(TextMeshProUGUI_Label,
                params1?.Input?.IsPanelOpen == true ? "Input.Drone.ReturnPanel" : "Input.Drone.ReturnFlight");
        }
    }
}
