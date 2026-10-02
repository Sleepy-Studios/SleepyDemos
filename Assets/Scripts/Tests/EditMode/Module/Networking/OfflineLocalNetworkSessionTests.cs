using System;
using System.Collections.Generic;
using System.Threading;
using Core.Runtime.Networking;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Module
{
    public sealed class OfflineLocalNetworkSessionTests
    {
        private static NetworkCreateOptions CreateOptions(int maxPlayers = 1) => new NetworkCreateOptions
        {
            Region = "asia", ProtocolVersion = 1, ContentVersion = "test-content-1",
            MaxPlayers = maxPlayers, LocalDisplayName = "本地验证"
        };

        [Test]
        public void CreatedSessionIsExplicitlyOfflineWithOneTrustedLocalAuthority()
        {
            using var service = new OfflineLocalNetworkSessionService();
            var states = new List<NetworkSessionState>();
            service.StateChanged += states.Add;
            var room = service.CreateAsync(CreateOptions()).GetAwaiter().GetResult();
            Assert.That(room.TransportKind, Is.EqualTo(NetworkTransportKind.Offline));
            Assert.That(service.TransportKind, Is.EqualTo(NetworkTransportKind.Offline));
            Assert.That(room.MaxPlayers, Is.EqualTo(1));
            Assert.That(room.ContentVersion, Is.EqualTo("test-content-1"));
            Assert.That(service.Members.Count, Is.EqualTo(1));
            Assert.That(service.Members[0].IsLocal, Is.True);
            Assert.That(service.Members[0].MemberId, Is.EqualTo(service.LocalMemberId));
            Assert.That(service.AuthorityMemberId, Is.EqualTo(service.LocalMemberId));
            Assert.That(service.AuthorityEpoch, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { NetworkSessionState.Connecting, NetworkSessionState.Joined }, states);
        }

        [Test]
        public void OfflineServiceCannotImpersonateMultiplayerOrJoinInternetRoom()
        {
            using var service = new OfflineLocalNetworkSessionService();
            Assert.Throws<ArgumentOutOfRangeException>(() => service.CreateAsync(CreateOptions(6)).GetAwaiter().GetResult());
            Assert.That(service.State, Is.EqualTo(NetworkSessionState.Idle));
            Assert.Throws<NotSupportedException>(() => service.JoinAsync(new NetworkJoinOptions
            { Code = "V1-asia-ABCDEFGH", ContentVersion = "test-content-1", DisplayName = "测试" }).GetAwaiter().GetResult());
            Assert.That(service.CurrentSession, Is.Null);
            Assert.That(service.Members, Is.Empty);
        }

        [Test]
        public void CommandReceiverGetsTransportIdentityAndRejectsDuplicateOrOldSequence()
        {
            using var service = new OfflineLocalNetworkSessionService();
            service.CreateAsync(CreateOptions()).GetAwaiter().GetResult();
            var received = new List<NetworkCommandEvent>();
            service.CommandReceived += received.Add;
            service.SendCommandAsync(new NetworkCommand(4, 2, new byte[] { 7 })).GetAwaiter().GetResult();
            service.SendCommandAsync(new NetworkCommand(4, 2, new byte[] { 9 })).GetAwaiter().GetResult();
            service.SendCommandAsync(new NetworkCommand(4, 1, new byte[] { 9 })).GetAwaiter().GetResult();
            service.SendCommandAsync(new NetworkCommand(4, 3, new byte[] { 8 })).GetAwaiter().GetResult();
            service.SendCommandAsync(new NetworkCommand(5, 1, new byte[] { 8 })).GetAwaiter().GetResult();
            Assert.That(received.Count, Is.EqualTo(3));
            Assert.That(received[0].SenderMemberId, Is.EqualTo(service.LocalMemberId));
            Assert.That(received[0].AuthorityEpoch, Is.EqualTo(service.AuthorityEpoch));
            Assert.That(received[1].Command.Sequence, Is.EqualTo(3));
            Assert.That(received[2].Command.Channel, Is.EqualTo(5));
        }

        [Test]
        public void SnapshotIsRetainedBeforeEventAndCannotBeReplacedWithOlderState()
        {
            using var service = new OfflineLocalNetworkSessionService();
            service.CreateAsync(CreateOptions()).GetAwaiter().GetResult();
            int events = 0;
            bool retainedBeforeEvent = false;
            service.SnapshotReceived += message =>
            {
                retainedBeforeEvent = ReferenceEquals(service.Snapshots[message.Snapshot.Channel].Snapshot, message.Snapshot);
                events++;
            };
            service.PublishSnapshotAsync(new NetworkSnapshot(1, 8, new byte[] { 10, 20 })).GetAwaiter().GetResult();
            Assert.Throws<InvalidOperationException>(() => service.PublishSnapshotAsync(new NetworkSnapshot(1, 8, new byte[] { 99 })).GetAwaiter().GetResult());
            Assert.Throws<InvalidOperationException>(() => service.PublishSnapshotAsync(new NetworkSnapshot(1, 7, new byte[] { 99 })).GetAwaiter().GetResult());
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(8));
            Assert.That(service.Snapshots[1].AuthorityMemberId, Is.EqualTo(service.LocalMemberId));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(retainedBeforeEvent, Is.True);
        }

        [Test]
        public void MessagePayloadOwnershipCannotBeChangedByCallerOrSubscriber()
        {
            var source = new byte[] { 1, 2 };
            var command = new NetworkCommand(1, 1, source);
            var snapshot = new NetworkSnapshot(1, 1, source);
            source[0] = 99;
            command.Payload[0] = 98;
            snapshot.Payload[0] = 97;
            CollectionAssert.AreEqual(new byte[] { 1, 2 }, command.Payload);
            CollectionAssert.AreEqual(new byte[] { 1, 2 }, snapshot.Payload);
        }

        [Test]
        public void LeaveClearsStateAndNextSessionAcceptsSequenceOneAgain()
        {
            using var service = new OfflineLocalNetworkSessionService();
            service.CreateAsync(CreateOptions()).GetAwaiter().GetResult();
            string previousId = service.LocalMemberId;
            service.SendCommandAsync(new NetworkCommand(1, 2, Array.Empty<byte>())).GetAwaiter().GetResult();
            service.PublishSnapshotAsync(new NetworkSnapshot(1, 3, Array.Empty<byte>())).GetAwaiter().GetResult();
            service.LeaveAsync().GetAwaiter().GetResult();
            service.LeaveAsync().GetAwaiter().GetResult();
            Assert.That(service.State, Is.EqualTo(NetworkSessionState.Idle));
            Assert.That(service.CurrentSession, Is.Null);
            Assert.That(service.LocalMemberId, Is.Null);
            Assert.That(service.AuthorityMemberId, Is.Null);
            Assert.That(service.AuthorityEpoch, Is.Zero);
            Assert.That(service.Members, Is.Empty);
            Assert.That(service.Snapshots, Is.Empty);
            service.CreateAsync(CreateOptions()).GetAwaiter().GetResult();
            Assert.That(service.LocalMemberId, Is.Not.EqualTo(previousId));
            int commands = 0;
            service.CommandReceived += _ => commands++;
            service.SendCommandAsync(new NetworkCommand(1, 1, Array.Empty<byte>())).GetAwaiter().GetResult();
            Assert.That(commands, Is.EqualTo(1));
            service.PublishSnapshotAsync(new NetworkSnapshot(1, 1, Array.Empty<byte>())).GetAwaiter().GetResult();
            Assert.That(service.Snapshots[1].Snapshot.Sequence, Is.EqualTo(1));
        }

        [Test]
        public void PreCanceledOperationsDoNotCreateOrMutateSession()
        {
            using var service = new OfflineLocalNetworkSessionService();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => service.CreateAsync(CreateOptions(), cancellation.Token).GetAwaiter().GetResult());
            Assert.That(service.State, Is.EqualTo(NetworkSessionState.Idle));
            service.CreateAsync(CreateOptions()).GetAwaiter().GetResult();
            Assert.Throws<OperationCanceledException>(() => service.PublishSnapshotAsync(new NetworkSnapshot(1, 1, new byte[] { 1 }), cancellation.Token).GetAwaiter().GetResult());
            Assert.That(service.Snapshots, Is.Empty);
            Assert.Throws<OperationCanceledException>(() => service.LeaveAsync(cancellation.Token).GetAwaiter().GetResult());
            Assert.That(service.State, Is.EqualTo(NetworkSessionState.Joined));
        }

        [Test]
        public void AvatarEventUsesLocalIdentityWithoutCameraOwnership()
        {
            using var service = new OfflineLocalNetworkSessionService();
            service.CreateAsync(CreateOptions()).GetAwaiter().GetResult();
            NetworkAvatarPoseEvent received = default;
            service.AvatarPoseReceived += message => received = message;
            var pose = new NetworkAvatarPose(new Vector3(1, 2, 3), Quaternion.Euler(0, 45, 0), 20);
            service.PublishAvatarPose(pose);
            Assert.That(received.MemberId, Is.EqualTo(service.LocalMemberId));
            Assert.That(received.Pose.Position, Is.EqualTo(pose.Position));
            Assert.That(received.Pose.Rotation, Is.EqualTo(pose.Rotation));
            Assert.That(received.Pose.HeadPitch, Is.EqualTo(20));
        }

        [Test]
        public void DisposeIsIdempotentAndForbidsReuse()
        {
            var service = new OfflineLocalNetworkSessionService();
            service.CreateAsync(CreateOptions()).GetAwaiter().GetResult();
            service.Dispose();
            service.Dispose();
            Assert.That(service.State, Is.EqualTo(NetworkSessionState.Disposed));
            Assert.That(service.CurrentSession, Is.Null);
            Assert.That(service.Members, Is.Empty);
            Assert.Throws<ObjectDisposedException>(() => service.CreateAsync(CreateOptions()).GetAwaiter().GetResult());
            Assert.Throws<ObjectDisposedException>(() => service.PublishAvatarPose(default));
        }
    }
}
