using Core.Runtime;
using Core.Runtime.Rendering.Streamline;
using Core.Runtime.Settings;
using Cysharp.Threading.Tasks;

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
        protected override void Reduce(GraphicsSettingsSetModeAction action)
        {
            ApplyModeAsync(action).Forget();
        }

        private static async UniTask ApplyModeAsync(GraphicsSettingsSetModeAction action)
        {
            var candidate = GameSettingsService.Current;
            candidate.Graphics.DlssMode = action.Mode.HasValue ? (int)action.Mode.Value : 0;
            var result = await GameSettingsService.ApplyAsync(candidate);
            if (!result.Succeeded && !result.Canceled)
            {
                UnityEngine.Debug.LogWarning(result.Message);
            }
        }

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
