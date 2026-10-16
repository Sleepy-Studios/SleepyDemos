using System;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Settings;
using UnityEngine;

namespace Hotfix
{
    /// 公共输入参数；各宿主的动作资产与改键界面不进入公共设置。
    [Module("Settings")]
    [UIBind("InputSettingsPanelView")]
    public partial class InputSettingsPanelView : SettingsPanelView
    {
        protected override void OnGameObjectInitialize()
        {
            InitializeRows(
                UISettingRow_GamepadStyle,
                UISettingRow_MouseLookMultiplier,
                UISettingRow_TouchLookMultiplier,
                UISettingRow_GamepadLookMultiplier,
                UISettingRow_GamepadLookDegreesPerSecond,
                UISettingRow_GamepadDeadzone,
                UISettingRow_GamepadMaximum,
                UISettingRow_RumbleStrength,
                UISettingRow_GamepadInvertY,
                UISettingRow_RumbleEnabled);
            base.OnGameObjectInitialize();
        }

        public override void Refresh()
        {
            if (!IsLoaded)
            {
                return;
            }
            var value = Candidate.Input;
            var ranges = GameSettingsService.Options;
            Slider(UISettingRow_MouseLookMultiplier, ranges.LookMultiplier, value.MouseLookMultiplier, next => value.MouseLookMultiplier = next);
            Slider(UISettingRow_TouchLookMultiplier, ranges.LookMultiplier, value.TouchLookMultiplier, next => value.TouchLookMultiplier = next);
            Slider(UISettingRow_GamepadLookMultiplier, ranges.LookMultiplier, value.GamepadLookMultiplier, next => value.GamepadLookMultiplier = next);
            Slider(UISettingRow_GamepadLookDegreesPerSecond, ranges.GamepadAngularSpeed, value.GamepadLookDegreesPerSecond, next => value.GamepadLookDegreesPerSecond = next);
            Slider(UISettingRow_GamepadDeadzone, ranges.GamepadDeadzone, value.GamepadDeadzone, next => value.GamepadDeadzone = next);
            Slider(UISettingRow_GamepadMaximum, ranges.GamepadMaximum, value.GamepadMaximum, next => value.GamepadMaximum = next);
            Slider(UISettingRow_RumbleStrength, ranges.RumbleStrength, value.RumbleStrength, next => value.RumbleStrength = next);
            Toggle(UISettingRow_GamepadInvertY, value.GamepadInvertY, next => value.GamepadInvertY = next);
            Toggle(UISettingRow_RumbleEnabled, value.RumbleEnabled, next => value.RumbleEnabled = next);
            UISettingRow_GamepadStyle.SetLabel("Settings.GamepadStyle");
            UISettingRow_GamepadStyle.SetOptions(new[] { "Xbox", "PS5", "NS" }, (int)value.GamepadStyle - 1,
                index => Change(() => value.GamepadStyle = (GamepadStyle)(index + 1)));
        }

        private void Slider(UISettingRow row, Vector2 range, float value, Action<float> update)
        {
            row.SetLabel("Settings." + row.Key);
            row.SetValue(value.ToString("0.##", LanguageService.CurrentCulture));
            row.SetSlider(range, value, next =>
            {
                row.SetValue(next.ToString("0.##", LanguageService.CurrentCulture));
                Change(() => update(next));
            });
        }

        private void Toggle(UISettingRow row, bool value, Action<bool> update)
        {
            row.SetLabel("Settings." + row.Key);
            row.SetToggle(value, next => Change(() => update(next)));
        }
    }
}
