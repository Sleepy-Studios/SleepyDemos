using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using Channel = FishNet.Transporting.Channel;

namespace Core.Runtime.Networking
{
    /// 单房间直连 Host；连接、序列、可信身份与官方预测载体归 AOT，规则归业务。
    public sealed class FishNetNetworkSessionService : INetworkSessionService
    {
        private const string HostAddress = "LoadResources/Prefabs/Networking/SessionHost";
        private readonly string defaultContentVersion;
        private readonly ushort defaultProtocol;
        private readonly Dictionary<int, string> serverMembers = new();
        private readonly Dictionary<int, NetworkObject> bridges = new();
        private readonly Dictionary<int, SessionHello> pendingHellos = new();
        private readonly HashSet<FishNetPredictionBridge> predictionBridges = new();
        private readonly HashSet<int> rejectingMembers = new();
        private readonly Dictionary<(int, ushort), ulong> commandSequences = new();
        private readonly Dictionary<ushort, NetworkSnapshotEvent> snapshots = new();
        private readonly Dictionary<ushort, SessionSnapshot> pendingSnapshots = new();
        private readonly ReadOnlyDictionary<ushort, NetworkSnapshotEvent> snapshotView;
        private IResourceLoader loader;
        private NetworkManager manager;
        private FishNetSessionRuntime runtime;
        private UniTaskCompletionSource<NetworkSessionInfo> joined;
        private CancellationTokenSource connectionCancellation;
        private Func<bool> admissionRule;
        private IReadOnlyList<NetworkMemberInfo> members = Array.AsReadOnly(Array.Empty<NetworkMemberInfo>());
        private HashSet<int> memberIds = new();
        private string endpoint, contentVersion, displayName;
        private ushort port, protocol;
        private int capacity;
        private bool changing, shuttingDown;
        private uint lastAuthorityTick = uint.MaxValue;

        /// <summary>保存公开配置，实际 SDK 对象在 Create/Join 内异步加载。</summary>
        /// <param name="settings">协议和内容版本。</param>
        public FishNetNetworkSessionService(NetworkSessionSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }
            defaultContentVersion = settings.ContentVersion;
            defaultProtocol = settings.ProtocolVersion;
            snapshotView = new ReadOnlyDictionary<ushort, NetworkSnapshotEvent>(snapshots);
        }

        /// 实际直连传输，失败不回退离线。
        public NetworkTransportKind TransportKind => NetworkTransportKind.Internet;
        /// 当前会话生命周期。
        public NetworkSessionState State { get; private set; }
        /// 已完成握手的直连会话。
        public NetworkSessionInfo CurrentSession { get; private set; }
        /// 经 Host 验证的成员列表。
        public IReadOnlyList<NetworkMemberInfo> Members => members;
        /// 本地 Connection.ClientId 的字符串形式。
        public string LocalMemberId { get; private set; }
        /// 当前进程执行服务器规则，包含 Host。
        public bool IsAuthority => manager != null && manager.IsServerStarted && !shuttingDown;
        /// 创建 Host 的本地成员身份；本版不做权威迁移。
        public string AuthorityMemberId { get; private set; }
        /// 本次会话的固定权威代次。
        public ulong AuthorityEpoch => State == NetworkSessionState.Joined ? 1UL : 0;
        /// 各通道最新完整共享状态。
        public IReadOnlyDictionary<ushort, NetworkSnapshotEvent> Snapshots => snapshotView;
        internal INetworkSimulation Simulation { get; private set; }

        /// 生命周期变化。
        public event Action<NetworkSessionState> StateChanged;
        /// 握手成功或退出后的会话变化。
        public event Action<NetworkSessionInfo> SessionChanged;
        /// 成员加入或退出。
        public event Action<IReadOnlyList<NetworkMemberInfo>> MembersChanged;
        /// Host 身份或退出时的空权威。
        public event Action<NetworkAuthorityInfo> AuthorityChanged;
        /// 可信连接提交的新可靠命令。
        public event Action<NetworkCommandEvent> CommandReceived;
        /// 已保留的最新完整快照。
        public event Action<NetworkSnapshotEvent> SnapshotReceived;
        /// 可信连接提交的角色姿态。
        public event Action<NetworkAvatarPoseEvent> AvatarPoseReceived;

