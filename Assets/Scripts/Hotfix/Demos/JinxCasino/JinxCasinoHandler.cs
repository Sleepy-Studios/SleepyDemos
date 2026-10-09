using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Core.Runtime;
using Hotfix.JinxCasino.Rules;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.UI;
using UnityEngine;

namespace Hotfix.JinxCasino
{
    /// 本场业务命令、持久状态修改及完整操作后的发布入口。
    internal sealed class JinxCasinoHandler : HandlerBase<JinxCasinoAction, JinxCasinoData>, IDisposable
    {
        private readonly JinxCasinoController scene;
        private int reducing;
        private bool uiReady;
        private View window;
        private View returnPage;
        private Type requestedPage;
        private CancellationTokenSource pageLifetime;
        private Task pageChange = Task.CompletedTask;
        internal JinxCasinoHandler(JinxCasinoController scene)
        {
            this.scene = scene;
        }

        protected override void OnInit()
        {
            State.Game.Changed += OnGame;
            if (State.Player != null)
                State.Player.Changed += Publish;
            if (State.Settings != null)
                State.Settings.Changed += Publish;
        }

        private void OnGame(CasinoSceneEffect[] _) => Publish();
        private void SetExiting(bool value)
        {
            State.IsExiting = value;
            State.Game.CommandInputEnabled = !value;
            Publish();
        }

        internal void Publish()
        {
            if (reducing != 0 || !ReferenceEquals(GlobalData.Get<JinxCasinoData>(), State))
                return;
            if (scene != null)
                State.Page = State.ResolvePage();
            ApplyState();
            UpdatePage();
        }

