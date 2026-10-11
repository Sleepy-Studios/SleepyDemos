using Core.Runtime.Localization;
using UnityEngine;

namespace Hotfix
{
    /// 音量页只编辑全局增益，Demo 的本地音量继续独立维护。
    public sealed class AudioSettingsPage : SettingsPage
    {
        public override void Refresh()
        {
            SetVolume("Master", "Settings.Master", Candidate.Audio.Master, v => Candidate.Audio.Master = v);
            SetVolume("Music", "Settings.Music", Candidate.Audio.Music, v => Candidate.Audio.Music = v);
            SetVolume("Effects", "Settings.Effects", Candidate.Audio.Effects, v => Candidate.Audio.Effects = v);
            SetVolume("Ui", "Settings.Ui", Candidate.Audio.Ui, v => Candidate.Audio.Ui = v);
            Row("Muted").SetLabel("Settings.Muted");
            Row("Muted").SetToggle(Candidate.Audio.Muted, value => Change(() => Candidate.Audio.Muted = value));
        }

        private void SetVolume(string field, string textKey, float selected, System.Action<float> update)
        {
            var row = Row(field);
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
