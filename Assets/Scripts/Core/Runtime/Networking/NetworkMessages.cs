using System;

namespace Core.Runtime.Networking
{
    /// 可靠业务命令；Core 不解释 Payload。同成员同通道 Sequence 在一次会话内递增。
    public sealed class NetworkCommand
    {
        private readonly byte[] payload;

        /// <summary>创建业务命令并复制载荷。</summary>
        /// <param name="channel">业务协议中的命令通道。</param>
        /// <param name="sequence">当前成员当前通道的非零序号。</param>
        /// <param name="payload">业务字节，构造后修改源数组不影响命令。</param>
        public NetworkCommand(ushort channel, ulong sequence, byte[] payload)
        {
            if (sequence == 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            Channel = channel;
            Sequence = sequence;
            this.payload = (byte[])payload.Clone();
        }
        /// 由业务协议约定的命令通道。
        public ushort Channel { get; }
        /// 当前成员当前通道的非零递增序号。
        public ulong Sequence { get; }
        /// 返回独立的业务载荷副本，防止调用方改变已发送命令。
        public byte[] Payload => (byte[])payload.Clone();
    }

    /// 权威接收事件；SenderMemberId 必须来自传输层，不得从业务载荷采信。
    public readonly struct NetworkCommandEvent
    {
        /// <summary>由传输层构造已确认来源的接收事件。</summary>
        /// <param name="senderMemberId">从 SDK 得到的发送成员，不从载荷读取。</param>
        /// <param name="authorityEpoch">接收时的非零权威代次。</param>
        /// <param name="command">已接收的命令。</param>
        public NetworkCommandEvent(string senderMemberId, ulong authorityEpoch, NetworkCommand command)
        {
            if (string.IsNullOrWhiteSpace(senderMemberId)) throw new ArgumentException("发送成员不能为空。", nameof(senderMemberId));
            if (authorityEpoch == 0) throw new ArgumentOutOfRangeException(nameof(authorityEpoch));
            SenderMemberId = senderMemberId;
            AuthorityEpoch = authorityEpoch;
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }
        /// 可信发送方身份。
        public string SenderMemberId { get; }
        /// 接收命令时的权威代次。
        public ulong AuthorityEpoch { get; }
        /// 业务命令。
        public NetworkCommand Command { get; }
    }

    /// 一个通道的完整共享状态，不是增量；新权威必须能从保留快照恢复。
    public sealed class NetworkSnapshot
    {
        private readonly byte[] payload;

        /// <summary>创建完整共享快照并复制载荷。</summary>
        /// <param name="channel">业务状态通道。</param>
        /// <param name="sequence">非零序号，交接后继续在最新值上递增。</param>
        /// <param name="payload">该通道的完整状态，不能只包含增量。</param>
        public NetworkSnapshot(ushort channel, ulong sequence, byte[] payload)
        {
            if (sequence == 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (payload == null) throw new ArgumentNullException(nameof(payload));
            Channel = channel;
            Sequence = sequence;
            this.payload = (byte[])payload.Clone();
        }
        /// 业务快照通道。
        public ushort Channel { get; }
        /// 当前通道序号，权威交接后也必须在原最新值上继续递增。
        public ulong Sequence { get; }
        /// 独立的完整状态载荷副本。
        public byte[] Payload => (byte[])payload.Clone();
    }

    /// 带来源权威的快照事件。
    public readonly struct NetworkSnapshotEvent
    {
        /// <summary>由传输层构造带权威来源的快照事件。</summary>
        /// <param name="authorityMemberId">SDK 确认的发布权威成员。</param>
        /// <param name="authorityEpoch">非零权威代次。</param>
        /// <param name="snapshot">完整共享状态。</param>
        public NetworkSnapshotEvent(string authorityMemberId, ulong authorityEpoch, NetworkSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(authorityMemberId)) throw new ArgumentException("权威成员不能为空。", nameof(authorityMemberId));
            if (authorityEpoch == 0) throw new ArgumentOutOfRangeException(nameof(authorityEpoch));
            AuthorityMemberId = authorityMemberId;
            AuthorityEpoch = authorityEpoch;
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
        /// 传输层确认的权威成员。
        public string AuthorityMemberId { get; }
        /// 发布时的权威代次。
        public ulong AuthorityEpoch { get; }
        /// 完整业务快照。
        public NetworkSnapshot Snapshot { get; }
    }
}
