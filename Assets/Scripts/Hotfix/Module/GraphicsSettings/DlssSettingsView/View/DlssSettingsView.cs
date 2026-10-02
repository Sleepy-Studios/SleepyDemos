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
        private bool entrySuppressed;
        private bool originalOpenButton;
        private bool originalPanel;
        public bool IsSettingsPanelOpen => State == ViewState.Visible && settingsPanel != null && settingsPanel.gameObject.activeSelf;

        /// <summary>显示或关闭本View拥有的面板；抑制期间不会重开已隐藏的控件。</summary>
        /// <param name="open">所需面板开闭状态。</param>
        public void SetSettingsPanelOpen(bool open)
        {
            if (State != ViewState.Visible || entrySuppressed || settingsPanel == null) return;
            if (open) { transform.SetAsLastSibling(); RefreshState(); }
            settingsPanel.gameObject.SetActive(open);
        }

        private void ApplyEntrySuppression(bool suppressed)
        {
            if (entrySuppressed == suppressed) return;
            entrySuppressed = suppressed;
            if (suppressed)
            {
                originalOpenButton = Button_OpenButton.gameObject.activeSelf;
                originalPanel = settingsPanel.gameObject.activeSelf;
                Button_OpenButton.gameObject.SetActive(false); settingsPanel.gameObject.SetActive(false);
            }
            else
            {
                if (Button_OpenButton != null) Button_OpenButton.gameObject.SetActive(originalOpenButton);
                if (settingsPanel != null) settingsPanel.gameObject.SetActive(originalPanel);
            }
        }
        protected override void OnGameObjectInitialize() { }
        protected override void OnShow()
        {
            base.OnShow();
            settingsPanel = transform.Find("SettingsPanel");
            settingsPanel.gameObject.SetActive(false);
            GraphicsSettingsUI.EntrySuppressionChanged += ApplyEntrySuppression;
            ApplyEntrySuppression(GraphicsSettingsUI.IsEntrySuppressed);
            StreamlineRuntime.Changed += RefreshState;
            RefreshState();
        }
        protected override void OnHide()
        {
            GraphicsSettingsUI.EntrySuppressionChanged -= ApplyEntrySuppression;
            ApplyEntrySuppression(false);
            StreamlineRuntime.Changed -= RefreshState;
            base.OnHide();
        }
        protected override void OnDestroy()
        {
            GraphicsSettingsUI.EntrySuppressionChanged -= ApplyEntrySuppression;
            ApplyEntrySuppression(false);
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
        private void OnOpenButtonClick() => SetSettingsPanelOpen(true);
        private void OnCloseButtonClick() => SetSettingsPanelOpen(false);
    }
}