        /// <inheritdoc />
        public void RegisterSimulation(INetworkSimulation simulation)
        {
            EnsureNotDisposed();
            if (ReferenceEquals(Simulation, simulation))
            {
                return;
            }
            foreach (var bridge in predictionBridges)
            {
                bridge.ClearReplicateCache();
            }
            Simulation = simulation;
        }

        /// <inheritdoc />
        public void SetAdmissionRule(Func<bool> canJoin)
        {
            EnsureNotDisposed();
            admissionRule = canJoin;
        }

        internal void RegisterBridge(FishNetPredictionBridge bridge) => predictionBridges.Add(bridge);
        internal void UnregisterBridge(FishNetPredictionBridge bridge) => predictionBridges.Remove(bridge);

        /// <inheritdoc />
        public async UniTask<NetworkSessionInfo> CreateAsync(NetworkCreateOptions options, CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            if (changing || State != NetworkSessionState.Idle)
            {
                throw new InvalidOperationException("请先离开当前会话。");
            }
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            if (options.MaxPlayers < 0 || options.MaxPlayers > 8)
            {
                throw new ArgumentOutOfRangeException(nameof(options.MaxPlayers));
            }
            // 传输预留八个连接，具体房间容量与开赛门闩由即时准入规则判断。
            capacity = 8;
            return await ConnectAsync(true, options.Address, options.Port,
                options.ProtocolVersion == 0 ? defaultProtocol : options.ProtocolVersion,
                options.ContentVersion ?? defaultContentVersion, options.LocalDisplayName, cancellationToken);
        }