        /// <summary>
        /// 同步处理本场命令，嵌套规则事件在完整操作结束后统一发布。
        /// </summary>
        /// <param name="action">本场业务请求；旧场景或旧规则来源被忽略。</param>
        protected override void Reduce(JinxCasinoAction action)
        {
            // 嵌套规则事件不能让页面读到尚未完成的交易或场景装配。
            reducing++;
            try
            {
                switch (action)
                {
                    case JinxCasinoExitingAction closing when ReferenceEquals(closing.Source, scene):
                        SetExiting(closing.Exiting);
                        break;
                    case JinxCasinoSetStatusAction request when ReferenceEquals(request.Source, State.Game):
                        State.Game.SetStatus(request.Message);
                        break;
                    case JinxCasinoStartAdventureAction request when ReferenceEquals(request.Source, State.Game):
                        State.Game.StartAdventure(request.Mode, request.Config, request.Seed);
                        break;
                    case JinxCasinoStartTutorialAdventureAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.StartTutorialAdventure(request.Config, request.ReplaceCurrentRun, request.Seed);
                        break;
                    case JinxCasinoTickAction request when ReferenceEquals(request.Source, State.Game):
                        State.Game.Tick(request.DeltaSeconds);
                        break;
                    case JinxCasinoPurchaseItemAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.PurchaseItem(request.ItemId, request.FrameId);
                        break;
                    case JinxCasinoUseItemAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.UseItem(request.ItemId, request.TargetId, request.FrameId);
                        break;
                    case JinxCasinoCancelPreparedItemAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.CancelPreparedItem(request.ItemId, request.FrameId);
                        break;
                    case JinxCasinoCompleteStageAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.CompleteStage(request.FrameId);
                        break;
                    case JinxCasinoChooseEndingAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.ChooseEnding(request.Ending, request.FrameId);
                        break;
                    case JinxCasinoSaveAdventureAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.SaveAdventure(request.Slot);
                        break;
                    case JinxCasinoLoadAdventureAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.LoadAdventure(request.Slot);
                        break;
                    case JinxCasinoObserveTutorialAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.ObserveTutorial(request.Fact, request.Value, request.StationId, request.Notify);
                        break;
                    case JinxCasinoSkipTutorialAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.SkipTutorial();
                        break;
                    case JinxCasinoCompleteTutorialAction request when ReferenceEquals(request.Source, State.Game):
                        request.Result = State.Game.CompleteTutorial();
                        break;
                    case JinxCasinoUiAction request when ReferenceEquals(request.Source, scene):
                        if (State.IsExiting)
                            return;
                        switch (request.Command)
                        {
                            case JinxCasinoUiCommand.Pause:
                                State.Player.Pause();
                                break;
                            case JinxCasinoUiCommand.Resume:
                                State.Player.Resume();
                                break;
                            case JinxCasinoUiCommand.Interact:
                                State.Player.Interact();
                                break;
                            case JinxCasinoUiCommand.ExitTable:
                                State.Player.CloseTable(true);
                                break;
                            case JinxCasinoUiCommand.Exit:
                                scene.ExitScene();
                                break;
                            case JinxCasinoUiCommand.Quit:
                                scene.QuitStandaloneApplication();
                                break;
                            case JinxCasinoUiCommand.OpenSettings:
                                OpenSettings();
                                break;
                            case JinxCasinoUiCommand.CloseSettings:
                                State.SettingsOpen = false;
                                State.LastSettingsCancelFrame = Time.frameCount;
                                break;
                            case JinxCasinoUiCommand.OpenSaveLoad:
                                OpenSaveWindow(false);
                                break;
                            case JinxCasinoUiCommand.OpenSaveWrite:
                                OpenSaveWindow(true);
                                break;
                            case JinxCasinoUiCommand.ChooseSaveSlot:
                                ChooseSaveSlot(request.Slot);
                                break;
                            case JinxCasinoUiCommand.ConfirmSaveOperation:
                                ConfirmSaveOperation();
                                break;
                            case JinxCasinoUiCommand.CancelSaveWindow:
                                CancelSaveWindow();
                                break;
                            case JinxCasinoUiCommand.StartTeaching:
                                StartTeaching();
                                break;
                            case JinxCasinoUiCommand.SkipTeaching:
                                SkipTeaching();
                                break;
                            case JinxCasinoUiCommand.CompleteTeaching:
                                CompleteTeaching();
                                break;
                            case JinxCasinoUiCommand.RetryTeaching:
                                RetryTeaching();
                                break;
                            case JinxCasinoUiCommand.RequestTeachingStandard:
                                RequestTeachingStandard();
                                break;
                            case JinxCasinoUiCommand.DeferTeachingCompletion:
                                DeferTeachingCompletion();
                                break;
                            case JinxCasinoUiCommand.ContinueTeachingPractice:
                                ContinueTeachingPractice();
                                break;
                            case JinxCasinoUiCommand.ReopenTeachingChoice:
                                ReopenTeachingChoice();
                                break;
                            case JinxCasinoUiCommand.ResumeTeachingView:
                                ResumeTeachingView();
                                break;
                            case JinxCasinoUiCommand.ConfirmTeachingReplacement:
                                ConfirmTeachingReplacement();
                                break;
                            case JinxCasinoUiCommand.CancelTutorialWindow:
                                CancelTutorialWindow();
                                break;
                            case JinxCasinoUiCommand.CancelWindow:
                                CancelWindow();
                                break;
                        }

                        break;
                    case JinxCasinoSettingsAction request when ReferenceEquals(request.Source, scene):
                        ApplySettings(request);
                        break;
                    case JinxCasinoTableActionRequest request when ReferenceEquals(request.Source, scene) && !State.IsExiting:
                        request.Result = request.Table.Apply(request.Action, request.Value, request.Frame);
                        break;
                    default:
                        return;
                }
            }
            finally
            {
                reducing--;
            }

            Publish();
        }

        internal void BeginUi()
        {
            uiReady = true;
            Publish();
        }

        private void UpdatePage()
        {
            if (!uiReady)
                return;
            Type target = State.IsExiting ? null : PageType(State.Page);
            if (target == requestedPage)
                return;
            if (target == null)
                State.Player.SetMenuState(false, false, null);
            requestedPage = target;
            CancelPageRequest();
            pageLifetime = CancellationTokenSource.CreateLinkedTokenSource(scene.Lifetime);
            pageChange = ChangePageAsync(State.IsExiting ? JinxCasinoPage.None : State.Page, pageLifetime.Token).AsTask();
            pageChange.AsUniTask().Forget();
        }

