using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Core.Runtime.Networking;
using Cysharp.Threading.Tasks;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class CasinoNetworkCoordinatorTests
    {
        [Test]
        public void OfflineCallbacksPublishCommittedRunAndUseTrustedIdentity()
        {
            using var service = JoinedService();
            using var coordinator = new CasinoNetworkCoordinator(service);
            coordinator.InitializeRunAsync(1).GetAwaiter().GetResult();
            int changes = 0;
            int callbackThread = 0;
            coordinator.Changed += () => { changes++; callbackThread = Thread.CurrentThread.ManagedThreadId; };
            var request = Request(coordinator.Session.RunId, "forged-player", "a", 10);
            service.SendCommandAsync(Command(request, 1)).GetAwaiter().GetResult();
            Assert.That(coordinator.LastReceipt.Request.PlayerId, Is.EqualTo(service.LocalMemberId));
            Assert.That(coordinator.LastReceipt.Accepted, Is.True);
            Assert.That(coordinator.Session.Balance, Is.EqualTo(1010));
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(2));
            Assert.That(callbackThread, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
            Assert.That(changes, Is.EqualTo(1));
            Assert.That(service.TransportKind, Is.EqualTo(NetworkTransportKind.Offline));
        }

        [Test]
        public void RejectedBetPublishesLedgerWithoutIncreasingRuleRevision()
        {
            using var service = JoinedService();
            using var coordinator = new CasinoNetworkCoordinator(service);
            coordinator.InitializeRunAsync(1).GetAwaiter().GetResult();
            coordinator.BetAsync(CasinoGameKind.CoinFlip, 0, 0).GetAwaiter().GetResult();
            Assert.That(coordinator.LastReceipt.Error, Is.EqualTo(CasinoBetError.InvalidStake));
            Assert.That(coordinator.Session.Revision, Is.Zero);
            Assert.That(coordinator.Session.Balance, Is.EqualTo(1000));
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void RecreatedCoordinatorRestoresRunAndContinuesCommandSequence()
        {
            using var service = JoinedService();
            var first = new CasinoNetworkCoordinator(service);
            first.InitializeRunAsync(1).GetAwaiter().GetResult();
            first.BetAsync(CasinoGameKind.CoinFlip, 10, 0).GetAwaiter().GetResult();
            string committed = first.Session.ToSnapshotJson();
            first.Dispose();
            using var restored = new CasinoNetworkCoordinator(service);
            restored.InitializeRunAsync(999).GetAwaiter().GetResult();
            Assert.That(restored.Session.ToSnapshotJson(), Is.EqualTo(committed));
            Assert.That(restored.LastReceipt, Is.Not.Null);
            restored.BetAsync(CasinoGameKind.CoinFlip, 10, 0).GetAwaiter().GetResult();
            Assert.That(restored.Session.Revision, Is.EqualTo(2));
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(3));
        }

        [Test]
        public void FailedPublishKeepsWalletAndLedgerExactlyCommitted()
        {
            using var service = JoinedService();
            using var coordinator = new CasinoNetworkCoordinator(service);
            coordinator.InitializeRunAsync(1).GetAwaiter().GetResult();
            string before = coordinator.Session.ToSnapshotJson();
            service.FailNextPublish = true;
            coordinator.BetAsync(CasinoGameKind.CoinFlip, 10, 0).GetAwaiter().GetResult();
            Assert.That(coordinator.Session.ToSnapshotJson(), Is.EqualTo(before));
            Assert.That(coordinator.LastReceipt, Is.Null);
            Assert.That(coordinator.LastError, Does.Contain("测试发布失败"));
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(1));
            coordinator.BetAsync(CasinoGameKind.CoinFlip, 10, 0).GetAwaiter().GetResult();
            Assert.That(coordinator.Session.Balance, Is.EqualTo(1010));
            Assert.That(coordinator.LastError, Is.Null);
        }

        [Test]
        public void DelayedPublishDoesNotExposeCandidateAndSerializesLaterCommands()
        {
            using var service = JoinedService();
            using var coordinator = new CasinoNetworkCoordinator(service);
            coordinator.InitializeRunAsync(1).GetAwaiter().GetResult();
            var gate = new UniTaskCompletionSource();
            service.NextPublishGate = gate;
            coordinator.BetAsync(CasinoGameKind.CoinFlip, 10, 0).GetAwaiter().GetResult();
            coordinator.BetAsync(CasinoGameKind.CoinFlip, 10, 0).GetAwaiter().GetResult();
            Assert.That(coordinator.Session.Balance, Is.EqualTo(1000));
            Assert.That(coordinator.Session.Revision, Is.Zero);
            Assert.That(coordinator.LastReceipt, Is.Null);
            gate.TrySetResult();
            Assert.That(coordinator.Session.Revision, Is.EqualTo(2));
            Assert.That(coordinator.Session.Balance, Is.EqualTo(1020));
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(3));
        }

        [Test]
        public void AuthorityCallbackRestoresSnapshotBeforeNewEpochCommand()
        {
            using var service = JoinedService();
            using var coordinator = new CasinoNetworkCoordinator(service);
            coordinator.InitializeRunAsync(1).GetAwaiter().GetResult();
            // 故意修改公开规则实例，模拟宿主误用；交接回调必须从保留快照恢复。
            coordinator.Session.Apply(Request(coordinator.Session.RunId, service.LocalMemberId, "uncommitted", 10));
            Assert.That(coordinator.Session.Balance, Is.EqualTo(1010));
            service.RaiseAuthorityChanged(2);
            Assert.That(coordinator.Session.Balance, Is.EqualTo(1000));
            var command = Command(Request(coordinator.Session.RunId, "forged", "a", 10), 1);
            service.RaiseCommand(new NetworkCommandEvent(service.LocalMemberId, 1, command));
            Assert.That(coordinator.Session.Revision, Is.Zero);
            service.RaiseCommand(new NetworkCommandEvent(service.LocalMemberId, 2, command));
            Assert.That(coordinator.Session.Revision, Is.EqualTo(1));
            Assert.That(coordinator.LastReceipt.Request.PlayerId, Is.EqualTo(service.LocalMemberId));
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void AuthorityChangeCancelsPendingPublishBeforeItCanCommitUnderNewEpoch()
        {
            using var service = JoinedService();
            using var coordinator = new CasinoNetworkCoordinator(service);
            coordinator.InitializeRunAsync(1).GetAwaiter().GetResult();
            string before = coordinator.Session.ToSnapshotJson();
            var gate = new UniTaskCompletionSource();
            service.NextPublishGate = gate;
            coordinator.BetAsync(CasinoGameKind.CoinFlip, 10, 0).GetAwaiter().GetResult();
            service.RaiseAuthorityChanged(2);
            gate.TrySetResult();
            Assert.That(coordinator.Session.ToSnapshotJson(), Is.EqualTo(before));
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(1));
            coordinator.BetAsync(CasinoGameKind.CoinFlip, 10, 0).GetAwaiter().GetResult();
            Assert.That(coordinator.Session.Balance, Is.EqualTo(1010));
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(2));
        }

        [Test]
        public void CorruptedRetainedSnapshotCannotBeOverwrittenByInitialization()
        {
            using var service = JoinedService();
            service.PublishSnapshotAsync(new NetworkSnapshot(1, 7, Encoding.UTF8.GetBytes("{}"))).GetAwaiter().GetResult();
            using var coordinator = new CasinoNetworkCoordinator(service);
            Assert.That(coordinator.Session, Is.Null);
            Assert.Throws<InvalidOperationException>(() => coordinator.InitializeRunAsync(1).GetAwaiter().GetResult());
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(7));
        }

        [Test]
        public void LeaveClearsRuleStateAndDisposeDoesNotOwnNetworkService()
        {
            using var service = JoinedService();
            var coordinator = new CasinoNetworkCoordinator(service);
            coordinator.InitializeRunAsync(1).GetAwaiter().GetResult();
            service.LeaveAsync().GetAwaiter().GetResult();
            Assert.That(coordinator.Session, Is.Null);
            Assert.That(coordinator.LastReceipt, Is.Null);
            coordinator.Dispose();
            coordinator.Dispose();
            Assert.That(service.State, Is.EqualTo(NetworkSessionState.Idle));
            Assert.Throws<ObjectDisposedException>(() => coordinator.BetAsync(CasinoGameKind.CoinFlip, 1, 0).GetAwaiter().GetResult());
        }

        private static ControlledOfflineService JoinedService()
        {
            var service = new ControlledOfflineService();
            service.CreateAsync(new NetworkCreateOptions
            {
                Region = "asia", ProtocolVersion = 1, ContentVersion = "jinx-p0-test", MaxPlayers = 1, LocalDisplayName = "离线验证"
            }).GetAwaiter().GetResult();
            return service;
        }

        private static CasinoBetRequest Request(string runId, string playerId, string requestId, long stake)
        {
            return new CasinoBetRequest { RunId = runId, PlayerId = playerId, RequestId = requestId, Game = CasinoGameKind.CoinFlip, Stake = stake, Choice = 0 };
        }

        private static NetworkCommand Command(CasinoBetRequest request, ulong sequence)
        {
            return new NetworkCommand(1, sequence, Encoding.UTF8.GetBytes(JsonUtility.ToJson(request)));
        }

        // 测试专用离线装饰器：增加可控发布失败/延迟和回调代次，不声称存在互联网成员。
        private sealed class ControlledOfflineService : INetworkSessionService
        {
            private readonly OfflineLocalNetworkSessionService inner = new OfflineLocalNetworkSessionService();
            private ulong epoch;
            public bool FailNextPublish;
            public UniTaskCompletionSource NextPublishGate;

            public ControlledOfflineService()
            {
                inner.CommandReceived += command => CommandReceived?.Invoke(new NetworkCommandEvent(command.SenderMemberId, AuthorityEpoch, command.Command));
                inner.AuthorityChanged += authority => { epoch = authority.Epoch; AuthorityChanged?.Invoke(authority); };
                inner.SnapshotReceived += snapshot => SnapshotReceived?.Invoke(new NetworkSnapshotEvent(snapshot.AuthorityMemberId, AuthorityEpoch, snapshot.Snapshot));
            }

            public NetworkTransportKind TransportKind => NetworkTransportKind.Offline;
            public NetworkSessionState State => inner.State;
            public NetworkSessionInfo CurrentSession => inner.CurrentSession;
            public IReadOnlyList<NetworkMemberInfo> Members => inner.Members;
            public string LocalMemberId => inner.LocalMemberId;
            public string AuthorityMemberId => inner.AuthorityMemberId;
            public ulong AuthorityEpoch => epoch;
            public IReadOnlyDictionary<ushort, NetworkSnapshotEvent> Snapshots => inner.Snapshots;

            public event Action<NetworkSessionState> StateChanged { add => inner.StateChanged += value; remove => inner.StateChanged -= value; }
            public event Action<NetworkSessionInfo> SessionChanged { add => inner.SessionChanged += value; remove => inner.SessionChanged -= value; }
            public event Action<IReadOnlyList<NetworkMemberInfo>> MembersChanged { add => inner.MembersChanged += value; remove => inner.MembersChanged -= value; }
            public event Action<NetworkAuthorityInfo> AuthorityChanged;
            public event Action<NetworkCommandEvent> CommandReceived;
            public event Action<NetworkSnapshotEvent> SnapshotReceived;
            public event Action<NetworkAvatarPoseEvent> AvatarPoseReceived { add => inner.AvatarPoseReceived += value; remove => inner.AvatarPoseReceived -= value; }

            public UniTask<NetworkSessionInfo> CreateAsync(NetworkCreateOptions options, CancellationToken cancellationToken = default) => inner.CreateAsync(options, cancellationToken);
            public UniTask<NetworkSessionInfo> JoinAsync(NetworkJoinOptions options, CancellationToken cancellationToken = default) => inner.JoinAsync(options, cancellationToken);
            public UniTask LeaveAsync(CancellationToken cancellationToken = default) => inner.LeaveAsync(cancellationToken);
            public UniTask SendCommandAsync(NetworkCommand command, CancellationToken cancellationToken = default) => inner.SendCommandAsync(command, cancellationToken);
            public void PublishAvatarPose(NetworkAvatarPose pose) => inner.PublishAvatarPose(pose);

            public async UniTask PublishSnapshotAsync(NetworkSnapshot snapshot, CancellationToken cancellationToken = default)
            {
                if (FailNextPublish)
                {
                    FailNextPublish = false;
                    throw new InvalidOperationException("测试发布失败");
                }
                var gate = NextPublishGate;
                NextPublishGate = null;
                if (gate != null) await gate.Task.AttachExternalCancellation(cancellationToken);
                await inner.PublishSnapshotAsync(snapshot, cancellationToken);
            }

            public void RaiseAuthorityChanged(ulong newEpoch)
            {
                epoch = newEpoch;
                AuthorityChanged?.Invoke(new NetworkAuthorityInfo(AuthorityMemberId, epoch));
            }

            public void RaiseCommand(NetworkCommandEvent command) => CommandReceived?.Invoke(command);
            public void Dispose() => inner.Dispose();
        }
    }
}
