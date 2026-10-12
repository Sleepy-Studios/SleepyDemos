using Core.Runtime;
using Core.Runtime.Localization;
using UnityEngine;

namespace Hotfix
{
    /// 音量页只编辑全局增益，Demo 的本地音量继续独立维护。
    [Module("Settings")]
    [UIBind("AudioSettingsPanelView")]
    public partial class AudioSettingsPanelView : SettingsPanelView
    {
        protected override void OnGameObjectInitialize()
        {
            InitializeRows(
                UISettingRow_Master,
                UISettingRow_Music,
                UISettingRow_Effects,
                UISettingRow_Ui,
                UISettingRow_Muted);
            base.OnGameObjectInitialize();
        }

        public override void Refresh()
        {
            if (!IsLoaded) return;
            SetVolume(UISettingRow_Master, "Settings.Master", Candidate.Audio.Master, v => Candidate.Audio.Master = v);
            SetVolume(UISettingRow_Music, "Settings.Music", Candidate.Audio.Music, v => Candidate.Audio.Music = v);
            SetVolume(UISettingRow_Effects, "Settings.Effects", Candidate.Audio.Effects, v => Candidate.Audio.Effects = v);
            SetVolume(UISettingRow_Ui, "Settings.Ui", Candidate.Audio.Ui, v => Candidate.Audio.Ui = v);
            UISettingRow_Muted.SetLabel("Settings.Muted");
            UISettingRow_Muted.SetToggle(Candidate.Audio.Muted, value => Change(() => Candidate.Audio.Muted = value));
        }

        private void SetVolume(UISettingRow row, string textKey, float selected, System.Action<float> update)
        {
            row.SetLabel(textKey);
            row.SetValue(selected.ToString("P0", LanguageService.CurrentCulture));
            row.SetSlider(new Vector2(0, 1), selected, value =>
            {
                row.SetValue(value.ToString("P0", LanguageService.CurrentCulture));
                Change(() => update(value));
            });
        }
    }
}
