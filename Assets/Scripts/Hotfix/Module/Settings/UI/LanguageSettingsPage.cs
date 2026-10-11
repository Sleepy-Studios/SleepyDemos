using Core.Runtime;
using Core.Runtime.Localization;
using TMPro;
using UnityEngine;

namespace Hotfix
{
    /// 语言候选仅在 Hub 展示；选择时不改变运行服务，应用由 Hub 重建事务完成。
    public sealed class LanguageSettingsPage : SettingsPage
    {
        [SerializeField] private UITab choices;
        [SerializeField] private TMP_Text help;

        public override void Refresh()
        {
            var languages = LanguageService.Languages;
            var names = new string[languages.Count];
            int selected = 0;
            for (int index = 0; index < languages.Count; index++)
            {
                names[index] = languages[index].DisplayName;
                if (languages[index].Id == Candidate.Language) selected = index;
            }
            LanguageService.SetText(help, "Settings.LanguageHelp");
            choices.Init(names, initIndex: selected, notify: false, isAsync: false);
            choices.Unregister(OnLanguageSelected);
            choices.Register(OnLanguageSelected);
        }

        private void OnLanguageSelected(int index) => Change(() => Candidate.Language = LanguageService.Languages[index].Id);
        public override void Release() => choices.Unregister(OnLanguageSelected);
    }
}
