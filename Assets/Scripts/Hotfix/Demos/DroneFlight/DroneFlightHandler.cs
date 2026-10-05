using System;
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
        private DroneFlightUIController ui;
        internal DroneFlightHandler(Action<DroneVehicleKind> start, Action back) { this.start=start; this.back=back; }
        protected override void OnInit() { InputDeviceState.Changed += RefreshInput; RefreshInput(); }
        internal void ConfigureSelection(Action<DroneVehicleKind> start, Action back, DroneFlightUIController ui) { this.start=start; this.back=back; this.ui=ui; }
        internal void AttachInput(DronePlayerInput value, DroneFlightUIController controller=null)
        {
            if (controller != null) ui=controller;
            if (!ReferenceEquals(input,value))
            {
                if (input != null) { input.PanelChanged -= OnInputChanged; input.HelpChanged -= OnInputChanged; input.DebugChanged -= OnInputChanged; }
                input=value;
                if (input != null) { input.PanelChanged += OnInputChanged; input.HelpChanged += OnInputChanged; input.DebugChanged += OnInputChanged; }
            }
            RefreshInput();
        }
        private void OnInputChanged(bool value) => RefreshInput();
        private void RefreshInput()
        {
            State.DeviceKind=InputDeviceState.ActiveKind;
            State.PanelVisible=input != null && input.IsPanelOpen;
            State.ControlsSuppressed=input != null && (input.IsHelpOpen || input.IsDebugOpen);
            ApplyState();
        }
        internal void SetFeedback(string value) { State.Feedback=value; if(State.Mode==DroneFlightSessionMode.Loading) State.Mode=DroneFlightSessionMode.Selecting; ApplyState(); }
        internal void SetShuttingDown(bool value)
        {
            State.ShuttingDown=value;
            if(value) { State.HelpRequested=State.DebugRequested=false; input?.SetHelpOpen(false); input?.SetDebugOpen(false); }
            ApplyState();
        }
        internal void BeginLeaving() { State.Mode=DroneFlightSessionMode.Leaving; ApplyState(); }
        internal void SetDebugVisible(bool value) { State.DebugVisible=value; State.DebugRequested=value; input?.SetDebugOpen(value); ApplyState(); }
        internal void SetHelpRequested(bool value) { State.HelpRequested=value; input?.SetHelpOpen(value); ApplyState(); }
        internal void SetDebugDraw(bool value) { State.DebugDrawVisible=value; ApplyState(); }
        internal void RestoreMode(bool active) { State.Mode=active ? DroneFlightSessionMode.Active : DroneFlightSessionMode.Selecting; ApplyState(); }
        protected override void Reduce(DroneFlightAction action)
        {
            switch(action)
            {
                case DroneFlightSelectAction select:
                    if (State.Mode!=DroneFlightSessionMode.Selecting || (uint)select.Kind>2) return;
                    State.SelectedKind=select.Kind; State.Feedback=null; break;
                case DroneFlightStartAction:
                    if(State.Mode!=DroneFlightSessionMode.Selecting || start==null) return;
                    State.Version++; State.Mode=DroneFlightSessionMode.Loading; State.Feedback=null; ApplyState(); start(State.SelectedKind); return;
                case DroneFlightSelectionResultAction result:
                    if(result.Version!=State.Version || State.Mode!=DroneFlightSessionMode.Loading) return;
                    State.Mode=result.Succeeded ? DroneFlightSessionMode.Active : DroneFlightSessionMode.Selecting; State.Feedback=result.Message; break;
                case DroneFlightExitAction:
                    if(State.Mode is DroneFlightSessionMode.Loading or DroneFlightSessionMode.Leaving || back==null) return;
                    State.Mode=DroneFlightSessionMode.Leaving; ApplyState(); back(); return;
                case DroneFlightTelemetryAction telemetry:
                    if(telemetry.SessionId!=State.SessionId) return;
                    State.Snapshot=telemetry.Snapshot; State.HasSnapshot=true; break;
                case DroneFlightHoldAction hold:
                    if(hold.Command=="ArmOrReset") input?.SetTouchArmHeld(hold.Held);
                    if(hold.Command=="ReelIn") input?.SetTouchLine(hold.Held ? -1 : 0);
                    if(hold.Command=="ReelOut") input?.SetTouchLine(hold.Held ? 1 : 0);
                    return;
                case DroneFlightControlAction control:
                    switch(control.Command)
                    {
                        case "Help": SetHelpRequested(!State.HelpRequested); ui?.SynchronizeHelp(); return;
                        case "DebugPanel": if(ui?.DebugChanging==true) return; State.DebugRequested=!State.DebugVisible; input?.SetDebugOpen(State.DebugRequested); ApplyState(); ui?.SynchronizeDebug(); return;
                        case "DebugDraw": State.DebugDrawVisible=!State.DebugDrawVisible; ApplyState(); ui?.ApplyDebugDraw(); return;
                        case "CopyTelemetry": input?.GetComponent<DroneTelemetryRecorder>()?.CopySummary(); return;
                        case "ViewModifier": State.TouchLook=!State.TouchLook; input?.SetTouchLookMode(State.TouchLook); break;
                        case "ClosePanel": input?.SetPanelOpen(false); return;
                        default: if(control.Command!="ReelIn" && control.Command!="ReelOut") input?.Execute(control.Command); return;
                    }
                    break;
            }
            ApplyState();
        }
        public void Dispose() { InputDeviceState.Changed -= RefreshInput; AttachInput(null); ui=null; start=null; back=null; }
    }
}
