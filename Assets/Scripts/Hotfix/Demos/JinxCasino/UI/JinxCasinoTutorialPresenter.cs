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
    public sealed class JinxCasinoTutorialPresenter : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private Button tutorialCompleteButton;
        [SerializeField] private Button tutorialReadyBackButton;
        [SerializeField] private Button tutorialContinueButton;
        [SerializeField] private Button tutorialStandardButton;
        [SerializeField] private Button tutorialConfirmButton;
        [SerializeField] private Button tutorialCancelButton;
        [SerializeField] private GameObject tutorialReadyPanel;
        [SerializeField] private GameObject tutorialChoicePanel;
        [SerializeField] private GameObject tutorialConfirmPanel;
        [SerializeField] private TMP_Text tutorialChoiceMessageText;
        [SerializeField] private TMP_Text tutorialConfirmTitleText;
        [SerializeField] private TMP_Text tutorialConfirmMessageText;
        [SerializeField] private TMP_Text tutorialConfirmFeedbackText;
        private JinxCasinoController owner;
        private int shownState = -1;
        private void Awake()
        {

            tutorialCompleteButton.onClick.AddListener(() => owner?.UI.Tutorial.CompleteTeaching());
            tutorialReadyBackButton.onClick.AddListener(() => owner?.UI.Tutorial.DeferTeachingCompletion());
            tutorialContinueButton.onClick.AddListener(() => owner?.UI.Tutorial.ContinueTeachingPractice());
            tutorialStandardButton.onClick.AddListener(() => owner?.UI.Tutorial.RequestTeachingStandard());
            tutorialConfirmButton.onClick.AddListener(() => owner?.UI.Tutorial.ConfirmTeachingReplacement());
            tutorialCancelButton.onClick.AddListener(() => owner?.UI.Tutorial.CancelTutorialWindow());
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
            tutorialReadyPanel.SetActive(state == 3); tutorialChoicePanel.SetActive(state == 4); tutorialConfirmPanel.SetActive(state == 5);
            tutorialChoiceMessageText.text = owner.Player.Tutorial.Hint + (owner.Game.HasActiveRound ? "\n当前机台已投入，继续练习可以接着完成。" : string.Empty);
            bool restarting = owner.UI.Tutorial.Restarting;
            tutorialConfirmTitleText.text = restarting ? "重新开始教学？" : "开始正式冒险？";
            tutorialConfirmMessageText.text = restarting ? "会结束当前练习并重新开始互动教学。未保存的练习进度不会继续。" : "会结束当前练习并开始新的正式冒险，筹码重新计算。";
            if (owner.Game.HasActiveRound) tutorialConfirmMessageText.text += "\n当前已投入的机台也将结束。";
            tutorialConfirmFeedbackText.text = owner.UI.Tutorial.Feedback ?? string.Empty;
            foreach (var button in new[] { tutorialCompleteButton, tutorialReadyBackButton, tutorialContinueButton, tutorialStandardButton, tutorialConfirmButton, tutorialCancelButton }) button.interactable = !owner.IsBusy;
            if (shownState != state) { shownState = state; owner.UI.SetFirstSelection(state == 3 ? tutorialCompleteButton.gameObject : state == 4 ? tutorialContinueButton.gameObject : tutorialCancelButton.gameObject); }
        }
        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
        public void OnCancel(BaseEventData value) { value.Use(); owner?.UI.CancelImmersionHudWindow(); }
        private void OnDestroy() => Unbind();
    }
}
