using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.Runtime;
using Core.Runtime.Inputs;

namespace Hotfix.DroneFlight
{
    /// 处理选择与UI命令，实际机体生成和页面过渡由场景服务执行。
    internal sealed class DroneFlightHandler : HandlerBase<DroneFlightAction, DroneFlightData>, IDisposable
    {
        private Action<DroneVehicleKind> start;
        private Action back;
        private DronePlayerInput input;
        private DroneFlightViewData viewData;
        private DroneFlightDebugDrawRenderer debugRenderer;
        private View helpView;
        private View debugView;
        private CancellationToken lifetime;
        private CancellationTokenSource helpLifetime;
        private CancellationTokenSource debugLifetime;
        private Task helpChange = Task.CompletedTask;
        private Task debugChange = Task.CompletedTask;
        internal DroneFlightHandler(Action<DroneVehicleKind> start, Action back)
        {
            this.start = start;
            this.back = back;
        }

        protected override void OnInit()
        {
            InputDeviceState.Changed += RefreshInput;
            RefreshInput();
        }

        internal void ConfigureSelection(Action<DroneVehicleKind> start, Action back)
        {
            this.start = start;
            this.back = back;
        }

        internal void AttachInput(DronePlayerInput value)
        {
            if (!ReferenceEquals(input, value))
            {
                if (input != null)
                {
                    input.PresentationRequested -= OnPresentation;
                    input.PanelChanged -= OnInputChanged;
                    input.HelpChanged -= OnInputChanged;
                    input.DebugChanged -= OnInputChanged;
                }

                input = value;
                if (input != null)
                {
                    input.PresentationRequested += OnPresentation;
                    input.PanelChanged += OnInputChanged;
                    input.HelpChanged += OnInputChanged;
                    input.DebugChanged += OnInputChanged;
                }
            }

            RefreshInput();
        }

        private void OnInputChanged(bool value) => RefreshInput();
        private void OnPresentation(string command) => GlobalData.Dispatch(new DroneFlightControlAction(command));
        private void RefreshInput()
        {
            State.DeviceKind = InputDeviceState.ActiveKind;
            State.PanelVisible = input != null && input.IsPanelOpen;
            State.ControlsSuppressed = input != null && (input.IsHelpOpen || input.IsDebugOpen);
            ApplyState();
        }

        private void SetFeedback(string value)
        {
            State.Feedback = value;
            if (State.Mode == DroneFlightSessionMode.Loading)
                State.Mode = DroneFlightSessionMode.Selecting;
            ApplyState();
        }

        private void SetShuttingDown(bool value)
        {
            State.ShuttingDown = value;
            if (value)
            {
                State.HelpRequested = State.DebugRequested = false;
                input?.SetHelpOpen(false);
                input?.SetDebugOpen(false);
            }

            ApplyState();
        }

        private void BeginLeaving()
        {
            State.Mode = DroneFlightSessionMode.Leaving;
            ApplyState();
        }

        private void SetDebugVisible(bool value)
        {
            State.DebugVisible = value;
            State.DebugRequested = value;
            input?.SetDebugOpen(value);
            ApplyState();
        }

        private void SetHelpRequested(bool value)
        {
            State.HelpRequested = value;
            input?.SetHelpOpen(value);
            ApplyState();
        }

        private void SetDebugDraw(bool value)
        {
            State.DebugDrawVisible = value;
            ApplyState();
        }

        private void RestoreMode(bool active)
        {
            State.Mode = active ? DroneFlightSessionMode.Active : DroneFlightSessionMode.Selecting;
            ApplyState();
        }

