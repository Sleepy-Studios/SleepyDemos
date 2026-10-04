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
    public sealed class JinxCasinoPausePresenter : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private GameObject pauseMenu;
        [SerializeField] private Button resume;
        [SerializeField] private Button leave;
        [SerializeField] private Button tutorialSkipButton;
        [SerializeField] private Button tutorialRetryButton;
        [SerializeField] private Button tutorialReviewButton;
        [SerializeField] private TMP_Text tutorialPauseFeedbackText;
        [SerializeField] private Button savePauseSaveButton;
        [SerializeField] private Button savePauseLoadButton;
        [SerializeField] private Button settingsPauseButton;
        private JinxCasinoController owner;
        private int shownState = -1;
        private void Awake()
        {

            resume.onClick.AddListener(() => owner?.Player.Resume());
            leave.onClick.AddListener(() => owner?.RequestExit());
            savePauseSaveButton.onClick.AddListener(() => owner?.UI.OpenSaveWrite());
            savePauseLoadButton.onClick.AddListener(() => owner?.UI.OpenSaveLoad());
            settingsPauseButton.onClick.AddListener(() => owner?.UI.OpenSettings());
            tutorialSkipButton.onClick.AddListener(() => owner?.UI.Tutorial.SkipTeaching());
            tutorialReviewButton.onClick.AddListener(() => owner?.UI.Tutorial.ReopenTeachingChoice());
            tutorialRetryButton.onClick.AddListener(() => owner?.UI.Tutorial.RetryTeaching());
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
            bool active = owner.Player.Tutorial.Status == CasinoTutorialStatus.Active;
            bool review = owner.Player.Tutorial.Status == CasinoTutorialStatus.Completed || owner.Player.Tutorial.Status == CasinoTutorialStatus.Skipped || active && owner.Player.Tutorial.Step == CasinoTutorialStep.Ready;
            tutorialSkipButton.gameObject.SetActive(active && !review);
            tutorialReviewButton.gameObject.SetActive(review);
            tutorialRetryButton.gameObject.SetActive(owner.Player.Tutorial.Status != CasinoTutorialStatus.None);
            tutorialReviewButton.GetComponentInChildren<TMP_Text>(true).text = active ? "完成教学" : "教学后选择";
            tutorialPauseFeedbackText.text = owner.UI.Tutorial.Feedback ?? "旅程已暂停，准备好再继续。";
            leave.GetComponentInChildren<TMP_Text>(true).text = owner.IsStandalonePlayer ? "返回主菜单" : "返回大厅";
            var rect = (RectTransform)pauseMenu.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, tutorialRetryButton.gameObject.activeSelf ? 700 : active || review ? 620 : 540);
            var buttons = new List<Button> { resume, savePauseSaveButton, savePauseLoadButton, leave, settingsPauseButton };
            if (tutorialSkipButton.gameObject.activeSelf) buttons.Add(tutorialSkipButton);
            if (review) buttons.Add(tutorialReviewButton);
            if (tutorialRetryButton.gameObject.activeSelf) buttons.Add(tutorialRetryButton);
            foreach (var button in buttons) button.interactable = !owner.IsBusy;
            int mask = (active ? 1 : 0) | (review ? 2 : 0) | (tutorialRetryButton.gameObject.activeSelf ? 4 : 0);
            if (mask != navigationMask) { navigationMask = mask; JinxCasinoMenuNavigation.SaveNavigation(buttons); }
            if (shownState != state) { shownState = state; owner.UI.SetFirstSelection(resume.gameObject); }
        }
        private int navigationMask = -1;
        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
        public void OnCancel(BaseEventData value) { value.Use(); owner?.UI.CancelImmersionHudWindow(); }
        private void OnDestroy() => Unbind();
    }
}
