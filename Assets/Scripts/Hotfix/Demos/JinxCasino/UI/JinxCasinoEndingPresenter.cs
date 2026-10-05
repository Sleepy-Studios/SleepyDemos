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
    /// 独立窗口的显示与业务命令绑定，由对应 UIBind View 持有生命周期。
    public sealed class JinxCasinoEndingPresenter : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private GameObject standardEndingPanel;
        [SerializeField] private TMP_Text standardEndingTitleText;
        [SerializeField] private TMP_Text standardEndingMessageText;
        [SerializeField] private TMP_Text standardEndingStatsText;
        [SerializeField] private TMP_Text standardEndingProfileText;
        [SerializeField] private Button standardEndingSaveButton;
        [SerializeField] private Button standardEndingReturnButton;
        private JinxCasinoController owner;
        private int shownState = -1;
        private void Awake()
        {

            standardEndingSaveButton.onClick.AddListener(() => owner?.UI.OpenSaveWrite());
            standardEndingReturnButton.onClick.AddListener(() => Return());
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
            var snapshot = owner.Game.State;
            bool dignity = snapshot.Ending == CasinoAdventureEnding.LeaveWithDignity;
            bool withdraw = snapshot.Ending == CasinoAdventureEnding.Withdraw;
            standardEndingTitleText.text = dignity ? "见好就收 · 体面离场" : withdraw ? "狼狈撤离 · 下次再来" : "本次旅程已结束";
            standardEndingMessageText.text = dignity ? "你已通过本区核验，领取离场券。下次来，再试试新的选择。"
                : withdraw ? "你选择结束本次旅程。剩余筹码和本次记录如下。" : "本次旅程的结果已确定。";
            standardEndingStatsText.text = "剩余筹码  " + snapshot.Coins + "\n完成区域  " + snapshot.CompletedStages + " / " + snapshot.Config.StageCount +
                "\n已结算机台  " + snapshot.SettledRoundSequence + " 局";
            // 原宿主完成成长写盘后才更新此状态；UI 不登记、不推算声望奖励。
            standardEndingProfileText.text = owner.Game.ProfileStatus ?? string.Empty;
            if (standardEndingSaveButton != null) standardEndingSaveButton.interactable = !owner.IsBusy;
            standardEndingReturnButton.interactable = !owner.IsBusy;
            var returnLabel = standardEndingReturnButton.GetComponentInChildren<TMP_Text>(true);
            if (returnLabel != null) returnLabel.text = owner.IsStandalonePlayer ? "返回主菜单" : "返回大厅";

            if (shownState != state) { shownState = state; owner.UI.SetFirstSelection(standardEndingReturnButton.gameObject); }
        }
        private void Return() { if (owner != null && !owner.IsBusy && owner.Player.Exit.HasEnding && !owner.Game.HasActiveRound && !owner.Player.HasFocus) owner.RequestExit(); }
        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
        public void OnCancel(BaseEventData value) { value.Use(); owner?.UI.CancelImmersionHudWindow(); }
        private void OnDestroy() => Unbind();
    }
}
