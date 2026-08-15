namespace Hotfix
{
    using Core.Runtime;
    using DroneFlight;

    [Module("DroneFlight")]
    [Mvc("DroneFlightHudView")]
    public partial class DroneFlightHudView : View<DroneFlightViewData>
    {
        private DroneFlightUiTelemetrySource telemetrySource;
        private bool controlsVisible;

        protected override void OnGameObjectInitialize()
        {
        }

        protected override void OnShow()
        {
            base.OnShow();
            Unsubscribe();
            SetControlsVisible(false);
            telemetrySource = params1?.TelemetrySource;
            if (TextMeshProUGUI_ControlsText != null)
            {
                TextMeshProUGUI_ControlsText.text = DroneHudFormatter.FormatControls(
                    telemetrySource != null
                        ? telemetrySource.Current.Equipment.Kind
                        : DroneEquipmentKind.None);
            }
            if (telemetrySource != null)
            {
                telemetrySource.SnapshotChanged += OnSnapshotChanged;
                OnSnapshotChanged(telemetrySource.Current);
            }
        }

        protected override void OnHide()
        {
            Unsubscribe();
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            Unsubscribe();
            base.OnDestroy();
        }

        private void OnSnapshotChanged(DroneFlightUiSnapshot snapshot)
        {
            if (CanvasGroup_DroneFlightHudView != null)
            {
                CanvasGroup_DroneFlightHudView.alpha = snapshot.TelemetryVisible ? 1f : 0f;
                CanvasGroup_DroneFlightHudView.interactable = false;
                CanvasGroup_DroneFlightHudView.blocksRaycasts = false;
            }
            if (TextMeshProUGUI_FlightText != null)
                TextMeshProUGUI_FlightText.text = DroneHudFormatter.FormatFlight(snapshot.Hud);
            if (TextMeshProUGUI_CameraText != null)
                TextMeshProUGUI_CameraText.text = DroneHudFormatter.FormatCamera(snapshot.Hud);
            if (TextMeshProUGUI_PayloadText != null)
                TextMeshProUGUI_PayloadText.text = snapshot.EquipmentText;
            if (TextMeshProUGUI_WarningText != null)
                TextMeshProUGUI_WarningText.text = snapshot.WarningText;
            if (TextMeshProUGUI_ControlsText != null)
                TextMeshProUGUI_ControlsText.text = DroneHudFormatter.FormatControls(snapshot.Equipment.Kind);
            if (Image_ResetProgressFill != null)
            {
                Image_ResetProgressFill.fillAmount = snapshot.ResetProgress;
                Image_ResetProgressFill.transform.parent.gameObject.SetActive(snapshot.ResetProgress > 0f);
            }
            if (TextMeshProUGUI_ResetProgressText != null)
            {
                TextMeshProUGUI_ResetProgressText.text =
                    $"重新运行场景 {snapshot.ResetProgress * snapshot.ResetHoldSeconds:F1} / {snapshot.ResetHoldSeconds:F1} s";
            }
        }

        private void Unsubscribe()
        {
            if (telemetrySource != null)
            {
                telemetrySource.SnapshotChanged -= OnSnapshotChanged;
            }
            telemetrySource = null;
        }

        /// 切换完整操作说明；HUD 默认只保留飞行关键量。
        internal void ToggleControls()
        {
            SetControlsVisible(!controlsVisible);
        }

        private void SetControlsVisible(bool value)
        {
            controlsVisible = value;
            if (TextMeshProUGUI_ControlsText != null)
            {
                TextMeshProUGUI_ControlsText.transform.parent.gameObject.SetActive(value);
            }
        }
    }
}
