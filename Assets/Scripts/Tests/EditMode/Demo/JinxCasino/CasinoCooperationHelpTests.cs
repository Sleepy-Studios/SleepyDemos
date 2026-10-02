using System;
using System.Linq;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /// 协作助力是实际规则改变，最多一次，不通过提示文案替代成功条件。
    public sealed class CasinoCooperationHelpTests
    {
        [Test]
        public void LeverHelpExpandsWindowButStillRequiresRealPullAndNpcCompletion()
        {
            var baseline = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeLevers, 90, 100, 0);
            baseline.Advance(100); Assert.That(baseline.TryAct(CasinoMiniGameAction.PullLever, 0), Is.True);
            Assert.That(baseline.GetPresentation().SelectedValues[0], Is.Zero, "一百毫秒处原窗口尚未开放，应记错时而非成功");
            var helped = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeLevers, 90, 100, 0);
            Assert.That(helped.TryApplyCooperationHelp(), Is.True);
            Assert.That(helped.IsComplete, Is.False); Assert.That(helped.GetPresentation().SelectedValues, Is.EqualTo(new[] { 0, 0 }));
            helped.Advance(100); Assert.That(helped.TryAct(CasinoMiniGameAction.PullLever, 0), Is.True);
            Assert.That(helped.GetPresentation().SelectedValues[0], Is.EqualTo(1)); Assert.That(helped.IsComplete, Is.False);
            helped.Advance(400); Assert.That(helped.IsComplete, Is.True); Assert.That(helped.IsObjectiveSuccess, Is.True); Assert.That(helped.Payout, Is.EqualTo(400));
        }

        [Test]
        public void RestoredLeverHelpCannotStackOrConsumeRandomAndOutsideExpandedWindowStillFails()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeLevers, 91, 100, 0);
            uint random = ReadRandom(round); Assert.That(random, Is.GreaterThan(0)); Assert.That(round.TryApplyCooperationHelp(), Is.True); Assert.That(ReadRandom(round), Is.EqualTo(random));
            var restored = CasinoMiniGameRound.Restore(round.ToSnapshotJson());
            Assert.That(restored.CooperationHelpUsed, Is.True); string before = restored.ToSnapshotJson();
            Assert.That(restored.TryApplyCooperationHelp(), Is.False); Assert.That(restored.ToSnapshotJson(), Is.EqualTo(before));
            restored.Advance(600); Assert.That(restored.TryAct(CasinoMiniGameAction.PullLever, 0), Is.True);
            Assert.That(restored.GetPresentation().SelectedValues[0], Is.Zero, "扩窗结束于五百毫秒，不能恢复后再次扩至六百");
            Assert.That(restored.Description, Does.Contain("[100+i*250,500+i*250]"));
            Assert.That(CasinoMiniGameRound.DescribeRules(CasinoGameKind.CooperativeLevers, cooperationHelp: true), Does.Contain("[100+i*250,500+i*250]"));
        }

        [Test]
        public void VaultHelpChoosesUnrevealedDigitAndNeverUnlocksByItself()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeVault, 92, 100, 0, playerCount: 3);
            Assert.That(round.TryAct(CasinoMiniGameAction.InspectClue, 0), Is.True); uint random = ReadRandom(round); Assert.That(random, Is.GreaterThan(0));
            Assert.That(round.TryApplyCooperationHelp(), Is.True); Assert.That(ReadRandom(round), Is.EqualTo(random));
            Assert.That(round.GetPresentation().SelectedValues, Is.EqualTo(new[] { 1, 1, 0 }));
            Assert.That(round.IsComplete || round.IsObjectiveSuccess, Is.False);
            Assert.That(round.TryAct(CasinoMiniGameAction.EnterCode, 0), Is.True); Assert.That(round.IsObjectiveSuccess, Is.False);
            var restored = CasinoMiniGameRound.Restore(round.ToSnapshotJson());
            Assert.That(restored.TryApplyCooperationHelp(), Is.False);
            Assert.That(restored.TryAct(CasinoMiniGameAction.InspectClue, 2), Is.True); var digits = restored.GetPresentation().NumberValues;
            Assert.That(restored.TryAct(CasinoMiniGameAction.EnterCode, digits[0] * 100 + digits[1] * 10 + digits[2]), Is.True);
            Assert.That(restored.IsObjectiveSuccess, Is.True); Assert.That(restored.Payout, Is.EqualTo(300));
        }

        [Test]
        public void FullyViewedVaultRejectsHelpWithoutMutationOrInventoryConsumption()
        {
            var session = CasinoAdventureSession.Start(93, CasinoAdventureMode.Practice, 1, CasinoAdventureTests.QuietConfig());
            Assert.That(session.BeginGame("bet", CasinoGameKind.CooperativeVault, 100, 0).Success, Is.True);
            for (int digit = 0; digit < 3; digit++) Assert.That(session.Act("clue" + digit, CasinoMiniGameAction.InspectClue, digit).Success, Is.True);
            Assert.That(session.Purchase("buy", "duo_wrench").Success, Is.True); string before = session.State.ActiveRoundJson;
            Assert.That(session.UseItem("help", "duo_wrench").Error, Is.EqualTo("IncompatibleItem"));
            Assert.That(session.State.ActiveRoundJson, Is.EqualTo(before)); Assert.That(Count(session, "duo_wrench"), Is.EqualTo(1));
        }

        [Test]
        public void QueuedHelpOnlySpendsOneChargeWhenCompatibleRoundActuallyAcceptsIt()
        {
            var session = CasinoAdventureSession.Start(94, CasinoAdventureMode.Practice, 1, CasinoAdventureTests.QuietConfig());
            session.Purchase("buy1", "duo_wrench"); session.Purchase("buy2", "duo_wrench"); session.Purchase("buy3", "duo_wrench");
            Assert.That(session.UseItem("queue1", "duo_wrench").Success, Is.True); Assert.That(session.UseItem("queue2", "duo_wrench").Success, Is.True);
            Assert.That(session.State.CooperationHelpCharges, Is.EqualTo(2));
            Assert.That(session.BeginGame("instant", CasinoGameKind.CoinFlip, 10, 0).Success, Is.True);
            Assert.That(session.State.CooperationHelpCharges, Is.EqualTo(2));
            Assert.That(session.BeginGame("lever", CasinoGameKind.CooperativeLevers, 100, 0).Success, Is.True);
            Assert.That(session.State.CooperationHelpCharges, Is.EqualTo(1)); Assert.That(session.GetPresentation().CooperationHelpUsed, Is.True);
            Assert.That(session.UseItem("repeat-help", "duo_wrench").Error, Is.EqualTo("IncompatibleItem")); Assert.That(Count(session, "duo_wrench"), Is.EqualTo(1));
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.BeginGame("lever", CasinoGameKind.CooperativeLevers, 100, 0).Changed, Is.False);
            Assert.That(restored.State.CooperationHelpCharges, Is.EqualTo(1)); Assert.That(restored.UseItem("repeat-help", "duo_wrench").Changed, Is.False);
            Assert.That(Count(restored, "duo_wrench"), Is.EqualTo(1));
        }

        [Test]
        public void PreparedVaultHelpStillNeedsRealPasswordForFinalTakeover()
        {
            var config = CasinoAdventureTests.QuietConfig(); config.Targets = new long[] { 0 };
            var session = CasinoAdventureSession.Start(95, CasinoAdventureMode.Standard, 1, config);
            session.Purchase("buy", "duo_wrench"); session.UseItem("queue", "duo_wrench");
            Assert.That(session.CompleteStage("finish").Success, Is.True);
            Assert.That(session.BeginGame("challenge", CasinoGameKind.CooperativeVault, 100, 0).Success, Is.True);
            Assert.That(session.GetPresentation().CooperationHelpUsed, Is.True); Assert.That(session.State.TakeOverUnlocked, Is.False);
            Assert.That(session.ChooseEnding("premature", CasinoAdventureEnding.TakeOver).Success, Is.False);
            for (int digit = 1; digit <= 2; digit++) session.Act("clue" + digit, CasinoMiniGameAction.InspectClue, digit);
            var digits = session.GetPresentation().NumberValues;
            Assert.That(session.Act("unlock", CasinoMiniGameAction.EnterCode, digits[0] * 100 + digits[1] * 10 + digits[2]).Success, Is.True);
            Assert.That(session.State.TakeOverUnlocked, Is.True); Assert.That(session.ChooseEnding("takeover", CasinoAdventureEnding.TakeOver).Success, Is.True);
        }

        [Test]
        public void OtherGamesRejectHelpAndCannotRestoreForgedHelpFlag()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.Slots, 96, 100, 0); string before = round.ToSnapshotJson();
            Assert.That(round.TryApplyCooperationHelp(), Is.False); Assert.That(round.ToSnapshotJson(), Is.EqualTo(before));
            Assert.That(before, Does.Contain("\"CooperationHelpUsed\":false"));
            Assert.Throws<ArgumentException>(() => CasinoMiniGameRound.Restore(before.Replace("\"CooperationHelpUsed\":false", "\"CooperationHelpUsed\":true")));
            Assert.Throws<ArgumentOutOfRangeException>(() => CasinoMiniGameRound.DescribeRules(CasinoGameKind.Slots, cooperationHelp: true));
        }

        [Serializable]
        private sealed class RandomOnly { public uint RandomState; }
        private static uint ReadRandom(CasinoMiniGameRound round) => JsonUtility.FromJson<RandomOnly>(round.ToSnapshotJson()).RandomState;
        private static int Count(CasinoAdventureSession session, string id) => session.State.Inventory.Single(entry => entry.ItemId == id).Count;
    }
}
