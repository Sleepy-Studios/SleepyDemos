using System.Collections.Generic;
using Core.Runtime;

namespace Hotfix.KagitKart
{
    /// 页面只读取场景持有的唯一比赛，不复制圈数、道具或积分。
    public sealed class KagitKartData : IData
    {
        public List<IHandler> Handlers { get; } = new() { new KagitKartHandler() };
        public KagitKartWorld World { get; internal set; }
        public KagitKartSession Session => World?.Session;

        public void ClearData() => World = null;
    }
}
