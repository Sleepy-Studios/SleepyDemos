using UnityEngine;
using UnityEngine.EventSystems;
namespace Hotfix
{
    using Core.Runtime;
    using Core.Runtime.Inputs;
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
            BindData<DroneFlightData>(OnData);
            BindUpdate(UpdateControls);
            resetProgressBar = UIProgressBar_ResetProgressFill;
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

        private string Key(string action) => params1?.Input?.Label(action, "") ?? "";

        private void OnSnapshotChanged(DroneFlightUiSnapshot snapshot)
        {
            CanvasGroup_TelemetryRoot.alpha = snapshot.TelemetryVisible ? 1 : 0;
            CanvasGroup_TelemetryRoot.interactable = false;
            CanvasGroup_TelemetryRoot.blocksRaycasts = false;
            var hud = snapshot.Hud;
            TextMeshProUGUI_StatusText.text = $"<b>{DroneHudFormatter.FormatProfile(hud.Profile)}</b>\n<size=70%>{DroneHudPresentation.Status(hud)}</size>";
            TextMeshProUGUI_CameraText.text = DroneHudPresentation.Camera(hud, Key("SwitchCamera"), Key("Aim"));
            TextMeshProUGUI_HeightText.text = $"{hud.Height:F1} <size=65%>m</size>";
            TextMeshProUGUI_DistanceText.text = $"{hud.Distance:F1} <size=65%>m</size>";
            TextMeshProUGUI_HorizontalText.text = $"{hud.HorizontalSpeed:F1} <size=65%>m/s</size>";
            TextMeshProUGUI_VerticalText.text = $"{hud.VerticalSpeed:+0.0;-0.0;0.0} <size=65%>m/s</size>";
            TextMeshProUGUI_GearText.text = $"起落架  <color=#F4A23A>{DroneHudPresentation.Gear(hud.LandingGearState)}</color>  <size=75%>{Key("LandingGear")}</size>";
            bool equipped = snapshot.Equipment.Kind != DroneEquipmentKind.None;
            RectTransform_EquipmentPanel.gameObject.SetActive(equipped);
            TextMeshProUGUI_EquipmentText.text = snapshot.EquipmentText;
            TextMeshProUGUI_EquipmentActions.text = DroneHudFormatter.FormatEquipmentControls(snapshot.Equipment.Kind, Key);
            TextMeshProUGUI_WarningText.text = snapshot.WarningText;
            TextMeshProUGUI_WarningText.color = hud.OperationState == DroneFlightOperationState.Fault
                ? new UnityEngine.Color32(240, 77, 65, 255) : new UnityEngine.Color32(244, 162, 58, 255);
            RectTransform_WarningPanel.gameObject.SetActive(!string.IsNullOrEmpty(snapshot.WarningText));
            RectTransform_ResetPanel.gameObject.SetActive(snapshot.ResetProgress > 0);
            resetProgressBar.SetValue(snapshot.ResetProgress);
            TextMeshProUGUI_ResetProgressText.text = $"{Key("ArmOrReset")}  重新运行场景  {snapshot.ResetProgress * snapshot.ResetHoldSeconds:F1} / {snapshot.ResetHoldSeconds:F1} s  · 松开取消";
            SetEquipment(snapshot.Equipment.Kind);
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

        /// <summary>绑定当前实例并释放旧会话的输入订阅。</summary>
        /// <param name="value">当前无人机输入。</param>
        private void BindControls(DronePlayerInput value)
        {
            ReleaseInput(); input = value;
            foreach (var prompt in prompts) prompt.BindAsset(input?.Session?.Asset);
            RefreshControls();
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
            prompts = new Core.Runtime.Inputs.InputBindingPrompt[] { InputBindingPrompt_Prompt_解锁___长按复位, InputBindingPrompt_Prompt_装备操作, InputBindingPrompt_Prompt_瞄准, InputBindingPrompt_Prompt_镜头模式, InputBindingPrompt_Prompt_收线, InputBindingPrompt_Prompt_放线, InputBindingPrompt_Prompt_进入遥控, InputBindingPrompt_Prompt_返回, InputBindingPrompt_Prompt_操作面板, InputBindingPrompt_Prompt_自动起飞, InputBindingPrompt_Prompt_自动降落, InputBindingPrompt_Prompt_平稳模式, InputBindingPrompt_Prompt_普通模式, InputBindingPrompt_Prompt_运动模式, InputBindingPrompt_Prompt_起落架收放, InputBindingPrompt_Prompt_切换镜头, InputBindingPrompt_Prompt_放大视野, InputBindingPrompt_Prompt_缩小视野, InputBindingPrompt_Prompt_操作说明, InputBindingPrompt_Prompt_动力矢量, InputBindingPrompt_Prompt_调试面板, InputBindingPrompt_Prompt_复制遥测, InputBindingPrompt_Prompt_返回_Hub, InputBindingPrompt_Prompt_关闭面板, InputBindingPrompt_Prompt_确认, InputBindingPrompt_Prompt_返回1, InputBindingPrompt_Prompt_操作面板1, InputBindingPrompt_Prompt_操作说明1 };
        }
    }
}
