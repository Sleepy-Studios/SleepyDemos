using Core.Runtime;
using Core.Runtime.Rendering.Streamline;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    [Module("GraphicsSettings")]
    [UIBind("DlssSettingsView")]
    public partial class DlssSettingsView : View
    {
        private Hotfix.Dlss.DlssData dlssSession;

        protected override void OnShow()
        {
            dlssSession = GlobalData.Get<Hotfix.Dlss.DlssData>();
            base.OnShow();
        }

        protected override void OnGameObjectInitialize()
        {
            BindData<GraphicsSettingsData>(RefreshState);
            UIMenuScope_SettingsPanel.Canceled += OnCloseButtonClick;
        }

        protected override void OnHide()
        {
            GlobalData.Dispatch(new Hotfix.Dlss.DlssSettingsClosedAction(dlssSession));
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            UIMenuScope_SettingsPanel.Canceled -= OnCloseButtonClick;
            GlobalData.Dispatch(new Hotfix.Dlss.DlssSettingsClosedAction(dlssSession));
            base.OnDestroy();
        }

        private void RefreshState(GraphicsSettingsData data)
        {
            TextMeshProUGUI_DeviceText.text = data.Hardware?.GraphicsDeviceName + "\n" + data.Hardware?.GraphicsDeviceType;
            var mode = data.RequestedMode;
            string selected = mode == StreamlineDlssMode.Dlaa ? "DLAA" : mode?.ToString() ?? "关闭";
            TextMeshProUGUI_ModeText.text = "选择：" + selected + "\n生效：" + (data.EffectiveMode?.ToString() ?? "关闭");
            var input = data.InputSize;
            var output = data.OutputSize;
            TextMeshProUGUI_ResolutionText.text = $"输入 {input.x} x {input.y}\n输出 {output.x} x {output.y}";
            TextMeshProUGUI_StatusText.text = data.Status;
            bool enabled = !data.IsBusy;
            SetButton(Button_OffButton, !mode.HasValue, enabled);
            SetButton(Button_QualityButton, mode == StreamlineDlssMode.Quality, enabled);
            SetButton(Button_BalancedButton, mode == StreamlineDlssMode.Balanced, enabled);
            SetButton(Button_PerformanceButton, mode == StreamlineDlssMode.Performance, enabled);
            SetButton(Button_UltraPerformanceButton, mode == StreamlineDlssMode.UltraPerformance, enabled);
            SetButton(Button_DlaaButton, mode == StreamlineDlssMode.Dlaa, enabled);
        }

        private static void SetButton(Button button, bool selected, bool enabled)
        {
            button.interactable = enabled;
            button.GetComponent<UIState>().SetState(selected ? "Selected" : "Normal");
        }

        private void OnOffButtonClick() => GlobalData.Dispatch(new GraphicsSettingsSetModeAction(null));

        private void OnQualityButtonClick() => GlobalData.Dispatch(new GraphicsSettingsSetModeAction(StreamlineDlssMode.Quality));

        private void OnBalancedButtonClick() => GlobalData.Dispatch(new GraphicsSettingsSetModeAction(StreamlineDlssMode.Balanced));

        private void OnPerformanceButtonClick() => GlobalData.Dispatch(new GraphicsSettingsSetModeAction(StreamlineDlssMode.Performance));

        private void OnUltraPerformanceButtonClick() => GlobalData.Dispatch(new GraphicsSettingsSetModeAction(StreamlineDlssMode.UltraPerformance));

        private void OnDlaaButtonClick() => GlobalData.Dispatch(new GraphicsSettingsSetModeAction(StreamlineDlssMode.Dlaa));

        private void OnCloseButtonClick() => UIManager.Instance.CloseAsync<DlssSettingsView>().Forget();
    }
}
