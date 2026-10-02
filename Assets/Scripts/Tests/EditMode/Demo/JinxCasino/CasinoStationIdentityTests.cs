using System;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class CasinoStationIdentityTests
    {
        [Test]
        public void ActiveTableSurvivesRestoreAndRejectsOtherTableWithoutChangingRound()
        {
            var session = CasinoAdventureSession.Start(6, CasinoAdventureMode.Practice, 1);
            Assert.That(session.BeginGame("start", CasinoGameKind.CooperativeLevers, 50, 0, "levers-a").Success, Is.True);
            session = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            var before = session.State;
            Assert.That(session.Act("wrong", CasinoMiniGameAction.PullLever, 0, "levers-b").Error, Is.EqualTo("WrongStation"));
            Assert.That(session.Act("legacy", CasinoMiniGameAction.PullLever).Error, Is.EqualTo("WrongStation"));
            Assert.That(session.State.ActiveRoundJson, Is.EqualTo(before.ActiveRoundJson));
            Assert.That(session.State.RandomState, Is.EqualTo(before.RandomState));
            Assert.That(session.State.Coins, Is.EqualTo(before.Coins));
            Assert.That(session.State.ActiveStationId, Is.EqualTo("levers-a"));
            Assert.That(session.BeginGame("start", CasinoGameKind.CooperativeLevers, 50, 0, "levers-a").Changed, Is.False);
            Assert.That(session.BeginGame("start", CasinoGameKind.CooperativeLevers, 50, 0, "levers-b").Error, Is.EqualTo("ConflictingRequest"));
        }

        [Test]
        public void CompletedResultRemainsAtOriginalTableAndRetryCannotReaward()
        {
            var session = CasinoAdventureSession.Start(2, CasinoAdventureMode.Practice, 1);
            Assert.That(session.BeginGame("spin", CasinoGameKind.Slots, 50, 0, "slots-a").Success, Is.True);
            Assert.That(session.HasActiveRound, Is.False);
            Assert.That(session.State.LastStationId, Is.EqualTo("slots-a"));
            var before = session.State;
            session = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(session.BeginGame("spin", CasinoGameKind.Slots, 50, 0, "slots-a").Changed, Is.False);
            Assert.That(session.State.Coins, Is.EqualTo(before.Coins));
            Assert.That(session.State.SettledRoundSequence, Is.EqualTo(before.SettledRoundSequence));
            Assert.That(session.State.RandomState, Is.EqualTo(before.RandomState));
        }

        [Test]
        public void RestoreRejectsDifferentGameInActiveRoundMetadata()
        {
            var session = CasinoAdventureSession.Start(3, CasinoAdventureMode.Practice, 1);
            session.BeginGame("start", CasinoGameKind.CooperativeLevers, 50, 0, "levers-a");
            var damaged = session.State;
            damaged.ActiveGame = CasinoGameKind.CooperativeVault;
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(damaged)));
        }

    }
}
