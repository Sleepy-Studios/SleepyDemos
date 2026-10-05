using System;
using Core.Runtime;
using Hotfix.JinxCasino.Rules;
namespace Hotfix.JinxCasino
{
    internal sealed class JinxCasinoHandler : HandlerBase<JinxCasinoAction, JinxCasinoData>, IDisposable
    {
        private readonly JinxCasinoController scene;
        internal JinxCasinoHandler(JinxCasinoController scene) => this.scene = scene;
        protected override void OnInit()
        { State.Game.Changed += OnGame; if (State.Player != null) State.Player.Changed += Publish; if (State.Settings != null) State.Settings.Changed += Publish; }
        private void OnGame(CasinoSceneEffect[] _) => Publish();
        internal void SetExiting(bool value) { State.IsExiting = value; State.Game.CommandInputEnabled = !value; Publish(); }
        internal void Publish()
        {
            if (!ReferenceEquals(GlobalData.Get<JinxCasinoData>(), State)) return;
            if (scene != null) State.Page = State.SettingsOpen ? JinxCasinoPage.Settings : State.Save.IsOpen
                ? State.Save.PendingSlot > 0 ? JinxCasinoPage.SaveConfirm : JinxCasinoPage.SaveSlots
                : State.Player.Exit.HasEnding && !State.Game.HasActiveRound && !State.Player.HasFocus
                ? JinxCasinoPage.Ending : State.Tutorial.ResolveState();
            ApplyState();
        }
        protected override void Reduce(JinxCasinoAction action)
        {
            switch (action)
            {
                case JinxCasinoSetStatusAction request when ReferenceEquals(request.Source, State.Game):
                    State.Game.SetStatus(request.message); break;
                case JinxCasinoStartAdventureAction request when ReferenceEquals(request.Source, State.Game):
                    State.Game.StartAdventure(request.mode, request.config, request.seed); break;
                case JinxCasinoStartTutorialAdventureAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.StartTutorialAdventure(request.config, request.replaceCurrentRun, request.seed); break;
                case JinxCasinoTickAction request when ReferenceEquals(request.Source, State.Game):
                    State.Game.Tick(request.deltaSeconds); break;
                case JinxCasinoPurchaseItemAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.PurchaseItem(request.itemId, request.frameId); break;
                case JinxCasinoUseItemAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.UseItem(request.itemId, request.targetId, request.frameId); break;
                case JinxCasinoCancelPreparedItemAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.CancelPreparedItem(request.itemId, request.frameId); break;
                case JinxCasinoCompleteStageAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.CompleteStage(request.frameId); break;
                case JinxCasinoChooseEndingAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.ChooseEnding(request.ending, request.frameId); break;
                case JinxCasinoSaveAdventureAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.SaveAdventure(request.slot); break;
                case JinxCasinoLoadAdventureAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.LoadAdventure(request.slot); break;
                case JinxCasinoObserveTutorialAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.ObserveTutorial(request.fact, request.value, request.stationId, request.notify); break;
                case JinxCasinoSkipTutorialAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.SkipTutorial(); break;
                case JinxCasinoCompleteTutorialAction request when ReferenceEquals(request.Source, State.Game):
                    request.Result = State.Game.CompleteTutorial(); break;
                case JinxCasinoUiAction request when ReferenceEquals(request.Source, scene):
                    if (State.IsExiting) return;
                    switch (request.Command)
                    {
                        case JinxCasinoUiCommand.Pause: State.Player.Pause(); break;
                        case JinxCasinoUiCommand.Resume: State.Player.Resume(); break;
                        case JinxCasinoUiCommand.Interact: State.Player.Interact(); break;
                        case JinxCasinoUiCommand.ExitTable: State.Player.CloseTable(true); break;
                        case JinxCasinoUiCommand.Exit: scene.ExitScene(); break;
                        case JinxCasinoUiCommand.Quit: scene.QuitStandaloneApplication(); break;
                        case JinxCasinoUiCommand.OpenSettings: OpenSettings(); break;
                        case JinxCasinoUiCommand.CloseSettings: State.SettingsOpen = false; scene.UI.OnSettingsClosedCore(); break;
                        case JinxCasinoUiCommand.OpenSaveLoad: State.Save.Open(false); break;
                        case JinxCasinoUiCommand.OpenSaveWrite: State.Save.Open(true); break;
                        case JinxCasinoUiCommand.ChooseSaveSlot: State.Save.ChooseSaveSlot(request.Slot); break;
                        case JinxCasinoUiCommand.ConfirmSaveOperation: State.Save.ConfirmSaveOperation(); break;
                        case JinxCasinoUiCommand.CancelSaveWindow: State.Save.CancelSaveWindow(); break;
                        case JinxCasinoUiCommand.StartTeaching: State.Tutorial.StartTeaching(); break;
                        case JinxCasinoUiCommand.SkipTeaching: State.Tutorial.SkipTeaching(); break;
                        case JinxCasinoUiCommand.CompleteTeaching: State.Tutorial.CompleteTeaching(); break;
                        case JinxCasinoUiCommand.RetryTeaching: State.Tutorial.RetryTeaching(); break;
                        case JinxCasinoUiCommand.RequestTeachingStandard: State.Tutorial.RequestTeachingStandard(); break;
                        case JinxCasinoUiCommand.DeferTeachingCompletion: State.Tutorial.DeferTeachingCompletion(); break;
                        case JinxCasinoUiCommand.ContinueTeachingPractice: State.Tutorial.ContinueTeachingPractice(); break;
                        case JinxCasinoUiCommand.ReopenTeachingChoice: State.Tutorial.ReopenTeachingChoice(); break;
                        case JinxCasinoUiCommand.ResumeTeachingView: State.Tutorial.ResumeTeachingView(); break;
                        case JinxCasinoUiCommand.ConfirmTeachingReplacement: State.Tutorial.ConfirmTeachingReplacement(); break;
                        case JinxCasinoUiCommand.CancelTutorialWindow: State.Tutorial.CancelTutorialWindow(); break;
                        case JinxCasinoUiCommand.CancelWindow: scene.UI.CancelImmersionHudWindowCore(); break;
                    }
                    break;
                case JinxCasinoSettingsAction request when ReferenceEquals(request.Source, scene):
                    ApplySettings(request); break;
                case JinxCasinoTableActionRequest request when ReferenceEquals(request.Source, scene) && !State.IsExiting:
                    request.Result = request.Table.Apply(request.Action, request.Value, request.Frame); break;
                default: return;
            }
            Publish();
        }
        private void OpenSettings()
        {
            if (State.SettingsOpen || State.Page != JinxCasinoPage.MainMenu && State.Page != JinxCasinoPage.Pause) return;
            State.SettingsOpen = true;
        }
        private void ApplySettings(JinxCasinoSettingsAction request)
        {
            try
            {
                switch (request.Operation)
                {
                    case JinxCasinoSettingsOperation.Begin:
                        CancelSettings(); State.SettingsSaved = State.Settings.Value; State.SettingsDraft = State.SettingsSaved.Copy(); break;
                    case JinxCasinoSettingsOperation.Preview:
                        if (request.Candidate == null || !request.Candidate.IsValid) { request.Error = "当前参数超出可用范围。"; return; }
                        State.SettingsDraft = request.Candidate.Copy(); State.SettingsPreviewing = true; State.Settings.Apply(State.SettingsDraft); break;
                    case JinxCasinoSettingsOperation.Save:
                        State.Settings.Save(State.SettingsDraft); State.SettingsSaved = State.Settings.Value; State.SettingsPreviewing = false; break;
                    case JinxCasinoSettingsOperation.Cancel: CancelSettings(); break;
                }
                request.Success = true;
            }
            catch (Exception error) { request.Error = error.Message; }
        }
        private void CancelSettings()
        { if (State.SettingsPreviewing) { State.SettingsPreviewing = false; State.Settings.Apply(State.SettingsSaved); } }
        public void Dispose()
        { State.Game.Changed -= OnGame; if (State.Player != null) State.Player.Changed -= Publish; if (State.Settings != null) State.Settings.Changed -= Publish; CancelSettings(); }
    }
}
