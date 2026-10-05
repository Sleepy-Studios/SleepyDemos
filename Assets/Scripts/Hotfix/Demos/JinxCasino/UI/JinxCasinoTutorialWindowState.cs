using System;
using Core.Runtime;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using UnityEngine;
namespace Hotfix.JinxCasino.UI
{
    /// 教学窗口跨页面保留的确认和延后状态，不持有UI控件。
    internal sealed class JinxCasinoTutorialWindowState
    {
        private readonly JinxCasinoController owner;
        private enum TutorialConfirmation { None, NewStandard, RestartTutorial }
        private TutorialConfirmation tutorialConfirmation;
        private int lastTutorialCancelFrame = -1;
        private string tutorialDismissedRun, tutorialReadyDeferredRun, tutorialUiFeedback;
        private string TutorialRun => owner.Game.State?.RunId;
        internal string Feedback => tutorialUiFeedback;
        internal bool Restarting => tutorialConfirmation == TutorialConfirmation.RestartTutorial;
        internal JinxCasinoTutorialWindowState(JinxCasinoController controller) => owner = controller;
        internal void SetFeedback(string value) => tutorialUiFeedback = value;
        private void Refresh() => owner.Data.Handler.Publish();
        internal JinxCasinoPage ResolveState()
        {
            if (!owner.Game.HasAdventure) return JinxCasinoPage.MainMenu;
            if (tutorialConfirmation != TutorialConfirmation.None) return JinxCasinoPage.TutorialConfirm;
            bool canChoose = owner.Player.IsPaused || !owner.Player.HasFocus && !owner.Game.HasActiveRound;
            if (canChoose && tutorialDismissedRun != TutorialRun && (owner.Player.Tutorial.Status == CasinoTutorialStatus.Completed || owner.Player.Tutorial.Status == CasinoTutorialStatus.Skipped)) return JinxCasinoPage.TutorialChoice;
            if (canChoose && tutorialReadyDeferredRun != TutorialRun && owner.Player.Tutorial.Status == CasinoTutorialStatus.Active && owner.Player.Tutorial.Step == CasinoTutorialStep.Ready) return JinxCasinoPage.TutorialReady;
            return owner.Player.IsPaused ? JinxCasinoPage.Pause : JinxCasinoPage.Field;
        }
        internal void StartTeaching() { RecordTeachingResult(owner.Player.Tutorial.StartAdventure()); Refresh(); }

        internal void SkipTeaching() { RecordTeachingResult(owner.Player.Tutorial.Skip()); Refresh(); }

        internal void CompleteTeaching() { RecordTeachingResult(owner.Player.Tutorial.Complete()); Refresh(); }

        internal void RetryTeaching() { tutorialUiFeedback = null; tutorialConfirmation = TutorialConfirmation.RestartTutorial; Refresh(); }

        internal void RequestTeachingStandard() { tutorialUiFeedback = null; tutorialConfirmation = TutorialConfirmation.NewStandard; Refresh(); }

        internal void DeferTeachingCompletion()
        { tutorialReadyDeferredRun = TutorialRun; tutorialUiFeedback = null; Refresh(); }

        internal void ContinueTeachingPractice()
        { tutorialDismissedRun = TutorialRun; ResumeTeachingView(); }

        internal void ReopenTeachingChoice()
        { tutorialReadyDeferredRun = tutorialDismissedRun = null; tutorialUiFeedback = null; Refresh(); }

        internal void ResumeTeachingView()
        {
            tutorialUiFeedback = null;
            if (owner.Player.IsPaused && !owner.Player.Resume()) tutorialUiFeedback = "请先回到游戏窗口或接回手柄，再点击继续。";
            Refresh();
        }

        internal void ConfirmTeachingReplacement()
        {
            if (tutorialConfirmation == TutorialConfirmation.RestartTutorial)
            {
                var result = owner.Player.Tutorial.StartAdventure(replaceCurrentRun: true);
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

        internal void RecordTeachingResult(CasinoAdventureResult result)
        { tutorialUiFeedback = result.Success ? null : result.Description ?? "暂时不能执行，请先完成当前操作。"; }

        internal void CancelTutorialWindow()
        {
            if (owner == null || lastTutorialCancelFrame == Time.frameCount) return;
            // Core Cancel与手柄Menu/Pause同帧触发时，只处理一次当前菜单返回。
            lastTutorialCancelFrame = Time.frameCount;
            if (tutorialConfirmation != TutorialConfirmation.None) { tutorialConfirmation = TutorialConfirmation.None; tutorialUiFeedback = null; Refresh(); }
            else if (owner.Data.Page == JinxCasinoPage.TutorialReady) DeferTeachingCompletion();
            else if (owner.Data.Page == JinxCasinoPage.TutorialChoice) { tutorialDismissedRun = TutorialRun; tutorialUiFeedback = null; Refresh(); }
            else if (owner.Data.Page == JinxCasinoPage.Pause) { owner.Player.Resume(); Refresh(); }
        }
    }
}
