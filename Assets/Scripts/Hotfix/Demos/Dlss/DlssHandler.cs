using System;
using Core.Runtime;
using Core.Runtime.Inputs;

namespace Hotfix.Dlss
{
    internal sealed class DlssHandler : HandlerBase<DlssAction, DlssData>, IDisposable
    {
        private readonly DlssDemoController scene;
        internal DlssHandler(DlssDemoController scene) => this.scene=scene;
        protected override void OnInit() { InputDeviceState.Changed += RefreshDevice; RefreshDevice(); }
        private void RefreshDevice() { State.DeviceKind=InputDeviceState.ActiveKind; if(State.DeviceKind!=InputDeviceKind.Touch) State.Sprint=false; ApplyState(); }
        internal void Restore() { State.IsExiting=false; ApplyState(); }
        protected override void Reduce(DlssAction action)
        {
            switch(action)
            {
                case DlssSprintAction sprint:
                    if(State.Sprint==sprint.Held) return;
                    if(!sprint.Held || !State.IsExiting && !State.SettingsOpen) State.Sprint=sprint.Held; break;
                case DlssSettingsClosedAction:
                    if(!State.SettingsOpen) return; State.SettingsOpen=false; scene.RestoreControls(); break;
                case DlssControlAction control:
                    if(State.IsExiting || State.SettingsOpen) return;
                    switch(control.Command)
                    {
                        case "Reset": scene.ResetCamera(); break;
                        case "Settings": State.SettingsOpen=true; State.Sprint=false; ApplyState(); scene.ShowSettings(); return;
                        case "Exit": State.IsExiting=true; State.Sprint=false; ApplyState(); scene.ExitScene(); return;
                    }
                    break;
            }
            ApplyState();
        }
        public void Dispose() => InputDeviceState.Changed -= RefreshDevice;
    }
}
