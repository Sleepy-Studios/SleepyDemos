using System.Collections.Generic;
using Core.Runtime;

namespace Hotfix.WallSqueeze
{
    /// 场景会话的 UI 读取入口，规则只保存一份。
    public sealed class WallSqueezeData : IData
    {
        public List<IHandler> Handlers { get; } = new() { new WallSqueezeHandler() };
        /// 当前注册的场景会话。
        public WallSqueezeWorld World { get; internal set; }
        /// 读取场景唯一规则真源。
        public WallSqueezeSimulation Simulation => World?.Simulation;

        /// 释放当前场景来源。
        public void ClearData()
        {
            World = null;
        }
    }
}
