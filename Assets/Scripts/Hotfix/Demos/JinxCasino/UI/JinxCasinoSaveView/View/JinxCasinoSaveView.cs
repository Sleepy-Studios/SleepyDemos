using Hotfix.JinxCasino;
using Hotfix.JinxCasino.UI;
using System;
using System.Collections.Generic;
using Core.Runtime;
using Core.Runtime.Inputs;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix
{
    /// 页面持有控件和显示逻辑，业务命令通过 Flux 派发。
    [Module("JinxCasino")]
    [UIBind("JinxCasinoSaveView")]
    public sealed partial class JinxCasinoSaveView : View
    {
        private Button saveSlot1Button;

        private Button saveSlot2Button;

        private Button saveSlot3Button;

        private TMP_Text saveSlot1Text;

        private TMP_Text saveSlot2Text;

        private TMP_Text saveSlot3Text;

        private Button saveBackButton;

        private GameObject saveSlotsPanel;

        private TMP_Text saveTitleText;

        private TMP_Text saveFeedbackText;

        private GameObject saveConfirmPanel;

        private TMP_Text saveConfirmTitleText;

        private TMP_Text saveConfirmMessageText;

        private TMP_Text saveConfirmFeedbackText;

        private Button saveConfirmButton;

        private Button saveCancelButton;

        private JinxCasinoController owner;

        private void OnData(JinxCasinoData value)
        {
            owner = value.Scene;
            Refresh();
        }

        protected override void OnHide()
        {
            owner = null;
            base.OnHide();
        }

        private JinxCasinoPage shownState = JinxCasinoPage.None;

        protected override void OnGameObjectInitialize()
        {
            saveSlot1Button = Button_Slot1;
            saveSlot2Button = Button_Slot2;
            saveSlot3Button = Button_Slot3;
            saveSlot1Text = TextMeshProUGUI_Label;
            saveSlot2Text = TextMeshProUGUI_Label1;
            saveSlot3Text = TextMeshProUGUI_Label2;
            saveBackButton = Button_Back;
            saveSlotsPanel = RectTransform_SaveSlots.gameObject;
            saveTitleText = TextMeshProUGUI_Title;
            saveFeedbackText = TextMeshProUGUI_Feedback;
            saveConfirmPanel = RectTransform_SaveConfirm.gameObject;
            saveConfirmTitleText = TextMeshProUGUI_Title1;
            saveConfirmMessageText = TextMeshProUGUI_Message;
            saveConfirmFeedbackText = TextMeshProUGUI_Feedback1;
            saveConfirmButton = Button_Confirm;
            saveCancelButton = Button_Cancel;
            BindData<JinxCasinoData>(OnData);
            var relay = UICancelRelay_JinxCasinoSaveView;
            Action cancel = () => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CancelWindow));
            relay.Canceled += cancel;
            AddBinding(() => relay.Canceled -= cancel);
            saveSlot1Button.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.ChooseSaveSlot, 1)));
            saveSlot2Button.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.ChooseSaveSlot, 2)));
            saveSlot3Button.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.ChooseSaveSlot, 3)));
            saveBackButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CancelSaveWindow)));
            saveCancelButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CancelSaveWindow)));
            saveConfirmButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.ConfirmSaveOperation)));
        }

        /// <summary>显示前交付当前场景。</summary>
        /// <param name="controller">当前场景宿主。</param>
        public void SetData(JinxCasinoController controller)
        {
            owner = controller;
            shownState = JinxCasinoPage.None;
        }

        /// 隐藏时释放场景引用。
        private void Refresh()
        {
            if (owner == null)
                return;
            JinxCasinoPage state = owner.Data.Page;
            saveSlotsPanel.SetActive(state == JinxCasinoPage.SaveSlots);
            saveConfirmPanel.SetActive(state == JinxCasinoPage.SaveConfirm);
            bool writing = owner.Data.Save.Writing;
            int pending = owner.Data.Save.PendingSlot;
            saveTitleText.text = writing ? "选择保存位置" : "继续一段旅程";
            saveFeedbackText.text = owner.Data.Save.Feedback ?? (writing ? "保存包含已投入的机台、背包与教学进度。" : "选择存档继续；已投入的机台不会重新开奖。");
            var available = new List<Button>();
            for (int slot = 1; slot <= 3; slot++)
            {
                var button = SaveSlotButton(slot);
                button.interactable = !owner.IsBusy && owner.Data.Save.CanSelectSaveSlot(slot);
                SaveSlotText(slot).text = DescribeSaveInfo(owner.Data.Save.Info(slot));
                if (button.interactable)
                    available.Add(button);
            }

            available.Add(saveBackButton);
            JinxCasinoMenuNavigation.SaveNavigation(available);
            saveConfirmTitleText.text = writing ? "覆盖存档 " + pending + "？" : "读取存档 " + pending + "？";
            saveConfirmMessageText.text = writing ? "此槽将保存你当前的旅程，替换原来的进度。" : "会放弃当前旅程中尚未保存的进度，继续所选存档。";
            if (!writing && owner.Data.Game.HasActiveRound)
                saveConfirmMessageText.text += "\n当前已经投入的机台将由所选存档替换。";
            saveConfirmFeedbackText.text = owner.Data.Save.Feedback ?? string.Empty;
            saveConfirmButton.interactable = saveCancelButton.interactable = saveBackButton.interactable = !owner.IsBusy;
            if (shownState != state)
            {
                shownState = state;
                owner.UI.SetFirstSelection(First(state));
            }
        }

        private static string DescribeSaveInfo(CasinoSaveSlotInfo info)
        {
            if (info == null)
                return "无法读取";
            string title = "存档 " + info.Slot + "  ·  ";
            if (info.IsEmpty)
                return title + "空槽";
            if (!string.IsNullOrEmpty(info.Error))
                return title + "无法恢复\n保存可替换损坏的数据";
            string mode = info.Mode == "Practice" ? "自由练习" : info.Mode == "Endless" ? "无尽旅程" : "正式冒险";
            string date = TimeUtil.TryParseIso8601(info.SavedUtc, out var savedAt) ? TimeUtil.FormatTimestamp(savedAt.ToUnixTimeMilliseconds(), "MM-dd HH:mm") : "时间未知";
            return title + mode + "\n第 " + (info.StageIndex + 1) + (info.Mode == "Endless" ? " 轮" : " 区") + " · 筹码 " + info.Coins + " · " + date + (info.UsesBackup ? "\n将恢复上一个有效备份" : string.Empty);
        }

        private Button SaveSlotButton(int slot) => slot == 1 ? saveSlot1Button : slot == 2 ? saveSlot2Button : saveSlot3Button;

        private TMP_Text SaveSlotText(int slot) => slot == 1 ? saveSlot1Text : slot == 2 ? saveSlot2Text : saveSlot3Text;

        private GameObject First(JinxCasinoPage state)
        {
            if (state == JinxCasinoPage.SaveConfirm)
                return saveCancelButton.gameObject;
            for (int slot = 1; slot <= 3; slot++)
                if (owner.Data.Save.CanSelectSaveSlot(slot))
                    return SaveSlotButton(slot).gameObject;
            return saveBackButton.gameObject;
        }

        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
    }
}
