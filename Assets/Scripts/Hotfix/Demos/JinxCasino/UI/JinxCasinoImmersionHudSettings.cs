using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.UI
{
    public sealed partial class JinxCasinoImmersionHudPresenter
    {
        [SerializeField] private Button settingsMainButton;
        [SerializeField] private Button settingsPauseButton;
        [SerializeField] private JinxCasinoLocalSettingsPresenter localSettings;
        private bool settingsOpen;
        private int lastSettingsCancelFrame = -1;
        private bool HasSettingsUi => localSettings != null && (settingsMainButton != null || settingsPauseButton != null);

        private void BindSettingsControls()
        {
            settingsOpen = false; lastSettingsCancelFrame = -1;
            ListenTutorial(settingsMainButton, OpenSettings); ListenTutorial(settingsPauseButton, OpenSettings);
            // 入场先读取本机偏好，Router稍后建立时就使用已保存值。旧HUD未配置设置页也能读取。
            if (localSettings != null) localSettings.Bind(owner, OnSettingsClosed);
            else owner.Settings.Load();
            if (localSettings != null) localSettings.gameObject.SetActive(false);
        }

        private void UnbindSettingsControls()
        {
            UnlistenTutorial(settingsMainButton, OpenSettings); UnlistenTutorial(settingsPauseButton, OpenSettings);
            localSettings?.Unbind(); settingsOpen = false;
            if (localSettings != null) localSettings.gameObject.SetActive(false);
        }

        private void OpenSettings()
        {
            if (owner == null || owner.IsBusy || !HasSettingsUi || settingsOpen || menuState != 0 && menuState != 1) return;
            settingsOpen = true;
            localSettings.ShowSettings(); Refresh();
        }

        private void OnSettingsClosed()
        {
            if (owner == null) return;
            lastSettingsCancelFrame = Time.frameCount;
            settingsOpen = false; Refresh();
        }

        private int ResolveSettingsHudState(int normalState)
            => HasSettingsUi && settingsOpen ? 9 : normalState;

        private GameObject SettingsFirstSelection(int state)
            => state == 9 ? localSettings.FirstSelection : SaveFirstSelection(state);

        private void RefreshSettingsControls(int state)
        {
            if (settingsMainButton != null) settingsMainButton.gameObject.SetActive(state == 0);
            if (settingsPauseButton != null) settingsPauseButton.gameObject.SetActive(state == 1);
        }

        /// 设置取消只撤销预览并回原菜单；不解除用户、后台或设备断开产生的暂停。
        public void CloseSettingsWindow()
        {
            if (owner == null || !settingsOpen || lastSettingsCancelFrame == Time.frameCount) return;
            lastSettingsCancelFrame = Time.frameCount;
            localSettings.CloseSettings();
        }
    }
}
