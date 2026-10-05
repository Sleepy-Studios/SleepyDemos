using Core.Runtime;
using Core.Runtime.Rendering.Streamline;

namespace Hotfix
{
    /// 将公共渲染服务的真实状态发布给页面。
    public sealed class GraphicsSettingsHandler : HandlerBase<GraphicsSettingsSetModeAction, GraphicsSettingsData>
    {
        protected override void OnInit() { StreamlineRuntime.Changed += Refresh; Refresh(); }
        protected override void Reduce(GraphicsSettingsSetModeAction action) => StreamlineRuntime.SetMode(action.Mode);
        private void Refresh()
        {
            State.RequestedMode=StreamlineRuntime.RequestedMode; State.EffectiveMode=StreamlineRuntime.EffectiveMode;
            State.IsBusy=StreamlineRuntime.IsBusy; State.Status=StreamlineRuntime.Status;
            State.InputSize=StreamlineRuntime.InputSize; State.OutputSize=StreamlineRuntime.OutputSize;
            ApplyState();
        }
    }
}
