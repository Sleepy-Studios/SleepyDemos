using Core.Runtime;
using Core.Runtime.Rendering.Streamline;

namespace Hotfix
{
    /// 将公共渲染服务的真实状态发布给页面。
    public sealed class GraphicsSettingsHandler : HandlerBase<GraphicsSettingsSetModeAction, GraphicsSettingsData>
    {
        protected override void OnInit()
        {
            StreamlineRuntime.Changed += Refresh;
            Refresh();
        }

        /// <summary>
        /// 处理本模块业务命令，按实际服务与规则结果发布状态。
        /// </summary>
        /// <param name="action">当前模块的业务请求。</param>
        protected override void Reduce(GraphicsSettingsSetModeAction action) => StreamlineRuntime.SetMode(action.Mode);

        private void Refresh()
        {
            State.RequestedMode = StreamlineRuntime.RequestedMode;
            State.EffectiveMode = StreamlineRuntime.EffectiveMode;
            State.IsBusy = StreamlineRuntime.IsBusy;
            State.Status = StreamlineRuntime.Status;
            State.InputSize = StreamlineRuntime.InputSize;
            State.OutputSize = StreamlineRuntime.OutputSize;
            ApplyState();
        }
    }
}
