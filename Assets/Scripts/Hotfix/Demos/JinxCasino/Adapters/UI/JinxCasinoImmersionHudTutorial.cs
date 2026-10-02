using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    public sealed partial class JinxCasinoImmersionHudPresenter
    {
        [SerializeField] private Button tutorialStartButton;
        [SerializeField] private Button tutorialSkipButton;
        [SerializeField] private Button tutorialRetryButton;
        [SerializeField] private Button tutorialReviewButton;
        [SerializeField] private Button tutorialCompleteButton;
        [SerializeField] private Button tutorialReadyBackButton;
        [SerializeField] private Button tutorialContinueButton;
        [SerializeField] private Button tutorialStandardButton;
        [SerializeField] private Button tutorialConfirmButton;
        [SerializeField] private Button tutorialCancelButton;
        [SerializeField] private GameObject tutorialStrip;
        [SerializeField] private TMP_Text tutorialHintText;
        [SerializeField] private TMP_Text tutorialDirectionText;
        [SerializeField] private TMP_Text tutorialFeedbackText;
        [SerializeField] private GameObject tutorialReadyPanel;
        [SerializeField] private GameObject tutorialChoicePanel;
        [SerializeField] private GameObject tutorialConfirmPanel;
        [SerializeField] private TMP_Text tutorialChoiceMessageText;
        [SerializeField] private TMP_Text tutorialConfirmTitleText;
        [SerializeField] private TMP_Text tutorialConfirmMessageText;
        [SerializeField] private TMP_Text tutorialConfirmFeedbackText;
        [SerializeField] private TMP_Text tutorialMainFeedbackText;
        [SerializeField] private TMP_Text tutorialPauseFeedbackText;
        private TutorialConfirmation tutorialConfirmation;
        private int lastTutorialCancelFrame = -1;
        private string tutorialDismissedRun;
        private string tutorialReadyDeferredRun;
        private string tutorialUiFeedback;
        private int tutorialPauseNavigationMask = -1;

        private enum TutorialConfirmation { None, NewStandard, RestartTutorial }
        private string TutorialRun => owner.AdventureState?.RunId;
        private bool HasTutorialUi => tutorialStartButton != null && tutorialReadyPanel != null && tutorialChoicePanel != null && tutorialConfirmPanel != null;

        private void BindTutorialControls()
        {
            lastTutorialCancelFrame = -1;
            tutorialConfirmation = TutorialConfirmation.None; tutorialDismissedRun = tutorialReadyDeferredRun = tutorialUiFeedback = null;
            ListenTutorial(tutorialStartButton, StartTeaching); ListenTutorial(tutorialSkipButton, SkipTeaching); ListenTutorial(tutorialRetryButton, RetryTeaching);
            ListenTutorial(tutorialReviewButton, ReopenTeachingChoice); tutorialPauseNavigationMask = -1;
            ListenTutorial(tutorialCompleteButton, CompleteTeaching); ListenTutorial(tutorialReadyBackButton, DeferTeachingCompletion);
            ListenTutorial(tutorialContinueButton, ContinueTeachingPractice); ListenTutorial(tutorialStandardButton, RequestTeachingStandard);
            ListenTutorial(tutorialConfirmButton, ConfirmTeachingReplacement); ListenTutorial(tutorialCancelButton, CancelTutorialWindow);
        }
        private void UnbindTutorialControls()
        {
            UnlistenTutorial(tutorialStartButton, StartTeaching); UnlistenTutorial(tutorialSkipButton, SkipTeaching); UnlistenTutorial(tutorialRetryButton, RetryTeaching);
            UnlistenTutorial(tutorialReviewButton, ReopenTeachingChoice);
            UnlistenTutorial(tutorialCompleteButton, CompleteTeaching); UnlistenTutorial(tutorialReadyBackButton, DeferTeachingCompletion);
            UnlistenTutorial(tutorialContinueButton, ContinueTeachingPractice); UnlistenTutorial(tutorialStandardButton, RequestTeachingStandard);
            UnlistenTutorial(tutorialConfirmButton, ConfirmTeachingReplacement); UnlistenTutorial(tutorialCancelButton, CancelTutorialWindow);
            tutorialConfirmation = TutorialConfirmation.None;
        }
        private static void ListenTutorial(Button button, UnityAction action) { if (button != null) button.onClick.AddListener(action); }
        private static void UnlistenTutorial(Button button, UnityAction action) { if (button != null) button.onClick.RemoveListener(action); }

        // Ready/结束选择只在探索或玩家主动暂停时获得Core菜单导航，不能抢具体机台/退出过渡。
        private int ResolveTutorialHudState()
        {
            if (!owner.HasAdventure) return 0;
            if (HasTutorialUi)
            {
                if (tutorialConfirmation != TutorialConfirmation.None) return 5;
                bool canChoose = owner.IsImmersionPaused || !owner.HasImmersionFocus && !owner.HasActiveAdventureRound;
                if (canChoose && tutorialDismissedRun != TutorialRun && (owner.TutorialStatus == CasinoTutorialStatus.Completed || owner.TutorialStatus == CasinoTutorialStatus.Skipped)) return 4;
                if (canChoose && tutorialReadyDeferredRun != TutorialRun && owner.TutorialStatus == CasinoTutorialStatus.Active && owner.TutorialStep == CasinoTutorialStep.Ready) return 3;
            }
            return owner.IsImmersionPaused ? 1 : 2;
        }
        private GameObject TutorialFirstSelection(int state)
            => state == 0 ? start.gameObject : state == 1 ? resume.gameObject : state == 3 ? tutorialCompleteButton.gameObject : state == 4 ? tutorialContinueButton.gameObject : state == 5 ? tutorialCancelButton.gameObject : null;

        private void RefreshTutorialControls(int state)
        {
            if (!HasTutorialUi) return;
            bool active = owner.TutorialStatus == CasinoTutorialStatus.Active;
            tutorialReadyPanel.SetActive(state == 3); tutorialChoicePanel.SetActive(state == 4); tutorialConfirmPanel.SetActive(state == 5);
            tutorialStrip.SetActive(state == 2 && active);
            bool reviewable = owner.TutorialStatus == CasinoTutorialStatus.Completed || owner.TutorialStatus == CasinoTutorialStatus.Skipped || active && owner.TutorialStep == CasinoTutorialStep.Ready;
            tutorialSkipButton.gameObject.SetActive(state == 1 && active && !reviewable);
            tutorialReviewButton.gameObject.SetActive(state == 1 && reviewable);
            tutorialReviewButton.GetComponentInChildren<TMP_Text>(true).text = active ? "完成教学" : "教学后选择";
            tutorialRetryButton.gameObject.SetActive(state == 1 && owner.TutorialStatus != CasinoTutorialStatus.None);
            RefreshTeachingPauseNavigation();
            var pauseRect = (RectTransform)pauseMenu.transform;
            pauseRect.sizeDelta = new Vector2(pauseRect.sizeDelta.x, owner.TutorialStatus != CasinoTutorialStatus.None ? 680 : 510);
            tutorialHintText.text = owner.TutorialHint;
            tutorialDirectionText.text = owner.TutorialDirection;
            tutorialFeedbackText.text = !string.IsNullOrEmpty(owner.TutorialFeedback) ? owner.TutorialFeedback : tutorialUiFeedback ?? string.Empty;
            var stripRect = (RectTransform)tutorialStrip.transform;
            float stripHeight = !string.IsNullOrEmpty(tutorialFeedbackText.text) ? 156 : !string.IsNullOrEmpty(tutorialDirectionText.text) ? 112 : 82;
            stripRect.sizeDelta = new Vector2(stripRect.sizeDelta.x, stripHeight);
            tutorialMainFeedbackText.text = tutorialUiFeedback ?? "水果维修  /  发条牌桌  /  合拍拉杆";
            tutorialPauseFeedbackText.text = tutorialUiFeedback ?? "旅程已暂停，准备好再继续。";
            tutorialChoiceMessageText.text = owner.TutorialHint + (owner.HasActiveAdventureRound ? "\n当前机台已投入，继续练习可以接着完成。" : string.Empty);
            bool restarting = tutorialConfirmation == TutorialConfirmation.RestartTutorial;
            tutorialConfirmTitleText.text = restarting ? "重新开始教学？" : "开始正式冒险？";
            tutorialConfirmMessageText.text = restarting ? "会结束当前练习并重新开始互动教学。未保存的练习进度不会继续。" : "会结束当前练习并开始新的正式冒险，筹码重新计算。";
            if (owner.HasActiveAdventureRound) tutorialConfirmMessageText.text += "\n当前已投入的机台也将结束。";
            tutorialConfirmFeedbackText.text = tutorialUiFeedback ?? string.Empty;
        }

        private void StartTeaching() { RecordTeachingResult(owner.StartTutorialAdventure()); Refresh(); }
        private void SkipTeaching() { RecordTeachingResult(owner.SkipTutorial()); Refresh(); }
        private void CompleteTeaching() { RecordTeachingResult(owner.CompleteTutorial()); Refresh(); }
        private void RetryTeaching() { tutorialUiFeedback = null; tutorialConfirmation = TutorialConfirmation.RestartTutorial; Refresh(); }
        private void RequestTeachingStandard() { tutorialUiFeedback = null; tutorialConfirmation = TutorialConfirmation.NewStandard; Refresh(); }
        private void DeferTeachingCompletion()
        { tutorialReadyDeferredRun = TutorialRun; tutorialUiFeedback = null; Refresh(); }
        private void ContinueTeachingPractice()
        { tutorialDismissedRun = TutorialRun; ResumeTeachingView(); }
        private void ReopenTeachingChoice()
        { tutorialReadyDeferredRun = tutorialDismissedRun = null; tutorialUiFeedback = null; Refresh(); }
        private void RefreshTeachingPauseNavigation()
        {
            int mask = (tutorialSkipButton.gameObject.activeSelf ? 1 : 0) | (tutorialReviewButton.gameObject.activeSelf ? 2 : 0) | (tutorialRetryButton.gameObject.activeSelf ? 4 : 0) |
                (settingsPauseButton != null && settingsPauseButton.gameObject.activeSelf ? 8 : 0);
            if (mask == tutorialPauseNavigationMask) return;
            tutorialPauseNavigationMask = mask;
            var buttons = new System.Collections.Generic.List<Button> { resume, leave };
            if ((mask & 8) != 0) buttons.Add(settingsPauseButton);
            if ((mask & 1) != 0) buttons.Add(tutorialSkipButton);
            if ((mask & 2) != 0) buttons.Add(tutorialReviewButton);
            if ((mask & 4) != 0) buttons.Add(tutorialRetryButton);
            for (int i = 0; i < buttons.Count; i++)
            {
                var navigation = buttons[i].navigation; navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count]; navigation.selectOnDown = buttons[(i + 1) % buttons.Count];
                navigation.selectOnLeft = navigation.selectOnRight = null; buttons[i].navigation = navigation;
            }
        }
        private void ResumeTeachingView()
        {
            tutorialUiFeedback = null;
            if (owner.IsImmersionPaused && !owner.ResumeImmersion()) tutorialUiFeedback = "请先回到游戏窗口或接回手柄，再点击继续。";
            Refresh();
        }
        private void ConfirmTeachingReplacement()
        {
            if (tutorialConfirmation == TutorialConfirmation.RestartTutorial)
            {
                var result = owner.StartTutorialAdventure(replaceCurrentRun: true);
                RecordTeachingResult(result);
                if (!result.Success) { Refresh(); return; }
            }
            else if (tutorialConfirmation == TutorialConfirmation.NewStandard)
            {
                string previousRun = TutorialRun;
                owner.StartAdventure(CasinoAdventureMode.Standard);
                if (TutorialRun == previousRun) { tutorialUiFeedback = "暂时不能开始新冒险，请先完成当前操作。"; Refresh(); return; }
            }
            else return;
            tutorialConfirmation = TutorialConfirmation.None; tutorialDismissedRun = tutorialReadyDeferredRun = null;
            ResumeTeachingView();
        }
        private void RecordTeachingResult(CasinoAdventureResult result)
        { tutorialUiFeedback = result.Success ? null : result.Description ?? "暂时不能执行，请先完成当前操作。"; }

        /// 保存的取消事件只退当前选择，不提交资金命令或自动开始新局。
        public void CancelTutorialWindow()
        {
            if (owner == null || lastTutorialCancelFrame == Time.frameCount) return;
            // Core Cancel与手柄Menu/Pause同帧触发时，只处理一次当前菜单返回。
            lastTutorialCancelFrame = Time.frameCount;
            if (tutorialConfirmation != TutorialConfirmation.None) { tutorialConfirmation = TutorialConfirmation.None; tutorialUiFeedback = null; Refresh(); }
            else if (menuState == 3) DeferTeachingCompletion();
            else if (menuState == 4) { tutorialDismissedRun = TutorialRun; tutorialUiFeedback = null; Refresh(); }
            else if (menuState == 1) { owner.ResumeImmersion(); Refresh(); }
        }
    }
}
