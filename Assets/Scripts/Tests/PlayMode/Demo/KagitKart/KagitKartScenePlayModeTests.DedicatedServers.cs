#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Networking;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.KagitKart;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using Process = System.Diagnostics.Process;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed partial class KagitKartScenePlayModeTests
    {
        [UnityTest, Explicit("需要已启动的外部.NET RoomService和当前Windows Dedicated构建"), Timeout(240000)]
        public IEnumerator ExternalDedicatedRoomCodeClientsAndReconnectPreserveIdentityAndLeader()
        {
            return RunExternalDedicatedAsync(false).ToCoroutine();
        }

        [UnityTest, Explicit("自然首章、Results恢复及真实准备超时保GP积分；需要外部Windows服务，最长约14分钟"), Timeout(900000)]
        public IEnumerator ExternalDedicatedNaturalGrandPrixRoundRestoresResultsBaselineAndPoints()
        {
            return RunExternalDedicatedAsync(true).ToCoroutine();
        }

        [UnityTest, Explicit("需要空闲的外部Windows .NET RoomService与当前Dedicated构建；真实取消首次创建并重试"), Timeout(180000)]
        public IEnumerator ExternalDedicatedCreateBackCancelsPendingAuthorizationAndAllowsRetry()
        {
            return RunExternalCreateCancellationAsync().ToCoroutine();
        }

        [UnityTest, Explicit("需要空闲的Windows RoomService与Dedicated构建；两个并行房间、真实60秒恢复期限及AI代驾"), Timeout(240000)]
        public IEnumerator ExternalDedicatedParallelRoomsKeepIdentityIsolationAndExpireDisconnectedSeats()
        {
            return RunExternalParallelRoomExpiryAsync().ToCoroutine();
        }

        private static async UniTask RunExternalParallelRoomExpiryAsync()
        {
            string executable = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../SleepyDemos-Server/artifacts/game-server/win-x64/KagitKartServer.exe"));
            Assert.That(File.Exists(executable), Is.True);
            string url = KagitKartOnlineSettings.Load().directoryUrl;
            var directory = new RoomDirectoryClient(url);
            using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(205));
            using (var health = UnityWebRequest.Get(url.TrimEnd('/') + "/health"))
            {
                await health.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);
                Assert.That(health.responseCode, Is.EqualTo(200));
            }
            var previous = Process.GetProcessesByName("KagitKartServer");
            int[] previousIds = previous.Select(process => process.Id).ToArray();
            foreach (var process in previous)
            {
                process.Dispose();
            }
            var owned = new List<Process>();
            var grants = new List<RoomJoinGrant>();
            var peers = new List<ExternalKartPeer>();
            try
            {
                // 请求顺序提交以保留每份清理凭据；两个真正的游戏进程随后同时运行。
                for (int room = 0; room < 2; room++)
                {
                    var configuration = new KagitKartRoomState
                    {
                        Name = "双房隔离" + room + Guid.NewGuid().ToString("N").Substring(0, 8), Capacity = 2,
                        Mode = room == 0 ? KagitKartRaceMode.SingleRace : KagitKartRaceMode.GrandPrix,
                        SpeedClass = 80, TrackId = room == 0 ? "bosphorus" : "meadow"
                    };
                    var leaderGrant = await directory.CreateAsync(JsonUtility.ToJson(new ExternalCreateRequest { configuration = configuration }), lifetime.Token);
                    grants.Add(leaderGrant);
                    grants.Add(await directory.JoinAsync(leaderGrant.code, JsonUtility.ToJson(new ExternalJoinRequest()), lifetime.Token));
                }
                Assert.That(grants.Select(grant => grant.memberId).Distinct().Count(), Is.EqualTo(4));
                Assert.That(grants[0].roomId, Is.Not.EqualTo(grants[2].roomId));
                Assert.That(grants[0].code, Is.Not.EqualTo(grants[2].code));
                Assert.That(grants[0].port, Is.Not.EqualTo(grants[2].port));
                foreach (var grant in grants)
                {
                    var peer = new ExternalKartPeer();
                    peers.Add(peer);
                    await peer.ConnectAsync(grant, lifetime.Token);
                }
                await WaitExternalAsync(() =>
                {
                    foreach (var peer in peers)
                    {
                        if (peer.Failure != null)
                        {
                            throw new InvalidOperationException("双房UDP握手失败。", peer.Failure);
                        }
                    }
                    return peers.All(peer => peer.Room?.Players.Length == 2 && peer.Transport.Members.Count == 2);
                }, "两房各两份真实票据均完成UDP入席", lifetime.Token);
                CaptureExternalCreatedServers(executable, previousIds, owned);
                Assert.That(owned.Count, Is.EqualTo(2), "必须是两台同时运行的独立Unity服务器，不能用单进程伪房间代替。");
                Assert.That(owned.All(process => !process.HasExited), Is.True);
                for (int room = 0; room < 2; room++)
                {
                    string[] memberIds = grants.Skip(room * 2).Take(2).Select(grant => grant.memberId).ToArray();
                    foreach (var peer in peers.Skip(room * 2).Take(2))
                    {
                        Assert.That(peer.Transport.AuthorityMemberId, Is.EqualTo(grants[room * 2].roomId));
                        CollectionAssert.AreEquivalent(memberIds, peer.Transport.Members.Select(member => member.MemberId));
                        CollectionAssert.AreEquivalent(memberIds, peer.Room.Players.Select(player => player.MemberId));
                        Assert.That(peer.Room.LeaderMemberId, Is.EqualTo(memberIds[0]));
                        Assert.That(peer.Room.TrackId, Is.EqualTo(room == 0 ? "bosphorus" : "meadow"));
                        Assert.That(peer.Transport.IsAuthority, Is.False);
                    }
                }
                await peers[0].SendAsync("character", false, lifetime.Token, "ahtapot");
                await WaitExternalAsync(() => peers[0].Room.Players.Single(player => player.MemberId == grants[0].memberId).CharacterId == "ahtapot",
                    "第一房修改自己的真人网格角色", lifetime.Token);
                Assert.That(peers[2].Room.Players.Single(player => player.MemberId == grants[2].memberId).CharacterId, Is.EqualTo("tilki"),
                    "角色配置不能影响另一房相同网格位置的成员。");
                foreach (var peer in peers)
                {
                    await peer.SendAsync("ready", true, lifetime.Token);
                }
                await WaitExternalAsync(() => peers.All(peer => peer.Room.Players.All(player => player.Ready)),
                    "各房独立收到自己的准备状态", lifetime.Token);
                await peers[0].SendAsync("start", false, lifetime.Token);
                await peers[2].SendAsync("start", false, lifetime.Token);
                await WaitExternalAsync(() =>
                {
                    foreach (var peer in peers)
                    {
                        if (peer.Failure != null)
                        {
                            throw new InvalidOperationException("双房比赛基线失败。", peer.Failure);
                        }
                    }
                    return peers.All(peer => peer.BaselineReceived && peer.Room.Phase == "Racing"
                        && peer.Session?.Race.State == KagitKartRaceState.Running && peer.Room.Players.All(player => player.Loaded));
                }, "两台权威服务器分别完成真实Loaded与起跑", lifetime.Token);
                foreach (var peer in peers)
                {
                    Assert.That(peer.Session.Karts.Count, Is.EqualTo(8));
                    CollectionAssert.AreEquivalent(peer.Room.Grid, peer.Session.Karts.Select(kart => kart.Id));
                    Assert.That(peer.Session.Player.Id, Is.EqualTo(peer.Room.Players.Single(player => player.MemberId == peer.Grant.memberId).CharacterId),
                        "预测载体只能绑定当前房间中此稳定身份的网格角色。");
                }
                Assert.That(peers[0].Room.Mode, Is.EqualTo(KagitKartRaceMode.SingleRace));
                Assert.That(peers[2].Room.Mode, Is.EqualTo(KagitKartRaceMode.GrandPrix));
                string disconnectedId = grants[1].memberId;
                string character = peers[0].Room.Players.Single(player => player.MemberId == disconnectedId).CharacterId;
                await peers[1].DisconnectAsync(false, lifetime.Token);
                await WaitExternalAsync(() => peers[0].Room.Players.Any(player => player.MemberId == disconnectedId && !player.Connected),
                    "普通成员真实UDP断开并保留恢复席位", lifetime.Token, peers[0]);
                double disconnectedAt = Time.realtimeSinceStartupAsDouble;
                var abandoned = peers[0].Session.Karts.Single(kart => kart.Id == character);
                double beforeProgress = abandoned.Progress;
                double otherRoomTime = peers[2].Session.RaceTime;
                // 被测赛车是该Peer的远端车辆：只有正式权威快照会改变它的Progress，夹具只预测自己的本地赛车。
                await WaitExternalAsync(() => abandoned.Autopilot && Math.Abs(abandoned.Progress - beforeProgress) > 1
                    && peers[2].Session.RaceTime > otherRoomTime + 2 && peers[3].Session.RaceTime > otherRoomTime + 2,
                    "断线赛车由服务器AI代驾推进，另一房同时继续比赛", lifetime.Token, peers[0], 20);
                await WaitExternalAsync(() => Time.realtimeSinceStartupAsDouble >= disconnectedAt + 65,
                    "等待真实恢复期限结束，不修改服务器时间", lifetime.Token, peers[0], 70);
                Assert.That(peers[0].Room.Phase, Is.EqualTo("Racing"));
                Assert.That(peers[2].Room.Phase, Is.EqualTo("Racing"));
                Assert.That(owned.All(process => !process.HasExited), Is.True);
                Assert.That(peers[0].Room.LeaderMemberId, Is.EqualTo(grants[0].memberId));
                Assert.That(peers[2].Room.Players.All(player => player.Connected), Is.True);
                CollectionAssert.AreEquivalent(grants.Skip(2).Select(grant => grant.memberId), peers[2].Transport.Members.Select(member => member.MemberId));
                Assert.That(peers[2].Room.Players.Any(player => player.MemberId == disconnectedId), Is.False);
                foreach (var expected in new[]
                {
                    (path: "/v1/sessions/resume", json: JsonUtility.ToJson(new ExternalResumeRequest { resumeToken = grants[1].resumeToken }), status: 401L, code: "session_expired"),
                    (path: "/v1/rooms/" + grants[0].code + "/join", json: JsonUtility.ToJson(new ExternalJoinRequest()), status: 409L, code: "room_not_in_lobby")
                })
                {
                    using var request = new UnityWebRequest(url.TrimEnd('/') + expected.path, "POST");
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(expected.json));
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.timeout = 10;
                    try
                    {
                        await request.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);
                    }
                    catch (UnityWebRequestException) when (request.responseCode == expected.status)
                    {
                        // 只接受明确的业务拒绝；其它网络错误和服务故障不能充当过期证据。
                    }
                    Assert.That(request.responseCode, Is.EqualTo(expected.status));
                    Assert.That(JsonUtility.FromJson<ExternalDirectoryError>(request.downloadHandler.text).code, Is.EqualTo(expected.code));
                }
            }
            finally
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    // 每个客户端都执行自己的UDP与HTTP离房，不回收共享RoomService。
                    foreach (var peer in peers)
                    {
                        await peer.DisconnectAsync(true, cleanup.Token);
                    }
                    foreach (var grant in grants)
                    {
                        await directory.LeaveAsync(JsonUtility.ToJson(new ExternalResumeRequest { resumeToken = grant.resumeToken }), cleanup.Token);
                    }
                    CaptureExternalCreatedServers(executable, previousIds, owned);
                    await WaitExternalAsync(() => owned.All(process => process.HasExited), "两房所有身份退出后回收独立服务器", cleanup.Token, seconds: 20);
                }
                finally
                {
                    foreach (var peer in peers)
                    {
                        peer.Dispose();
                    }
                    foreach (var process in owned)
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                        process.Dispose();
                    }
                }
            }
        }

        private static async UniTask RunExternalCreateCancellationAsync()
        {
            string executable = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../SleepyDemos-Server/artifacts/game-server/win-x64/KagitKartServer.exe"));
            Assert.That(File.Exists(executable), Is.True, "需要正式Windows Dedicated构建。");
            string url = KagitKartOnlineSettings.Load().directoryUrl;
            using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(150));
            using (var health = UnityWebRequest.Get(url.TrimEnd('/') + "/health"))
            {
                await health.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);
                Assert.That(health.responseCode, Is.EqualTo(200));
            }
            var previous = Process.GetProcessesByName("KagitKartServer");
            int[] previousIds = previous.Select(process => process.Id).ToArray();
            foreach (var process in previous)
            {
                process.Dispose();
            }
            var owned = new List<Process>();
            KagitKartWorld world = null;
            try
            {
                await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart);
                await WaitExternalAsync(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null && !world.Busy
                    && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "正式AppEntrance进入Demo", lifetime.Token);
                world.Dispatch(KagitKartCommand.CreateRoom);
                await WaitExternalAsync(() => !world.Busy && UIManager.Instance.Get<KagitKartCreateRoomView>()?.State == ViewState.Visible,
                    "正式创建房间表单", lifetime.Token);
                var createView = UIManager.Instance.Get<KagitKartCreateRoomView>();
                var back = Button(createView, "Back");
                var label = back.GetComponentInChildren<TMPro.TMP_Text>(true);
                Assert.That(label, Is.Not.Null);

                // 同一帧提交真实HTTP并点真实Back；不等待服务器ready，不用代理或伪造授权。
                var create = world.CreateOnlineRoomAsync("取消首次授权契约", 2, KagitKartRaceMode.SingleRace, 120, "meadow").Preserve();
                Assert.That(world.ConnectingOnlineRoom && world.Busy, Is.True);
                Assert.That(world.Network.Connected, Is.False);
                Assert.That(back.interactable, Is.True, "首次连接期间必须能取消，不能随Busy禁用返回。");
                Assert.That(label.text, Is.EqualTo("取消连接"));
                back.onClick.Invoke();
                Assert.That(world.CancelingOnlineConnection, Is.True);
                Assert.That(back.interactable, Is.False, "原任务收口期间不能重复提交取消。");
                Assert.That(label.text, Is.EqualTo("正在取消…"));
                bool sawRoom = false;
                await WaitExternalAsync(() =>
                {
                    CaptureExternalCreatedServers(executable, previousIds, owned);
                    sawRoom |= UIManager.Instance.Get<KagitKartRoomView>()?.State == ViewState.Visible;
                    return create.Status.IsCompleted() && !world.Busy && !world.ConnectingOnlineRoom && !world.CancelingOnlineConnection
                        && UIManager.Instance.Get<KagitKartOnlineView>()?.State == ViewState.Visible;
                }, "原HTTP事务结束并撤销晚到授权后，返回正式Online页面", lifetime.Token, seconds: 115);
                await create;
                Assert.That(sawRoom, Is.False, "取消后晚到授权不能复活房间页面。");
                Assert.That(world.Network.Connected, Is.False);
                Assert.That(world.Network.ConnectionCleanupPending, Is.False);
                Assert.That(world.Network.Code, Is.Null, "已撤销的首次授权不能残留在客户端会话。");
                Assert.That(owned.Count, Is.EqualTo(1), "取消的真实创建事务必须确实启动独立服务器，不能以请求根本未到达代替取消证据。");
                await WaitExternalAsync(() => owned.All(process => process.HasExited),
                    "晚到创建授权撤销后回收本房间服务器进程", lifetime.Token, seconds: 20);

                await world.CreateOnlineRoomAsync("取消后重新创建契约", 2, KagitKartRaceMode.SingleRace, 120, "meadow")
                    .AttachExternalCancellation(lifetime.Token);
                CaptureExternalCreatedServers(executable, previousIds, owned);
                await WaitExternalAsync(() => world.Network.Connected && !world.Busy && world.Network.Room?.Players.Length == 1
                    && UIManager.Instance.Get<KagitKartRoomView>()?.State == ViewState.Visible,
                    "取消收口后重新创建并正常入席", lifetime.Token);
                Assert.That(world.Network.Room.Players[0].MemberId, Is.EqualTo(world.Network.LocalMemberId));
                Assert.That(world.Network.Code, Does.Match("^[0-9]{6}$"));
                Assert.That(owned.Count, Is.EqualTo(2), "重试应创建新的独立房间服务器。");
                world.Dispatch(KagitKartCommand.LeaveRoom);
                await WaitExternalAsync(() => !world.Busy && !world.Network.Connected
                    && UIManager.Instance.Get<KagitKartOnlineView>()?.State == ViewState.Visible,
                    "重试成功后通过正式离房流程清理", lifetime.Token);
                await WaitExternalAsync(() => owned.All(process => process.HasExited),
                    "两次真实创建均无服务器进程残留", lifetime.Token, seconds: 20);
                CaptureExternalCreatedServers(executable, previousIds, owned);
                Assert.That(owned.All(process => process.HasExited), Is.True);
            }
            finally
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                try
                {
                    if (world?.Network != null)
                    {
                        await world.Network.LeaveAsync();
                    }
                    CaptureExternalCreatedServers(executable, previousIds, owned);
                    await WaitExternalAsync(() => owned.All(process => process.HasExited),
                        "清理本测试创建的服务器进程", cleanup.Token, seconds: 15);
                }
                finally
                {
                    foreach (var process in owned)
                    {
                        // 只处理本测试开始后出现且核对过正式构建路径的进程。
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                        process.Dispose();
                    }
                }
            }
        }

        private static void CaptureExternalCreatedServers(string executable, int[] previousIds, List<Process> owned)
        {
            foreach (var process in Process.GetProcessesByName("KagitKartServer"))
            {
                bool keep = false;
                try
                {
                    if (previousIds.Contains(process.Id) || owned.Any(value => value.Id == process.Id) || process.HasExited)
                    {
                        continue;
                    }
                    if (string.Equals(Path.GetFullPath(process.MainModule.FileName), executable, StringComparison.OrdinalIgnoreCase))
                    {
                        owned.Add(process);
                        keep = true;
                    }
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // 进程恰好在枚举与读取路径之间完成目录回收。
                }
                catch (System.ComponentModel.Win32Exception) when (process.HasExited)
                {
                    // 已退出进程的模块信息可能已不可读，不能据此认领其它进程。
                }
                finally
                {
                    if (!keep)
                    {
                        process.Dispose();
                    }
                }
            }
        }

        private static async UniTask RunExternalDedicatedAsync(bool awaitNaturalResults)
        {
            string executable = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../../SleepyDemos-Server/artifacts/game-server/win-x64/KagitKartServer.exe"));
            Assert.That(File.Exists(executable), Is.True, "先用正式Server构建菜单生成外部Unity专服。");
            string url = KagitKartOnlineSettings.Load().directoryUrl;
            using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(awaitNaturalResults ? 850 : 210));
            using (var health = UnityWebRequest.Get(url.TrimEnd('/') + "/health"))
            {
                await health.SendWebRequest().ToUniTask(cancellationToken: lifetime.Token);
                Assert.That(health.responseCode, Is.EqualTo(200), "需要真实.NET房间目录，不能用内存房间替代。");
            }
            var previousProcesses = Process.GetProcessesByName("KagitKartServer");
            int[] previousIds = previousProcesses.Select(process => process.Id).ToArray();
            foreach (var process in previousProcesses) process.Dispose();
            KagitKartWorld world = null;
            ExternalKartPeer peer = null;
            INetworkSessionService unloadedPeer = null;
            NetworkSessionSettings unloadedSettings = null;
            RoomJoinGrant unloadedGrant = null;
            Action<NetworkSnapshotEvent> unloadedSnapshots = null;
            Process gameServer = null;
            try
            {
                await GameSceneNavigator.Instance.SwitchAsync(GameSceneId.KagitKart);
                await WaitExternalAsync(() => (world = Object.FindFirstObjectByType<KagitKartWorld>()) != null && !world.Busy
                    && UIManager.Instance.Get<KagitKartTitleView>()?.State == ViewState.Visible, "正式Demo入口", lifetime.Token);
                await world.CreateOnlineRoomAsync("外部专服契约", 2, KagitKartRaceMode.GrandPrix, 200, "meadow");
                Assert.That(world.Network?.Connected, Is.True, world.Feedback ?? "创建外部房间失败。");
                await WaitExternalAsync(() => world.Network.Room?.Players.Length == 1, "目录授权的创建者入席", lifetime.Token);
                var spawned = Process.GetProcessesByName("KagitKartServer");
                var owned = spawned.Where(process => !previousIds.Contains(process.Id)).ToArray();
                Assert.That(owned.Length, Is.EqualTo(1), "创建房间应启动独立Unity游戏服务器进程。");
                var candidate = owned[0];
                Assert.That(Path.GetFullPath(candidate.MainModule.FileName), Is.EqualTo(executable).IgnoreCase);
                gameServer = candidate;
                foreach (var process in spawned.Where(process => process.Id != gameServer.Id))
                {
                    process.Dispose();
                }
                string code = world.Network.Code;
                Assert.That(code, Does.Match("^[0-9]{6}$"));
                string creatorId = world.Network.LocalMemberId;
                peer = new ExternalKartPeer();
                var directory = new RoomDirectoryClient(url);
                var grant = await directory.JoinAsync(code, JsonUtility.ToJson(new ExternalJoinRequest()), lifetime.Token);
                await peer.ConnectAsync(grant, lifetime.Token);
                await WaitExternalAsync(() => peer.Room?.Players.Length == 2 && world.Network.Room.Players.Length == 2,
                    "HTTP票据和真实UDP握手后的两位车手", lifetime.Token, peer);
                Assert.That(peer.Transport.IsAuthority, Is.False);
                Assert.That(peer.Transport.Members.Count, Is.EqualTo(2), "独立服务器不占玩家席位。");
                Assert.That(peer.Transport.Members.Any(member => member.MemberId == peer.Transport.AuthorityMemberId), Is.False);
                Assert.That(peer.Transport.LocalMemberId, Is.EqualTo(grant.memberId));
                Assert.That(peer.Room.LeaderMemberId, Is.EqualTo(creatorId));
                await AssertExternalTicketReplayRejectedAsync(grant, lifetime.Token);
                Assert.That(peer.Transport.State, Is.EqualTo(NetworkSessionState.Joined), "复用票据不能顶替原客户端。");
                Assert.That(peer.Transport.LocalMemberId, Is.EqualTo(grant.memberId));
                Assert.That(world.Network.Room.Players.Count(player => player.Connected), Is.EqualTo(2));
                await peer.SendAsync("ready", true, lifetime.Token);
                world.Network.SetReady(true);
                await WaitExternalAsync(() => world.Network.CanStart, "服务器确认全员准备", lifetime.Token, peer);
                world.Network.StartRace();
                await WaitExternalAsync(() => peer.BaselineReceived && peer.Room.Phase == "Racing" &&
                    peer.Session?.Race.State == KagitKartRaceState.Running && world.Session?.Race.State == KagitKartRaceState.Running,
                    "真实Loaded、权威基线和正常倒计时起跑", lifetime.Token, peer);
                Assert.That(peer.Session.Karts.Count, Is.EqualTo(8), "两个真人席位外应使用正式CPU车手。");
                await WaitExternalAsync(() => peer.Reconciles > 2 && peer.Replays > 0,
                    "官方预测校正与重放到真实KagitKartSession", lifetime.Token, peer);
                string stableId = peer.Transport.LocalMemberId;
                double beforeDisconnect = peer.Session.RaceTime;
                string resumeToken = peer.Grant.resumeToken;
                await peer.DisconnectAsync(false, lifetime.Token);
                await WaitExternalAsync(() => world.Network.Room.Players.Any(player => player.MemberId == stableId && !player.Connected),
                    "真实UDP短断开保留服务器重连席位", lifetime.Token);
                var resumed = await directory.ResumeAsync(JsonUtility.ToJson(new ExternalResumeRequest { resumeToken = resumeToken }), lifetime.Token);
                Assert.That(resumed.memberId, Is.EqualTo(stableId));
                await peer.ConnectAsync(resumed, lifetime.Token);
                await WaitExternalAsync(() => peer.BaselineReceived && peer.Room.Players.Any(player => player.MemberId == stableId && player.Loaded),
                    "Racing阶段凭秘密Resume恢复原身份和完整基线", lifetime.Token, peer);
                Assert.That(peer.Session.RaceTime, Is.GreaterThan(beforeDisconnect));
                var creatorRoom = world.Network;
                var oldWorldTransport = ReadExternalTransport(creatorRoom);
                MonoBehaviour oldCarrier = null;
                await WaitExternalAsync(() => (oldCarrier = FindExternalOwnedCarrier(oldWorldTransport)) != null,
                    "正式World拥有官方预测载体", lifetime.Token, peer, 15);
                int oldCarrierId = oldCarrier.GetInstanceID();
                var oldWorldSession = world.Session;
                double beforeWorldDisconnect = peer.Session.RaceTime;
                bool sawAutomaticReconnect = false;
                Action<NetworkSessionState> watchReconnect = _ => sawAutomaticReconnect |= creatorRoom.Reconnecting;
                oldWorldTransport.StateChanged += watchReconnect;
                try
                {
                    StopExternalSdkClient(oldWorldTransport);
                    await WaitExternalAsync(() => sawAutomaticReconnect || creatorRoom.Reconnecting,
                        "SDK真实断开后正式World进入自动重连", lifetime.Token, peer, 15);
                    sawAutomaticReconnect |= creatorRoom.Reconnecting;
                    await WaitExternalAsync(() => peer.Room.LeaderMemberId == stableId,
                        "原房主短断开时剩余车手取得房主权限", lifetime.Token, peer, 15);
                    await WaitExternalAsync(() => !creatorRoom.Reconnecting && creatorRoom.Connected && !world.Busy
                        && creatorRoom.LocalMemberId == creatorId && world.Session != null
                        && world.Session.RaceTime > beforeWorldDisconnect
                        && creatorRoom.Room.Players.Any(player => player.MemberId == creatorId && player.Connected && player.Loaded),
                        "正式World自动Resume同一稳定身份并恢复比赛完整基线", lifetime.Token, peer, 75);
                }
                finally
                {
                    oldWorldTransport.StateChanged -= watchReconnect;
                }
                Assert.That(sawAutomaticReconnect, Is.True);
                Assert.That(world.Session, Is.Not.SameAs(oldWorldSession), "重连必须重新加载并恢复客户端模拟。");
                var restoredTransport = ReadExternalTransport(creatorRoom);
                Assert.That(restoredTransport, Is.Not.SameAs(oldWorldTransport));
                MonoBehaviour restoredCarrier = null;
                await WaitExternalAsync(() => (restoredCarrier = FindExternalOwnedCarrier(restoredTransport)) != null,
                    "恢复后的正式World重新取得官方拥有者预测载体", lifetime.Token, peer, 15);
                Assert.That(restoredCarrier.GetInstanceID(), Is.Not.EqualTo(oldCarrierId));
                Assert.That(creatorRoom.IsHost, Is.False, "原房主恢复稳定身份后不能夺回已转交的房主权限。");
                Assert.That(creatorRoom.Room.LeaderMemberId, Is.EqualTo(stableId));
                double afterAutomaticResume = peer.Session.RaceTime;
                await WaitExternalAsync(() => peer.Session.RaceTime > afterAutomaticResume + 1 && creatorRoom.Racing,
                    "正式World恢复后外部权威比赛仍持续推进", lifetime.Token, peer, 15);
                if (!awaitNaturalResults)
                {
                    var creatorGrant = (RoomJoinGrant)typeof(KagitKartNetworkRoom)
                        .GetField("grant", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(creatorRoom);
                    string creatorResumeToken = creatorGrant.resumeToken;
                    await creatorRoom.LeaveAsync();
                    await WaitExternalAsync(() => peer.Room.LeaderMemberId == stableId && peer.Room.Players.All(player => player.MemberId != creatorId || !player.Connected),
                        "原房主主动退出后剩余真实车手保留已转交的房主权限", lifetime.Token, peer);
                    await AssertExternalResumeRevokedAsync(url, creatorResumeToken, lifetime.Token);
                    double afterLeave = peer.Session.RaceTime;
                    await WaitExternalAsync(() => peer.Session.RaceTime > afterLeave + 1 && peer.Room.Phase == "Racing",
                        "房主退出不能停止外部权威比赛", lifetime.Token, peer);
                    Assert.That(gameServer.HasExited, Is.False);
                    return;
                }

                // Peer只用原作AI规划生成输入发送；完赛、圈数和积分全部由外部服务器自然推进。
                world.Session.Pause.RequestPause(LocalPauseReason.User);
                await WaitExternalAsync(() => UIManager.Instance.Get<KagitKartPauseView>()?.State == ViewState.Visible,
                    "客户端暂停菜单实际显示，外部比赛继续", lifetime.Token, peer);
                await WaitExternalAsync(() => peer.Room.Phase == "Results" && peer.Session.Race.State == KagitKartRaceState.Done,
                    "正式AI及玩家输入自然完成大奖赛首章", lifetime.Token, peer, 720);
                await WaitExternalAsync(() => !world.Busy && world.Session.Race.State == KagitKartRaceState.Done
                    && UIManager.Instance.Get<KagitKartResultsView>()?.State == ViewState.Visible
                    && UIManager.Instance.Get<KagitKartPauseView>()?.State != ViewState.Visible,
                    "客户端暂停期间服务器结算后，正式成绩页必须替换暂停页", lifetime.Token, peer);
                Assert.That(peer.Session.Race.Results.Count, Is.EqualTo(8));
                Assert.That(peer.Session.Race.Entries.Any(entry => !entry.Estimated && entry.Progress.Finished
                    && entry.Progress.LapTimes.Count >= peer.Session.Race.Laps), Is.True, "至少一位实际跑完所有圈数，不能以篡改圈数作为自然比赛证据。");
                var expectedPoints = peer.Room.CupPoints.ToArray();
                var expectedPlaces = peer.Room.CupPlaces.ToArray();
                var expectedTimes = peer.Room.CupTimes.ToArray();
                resumeToken = peer.Grant.resumeToken;
                await peer.DisconnectAsync(false, lifetime.Token);
                await UniTask.Delay(2500, ignoreTimeScale: true, cancellationToken: lifetime.Token);
                resumed = await directory.ResumeAsync(JsonUtility.ToJson(new ExternalResumeRequest { resumeToken = resumeToken }), lifetime.Token);
                await peer.ConnectAsync(resumed, lifetime.Token);
                await WaitExternalAsync(() => peer.BaselineReceived && peer.Room.Phase == "Results" && peer.Session.Race.State == KagitKartRaceState.Done,
                    "Results阶段重连恢复完整排名和GP积分", lifetime.Token, peer);
                Assert.That(peer.Transport.LocalMemberId, Is.EqualTo(stableId));
                Assert.That(peer.GrandPrix.Index, Is.Zero);
                Assert.That(peer.GrandPrix.RoundComplete, Is.True);
                CollectionAssert.AreEqual(expectedPoints, peer.Room.CupPoints);
                CollectionAssert.AreEqual(expectedPlaces, peer.Room.CupPlaces);
                CollectionAssert.AreEqual(expectedTimes, peer.Room.CupTimes);
                foreach (var result in peer.Session.Race.Results)
                {
                    var standing = peer.GrandPrix.SortedStandings().Single(value => value.CharacterId == result.CharacterId);
                    Assert.That(result.Points, Is.EqualTo((int)peer.Source.config.race.points[result.Place - 1]));
                    Assert.That(standing.Points, Is.EqualTo(result.Points), "首章Results恢复不能重复累计GP积分。");
                    Assert.That(standing.LastPlace, Is.EqualTo(result.Place));
                    Assert.That(standing.TotalTime, Is.EqualTo(result.Time).Within(0.000001));
                }

                // 保留自然首章的非零积分，再用同一身份真实Resume成只Ready、不Loaded的Core客户端。
                Assert.That(expectedPoints.Any(points => points > 0), Is.True);
                string[] expectedNextGrid = peer.GrandPrix.SortedStandings().Reverse().Select(standing => standing.CharacterId).ToArray();
                resumeToken = peer.Grant.resumeToken;
                await peer.DisconnectAsync(false, lifetime.Token);
                unloadedGrant = await directory.ResumeAsync(JsonUtility.ToJson(new ExternalResumeRequest { resumeToken = resumeToken }), lifetime.Token);
                unloadedSettings = ScriptableObject.CreateInstance<NetworkSessionSettings>();
                unloadedPeer = NetworkSessionServices.CreateInternet(unloadedSettings);
                KagitKartRoomState unloadedRoom = null;
                unloadedSnapshots = snapshot =>
                {
                    if (snapshot.Snapshot.Channel == 710)
                    {
                        unloadedRoom = JsonUtility.FromJson<KagitKartRoomState>(Encoding.UTF8.GetString(snapshot.Snapshot.Payload));
                    }
                };
                unloadedPeer.SnapshotReceived += unloadedSnapshots;
                await unloadedPeer.JoinAsync(new NetworkJoinOptions
                {
                    Address = unloadedGrant.address, Port = checked((ushort)unloadedGrant.port), Ticket = unloadedGrant.ticket,
                    ProtocolVersion = checked((ushort)unloadedGrant.protocolVersion), ContentVersion = unloadedGrant.contentVersion
                }, lifetime.Token);
                await WaitExternalAsync(() => unloadedRoom?.Phase == "Results" && unloadedRoom.Players.Count(player => player.Connected) == 2
                    && world.Network.IsHost, "完整GP积分下的真实未Loaded成员与剩余房主", lifetime.Token);
                ulong unloadedSequence = 0;
                await unloadedPeer.SendCommandAsync(new NetworkCommand(710, ++unloadedSequence, Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ExternalCommand
                {
                    Kind = "ready", RoundVersion = unloadedRoom.RoundVersion, Ready = true
                }))), lifetime.Token);
                world.Network.SetReady(true);
                await WaitExternalAsync(() => world.Network.CanStart, "下一章双方准备，Core夹具不创建Session也不发送Loaded", lifetime.Token);
                double preparationStarted = Time.realtimeSinceStartupAsDouble;
                world.Network.StartRace();
                await WaitExternalAsync(() => unloadedRoom.Phase == "Preparing" && unloadedRoom.ServerLoaded
                    && unloadedRoom.Players.Any(player => player.MemberId == creatorId && player.Loaded)
                    && unloadedRoom.Players.Any(player => player.MemberId == unloadedGrant.memberId && !player.Loaded),
                    "服务器与正式World完成准备，只有Core成员缺少Loaded", lifetime.Token);
                int preparingVersion = unloadedRoom.RoundVersion;
                int preparingCupIndex = unloadedRoom.CupIndex;
                string preparingTrack = unloadedRoom.TrackId;
                Assert.That(preparingCupIndex, Is.EqualTo(1));
                Assert.That(preparingTrack, Is.EqualTo("bosphorus"));
                Assert.That(unloadedRoom.CupRoundComplete, Is.False);
                CollectionAssert.AreEqual(expectedNextGrid, unloadedRoom.Grid);
                CollectionAssert.AreEqual(expectedPoints, unloadedRoom.CupPoints);
                CollectionAssert.AreEqual(expectedPlaces, unloadedRoom.CupPlaces);
                CollectionAssert.AreEqual(expectedTimes, unloadedRoom.CupTimes);
                await WaitExternalAsync(() => unloadedRoom.Phase == "Lobby" && !world.Busy && world.Network.Room.Phase == "Lobby",
                    "等待真实60秒准备超时返回大厅", lifetime.Token, seconds: 75);
                Assert.That(Time.realtimeSinceStartupAsDouble - preparationStarted, Is.GreaterThanOrEqualTo(60));
                Assert.That(unloadedRoom.Message, Does.Contain("准备超时"));
                Assert.That(unloadedRoom.RoundVersion, Is.EqualTo(preparingVersion));
                Assert.That(unloadedRoom.CupIndex, Is.EqualTo(preparingCupIndex));
                Assert.That(unloadedRoom.TrackId, Is.EqualTo(preparingTrack));
                Assert.That(unloadedRoom.CupRoundComplete, Is.False);
                CollectionAssert.AreEqual(expectedNextGrid, unloadedRoom.Grid);
                CollectionAssert.AreEqual(expectedPoints, unloadedRoom.CupPoints);
                CollectionAssert.AreEqual(expectedPlaces, unloadedRoom.CupPlaces);
                CollectionAssert.AreEqual(expectedTimes, unloadedRoom.CupTimes);
                Assert.That(unloadedRoom.Players.All(player => !player.Ready), Is.True);
                unloadedPeer.SnapshotReceived -= unloadedSnapshots;
                await unloadedPeer.LeaveAsync();
                unloadedPeer.Dispose();
                unloadedPeer = null;
                Object.Destroy(unloadedSettings);
                unloadedSettings = null;
                resumed = await directory.ResumeAsync(JsonUtility.ToJson(new ExternalResumeRequest { resumeToken = resumeToken }), lifetime.Token);
                await peer.ConnectAsync(resumed, lifetime.Token);
                unloadedGrant = null;
                await WaitExternalAsync(() => peer.Room?.Phase == "Lobby" && peer.Room.Players.Count(player => player.Connected) == 2,
                    "相同稳定身份换回真实Session客户端，准备重试", lifetime.Token, peer);
                await peer.SendAsync("ready", true, lifetime.Token);
                world.Network.SetReady(true);
                await WaitExternalAsync(() => world.Network.CanStart, "资源恢复后双方重新准备", lifetime.Token, peer);
                world.Network.StartRace();
                await WaitExternalAsync(() => peer.BaselineReceived && peer.Room.Phase == "Racing" && peer.Room.RoundVersion == preparingVersion + 1
                    && peer.Session.Race.State == KagitKartRaceState.Running && world.Session.Race.State == KagitKartRaceState.Running,
                    "准备超时后使用新version、真实Loaded重试同一GP章节", lifetime.Token, peer);
                Assert.That(peer.Room.CupIndex, Is.EqualTo(preparingCupIndex));
                Assert.That(peer.Room.TrackId, Is.EqualTo(preparingTrack));
                Assert.That(peer.Room.CupRoundComplete, Is.False);
                CollectionAssert.AreEqual(expectedNextGrid, peer.Room.Grid);
                CollectionAssert.AreEqual(expectedPoints, peer.Room.CupPoints);
                CollectionAssert.AreEqual(expectedPlaces, peer.Room.CupPlaces);
                CollectionAssert.AreEqual(expectedTimes, peer.Room.CupTimes);
            }
            finally
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try
                {
                    if (unloadedPeer != null)
                    {
                        unloadedPeer.SnapshotReceived -= unloadedSnapshots;
                        await unloadedPeer.LeaveAsync();
                        unloadedPeer.Dispose();
                    }
                    if (unloadedSettings != null)
                    {
                        Object.Destroy(unloadedSettings);
                    }
                    if (unloadedGrant != null)
                    {
                        await new RoomDirectoryClient(url).LeaveAsync(JsonUtility.ToJson(new ExternalResumeRequest { resumeToken = unloadedGrant.resumeToken }), cleanup.Token);
                    }
                    if (peer != null) await peer.DisconnectAsync(true, cleanup.Token);
                    if (world?.Network != null) await world.Network.LeaveAsync();
                    if (gameServer != null)
                    {
                        await WaitExternalAsync(() => gameServer.HasExited, "所有真实成员离开后目录应回收本房间游戏进程", cleanup.Token, seconds: 20);
                    }
                }
                finally
                {
                    // 只回收本测试创建并核对过路径的游戏进程；不停止共享.NET RoomService。
                    if (gameServer != null && !gameServer.HasExited) gameServer.Kill();
                    gameServer?.Dispose();
                    peer?.Dispose();
                }
            }
        }

        private static async UniTask AssertExternalTicketReplayRejectedAsync(RoomJoinGrant originalGrant, CancellationToken token)
        {
            var settings = ScriptableObject.CreateInstance<NetworkSessionSettings>();
            var rejected = NetworkSessionServices.CreateInternet(settings);
            try
            {
                Exception rejection = null;
                try
                {
                    await rejected.JoinAsync(new NetworkJoinOptions
                    {
                        Address = originalGrant.address, Port = checked((ushort)originalGrant.port), Ticket = originalGrant.ticket,
                        ProtocolVersion = checked((ushort)originalGrant.protocolVersion), ContentVersion = originalGrant.contentVersion
                    }, token);
                }
                catch (InvalidOperationException exception)
                {
                    rejection = exception;
                }
                Assert.That(rejection, Is.Not.Null, "已经消费的签名票据不能供额外UDP连接复用。");
                Assert.That(rejection.Message, Does.Contain("入房凭据无效"), "必须由认证拒绝，不能用房满或普通准入失败代替票据防重放证据。");
                Assert.That(rejected.State, Is.EqualTo(NetworkSessionState.Idle));
                Assert.That(rejected.Members, Is.Empty);
            }
            finally
            {
                await rejected.LeaveAsync();
                rejected.Dispose();
                Object.Destroy(settings);
            }
        }

        private static INetworkSessionService ReadExternalTransport(KagitKartNetworkRoom room)
        {
            return (INetworkSessionService)typeof(KagitKartNetworkRoom)
                .GetField("service", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(room);
        }

        private static object ReadExternalManager(INetworkSessionService transport)
        {
            return transport?.GetType().GetField("manager", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(transport);
        }

        private static MonoBehaviour FindExternalOwnedCarrier(INetworkSessionService transport)
        {
            object manager = ReadExternalManager(transport);
            if (manager == null)
            {
                return null;
            }
            return Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).SingleOrDefault(component =>
                component.GetType().FullName == "Core.Runtime.Networking.FishNetPredictionBridge" &&
                ReferenceEquals(component.GetType().GetProperty("NetworkManager").GetValue(component), manager) &&
                (bool)component.GetType().GetProperty("IsOwner").GetValue(component));
        }

        private static void StopExternalSdkClient(INetworkSessionService transport)
        {
            object manager = ReadExternalManager(transport);
            Assert.That(manager, Is.Not.Null);
            object client = manager.GetType().GetProperty("ClientManager").GetValue(manager);
            bool stopped = (bool)client.GetType().GetMethod("StopConnection", Type.EmptyTypes).Invoke(client, null);
            Assert.That(stopped, Is.True, "只触发SDK真实断开，由正式World处理后续清理和自动重连。");
        }

        private static async UniTask AssertExternalResumeRevokedAsync(string url, string resumeToken, CancellationToken token)
        {
            using var request = new UnityWebRequest(url.TrimEnd('/') + "/v1/sessions/resume", "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ExternalResumeRequest { resumeToken = resumeToken })));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: token);
            }
            catch (UnityWebRequestException) when (request.responseCode == 401)
            {
                // 业务拒绝是预期结果；其它网络或服务故障仍让测试失败。
            }
            Assert.That(request.responseCode, Is.EqualTo(401), "主动离房必须立即撤销原Resume秘密，不能重新认领车手。");
            var error = JsonUtility.FromJson<ExternalDirectoryError>(request.downloadHandler.text);
            Assert.That(error.code, Is.EqualTo("session_expired"));
        }

        private static async UniTask WaitExternalAsync(Func<bool> condition, string message, CancellationToken token,
            ExternalKartPeer peer = null, int seconds = 60)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (!condition() && Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (peer?.Failure != null) throw new InvalidOperationException(message, peer.Failure);
                await UniTask.Yield(token);
            }
            Assert.That(condition(), Is.True, message);
        }

        [Serializable]
        private sealed class ExternalCreateRequest
        {
            public string requestId = Guid.NewGuid().ToString("N");
            public string displayName = "双房真实车手";
            public int protocolVersion = KagitKartOnlineSettings.ProtocolVersion;
            public string contentVersion = KagitKartOnlineSettings.ContentVersion;
            public KagitKartRoomState configuration;
        }

        [Serializable]
        private sealed class ExternalJoinRequest
        {
            public string displayName = "UDP真实车手";
            public int protocolVersion = KagitKartOnlineSettings.ProtocolVersion;
            public string contentVersion = KagitKartOnlineSettings.ContentVersion;
        }

        [Serializable]
        private sealed class ExternalResumeRequest
        {
            public string resumeToken;
            public int protocolVersion = KagitKartOnlineSettings.ProtocolVersion;
            public string contentVersion = KagitKartOnlineSettings.ContentVersion;
        }

        [Serializable]
        private sealed class ExternalDirectoryError
        {
            public string code;
        }

        [Serializable]
        private sealed class ExternalCommand
        {
            public string Kind;
            public string CharacterId;
            public int RoundVersion;
            public bool Ready;
        }

        // 同一Editor进程内的第二个真实UDP客户端，无额外Camera/UI，不代表独立第二个Player进程验收。
        private sealed class ExternalKartPeer : INetworkSimulation, IDisposable
        {
            private const ushort RoomChannel = 710, RaceChannel = 711;
            private const string DataRoot = "Assets/LoadResources/Demos/kagit_kart/Data/";
            private NetworkSessionSettings settings;
            private ulong commandSequence;
            private int preparedRound = -1;
            private NetworkSnapshotEvent? deferredBaseline;
            private CancellationToken token;
            public INetworkSessionService Transport { get; private set; }
            public RoomJoinGrant Grant { get; private set; }
            public KagitKartRoomState Room { get; private set; }
            public KagitKartSession Session { get; private set; }
            public KagitKartGrandPrix GrandPrix { get; private set; }
            public KagitKartSourceCatalog Source { get; } = KagitKartSourceCatalog.Parse(File.ReadAllText(DataRoot + "SourceCatalog.json"));
            public bool BaselineReceived { get; private set; }
            public Exception Failure { get; private set; }
            public int Reconciles { get; private set; }
            public int Replays { get; private set; }

            public async UniTask ConnectAsync(RoomJoinGrant grant, CancellationToken connectionToken)
            {
                Grant = grant;
                token = connectionToken;
                settings = ScriptableObject.CreateInstance<NetworkSessionSettings>();
                Transport = NetworkSessionServices.CreateInternet(settings);
                Transport.SnapshotReceived += Snapshot;
                Transport.RegisterSimulation(this);
                await Transport.JoinAsync(new NetworkJoinOptions
                {
                    Address = grant.address, Port = checked((ushort)grant.port), Ticket = grant.ticket,
                    ProtocolVersion = checked((ushort)grant.protocolVersion), ContentVersion = grant.contentVersion
                }, token);
            }

            /// <summary>按本连接序号与当前房间版本发送真实业务命令。</summary>
            /// <param name="kind">服务器已支持的准备、开赛、角色选择或离房命令。</param>
            /// <param name="ready">仅准备命令使用的目标状态。</param>
            /// <param name="cancellationToken">发送等待令牌。</param>
            /// <param name="characterId">角色选择目标；其它命令保持空值。</param>
            public UniTask SendAsync(string kind, bool ready, CancellationToken cancellationToken, string characterId = null)
            {
                byte[] payload = Encoding.UTF8.GetBytes(JsonUtility.ToJson(new ExternalCommand
                {
                    Kind = kind, Ready = ready, RoundVersion = Room.RoundVersion, CharacterId = characterId
                }));
                return Transport.SendCommandAsync(new NetworkCommand(RoomChannel, ++commandSequence, payload), cancellationToken);
            }

            private void Snapshot(NetworkSnapshotEvent value)
            {
                try
                {
                    if (value.Snapshot.Channel == RoomChannel)
                    {
                        Room = JsonUtility.FromJson<KagitKartRoomState>(Encoding.UTF8.GetString(value.Snapshot.Payload));
                        if (Room.RoundVersion <= 0 || preparedRound == Room.RoundVersion) return;
                        var player = Room.Players.Single(entry => entry.MemberId == Grant.memberId);
                        string name = KagitKartCatalog.TrackAssetName(Room.TrackId) + ".json";
                        var data = KagitKartTrackData.Parse(File.ReadAllText(DataRoot + "Tracks/" + name));
                        var plan = KagitKartAiPlan.Parse(File.ReadAllText(DataRoot + "AiPlans/" + name), data);
                        Session?.Dispose();
                        GrandPrix = Room.Mode == KagitKartRaceMode.GrandPrix ? new KagitKartGrandPrix(Source.roster) : null;
                        if (GrandPrix != null && Room.CupPoints != null)
                        {
                            GrandPrix.RestoreNetworkState(Room.CupIndex, false, Room.CupPoints, Room.CupPlaces, Room.CupTimes);
                        }
                        Session = new KagitKartSession(Source, data, plan, Room.Mode, player.CharacterId, Room.SpeedClass,
                            Room.Seed, GrandPrix, grid: Room.Grid, humanIds: Room.Players.Select(entry => entry.CharacterId).ToArray());
                        if (GrandPrix != null && Room.CupRoundComplete)
                        {
                            GrandPrix.RestoreNetworkState(Room.CupIndex, true, Room.CupPoints, Room.CupPlaces, Room.CupTimes);
                        }
                        preparedRound = Room.RoundVersion;
                        if (Room.Phase == "Preparing") SendAsync("loaded", false, token).Forget(error => Failure = error);
                        if (deferredBaseline.HasValue)
                        {
                            var baseline = deferredBaseline.Value;
                            deferredBaseline = null;
                            Snapshot(baseline);
                        }
                    }
                    else if (value.Snapshot.Channel == RaceChannel)
                    {
                        if (Session == null)
                        {
                            deferredBaseline = value;
                            return;
                        }
                        using var stream = new MemoryStream(value.Snapshot.Payload, false);
                        using var reader = new BinaryReader(stream);
                        if (reader.ReadInt32() != Room.RoundVersion) return;
                        Session.ReadNetworkState(reader, restoreLocal: !BaselineReceived, notify: false);
                        Session.Player.Autopilot = false;
                        if (!BaselineReceived)
                        {
                            BaselineReceived = true;
                            SendAsync("loaded", false, token).Forget(error => Failure = error);
                        }
                    }
                }
                catch (Exception error)
                {
                    Failure = error;
                }
            }

            public byte[] ReadInput()
            {
                if (!BaselineReceived || Room?.Phase != "Racing" || Session == null) return null;
                // 驾驶夹具只计算本地控制量，再走真实UDP、服务器规则及官方预测；不修改进度或完赛状态。
                Session.Player.Autopilot = true;
                try
                {
                    Session.Ai.Advance(1d / 120, Session.RaceTime);
                }
                finally
                {
                    Session.Player.Autopilot = false;
                }
                using var stream = new MemoryStream(17);
                using var writer = new BinaryWriter(stream);
                writer.Write(Room.RoundVersion);
                KagitKartNetworkCodec.WriteControls(writer, Session.Player.Controls);
                return stream.ToArray();
            }

            public void Replicate(string memberId, uint tick, float dt, byte[] input, bool replay, bool isAuthority)
            {
                if (isAuthority) throw new InvalidOperationException("测试Peer不能成为比赛权威。");
                if (Session == null || input == null || memberId != Grant.memberId) return;
                using var stream = new MemoryStream(input, false);
                using var reader = new BinaryReader(stream);
                if (reader.ReadInt32() != Room.RoundVersion) return;
                Session.Predict(dt, KagitKartNetworkCodec.ReadControls(reader), replay);
                if (replay) Replays++;
            }

            public void AuthorityTick(uint tick, float dt) => throw new InvalidOperationException("客户端不能推进权威世界。");

            public byte[] CaptureState(string memberId)
            {
                if (Session == null || memberId != Grant.memberId) return null;
                using var stream = new MemoryStream();
                using var writer = new BinaryWriter(stream);
                writer.Write(Room.RoundVersion);
                Session.Player.WriteNetworkState(writer);
                return stream.ToArray();
            }

            public void Reconcile(string memberId, uint tick, byte[] state)
            {
                if (Session == null || memberId != Grant.memberId) return;
                using var stream = new MemoryStream(state, false);
                using var reader = new BinaryReader(stream);
                if (reader.ReadInt32() != Room.RoundVersion) return;
                Session.Player.ReadNetworkState(reader, Session.Karts, false);
                Reconciles++;
            }

            public async UniTask DisconnectAsync(bool voluntarilyLeave, CancellationToken cancellationToken)
            {
                if (Transport == null) return;
                if (voluntarilyLeave && Room != null && Transport.State == NetworkSessionState.Joined)
                {
                    await SendAsync("leave", false, cancellationToken);
                    await UniTask.Delay(150, ignoreTimeScale: true, cancellationToken: cancellationToken);
                }
                Transport.RegisterSimulation(null);
                Transport.SnapshotReceived -= Snapshot;
                await Transport.LeaveAsync();
                Transport.Dispose();
                Transport = null;
                Object.Destroy(settings);
                settings = null;
                Session?.Dispose();
                Session = null;
                GrandPrix = null;
                Room = null;
                preparedRound = -1;
                deferredBaseline = null;
                BaselineReceived = false;
                commandSequence = 0;
            }

            public void Dispose()
            {
                Session?.Dispose();
            }
        }
    }
}
#endif
