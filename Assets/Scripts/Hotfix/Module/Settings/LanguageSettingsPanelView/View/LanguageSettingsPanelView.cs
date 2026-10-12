using Core.Runtime;
using Core.Runtime.Localization;
using TMPro;
using UnityEngine;

namespace Hotfix
{
    /// 语言候选仅在 Hub 展示；选择时不改变运行服务，应用由 Hub 重建事务完成。
    [Module("Settings")]
    [UIBind("LanguageSettingsPanelView")]
    public partial class LanguageSettingsPanelView : SettingsPanelView
    {
        protected override void OnGameObjectInitialize()
        {
            InitializeRows();
            base.OnGameObjectInitialize();
        }


        public override void Refresh()
        {
            if (!IsLoaded) return;
            var languages = LanguageService.Languages;
            var names = new string[languages.Count];
            int selected = 0;
            for (int index = 0; index < languages.Count; index++)
            {
                names[index] = languages[index].DisplayName;
                if (languages[index].Id == Candidate.Language) selected = index;
            }
            LanguageService.SetText(TextMeshProUGUI_Language_help, "Settings.LanguageHelp");
            UITab_Language_choices.Init(names, initIndex: selected, notify: false, isAsync: false);
            UITab_Language_choices.Unregister(OnLanguageSelected);
            UITab_Language_choices.Register(OnLanguageSelected);
        }

        private void OnLanguageSelected(int index) => Change(() => Candidate.Language = LanguageService.Languages[index].Id);
        public override void Release()
        {
            if (UITab_Language_choices != null) UITab_Language_choices.Unregister(OnLanguageSelected);
        }
    }
}
