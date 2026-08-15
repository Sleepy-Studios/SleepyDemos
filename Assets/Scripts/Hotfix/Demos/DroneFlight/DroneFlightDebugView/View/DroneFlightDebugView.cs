namespace Hotfix
{
    using Core.Runtime;
    using DroneFlight;

    [Module("DroneFlight")]
    [Mvc("DroneFlightDebugView")]
    public partial class DroneFlightDebugView : View<DroneFlightViewData>
    {
        private DroneFlightUiTelemetrySource telemetrySource;

        protected override void OnGameObjectInitialize()
        {
        }

        protected override void OnShow()
        {
            base.OnShow();
            Unsubscribe();
            telemetrySource = params1?.TelemetrySource;
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
            if (TextMeshProUGUI_DebugText != null)
            {
                TextMeshProUGUI_DebugText.text = snapshot.DebugText;
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
    }
}
