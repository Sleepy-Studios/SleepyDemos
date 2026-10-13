namespace Core.Runtime.Networking
{
    /// 主线程预测规则桥；Core 只传递字节，不解释车手、道具或比赛规则。
    public interface INetworkSimulation
    {
        /// <summary>读取拥有者本步输入；短按应在读取后消耗。</summary>
        /// <returns>独立输入，最多 1 KiB；没有输入时返回 null。</returns>
        byte[] ReadInput();

        /// <summary>接收官方 Replicate 本步或重放；服务器这里只收集输入，共享规则在 AuthorityTick 推进一次。</summary>
        /// <param name="memberId">来自 NetworkObject.Owner 的可信身份。</param>
        /// <param name="tick">服务器应用 tick；客户端为官方输入或重放 tick，不能跨时钟直接相减。</param>
        /// <param name="dt">固定 1/120 秒。</param>
        /// <param name="input">输入字节；缺包时可能为空。</param>
        /// <param name="replay">官方预测是否正在重放，不能重复产生表现或存档副作用。</param>
        /// <param name="isAuthority">是否为服务器，Host 也只进入权威路径。</param>
        void Replicate(string memberId, uint tick, float dt, byte[] input, bool replay, bool isAuthority);

        /// <summary>服务器每 tick 收完各连接输入后，仅推进一次完整世界。</summary>
        /// <param name="tick">服务器 tick。</param>
        /// <param name="dt">固定 1/120 秒。</param>
        void AuthorityTick(uint tick, float dt);

        /// <summary>捕获指定拥有者完整校正状态，包含会影响后续模拟的隐藏字段。</summary>
        /// <param name="memberId">被校正的连接身份。</param>
        /// <returns>独立状态，最多 256 KiB；空值表示尚未开始模拟。</returns>
        byte[] CaptureState(string memberId);

        /// <summary>应用官方 Reconcile 后，由 FishNet 回放已保留输入。</summary>
        /// <param name="memberId">拥有者连接身份。</param>
        /// <param name="tick">官方校正 tick。</param>
        /// <param name="state">完整状态；不能在回调后修改该数组。</param>
        void Reconcile(string memberId, uint tick, byte[] state);
    }

    /// 公共传输边界的最大单消息尺寸，不由业务载荷决定。
    public static class NetworkPayloadLimits
    {
        /// 每 tick 输入上限。
        public const int Input = 1024;
        /// 完整校正与共享快照上限。
        public const int State = 256 * 1024;
        /// 可靠业务命令上限。
        public const int Command = 64 * 1024;
    }
}
