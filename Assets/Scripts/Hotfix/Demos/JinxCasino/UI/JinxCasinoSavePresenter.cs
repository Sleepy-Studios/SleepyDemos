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
namespace Hotfix.JinxCasino.UI
{
    /// 独立窗口的显示与业务命令绑定，由对应 MvcBind View 持有生命周期。
    public sealed class JinxCasinoSavePresenter : MonoBehaviour, ICancelHandler
    {
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
        private JinxCasinoController owner;
        private int shownState = -1;
        private void Awake()
        {

            saveSlot1Button.onClick.AddListener(() => owner?.UI.Save.ChooseSaveSlot(1));
            saveSlot2Button.onClick.AddListener(() => owner?.UI.Save.ChooseSaveSlot(2));
            saveSlot3Button.onClick.AddListener(() => owner?.UI.Save.ChooseSaveSlot(3));
            saveBackButton.onClick.AddListener(() => owner?.UI.Save.CancelSaveWindow());
            saveCancelButton.onClick.AddListener(() => owner?.UI.Save.CancelSaveWindow());
            saveConfirmButton.onClick.AddListener(() => owner?.UI.Save.ConfirmSaveOperation());
        }
        /// <summary>显示前交付当前场景。</summary>
        /// <param name="controller">当前场景宿主。</param>
        public void Bind(JinxCasinoController controller) { Unbind(); owner = controller; shownState = -1; Refresh(); }
        /// 隐藏时释放场景引用。
        public void Unbind() => owner = null;
        private void Update() { if (owner != null) Refresh(); }
        private void Refresh()
        {
            if (owner == null) return;
            int state = owner.UI.State;
            saveSlotsPanel.SetActive(state == 6); saveConfirmPanel.SetActive(state == 7);
            bool writing = owner.UI.Save.Writing; int pending = owner.UI.Save.PendingSlot;
            saveTitleText.text = writing ? "选择保存位置" : "继续一段旅程";
            saveFeedbackText.text = owner.UI.Save.Feedback ?? (writing ? "保存包含已投入的机台、背包与教学进度。" : "选择存档继续；已投入的机台不会重新开奖。");
            var available = new List<Button>();
            for (int slot = 1; slot <= 3; slot++)
            {
                var button = SaveSlotButton(slot); button.interactable = !owner.IsBusy && owner.UI.Save.CanSelectSaveSlot(slot);
                SaveSlotText(slot).text = DescribeSaveInfo(owner.UI.Save.Info(slot)); if (button.interactable) available.Add(button);
            }
            available.Add(saveBackButton); JinxCasinoMenuNavigation.SaveNavigation(available);
            saveConfirmTitleText.text = writing ? "覆盖存档 " + pending + "？" : "读取存档 " + pending + "？";
            saveConfirmMessageText.text = writing ? "此槽将保存你当前的旅程，替换原来的进度。" : "会放弃当前旅程中尚未保存的进度，继续所选存档。";
            if (!writing && owner.Game.HasActiveRound) saveConfirmMessageText.text += "\n当前已经投入的机台将由所选存档替换。";
            saveConfirmFeedbackText.text = owner.UI.Save.Feedback ?? string.Empty;
            saveConfirmButton.interactable = saveCancelButton.interactable = saveBackButton.interactable = !owner.IsBusy;
            if (shownState != state) { shownState = state; owner.UI.SetFirstSelection(First(state)); }
        }
        private static string DescribeSaveInfo(CasinoSaveSlotInfo info)
        {
            if (info == null) return "无法读取";
            string title = "存档 " + info.Slot + "  ·  ";
            if (info.IsEmpty) return title + "空槽";
            if (!string.IsNullOrEmpty(info.Error)) return title + "无法恢复\n保存可替换损坏的数据";
            string mode = info.Mode == "Practice" ? "自由练习" : info.Mode == "Endless" ? "无尽旅程" : "正式冒险";
            string date = TimeUtil.TryParseIso8601(info.SavedUtc, out var savedAt)
                ? TimeUtil.FormatTimestamp(savedAt.ToUnixTimeMilliseconds(), "MM-dd HH:mm") : "时间未知";
            return title + mode + "\n第 " + (info.StageIndex + 1) + (info.Mode == "Endless" ? " 轮" : " 区") + " · 筹码 " + info.Coins + " · " + date + (info.UsesBackup ? "\n将恢复上一个有效备份" : string.Empty);
        }
        private Button SaveSlotButton(int slot) => slot == 1 ? saveSlot1Button : slot == 2 ? saveSlot2Button : saveSlot3Button;
        private TMP_Text SaveSlotText(int slot) => slot == 1 ? saveSlot1Text : slot == 2 ? saveSlot2Text : saveSlot3Text;
        private GameObject First(int state)
        {
            if (state == 7) return saveCancelButton.gameObject;
            for (int slot = 1; slot <= 3; slot++) if (owner.UI.Save.CanSelectSaveSlot(slot)) return SaveSlotButton(slot).gameObject;
            return saveBackButton.gameObject;
        }
        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
        public void OnCancel(BaseEventData value) { value.Use(); owner?.UI.CancelImmersionHudWindow(); }
        private void OnDestroy() => Unbind();
    }
}
