using System;
using System.Linq;
using System.Text.RegularExpressions;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class CasinoAdventureTests
    {
        [Test]
        public void ShortcutUsesFullStageIdentityAndDoesNotExpireWhenExtraTimeIsAdded()
        {
            var config = new CasinoAdventureConfig { Targets = new long[] { 0, 0, 0, 0 } };
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Endless, 1, config);
            Assert.That(session.Purchase("key", "shortcut_key").Success, Is.True);
            Assert.That(session.UseItem("open", "shortcut_key", "local").Success, Is.True);
            var effect = session.State.Effects.Find(value => value.EffectKind == "OpenShortcut");
            Assert.That(effect.DurationMilliseconds, Is.Zero);
            Assert.That(effect.Value, Is.EqualTo(0));
            Assert.That(session.Purchase("time", "extra_time").Success, Is.True);
            Assert.That(session.UseItem("extend", "extra_time").Success, Is.True);
            session = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(session.State.Effects.Find(value => value.Id == effect.Id).DurationMilliseconds, Is.Zero);
            for (int index = 0; index < 4; index++)
            {
                Assert.That(session.CompleteStage("complete" + index).Success, Is.True);
                Assert.That(session.BeginNextStage("next" + index).Success, Is.True);
            }
            Assert.That(session.State.StageIndex, Is.EqualTo(4));
            Assert.That(session.State.Effects.Find(value => value.Id == effect.Id).Value, Is.Not.EqualTo(session.State.StageIndex));
            Assert.That(session.Purchase("new-key", "shortcut_key").Success, Is.True);
            var next = session.UseItem("new-open", "shortcut_key", "local");
            Assert.That(next.Success, Is.True);
            Assert.That(next.Effects[0].Value, Is.EqualTo(4));
        }

        [Test]
        public void GoalIsThresholdAndRequiresExplicitShoppingConfirmation()
        {
            var config = QuietConfig(); config.StageCount = 2; config.Targets = new long[] { 1100, 1100 };
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1, config);
            Assert.That(session.BeginGame("win", CasinoGameKind.CoinFlip, 100, 0).Success, Is.True);
            Assert.That(session.State.Coins, Is.EqualTo(1100));
            Assert.That(session.State.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            Assert.That(session.CompleteStage("complete").Success, Is.True);
            Assert.That(session.State.Coins, Is.EqualTo(1100), "额度仅为门槛，不能自动扣钱");
            Assert.That(session.State.Phase, Is.EqualTo(CasinoAdventurePhase.Shopping));
            Assert.That(session.BeginGame("too-soon", CasinoGameKind.CoinFlip, 10, 0).Success, Is.False);
            Assert.That(session.BeginNextStage("continue").Success, Is.True);
            Assert.That(session.State.StageIndex, Is.EqualTo(1));
            Assert.That(session.State.RemainingMilliseconds, Is.EqualTo(240000));
            Assert.That(session.BeginNextStage("continue").Changed, Is.False);
            Assert.That(session.State.StageIndex, Is.EqualTo(1));
        }

        [TestCase(900, CasinoAdventurePhase.Finale)]
        [TestCase(1100, CasinoAdventurePhase.Failed)]
        public void DeadlineEvaluatesWalletWithoutChargingQuota(long target, CasinoAdventurePhase expected)
        {
            var config = QuietConfig(); config.Targets = new[] { target }; config.StageDurationMilliseconds = 1000;
            var session = CasinoAdventureSession.Start(7, CasinoAdventureMode.Standard, 1, config);
            Assert.That(session.Advance(999).Success, Is.True);
            Assert.That(session.State.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            session.Advance(1);
            Assert.That(session.State.Phase, Is.EqualTo(expected));
            Assert.That(session.State.Coins, Is.EqualTo(1000));
            session.Advance(10000);
            Assert.That(session.State.CompletedStages, Is.EqualTo(expected == CasinoAdventurePhase.Finale ? 1 : 0));
        }

        [Test]
        public void AuctionLocksBudgetButChargesOnlyWinningBidOnceAcrossRestore()
        {
            var session = CasinoAdventureSession.Start(14, CasinoAdventureMode.Practice, 1, QuietConfig());
            Assert.That(session.BeginGame("auction", CasinoGameKind.BlindAuction, 300, 0).Success, Is.True);
            Assert.That(session.State.Coins, Is.EqualTo(1000));
            Assert.That(session.State.LockedCoins, Is.EqualTo(300));
            Assert.That(session.BeginGame("second", CasinoGameKind.CoinFlip, 1, 0).Success, Is.False);
            Assert.That(session.Act("bid", CasinoMiniGameAction.Bid, 250).Success, Is.True);
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.HasActiveRound, Is.True);
            Assert.That(restored.State.LockedCoins, Is.EqualTo(300));
            restored.Advance(10000);
            Assert.That(restored.HasActiveRound, Is.False);
            Assert.That(restored.State.LastRoundCost, Is.EqualTo(250));
            long settled = restored.State.Coins;
            Assert.That(settled, Is.EqualTo(1000 - 250 + restored.State.LastRoundPayout));
            Assert.That(restored.Act("bid", CasinoMiniGameAction.Bid, 250).Success, Is.True);
            Assert.That(restored.State.Coins, Is.EqualTo(settled));
            var after = CasinoAdventureSession.Restore(restored.ToSnapshotJson());
            after.Advance(10000);
            Assert.That(after.State.Coins, Is.EqualTo(settled));
        }

        [Test]
        public void CommittedGameSurvivesDeadlineAndCannotBeCanceledByEnding()
        {
            var config = QuietConfig(); config.StageDurationMilliseconds = 10; config.Targets = new long[] { 0 };
            var session = CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1, config);
            Assert.That(session.BeginGame("cards", CasinoGameKind.CoinFlip, 100, 2).Success, Is.True);
            Assert.That(session.HasActiveRound, Is.True);
            session.Advance(10);
            Assert.That(session.State.Phase, Is.EqualTo(CasinoAdventurePhase.Closing));
            Assert.That(session.ChooseEnding("exit", CasinoAdventureEnding.Withdraw).Success, Is.False);
            Assert.That(session.HasActiveRound, Is.True);
            // 种子2的首个硬币为反面；先执行合法猜测，再使用已解锁的连胜收手。
            Assert.That(session.Act("after-deadline", CasinoMiniGameAction.GuessTails).Success, Is.True);
            Assert.That(session.HasActiveRound, Is.True);
            Assert.That(session.Act("stand", CasinoMiniGameAction.CashOut).Success, Is.True);
            Assert.That(session.State.Phase, Is.EqualTo(CasinoAdventurePhase.Finale));
            Assert.That(session.State.LockedCoins, Is.Zero);
        }

        [Test]
        public void RejectedRequestAndConflictingPayloadRemainRejectedAfterRestore()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1, QuietConfig());
            Assert.That(session.BeginGame("denied", CasinoGameKind.CoinFlip, 1500, 0).Success, Is.False);
            Assert.That(session.BeginGame("win", CasinoGameKind.CoinFlip, 1000, 0).Success, Is.True);
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.State.Coins, Is.EqualTo(2000));
            Assert.That(restored.BeginGame("denied", CasinoGameKind.CoinFlip, 1500, 0).Success, Is.False);
            Assert.That(restored.BeginGame("win", CasinoGameKind.CoinFlip, 1, 0).Error, Is.EqualTo("ConflictingRequest"));
            Assert.That(restored.State.Coins, Is.EqualTo(2000));
        }

        [Test]
        public void PreparedItemsAreReservedUntilCompatibleRoundIsCommitted()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1, QuietConfig());
            Assert.That(session.Purchase("buy", "xray").Success, Is.True);
            Assert.That(session.UseItem("prepare", "xray").Success, Is.True);
            Assert.That(Count(session, "xray"), Is.EqualTo(1));
            long balance = session.State.Coins;
            Assert.That(session.BeginGame("incompatible", CasinoGameKind.CoinFlip, 100, 0).Success, Is.False);
            Assert.That(session.State.Coins, Is.EqualTo(balance));
            Assert.That(session.GetPreparedItems(), Is.EqualTo(new[] { "xray" }));
            Assert.That(Count(session, "xray"), Is.EqualTo(1));
            Assert.That(session.CancelPreparedItem("cancel", "xray").Success, Is.True);
            Assert.That(session.GetPreparedItems(), Is.Empty);
            Assert.That(Count(session, "xray"), Is.EqualTo(1));
            Assert.That(session.BeginGame("valid", CasinoGameKind.CooperativeVault, 100, 0).Success, Is.True);
            Assert.That(session.UseItem("apply", "xray").Success, Is.True);
            Assert.That(Count(session, "xray"), Is.Zero);
        }

        [Test]
        public void StopLossUsesRealRuleAndCannotBeConsumedTwice()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1, QuietConfig());
            session.Purchase("buy", "stop_loss"); session.UseItem("prepare", "stop_loss");
            Assert.That(session.BeginGame("lose", CasinoGameKind.CoinFlip, 100, 1).Success, Is.True);
            Assert.That(session.State.LastRoundPayout, Is.EqualTo(50));
            Assert.That(Count(session, "stop_loss"), Is.Zero);
            long balance = session.State.Coins;
            session.BeginGame("lose", CasinoGameKind.CoinFlip, 100, 1);
            Assert.That(session.State.Coins, Is.EqualTo(balance));
        }

        [Test]
        public void WalletOverflowRollsBackRandomAndFundsThenKeepsRejectedReceipt()
        {
            var config = QuietConfig(); config.StartingCoins = long.MaxValue;
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1, config);
            uint random = session.State.RandomState;
            Assert.That(session.BeginGame("overflow", CasinoGameKind.CoinFlip, 1, 0).Error, Is.EqualTo("BalanceOverflow"));
            Assert.That(session.State.Coins, Is.EqualTo(long.MaxValue));
            Assert.That(session.State.RandomState, Is.EqualTo(random));
            Assert.That(session.HasActiveRound, Is.False);
            Assert.That(session.State.LockedCoins, Is.Zero);
            Assert.That(CasinoAdventureSession.Restore(session.ToSnapshotJson()).BeginGame("overflow", CasinoGameKind.CoinFlip, 1, 0).Success, Is.False);
        }

        [Test]
        public void PracticeRefillIsExplicitAndCannotEraseActiveGame()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1, QuietConfig());
            session.BeginGame("loss", CasinoGameKind.CoinFlip, 1000, 1);
            Assert.That(session.State.Coins, Is.Zero);
            session.Advance(10000);
            Assert.That(session.State.Coins, Is.Zero);
            Assert.That(session.ResetPractice("refill").Success, Is.True);
            Assert.That(session.State.Coins, Is.EqualTo(1000));
            session.BeginGame("active", CasinoGameKind.BlindAuction, 100, 0);
            Assert.That(session.ResetPractice("bad-refill").Success, Is.False);
            Assert.That(session.HasActiveRound, Is.True);
            Assert.That(session.CompleteStage("formal-goal").Success, Is.False);
        }

        [Test]
        public void PrankProtectionPersistsAcrossSnapshotAndRejectedUseKeepsInventory()
        {
            var session = CasinoAdventureSession.Start(11, CasinoAdventureMode.Practice, 1, QuietConfig());
            session.Purchase("buy1", "bubble_gun"); session.Purchase("buy2", "banana_peel");
            var effect = session.UseItem("prank", "bubble_gun", "friend").Effects.Single();
            Assert.That(effect.DurationMilliseconds, Is.LessThanOrEqualTo(3000));
            Assert.That(effect.ProtectionMilliseconds, Is.EqualTo(5000));
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.UseItem("protected", "banana_peel", "friend").Error, Is.EqualTo("TargetProtected"));
            Assert.That(Count(restored, "banana_peel"), Is.EqualTo(1));
            restored.Advance(5000);
            Assert.That(restored.UseItem("later", "banana_peel", "friend").Success, Is.True);
            Assert.That(Count(restored, "banana_peel"), Is.Zero);
        }

        [Test]
        public void ChipRainRequiresUniqueInteractionsAndSnapshotDoesNotPayAgain()
        {
            var session = CasinoAdventureSession.Start(4, CasinoAdventureMode.Standard, 1, SingleEventConfig("chip_rain"));
            session.Advance(1000);
            var task = session.State.ActiveMission;
            Assert.That(task.Progress, Is.Zero);
            Assert.That(session.State.Coins, Is.EqualTo(1000), "任务出现本身不发奖励");
            Assert.That(session.AdvanceTask("one", task.Id, CasinoTaskAction.Collect, 0).Success, Is.True);
            Assert.That(session.AdvanceTask("duplicate-spot", task.Id, CasinoTaskAction.Collect, 0).Success, Is.False);
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            for (int index = 1; index < 5; index++) Assert.That(restored.AdvanceTask("pick" + index, task.Id, CasinoTaskAction.Collect, index).Success, Is.True);
            Assert.That(restored.State.ActiveMission.Completed, Is.True);
            Assert.That(restored.State.Coins, Is.EqualTo(1150));
            var finished = CasinoAdventureSession.Restore(restored.ToSnapshotJson());
            finished.AdvanceTask("pick4", task.Id, CasinoTaskAction.Collect, 4);
            Assert.That(finished.State.Coins, Is.EqualTo(1150));
        }

        [Test]
        public void GoldMissionMustPickUpBeforeDeliveryAndExpiredTaskCannotReward()
        {
            var session = CasinoAdventureSession.Start(4, CasinoAdventureMode.Standard, 1, SingleEventConfig("gold_delivery"));
            session.Advance(1000); string task = session.State.ActiveMission.Id;
            Assert.That(session.AdvanceTask("no-carry", task, CasinoTaskAction.Deliver).Success, Is.False);
            Assert.That(session.AdvanceTask("carry", task, CasinoTaskAction.Carry).Success, Is.True);
            session.Advance(45000);
            Assert.That(session.AdvanceTask("too-late", task, CasinoTaskAction.Deliver).Success, Is.False);
            Assert.That(session.State.Coins, Is.EqualTo(1000));
        }

        [TestCase(1)]
        [TestCase(6)]
        public void SyncMissionSupportsSoloAndCountsDistinctPlayers(int players)
        {
            var session = CasinoAdventureSession.Start(4, CasinoAdventureMode.Standard, players, SingleEventConfig("sync_buttons"));
            session.Advance(1000); string task = session.State.ActiveMission.Id;
            Assert.That(session.AdvanceTask("press0", task, CasinoTaskAction.Press, actorId: "p0").Success, Is.True);
            if (players > 1) Assert.That(session.AdvanceTask("same-player", task, CasinoTaskAction.Press, actorId: "p0").Success, Is.False);
            for (int index = 1; index < players; index++) Assert.That(session.AdvanceTask("press" + index, task, CasinoTaskAction.Press, actorId: "p" + index).Success, Is.True);
            Assert.That(session.State.ActiveMission.Completed, Is.True);
            Assert.That(session.State.Coins, Is.GreaterThan(1000));
        }

        [Test]
        public void MerchantRequiresChoiceAndChargesExactlyOnceAfterRestore()
        {
            var session = CasinoAdventureSession.Start(6, CasinoAdventureMode.Standard, 1, SingleEventConfig("mystery_merchant"));
            session.Advance(1000);
            Assert.That(session.State.Inventory, Is.Empty);
            Assert.That(session.GetEventActions().Length, Is.EqualTo(2));
            Assert.That(session.ResolveEvent("buy-event", 1).Success, Is.True);
            Assert.That(session.State.Coins, Is.EqualTo(920));
            Assert.That(session.State.Inventory.Sum(item => item.Count), Is.EqualTo(1));
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            restored.ResolveEvent("buy-event", 1);
            Assert.That(restored.State.Coins, Is.EqualTo(920));
            Assert.That(restored.State.Inventory.Sum(item => item.Count), Is.EqualTo(1));
        }

        [Test]
        public void RuleEventShowsBeforeBetAndCannotChangeAlreadyCommittedPayout()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1, SingleEventConfig("low_stakes"));
            session.Advance(1000);
            Assert.That(session.State.EventStakeCap, Is.EqualTo(100));
            Assert.That(session.NextBetDescription, Does.Contain("100"));
            Assert.That(session.BeginGame("too-big", CasinoGameKind.CoinFlip, 101, 0).Success, Is.False);
            Assert.That(session.BeginGame("valid", CasinoGameKind.CoinFlip, 100, 0).Success, Is.True);
            Assert.That(session.State.ActiveRoundPayoutBonusPercent, Is.Zero, "已完成局清空锁定加成");
            Assert.That(session.State.Coins, Is.EqualTo(1000 - session.State.LastRoundCost + session.State.LastRoundPayout));
        }

        [Test]
        public void TakeOverRequiresActualCooperativeChallengeVictory()
        {
            var config = QuietConfig(); config.Targets = new long[] { 0 };
            var session = CasinoAdventureSession.Start(3, CasinoAdventureMode.Standard, 1, config);
            session.CompleteStage("finale");
            Assert.That(session.ChooseEnding("cheat", CasinoAdventureEnding.TakeOver).Error, Is.EqualTo("ChallengeRequired"));
            Assert.That(session.BeginGame("challenge", CasinoGameKind.CooperativeVault, 10, 0).Success, Is.True);
            for (int index = 0; index < 3; index++) Assert.That(session.Act("clue" + index, CasinoMiniGameAction.InspectClue, index).Success, Is.True);
            var clue = Regex.Match(session.ActiveRoundDescription, "互补线索：(\\d{3})");
            Assert.That(clue.Success, Is.True, session.ActiveRoundDescription);
            Assert.That(session.Act("open", CasinoMiniGameAction.EnterCode, int.Parse(clue.Groups[1].Value)).Success, Is.True);
            Assert.That(session.State.TakeOverUnlocked, Is.True);
            Assert.That(session.ChooseEnding("take-over", CasinoAdventureEnding.TakeOver).Success, Is.True);
            Assert.That(session.State.Ending, Is.EqualTo(CasinoAdventureEnding.TakeOver));
        }

        [Test]
        public void ConfigAndExposedStateCannotMutateLiveWalletOrRules()
        {
            var config = QuietConfig();
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1, config);
            config.StartingCoins = 999999; config.Targets[0] = 0;
            var copy = session.State; copy.Coins = 0; copy.Config.Targets[0] = 0;
            Assert.That(session.State.Coins, Is.EqualTo(1000));
            Assert.That(session.CurrentTarget, Is.EqualTo(1100));
            copy.LockedCoins = 1001;
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(copy)));
        }

        [Test]
        public void InsuranceCompensationCannotForgeCooperativeChallengeVictory()
        {
            var config = QuietConfig(); config.Targets = new long[] { 0 };
            var session = CasinoAdventureSession.Start(3, CasinoAdventureMode.Standard, 1, config);
            session.Purchase("buy-insurance", "stop_loss"); session.Purchase("buy-coupon", "jackpot_coupon");
            session.UseItem("insurance", "stop_loss"); session.UseItem("coupon", "jackpot_coupon");
            session.CompleteStage("finale");
            session.BeginGame("challenge", CasinoGameKind.CooperativeVault, 10, 0);
            for (int index = 0; index < 3; index++) session.Act("clue" + index, CasinoMiniGameAction.InspectClue, index);
            for (int index = 0; index < 3; index++) Assert.That(session.Act("wrong" + index, CasinoMiniGameAction.EnterCode, 0).Success, Is.True);
            Assert.That(session.State.LastRoundPayout, Is.GreaterThan(session.State.LastRoundCost));
            Assert.That(session.State.TakeOverUnlocked, Is.False);
            Assert.That(session.ChooseEnding("take-over", CasinoAdventureEnding.TakeOver).Error, Is.EqualTo("ChallengeRequired"));
        }

        [Test]
        public void EndlessCyclesScaleGoalsAndNeverExposeStandardSuccessEnding()
        {
            var config = QuietConfig(); config.Targets = new long[] { 100 };
            var session = CasinoAdventureSession.Start(3, CasinoAdventureMode.Endless, 1, config);
            session.CompleteStage("first"); session.BeginNextStage("next");
            Assert.That(session.CurrentTarget, Is.EqualTo(150));
            Assert.That(session.State.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            Assert.That(session.ChooseEnding("success", CasinoAdventureEnding.LeaveWithDignity).Success, Is.False);
            Assert.That(session.ChooseEnding("leave", CasinoAdventureEnding.Withdraw).Success, Is.True);
        }

        [Test]
        public void NoMissionRemainsNullAcrossDefensiveCopiesRollbackAndSnapshotRestore()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1, SingleEventConfig("chip_rain"));
            Assert.That(session.State.ActiveMission, Is.Null);
            Assert.That(session.BeginGame("bad-stake", CasinoGameKind.CoinFlip, 1001, 0).Success, Is.False);
            Assert.That(session.CaptureState().ActiveMission, Is.Null, "失败请求回滚不能生成幽灵任务");
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.State.ActiveMission, Is.Null);
            Assert.That(restored.State.Coins, Is.EqualTo(1000));
            Assert.That(restored.BeginGame("bad-stake", CasinoGameKind.CoinFlip, 1001, 0).Success, Is.False);
            Assert.That(restored.Advance(1000).Success, Is.True);
            Assert.That(restored.State.ActiveMission, Is.Not.Null, "无任务占位不能拦截下一随机事件");
            Assert.That(restored.State.ActiveMission.EventId, Is.EqualTo("chip_rain"));
            Assert.That(restored.State.ActiveMission.Progress, Is.Zero);
        }

        [Test]
        public void PartialOrCorruptedMissionCannotBeNormalizedAway()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1, QuietConfig());
            var invalid = session.State;
            invalid.ActiveMission = new CasinoAdventureMission { EventId = "chip_rain" };
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(invalid)));
            invalid.ActiveMission = new CasinoAdventureMission { RewardCoins = 150 };
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(invalid)));
            invalid.ActiveMission = new CasinoAdventureMission { Id = "broken", TargetCount = 5, Progress = 6, EventId = "chip_rain" };
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(invalid)));
        }

        internal static CasinoAdventureConfig QuietConfig() => new CasinoAdventureConfig { StageCount = 1, Targets = new long[] { 1100 }, EventIntervalMilliseconds = 0 };
        internal static CasinoAdventureConfig SingleEventConfig(string id)
        {
            var config = QuietConfig(); config.EventIntervalMilliseconds = 1000;
            config.EventWeights = CasinoContentCatalog.Events.Select(entry => new CasinoEventWeight { EventId = entry.Id, Weight = entry.Id == id ? 1 : 0 }).ToArray();
            return config;
        }
        private static int Count(CasinoAdventureSession session, string id) => session.State.Inventory.Find(item => item.ItemId == id)?.Count ?? 0;
    }
}
