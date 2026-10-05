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
        private DroneFlightUiTelemetrySource telemetrySource;

        protected override void OnGameObjectInitialize()
        {
            base.OnGameObjectInitialize();
            resetProgressBar = Image_ResetProgressFill.GetComponent<UIProgressBar>();
        }

        /// <summary>交付会话数据；已显示的 HUD 立即切换遥测订阅。</summary>
        /// <param name="data">当前机体与输入会话。</param>
        /// <returns>当前 HUD。</returns>
        public override View<DroneFlightViewData> SetData(DroneFlightViewData data)
        {
            base.SetData(data);
            if (State == ViewState.Visible) BindTelemetry();
            return this;
        }

        protected override void OnShow()
        {
            base.OnShow();
            BindTelemetry();
        }

        private void BindTelemetry()
        {
            Unsubscribe();
            telemetrySource = params1?.TelemetrySource;
            InputDeviceState.Changed += RefreshPrompt;
            if (telemetrySource != null)
            {
                telemetrySource.SnapshotChanged += OnSnapshotChanged;
                OnSnapshotChanged(telemetrySource.Current);
            }
        }

        protected override void OnHide() { Unsubscribe(); base.OnHide(); }
        protected override void OnDestroy() { Unsubscribe(); base.OnDestroy(); }

        private void RefreshPrompt()
        {
            if (telemetrySource != null) OnSnapshotChanged(telemetrySource.Current);
        }

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
            DroneControlsPresenter_DroneFlightHudView.SetEquipment(snapshot.Equipment.Kind);
        }

        private void Unsubscribe()
        {
            InputDeviceState.Changed -= RefreshPrompt;
            if (telemetrySource != null) telemetrySource.SnapshotChanged -= OnSnapshotChanged;
            telemetrySource = null;
        }
    }
}
