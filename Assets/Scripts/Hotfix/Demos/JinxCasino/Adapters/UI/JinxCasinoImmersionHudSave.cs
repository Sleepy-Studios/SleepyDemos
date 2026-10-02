using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Adapters.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    public sealed partial class JinxCasinoImmersionHudPresenter
    {
        [SerializeField] private Button saveMainLoadButton;
        [SerializeField] private Button savePauseSaveButton;
        [SerializeField] private Button savePauseLoadButton;
        [SerializeField] private Button saveSlot1Button;
        [SerializeField] private Button saveSlot2Button;
        [SerializeField] private Button saveSlot3Button;
        [SerializeField] private TMP_Text saveSlot1Text;
        [SerializeField] private TMP_Text saveSlot2Text;
        [SerializeField] private TMP_Text saveSlot3Text;
        [SerializeField] private Button saveBackButton;
        [SerializeField] private GameObject saveSlotsPanel;
        [SerializeField] private TMP_Text saveTitleText;
        [SerializeField] private TMP_Text saveFeedbackText;
        [SerializeField] private GameObject saveConfirmPanel;
        [SerializeField] private TMP_Text saveConfirmTitleText;
        [SerializeField] private TMP_Text saveConfirmMessageText;
        [SerializeField] private TMP_Text saveConfirmFeedbackText;
        [SerializeField] private Button saveConfirmButton;
        [SerializeField] private Button saveCancelButton;
        private readonly CasinoSaveSlotInfo[] saveInfos = new CasinoSaveSlotInfo[3];
        private bool saveBrowserOpen;
        private bool saveWriting;
        private int savePendingSlot;
        private string saveSourceRun;
        private string saveUiFeedback;
        private int lastSaveActionFrame = -1;
        private int lastSaveCancelFrame = -1;
        private int savePauseNavigationMask = -1;

        private bool HasSaveUi => saveMainLoadButton != null && saveSlotsPanel != null && saveConfirmPanel != null;
        private Button SaveSlotButton(int slot) => slot == 1 ? saveSlot1Button : slot == 2 ? saveSlot2Button : saveSlot3Button;
        private TMP_Text SaveSlotText(int slot) => slot == 1 ? saveSlot1Text : slot == 2 ? saveSlot2Text : saveSlot3Text;

        private void BindSaveControls()
        {
            saveBrowserOpen = false; savePendingSlot = 0; saveUiFeedback = null;
            lastSaveActionFrame = lastSaveCancelFrame = savePauseNavigationMask = -1;
            ListenTutorial(saveMainLoadButton, OpenSaveLoad); ListenTutorial(savePauseLoadButton, OpenSaveLoad);
            ListenTutorial(savePauseSaveButton, OpenSaveWrite); ListenTutorial(saveSlot1Button, ChooseSaveSlot1);
            ListenTutorial(saveSlot2Button, ChooseSaveSlot2); ListenTutorial(saveSlot3Button, ChooseSaveSlot3);
            ListenTutorial(saveBackButton, CancelSaveWindow); ListenTutorial(saveCancelButton, CancelSaveWindow);
            ListenTutorial(saveConfirmButton, ConfirmSaveOperation);
        }
        private void UnbindSaveControls()
        {
            UnlistenTutorial(saveMainLoadButton, OpenSaveLoad); UnlistenTutorial(savePauseLoadButton, OpenSaveLoad);
            UnlistenTutorial(savePauseSaveButton, OpenSaveWrite); UnlistenTutorial(saveSlot1Button, ChooseSaveSlot1);
            UnlistenTutorial(saveSlot2Button, ChooseSaveSlot2); UnlistenTutorial(saveSlot3Button, ChooseSaveSlot3);
            UnlistenTutorial(saveBackButton, CancelSaveWindow); UnlistenTutorial(saveCancelButton, CancelSaveWindow);
            UnlistenTutorial(saveConfirmButton, ConfirmSaveOperation);
            saveBrowserOpen = false; savePendingSlot = 0;
        }
        private void OpenSaveLoad() => OpenSaveBrowser(false);
        private void OpenSaveWrite() => OpenSaveBrowser(true);
        private void OpenSaveBrowser(bool writing)
        {
            if (owner == null || owner.IsBusy || !HasSaveUi || writing && !owner.HasAdventure) return;
            saveWriting = writing; saveSourceRun = owner.AdventureState?.RunId;
            saveBrowserOpen = true; savePendingSlot = 0; saveUiFeedback = null;
            ReloadSaveInfos(); Refresh();
        }
        private void ReloadSaveInfos()
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                try { saveInfos[slot - 1] = owner.GetSaveSlotInfo(slot); }
                catch (Exception) { saveInfos[slot - 1] = new CasinoSaveSlotInfo { Slot = slot, Error = "暂时无法读取此槽，请稍后再试。" }; }
            }
        }
        private int ResolveSaveHudState(int normalState) => HasSaveUi && saveBrowserOpen ? savePendingSlot > 0 ? 7 : 6 : normalState;
        private GameObject SaveFirstSelection(int state)
        {
            if (state == 8) return standardEndingReturnButton.gameObject;
            if (state == 7) return saveCancelButton.gameObject;
            if (state != 6) return TutorialFirstSelection(state);
            for (int slot = 1; slot <= 3; slot++) if (CanSelectSaveSlot(slot)) return SaveSlotButton(slot).gameObject;
            return saveBackButton.gameObject;
        }
        private bool CanSelectSaveSlot(int slot)
        {
            var info = saveInfos[slot - 1];
            return saveWriting || info != null && !info.IsEmpty && string.IsNullOrEmpty(info.Error);
        }
        private void RefreshSaveControls(int state)
        {
            if (!HasSaveUi) return;
            saveSlotsPanel.SetActive(state == 6); saveConfirmPanel.SetActive(state == 7);
            if (state == 6 || state == 7)
            {
                saveTitleText.text = saveWriting ? "选择保存位置" : "继续一段旅程";
                saveFeedbackText.text = saveUiFeedback ?? (saveWriting ? "保存包含已投入的机台、背包与教学进度。" : "选择存档继续；已投入的机台不会重新开奖。");
                var available = new List<Button>();
                for (int slot = 1; slot <= 3; slot++)
                {
                    var button = SaveSlotButton(slot); button.interactable = CanSelectSaveSlot(slot);
                    SaveSlotText(slot).text = DescribeSaveInfo(saveInfos[slot - 1]);
                    if (button.interactable) available.Add(button);
                }
                available.Add(saveBackButton); SaveNavigation(available);
                saveConfirmTitleText.text = saveWriting ? "覆盖存档 " + savePendingSlot + "？" : "读取存档 " + savePendingSlot + "？";
                saveConfirmMessageText.text = saveWriting ? "此槽将保存你当前的旅程，替换原来的进度。"
                    : "会放弃当前旅程中尚未保存的进度，继续所选存档。";
                if (!saveWriting && owner.HasActiveAdventureRound) saveConfirmMessageText.text += "\n当前已经投入的机台将由所选存档替换。";
                saveConfirmFeedbackText.text = saveUiFeedback ?? string.Empty;
            }
            RefreshSavePauseNavigation(state);
        }
        private void RefreshSavePauseNavigation(int state)
        {
            savePauseSaveButton.gameObject.SetActive(state == 1); savePauseLoadButton.gameObject.SetActive(state == 1);
            bool skip = HasTutorialUi && tutorialSkipButton.gameObject.activeSelf;
            bool review = HasTutorialUi && tutorialReviewButton.gameObject.activeSelf;
            bool retry = HasTutorialUi && tutorialRetryButton.gameObject.activeSelf;
            int mask = (state == 1 ? 1 : 0) | (skip ? 2 : 0) | (review ? 4 : 0) | (retry ? 8 : 0);
            if (state == 1)
            {
                var rect = (RectTransform)pauseMenu.transform;
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, retry ? 700 : skip || review ? 620 : 540);
            }
            if (savePauseNavigationMask == mask) return;
            savePauseNavigationMask = mask;
            var buttons = new List<Button> { resume, savePauseSaveButton, savePauseLoadButton, leave };
            if (skip) buttons.Add(tutorialSkipButton); if (review) buttons.Add(tutorialReviewButton); if (retry) buttons.Add(tutorialRetryButton);
            SaveNavigation(buttons);
        }
        private static void SaveNavigation(IList<Button> buttons)
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                var navigation = buttons[i].navigation; navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = navigation.selectOnLeft = buttons[(i + buttons.Count - 1) % buttons.Count];
                navigation.selectOnDown = navigation.selectOnRight = buttons[(i + 1) % buttons.Count]; buttons[i].navigation = navigation;
            }
        }
        private static string DescribeSaveInfo(CasinoSaveSlotInfo info)
        {
            if (info == null) return "无法读取";
            string title = "存档 " + info.Slot + "  ·  ";
            if (info.IsEmpty) return title + "空槽";
            if (!string.IsNullOrEmpty(info.Error)) return title + "无法恢复\n保存可替换损坏的数据";
            string mode = info.Mode == "Practice" ? "自由练习" : info.Mode == "Endless" ? "无尽旅程" : "正式冒险";
            string date = DateTime.TryParse(info.SavedUtc, out var savedAt) ? savedAt.ToLocalTime().ToString("MM-dd HH:mm") : "时间未知";
            return title + mode + "\n第 " + (info.StageIndex + 1) + (info.Mode == "Endless" ? " 轮" : " 区") + " · 筹码 " + info.Coins + " · " + date + (info.UsesBackup ? "\n将恢复上一个有效备份" : string.Empty);
        }
        private void ChooseSaveSlot1() => ChooseSaveSlot(1);
        private void ChooseSaveSlot2() => ChooseSaveSlot(2);
        private void ChooseSaveSlot3() => ChooseSaveSlot(3);
        private void ChooseSaveSlot(int slot)
        {
            if (!saveBrowserOpen || savePendingSlot > 0 || owner.IsBusy || !CanSelectSaveSlot(slot)) return;
            ReloadSaveInfos();
            if (!CanSelectSaveSlot(slot)) { saveUiFeedback = "此槽暂时无法恢复，请选择另一个存档。"; Refresh(); return; }
            var info = saveInfos[slot - 1]; saveUiFeedback = null;
            if (saveWriting && !info.IsEmpty || !saveWriting && owner.HasAdventure) { savePendingSlot = slot; Refresh(); }
            else ExecuteSaveOperation(slot);
        }
        private void ConfirmSaveOperation() { if (savePendingSlot > 0) ExecuteSaveOperation(savePendingSlot); }
        private void ExecuteSaveOperation(int slot)
        {
            if (owner.IsBusy || lastSaveActionFrame == Time.frameCount) return;
            lastSaveActionFrame = Time.frameCount;
            if (saveSourceRun != owner.AdventureState?.RunId)
            { saveUiFeedback = "当前旅程已改变，请返回后重新选择。"; Refresh(); return; }
            bool success = saveWriting ? owner.SaveAdventure(slot) : owner.LoadAdventure(slot);
            saveUiFeedback = owner.AdventureStatus;
            if (!success) { Refresh(); return; }
            savePendingSlot = 0;
            if (saveWriting)
            { ReloadSaveInfos(); saveSourceRun = owner.AdventureState?.RunId; Refresh(); }
            else
            {
                saveBrowserOpen = false;
                if (owner.IsImmersionPaused && !owner.ResumeImmersion()) tutorialUiFeedback = "请先回到游戏窗口或接回手柄，再点击继续。";
                Refresh();
            }
        }

        /// <summary>退回存档列表或打开它的菜单，不提交保存、不自动解除暂停。</summary>
        public void CancelSaveWindow()
        {
            if (owner == null || lastSaveCancelFrame == Time.frameCount) return;
            lastSaveCancelFrame = Time.frameCount;
            if (!saveBrowserOpen) { CancelTutorialWindow(); return; }
            if (savePendingSlot > 0) savePendingSlot = 0;
            else saveBrowserOpen = false;
            saveUiFeedback = null; Refresh();
        }
    }
}
