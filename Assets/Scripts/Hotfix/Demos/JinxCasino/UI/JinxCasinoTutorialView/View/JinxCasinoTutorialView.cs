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
    [UIBind("JinxCasinoTutorialView")]
    public sealed partial class JinxCasinoTutorialView : View
    {
        private Button tutorialCompleteButton;
        private Button tutorialReadyBackButton;
        private Button tutorialContinueButton;
        private Button tutorialStandardButton;
        private Button tutorialConfirmButton;
        private Button tutorialCancelButton;
        private GameObject tutorialReadyPanel;
        private GameObject tutorialChoicePanel;
        private GameObject tutorialConfirmPanel;
        private TMP_Text tutorialChoiceMessageText;
        private TMP_Text tutorialConfirmTitleText;
        private TMP_Text tutorialConfirmMessageText;
        private TMP_Text tutorialConfirmFeedbackText;
        private JinxCasinoController owner;
        private void OnData(JinxCasinoData value) { owner = value.Scene; Refresh(); }
        protected override void OnHide() { owner = null; base.OnHide(); }
        private JinxCasinoPage shownState = JinxCasinoPage.None;
        protected override void OnGameObjectInitialize()
        {
            tutorialCompleteButton = Button_Complete;
            tutorialReadyBackButton = Button_Back;
            tutorialContinueButton = Button_Continue;
            tutorialStandardButton = Button_Standard;
            tutorialConfirmButton = Button_Confirm;
            tutorialCancelButton = Button_Cancel;
            tutorialReadyPanel = RectTransform_TutorialReady.gameObject;
            tutorialChoicePanel = RectTransform_TutorialChoice.gameObject;
            tutorialConfirmPanel = RectTransform_TutorialConfirm.gameObject;
            tutorialChoiceMessageText = TextMeshProUGUI_Hint;
            tutorialConfirmTitleText = TextMeshProUGUI_Title;
            tutorialConfirmMessageText = TextMeshProUGUI_Hint1;
            tutorialConfirmFeedbackText = TextMeshProUGUI_Feedback;
            BindData<JinxCasinoData>(OnData);
            var relay = UICancelRelay_JinxCasinoTutorialView;
            Action cancel = () => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CancelWindow));
            relay.Canceled += cancel; AddBinding(() => relay.Canceled -= cancel);

            tutorialCompleteButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CompleteTeaching)));
            tutorialReadyBackButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.DeferTeachingCompletion)));
            tutorialContinueButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.ContinueTeachingPractice)));
            tutorialStandardButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.RequestTeachingStandard)));
            tutorialConfirmButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.ConfirmTeachingReplacement)));
            tutorialCancelButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CancelTutorialWindow)));
        }
        /// <summary>显示前交付当前场景。</summary>
        /// <param name="controller">当前场景宿主。</param>
        public void SetData(JinxCasinoController controller) { owner = controller; shownState = JinxCasinoPage.None; }
        /// 隐藏时释放场景引用。


        private void Refresh()
        {
            if (owner == null) return;
            JinxCasinoPage state = owner.Data.Page;
            tutorialReadyPanel.SetActive(state == JinxCasinoPage.TutorialReady); tutorialChoicePanel.SetActive(state == JinxCasinoPage.TutorialChoice); tutorialConfirmPanel.SetActive(state == JinxCasinoPage.TutorialConfirm);
            tutorialChoiceMessageText.text = owner.Player.Tutorial.Hint + (owner.Game.HasActiveRound ? "\n当前机台已投入，继续练习可以接着完成。" : string.Empty);
            bool restarting = owner.UI.Tutorial.Restarting;
            tutorialConfirmTitleText.text = restarting ? "重新开始教学？" : "开始正式冒险？";
            tutorialConfirmMessageText.text = restarting ? "会结束当前练习并重新开始互动教学。未保存的练习进度不会继续。" : "会结束当前练习并开始新的正式冒险，筹码重新计算。";
            if (owner.Game.HasActiveRound) tutorialConfirmMessageText.text += "\n当前已投入的机台也将结束。";
            tutorialConfirmFeedbackText.text = owner.UI.Tutorial.Feedback ?? string.Empty;
            foreach (var button in new[] { tutorialCompleteButton, tutorialReadyBackButton, tutorialContinueButton, tutorialStandardButton, tutorialConfirmButton, tutorialCancelButton }) button.interactable = !owner.IsBusy;
            if (shownState != state) { shownState = state; owner.UI.SetFirstSelection(state == JinxCasinoPage.TutorialReady ? tutorialCompleteButton.gameObject : state == JinxCasinoPage.TutorialChoice ? tutorialContinueButton.gameObject : tutorialCancelButton.gameObject); }
        }
        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>


    }
}
