using Core.Runtime;

namespace Hotfix.WallSqueeze
{
    /// 场景流程命令。
    public enum WallSqueezeCommand
    {
        Pause,
        Continue,
        Retry,
        Next,
        Hub,
        Refresh
    }

    /// 当前 Demo 的流程请求；旧场景来源会被拒绝。
    public sealed class WallSqueezeAction : IAction
    {
        /// 请求所属的场景来源。
        public WallSqueezeWorld Owner;
        /// 本次业务命令。
        public WallSqueezeCommand Command;

        /// <summary>构造后经 GlobalData.Dispatch 处理。</summary>
        /// <param name="owner">请求所属的活跃场景。</param>
        /// <param name="command">暂停、继续、重试、下一关或返回 Hub。</param>
        public WallSqueezeAction(WallSqueezeWorld owner, WallSqueezeCommand command)
        {
            Owner = owner;
            Command = command;
        }
    }
}
