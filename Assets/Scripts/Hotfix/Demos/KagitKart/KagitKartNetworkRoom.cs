using System;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Core.Runtime.Networking;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.KagitKart
{
    [Serializable]
    public sealed class KagitKartRoomPlayer
    {
        public string MemberId;
        public string Name;
        public string CharacterId;
        public bool Ready;
        public bool Loaded;
        public bool Connected = true;
        public double ResumeUntil;
    }

    [Serializable]
    public sealed class KagitKartRoomState
    {
        public string Name = "纸艺卡丁车房间";
        public int Capacity = 8;
        public KagitKartRaceMode Mode = KagitKartRaceMode.SingleRace;
        public int SpeedClass = 120;
        public string TrackId = "meadow";
        public string Phase = "Lobby";
        public string Message;
        public int RoundVersion;
        public uint Seed;
        public string[] Grid;
        public KagitKartRoomPlayer[] Players = Array.Empty<KagitKartRoomPlayer>();
        public string LeaderMemberId;
        public bool ServerLoaded;
        public int CupIndex;
        public bool CupRoundComplete;
        public int[] CupPoints, CupPlaces;
        public double[] CupTimes;
    }

    /// 卡丁车房间与网络规则；网络 SDK 和预测历史均由公共层负责。
    public sealed class KagitKartNetworkRoom : INetworkSimulation, IDisposable
    {
        private const ushort RoomChannel = 710;
        private const ushort RaceChannel = 711;
        private readonly KagitKartWorld world;
        private readonly KagitKartDedicatedServer server;
        private readonly RoomDirectoryClient directory;
        private RoomJoinGrant grant;
        private CancellationTokenSource reconnectCancellation;
        private bool reconnecting;
        private bool receivedRaceBaseline;
        private double reconnectDeadline;
        private bool preparingClient;
        private NetworkSnapshotEvent? deferredRaceSnapshot;
        private readonly CancellationToken token;
        private readonly Dictionary<string, KagitKartControls> inputs = new();
        private readonly Dictionary<string, uint> inputTicks = new();
        private readonly List<(string id, KagitKartVehicleSignal signal, KagitKartVehicle.SignalContext context)> vehicleEvents = new();
        private readonly List<KagitKartItemEvent> itemEvents = new();
        private INetworkSessionService service;
        private NetworkSessionSettings settings;
        private ulong commandSequence;
        private ulong roomSequence;
        private ulong raceSequence;
        private int preparedVersion = -1;
        private bool leaving;
        private bool disposed;
        private bool connecting;
        private CancellationTokenSource connectionCancellation;
        private UniTaskCompletionSource connectionCompleted;
        private UniTaskCompletionSource reconnectCompleted;
        private UniTaskCompletionSource preparationCompleted;
        private UniTaskCompletionSource leaveCompleted;
        private bool wasJoined;
        private double preparationDeadline;
        private string appliedPhase;
        private KagitKartSession subscribedSession;
        private CancellationTokenSource preparationCancellation;
        private int measuredSteps;
        private long measuredTicks, maximumStepTicks;

        [Serializable]
        private sealed class Request
        {
            public string Kind;
            public int RoundVersion;
            public bool Ready;
            public string CharacterId;
            public KagitKartRoomState Configuration;
        }

        public KagitKartRoomState Room { get; private set; }
        private bool IsAuthority => service?.IsAuthority == true;
        private KagitKartSession Session => world != null ? world.Session : server.Session;
        private KagitKartGrandPrix GrandPrix => world != null ? world.GrandPrix : server.GrandPrix;
        private IReadOnlyList<KagitKartCharacter> Roster => world != null ? world.Roster : server.Source.roster;
        public bool IsHost => Connected && Room?.LeaderMemberId == LocalMemberId;
        public string LocalMemberId => service?.LocalMemberId ?? grant?.memberId;
        public string AuthorityMemberId => Room?.LeaderMemberId;
        public string Code => grant?.code ?? server?.Configuration.code;
        public bool Reconnecting => reconnecting;
        /// 首次连接清理尚未成功；保留授权供离房重试，不能当作已取消。
        public bool ConnectionCleanupPending { get; private set; }
        public int ReconnectSeconds => Math.Max(0, (int)Math.Ceiling(reconnectDeadline - Time.realtimeSinceStartupAsDouble));
        public string Status { get; private set; }
        public bool Connected => service?.State == NetworkSessionState.Joined;
        public bool Racing => Connected && Room?.Phase == "Racing";
        public bool CanStart => IsHost && Room != null && (Room.Phase == "Lobby" || Room.Phase == "Results")
            && Room.Players.Count(p => p.Connected) >= 2 && Room.Players.Where(p => p.Connected).All(p => p.Ready);
        private bool CanAuthorityStart => Room != null && Room.Phase is "Lobby" or "Results"
            && Room.Players.Count(p => p.Connected) >= 2 && Room.Players.Where(p => p.Connected).All(p => p.Ready);

        /// <summary>绑定本 Demo，连接仅在明确创建或加入时启动。</summary>
        /// <param name="world">场景宿主。</param>
        /// <param name="token">场景退出令牌。</param>
        public KagitKartNetworkRoom(KagitKartWorld world, CancellationToken token)
        {
            this.world = world;
            this.token = token;
            directory = new RoomDirectoryClient(KagitKartOnlineSettings.Load().directoryUrl);
        }

        internal KagitKartNetworkRoom(KagitKartDedicatedServer server, CancellationToken token)
        {
            this.server = server;
            this.token = token;
        }

        private void InitializeService()
        {
            if (service != null) throw new InvalidOperationException("请先离开当前房间。");
            settings = ScriptableObject.CreateInstance<NetworkSessionSettings>();
            service = NetworkSessionServices.CreateInternet(settings);
            service.MembersChanged += MembersChanged;
            service.CommandReceived += ReceiveCommand;
            service.SnapshotReceived += ReceiveSnapshot;
            service.StateChanged += StateChanged;
            service.RegisterSimulation(this);
            if (server != null) service.SetAuthenticationRule(server.Authenticate);
            service.SetAdmissionRule(() => Room != null && Room.Phase == "Lobby" && Room.Players.Length < Room.Capacity);
        }

        internal async UniTask StartServerAsync()
        {
            Room = server.Configuration.configuration;
            Room.LeaderMemberId = server.Configuration.leaderMemberId;
            ValidateConfiguration(Room);
            InitializeService();
            await service.StartServerAsync(new NetworkServerOptions
            {
                Address = "0.0.0.0", Port = checked((ushort)server.Configuration.port),
                ProtocolVersion = checked((ushort)server.Configuration.protocolVersion),
                ContentVersion = server.Configuration.contentVersion, MaxPlayers = 8,
                ServerId = server.Configuration.roomId, HostPrefab = server.NetworkHost
            }, token);
            PublishRoom();
        }

        /// <summary>通过目录创建独立服务器房间，并以普通客户端入席。</summary>
        /// <param name="configuration">已校验的比赛配置；大奖赛从第一章开始。</param>
        public UniTask CreateAsync(KagitKartRoomState configuration)
        {
            ValidateConfiguration(configuration);
            var request = new CreateRequest { requestId = Guid.NewGuid().ToString("N"), configuration = configuration };
            return ConnectRoomAsync(() => directory.CreateAsync(JsonUtility.ToJson(request), CancellationToken.None),
                "正在创建房间……", "等待玩家加入并准备。");
        }

        /// <summary>按联机码兑换一次性授权并连接独立服务器；失败后允许重试。</summary>
        /// <param name="code">保留前导零的六位数字联机码。</param>
        /// <param name="name">仅作显示的玩家名。</param>
        public UniTask JoinAsync(string code, string name)
        {
            return ConnectRoomAsync(() => directory.JoinAsync(code, JsonUtility.ToJson(new JoinRequest { displayName = name }), CancellationToken.None),
                "正在加入房间……", "已连接，正在读取房间信息……");
        }

        private async UniTask ConnectRoomAsync(Func<UniTask<RoomJoinGrant>> request, string pendingStatus, string connectedStatus)
        {
            token.ThrowIfCancellationRequested();
            if (disposed || connecting || reconnecting || leaving || service != null || grant != null)
            {
                throw new InvalidOperationException("请先完成当前连接或离房操作。");
            }
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var completion = new UniTaskCompletionSource();
            connectionCancellation = cancellation;
            connectionCompleted = completion;
            connecting = true;
            Status = pendingStatus;
            Exception cleanupFailure = null;
            try
            {
                // HTTP 保留原有请求超时，让晚到授权交回唯一任务撤销；直接 Abort 会遗失服务器已创建的身份。
                grant = await request();
                cancellation.Token.ThrowIfCancellationRequested();
                await ConnectGrantAsync(cancellation.Token);
                cancellation.Token.ThrowIfCancellationRequested();
                Status = connectedStatus;
            }
            catch (Exception connectionFailure)
            {
                try
                {
                    // JoinAsync 已返回，公共层 changing 门闩已经退出，此时才允许清理传输。
                    await LeaveSessionAsync(requireRevocation: true);
                }
                catch (Exception failure)
                {
                    cleanupFailure = failure;
                    ConnectionCleanupPending = true;
                    throw new AggregateException("连接失败且离房收口未完成。", connectionFailure, failure);
                }
                throw;
            }
            finally
            {
                connecting = false;
                connectionCancellation = null;
                connectionCompleted = null;
                if (cleanupFailure == null)
                {
                    completion.TrySetResult();
                }
                else
                {
                    completion.TrySetException(cleanupFailure);
                }
            }
        }

        /// 取消当前首次连接，并等待原 Create/Join 撤销授权和释放传输；不抢占它的清理权。
        public async UniTask CancelConnectionAsync()
        {
            var completion = connectionCompleted;
            if (completion == null)
            {
                return;
            }
            connectionCancellation.Cancel();
            Status = "正在取消连接，请稍候……";
            await completion.Task;
        }

        private async UniTask ConnectGrantAsync(CancellationToken connectionToken)
        {
            InitializeService();
            await service.JoinAsync(new NetworkJoinOptions
            {
                Address = grant.address, Port = checked((ushort)grant.port), Ticket = grant.ticket,
                ProtocolVersion = checked((ushort)grant.protocolVersion), ContentVersion = grant.contentVersion
            }, connectionToken);
        }

        [Serializable]
        private class JoinRequest
        {
            public string displayName = "车手";
            public int protocolVersion = KagitKartOnlineSettings.ProtocolVersion;
            public string contentVersion = KagitKartOnlineSettings.ContentVersion;
        }

        [Serializable]
        private sealed class CreateRequest : JoinRequest
        {
            public string requestId;
            public KagitKartRoomState configuration;
        }

        [Serializable]
        private sealed class ResumeRequest
        {
            public string resumeToken;
            public int protocolVersion = KagitKartOnlineSettings.ProtocolVersion;
            public string contentVersion = KagitKartOnlineSettings.ContentVersion;
        }

        /// <summary>通过权威请求改变本机准备状态。</summary>
        /// <param name="ready">准备或取消准备。</param>
        public void SetReady(bool ready) => Send(new Request { Kind = "ready", Ready = ready });

        /// <summary>选择未被其它玩家占用的角色。</summary>
        /// <param name="id">原作角色 ID。</param>
        public void ChooseCharacter(string id) => Send(new Request { Kind = "character", CharacterId = id });

        /// <summary>房主修改大厅参数并重置全员准备。</summary>
        /// <param name="configuration">新的有效配置。</param>
        public void Configure(KagitKartRoomState configuration)
        {
            ValidateConfiguration(configuration);
            Send(new Request { Kind = "configure", Configuration = configuration });
        }

        public void StartRace() => Send(new Request { Kind = "start" });

        /// 单场或完整大奖赛结束后，房主重新开放原大厅。
        public void ReturnToLobby()
        {
            if (IsHost) Send(new Request { Kind = "lobby" });
        }

        private void Send(Request request)
        {
            if (!Connected || Room == null) return;
            request.RoundVersion = Room.RoundVersion;
            byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(request));
            service.SendCommandAsync(new NetworkCommand(RoomChannel, ++commandSequence, payload), token).Forget(HandleFailure);
        }

        private void MembersChanged(IReadOnlyList<NetworkMemberInfo> members)
        {
            if (!IsAuthority || Room == null) return;
            var players = Room.Players.ToList();
            foreach (var player in players)
            {
                bool connected = !server.HasDeparted(player.MemberId) && members.Any(member => member.MemberId == player.MemberId);
                if (player.Connected && !connected)
                {
                    player.ResumeUntil = server.HasDeparted(player.MemberId) ? 0 : Time.realtimeSinceStartupAsDouble + 60;
                    player.Ready = player.Loaded = false;
                }
                if (!player.Connected && connected)
                {
                    player.Loaded = false;
                    player.ResumeUntil = 0;
                }
                player.Connected = connected;
                if (!player.Connected)
                {
                    var kart = Session?.FindNetworkKart(player.CharacterId);
                    if (kart != null) kart.Autopilot = true;
                    inputs.Remove(player.CharacterId);
                }
            }
            foreach (var member in members)
            {
                if (server.HasDeparted(member.MemberId)) continue;
                if (players.Any(p => p.MemberId == member.MemberId)) continue;
                // 已开始比赛的连接不能取得赛车或完整房间状态。
                if (Room.Phase != "Lobby") continue;
                string character = Roster.First(c => players.All(p => p.CharacterId != c.id)).id;
                players.Add(new KagitKartRoomPlayer { MemberId = member.MemberId, Name = member.DisplayName, CharacterId = character });
            }
            Room.Players = players.ToArray();
            TransferLeader();
            if (Room.Phase == "Preparing" && players.Count(p => p.Connected) < 2)
            {
                Room.Phase = "Lobby";
                Room.Message = "玩家已离开，至少两名真人才能开始比赛。";
                foreach (var player in Room.Players) player.Ready = false;
            }
            PublishRoom();
        }

        private void ReceiveCommand(NetworkCommandEvent value)
        {
            if (!IsAuthority || value.Command.Channel != RoomChannel || Room == null) return;
            byte[] data = value.Command.Payload;
            if (data.Length > 2048) return;
            try
            {
                var request = JsonUtility.FromJson<Request>(Encoding.UTF8.GetString(data));
                var player = Array.Find(Room.Players, p => p.MemberId == value.SenderMemberId && p.Connected);
                if (request == null || player == null || request.RoundVersion != Room.RoundVersion) return;
                bool lobby = Room.Phase == "Lobby" || Room.Phase == "Results";
                switch (request.Kind)
                {
                    case "leave":
                        player.Connected = player.Ready = player.Loaded = false;
                        player.ResumeUntil = 0;
                        server.MarkDeparted(player.MemberId);
                        var abandonedKart = Session?.FindNetworkKart(player.CharacterId);
                        if (abandonedKart != null) abandonedKart.Autopilot = true;
                        TransferLeader();
                        break;
                    case "lobby" when Room.Phase == "Results" && value.SenderMemberId == Room.LeaderMemberId
                        && (Room.Mode != KagitKartRaceMode.GrandPrix || GrandPrix?.Completed == true):
                        Room.Phase = "Lobby";
                        break;
                    case "ready" when lobby:
                        player.Ready = request.Ready;
                        if (request.Ready) Room.Message = null;
                        break;
                    case "character" when Room.Phase == "Lobby":
                        if (Roster.Any(c => c.id == request.CharacterId) && Room.Players.All(p => p == player || p.CharacterId != request.CharacterId))
                        {
                            player.CharacterId = request.CharacterId;
                            player.Ready = false;
                        }
                        break;
                    case "configure" when Room.Phase == "Lobby" && value.SenderMemberId == Room.LeaderMemberId:
                        ValidateConfiguration(request.Configuration);
                        if (request.Configuration.Capacity < Room.Players.Length) return;
                        Room.Name = request.Configuration.Name;
                        Room.Capacity = request.Configuration.Capacity;
                        Room.Mode = request.Configuration.Mode;
                        Room.SpeedClass = request.Configuration.SpeedClass;
                        Room.TrackId = request.Configuration.TrackId;
                        foreach (var member in Room.Players) member.Ready = false;
                        break;
                    case "start" when value.SenderMemberId == Room.LeaderMemberId && CanAuthorityStart:
                        Room.Phase = "Preparing";
                        Room.Message = null;
                        Room.RoundVersion++;
                        Room.Seed = unchecked((uint)DateTime.UtcNow.Ticks);
                        Room.Grid = Roster.Select(c => c.id).ToArray();
                        if (Room.Mode == KagitKartRaceMode.GrandPrix && (GrandPrix == null || GrandPrix.Completed)) Room.TrackId = "meadow";
                        if (Room.Mode == KagitKartRaceMode.GrandPrix && GrandPrix != null && !GrandPrix.Completed)
                        {
                            if (GrandPrix.RoundComplete)
                            {
                                Room.TrackId = new[] { "meadow", "bosphorus", "glacier", "desk" }[GrandPrix.Index + 1];
                            }
                            if (GrandPrix.RoundComplete || GrandPrix.Index > 0)
                            {
                                Room.Grid = GrandPrix.SortedStandings().Reverse().Select(p => p.CharacterId).ToArray();
                            }
                        }
                        foreach (var member in Room.Players) member.Loaded = false;
                        Room.ServerLoaded = false;
                        preparationDeadline = Time.realtimeSinceStartupAsDouble + 60;
                        inputs.Clear();
                        inputTicks.Clear();
                        vehicleEvents.Clear();
                        itemEvents.Clear();
                        preparedVersion = Room.RoundVersion;
                        PrepareRaceAsync(Room.RoundVersion, token).Forget(HandleFailure);
                        break;
                    case "loaded" when Room.Phase is "Preparing" or "Racing" or "Results":
                        player.Loaded = true;
                        var restoredKart = Session?.FindNetworkKart(player.CharacterId);
                        if (restoredKart != null && !restoredKart.Finished) restoredKart.Autopilot = false;
                        break;
                    default:
                        return;
                }
                if (Room.Phase == "Preparing" && Room.ServerLoaded && Room.Players.Count(p => p.Connected) >= 2
                    && Room.Players.Where(p => p.Connected).All(p => p.Loaded)) Room.Phase = "Racing";
                PublishRoom();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidDataException)
            {
                Status = "房间操作无效。";
            }
        }

        private void PublishRoom()
        {
            if (!IsAuthority || Room == null) return;
            if (Room.Phase is "Lobby" or "Results") Room.Players = Room.Players.Where(p => p.Connected || p.ResumeUntil > Time.realtimeSinceStartupAsDouble).ToArray();
            if (GrandPrix != null)
            {
                var sorted = GrandPrix.SortedStandings();
                var standings = Roster.Select(character => sorted.First(value => value.CharacterId == character.id)).ToArray();
                Room.CupIndex = GrandPrix.Index;
                Room.CupRoundComplete = GrandPrix.RoundComplete;
                Room.CupPoints = standings.Select(value => value.Points).ToArray();
                Room.CupPlaces = standings.Select(value => value.LastPlace).ToArray();
                Room.CupTimes = standings.Select(value => value.TotalTime).ToArray();
            }
            service.PublishSnapshotAsync(new NetworkSnapshot(RoomChannel, ++roomSequence,
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(Room)), reliable: true), token).Forget(HandleFailure);
            ApplyRoom();
        }

        private void ReceiveSnapshot(NetworkSnapshotEvent value)
        {
            if (IsAuthority || leaving || disposed || service?.State != NetworkSessionState.Joined)
            {
                return;
            }
            if (value.Snapshot.Channel == RaceChannel && (preparingClient || Session == null))
            {
                deferredRaceSnapshot = value;
                return;
            }
            try
            {
                if (value.Snapshot.Channel == RoomChannel)
                {
                    Room = JsonUtility.FromJson<KagitKartRoomState>(Encoding.UTF8.GetString(value.Snapshot.Payload));
                    ApplyRoom();
                }
                else if (value.Snapshot.Channel == RaceChannel && Session != null && Room != null && !preparingClient)
                {
                    using var stream = new MemoryStream(value.Snapshot.Payload, false);
                    using var reader = new BinaryReader(stream);
                    if (reader.ReadInt32() != Room.RoundVersion) return;
                    bool notifyEvents = receivedRaceBaseline;
                    if (!receivedRaceBaseline)
                    {
                        Session.ReadNetworkState(reader, restoreLocal: true, notify: false);
                        if (!Session.Player.Finished) Session.Player.Autopilot = false;
                        receivedRaceBaseline = true;
                        Send(new Request { Kind = "loaded" });
                        world.OnNetworkStateRestored();
                    }
                    else Session.ReadNetworkState(reader);
                    int count = KagitKartNetworkCodec.ReadCount(reader, 128);
                    for (int i = 0; i < count; i++)
                    {
                        var kart = Session.FindNetworkKart(reader.ReadString());
                        var signal = (KagitKartVehicleSignal)reader.ReadInt32();
                        var context = KagitKartVehicle.SignalContext.Read(reader);
                        if (notifyEvents) kart?.NotifyNetworkSignal(signal, context);
                    }
                    count = KagitKartNetworkCodec.ReadCount(reader, 128);
                    for (int i = 0; i < count; i++)
                    {
                        string kind = reader.ReadString();
                        var kart = Session.FindNetworkKart(reader.ReadString());
                        var target = Session.FindNetworkKart(reader.ReadString());
                        string item = reader.ReadString();
                        var position = KagitKartNetworkCodec.ReadVector(reader);
                        var itemEvent = new KagitKartItemEvent(kind, item, position, kart,
                            reader.ReadString(), target, reader.ReadSingle(), reader.ReadBoolean());
                        if (notifyEvents) Session.Items.NotifyNetworkEvent(itemEvent);
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException)
            {
                HandleFailure(exception);
            }
        }

        private void ApplyRoom()
        {
            if (Room == null || disposed) return;
            string previousPhase = appliedPhase;
            appliedPhase = Room.Phase;
            if (server != null)
            {
                return;
            }
            bool allReady = Room.Players.Count(p => p.Connected) >= 2 && Room.Players.Where(p => p.Connected).All(p => p.Ready);
            Status = Room.Message ?? (Room.Phase == "Preparing" ? "正在准备比赛资源……" : Room.Phase == "Racing" ? "比赛进行中"
                : allReady ? IsHost ? "车手已准备完毕，可以开始比赛。" : "车手已准备完毕，等待房主开赛。" : "等待玩家准备。");
            if (Room.Phase == "Lobby" && previousPhase == "Preparing")
            {
                preparationCancellation?.Cancel();
                world.ReturnOnlineRoomAsync().Forget(HandleFailure);
            }
            if (Room.Phase is "Preparing" or "Racing" or "Results" && preparedVersion != Room.RoundVersion && Room.RoundVersion > 0)
            {
                preparedVersion = Room.RoundVersion;
                preparationCancellation?.Cancel();
                preparationCancellation?.Dispose();
                preparationCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                PrepareRaceAsync(Room.RoundVersion, preparationCancellation.Token).Forget(HandleFailure);
            }
            if (Room.Phase == "Racing" && receivedRaceBaseline) world.BeginOnlineCountdown();
            if (Room.Phase is "Lobby" or "Results") world.PrepareRoomSelection(Room);
            world.Dispatch(KagitKartCommand.Refresh);
        }

        private async UniTask PrepareRaceAsync(int version, CancellationToken preparationToken)
        {
            var previous = preparationCompleted;
            var completion = new UniTaskCompletionSource();
            preparationCompleted = completion;
            try
            {
                if (previous != null)
                {
                    await previous.Task;
                }
                preparationToken.ThrowIfCancellationRequested();
                await PrepareRaceCoreAsync(version, preparationToken);
            }
            finally
            {
                if (ReferenceEquals(preparationCompleted, completion))
                {
                    preparationCompleted = null;
                }
                completion.TrySetResult();
            }
        }

        private async UniTask PrepareRaceCoreAsync(int version, CancellationToken preparationToken)
        {
            if (server != null)
            {
                DetachRaceEvents();
                server.PrepareRound(Room);
            }
            else
            {
                preparingClient = true;
                try
                {
                    receivedRaceBaseline = false;
                    await world.PrepareOnlineRaceAsync(Room, preparationToken);
                }
                finally
                {
                    preparingClient = false;
                }
            }
            preparationToken.ThrowIfCancellationRequested();
            if (!Connected || Room == null || Room.RoundVersion != version) return;
            DetachRaceEvents();
            subscribedSession = Session;
            foreach (var kart in Session.Karts) kart.Signaled += OnVehicleSignal;
            Session.Items.Signaled += OnItemEvent;
            if (server != null)
            {
                Room.ServerLoaded = true;
                PublishRoom();
            }
            else if (Room.Phase == "Preparing") Send(new Request { Kind = "loaded" });
            if (world != null && deferredRaceSnapshot.HasValue)
            {
                var snapshot = deferredRaceSnapshot.Value;
                deferredRaceSnapshot = null;
                ReceiveSnapshot(snapshot);
            }
        }

        private void OnVehicleSignal(KagitKartVehicle kart, KagitKartVehicleSignal signal)
        {
            if (IsAuthority && vehicleEvents.Count < 128) vehicleEvents.Add((kart.Id, signal, kart.CaptureSignalContext()));
        }

        private void OnItemEvent(KagitKartItemEvent value)
        {
            if (IsAuthority && itemEvents.Count < 128) itemEvents.Add(value);
        }

        public byte[] ReadInput()
        {
            if (!Racing || Session == null || reconnecting || !receivedRaceBaseline) return null;
            var controls = world.Busy || Session.Pause.IsPaused ? default : world.Input.ReadControls(Session.Race.State == KagitKartRaceState.Countdown);
            // 预测历史和发送队列持有此包，返回独占数组，不复用仍在途的字节。
            var payload = new byte[17];
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(0, 4), Room.RoundVersion);
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(4, 4), BitConverter.SingleToInt32Bits(controls.Steer));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(8, 4), BitConverter.SingleToInt32Bits(controls.Throttle));
            BinaryPrimitives.WriteInt32LittleEndian(payload.AsSpan(12, 4), BitConverter.SingleToInt32Bits(controls.Brake));
            payload[16] = (byte)((controls.Drift ? 1 : 0) | (controls.Item ? 2 : 0) | (controls.LookBack ? 4 : 0));
            return payload;
        }

        public void Replicate(string memberId, uint tick, float dt, byte[] input, bool replay, bool isAuthority)
        {
            if (!Racing || Session == null || input == null || input.Length != 17) return;
            using var stream = new MemoryStream(input, false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != Room.RoundVersion) return;
            try
            {
                var controls = KagitKartNetworkCodec.ReadControls(reader);
                if (isAuthority)
                {
                    var player = Array.Find(Room.Players, p => p.MemberId == memberId && p.Connected);
                    if (player == null || !player.Loaded) return;
                    inputs[player.CharacterId] = controls;
                    inputTicks[player.CharacterId] = tick;
                }
                else if (memberId == LocalMemberId)
                {
                    Session.Predict(dt, controls, replay);
                }
            }
            catch (InvalidDataException)
            {
                if (isAuthority)
                {
                    var player = Array.Find(Room.Players, p => p.MemberId == memberId);
                    if (player != null) inputs[player.CharacterId] = default;
                }
            }
        }

        public void AuthorityTick(uint tick, float dt)
        {
            bool membershipChanged = false;
            foreach (var player in Room.Players)
            {
                if (!player.Connected && player.ResumeUntil > 0 && player.ResumeUntil <= Time.realtimeSinceStartupAsDouble)
                {
                    player.ResumeUntil = 0;
                    server.MarkDeparted(player.MemberId);
                    membershipChanged = true;
                }
            }
            if (membershipChanged) PublishRoom();
            if (Room?.Phase == "Preparing" && Time.realtimeSinceStartupAsDouble > preparationDeadline)
            {
                Room.Phase = "Lobby";
                foreach (var player in Room.Players) player.Ready = false;
                Room.Message = "准备超时，请检查各玩家资源后重试。";
                PublishRoom();
            }
            if (!Racing || Session == null) return;
            foreach (var player in Room.Players)
            {
                if (inputTicks.TryGetValue(player.CharacterId, out uint last) && tick - last > 120) inputs[player.CharacterId] = default;
            }
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            Session.AdvanceNetwork(dt, inputs);
            long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
            measuredTicks += elapsed;
            maximumStepTicks = Math.Max(maximumStepTicks, elapsed);
            if (++measuredSteps == 3600 || Session.Race.State == KagitKartRaceState.Done)
            {
                double milliseconds = 1000d / System.Diagnostics.Stopwatch.Frequency;
                Debug.Log($"卡丁车权威规则步：round={Room.RoundVersion} steps={measuredSteps} avgMs={measuredTicks * milliseconds / measuredSteps:F3} maxMs={maximumStepTicks * milliseconds:F3}");
                measuredSteps = 0;
                measuredTicks = maximumStepTicks = 0;
            }
            if (tick % 4 != 0) return;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Room.RoundVersion);
            Session.WriteNetworkState(writer);
            writer.Write(vehicleEvents.Count);
            foreach (var value in vehicleEvents)
            {
                writer.Write(value.id);
                writer.Write((int)value.signal);
                value.context.Write(writer);
            }
            writer.Write(itemEvents.Count);
            foreach (var value in itemEvents)
            {
                writer.Write(value.Kind);
                writer.Write(value.Kart?.Id ?? string.Empty);
                writer.Write(value.Target?.Id ?? string.Empty);
                writer.Write(value.Item ?? string.Empty);
                KagitKartNetworkCodec.WriteVector(writer, value.Position);
                writer.Write(value.State ?? string.Empty);
                writer.Write(value.Eta);
                writer.Write(value.Backward);
            }
            vehicleEvents.Clear();
            itemEvents.Clear();
            service.PublishSnapshotAsync(new NetworkSnapshot(RaceChannel, ++raceSequence, stream.ToArray(),
                reliable: Session.Race.State == KagitKartRaceState.Done), token).Forget(HandleFailure);
            if (Session.Race.State == KagitKartRaceState.Done)
            {
                Room.Phase = "Results";
                foreach (var player in Room.Players) player.Ready = false;
                PublishRoom();
            }
        }

        public byte[] CaptureState(string memberId)
        {
            if (Session == null || Room == null) return null;
            var player = Array.Find(Room.Players, p => p.MemberId == memberId);
            var kart = Session.FindNetworkKart(player?.CharacterId);
            if (kart == null) return null;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Room.RoundVersion);
            kart.WriteNetworkState(writer);
            return stream.ToArray();
        }

        public void Reconcile(string memberId, uint tick, byte[] state)
        {
            if (preparingClient || !receivedRaceBaseline || Session == null || Room == null || state == null || memberId != LocalMemberId) return;
            using var stream = new MemoryStream(state, false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != Room.RoundVersion) return;
            Session.Player.ReadNetworkState(reader, Session.Karts, false);
        }

        private void StateChanged(NetworkSessionState state)
        {
            if (state == NetworkSessionState.Joined) wasJoined = true;
            if (!connecting && !leaving && !disposed && wasJoined && state is NetworkSessionState.Faulted or NetworkSessionState.Idle)
            {
                wasJoined = false;
                if (server != null)
                {
                    Application.Quit(1);
                    return;
                }
                if (!reconnecting) ReconnectAsync().Forget(HandleFailure);
            }
        }

        private void HandleFailure(Exception exception)
        {
            if (exception is OperationCanceledException || disposed) return;
            Status = "联机操作失败，请退出后重试。";
            Debug.LogException(exception);
            world?.Dispatch(KagitKartCommand.Refresh);
        }

        /// 合并重复离房请求，先等待在途任务结束，再由一个任务释放会话。
        public UniTask LeaveAsync()
        {
            if (leaveCompleted != null)
            {
                return leaveCompleted.Task;
            }
            var completion = new UniTaskCompletionSource();
            leaveCompleted = completion;
            LeaveSerializedAsync(completion).Forget();
            return completion.Task;
        }

        private async UniTask LeaveSerializedAsync(UniTaskCompletionSource completion)
        {
            leaving = true;
            try
            {
                connectionCancellation?.Cancel();
                reconnectCancellation?.Cancel();
                var connection = connectionCompleted;
                var reconnect = reconnectCompleted;
                if (connection != null)
                {
                    await connection.Task;
                }
                if (reconnect != null)
                {
                    await reconnect.Task;
                }
                await LeaveSessionAsync(requireRevocation: ConnectionCleanupPending);
                completion.TrySetResult();
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
            finally
            {
                leaving = false;
                leaveCompleted = null;
            }
        }

        private async UniTask LeaveSessionAsync(bool requireRevocation = false)
        {
            Exception revokeFailure = null;
            if (directory != null && grant != null)
            {
                using var revokeTimeout = new CancellationTokenSource();
                using var timer = revokeTimeout.CancelAfterSlim(TimeSpan.FromSeconds(5));
                try
                {
                    await directory.LeaveAsync(JsonUtility.ToJson(new ResumeRequest { resumeToken = grant.resumeToken }), revokeTimeout.Token);
                }
                catch (Exception exception) when (exception is InvalidOperationException || exception is OperationCanceledException)
                {
                    if (requireRevocation)
                    {
                        revokeFailure = exception;
                    }
                    else
                    {
                        Debug.LogWarning("服务器暂时不可达，断线席位将在重连期限后释放。");
                    }
                }
            }
            if (world != null && Connected)
            {
                Send(new Request { Kind = "leave" });
                await UniTask.Delay(100, ignoreTimeScale: true);
            }
            await ReleaseTransportAsync();
            Room = null;
            inputs.Clear();
            inputTicks.Clear();
            commandSequence = roomSequence = raceSequence = 0;
            preparedVersion = -1;
            appliedPhase = null;
            wasJoined = false;
            if (revokeFailure == null)
            {
                grant = null;
            }
            reconnecting = false;
            if (revokeFailure != null)
            {
                throw new InvalidOperationException("连接已断开，但目录授权撤销失败；请重试取消。", revokeFailure);
            }
            ConnectionCleanupPending = false;
        }

        private void DetachRaceEvents()
        {
            if (subscribedSession == null) return;
            foreach (var kart in subscribedSession.Karts) kart.Signaled -= OnVehicleSignal;
            subscribedSession.Items.Signaled -= OnItemEvent;
            subscribedSession = null;
        }

        private void ValidateConfiguration(KagitKartRoomState configuration)
        {
            if (configuration == null || string.IsNullOrWhiteSpace(configuration.Name) || configuration.Name.Length > 32
                || configuration.Capacity is not (2 or 4 or 8) || configuration.Mode is not (KagitKartRaceMode.SingleRace or KagitKartRaceMode.GrandPrix)
                || configuration.SpeedClass is not (80 or 120 or 200)) throw new ArgumentException("房间设置无效。");
            KagitKartCatalog.TrackName(configuration.TrackId);
            if (configuration.Mode == KagitKartRaceMode.GrandPrix && Room == null) configuration.TrackId = "meadow";
        }

        private void TransferLeader()
        {
            if (Room.Players.Any(player => player.Connected && player.MemberId == Room.LeaderMemberId)) return;
            Room.LeaderMemberId = Room.Players.FirstOrDefault(player => player.Connected)?.MemberId;
        }

        internal void RemovePlayer(string memberId)
        {
            var player = Array.Find(Room.Players, value => value.MemberId == memberId);
            server.MarkDeparted(memberId);
            if (player == null) return;
            player.Connected = player.Ready = player.Loaded = false;
            player.ResumeUntil = 0;
            var kart = Session?.FindNetworkKart(player.CharacterId);
            if (kart != null) kart.Autopilot = true;
            inputs.Remove(player.CharacterId);
            inputTicks.Remove(player.CharacterId);
            TransferLeader();
            PublishRoom();
        }

        private async UniTask ReleaseTransportAsync()
        {
            // 先断开回调来源，再取消并等待本轮资源准备，旧基线不能跨重连尝试使用。
            var ownedService = service;
            if (ownedService != null)
            {
                ownedService.RegisterSimulation(null);
                ownedService.MembersChanged -= MembersChanged;
                ownedService.CommandReceived -= ReceiveCommand;
                ownedService.SnapshotReceived -= ReceiveSnapshot;
                ownedService.StateChanged -= StateChanged;
            }
            preparationCancellation?.Cancel();
            var preparation = preparationCompleted;
            if (preparation != null)
            {
                await preparation.Task;
            }
            preparationCancellation?.Dispose();
            preparationCancellation = null;
            deferredRaceSnapshot = null;
            receivedRaceBaseline = false;
            preparedVersion = -1;
            appliedPhase = null;
            preparingClient = false;
            DetachRaceEvents();
            if (ownedService == null)
            {
                return;
            }
            await ownedService.LeaveAsync();
            ownedService.Dispose();
            service = null;
            UnityEngine.Object.Destroy(settings);
            settings = null;
            commandSequence = 0;
        }

        private async UniTask ReconnectAsync()
        {
            if (reconnecting || leaving || disposed)
            {
                return;
            }
            reconnecting = true;
            var completion = new UniTaskCompletionSource();
            reconnectCompleted = completion;
            reconnectDeadline = Time.realtimeSinceStartupAsDouble + 60;
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            reconnectCancellation = cancellation;
            var reconnectToken = cancellation.Token;
            using var deadlineTimer = cancellation.CancelAfterSlim(TimeSpan.FromSeconds(60));
            bool restored = false;
            bool notifyTimeout = false;
            Exception cleanupFailure = null;
            world.OnReconnectStarted();
            try
            {
                await ReleaseTransportAsync();
                while (Time.realtimeSinceStartupAsDouble < reconnectDeadline)
                {
                    reconnectToken.ThrowIfCancellationRequested();
                    Status = "正在重新连接，AI 正在暂时代驾……";
                    try
                    {
                        // Resume 秘密不轮换；中止 HTTP 后仍能用旧 grant 撤销身份，保持 60 秒截止时间。
                        grant = await directory.ResumeAsync(JsonUtility.ToJson(new ResumeRequest { resumeToken = grant.resumeToken }), reconnectToken);
                        reconnectToken.ThrowIfCancellationRequested();
                        await ConnectGrantAsync(reconnectToken);
                        var attemptService = service;
                        string expectedMemberId = grant.memberId;
                        await UniTask.WaitUntil(() =>
                        {
                            RequireReconnectIdentity(attemptService, expectedMemberId);
                            return !preparingClient && Room != null && (Room.Phase == "Lobby" || receivedRaceBaseline);
                        }, cancellationToken: reconnectToken);
                        reconnectToken.ThrowIfCancellationRequested();
                        RequireReconnectIdentity(attemptService, expectedMemberId);
                        restored = true;
                        reconnecting = false;
                        world.OnReconnectFinished();
                        return;
                    }
                    catch (Exception exception) when (exception is not OperationCanceledException)
                    {
                        Status = "本次恢复连接已中断，正在重试……";
                        await ReleaseTransportAsync();
                        await UniTask.Delay(1500, ignoreTimeScale: true, cancellationToken: reconnectToken);
                    }
                }
                notifyTimeout = true;
            }
            catch (OperationCanceledException) when (reconnectToken.IsCancellationRequested)
            {
                notifyTimeout = !token.IsCancellationRequested && !leaving && !disposed;
            }
            finally
            {
                if (!restored)
                {
                    try
                    {
                        await ReleaseTransportAsync();
                    }
                    catch (Exception exception)
                    {
                        cleanupFailure = exception;
                    }
                }
                reconnecting = false;
                reconnectCancellation = null;
                reconnectCompleted = null;
                if (cleanupFailure == null)
                {
                    completion.TrySetResult();
                }
                else
                {
                    completion.TrySetException(cleanupFailure);
                }
            }
            if (cleanupFailure != null)
            {
                throw cleanupFailure;
            }
            // 先完成重连任务，再回调 World 离房；否则 Leave 等待本任务会形成自等待。
            if (notifyTimeout && !leaving && !disposed && !token.IsCancellationRequested)
            {
                Status = "重连已超时，请在下一场重新加入。";
                await world.OnOnlineDisconnectedAsync();
            }
        }

        private void RequireReconnectIdentity(INetworkSessionService attempt, string memberId)
        {
            if (!ReferenceEquals(service, attempt) || attempt == null || attempt.State != NetworkSessionState.Joined
                || attempt.LocalMemberId != memberId)
            {
                throw new InvalidOperationException("本次恢复连接或稳定身份已失效。");
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // 统一离房任务只请求取消并等待原连接/重连收口，不与 SDK 的 changing 操作并行。
            LeaveAsync().Forget(Debug.LogException);
        }
    }
}
