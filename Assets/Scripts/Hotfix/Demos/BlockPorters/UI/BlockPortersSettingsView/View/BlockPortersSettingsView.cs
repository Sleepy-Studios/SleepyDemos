using Cysharp.Threading.Tasks;
using Core.Runtime;
using Hotfix.BlockPorters;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Hotfix
{
    /// 设置与暂停页；通过Action切换声音、重开和退出。
    [Module("BlockPorters")]
    [UIBind("BlockPortersSettingsView")]
    public sealed partial class BlockPortersSettingsView : View
    {
        private Button sound;
        private Button restart;
        private Button exit;
        private Button settingsContinue;
        private Button settingsClose;
        private TextMeshProUGUI soundLabel;
        private BlockPortersData owner;
        private UIMenuScope menu;
        private UIBtnSwitch soundSwitch;
        protected override void OnGameObjectInitialize()
        {
            InitializeReferences();
            owner = GlobalData.Get<BlockPortersData>();
            BindData<BlockPortersData>(OnData);

            menu = gameObject.GetComponent<UIMenuScope>();
            menu.Canceled += Close; AddBinding(() => { if (menu != null) menu.Canceled -= Close; });
            soundSwitch = sound.GetComponent<UIBtnSwitch>();
            soundSwitch.SetAction(value => { if (owner != null && value == owner.IsMuted) GlobalData.Dispatch(new BlockPortersToggleSoundAction()); });
            restart.onClick.AddListener(() => GlobalData.Dispatch(new BlockPortersRestartAction()));
            exit.onClick.AddListener(() => GlobalData.Dispatch(new BlockPortersExitAction()));
            settingsContinue.onClick.AddListener(Close);
            settingsClose.onClick.AddListener(Close);
        }



        private void Close() => GlobalData.Dispatch(new BlockPortersCloseSettingsAction());
        private void Refresh()
        {
            if (owner == null) return;
            soundSwitch.SetStatus(!owner.IsMuted);
            soundLabel.text = owner.IsMuted ? "声音已关闭" : "声音已开启";
            sound.interactable = restart.interactable = exit.interactable = settingsContinue.interactable = settingsClose.interactable = !owner.IsExiting;
        }

        private void OnData(BlockPortersData value) { owner = value; Refresh(); }
        private void InitializeReferences()
        {
            sound = Button_Sound;
            restart = Button_Restart;
            exit = Button_Exit;
            settingsContinue = Button_SettingsContinue;
            settingsClose = Button_SettingsClose;
            soundLabel = TextMeshProUGUI_SoundLabel;
        }
    }
}
