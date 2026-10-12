using Core.Runtime;
using Core.Runtime.Localization;
using Core.Runtime.Settings;

namespace Hotfix
{
    /// 辅助页保存允许偏好；最终动态和抖动限制仍由公共策略落实。
    [Module("Settings")]
    [UIBind("AccessibilitySettingsPanelView")]
    public partial class AccessibilitySettingsPanelView : SettingsPanelView
    {
        protected override void OnGameObjectInitialize()
        {
            InitializeRows(
                UISettingRow_ReducedMotion,
                UISettingRow_CameraShake,
                UISettingRow_InputPrompts);
            base.OnGameObjectInitialize();
        }

        public override void Refresh()
        {
            if (!IsLoaded) return;
            var motion = UISettingRow_ReducedMotion;
            motion.SetLabel("Settings.ReducedMotion");
            motion.SetOptions(new[] { LanguageService.Get("Common.Auto"), LanguageService.Get("Common.On"), LanguageService.Get("Common.Off") },
                (int)Candidate.Accessibility.ReducedMotion, value => Change(() => Candidate.Accessibility.ReducedMotion = (ReducedMotionMode)value));
            UISettingRow_CameraShake.SetLabel("Settings.CameraShake");
            UISettingRow_CameraShake.SetToggle(Candidate.Accessibility.CameraShake, value => Change(() => Candidate.Accessibility.CameraShake = value));
            UISettingRow_InputPrompts.SetLabel("Settings.InputPrompts");
            UISettingRow_InputPrompts.SetToggle(Candidate.Accessibility.ShowInputPrompts, value => Change(() => Candidate.Accessibility.ShowInputPrompts = value));
        }
    }
}
