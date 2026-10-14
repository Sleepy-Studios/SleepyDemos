using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    }

    /// 卡丁车房间与网络规则；网络 SDK 和预测历史均由公共层负责。
    public sealed class KagitKartNetworkRoom : INetworkSimulation, IDisposable
    {
        private const ushort RoomChannel = 710;
        private const ushort RaceChannel = 711;
        private readonly KagitKartWorld world;
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
        private bool wasJoined;
        private double preparationDeadline;
        private string appliedPhase;
        private KagitKartSession subscribedSession;
        private CancellationTokenSource preparationCancellation;

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
        public bool IsHost => service?.IsAuthority == true;
        public string LocalMemberId => service?.LocalMemberId;
        public string AuthorityMemberId => service?.AuthorityMemberId;
        public string Address { get; private set; }
        public string Status { get; private set; }
        public bool Connected => service?.State == NetworkSessionState.Joined;
        public bool Racing => Connected && Room?.Phase == "Racing";
        public bool CanStart => IsHost && Room != null && (Room.Phase == "Lobby" || Room.Phase == "Results")
            && Room.Players.Count(p => p.Connected) >= 2 && Room.Players.Where(p => p.Connected).All(p => p.Ready);

        /// <summary>绑定本 Demo，连接仅在明确创建或加入时启动。</summary>
        /// <param name="world">场景宿主。</param>
        /// <param name="token">场景退出令牌。</param>
        public KagitKartNetworkRoom(KagitKartWorld world, CancellationToken token)
        {
            this.world = world;
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
            service.SetAdmissionRule(() => Room != null && Room.Phase == "Lobby"
                && Room.Players.Count(p => p.Connected) < Room.Capacity);
        }

        /// <summary>本机启动 Host 并创建一个房间，不连接公共目录服务。</summary>
        /// <param name="configuration">已校验比赛配置。</param>
        public async UniTask CreateAsync(KagitKartRoomState configuration)
        {
            ValidateConfiguration(configuration);
            InitializeService();
            Room = configuration;
            Status = "正在创建房间……";
            connecting = true;
            try
            {
                await service.CreateAsync(new NetworkCreateOptions { Address = "127.0.0.1", Port = 7777,
                    ProtocolVersion = settings.ProtocolVersion, ContentVersion = settings.ContentVersion,
                    MaxPlayers = 8, LocalDisplayName = "房主" }, token);
                if (disposed) throw new OperationCanceledException(token);
                token.ThrowIfCancellationRequested();
                Address = LocalAddress() + ":7777";
                MembersChanged(service.Members);
                Status = "等待玩家加入并准备。";
            }
            catch
            {
                await LeaveAsync();
                throw;
            }
            finally
            {
                connecting = false;
            }
        }

        /// <summary>连接玩家 Host；失败收口后允许再次连接。</summary>
        /// <param name="address">房主 IP 或域名。</param>
        /// <param name="port">房主监听端口。</param>
        /// <param name="name">仅作显示的玩家名。</param>
        public async UniTask JoinAsync(string address, ushort port, string name)
        {
            InitializeService();
            Status = "正在加入房间……";
            connecting = true;
            try
            {
                await service.JoinAsync(new NetworkJoinOptions { Address = address, Port = port,
                    ContentVersion = settings.ContentVersion, DisplayName = string.IsNullOrWhiteSpace(name) ? "玩家" : name.Trim() }, token);
                if (disposed) throw new OperationCanceledException(token);
                token.ThrowIfCancellationRequested();
                Address = address.Trim() + ":" + port;
                Status = "已连接，正在读取房间信息……";
            }
            catch
            {
                await LeaveAsync();
                throw;
            }
            finally
            {
                connecting = false;
            }
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
            if (!IsHost || Room?.Phase != "Results" || Room.Mode == KagitKartRaceMode.GrandPrix && world.GrandPrix?.Completed != true) return;
            Room.Phase = "Lobby";
            PublishRoom();
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
            if (!IsHost || Room == null) return;
            var players = Room.Players.ToList();
            foreach (var player in players)
            {
                player.Connected = members.Any(member => member.MemberId == player.MemberId);
                if (!player.Connected)
                {
                    var kart = world.Session?.FindNetworkKart(player.CharacterId);
                    if (kart != null) kart.Autopilot = true;
                    inputs.Remove(player.CharacterId);
                }
            }
            if (Room.Phase == "Lobby" || Room.Phase == "Results") players.RemoveAll(p => !p.Connected);
            foreach (var member in members)
            {
                if (players.Any(p => p.MemberId == member.MemberId)) continue;
                // 已开始比赛的连接不能取得赛车或完整房间状态。
                if (Room.Phase != "Lobby") continue;
                string character = world.Roster.First(c => players.All(p => p.CharacterId != c.id)).id;
                players.Add(new KagitKartRoomPlayer { MemberId = member.MemberId, Name = member.DisplayName, CharacterId = character });
            }
            Room.Players = players.ToArray();
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
            if (!IsHost || value.Command.Channel != RoomChannel || Room == null) return;
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
                    case "ready" when lobby:
                        player.Ready = request.Ready;
                        if (request.Ready) Room.Message = null;
                        break;
                    case "character" when Room.Phase == "Lobby":
                        if (world.Roster.Any(c => c.id == request.CharacterId) && Room.Players.All(p => p == player || p.CharacterId != request.CharacterId))
                        {
                            player.CharacterId = request.CharacterId;
                            player.Ready = false;
                        }
                        break;
                    case "configure" when Room.Phase == "Lobby" && value.SenderMemberId == service.AuthorityMemberId:
                        ValidateConfiguration(request.Configuration);
                        if (request.Configuration.Capacity < Room.Players.Length) return;
                        Room.Name = request.Configuration.Name;
                        Room.Capacity = request.Configuration.Capacity;
                        Room.Mode = request.Configuration.Mode;
                        Room.SpeedClass = request.Configuration.SpeedClass;
                        Room.TrackId = request.Configuration.TrackId;
                        foreach (var member in Room.Players) member.Ready = false;
                        break;
                    case "start" when value.SenderMemberId == service.AuthorityMemberId && CanStart:
                        Room.Phase = "Preparing";
                        Room.Message = null;
                        Room.RoundVersion++;
                        Room.Seed = unchecked((uint)DateTime.UtcNow.Ticks);
                        Room.Grid = world.Roster.Select(c => c.id).ToArray();
                        if (Room.Mode == KagitKartRaceMode.GrandPrix && (world.GrandPrix == null || world.GrandPrix.Completed)) Room.TrackId = "meadow";
                        if (Room.Mode == KagitKartRaceMode.GrandPrix && world.GrandPrix != null && !world.GrandPrix.Completed)
                        {
                            if (world.GrandPrix.RoundComplete)
                            {
                                Room.TrackId = new[] { "meadow", "bosphorus", "glacier", "desk" }[world.GrandPrix.Index + 1];
                            }
                            if (world.GrandPrix.RoundComplete || world.GrandPrix.Index > 0)
                            {
                                Room.Grid = world.GrandPrix.SortedStandings().Reverse().Select(p => p.CharacterId).ToArray();
                            }
                        }
                        foreach (var member in Room.Players) member.Loaded = false;
                        preparationDeadline = Time.realtimeSinceStartupAsDouble + 60;
                        inputs.Clear();
                        inputTicks.Clear();
                        vehicleEvents.Clear();
                        itemEvents.Clear();
                        break;
                    case "loaded" when Room.Phase == "Preparing":
                        player.Loaded = true;
                        break;
                    default:
                        return;
                }
                if (Room.Phase == "Preparing" && Room.Players.Where(p => p.Connected).All(p => p.Loaded)) Room.Phase = "Racing";
                PublishRoom();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidDataException)
            {
                Status = "房间操作无效。";
            }
        }

        private void PublishRoom()
        {
            if (!IsHost || Room == null) return;
            if (Room.Phase is "Lobby" or "Results") Room.Players = Room.Players.Where(p => p.Connected).ToArray();
            service.PublishSnapshotAsync(new NetworkSnapshot(RoomChannel, ++roomSequence,
                Encoding.UTF8.GetBytes(JsonUtility.ToJson(Room)), reliable: true), token).Forget(HandleFailure);
            ApplyRoom();
        }

        private void ReceiveSnapshot(NetworkSnapshotEvent value)
        {
            if (IsHost) return;
            try
            {
                if (value.Snapshot.Channel == RoomChannel)
                {
                    Room = JsonUtility.FromJson<KagitKartRoomState>(Encoding.UTF8.GetString(value.Snapshot.Payload));
                    ApplyRoom();
                }
                else if (value.Snapshot.Channel == RaceChannel && world.Session != null && Room != null)
                {
                    using var stream = new MemoryStream(value.Snapshot.Payload, false);
                    using var reader = new BinaryReader(stream);
                    if (reader.ReadInt32() != Room.RoundVersion) return;
                    world.Session.ReadNetworkState(reader);
                    int count = KagitKartNetworkCodec.ReadCount(reader, 128);
                    for (int i = 0; i < count; i++)
                    {
                        var kart = world.Session.FindNetworkKart(reader.ReadString());
                        var signal = (KagitKartVehicleSignal)reader.ReadInt32();
                        var context = KagitKartVehicle.SignalContext.Read(reader);
                        kart?.NotifyNetworkSignal(signal, context);
                    }
                    count = KagitKartNetworkCodec.ReadCount(reader, 128);
                    for (int i = 0; i < count; i++)
                    {
                        string kind = reader.ReadString();
                        var kart = world.Session.FindNetworkKart(reader.ReadString());
                        var target = world.Session.FindNetworkKart(reader.ReadString());
                        string item = reader.ReadString();
                        var position = KagitKartNetworkCodec.ReadVector(reader);
                        world.Session.Items.NotifyNetworkEvent(new KagitKartItemEvent(kind, item, position, kart,
                            reader.ReadString(), target, reader.ReadSingle(), reader.ReadBoolean()));
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
            bool allReady = Room.Players.Count(p => p.Connected) >= 2 && Room.Players.Where(p => p.Connected).All(p => p.Ready);
            Status = Room.Message ?? (Room.Phase == "Preparing" ? "正在准备比赛资源……" : Room.Phase == "Racing" ? "比赛进行中"
                : allReady ? IsHost ? "车手已准备完毕，可以开始比赛。" : "车手已准备完毕，等待房主开赛。" : "等待玩家准备。");
            if (Room.Phase == "Lobby" && previousPhase == "Preparing")
            {
                preparationCancellation?.Cancel();
                world.ReturnOnlineRoomAsync().Forget(HandleFailure);
            }
            if (Room.Phase == "Preparing" && preparedVersion != Room.RoundVersion)
            {
                preparedVersion = Room.RoundVersion;
                preparationCancellation?.Cancel();
                preparationCancellation?.Dispose();
                preparationCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
                PrepareRaceAsync(Room.RoundVersion, preparationCancellation.Token).Forget(HandleFailure);
            }
            if (Room.Phase == "Racing") world.BeginOnlineCountdown();
            if (Room.Phase is "Lobby" or "Results") world.PrepareRoomSelection(Room);
            world.Dispatch(KagitKartCommand.Refresh);
        }

        private async UniTask PrepareRaceAsync(int version, CancellationToken preparationToken)
        {
            await world.PrepareOnlineRaceAsync(Room, preparationToken);
            preparationToken.ThrowIfCancellationRequested();
            if (!Connected || Room == null || Room.RoundVersion != version || Room.Phase != "Preparing") return;
            DetachRaceEvents();
            subscribedSession = world.Session;
            foreach (var kart in world.Session.Karts) kart.Signaled += OnVehicleSignal;
            world.Session.Items.Signaled += OnItemEvent;
            Send(new Request { Kind = "loaded" });
        }

        private void OnVehicleSignal(KagitKartVehicle kart, KagitKartVehicleSignal signal)
        {
            if (IsHost && vehicleEvents.Count < 128) vehicleEvents.Add((kart.Id, signal, kart.CaptureSignalContext()));
        }

        private void OnItemEvent(KagitKartItemEvent value)
        {
            if (IsHost && itemEvents.Count < 128) itemEvents.Add(value);
        }

        public byte[] ReadInput()
        {
            if (!Racing || world.Session == null) return null;
            using var stream = new MemoryStream(20);
            using var writer = new BinaryWriter(stream);
            writer.Write(Room.RoundVersion);
            KagitKartNetworkCodec.WriteControls(writer, world.Busy || world.Session.Pause.IsPaused ? default : world.Input.ReadControls(world.Session.Race.State == KagitKartRaceState.Countdown));
            return stream.ToArray();
        }

        public void Replicate(string memberId, uint tick, float dt, byte[] input, bool replay, bool isAuthority)
        {
            if (!Racing || world.Session == null || input == null || input.Length != 17) return;
            using var stream = new MemoryStream(input, false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != Room.RoundVersion) return;
            try
            {
                var controls = KagitKartNetworkCodec.ReadControls(reader);
                if (isAuthority)
                {
                    var player = Array.Find(Room.Players, p => p.MemberId == memberId && p.Connected);
                    if (player == null) return;
                    inputs[player.CharacterId] = controls;
                    inputTicks[player.CharacterId] = tick;
                }
                else if (memberId == LocalMemberId)
                {
                    world.Session.Predict(dt, controls, replay);
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
            if (Room?.Phase == "Preparing" && Time.realtimeSinceStartupAsDouble > preparationDeadline)
            {
                Room.Phase = "Lobby";
                foreach (var player in Room.Players) player.Ready = false;
                Room.Message = "准备超时，请检查各玩家资源后重试。";
                PublishRoom();
            }
            if (!Racing || world.Session == null) return;
            foreach (var player in Room.Players)
            {
                if (inputTicks.TryGetValue(player.CharacterId, out uint last) && tick - last > 120) inputs[player.CharacterId] = default;
            }
            world.Session.AdvanceNetwork(dt, inputs);
            if (tick % 4 != 0) return;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Room.RoundVersion);
            world.Session.WriteNetworkState(writer);
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
                reliable: world.Session.Race.State == KagitKartRaceState.Done), token).Forget(HandleFailure);
            if (world.Session.Race.State == KagitKartRaceState.Done)
            {
                Room.Phase = "Results";
                foreach (var player in Room.Players) player.Ready = false;
                PublishRoom();
            }
        }

        public byte[] CaptureState(string memberId)
        {
            if (world.Session == null || Room == null) return null;
            var player = Array.Find(Room.Players, p => p.MemberId == memberId);
            var kart = world.Session.FindNetworkKart(player?.CharacterId);
            if (kart == null) return null;
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Room.RoundVersion);
            kart.WriteNetworkState(writer);
            return stream.ToArray();
        }

        public void Reconcile(string memberId, uint tick, byte[] state)
        {
            if (world.Session == null || Room == null || state == null || memberId != LocalMemberId) return;
            using var stream = new MemoryStream(state, false);
            using var reader = new BinaryReader(stream);
            if (reader.ReadInt32() != Room.RoundVersion) return;
            world.Session.Player.ReadNetworkState(reader, world.Session.Karts, false);
        }

        private void StateChanged(NetworkSessionState state)
        {
            if (state == NetworkSessionState.Joined) wasJoined = true;
            if (!leaving && !disposed && wasJoined && state is NetworkSessionState.Faulted or NetworkSessionState.Idle)
            {
                wasJoined = false;
                Status = "与房主的连接已断开，房间已结束。";
                world.OnOnlineDisconnectedAsync().Forget();
            }
        }

        private void HandleFailure(Exception exception)
        {
            if (exception is OperationCanceledException || disposed) return;
            Status = "联机操作失败，请退出后重试。";
            Debug.LogException(exception);
            world.Dispatch(KagitKartCommand.Refresh);
        }

        public async UniTask LeaveAsync()
        {
            leaving = true;
            preparationCancellation?.Cancel();
            preparationCancellation?.Dispose();
            preparationCancellation = null;
            DetachRaceEvents();
            if (service != null)
            {
                service.RegisterSimulation(null);
                service.MembersChanged -= MembersChanged;
                service.CommandReceived -= ReceiveCommand;
                service.SnapshotReceived -= ReceiveSnapshot;
                service.StateChanged -= StateChanged;
                await service.LeaveAsync();
                service.Dispose();
                service = null;
            }
            if (settings != null) UnityEngine.Object.Destroy(settings);
            Room = null;
            inputs.Clear();
            inputTicks.Clear();
            commandSequence = roomSequence = raceSequence = 0;
            preparedVersion = -1;
            appliedPhase = null;
            wasJoined = false;
            leaving = false;
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

        private static string LocalAddress()
        {
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces().Where(a => a.OperationalStatus == OperationalStatus.Up))
                {
                    var properties = adapter.GetIPProperties();
                    if (!properties.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any))) continue;
                    var local = properties.UnicastAddresses.Select(a => a.Address).FirstOrDefault(IsReachableLocalAddress);
                    if (local != null) return local.ToString();
                }
            }
            catch (Exception exception) when (exception is NetworkInformationException || exception is NotSupportedException)
            {
            }
            try
            {
                return Dns.GetHostAddresses(Dns.GetHostName()).FirstOrDefault(IsReachableLocalAddress)?.ToString() ?? "127.0.0.1";
            }
            catch (SocketException) { return "127.0.0.1"; }
        }

        private static bool IsReachableLocalAddress(IPAddress address) => address.AddressFamily == AddressFamily.InterNetwork
            && !IPAddress.IsLoopback(address) && !(address.GetAddressBytes()[0] == 169 && address.GetAddressBytes()[1] == 254);

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            // 连接中的取消由原 Create/Join 等待结束后独占收口，避免重入公共服务的 changing 门闩。
            if (!connecting) LeaveAsync().Forget(Debug.LogException);
        }
    }
}
