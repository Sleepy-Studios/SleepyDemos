using Core.Runtime;

namespace Hotfix.KagitKart
{
    public sealed class KagitKartHandler : HandlerBase<KagitKartAction, KagitKartData>
    {
        /// <summary>核对场景来源后处理流程，并同步回显当前会话。</summary>
        /// <param name="action">页面或输入提交的本场命令。</param>
        protected override void Reduce(KagitKartAction action)
        {
            if (State.World == null || State.World != action.Owner)
            {
                return;
            }
            State.World.ApplyCommand(action);
            ApplyState();
        }
    }
}
