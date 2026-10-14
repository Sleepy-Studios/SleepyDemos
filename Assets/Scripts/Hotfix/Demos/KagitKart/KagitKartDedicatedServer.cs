using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Core.Runtime.Networking;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Hotfix.KagitKart
{
    /// 单房间无画面组合根，不启动客户端资源、声音、页面或个人存档。
    public sealed class KagitKartDedicatedServer : MonoBehaviour
    {
        [SerializeField] private GameObject networkHost;
        private readonly CancellationTokenSource lifetime = new();
        private readonly HashSet<string> consumedNonces = new();
        private readonly HashSet<string> departed = new();
        private readonly Dictionary<string, (KagitKartTrackData data, KagitKartAiPlan plan)> tracks = new();
        private KagitKartNetworkRoom network;
        private byte[] signingKey;
        private string rulesRoot;
        private bool ready;
        private bool stopping;

        public GameObject NetworkHost => networkHost;
        public KagitKartSourceCatalog Source { get; private set; }
        public KagitKartSession Session { get; private set; }
        public KagitKartGrandPrix GrandPrix { get; private set; }
        public ServerConfiguration Configuration { get; private set; }

        [Serializable]
        public sealed class ServerConfiguration
        {
            public string roomId, code, signingKey, controlUrl, controlToken, leaderMemberId, contentVersion;
            public int port, protocolVersion;
            public KagitKartRoomState configuration;
        }

        [Serializable]
        private sealed class TicketClaims
        {
            public string roomId, memberId, displayName, nonce;
            public long expiresAtUnixSeconds;
        }

        [Serializable]
        private sealed class RoomStatus
        {
            public bool ready;
            public int capacity;
            public string phase, error;
            public string[] memberIds, reservedMemberIds, departedMemberIds;
        }

        [Serializable]
        private sealed class ControlResponse
        {
            public string[] departedMemberIds;
        }

        private void Start() => RunAsync().Forget(exception =>
        {
            Debug.LogException(exception);
            Application.Quit(1);
        });

        private async UniTask RunAsync()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-sleepyRoomConfig");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("缺少房间启动配置。");
            Configuration = JsonConvert.DeserializeObject<ServerConfiguration>(File.ReadAllText(args[index + 1]));
            if (Configuration?.protocolVersion != KagitKartOnlineSettings.ProtocolVersion
                || Configuration.contentVersion != KagitKartOnlineSettings.ContentVersion
                || string.IsNullOrWhiteSpace(Configuration.roomId) || Configuration.code?.Length != 6
                || Configuration.port is < 1 or > 65535 || !Uri.TryCreate(Configuration.controlUrl, UriKind.Absolute, out var control)
                || !control.IsLoopback || control.Scheme != "http" || string.IsNullOrWhiteSpace(Configuration.controlToken))
            {
                throw new ArgumentException("房间启动配置或内容版本无效。");
            }
            signingKey = Convert.FromBase64String(Configuration.signingKey);
            if (signingKey.Length != 32) throw new ArgumentException("房间签名密钥长度无效。");
            rulesRoot = Path.Combine(Application.streamingAssetsPath, "KagitKartRules");
            Source = KagitKartSourceCatalog.Parse(File.ReadAllText(Path.Combine(rulesRoot, "SourceCatalog.json")));
            LoadTrack(Configuration.configuration.TrackId);
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 120;
            network = new KagitKartNetworkRoom(this, lifetime.Token);
            await network.StartServerAsync();
            ready = true;
            Debug.Log("卡丁车独立服务器已就绪，房间 " + Configuration.roomId);
            while (!lifetime.IsCancellationRequested)
            {
                await ReportStatusAsync();
                await UniTask.Delay(TimeSpan.FromSeconds(2), ignoreTimeScale: true, cancellationToken: lifetime.Token);
            }
        }

        private (KagitKartTrackData data, KagitKartAiPlan plan) LoadTrack(string id)
        {
            if (tracks.TryGetValue(id, out var loaded)) return loaded;
            string name = KagitKartCatalog.TrackAssetName(id) + ".json";
            var data = KagitKartTrackData.Parse(File.ReadAllText(Path.Combine(rulesRoot, "Tracks", name)));
            var plan = KagitKartAiPlan.Parse(File.ReadAllText(Path.Combine(rulesRoot, "AiPlans", name)), data);
            loaded = (data, plan);
            tracks.Add(id, loaded);
            return loaded;
        }

        internal void PrepareRound(KagitKartRoomState room)
        {
            if (room.Mode == KagitKartRaceMode.GrandPrix)
            {
                if (GrandPrix == null || GrandPrix.Completed || room.TrackId == "meadow") GrandPrix = new KagitKartGrandPrix(Source.roster);
                else if (GrandPrix.TrackId != room.TrackId && !GrandPrix.NextRound()) throw new InvalidOperationException("大奖赛章节尚未完成。");
            }
            else GrandPrix = null;
            var data = LoadTrack(room.TrackId);
            Session?.Dispose();
            Session = new KagitKartSession(Source, data.data, data.plan, room.Mode, null, room.SpeedClass, room.Seed,
                GrandPrix, grid: room.Grid, humanIds: room.Players.Select(player => player.CharacterId).ToArray());
            foreach (var player in room.Players)
            {
                var kart = Session.FindNetworkKart(player.CharacterId);
                if (kart != null) kart.Autopilot = !player.Connected;
            }
        }

        internal NetworkAuthenticationResult Authenticate(NetworkAuthenticationRequest request)
        {
            try
            {
                if (request.Ticket == null || request.Ticket.Length > 4096) throw new ArgumentException();
                var parts = request.Ticket.Split('.');
                if (parts.Length != 2) throw new ArgumentException();
                using var hmac = new HMACSHA256(signingKey);
                byte[] expected = hmac.ComputeHash(Encoding.ASCII.GetBytes(parts[0]));
                byte[] signature = DecodeBase64Url(parts[1]);
                int difference = expected.Length ^ signature.Length;
                for (int i = 0; i < expected.Length && i < signature.Length; i++) difference |= expected[i] ^ signature[i];
                if (difference != 0) throw new ArgumentException();
                var claims = JsonUtility.FromJson<TicketClaims>(Encoding.UTF8.GetString(DecodeBase64Url(parts[0])));
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (claims == null || claims.roomId != Configuration.roomId || claims.expiresAtUnixSeconds < now
                    || claims.expiresAtUnixSeconds > now + 60 || string.IsNullOrWhiteSpace(claims.memberId)
                    || claims.memberId.Length > 128 || string.IsNullOrWhiteSpace(claims.displayName)
                    || claims.displayName.Length > 32 || string.IsNullOrWhiteSpace(claims.nonce)
                    || claims.nonce.Length > 128 || consumedNonces.Contains(claims.nonce) || departed.Contains(claims.memberId))
                {
                    throw new ArgumentException();
                }
                var room = network.Room;
                var existing = Array.Find(room.Players, player => player.MemberId == claims.memberId);
                if (existing == null && (room.Phase != "Lobby" || room.Players.Length >= room.Capacity))
                {
                    return new NetworkAuthenticationResult(false, reason: "房间已满或比赛已经开始。");
                }
                if (existing != null && !existing.Connected && existing.ResumeUntil < Time.realtimeSinceStartupAsDouble)
                {
                    return new NetworkAuthenticationResult(false, reason: "重连资格已过期。");
                }
                consumedNonces.Add(claims.nonce);
                return new NetworkAuthenticationResult(true, claims.memberId, claims.displayName,
                    replaceExistingConnection: existing != null);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException)
            {
                return new NetworkAuthenticationResult(false, reason: "入房凭据无效或已过期，请重新加入。");
            }
        }

        private static byte[] DecodeBase64Url(string value)
        {
            string encoded = value.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='));
        }

        internal void MarkDeparted(string memberId) => departed.Add(memberId);
        internal bool HasDeparted(string memberId) => departed.Contains(memberId);

        private async UniTask ReportStatusAsync()
        {
            var room = network.Room;
            var status = new RoomStatus
            {
                ready = ready, phase = room.Phase, capacity = room.Capacity,
                memberIds = room.Players.Where(player => player.Connected).Select(player => player.MemberId).ToArray(),
                reservedMemberIds = room.Players.Where(player => !player.Connected && player.ResumeUntil > Time.realtimeSinceStartupAsDouble)
                    .Select(player => player.MemberId).ToArray(),
                departedMemberIds = departed.ToArray()
            };
            using var request = new UnityWebRequest(Configuration.controlUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(status)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + Configuration.controlToken);
            request.timeout = 4;
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);
                var response = JsonUtility.FromJson<ControlResponse>(string.IsNullOrWhiteSpace(request.downloadHandler.text) ? "{}" : request.downloadHandler.text);
                if (response?.departedMemberIds != null)
                    foreach (string memberId in response.departedMemberIds) network.RemovePlayer(memberId);
            }
            catch (UnityWebRequestException)
            {
                Debug.LogWarning("房间控制服务暂时不可达。");
            }
        }

        private void OnApplicationQuit()
        {
            stopping = true;
            lifetime.Cancel();
        }

        private void OnDestroy()
        {
            lifetime.Cancel();
            if (!stopping) network?.Dispose();
            Session?.Dispose();
            lifetime.Dispose();
        }
    }
}
