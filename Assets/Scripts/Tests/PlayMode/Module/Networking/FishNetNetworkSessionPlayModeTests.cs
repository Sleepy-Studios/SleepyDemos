#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Networking;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Module
{
    /// 真实 UDP 与正式 AOT 载体的稳定契约；不使用假传输或 Demo 规则。
    public sealed class FishNetNetworkSessionPlayModeTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator HostClientHandshakeReliableMessagesPredictionAdmissionAndExit()
        {
            return RunContractAsync().ToCoroutine();
        }

        private static async UniTask RunContractAsync()
        {
            await ResourceServices.Default.InitializeAsync(ResourceInitializeOptions.Default);
            var settings = ScriptableObject.CreateInstance<NetworkSessionSettings>();
            INetworkSessionService host = null, client = null, rejected = null;
            try
            {
                host = NetworkSessionServices.CreateInternet(settings);
                client = NetworkSessionServices.CreateInternet(settings);
                rejected = NetworkSessionServices.CreateInternet(settings);
                var authority = new ContractSimulation(true);
                var prediction = new ContractSimulation(false);
                host.RegisterSimulation(authority);
                client.RegisterSimulation(prediction);
                host.SetAdmissionRule(() => host.Members.Count < 2);
                ushort port = AvailablePort();
                await host.CreateAsync(new NetworkCreateOptions
                {
                    Address = "127.0.0.1", Port = port, ProtocolVersion = settings.ProtocolVersion,
                    ContentVersion = settings.ContentVersion, MaxPlayers = 8, LocalDisplayName = "Host"
                });
                // 晚加入者使用自己的本地输入时钟，不能拿该时钟和服务器直接相减。
                await WaitAsync(() => authority.AuthorityTicks.Count >= 140, "Host 应持续以独立 tick 推进。");
                await client.JoinAsync(JoinOptions(settings, port));
                await WaitAsync(() => host.Members.Count == 2 && client.Members.Count == 2, "握手成功后两侧应收到相同成员。");
                string clientId = client.LocalMemberId;
                Assert.That(host.IsAuthority, Is.True);
                Assert.That(client.IsAuthority, Is.False);
                Assert.That(clientId, Is.Not.EqualTo(host.LocalMemberId));
                Assert.That(client.AuthorityMemberId, Is.EqualTo(host.LocalMemberId));
                CollectionAssert.AreEquivalent(host.Members.Select(member => member.MemberId), client.Members.Select(member => member.MemberId));

                var received = new List<NetworkCommandEvent>();
                host.CommandReceived += received.Add;
                byte[] command = Encoding.UTF8.GetBytes("sender=forged-member");
                await client.SendCommandAsync(new NetworkCommand(12, 1, command));
                await client.SendCommandAsync(new NetworkCommand(12, 1, command));
                await client.SendCommandAsync(new NetworkCommand(12, 2, command));
                await WaitAsync(() => received.Count == 2, "可靠命令应送达且重复序号不能重派发。");
                Assert.That(received.All(message => message.SenderMemberId == clientId), Is.True, "身份必须来自连接，不能采信 payload。");
                CollectionAssert.AreEqual(command, received[0].Command.Payload);
                CollectionAssert.AreEqual(new ulong[] { 1, 2 }, received.Select(message => message.Command.Sequence));

                byte[] large = Enumerable.Range(0, 4096).Select(index => (byte)(index % 251)).ToArray();
                await host.PublishSnapshotAsync(new NetworkSnapshot(23, 1, large, reliable: true));
                await WaitAsync(() => client.Snapshots.ContainsKey(23), "超过 MTU 的可靠完整快照应通过官方分片送达。");
                Assert.That(client.Snapshots[23].Snapshot.Reliable, Is.True);
                CollectionAssert.AreEqual(large, client.Snapshots[23].Snapshot.Payload);
                await host.PublishSnapshotAsync(new NetworkSnapshot(24, 1, large));
                await WaitAsync(() => client.Snapshots.ContainsKey(24), "同尺寸常规快照也应由 SDK 自动可靠分片并重组送达。");
                Assert.That(client.Snapshots[24].Snapshot.Reliable, Is.False, "请求标识保留，超过 MTU 的实际通道由 SDK 处理。");
                CollectionAssert.AreEqual(large, client.Snapshots[24].Snapshot.Payload);
                Assert.Throws<InvalidOperationException>(() => host.PublishSnapshotAsync(new NetworkSnapshot(23, 1, Array.Empty<byte>())).GetAwaiter().GetResult());
                Assert.Throws<ArgumentOutOfRangeException>(() => client.SendCommandAsync(new NetworkCommand(12, 3,
                    new byte[NetworkPayloadLimits.Command + 1])).GetAwaiter().GetResult());

                await WaitAsync(() => prediction.SawAuthorityState && prediction.Replays > 0 && authority.InputMembers.Contains(clientId),
                    "远端输入、服务器 CaptureState、客户端官方 Reconcile/Replicate 重放应完整运行。");
                Assert.That(authority.HasDuplicateAuthorityTick, Is.False, "Host 同一 tick 只能推进一次共享世界。");
                Assert.That(authority.Reconciles, Is.Zero, "Host 不能同时校正另一份客户端模拟。");
                Assert.That(prediction.AuthorityTicks, Is.Empty);
                CollectionAssert.AreEquivalent(new[] { clientId }, prediction.InputMembers, "客户端只预测自己的拥有者。");
                Assert.That(authority.HasInvalidDelta || prediction.HasInvalidDelta, Is.False);
                Assert.That(authority.HasWrongAuthority || prediction.HasWrongAuthority, Is.False);
                Assert.That(authority.LastInputTick[clientId], Is.EqualTo(authority.LastAuthorityTick), "服务器输入应用 tick 必须属于服务器时域。");
                int emptyBeforePause = authority.EmptyInputs;
                prediction.PauseInputs = true;
                await WaitAsync(() => authority.EmptyInputs > emptyBeforePause + 5, "停止发送新输入后，官方缺包路径仍须交付空输入。");
                prediction.PauseInputs = false;

                Exception rejection = null;
                try
                {
                    await rejected.JoinAsync(JoinOptions(settings, port));
                }
                catch (InvalidOperationException exception)
                {
                    rejection = exception;
                }
                Assert.That(rejection, Is.Not.Null, "准入拒绝必须失败，不能先进入 Joined。");
                Assert.That(rejected.State, Is.EqualTo(NetworkSessionState.Idle));
                Assert.That(rejected.CurrentSession, Is.Null);
                Assert.That(host.Members.Count, Is.EqualTo(2), "拒绝连接不能占用已确认成员名额。");

                await client.LeaveAsync();
                await WaitAsync(() => host.Members.Count == 1, "客户端退出后 Host 应移除真实连接。");
                Assert.That(client.Snapshots, Is.Empty);
                Assert.That(client.CurrentSession, Is.Null);
                host.RegisterSimulation(null);
                await host.LeaveAsync();
                Assert.That(host.State, Is.EqualTo(NetworkSessionState.Idle));
                Assert.That(host.Members, Is.Empty);
                Assert.That(host.Snapshots, Is.Empty);
                // 复用原端口实际重连，证明退出已释放 socket，而不只改了公开状态。
                await host.CreateAsync(new NetworkCreateOptions
                {
                    Address = "127.0.0.1", Port = port, ProtocolVersion = settings.ProtocolVersion,
                    ContentVersion = settings.ContentVersion, MaxPlayers = 8
                });
                Assert.That(host.State, Is.EqualTo(NetworkSessionState.Joined));
            }
            finally
            {
                foreach (var service in new[] { rejected, client, host })
                {
                    if (service == null)
                    {
                        continue;
                    }
                    service.RegisterSimulation(null);
                    await service.LeaveAsync();
                    service.Dispose();
                }
                UnityEngine.Object.Destroy(settings);
                await UniTask.Yield();
            }
        }

        private static NetworkJoinOptions JoinOptions(NetworkSessionSettings settings, ushort port) => new()
        {
            Address = "127.0.0.1", Port = port, ProtocolVersion = settings.ProtocolVersion,
            ContentVersion = settings.ContentVersion, DisplayName = "Client"
        };

        private static ushort AvailablePort()
        {
            using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            return checked((ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port);
        }

        private static async UniTask WaitAsync(Func<bool> condition, string failure)
        {
            using var timeout = new CancellationTokenSource();
            using var timeoutTimer = timeout.CancelAfterSlim(TimeSpan.FromSeconds(8));
            try
            {
                await UniTask.WaitUntil(condition, cancellationToken: timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail(failure);
            }
        }

        private sealed class ContractSimulation : INetworkSimulation
        {
            private readonly bool authority;
            public readonly HashSet<uint> AuthorityTicks = new();
            public readonly HashSet<string> InputMembers = new();
            public readonly Dictionary<string, uint> LastInputTick = new();
            public uint LastAuthorityTick;
            public int Reconciles, Replays, EmptyInputs;
            public bool SawAuthorityState, HasDuplicateAuthorityTick, HasInvalidDelta, HasWrongAuthority, PauseInputs;

            public ContractSimulation(bool authority) { this.authority = authority; }
            public byte[] ReadInput() => PauseInputs ? null : new byte[] { 1 };
            public void Replicate(string memberId, uint tick, float dt, byte[] input, bool replay, bool isAuthority)
            {
                HasInvalidDelta |= Math.Abs(dt - 1f / 120) > 1e-7f;
                HasWrongAuthority |= isAuthority != authority;
                InputMembers.Add(memberId);
                LastInputTick[memberId] = tick;
                if (input == null)
                {
                    EmptyInputs++;
                }
                if (replay)
                {
                    Replays++;
                }
            }
            public void AuthorityTick(uint tick, float dt)
            {
                HasInvalidDelta |= Math.Abs(dt - 1f / 120) > 1e-7f;
                HasDuplicateAuthorityTick |= !AuthorityTicks.Add(tick);
                LastAuthorityTick = tick;
            }
            public byte[] CaptureState(string memberId) => new byte[] { authority ? (byte)1 : (byte)0, 17, 29, 43 };
            public void Reconcile(string memberId, uint tick, byte[] state)
            {
                Reconciles++;
                SawAuthorityState |= state.Length == 4 && state[0] == 1 && state[1] == 17 && state[2] == 29 && state[3] == 43;
            }
        }
    }
}
#endif
