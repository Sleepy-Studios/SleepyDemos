namespace Hotfix
{
    using Core.Runtime;
    using DroneFlight;
    using DroneFlight.Adapters.SleepyDemos;
    using TMPro;

    [Module("DroneFlight")]
    [Mvc("DroneFlightHudView")]
    public partial class DroneFlightHudView : View<DroneFlightViewData>
    {
        private DroneFlightUiTelemetrySource telemetrySource;
        private bool controlsVisible;
        private TextMeshProUGUI controlsHeaderText;
        private TextMeshProUGUI flightControlsText;
        private TextMeshProUGUI cameraControlsText;
        private TextMeshProUGUI systemControlsText;

        protected override void OnGameObjectInitialize()
        {
            ResolveControlSections();
        }

        protected override void OnShow()
        {
            base.OnShow();
            Unsubscribe();
            ResolveControlSections();
            SetControlsVisible(true);
            telemetrySource = params1?.TelemetrySource;
            RefreshControlSections(telemetrySource != null
                ? telemetrySource.Current.Equipment.Kind
                : DroneEquipmentKind.None);
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
            RefreshControlSections(snapshot.Equipment.Kind);
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

        /// 切换分类操作说明；HUD 每次显示时默认展开。
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
            if (controlsHeaderText != null)
            {
                controlsHeaderText.text = value ? "操作提示  ·  F1 收起" : "操作提示  ·  F1 展开";
            }
        }

        private void ResolveControlSections()
        {
            var panel = TextMeshProUGUI_ControlsText != null
                ? TextMeshProUGUI_ControlsText.transform.parent
                : null;
            if (panel == null)
            {
                return;
            }

            controlsHeaderText = panel.Find("ControlsHeaderText")?.GetComponent<TextMeshProUGUI>();
            flightControlsText = panel.Find("FlightControlsText")?.GetComponent<TextMeshProUGUI>();
            cameraControlsText = panel.Find("CameraControlsText")?.GetComponent<TextMeshProUGUI>();
            systemControlsText = panel.Find("SystemControlsText")?.GetComponent<TextMeshProUGUI>();
        }

        private void RefreshControlSections(DroneEquipmentKind kind)
        {
            if (flightControlsText != null)
            {
                flightControlsText.text = "<b>飞行与档位</b>\n" + DroneHudFormatter.FormatFlightControls();
            }
            if (cameraControlsText != null)
            {
                cameraControlsText.text = "<b>视角与机构</b>\n" + DroneHudFormatter.FormatCameraControls();
            }
            if (systemControlsText != null)
            {
                systemControlsText.text = "<b>系统</b>\n" + DroneHudFormatter.FormatSystemControls();
            }
            if (TextMeshProUGUI_ControlsText != null)
            {
                TextMeshProUGUI_ControlsText.text =
                    "<b>当前装备</b>    " + DroneHudFormatter.FormatEquipmentControls(kind);
            }
        }
    }
}