        /// <inheritdoc />
        public UniTask<NetworkSessionInfo> JoinAsync(NetworkJoinOptions options, CancellationToken cancellationToken = default)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }
            return ConnectAsync(false, options.Address, options.Port, options.ProtocolVersion,
                options.ContentVersion ?? defaultContentVersion, options.DisplayName, cancellationToken);
        }

        private async UniTask<NetworkSessionInfo> ConnectAsync(bool host, string address, ushort selectedPort,
            ushort selectedProtocol, string version, string name, CancellationToken cancellationToken)
        {
            EnsureNotDisposed();
            if (changing || State != NetworkSessionState.Idle)
            {
                throw new InvalidOperationException("请先离开当前会话。");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(address) || address.Length > 255)
            {
                throw new ArgumentException("请输入 Host 的 IP 或域名。", nameof(address));
            }
            if (selectedPort == 0 || selectedProtocol == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(selectedPort));
            }
            if (string.IsNullOrWhiteSpace(version) || version.Length > 128)
            {
                throw new ArgumentException("内容版本不能为空或过长。", nameof(version));
            }
            if (name?.Length > 32)
            {
                throw new ArgumentException("玩家名称最多32个字符。", nameof(name));
            }
            endpoint = address.Trim();
            port = selectedPort;
            protocol = selectedProtocol;
            contentVersion = version;
            displayName = name ?? string.Empty;
            changing = true;
            shuttingDown = false;
            joined = new UniTaskCompletionSource<NetworkSessionInfo>();
            connectionCancellation = new CancellationTokenSource();
            SetState(NetworkSessionState.Connecting);
            try
            {
                loader = ResourceServices.CreateLoader();
                var hostObject = await loader.InstantiateAsync(HostAddress, null);
                cancellationToken.ThrowIfCancellationRequested();
                if (hostObject == null)
                {
                    throw new InvalidOperationException("公共网络宿主加载失败。");
                }
                manager = hostObject.GetComponent<NetworkManager>();
                runtime = hostObject.GetComponent<FishNetSessionRuntime>();
                if (manager == null || runtime?.PredictionPrefab == null || !manager.Initialized)
                {
                    throw new InvalidOperationException("公共网络宿主或预测 Prefab 尚未正确装配。");
                }
                if (!runtime.PredictionPrefab.EnablePrediction || runtime.PredictionPrefab.GetComponent<FishNetPredictionBridge>() == null)
                {
                    throw new InvalidOperationException("预测载体必须启用 FishNet Prediction 并挂载 AOT Bridge。");
                }
                runtime.Service = this;
                manager.TimeManager.SetTickRate(120);
                manager.TransportManager.Transport.SetMaximumClients(8);
                Subscribe();
                if (host && !manager.ServerManager.StartConnection(port))
                {
                    throw new InvalidOperationException("无法启动 Host，请检查端口是否占用。");
                }
                if (!manager.ClientManager.StartConnection(host ? "127.0.0.1" : endpoint, port))
                {
                    throw new InvalidOperationException("无法开始连接 Host。");
                }
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                using var timeoutTimer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(15));
                try
                {
                    return await joined.Task.AttachExternalCancellation(timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException("连接 Host 超时，请检查地址、端口和网络可达性。");
                }
            }
            catch
            {
                await ShutdownAsync();
                SetState(NetworkSessionState.Idle);
                throw;
            }
            finally
            {
                changing = false;
            }
        }

        private void Subscribe()
        {
            manager.ClientManager.OnAuthenticated += OnAuthenticated;
            manager.ClientManager.OnClientConnectionState += OnClientState;
            manager.ServerManager.OnServerConnectionState += OnServerState;
            manager.ServerManager.OnRemoteConnectionState += OnRemoteState;
            manager.SceneManager.OnClientLoadedStartScenes += OnLoadedStartScenes;
            manager.ClientManager.RegisterBroadcast<SessionWelcome>(OnWelcome);
            manager.ClientManager.RegisterBroadcast<SessionSnapshot>(OnSnapshot);
            manager.ClientManager.RegisterBroadcast<SessionAvatar>(OnAvatar);
            manager.ServerManager.RegisterBroadcast<SessionHello>(OnHello);
            manager.ServerManager.RegisterBroadcast<SessionCommand>(OnCommand);
            manager.ServerManager.RegisterBroadcast<SessionAvatar>(OnAvatarCommand);
            // 先注册共享世界推进，后来 Spawn 的载体在其后捕获 Reconcile。
            manager.TimeManager.OnPostTick += OnAuthorityTick;
        }

        private void Unsubscribe()
        {
            if (manager == null)
            {
                return;
            }
            manager.ClientManager.OnAuthenticated -= OnAuthenticated;
            manager.ClientManager.OnClientConnectionState -= OnClientState;
            manager.ServerManager.OnServerConnectionState -= OnServerState;
            manager.ServerManager.OnRemoteConnectionState -= OnRemoteState;
            manager.SceneManager.OnClientLoadedStartScenes -= OnLoadedStartScenes;
            manager.ClientManager.UnregisterBroadcast<SessionWelcome>(OnWelcome);
            manager.ClientManager.UnregisterBroadcast<SessionSnapshot>(OnSnapshot);
            manager.ClientManager.UnregisterBroadcast<SessionAvatar>(OnAvatar);
            manager.ServerManager.UnregisterBroadcast<SessionHello>(OnHello);
            manager.ServerManager.UnregisterBroadcast<SessionCommand>(OnCommand);
            manager.ServerManager.UnregisterBroadcast<SessionAvatar>(OnAvatarCommand);
            manager.TimeManager.OnPostTick -= OnAuthorityTick;
        }

        private void OnAuthenticated()
        {
            if (shuttingDown)
            {
                return;
            }
            LocalMemberId = manager.ClientManager.Connection.ClientId.ToString();
            if (IsAuthority)
            {
                AuthorityMemberId = LocalMemberId;
            }
            manager.ClientManager.Broadcast(new SessionHello { Protocol = protocol, ContentVersion = contentVersion, DisplayName = displayName });
        }

        private void OnHello(NetworkConnection connection, SessionHello hello, Channel channel)
        {
            if (shuttingDown || State == NetworkSessionState.Faulted || !connection.IsAuthenticated || rejectingMembers.Contains(connection.ClientId))
            {
                return;
            }
            if (hello.Protocol != protocol || hello.ContentVersion != contentVersion || hello.DisplayName?.Length > 32)
            {
                Reject(connection, "协议、内容版本或名称不兼容。");
                return;
            }
            if (serverMembers.ContainsKey(connection.ClientId))
            {
                return;
            }
            if (!connection.LoadedStartScenes(true))
            {
                // 官方场景握手完成后才准入并 Spawn，尚未确认的连接不占业务成员。
                pendingHellos[connection.ClientId] = hello;
                return;
            }
            if (serverMembers.Count >= capacity)
            {
                Reject(connection, "房间人数已满。");
                return;
            }
            bool allowed;
            try
            {
                allowed = admissionRule?.Invoke() ?? true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                Reject(connection, "房间当前无法加入，请稍后重试。");
                return;
            }
            if (!allowed)
            {
                Reject(connection, "房间人数已满或比赛已开始。");
                return;
            }
            var instance = manager.GetPooledInstantiated(runtime.PredictionPrefab, asServer: true);
            if (instance == null)
            {
                Reject(connection, "预测载体未加入 SpawnablePrefabs。");
                return;
            }
            serverMembers.Add(connection.ClientId, hello.DisplayName ?? string.Empty);
            PublishMembers();
            manager.SceneManager.AddConnectionToScene(connection, instance.gameObject.scene);
            manager.ServerManager.Spawn(instance, connection);
            bridges.Add(connection.ClientId, instance);
            foreach (var snapshot in snapshots.Values)
            {
                manager.ServerManager.Broadcast(connection, ToWire(snapshot.Snapshot), channel: Channel.Reliable);
            }
        }

        private void OnLoadedStartScenes(NetworkConnection connection, bool asServer)
        {
            if (asServer && pendingHellos.Remove(connection.ClientId, out var hello))
            {
                OnHello(connection, hello, Channel.Reliable);
            }
        }

        private void PublishMembers()
        {
            var roster = serverMembers.OrderBy(pair => pair.Key).Select(pair => new SessionMember { Id = pair.Key, DisplayName = pair.Value }).ToArray();
            memberIds = new HashSet<int>(serverMembers.Keys);
            if (State == NetworkSessionState.Joined)
            {
                UpdateLocalMembers(roster);
            }
            foreach (int id in serverMembers.Keys)
            {
                if (!manager.ServerManager.Clients.TryGetValue(id, out var connection))
                {
                    continue;
                }
                manager.ServerManager.Broadcast(connection, new SessionWelcome
                {
                    Protocol = protocol, ContentVersion = contentVersion, Capacity = capacity, LocalId = id,
                    AuthorityId = int.TryParse(AuthorityMemberId, out int hostId) ? hostId : -1, Members = roster
                });
            }
        }

        private void OnWelcome(SessionWelcome welcome, Channel channel)
        {
            if (shuttingDown)
            {
                return;
            }
            if (!string.IsNullOrEmpty(welcome.Error))
            {
                Fail(new InvalidOperationException(welcome.Error));
                return;
            }
            int actualId = manager.ClientManager.Connection.ClientId;
            if (welcome.Protocol != protocol || welcome.ContentVersion != contentVersion || welcome.LocalId != actualId ||
                welcome.Capacity < 1 || welcome.Capacity > 8 || welcome.Members == null || welcome.Members.Length > welcome.Capacity ||
                !welcome.Members.Any(member => member.Id == actualId) || !welcome.Members.Any(member => member.Id == welcome.AuthorityId) ||
                welcome.Members.Any(member => member.Id < 0 || member.DisplayName?.Length > 32) || welcome.Members.Select(member => member.Id).Distinct().Count() != welcome.Members.Length)
            {
                Fail(new InvalidOperationException("Host 的会话信息无效或版本不兼容。"));
                return;
            }
            LocalMemberId = actualId.ToString();
            AuthorityMemberId = welcome.AuthorityId.ToString();
            capacity = welcome.Capacity;
            SessionMember[] roster = IsAuthority
                ? serverMembers.OrderBy(pair => pair.Key).Select(pair => new SessionMember { Id = pair.Key, DisplayName = pair.Value }).ToArray()
                : welcome.Members;
            memberIds = new HashSet<int>(roster.Select(member => member.Id));
            members = Array.AsReadOnly(roster.Select(member => new NetworkMemberInfo(member.Id.ToString(), member.DisplayName, member.Id == actualId)).ToArray());
            bool entering = State == NetworkSessionState.Connecting;
            CurrentSession ??= new NetworkSessionInfo(endpoint, port, contentVersion, capacity);
            if (entering)
            {
                SetState(NetworkSessionState.Joined);
                Emit(SessionChanged, CurrentSession);
                Emit(AuthorityChanged, new NetworkAuthorityInfo(AuthorityMemberId, 1));
            }
            Emit(MembersChanged, members);
            if (entering)
            {
                joined.TrySetResult(CurrentSession);
            }
        }

        private void UpdateLocalMembers(SessionMember[] roster)
        {
            members = Array.AsReadOnly(roster.Select(member => new NetworkMemberInfo(member.Id.ToString(), member.DisplayName,
                member.Id.ToString() == LocalMemberId)).ToArray());
            // 服务器立即更新业务人数，两个同帧握手不能共同占用最后一个名额。
            Emit(MembersChanged, members);
        }

        private void OnRemoteState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped || shuttingDown)
            {
                return;
            }
            rejectingMembers.Remove(connection.ClientId);
            pendingHellos.Remove(connection.ClientId);
            if (!serverMembers.Remove(connection.ClientId))
            {
                return;
            }
            if (bridges.Remove(connection.ClientId, out var bridge) && bridge != null && bridge.IsSpawned)
            {
                manager.ServerManager.Despawn(bridge);
            }
            foreach (var key in commandSequences.Keys.Where(key => key.Item1 == connection.ClientId).ToArray())
            {
                commandSequences.Remove(key);
            }
            PublishMembers();
        }

        private void OnClientState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped && !shuttingDown)
            {
                Fail(new InvalidOperationException("与 Host 的连接已断开。"));
            }
        }

        private void OnServerState(ServerConnectionStateArgs args)
        {
            if (args.ConnectionState == LocalConnectionState.Stopped && !shuttingDown)
            {
                Fail(new InvalidOperationException("Host 已停止。"));
            }
        }

        private void OnAuthorityTick()
        {
            if (!IsAuthority || State != NetworkSessionState.Joined)
            {
                return;
            }
            uint tick = manager.TimeManager.LocalTick;
            if (tick == lastAuthorityTick)
            {
                return;
            }
            lastAuthorityTick = tick;
            try
            {
                Simulation?.AuthorityTick(tick, (float)manager.TimeManager.TickDelta);
                if (tick % 4 != 0)
                {
                    return;
                }
                foreach (var snapshot in pendingSnapshots.Values)
                {
                    manager.ServerManager.Broadcast(snapshot, channel: snapshot.Reliable ? Channel.Reliable : Channel.Unreliable);
                }
                pendingSnapshots.Clear();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        }

        internal bool IsKnownMember(int id) => memberIds.Contains(id) && !rejectingMembers.Contains(id);

        internal void Reject(NetworkConnection connection, string error)
        {
            if (!rejectingMembers.Add(connection.ClientId))
            {
                return;
            }
            manager.ServerManager.Broadcast(connection, new SessionWelcome { Error = error });
            CompleteRejectionAsync(connection, manager, connectionCancellation.Token).Forget();
        }

        private async UniTaskVoid CompleteRejectionAsync(NetworkConnection connection, NetworkManager rejectingManager, CancellationToken cancellationToken)
        {
            int id = connection.ClientId;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            using var timeoutTimer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(2));
            try
            {
                // 让可靠失败消息先送到客户端；正常客户端会在收到后主动断开。
                await UniTask.WaitUntil(() => !connection.IsValid, cancellationToken: timeout.Token);
            }
            catch (OperationCanceledException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
            }
            if (manager == rejectingManager && manager != null &&
                manager.ServerManager.Clients.TryGetValue(id, out var current) && ReferenceEquals(current, connection))
            {
                connection.Kick(KickReason.ExploitAttempt);
            }
        }

        internal void Fail(Exception exception)
        {
            if (shuttingDown || State == NetworkSessionState.Faulted || State == NetworkSessionState.Disposed)
            {
                return;
            }
            Debug.LogWarning(exception.Message);
            joined?.TrySetException(exception);
            SetState(NetworkSessionState.Faulted);
            // State 已立即禁止规则推进；关闭传输必须等当前 SDK 收包调用栈退回。
            if (!shuttingDown && manager != null)
            {
                StopFaultedTransportAsync(manager).Forget(Debug.LogException);
            }
        }

        private async UniTask StopFaultedTransportAsync(NetworkManager faultedManager)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (manager != faultedManager || shuttingDown || State != NetworkSessionState.Faulted)
            {
                return;
            }
            manager.ClientManager.StopConnection();
            await WaitForStoppedAsync(asServer: false);
            if (manager != faultedManager || shuttingDown)
            {
                return;
            }
            manager.ServerManager.StopConnection(true);
            await WaitForStoppedAsync(asServer: true);
        }

        /// <inheritdoc />
        public UniTask SendCommandAsync(NetworkCommand command, CancellationToken cancellationToken = default)
        {
            EnsureJoined();
            cancellationToken.ThrowIfCancellationRequested();
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }
            byte[] payload = command.Payload;
            ValidatePayload(payload, NetworkPayloadLimits.Command);
            var message = new SessionCommand { Channel = command.Channel, Sequence = command.Sequence, Payload = payload };
            if (IsAuthority)
            {
                OnCommand(manager.ServerManager.Clients[int.Parse(LocalMemberId)], message, Channel.Reliable);
            }
            else
            {
                manager.ClientManager.Broadcast(message);
            }
            return UniTask.CompletedTask;
        }

        private void OnCommand(NetworkConnection connection, SessionCommand command, Channel channel)
        {
            if (State != NetworkSessionState.Joined || !IsKnownMember(connection.ClientId))
            {
                return;
            }
            if (command.Sequence == 0 || command.Payload == null || command.Payload.Length > NetworkPayloadLimits.Command)
            {
                Reject(connection, "命令载荷无效或超过上限。");
                return;
            }
            var key = (connection.ClientId, command.Channel);
            if (commandSequences.TryGetValue(key, out ulong latest) && command.Sequence <= latest)
            {
                return;
            }
            commandSequences[key] = command.Sequence;
            Emit(CommandReceived, new NetworkCommandEvent(connection.ClientId.ToString(), 1, new NetworkCommand(command.Channel, command.Sequence, command.Payload)));
        }

        /// <inheritdoc />
        public UniTask PublishSnapshotAsync(NetworkSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            EnsureJoined();
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsAuthority)
            {
                throw new InvalidOperationException("仅 Host 可以发布权威快照。");
            }
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }
            var wire = ToWire(snapshot);
            ValidatePayload(wire.Payload, NetworkPayloadLimits.State);
            if (snapshots.TryGetValue(snapshot.Channel, out var latest) && snapshot.Sequence <= latest.Snapshot.Sequence)
            {
                throw new InvalidOperationException("快照序号必须递增。");
            }
            // 同通道只保留本次30Hz发送前的最新完整状态，不积压过期快照。
            pendingSnapshots[snapshot.Channel] = wire;
            OnSnapshot(wire, Channel.Unreliable);
            return UniTask.CompletedTask;
        }

        private static SessionSnapshot ToWire(NetworkSnapshot snapshot) => new()
        { Channel = snapshot.Channel, Sequence = snapshot.Sequence, Payload = snapshot.Payload, Reliable = snapshot.Reliable };

        private void OnSnapshot(SessionSnapshot wire, Channel channel)
        {
            if (State != NetworkSessionState.Joined)
            {
                return;
            }
            if (wire.Sequence == 0 || wire.Payload == null || wire.Payload.Length > NetworkPayloadLimits.State)
            {
                Fail(new InvalidOperationException("权威快照载荷无效。"));
                return;
            }
            if (snapshots.TryGetValue(wire.Channel, out var latest) && wire.Sequence <= latest.Snapshot.Sequence)
            {
                return;
            }
            var snapshot = new NetworkSnapshotEvent(AuthorityMemberId, 1, new NetworkSnapshot(wire.Channel, wire.Sequence, wire.Payload, wire.Reliable));
            snapshots[wire.Channel] = snapshot;
            Emit(SnapshotReceived, snapshot);
        }

        /// <inheritdoc />
        public void PublishAvatarPose(NetworkAvatarPose pose)
        {
            EnsureJoined();
            var wire = new SessionAvatar { Position = pose.Position, Rotation = pose.Rotation, HeadPitch = pose.HeadPitch };
            if (!Finite(wire))
            {
                throw new ArgumentException("姿态包含非有限数值。", nameof(pose));
            }
            if (IsAuthority)
            {
                OnAvatarCommand(manager.ServerManager.Clients[int.Parse(LocalMemberId)], wire, Channel.Unreliable);
            }
            else
            {
                manager.ClientManager.Broadcast(wire, Channel.Unreliable);
            }
        }

        private void OnAvatarCommand(NetworkConnection connection, SessionAvatar wire, Channel channel)
        {
            if (State != NetworkSessionState.Joined || !IsKnownMember(connection.ClientId))
            {
                return;
            }
            if (!Finite(wire))
            {
                Reject(connection, "姿态包含非有限数值。");
                return;
            }
            wire.Id = connection.ClientId;
            manager.ServerManager.Broadcast(wire, channel: Channel.Unreliable);
        }

        private void OnAvatar(SessionAvatar wire, Channel channel)
        {
            if (State == NetworkSessionState.Joined && IsKnownMember(wire.Id) && Finite(wire))
            {
                Emit(AvatarPoseReceived, new NetworkAvatarPoseEvent(wire.Id.ToString(), new NetworkAvatarPose(wire.Position, wire.Rotation, wire.HeadPitch)));
            }
        }

        private static bool Finite(SessionAvatar pose) => Finite(pose.Position.x) && Finite(pose.Position.y) && Finite(pose.Position.z) &&
            Finite(pose.Rotation.x) && Finite(pose.Rotation.y) && Finite(pose.Rotation.z) && Finite(pose.Rotation.w) && Finite(pose.HeadPitch);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        /// <inheritdoc />
        public async UniTask LeaveAsync(CancellationToken cancellationToken = default)
        {
            EnsureNotDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            if (changing)
            {
                throw new InvalidOperationException("连接或退出尚未完成。");
            }
            if (State == NetworkSessionState.Idle)
            {
                return;
            }
            changing = true;
            SetState(NetworkSessionState.Leaving);
            try
            {
                await ShutdownAsync();
                SetState(NetworkSessionState.Idle);
            }
            finally
            {
                changing = false;
            }
        }

        private async UniTask ShutdownAsync()
        {
            shuttingDown = true;
            // Joined/失败回调可同步调用退出，此时 FishNet 仍在解析同一包的后续 Spawn。
            // 在主线程帧末收口，让收包与对象缓存迭代完整退出后再切换 Host 状态。
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            if (manager != null)
            {
                manager.ClientManager.StopConnection();
                await WaitForStoppedAsync(asServer: false);
                manager.ServerManager.StopConnection(true);
                await WaitForStoppedAsync(asServer: true);
            }
            Cleanup();
        }

        private async UniTask WaitForStoppedAsync(bool asServer)
        {
            var stoppingManager = manager;
            using var timeout = new CancellationTokenSource();
            using var timeoutTimer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(5));
            try
            {
                // Started 在 Stopping 已为 false；必须等待真正 Stopped，不能提前释放宿主。
                await UniTask.WaitUntil(() => stoppingManager == null ||
                    stoppingManager.TransportManager.Transport.GetConnectionState(asServer) == LocalConnectionState.Stopped,
                    cancellationToken: timeout.Token);
            }
            catch (OperationCanceledException)
            {
                SetState(NetworkSessionState.Faulted);
                throw new InvalidOperationException(asServer ? "Host 停止超时，请再次退出。" : "客户端停止超时，请再次退出。");
            }
        }

        /// 幂等释放 AOT 宿主、句柄及订阅；调用前须等待连接或退出完成。
        public void Dispose()
        {
            if (State == NetworkSessionState.Disposed)
            {
                return;
            }
            if (changing)
            {
                throw new InvalidOperationException("请等待连接或退出完成后再释放服务。");
            }
            if (manager != null)
            {
                throw new InvalidOperationException("请先等待 LeaveAsync 完成，再释放服务。");
            }
            shuttingDown = true;
            Cleanup();
            Simulation = null;
            SetState(NetworkSessionState.Disposed);
            StateChanged = null;
            SessionChanged = null;
            MembersChanged = null;
            AuthorityChanged = null;
            CommandReceived = null;
            SnapshotReceived = null;
            AvatarPoseReceived = null;
        }

        private void Cleanup()
        {
            connectionCancellation?.Cancel();
            connectionCancellation?.Dispose();
            connectionCancellation = null;
            admissionRule = null;
            Unsubscribe();
            if (runtime != null)
            {
                runtime.Service = null;
            }
            loader?.Dispose();
            loader = null;
            manager = null;
            runtime = null;
            bridges.Clear();
            pendingHellos.Clear();
            predictionBridges.Clear();
            rejectingMembers.Clear();
            serverMembers.Clear();
            memberIds.Clear();
            commandSequences.Clear();
            snapshots.Clear();
            pendingSnapshots.Clear();
            members = Array.AsReadOnly(Array.Empty<NetworkMemberInfo>());
            CurrentSession = null;
            LocalMemberId = AuthorityMemberId = null;
            lastAuthorityTick = uint.MaxValue;
            Emit(SessionChanged, null);
            Emit(MembersChanged, members);
            Emit(AuthorityChanged, new NetworkAuthorityInfo(null, 0));
        }

        internal static void ValidatePayload(byte[] payload, int limit, bool allowNull = false)
        {
            if (payload == null)
            {
                if (allowNull)
                {
                    return;
                }
                throw new ArgumentNullException(nameof(payload));
            }
            if (payload.Length > limit)
            {
                throw new ArgumentOutOfRangeException(nameof(payload), $"载荷超过{limit}字节上限。");
            }
        }

        private void EnsureNotDisposed()
        {
            if (State == NetworkSessionState.Disposed)
            {
                throw new ObjectDisposedException(nameof(FishNetNetworkSessionService));
            }
        }

        private void EnsureJoined()
        {
            EnsureNotDisposed();
            if (State != NetworkSessionState.Joined)
            {
                throw new InvalidOperationException("尚未连接到 Host。");
            }
        }

        private void SetState(NetworkSessionState state)
        {
            State = state;
            Emit(StateChanged, state);
        }

        private static void Emit<T>(Action<T> handlers, T value)
        {
            if (handlers == null)
            {
                return;
            }
            foreach (Action<T> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(value);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception);
                }
            }
        }
    }
}
