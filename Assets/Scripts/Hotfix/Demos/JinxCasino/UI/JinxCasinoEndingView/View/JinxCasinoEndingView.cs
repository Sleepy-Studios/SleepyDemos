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
    [UIBind("JinxCasinoEndingView")]
    public sealed partial class JinxCasinoEndingView : View
    {
        private GameObject standardEndingPanel;

        private TMP_Text standardEndingTitleText;

        private TMP_Text standardEndingMessageText;

        private TMP_Text standardEndingStatsText;

        private TMP_Text standardEndingProfileText;

        private Button standardEndingSaveButton;

        private Button standardEndingReturnButton;

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
            standardEndingPanel = RectTransform_StandardEnding.gameObject;
            standardEndingTitleText = TextMeshProUGUI_Title;
            standardEndingMessageText = TextMeshProUGUI_Message;
            standardEndingStatsText = TextMeshProUGUI_Stats;
            standardEndingProfileText = TextMeshProUGUI_Profile;
            standardEndingSaveButton = Button_Save;
            standardEndingReturnButton = Button_ReturnToHub;
            BindData<JinxCasinoData>(OnData);
            var relay = UICancelRelay_JinxCasinoEndingView;
            Action cancel = () => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CancelWindow));
            relay.Canceled += cancel;
            AddBinding(() => relay.Canceled -= cancel);
            standardEndingSaveButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.OpenSaveWrite)));
            standardEndingReturnButton.onClick.AddListener(() => Return());
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
            var snapshot = owner.Data.Game.State;
            bool dignity = snapshot.Ending == CasinoAdventureEnding.LeaveWithDignity;
            bool withdraw = snapshot.Ending == CasinoAdventureEnding.Withdraw;
            standardEndingTitleText.text = dignity ? "见好就收 · 体面离场" : withdraw ? "狼狈撤离 · 下次再来" : "本次旅程已结束";
            standardEndingMessageText.text = dignity ? "你已通过本区核验，领取离场券。下次来，再试试新的选择。" : withdraw ? "你选择结束本次旅程。剩余筹码和本次记录如下。" : "本次旅程的结果已确定。";
            standardEndingStatsText.text = "剩余筹码  " + snapshot.Coins + "\n完成区域  " + snapshot.CompletedStages + " / " + snapshot.Config.StageCount + "\n已结算机台  " + snapshot.SettledRoundSequence + " 局";
            // 原宿主完成成长写盘后才更新此状态；UI 不登记、不推算声望奖励。
            standardEndingProfileText.text = owner.Data.Game.ProfileStatus ?? string.Empty;
            if (standardEndingSaveButton != null)
                standardEndingSaveButton.interactable = !owner.IsBusy;
            standardEndingReturnButton.interactable = !owner.IsBusy;
            var returnLabel = standardEndingReturnButton.GetComponentInChildren<TMP_Text>(true);
            if (returnLabel != null)
                returnLabel.text = owner.IsStandalonePlayer ? "返回主菜单" : "返回大厅";
            if (shownState != state)
            {
                shownState = state;
                owner.UI.SetFirstSelection(standardEndingReturnButton.gameObject);
            }
        }

        private void Return()
        {
            if (owner != null && !owner.IsBusy && owner.Data.Player.Exit.HasEnding && !owner.Data.Game.HasActiveRound && !owner.Data.Player.HasFocus)
                owner.RequestExit();
        }

        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
    }
}
