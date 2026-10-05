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
    [UIBind("JinxCasinoPauseView")]
    public sealed partial class JinxCasinoPauseView : View
    {
        private GameObject pauseMenu;

        private Button resume;

        private Button leave;

        private Button tutorialSkipButton;

        private Button tutorialRetryButton;

        private Button tutorialReviewButton;

        private TMP_Text tutorialPauseFeedbackText;

        private Button savePauseSaveButton;

        private Button savePauseLoadButton;

        private Button settingsPauseButton;

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
            pauseMenu = RectTransform_PauseMenu.gameObject;
            resume = Button_Resume;
            leave = Button_Leave;
            tutorialSkipButton = Button_TutorialSkip;
            tutorialRetryButton = Button_TutorialRetry;
            tutorialReviewButton = Button_TutorialReview;
            tutorialPauseFeedbackText = TextMeshProUGUI_Hint;
            savePauseSaveButton = Button_SaveAdventure;
            savePauseLoadButton = Button_LoadAdventure;
            settingsPauseButton = Button_Settings;
            BindData<JinxCasinoData>(OnData);
            var relay = UICancelRelay_JinxCasinoPauseView;
            Action cancel = () => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CancelWindow));
            relay.Canceled += cancel;
            AddBinding(() => relay.Canceled -= cancel);
            resume.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.Resume)));
            leave.onClick.AddListener(() => owner?.RequestExit());
            savePauseSaveButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.OpenSaveWrite)));
            savePauseLoadButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.OpenSaveLoad)));
            settingsPauseButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.OpenSettings)));
            tutorialSkipButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.SkipTeaching)));
            tutorialReviewButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.ReopenTeachingChoice)));
            tutorialRetryButton.onClick.AddListener(() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.RetryTeaching)));
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
            bool active = owner.Data.Player.Tutorial.Status == CasinoTutorialStatus.Active;
            bool review = owner.Data.Player.Tutorial.Status == CasinoTutorialStatus.Completed || owner.Data.Player.Tutorial.Status == CasinoTutorialStatus.Skipped || active && owner.Data.Player.Tutorial.Step == CasinoTutorialStep.Ready;
            tutorialSkipButton.gameObject.SetActive(active && !review);
            tutorialReviewButton.gameObject.SetActive(review);
            tutorialRetryButton.gameObject.SetActive(owner.Data.Player.Tutorial.Status != CasinoTutorialStatus.None);
            tutorialReviewButton.GetComponentInChildren<TMP_Text>(true).text = active ? "完成教学" : "教学后选择";
            tutorialPauseFeedbackText.text = owner.Data.Tutorial.Feedback ?? "旅程已暂停，准备好再继续。";
            leave.GetComponentInChildren<TMP_Text>(true).text = owner.IsStandalonePlayer ? "返回主菜单" : "返回大厅";
            var rect = (RectTransform)pauseMenu.transform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, tutorialRetryButton.gameObject.activeSelf ? 700 : active || review ? 620 : 540);
            var buttons = new List<Button>
            {
                resume,
                savePauseSaveButton,
                savePauseLoadButton,
                leave,
                settingsPauseButton
            };
            if (tutorialSkipButton.gameObject.activeSelf)
                buttons.Add(tutorialSkipButton);
            if (review)
                buttons.Add(tutorialReviewButton);
            if (tutorialRetryButton.gameObject.activeSelf)
                buttons.Add(tutorialRetryButton);
            foreach (var button in buttons)
                button.interactable = !owner.IsBusy;
            int mask = (active ? 1 : 0) | (review ? 2 : 0) | (tutorialRetryButton.gameObject.activeSelf ? 4 : 0);
            if (mask != navigationMask)
            {
                navigationMask = mask;
                JinxCasinoMenuNavigation.SaveNavigation(buttons);
            }

            if (shownState != state)
            {
                shownState = state;
                owner.UI.SetFirstSelection(resume.gameObject);
            }
        }

        private int navigationMask = -1;

        /// <summary>接收公共取消事件并只退出当前窗口层。</summary>
        /// <param name="value">公共UI模块的取消事件。</param>
    }
}
