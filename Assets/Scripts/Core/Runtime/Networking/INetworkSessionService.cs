using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Core.Runtime.Networking
{
    /// 主线程会话边界；实现负责 SDK 生命周期与协议兼容，调用方负责解释业务载荷。
    public interface INetworkSessionService : IDisposable
    {
        /// 实际传输性质；必须向 UI 明确展示离线模式。
        NetworkTransportKind TransportKind { get; }
        /// 当前生命周期状态。
        NetworkSessionState State { get; }
        /// 已加入的房间；未加入时为空。
        NetworkSessionInfo CurrentSession { get; }
        /// 当前成员的只读视图；不得包含伪造的离线远端成员。
        IReadOnlyList<NetworkMemberInfo> Members { get; }
        /// 本地成员身份；未加入时为空。
        string LocalMemberId { get; }
        /// 当前权威成员身份；无权威时为空。
        string AuthorityMemberId { get; }
        /// 同一会话内递增的权威代次。
        ulong AuthorityEpoch { get; }
        /// 各通道最新完整快照；交接与迟加入后仍可读取，离房时清空。
        IReadOnlyDictionary<ushort, NetworkSnapshotEvent> Snapshots { get; }

        /// 生命周期发生变化。
        event Action<NetworkSessionState> StateChanged;
        /// 成功入房或离房后的会话信息；离房参数为空。
        event Action<NetworkSessionInfo> SessionChanged;
        /// 成员列表变化后的只读快照。
        event Action<IReadOnlyList<NetworkMemberInfo>> MembersChanged;
        /// 权威交接；实现必须恢复完整共享状态再接收新业务命令。
        event Action<NetworkAuthorityInfo> AuthorityChanged;
        /// 当前权威接收可靠命令，身份来自传输层；重复或旧序号不再次派发。
        event Action<NetworkCommandEvent> CommandReceived;
        /// 新的完整共享快照；保留数据先更新，旧序号不得覆盖最新值。
        event Action<NetworkSnapshotEvent> SnapshotReceived;
        /// 最新角色姿态；允许丢帧，不承担业务结算可靠性。
        event Action<NetworkAvatarPoseEvent> AvatarPoseReceived;

        /// <summary>创建并加入房间；失败或取消后不得遗留 SDK 会话。</summary>
        /// <param name="options">区域、协议、内容版本、容量与本地显示名。</param>
        /// <param name="cancellationToken">取消本次连接；实现仍须等待底层清理完成。</param>
        /// <returns>实际加入的房间信息，离线实现必须标示 Offline。</returns>
        UniTask<NetworkSessionInfo> CreateAsync(NetworkCreateOptions options, CancellationToken cancellationToken = default);

        /// <summary>按房间码加入，拒绝不兼容的协议或内容版本，不按平台隔离房间。</summary>
        /// <param name="options">房间码、内容版本与显示名。</param>
        /// <param name="cancellationToken">取消连接；底层会话必须完成清理。</param>
        /// <returns>实际加入的房间信息。</returns>
        UniTask<NetworkSessionInfo> JoinAsync(NetworkJoinOptions options, CancellationToken cancellationToken = default);

        /// <summary>离开房间并清空会话状态；空闲时幂等，开始清理后取消不得中断 SDK 收口。</summary>
        /// <param name="cancellationToken">只取消清理开始前的请求。</param>
        UniTask LeaveAsync(CancellationToken cancellationToken = default);

        /// <summary>可靠发送到当前权威；完成只表示传输层接纳，不表示业务结算成功。</summary>
        /// <param name="command">递增序号的业务载荷；传输层确定真实发送者身份。</param>
        /// <param name="cancellationToken">取消提交前的等待。</param>
        UniTask SendCommandAsync(NetworkCommand command, CancellationToken cancellationToken = default);

        /// <summary>发布并保留完整共享快照，仅当前权威可调用。</summary>
        /// <param name="snapshot">通道内比当前最新序号更大的完整状态，交接后也不得重置序号。</param>
        /// <param name="cancellationToken">取消提交前的等待。</param>
        UniTask PublishSnapshotAsync(NetworkSnapshot snapshot, CancellationToken cancellationToken = default);

        /// <summary>提交本地角色最新姿态；发送方由实现填写，允许网络丢帧。</summary>
        /// <param name="pose">位置、身体旋转和头部俯仰，不携带相机。</param>
        void PublishAvatarPose(NetworkAvatarPose pose);
    }
}
