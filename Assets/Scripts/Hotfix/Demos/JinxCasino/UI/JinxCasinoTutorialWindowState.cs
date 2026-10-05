using Hotfix.JinxCasino.Rules;

namespace Hotfix.JinxCasino.UI
{
    /// 教学窗口待确认的操作类型。
    internal enum JinxCasinoTutorialConfirmation
    {
        None,
        NewStandard,
        RestartTutorial
    }

    /// 教学窗口的确认与延后状态，不执行教学命令或页面导航。
    internal sealed class JinxCasinoTutorialWindowState
    {
        internal JinxCasinoTutorialConfirmation Confirmation { get; set; }

        internal int LastCancelFrame { get; set; } = -1;

        internal string DismissedRun { get; set; }

        internal string ReadyDeferredRun { get; set; }

        internal string Feedback { get; set; }

        internal bool Restarting => Confirmation == JinxCasinoTutorialConfirmation.RestartTutorial;

        internal JinxCasinoPage ResolveState(JinxCasinoData data)
        {
            if (!data.Game.HasAdventure)
                return JinxCasinoPage.MainMenu;
            if (Confirmation != JinxCasinoTutorialConfirmation.None)
                return JinxCasinoPage.TutorialConfirm;
            string run = data.Game.State?.RunId;
            bool canChoose = data.Player.IsPaused || !data.Player.HasFocus && !data.Game.HasActiveRound;
            if (canChoose && DismissedRun != run && (data.Player.Tutorial.Status == CasinoTutorialStatus.Completed || data.Player.Tutorial.Status == CasinoTutorialStatus.Skipped))
                return JinxCasinoPage.TutorialChoice;
            if (canChoose && ReadyDeferredRun != run && data.Player.Tutorial.Status == CasinoTutorialStatus.Active && data.Player.Tutorial.Step == CasinoTutorialStep.Ready)
                return JinxCasinoPage.TutorialReady;
            return data.Player.IsPaused ? JinxCasinoPage.Pause : JinxCasinoPage.Field;
        }

        internal void ClearData()
        {
            Confirmation = JinxCasinoTutorialConfirmation.None;
            LastCancelFrame = -1;
            DismissedRun = ReadyDeferredRun = Feedback = null;
        }
    }
}
