using Core.Runtime;
using Core.Runtime.Rendering.Streamline;
using Hotfix.Dlss;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    [Module("Dlss")]
    [Mvc("DlssSettingsView")]
    public partial class DlssSettingsView : View<DlssDemoController>
    {
        private DlssDemoController controller;

        protected override void OnGameObjectInitialize() { }

        protected override void OnShow()
        {
            base.OnShow();
            controller = params1;
            if (controller != null) controller.Changed += RefreshState;
            RefreshState();
        }

        protected override void OnHide()
        {
            if (controller != null) controller.Changed -= RefreshState;
            controller = null;
            RawImage_WorldImage.texture = null;
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            if (controller != null) controller.Changed -= RefreshState;
            controller = null;
            base.OnDestroy();
        }

        private void RefreshState()
        {
            if (controller == null) return;
            var session = controller.Session;
            TextMeshProUGUI_DeviceText.text = SystemInfo.graphicsDeviceName + "\n" + SystemInfo.graphicsDeviceType;
            if (session == null) return;
            RawImage_WorldImage.texture = session.OutputTexture;
            TextMeshProUGUI_ModeText.text = "当前：" + (session.Mode == StreamlineDlssMode.Dlaa ? "DLAA" : session.Mode.HasValue ? session.Mode.Value.ToString() : "关闭");
            TextMeshProUGUI_ResolutionText.text = $"输入 {session.InputSize.x} x {session.InputSize.y}\n输出 {session.OutputSize.x} x {session.OutputSize.y}";
            string error = session.Error;
            TextMeshProUGUI_StatusText.text = controller.IsBusy ? "正在切换，请稍候…" :
                !string.IsNullOrEmpty(error) ? "未能启用效果：" + (error.Length > 150 ? error.Substring(0, 150) + "…" : error) :
                session.Mode.HasValue ? (session.HasEvaluatedFrame ? "运行中 · 已处理 " + session.CapturedFrames + " 帧" : "等待首帧…") : "原生分辨率 · DLSS 已关闭";
            SetButton(Button_OffButton, !session.Mode.HasValue, !controller.IsBusy);
            SetButton(Button_QualityButton, session.Mode == StreamlineDlssMode.Quality, !controller.IsBusy && StreamlineRuntime.IsBackendSupported);
            SetButton(Button_BalancedButton, session.Mode == StreamlineDlssMode.Balanced, !controller.IsBusy && StreamlineRuntime.IsBackendSupported);
            SetButton(Button_PerformanceButton, session.Mode == StreamlineDlssMode.Performance, !controller.IsBusy && StreamlineRuntime.IsBackendSupported);
            SetButton(Button_UltraPerformanceButton, session.Mode == StreamlineDlssMode.UltraPerformance, !controller.IsBusy && StreamlineRuntime.IsBackendSupported);
            SetButton(Button_DlaaButton, session.Mode == StreamlineDlssMode.Dlaa, !controller.IsBusy && StreamlineRuntime.IsBackendSupported);
            Button_BackButton.interactable = !controller.IsBusy;
            Button_ResetCameraButton.interactable = !controller.IsBusy;
        }

        private static void SetButton(Button button, bool selected, bool enabled)
        {
            button.interactable = enabled;
            button.image.color = selected ? new Color(0.05f, 0.42f, 0.48f, 1) : new Color(0.09f, 0.15f, 0.23f, 1);
        }

        private void OnOffButtonClick() => controller?.SetMode(null);
        private void OnQualityButtonClick() => controller?.SetMode(StreamlineDlssMode.Quality);
        private void OnBalancedButtonClick() => controller?.SetMode(StreamlineDlssMode.Balanced);
        private void OnPerformanceButtonClick() => controller?.SetMode(StreamlineDlssMode.Performance);
        private void OnUltraPerformanceButtonClick() => controller?.SetMode(StreamlineDlssMode.UltraPerformance);
        private void OnDlaaButtonClick() => controller?.SetMode(StreamlineDlssMode.Dlaa);
        private void OnResetCameraButtonClick() => controller?.ResetCamera();
        private void OnBackButtonClick() => controller?.RequestExit();
    }
}
