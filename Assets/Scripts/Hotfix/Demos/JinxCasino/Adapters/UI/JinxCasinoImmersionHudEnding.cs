using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    public sealed partial class JinxCasinoImmersionHudPresenter
    {
        [SerializeField] private GameObject standardEndingPanel;
        [SerializeField] private TMP_Text standardEndingTitleText;
        [SerializeField] private TMP_Text standardEndingMessageText;
        [SerializeField] private TMP_Text standardEndingStatsText;
        [SerializeField] private TMP_Text standardEndingProfileText;
        [SerializeField] private Button standardEndingSaveButton;
        [SerializeField] private Button standardEndingReturnButton;
        private int lastStandardReturnFrame = -1;
        private bool HasStandardEndingUi => standardEndingPanel != null && standardEndingReturnButton != null;

        private void BindStandardEndingControls()
        {
            lastStandardReturnFrame = -1;
            ListenTutorial(standardEndingReturnButton, ReturnFromStandardEnding);
            ListenTutorial(standardEndingSaveButton, SaveStandardEnding);
        }
        private void UnbindStandardEndingControls()
        {
            UnlistenTutorial(standardEndingReturnButton, ReturnFromStandardEnding);
            UnlistenTutorial(standardEndingSaveButton, SaveStandardEnding);
        }
        // 已提交局及相机退出过渡均保留原输入；不以 Closing/Finale 冒充结果。
        private int ResolveEndingHudState(int normalState) => HasStandardEndingUi && owner.HasStandardEnding &&
            !owner.HasActiveAdventureRound && !owner.HasImmersionFocus ? 8 : normalState;

        private void RefreshStandardEndingControls(int state)
        {
            if (!HasStandardEndingUi) return;
            standardEndingPanel.SetActive(state == 8);
            if (state != 8) return;
            var snapshot = owner.AdventureState;
            bool dignity = snapshot.Ending == CasinoAdventureEnding.LeaveWithDignity;
            bool withdraw = snapshot.Ending == CasinoAdventureEnding.Withdraw;
            standardEndingTitleText.text = dignity ? "见好就收 · 体面离场" : withdraw ? "狼狈撤离 · 下次再来" : "本次旅程已结束";
            standardEndingMessageText.text = dignity ? "你已通过本区核验，领取离场券。下次来，再试试新的选择。"
                : withdraw ? "你选择结束本次旅程。剩余筹码和本次记录如下。" : "本次旅程的结果已确定。";
            standardEndingStatsText.text = "剩余筹码  " + snapshot.Coins + "\n完成区域  " + snapshot.CompletedStages + " / " + snapshot.Config.StageCount +
                "\n已结算机台  " + snapshot.SettledRoundSequence + " 局";
            // 原宿主完成成长写盘后才更新此状态；UI 不登记、不推算声望奖励。
            standardEndingProfileText.text = owner.ProfileStatus ?? string.Empty;
            if (standardEndingSaveButton != null) standardEndingSaveButton.interactable = HasSaveUi && !owner.IsBusy;
            standardEndingReturnButton.interactable = !owner.IsBusy;
        }
        private void SaveStandardEnding()
        {
            if (ResolveEndingHudState(2) != 8 || !HasSaveUi) return;
            OpenSaveWrite();
        }
        private void ReturnFromStandardEnding()
        {
            if (owner == null || owner.IsBusy || ResolveEndingHudState(2) != 8 || lastStandardReturnFrame == Time.frameCount) return;
            lastStandardReturnFrame = Time.frameCount;
            owner.RequestExit();
        }

        /// <summary>取消键保持已完成结果；只有返回大厅按钮实际请求离场。</summary>
        public void CancelStandardEndingWindow()
        {
            if (owner != null && menuState == 8) Refresh();
        }
        private void CancelImmersionHudWindow()
        {
            if (lastSettingsCancelFrame == Time.frameCount) return;
            if (settingsOpen) CloseSettingsWindow();
            else if (menuState == 8) CancelStandardEndingWindow();
            else if (HasSaveUi) CancelSaveWindow();
            else if (HasTutorialUi) CancelTutorialWindow();
        }
    }
}