        private static Type PageType(JinxCasinoPage page) => page switch
        {
            JinxCasinoPage.MainMenu => typeof(JinxCasinoMainMenuView),
            JinxCasinoPage.Pause => typeof(JinxCasinoPauseView),
            JinxCasinoPage.TutorialReady or JinxCasinoPage.TutorialChoice or JinxCasinoPage.TutorialConfirm => typeof(JinxCasinoTutorialView),
            JinxCasinoPage.SaveSlots or JinxCasinoPage.SaveConfirm => typeof(JinxCasinoSaveView),
            JinxCasinoPage.Ending => typeof(JinxCasinoEndingView),
            JinxCasinoPage.Settings => typeof(JinxCasinoSettingsView),
            _ => null
        };
        private async UniTask ChangePageAsync(JinxCasinoPage page, CancellationToken token)
        {
            Type target = PageType(page);
            try
            {
                bool overlay = target == typeof(JinxCasinoSettingsView) || target == typeof(JinxCasinoSaveView);
                if (returnPage != null && returnPage.GetType() == target)
                {
                    if (!ReferenceEquals(window, returnPage))
                        await ClosePageAsync(window, token);
                    window = returnPage;
                    returnPage = null;
                    return;
                }

                if (window != null)
                {
                    if (overlay && returnPage == null)
                        returnPage = window;
                    else
                    {
                        await ClosePageAsync(window, token);
                        window = null;
                    }
                }

                if (!overlay && returnPage != null)
                {
                    await ClosePageAsync(returnPage, token);
                    returnPage = null;
                }

                if (target == null)
                    return;
                var options = new UIShowOptions(animated: false, hidePrevious: overlay);
                var result = page switch
                {
                    JinxCasinoPage.MainMenu => await UIManager.Instance.ShowAsync<JinxCasinoMainMenuView>(view => view.SetData(scene), options, token),
                    JinxCasinoPage.Pause => await UIManager.Instance.ShowAsync<JinxCasinoPauseView>(view => view.SetData(scene), options, token),
                    JinxCasinoPage.TutorialReady or JinxCasinoPage.TutorialChoice or JinxCasinoPage.TutorialConfirm => await UIManager.Instance.ShowAsync<JinxCasinoTutorialView>(view => view.SetData(scene), options, token),
                    JinxCasinoPage.SaveSlots or JinxCasinoPage.SaveConfirm => await UIManager.Instance.ShowAsync<JinxCasinoSaveView>(view => view.SetData(scene), options, token),
                    JinxCasinoPage.Ending => await UIManager.Instance.ShowAsync<JinxCasinoEndingView>(view => view.SetData(scene), options, token),
                    JinxCasinoPage.Settings => await UIManager.Instance.ShowAsync<JinxCasinoSettingsView>(view => view.SetData(scene), options, token),
                    _ => throw new ArgumentOutOfRangeException(nameof(page))};
                if (result.Status == UIOperationStatus.Failed)
                    throw result.Exception;
                if (result.Status is UIOperationStatus.Succeeded or UIOperationStatus.Ignored)
                    window = result.View;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, scene);
                if (ReferenceEquals(GlobalData.Get<JinxCasinoData>(), State))
                    GlobalData.Dispatch(new JinxCasinoSetStatusAction(State.Game, "页面打开失败：" + exception.Message));
            }
        }

        private static async UniTask ClosePageAsync(View view, CancellationToken token = default)
        {
            var result = await UIManager.Instance.CloseAsync(view, false, token);
            if (result.Status == UIOperationStatus.Failed)
                throw result.Exception;
            token.ThrowIfCancellationRequested();
        }

        private void CancelPageRequest()
        {
            pageLifetime?.Cancel();
            pageLifetime?.Dispose();
            pageLifetime = null;
        }

        internal async UniTask CloseWindowsAsync()
        {
            uiReady = false;
            CancelSettings();
            CancelPageRequest();
            await pageChange;
            if (window != null)
                await ClosePageAsync(window);
            if (returnPage != null)
                await ClosePageAsync(returnPage);
            window = returnPage = null;
            requestedPage = null;
        }

        private void OpenSettings()
        {
            if (State.SettingsOpen || State.Page != JinxCasinoPage.MainMenu && State.Page != JinxCasinoPage.Pause)
                return;
            State.SettingsOpen = true;
        }

