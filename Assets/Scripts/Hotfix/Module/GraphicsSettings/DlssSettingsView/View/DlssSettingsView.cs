using Core.Runtime;
using Core.Runtime.Rendering.Streamline;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    [Module("GraphicsSettings")]
    [Mvc("DlssSettingsView")]
    public partial class DlssSettingsView : View
    {
        private Transform settingsPanel;
        protected override void OnGameObjectInitialize() { }
        protected override void OnShow()
        {
            base.OnShow();
            settingsPanel = transform.Find("SettingsPanel");
            settingsPanel.gameObject.SetActive(false);
            StreamlineRuntime.Changed += RefreshState;
            RefreshState();
        }
        protected override void OnHide()
        {
            StreamlineRuntime.Changed -= RefreshState;
            base.OnHide();
        }
        protected override void OnDestroy()
        {
            StreamlineRuntime.Changed -= RefreshState;
            base.OnDestroy();
        }
        private void RefreshState()
        {
            TextMeshProUGUI_DeviceText.text = SystemInfo.graphicsDeviceName + "\n" + SystemInfo.graphicsDeviceType;
            var mode = StreamlineRuntime.RequestedMode;
            string selected = mode == StreamlineDlssMode.Dlaa ? "DLAA" : mode?.ToString() ?? "关闭";
            TextMeshProUGUI_ModeText.text = "选择：" + selected + "\n生效：" + (StreamlineRuntime.EffectiveMode?.ToString() ?? "关闭");
            var input = StreamlineRuntime.InputSize;
            var output = StreamlineRuntime.OutputSize;
            TextMeshProUGUI_ResolutionText.text = $"输入 {input.x} x {input.y}\n输出 {output.x} x {output.y}";
            TextMeshProUGUI_StatusText.text = StreamlineRuntime.Status;
            bool enabled = !StreamlineRuntime.IsBusy;
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
            button.image.color = selected ? new Color(0.05f, 0.42f, 0.48f, 1) : new Color(0.09f, 0.15f, 0.23f, 1);
        }

        private void OnOffButtonClick() => StreamlineRuntime.SetMode(null);
        private void OnQualityButtonClick() => StreamlineRuntime.SetMode(StreamlineDlssMode.Quality);
        private void OnBalancedButtonClick() => StreamlineRuntime.SetMode(StreamlineDlssMode.Balanced);
        private void OnPerformanceButtonClick() => StreamlineRuntime.SetMode(StreamlineDlssMode.Performance);
        private void OnUltraPerformanceButtonClick() => StreamlineRuntime.SetMode(StreamlineDlssMode.UltraPerformance);
        private void OnDlaaButtonClick() => StreamlineRuntime.SetMode(StreamlineDlssMode.Dlaa);
        private void OnOpenButtonClick() { transform.SetAsLastSibling(); RefreshState(); settingsPanel.gameObject.SetActive(true); }
        private void OnCloseButtonClick() => settingsPanel.gameObject.SetActive(false);
    }
}