        /// <summary>
        /// 处理本模块业务命令，按实际服务与规则结果发布状态。
        /// </summary>
        /// <param name="action">当前模块的业务请求。</param>
        protected override void Reduce(DroneFlightAction action)
        {
            switch (action)
            {
                case DroneFlightFeedbackAction feedback when ReferenceEquals(feedback.Source, State):
                    SetFeedback(feedback.Message);
                    return;
                case DroneFlightShuttingDownAction closing when ReferenceEquals(closing.Source, State):
                    SetShuttingDown(closing.ShuttingDown);
                    return;
                case DroneFlightHelpResultAction help when ReferenceEquals(help.Source, State):
                    SetHelpRequested(help.Visible);
                    return;
                case DroneFlightDebugResultAction debug when ReferenceEquals(debug.Source, State):
                    SetDebugVisible(debug.Visible);
                    return;
                case DroneFlightDebugDrawAction draw when ReferenceEquals(draw.Source, State):
                    SetDebugDraw(draw.Visible);
                    return;
                case DroneFlightBeginLeavingAction leaving when ReferenceEquals(leaving.Source, State):
                    BeginLeaving();
                    return;
                case DroneFlightRestoreModeAction restore when ReferenceEquals(restore.Source, State):
                    RestoreMode(restore.Active);
                    return;
                case DroneFlightSelectAction select:
                    if (State.Mode != DroneFlightSessionMode.Selecting || (uint)select.Kind > 2)
                        return;
                    State.SelectedKind = select.Kind;
                    State.Feedback = null;
                    break;
                case DroneFlightStartAction:
                    if (State.Mode != DroneFlightSessionMode.Selecting || start == null)
                        return;
                    State.Version++;
                    State.Mode = DroneFlightSessionMode.Loading;
                    State.Feedback = null;
                    ApplyState();
                    start(State.SelectedKind);
                    return;
                case DroneFlightSelectionResultAction result:
                    if (result.Version != State.Version || State.Mode != DroneFlightSessionMode.Loading)
                        return;
                    State.Mode = result.Succeeded ? DroneFlightSessionMode.Active : DroneFlightSessionMode.Selecting;
                    State.Feedback = result.Message;
                    break;
                case DroneFlightExitAction:
                    if (State.Mode is DroneFlightSessionMode.Loading or DroneFlightSessionMode.Leaving || back == null)
                        return;
                    State.Mode = DroneFlightSessionMode.Leaving;
                    ApplyState();
                    back();
                    return;
                case DroneFlightTelemetryAction telemetry:
                    if (telemetry.SessionId != State.SessionId)
                        return;
                    State.Snapshot = telemetry.Snapshot;
                    State.HasSnapshot = true;
                    break;
                case DroneFlightHoldAction hold:
                    if (hold.Command == "ArmOrReset")
                        input?.SetTouchArmHeld(hold.Held);
                    if (hold.Command == "ReelIn")
                        input?.SetTouchLine(hold.Held ? -1 : 0);
                    if (hold.Command == "ReelOut")
                        input?.SetTouchLine(hold.Held ? 1 : 0);
                    return;
                case DroneFlightControlAction control:
                    switch (control.Command)
                    {
                        case "Help":
                            SetHelpRequested(!State.HelpRequested);
                            ChangeHelp();
                            return;
                        case "DebugPanel":
                            State.DebugRequested = !State.DebugRequested;
                            input?.SetDebugOpen(State.DebugRequested);
                            ApplyState();
                            ChangeDebug();
                            return;
                        case "DebugDraw":
                            State.DebugDrawVisible = !State.DebugDrawVisible;
                            ApplyState();
                            if (debugRenderer != null)
                                debugRenderer.enabled = State.DebugDrawVisible;
                            return;
                        case "CopyTelemetry":
                            input?.GetComponent<DroneTelemetryRecorder>()?.CopySummary();
                            return;
                        case "ViewModifier":
                            State.TouchLook = !State.TouchLook;
                            input?.SetTouchLookMode(State.TouchLook);
                            break;
                        case "ClosePanel":
                            input?.SetPanelOpen(false);
                            return;
                        default:
                            if (control.Command != "ReelIn" && control.Command != "ReelOut")
                                input?.Execute(control.Command);
                            return;
                    }

                    break;
                default:
                    return;
            }

            ApplyState();
        }

