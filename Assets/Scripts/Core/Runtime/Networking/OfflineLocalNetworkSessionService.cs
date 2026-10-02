using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime.Networking
{
    /// 显式单人离线会话，供规则与 UI 验证使用；不连接互联网，也不模拟远端成员。
    public sealed class OfflineLocalNetworkSessionService : INetworkSessionService
    {
        private readonly Dictionary<ushort, NetworkSnapshotEvent> snapshots = new Dictionary<ushort, NetworkSnapshotEvent>();
        private readonly Dictionary<ushort, ulong> commandSequences = new Dictionary<ushort, ulong>();
        private readonly ReadOnlyDictionary<ushort, NetworkSnapshotEvent> snapshotView;
        private IReadOnlyList<NetworkMemberInfo> members = Array.AsReadOnly(Array.Empty<NetworkMemberInfo>());
        private bool isChangingSession;

        public OfflineLocalNetworkSessionService()
        { snapshotView = new ReadOnlyDictionary<ushort, NetworkSnapshotEvent>(snapshots); }

        /// 始终是离线验证。
        public NetworkTransportKind TransportKind => NetworkTransportKind.Offline;
        /// 当前生命周期。
        public NetworkSessionState State { get; private set; }
        /// 当前离线会话，空闲时为空。
        public NetworkSessionInfo CurrentSession { get; private set; }
        /// 至多一个真实本地成员。
        public IReadOnlyList<NetworkMemberInfo> Members => members;
        /// 当前本地成员标识。
        public string LocalMemberId { get; private set; }
        /// 单人会话的权威即本地成员。
        public string AuthorityMemberId => LocalMemberId;
        /// 单人会话没有交接；创建时为一，离房时为零。
        public ulong AuthorityEpoch { get; private set; }
        /// 当前各通道完整状态。
        public IReadOnlyDictionary<ushort, NetworkSnapshotEvent> Snapshots => snapshotView;

        /// 生命周期变化。
        public event Action<NetworkSessionState> StateChanged;
        /// 会话信息变化。
        public event Action<NetworkSessionInfo> SessionChanged;
        /// 成员变化。
        public event Action<IReadOnlyList<NetworkMemberInfo>> MembersChanged;
        /// 权威变化。
        public event Action<NetworkAuthorityInfo> AuthorityChanged;
        /// 本地权威收到命令。
        public event Action<NetworkCommandEvent> CommandReceived;
        /// 完整状态发布。
        public event Action<NetworkSnapshotEvent> SnapshotReceived;
        /// 本地姿态更新。
        public event Action<NetworkAvatarPoseEvent> AvatarPoseReceived;

        /// <summary>创建仅含本地成员的离线会话，不验证区域网络可达性。</summary>
        /// <param name="options">MaxPlayers 必须为一；其它字段保留上层正式接口。</param>
        /// <param name="cancellationToken">提交前取消不会创建会话。</param>
        /// <returns>TransportKind 明确为 Offline 的会话信息。</returns>
        public UniTask<NetworkSessionInfo> CreateAsync(NetworkCreateOptions options, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (isChangingSession || State != NetworkSessionState.Idle) throw new InvalidOperationException("已有会话或正在切换。请先离房。");
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.MaxPlayers != 1) throw new ArgumentOutOfRangeException(nameof(options), "离线会话只允许一人。");
            var code = NetworkRoomCodeCodec.Generate(options.ProtocolVersion, options.Region);
            var info = new NetworkSessionInfo(code, options.ContentVersion, 1, NetworkTransportKind.Offline);
            string displayName = options.LocalDisplayName;
            isChangingSession = true;
            try
            {
                SetState(NetworkSessionState.Connecting);
                LocalMemberId = "offline-" + Guid.NewGuid().ToString("N");
                AuthorityEpoch = 1;
                CurrentSession = info;
                members = Array.AsReadOnly(new[] { new NetworkMemberInfo(LocalMemberId, displayName, true) });
                SetState(NetworkSessionState.Joined);
                Emit(SessionChanged, CurrentSession);
                Emit(MembersChanged, members);
                Emit(AuthorityChanged, new NetworkAuthorityInfo(AuthorityMemberId, AuthorityEpoch));
            }
            finally { isChangingSession = false; }
            return UniTask.FromResult(info);
        }

        /// <summary>离线实现拒绝加入房间，避免被误认为能够联机。</summary>
        /// <param name="options">正式入房参数；离线实现不消费。</param>
        /// <param name="cancellationToken">已取消时优先返回取消异常。</param>
        /// <returns>始终抛出 NotSupportedException。</returns>
        public UniTask<NetworkSessionInfo> JoinAsync(NetworkJoinOptions options, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            throw new NotSupportedException("OfflineLocalNetworkSessionService 不支持互联网入房。");
        }

        /// <summary>同步清空本地状态；清理开始后不受取消影响。</summary>
        /// <param name="cancellationToken">只在开始清理前检查。</param>
        public UniTask LeaveAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (isChangingSession) throw new InvalidOperationException("会话切换尚未完成。");
            if (State == NetworkSessionState.Idle) return UniTask.CompletedTask;
            isChangingSession = true;
            try
            {
                SetState(NetworkSessionState.Leaving);
                ClearSession();
                SetState(NetworkSessionState.Idle);
                Emit(SessionChanged, CurrentSession);
                Emit(MembersChanged, members);
                Emit(AuthorityChanged, new NetworkAuthorityInfo(null, 0));
            }
            finally { isChangingSession = false; }
            return UniTask.CompletedTask;
        }

        /// <summary>立即交给本地权威，重复或旧命令序号忽略。</summary>
        /// <param name="command">当前通道内递增的命令。</param>
        /// <param name="cancellationToken">提交前取消令牌。</param>
        public UniTask SendCommandAsync(NetworkCommand command, CancellationToken cancellationToken = default)
        {
            EnsureJoined();
            cancellationToken.ThrowIfCancellationRequested();
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (commandSequences.TryGetValue(command.Channel, out ulong latest) && command.Sequence <= latest)
                return UniTask.CompletedTask;
            commandSequences[command.Channel] = command.Sequence;
            Emit(CommandReceived, new NetworkCommandEvent(LocalMemberId, AuthorityEpoch, command));
            return UniTask.CompletedTask;
        }

        /// <summary>保留完整快照并派发；旧序号和重复序号拒绝发布。</summary>
        /// <param name="snapshot">当前通道内递增的完整状态。</param>
        /// <param name="cancellationToken">提交前取消令牌。</param>
        public UniTask PublishSnapshotAsync(NetworkSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            EnsureJoined();
            cancellationToken.ThrowIfCancellationRequested();
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshots.TryGetValue(snapshot.Channel, out var latest) && snapshot.Sequence <= latest.Snapshot.Sequence)
                throw new InvalidOperationException("快照序号必须在当前最新值上递增。");
            var received = new NetworkSnapshotEvent(AuthorityMemberId, AuthorityEpoch, snapshot);
            snapshots[snapshot.Channel] = received;
            Emit(SnapshotReceived, received);
            return UniTask.CompletedTask;
        }

        /// <summary>派发本地角色姿态，不创建相机或联网对象。</summary>
        /// <param name="pose">本地姿态。</param>
        public void PublishAvatarPose(NetworkAvatarPose pose)
        {
            EnsureJoined();
            Emit(AvatarPoseReceived, new NetworkAvatarPoseEvent(LocalMemberId, pose));
        }

        /// 幂等释放所有本地状态和事件订阅。
        public void Dispose()
        {
            if (State == NetworkSessionState.Disposed) return;
            if (isChangingSession) throw new InvalidOperationException("不能在会话生命周期回调中释放服务。");
            isChangingSession = true;
            try
            {
                ClearSession();
                SetState(NetworkSessionState.Disposed);
            }
            finally
            {
                StateChanged = null;
                SessionChanged = null;
                MembersChanged = null;
                AuthorityChanged = null;
                CommandReceived = null;
                SnapshotReceived = null;
                AvatarPoseReceived = null;
                isChangingSession = false;
            }
        }

        private void EnsureNotDisposed()
        {
            if (State == NetworkSessionState.Disposed) throw new ObjectDisposedException(nameof(OfflineLocalNetworkSessionService));
        }

        private void EnsureJoined()
        {
            EnsureNotDisposed();
            if (State != NetworkSessionState.Joined) throw new InvalidOperationException("尚未加入会话。");
        }

        private void ClearSession()
        {
            CurrentSession = null;
            LocalMemberId = null;
            AuthorityEpoch = 0;
            members = Array.AsReadOnly(Array.Empty<NetworkMemberInfo>());
            snapshots.Clear();
            commandSequences.Clear();
        }

        private void SetState(NetworkSessionState state)
        {
            State = state;
            Emit(StateChanged, state);
        }

        private static void Emit<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            // 订阅者异常不得截断会话收口或使其他订阅者漏掉状态事件。
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try { handler(value); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }
    }
}
