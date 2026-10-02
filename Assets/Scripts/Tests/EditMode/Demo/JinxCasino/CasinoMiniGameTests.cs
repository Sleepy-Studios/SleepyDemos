using System;
using System.Text.RegularExpressions;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class CasinoMiniGameTests
    {
        [TestCase(CasinoGameKind.Slots, 0, 20)]
        [TestCase(CasinoGameKind.Roulette, 9, 360)]
        [TestCase(CasinoGameKind.CoinFlip, 0, 20)]
        public void InstantGamesPreserveOriginalGoldenPayoutsAndRestoreWithoutRedraw(CasinoGameKind game, int choice, long payout)
        {
            var round = CasinoMiniGameRound.Create(game, 1, 10, choice);
            Assert.That(round.IsComplete, Is.True);
            Assert.That(round.Cost, Is.EqualTo(10));
            Assert.That(round.Payout, Is.EqualTo(payout));
            string json = round.ToSnapshotJson();
            var restored = CasinoMiniGameRound.Restore(json);
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(json));
            restored.Advance(10000);
            Assert.That(restored.TryAct(CasinoMiniGameAction.Play), Is.False);
            Assert.That(restored.ApplyRuleItem("stop_loss"), Is.False);
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(json));
        }

        [Test]
        public void PreparedInstantItemsApplyBeforeResultAndRejectIncompatiblePreparation()
        {
            var protectedLoss = CasinoMiniGameRound.Create(CasinoGameKind.CoinFlip, 1, 10, 1,
                preparedItems: new[] { "stop_loss" });
            Assert.That(protectedLoss.Payout, Is.EqualTo(5));
            var coupon = CasinoMiniGameRound.Create(CasinoGameKind.CoinFlip, 1, 10, 0,
                preparedItems: new[] { "jackpot_coupon" });
            Assert.That(coupon.Payout, Is.EqualTo(30));
            Assert.Throws<ArgumentException>(() => CasinoMiniGameRound.Create(CasinoGameKind.CoinFlip, 1, 10, 0,
                preparedItems: new[] { "lock_reel" }));
            Assert.Throws<ArgumentException>(() => CasinoMiniGameRound.Create(CasinoGameKind.CoinFlip, 1, 10, 0,
                preparedItems: new[] { "stop_loss", "stop_loss" }));
        }

        [Test]
        public void EveryGameCreatesRestorableDistinctRulesAndRejectsUnknownActionsWithoutMutation()
        {
            var descriptions = new System.Collections.Generic.HashSet<string>();
            foreach (CasinoGameKind game in Enum.GetValues(typeof(CasinoGameKind)))
            {
                var round = CasinoMiniGameRound.Create(game, 51, 100, 0);
                descriptions.Add(CasinoMiniGameRound.DescribeRules(game));
                string before = round.ToSnapshotJson();
                Assert.That(CasinoMiniGameRound.Restore(before).ToSnapshotJson(), Is.EqualTo(before), game.ToString());
                Assert.That(round.TryAct((CasinoMiniGameAction)999, int.MaxValue), Is.False);
                Assert.That(round.ToSnapshotJson(), Is.EqualTo(before));
                Assert.That(round.Cost, Is.InRange(0, 100));
            }
            Assert.That(descriptions.Count, Is.EqualTo(17));
        }

        [TestCase(CasinoGameKind.Blackjack)]
        [TestCase(CasinoGameKind.HighLow)]
        public void CardContinuationAndRedrawKeepRngAcrossRestore(CasinoGameKind game)
        {
            var original = CasinoMiniGameRound.Create(game, 45, 100, 0);
            var restored = CasinoMiniGameRound.Restore(original.ToSnapshotJson());
            Assert.That(original.ApplyRuleItem("redraw_card"), Is.EqualTo(restored.ApplyRuleItem("redraw_card")));
            Assert.That(original.ApplyRuleItem("xray"), Is.EqualTo(restored.ApplyRuleItem("xray")));
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(original.ToSnapshotJson()));
            CasinoMiniGameAction action = game == CasinoGameKind.Blackjack ? CasinoMiniGameAction.Stand : CasinoMiniGameAction.GuessHigher;
            Assert.That(original.TryAct(action), Is.EqualTo(restored.TryAct(action)));
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(original.ToSnapshotJson()));
        }

        [Test]
        public void BingoRejectsDuplicateSelectionAndFinishesSameTenBallSequenceAfterRestore()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.Bingo, 72, 100, 0);
            for (int i = 1; i <= 5; i++) Assert.That(round.TryAct(CasinoMiniGameAction.SelectNumber, i), Is.True);
            string selected = round.ToSnapshotJson();
            Assert.That(round.TryAct(CasinoMiniGameAction.SelectNumber, 1), Is.False);
            Assert.That(round.ToSnapshotJson(), Is.EqualTo(selected));
            Assert.That(round.TryAct(CasinoMiniGameAction.DrawNumber), Is.True);
            Assert.That(round.TryAct(CasinoMiniGameAction.SelectNumber, 6), Is.False);
            var restored = CasinoMiniGameRound.Restore(round.ToSnapshotJson());
            round.Advance(10000); restored.Advance(37); restored.Advance(9963);
            Assert.That(round.IsComplete, Is.True);
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(round.ToSnapshotJson()));
        }

        [Test]
        public void CoinStreakOffersCashOutOnlyAfterWinAndPreservesOrdinaryMode()
        {
            var streak = CasinoMiniGameRound.Create(CasinoGameKind.CoinFlip, 1, 100, 2);
            Assert.That(streak.IsComplete, Is.False);
            Assert.That(streak.TryAct(CasinoMiniGameAction.CashOut), Is.False);
            Assert.That(streak.TryAct(CasinoMiniGameAction.GuessHeads), Is.True);
            Assert.That(streak.IsComplete, Is.False);
            var restored = CasinoMiniGameRound.Restore(streak.ToSnapshotJson());
            Assert.That(restored.TryAct(CasinoMiniGameAction.CashOut), Is.True);
            Assert.That(restored.Payout, Is.EqualTo(200));
        }

        [TestCase(CasinoGameKind.Plinko)]
        [TestCase(CasinoGameKind.MechanicalRace)]
        [TestCase(CasinoGameKind.BlindAuction)]
        [TestCase(CasinoGameKind.CooperativeVault)]
        [TestCase(CasinoGameKind.PassingBag)]
        public void TimedGamesUseFixedTicksAndResumePartialTickWithoutFrameRateDrift(CasinoGameKind game)
        {
            var original = CasinoMiniGameRound.Create(game, 97, 100, 0);
            if (game == CasinoGameKind.Plinko) Assert.That(original.TryAct(CasinoMiniGameAction.DropBall, 3), Is.True);
            if (game == CasinoGameKind.MechanicalRace) { original.TryAct(CasinoMiniGameAction.Boost); original.TryAct(CasinoMiniGameAction.Dodge); }
            if (game == CasinoGameKind.PassingBag) original.TryAct(CasinoMiniGameAction.PassBag);
            original.Advance(37);
            var restored = CasinoMiniGameRound.Restore(original.ToSnapshotJson());
            original.Advance(30000);
            for (int i = 0; i < 300; i++) restored.Advance(100);
            Assert.That(original.IsComplete, Is.True);
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(original.ToSnapshotJson()));
        }

        [Test]
        public void SinglePlayerLeverNpcCompletesSameCycleAndWrongTimingIsARealFailure()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeLevers, 1, 100, 0);
            round.Advance(300);
            Assert.That(round.TryAct(CasinoMiniGameAction.PullLever, 0), Is.True);
            round.Advance(200);
            Assert.That(round.IsComplete, Is.True); Assert.That(round.Payout, Is.EqualTo(400));
            var failed = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeLevers, 1, 100, 0);
            for (int i = 0; i < 3; i++) Assert.That(failed.TryAct(CasinoMiniGameAction.PullLever, 0), Is.True);
            Assert.That(failed.IsComplete, Is.True); Assert.That(failed.Payout, Is.Zero);
        }

        [Test]
        public void VaultExposesComplementaryCluesAndRequiresActualPasswordEntry()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeVault, 13, 100, 0);
            Assert.That(round.TryAct(CasinoMiniGameAction.EnterCode, 111), Is.False);
            Assert.That(round.TryAct(CasinoMiniGameAction.InspectClue, 0), Is.True);
            Assert.That(round.TryAct(CasinoMiniGameAction.InspectClue, 0), Is.False);
            round.Advance(4000);
            var match = Regex.Match(round.Description, "互补线索：([1-9]{3})");
            Assert.That(match.Success, Is.True, round.Description);
            var restored = CasinoMiniGameRound.Restore(round.ToSnapshotJson());
            Assert.That(restored.TryAct(CasinoMiniGameAction.EnterCode, int.Parse(match.Groups[1].Value)), Is.True);
            Assert.That(restored.Payout, Is.EqualTo(300));
            Assert.That(restored.IsObjectiveSuccess, Is.True);
            var protectedFailure = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeVault, 13, 100, 0,
                preparedItems: new[] { "stop_loss", "jackpot_coupon" });
            protectedFailure.Advance(20000);
            Assert.That(protectedFailure.Payout, Is.EqualTo(150));
            Assert.That(protectedFailure.IsObjectiveSuccess, Is.False);
            Assert.That(CasinoMiniGameRound.Restore(protectedFailure.ToSnapshotJson()).IsObjectiveSuccess, Is.False);
        }

        [Test]
        public void AuctionPaysOnlyWinningBidAndUnsuccessfulAuctionCostsZero()
        {
            var winner = CasinoMiniGameRound.Create(CasinoGameKind.BlindAuction, 19, 100, 0);
            Assert.That(winner.TryAct(CasinoMiniGameAction.RevealClue), Is.True);
            Assert.That(winner.TryAct(CasinoMiniGameAction.Bid, 90), Is.True);
            var restored = CasinoMiniGameRound.Restore(winner.ToSnapshotJson());
            winner.Advance(10000); restored.Advance(10000);
            Assert.That(winner.Cost, Is.EqualTo(90)); Assert.That(winner.Payout, Is.InRange(50, 200));
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(winner.ToSnapshotJson()));
            var loser = CasinoMiniGameRound.Create(CasinoGameKind.BlindAuction, 19, 100, 0);
            loser.TryAct(CasinoMiniGameAction.Bid, 1); loser.Advance(10000);
            Assert.That(loser.Cost, Is.Zero); Assert.That(loser.Payout, Is.Zero);
            var declined = CasinoMiniGameRound.Create(CasinoGameKind.BlindAuction, 19, 100, 0);
            declined.TryAct(CasinoMiniGameAction.CashOut); Assert.That(declined.IsComplete, Is.True); Assert.That(declined.Cost, Is.Zero);
        }

        [TestCase(0)]
        [TestCase(12)]
        [TestCase(25)]
        public void LuckyDrawRiskTubesArePlayerSelectedAndInvalidSelectionDoesNotDraw(int choice)
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.LuckyDraw, 87, 100, choice);
            string before = round.ToSnapshotJson();
            Assert.That(round.TryAct(CasinoMiniGameAction.PickPrize, 6), Is.False);
            Assert.That(round.ToSnapshotJson(), Is.EqualTo(before));
            var restored = CasinoMiniGameRound.Restore(before);
            Assert.That(round.TryAct(CasinoMiniGameAction.PickPrize, choice % 10), Is.True);
            Assert.That(restored.TryAct(CasinoMiniGameAction.PickPrize, choice % 10), Is.True);
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(round.ToSnapshotJson()));
        }

        [Test]
        public void DiceBiasAndRescueRemainDeterministicAndItemsRejectWithoutMutation()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.PushYourLuckDice, 81, 100, 0, diceBias: 6);
            Assert.That(round.Description, Does.Contain("37.5%"));
            Assert.That(round.ApplyRuleItem("reroll_dice"), Is.True);
            string before = round.ToSnapshotJson();
            Assert.That(round.ApplyRuleItem("reroll_dice"), Is.False);
            Assert.That(round.ApplyRuleItem("lock_reel"), Is.False);
            Assert.That(round.ToSnapshotJson(), Is.EqualTo(before));
            var restored = CasinoMiniGameRound.Restore(before);
            for (int i = 0; i < 4; i++) { round.TryAct(CasinoMiniGameAction.RollDice); restored.TryAct(CasinoMiniGameAction.RollDice); }
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(round.ToSnapshotJson()));
            round.TryAct(CasinoMiniGameAction.CashOut); restored.TryAct(CasinoMiniGameAction.CashOut);
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(round.ToSnapshotJson()));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(10)] [TestCase(15)] [TestCase(103)] [TestCase(118)] [TestCase(200)] [TestCase(235)] [TestCase(300)] [TestCase(305)]
        public void SicBoSupportsBetAreasAndRerollBeforeInstantDraw(int choice)
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.SicBo, 95, 100, choice, preparedItems: new[] { "reroll_dice" }, diceBias: 3);
            Assert.That(round.IsComplete, Is.True);
            Assert.That(CasinoMiniGameRound.Restore(round.ToSnapshotJson()).ToSnapshotJson(), Is.EqualTo(round.ToSnapshotJson()));
        }

        [Test]
        public void ElevatorSupportsFloorChoiceAndCashOutInsteadOfAutomaticSingleResult()
        {
            CasinoMiniGameRound round = null;
            for (uint seed = 1; seed <= 50; seed++)
            {
                var candidate = CasinoMiniGameRound.Create(CasinoGameKind.ChickenElevator, seed, 100, 0);
                candidate.TryAct(CasinoMiniGameAction.Climb, 0);
                if (!candidate.IsComplete) { round = candidate; break; }
            }
            Assert.That(round, Is.Not.Null);
            var restored = CasinoMiniGameRound.Restore(round.ToSnapshotJson());
            Assert.That(restored.TryAct(CasinoMiniGameAction.CashOut), Is.True);
            Assert.That(restored.Payout, Is.EqualTo(200));
        }

        [Test]
        public void EveryPlinkoLayerAndBagHoldingCanBeRestoredBeforeCompletion()
        {
            var plinko = CasinoMiniGameRound.Create(CasinoGameKind.Plinko, 9, 100, 0);
            plinko.TryAct(CasinoMiniGameAction.DropBall, 0);
            for (int i = 0; i < 8; i++)
            {
                plinko.Advance(100);
                Assert.That(CasinoMiniGameRound.Restore(plinko.ToSnapshotJson()).ToSnapshotJson(), Is.EqualTo(plinko.ToSnapshotJson()));
            }
            var bag = CasinoMiniGameRound.Create(CasinoGameKind.PassingBag, 9, 100, 0);
            bag.Advance(1000);
            Assert.That(bag.TryAct(CasinoMiniGameAction.HoldBag), Is.True);
            Assert.That(bag.TryAct(CasinoMiniGameAction.PassBag), Is.False);
            var restored = CasinoMiniGameRound.Restore(bag.ToSnapshotJson());
            restored.Advance(500);
            Assert.That(restored.TryAct(CasinoMiniGameAction.PassBag), Is.True);
        }

        [Test]
        public void InvalidSnapshotsChoicesAndNegativeTimeFailExplicitly()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.Bingo, 1, 100, 0);
            Assert.Throws<ArgumentException>(() => CasinoMiniGameRound.Restore("{}"));
            Assert.Throws<ArgumentException>(() => CasinoMiniGameRound.Restore(round.ToSnapshotJson().Replace("\"SchemaVersion\":1", "\"SchemaVersion\":2")));
            Assert.Throws<ArgumentOutOfRangeException>(() => CasinoMiniGameRound.Create(CasinoGameKind.LuckyDraw, 1, 100, 16));
            Assert.Throws<ArgumentOutOfRangeException>(() => CasinoMiniGameRound.Create(CasinoGameKind.SicBo, 1, 100, 40));
            Assert.Throws<ArgumentOutOfRangeException>(() => round.Advance(-1));
        }
    }
}
