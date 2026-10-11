using Core.Runtime.Localization;
using Core.Runtime.Settings;

namespace Hotfix
{
    /// 辅助页保存允许偏好；最终动态和抖动限制仍由公共策略落实。
    public sealed class AccessibilitySettingsPage : SettingsPage
    {
        public override void Refresh()
        {
            var motion = Row("ReducedMotion");
            motion.SetLabel("Settings.ReducedMotion");
            motion.SetOptions(new[] { LanguageService.Get("Common.Auto"), LanguageService.Get("Common.On"), LanguageService.Get("Common.Off") },
                (int)Candidate.Accessibility.ReducedMotion, value => Change(() => Candidate.Accessibility.ReducedMotion = (ReducedMotionMode)value));
            Row("CameraShake").SetLabel("Settings.CameraShake");
            Row("CameraShake").SetToggle(Candidate.Accessibility.CameraShake, value => Change(() => Candidate.Accessibility.CameraShake = value));
            Row("InputPrompts").SetLabel("Settings.InputPrompts");
            Row("InputPrompts").SetToggle(Candidate.Accessibility.ShowInputPrompts, value => Change(() => Candidate.Accessibility.ShowInputPrompts = value));
        }
    }
}
