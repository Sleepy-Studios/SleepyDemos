using Core.Runtime;

namespace Hotfix.WallSqueeze
{
    /// 核对当前场景后处理流程，不把规则复制到 UI 状态。
    public sealed class WallSqueezeHandler : HandlerBase<WallSqueezeAction, WallSqueezeData>
    {
        /// <summary>同步流程操作完成后发布当前会话。</summary>
        /// <param name="action">带来源的业务请求。</param>
        protected override void Reduce(WallSqueezeAction action)
        {
            if (State.World == null || State.World != action.Owner)
            {
                return;
            }
            State.World.ApplyCommand(action.Command);
            ApplyState();
        }
    }
}