        private void ApplySettings(JinxCasinoSettingsAction request)
        {
            try
            {
                switch (request.Operation)
                {
                    case JinxCasinoSettingsOperation.Begin:
                        CancelSettings();
                        State.SettingsSaved = State.Settings.Value;
                        State.SettingsDraft = State.SettingsSaved.Copy();
                        break;
                    case JinxCasinoSettingsOperation.Preview:
                        if (request.Candidate == null || !request.Candidate.IsValid)
                        {
                            request.Error = "当前参数超出可用范围。";
                            return;
                        }

                        State.SettingsDraft = request.Candidate.Copy();
                        State.SettingsPreviewing = true;
                        State.Settings.Apply(State.SettingsDraft);
                        break;
                    case JinxCasinoSettingsOperation.Save:
                        State.Settings.Save(State.SettingsDraft);
                        State.SettingsSaved = State.Settings.Value;
                        State.SettingsPreviewing = false;
                        break;
                    case JinxCasinoSettingsOperation.Cancel:
                        CancelSettings();
                        break;
                }

                request.Success = true;
            }
            catch (Exception error)
            {
                request.Error = error.Message;
            }
        }

        private void CancelSettings()
        {
            if (State.SettingsPreviewing)
            {
                State.SettingsPreviewing = false;
                State.Settings.Apply(State.SettingsSaved);
            }
        }

        public void Dispose()
        {
            uiReady = false;
            CancelPageRequest();
            foreach (var view in new[]
            {
                window,
                returnPage
            }

            )
                if (view != null)
                    UIManager.Instance.CloseAsync(view, false).Forget();
            window = returnPage = null;
            State.Game.Changed -= OnGame;
            if (State.Player != null)
                State.Player.Changed -= Publish;
            if (State.Settings != null)
                State.Settings.Changed -= Publish;
            CancelSettings();
        }

        private void OpenSaveWindow(bool writing)
        {
            if (State.IsExiting || writing && !State.Game.HasAdventure)
                return;
            State.Save.Writing = writing;
            State.Save.SourceRun = State.Game.State?.RunId;
            State.Save.IsOpen = true;
            State.Save.PendingSlot = 0;
            State.Save.Feedback = null;
            ReloadSaveInfos();
        }

        private void ReloadSaveInfos()
        {
            for (int slot = 1; slot <= 3; slot++)
            {
                try
                {
                    State.Save.Infos[slot - 1] = State.Game.GetSaveSlotInfo(slot);
                }
                catch (Exception)
                {
                    State.Save.Infos[slot - 1] = new CasinoSaveSlotInfo
                    {
                        Slot = slot,
                        Error = "暂时无法读取此槽，请稍后再试。"
                    };
                }
            }
        }

        private void ChooseSaveSlot(int slot)
        {
            if (!State.Save.IsOpen || State.Save.PendingSlot > 0 || State.IsExiting || !State.Save.CanSelectSaveSlot(slot))
                return;
            ReloadSaveInfos();
            if (!State.Save.CanSelectSaveSlot(slot))
            {
                State.Save.Feedback = "此槽暂时无法恢复，请选择另一个存档。";
                return;
            }

            var info = State.Save.Infos[slot - 1];
            State.Save.Feedback = null;
            if (State.Save.Writing && !info.IsEmpty || !State.Save.Writing && State.Game.HasAdventure)
            {
                State.Save.PendingSlot = slot;
            }
            else
                ExecuteSaveOperation(slot);
        }

        private void ConfirmSaveOperation()
        {
            if (State.Save.PendingSlot > 0)
                ExecuteSaveOperation(State.Save.PendingSlot);
        }

        private void ExecuteSaveOperation(int slot)
        {
            if (State.IsExiting || State.Save.LastActionFrame == Time.frameCount)
                return;
            State.Save.LastActionFrame = Time.frameCount;
            if (State.Save.SourceRun != State.Game.State?.RunId)
            {
                State.Save.Feedback = "当前旅程已改变，请返回后重新选择。";
                return;
            }

            bool success = State.Save.Writing ? State.Game.SaveAdventure(slot) : State.Game.LoadAdventure(slot);
            State.Save.Feedback = State.Game.Status;
            if (!success)
            {
                return;
            }

            State.Save.PendingSlot = 0;
            if (State.Save.Writing)
            {
                ReloadSaveInfos();
                State.Save.SourceRun = State.Game.State?.RunId;
            }
            else
            {
                State.Save.IsOpen = false;
                if (State.Player.IsPaused && !State.Player.Resume())
                    State.Tutorial.Feedback = "请先回到游戏窗口或接回手柄，再点击继续。";
            }
        }

        private void CancelSaveWindow()
        {
            if (scene == null || State.Save.LastCancelFrame == Time.frameCount)
                return;
            State.Save.LastCancelFrame = Time.frameCount;
            if (!State.Save.IsOpen)
            {
                CancelTutorialWindow();
                return;
            }

            if (State.Save.PendingSlot > 0)
                State.Save.PendingSlot = 0;
            else
                State.Save.IsOpen = false;
            State.Save.Feedback = null;
        }