        internal void ConfigureViews(DroneFlightViewData value, DroneFlightDebugDrawRenderer renderer, CancellationToken token)
        {
            viewData = value;
            debugRenderer = renderer;
            lifetime = token;
            SetShuttingDown(false);
            SetDebugDraw(false);
            if (debugRenderer != null)
                debugRenderer.enabled = false;
        }

        private void ChangeHelp()
        {
            if (viewData == null || State.ShuttingDown)
                return;
            helpLifetime?.Cancel();
            helpLifetime?.Dispose();
            helpLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            helpChange = ChangeOverlayAsync(true, State.HelpRequested, helpLifetime.Token).AsTask();
            helpChange.AsUniTask().Forget();
        }

        private void ChangeDebug()
        {
            if (viewData == null || State.ShuttingDown)
                return;
            debugLifetime?.Cancel();
            debugLifetime?.Dispose();
            debugLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime);
            debugChange = ChangeOverlayAsync(false, State.DebugRequested, debugLifetime.Token).AsTask();
            debugChange.AsUniTask().Forget();
        }

        private async UniTask ChangeOverlayAsync(bool help, bool show, CancellationToken token)
        {
            try
            {
                View current = help ? helpView : debugView;
                var options = new UIShowOptions(animated: false, hidePrevious: false);
                UIOperationResult result;
                if (show)
                    result = help
                        ? await UIManager.Instance.ShowAsync<DroneFlightHelpView, DroneFlightViewData>(viewData, options, token)
                        : await UIManager.Instance.ShowAsync<DroneFlightDebugView, DroneFlightViewData>(viewData, options, token);
                else
                    result = await UIManager.Instance.CloseAsync(current, false, token);
                if (token.IsCancellationRequested || !ReferenceEquals(GlobalData.Get<DroneFlightData>(), State) || State.ShuttingDown)
                    return;
                if (result.Status == UIOperationStatus.Failed)
                {
                    SetFeedback("页面操作失败：" + result.Exception?.Message);
                    if (help)
                        SetHelpRequested(current != null);
                    else
                        SetDebugVisible(current != null);
                    return;
                }

                if (result.Status == UIOperationStatus.Canceled && show)
                    return;
                if (help)
                {
                    helpView = show ? result.View : null;
                    SetHelpRequested(helpView != null);
                }
                else
                {
                    debugView = show ? result.View : null;
                    SetDebugVisible(debugView != null);
                }
            }
            catch (OperationCanceledException)
            {
            }
        }

        internal async UniTask CloseOverlaysAsync()
        {
            SetShuttingDown(true);
            CancelOverlayRequests();
            await helpChange;
            await debugChange;
            foreach (var view in new[]
            {
                helpView,
                debugView
            }

            )
            {
                if (view == null)
                    continue;
                var result = await UIManager.Instance.CloseAsync(view, false);
                if (result.Status == UIOperationStatus.Failed)
                    throw result.Exception;
            }

            helpView = debugView = null;
            SetHelpRequested(false);
            SetDebugVisible(false);
            if (debugRenderer != null)
                debugRenderer.enabled = false;
        }

        private void CancelOverlayRequests()
        {
            helpLifetime?.Cancel();
            helpLifetime?.Dispose();
            helpLifetime = null;
            debugLifetime?.Cancel();
            debugLifetime?.Dispose();
            debugLifetime = null;
        }

        public void Dispose()
        {
            InputDeviceState.Changed -= RefreshInput;
            AttachInput(null);
            CancelOverlayRequests();
            foreach (var view in new[]
            {
                helpView,
                debugView
            }

            )
                if (view != null)
                    UIManager.Instance.CloseAsync(view, false).Forget();
            helpView = debugView = null;
            viewData = null;
            if (debugRenderer != null)
                debugRenderer.enabled = false;
            debugRenderer = null;
            start = null;
            back = null;
        }
    }
}
