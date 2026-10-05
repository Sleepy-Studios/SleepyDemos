namespace Hotfix
{
    using Core.Runtime;
    using DroneFlight;

    [Module("DroneFlight")]
    [UIBind("DroneFlightDebugView")]
    public partial class DroneFlightDebugView : View<DroneFlightViewData>
    {
        private DroneFlightUiTelemetrySource telemetrySource;

        /// <summary>交付会话数据；已显示的调试台改为读取新机体。</summary>
        /// <param name="data">当前机体与输入会话。</param>
        /// <returns>当前调试台。</returns>
        public override View<DroneFlightViewData> SetData(DroneFlightViewData data)
        {
            base.SetData(data);
            if (State == ViewState.Visible) BindTelemetry();
            return this;
        }

        protected override void OnGameObjectInitialize()
        {
            Button_Close.onClick.AddListener(Close);
            Button_Vectors.onClick.AddListener(() => params1?.Input?.Execute("DebugDraw"));
            Button_Copy.onClick.AddListener(() => params1?.Input?.Execute("CopyTelemetry"));
            UIMenuScope_DroneFlightDebugView.Canceled += Close;
        }

        private void Close() => params1?.Input?.Execute("DebugPanel");

        protected override void OnShow()
        {
            base.OnShow();
            ScrollRect_Readout.verticalNormalizedPosition = 1f;
            BindTelemetry();
        }

        private void BindTelemetry()
        {
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
            var data = snapshot.Debug;
            TextMeshProUGUI_FrontLeft.text = $"左前 FL<pos=58%><color=#F4A23A><mspace=0.6em>{data.Motors.FrontLeft:F3}</mspace></color>";
            TextMeshProUGUI_FrontRight.text = $"右前 FR<pos=58%><color=#F4A23A><mspace=0.6em>{data.Motors.FrontRight:F3}</mspace></color>";
            TextMeshProUGUI_RearLeft.text = $"左后 RL<pos=58%><color=#F4A23A><mspace=0.6em>{data.Motors.RearLeft:F3}</mspace></color>";
            TextMeshProUGUI_RearRight.text = $"右后 RR<pos=58%><color=#F4A23A><mspace=0.6em>{data.Motors.RearRight:F3}</mspace></color>";
            TextMeshProUGUI_Thrust.text = $"总升力  {Number($"{data.TotalThrust:F1} N")}<pos=53%>物理步长  {Number($"{data.FixedStep:F3} s")}";
            TextMeshProUGUI_Pid.text = "<line-height=32>轴<pos=24%>误差<pos=44%>P<pos=64%>I<pos=84%>D\n"
                + DroneDebugFormatting.Axis("横滚", data.Roll) + "\n"
                + DroneDebugFormatting.Axis("俯仰", data.Pitch) + "\n"
                + DroneDebugFormatting.Axis("偏航", data.Yaw);
            TextMeshProUGUI_Mass.text = $"<line-height=32>主刚体<pos=30%>{Number($"{data.BodyMass:F2} kg")}<pos=57%>悬停指令<pos=83%>{Number($"{data.HoverCommand:F3}")}\n"
                + $"附加装备<pos=30%>{Number("0.00 kg")}<pos=57%>动力余量<pos=83%>{Number($"{data.PowerReserve:P0}")}\n"
                + $"当前总承载<pos=30%>{Number($"{data.SupportedMass:F2} kg")}<pos=57%>起落架<pos=83%><color=#F4A23A>{DroneFlightUiTelemetrySource.FormatGear(data.Gear)}</color>\n"
                + $"额定载重<pos=30%>{Number($"{data.RatedPayload:F2} kg")}";
            TextMeshProUGUI_Equipment.text = DroneDebugFormatting.Equipment(snapshot.Equipment);
        }

        private static string Number(string value) => $"<color=#F4A23A><mspace=0.6em>{value}</mspace></color>";

        private void Unsubscribe()
        {
            if (telemetrySource != null) telemetrySource.SnapshotChanged -= OnSnapshotChanged;
            telemetrySource = null;
        }
    }
}