        private void StartTeaching()
        {
            RecordTeachingResult(State.Player.Tutorial.StartAdventure());
        }

        private void SkipTeaching()
        {
            RecordTeachingResult(State.Player.Tutorial.Skip());
        }

        private void CompleteTeaching()
        {
            RecordTeachingResult(State.Player.Tutorial.Complete());
        }

        private void RetryTeaching()
        {
            State.Tutorial.Feedback = null;
            State.Tutorial.Confirmation = JinxCasinoTutorialConfirmation.RestartTutorial;
        }

        private void RequestTeachingStandard()
        {
            State.Tutorial.Feedback = null;
            State.Tutorial.Confirmation = JinxCasinoTutorialConfirmation.NewStandard;
        }

        private void DeferTeachingCompletion()
        {
            State.Tutorial.ReadyDeferredRun = State.Game.State?.RunId;
            State.Tutorial.Feedback = null;
        }

        private void ContinueTeachingPractice()
        {
            State.Tutorial.DismissedRun = State.Game.State?.RunId;
            ResumeTeachingView();
        }

        private void ReopenTeachingChoice()
        {
            State.Tutorial.ReadyDeferredRun = State.Tutorial.DismissedRun = null;
            State.Tutorial.Feedback = null;
        }

        private void ResumeTeachingView()
        {
            State.Tutorial.Feedback = null;
            if (State.Player.IsPaused && !State.Player.Resume())
                State.Tutorial.Feedback = "请先回到游戏窗口或接回手柄，再点击继续。";
        }

        private void ConfirmTeachingReplacement()
        {
            if (State.Tutorial.Confirmation == JinxCasinoTutorialConfirmation.RestartTutorial)
            {
                var result = State.Player.Tutorial.StartAdventure(replaceCurrentRun: true);
                RecordTeachingResult(result);
                if (!result.Success)
                {
                    return;
                }
            }
            else if (State.Tutorial.Confirmation == JinxCasinoTutorialConfirmation.NewStandard)
            {
                string previousRun = State.Game.State?.RunId;
                scene.StartAdventure(CasinoAdventureMode.Standard);
                if (State.Game.State?.RunId == previousRun)
                {
                    State.Tutorial.Feedback = "暂时不能开始新冒险，请先完成当前操作。";
                    return;
                }
            }
            else
                return;
            State.Tutorial.Confirmation = JinxCasinoTutorialConfirmation.None;
            State.Tutorial.DismissedRun = State.Tutorial.ReadyDeferredRun = null;
            ResumeTeachingView();
        }

        private void RecordTeachingResult(CasinoAdventureResult result)
        {
            State.Tutorial.Feedback = result.Success ? null : result.Description ?? "暂时不能执行，请先完成当前操作。";
        }

        private void CancelTutorialWindow()
        {
            if (scene == null || State.Tutorial.LastCancelFrame == Time.frameCount)
                return;
            // Core Cancel与手柄Menu/Pause同帧触发时，只处理一次当前菜单返回。
            State.Tutorial.LastCancelFrame = Time.frameCount;
            if (State.Tutorial.Confirmation != JinxCasinoTutorialConfirmation.None)
            {
                State.Tutorial.Confirmation = JinxCasinoTutorialConfirmation.None;
                State.Tutorial.Feedback = null;
            }
            else if (State.Page == JinxCasinoPage.TutorialReady)
                DeferTeachingCompletion();
            else if (State.Page == JinxCasinoPage.TutorialChoice)
            {
                State.Tutorial.DismissedRun = State.Game.State?.RunId;
                State.Tutorial.Feedback = null;
            }
            else if (State.Page == JinxCasinoPage.Pause)
            {
                State.Player.Resume();
            }
        }

        private void CancelWindow()
        {
            if (State.LastSettingsCancelFrame == Time.frameCount)
                return;
            if (State.SettingsOpen)
            {
                State.LastSettingsCancelFrame = Time.frameCount;
                State.SettingsOpen = false;
            }
            else if (State.Page == JinxCasinoPage.Ending)
                return;
            else if (State.Save.IsOpen)
                CancelSaveWindow();
            else
                CancelTutorialWindow();
        }
    }
}
