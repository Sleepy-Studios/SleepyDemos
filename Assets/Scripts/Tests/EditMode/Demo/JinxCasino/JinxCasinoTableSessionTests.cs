using System;
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;

namespace Tests.Demo
{
    /// 使用真实冒险/小游戏规则测试桌面适配，不把筹码草稿或拉柄演出当第二套开奖。
    public sealed class JinxCasinoTableSessionTests
    {
        [Test]
        public void SlotsRequirePreparationAndRealLeverCommitsExactlyOnce()
        {
            var host = new RuleHost(CasinoGameKind.Slots, "slots-a", 2);
            var table = Table(host); var before = host.AdventureState;
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 1).Success, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 50, 2).Success, Is.True);
            Assert.That(table.Apply(JinxCasinoTableAction.Commit, 0, 3).Success, Is.True);
            Assert.That(table.GetView().IsSlotsPrepared, Is.True); Assert.That(table.GetView().Presentation, Is.Null);
            Assert.That(host.AdventureState.RandomState, Is.EqualTo(before.RandomState)); Assert.That(host.AdventureState.Coins, Is.EqualTo(before.Coins));
            Assert.That(host.AdventureState.LockedCoins, Is.Zero); Assert.That(host.AdventureState.SettledRoundSequence, Is.Zero);
            var result = table.Apply(JinxCasinoTableAction.Primary, 0, 4); Assert.That(result.Success, Is.True);
            var committed = host.AdventureState;
            Assert.That(committed.LastStationId, Is.EqualTo("slots-a")); Assert.That(committed.SettledRoundSequence, Is.EqualTo(1));
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 4).Changed, Is.False);
            Assert.That(table.RetryLastCommand(5).RequestId, Is.EqualTo(result.RequestId));
            Assert.That(host.AdventureState.Coins, Is.EqualTo(committed.Coins)); Assert.That(host.AdventureState.RandomState, Is.EqualTo(committed.RandomState));
            Assert.That(host.AdventureState.SettledRoundSequence, Is.EqualTo(1));
            Assert.That(table.GetView().Presentation.Payout, Is.EqualTo(committed.LastRoundPayout));
        }

        [Test]
        public void DenominationsAndSameFrameGateNeverChangeRulesUntilCommit()
        {
            var host = new RuleHost(CasinoGameKind.Blackjack, "cards-a", 1); var table = Table(host);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 10, 1).Success, Is.True);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 10, 1).Changed, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 50, 1).Error, Is.EqualTo("FrameBusy"));
            Assert.That(table.GetView().DraftStake, Is.EqualTo(10));
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 50, 2).Success, Is.True);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 100, 3).Success, Is.True);
            Assert.That(table.GetView().DraftStake, Is.EqualTo(160));
            Assert.That(table.GetView().GetAvailability(JinxCasinoTableAction.ChipAdd, 7).IsAvailable, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipClear, 0, 4).Success, Is.True);
            Assert.That(table.GetView().DraftStake, Is.Zero); Assert.That(host.AdventureState.Coins, Is.EqualTo(1000));
            Assert.That(host.AdventureState.ProcessedRequests, Is.Empty);
        }

        [Test]
        public void SlotRulesAmountStageAndWorldGameChangesInvalidatePreparedCommit()
        {
            var host = new RuleHost(CasinoGameKind.Slots, "slots-a", 2); var table = Table(host);
            table.Apply(JinxCasinoTableAction.ChipAdd, 50, 1); table.Apply(JinxCasinoTableAction.Commit, 0, 2);
            table.Apply(JinxCasinoTableAction.ChipAdd, 10, 3); Assert.That(table.GetView().IsSlotsPrepared, Is.False);
            table.Apply(JinxCasinoTableAction.Commit, 0, 4);
            Assert.That(host.Rules.Purchase("coupon-buy", "jackpot_coupon").Success, Is.True);
            Assert.That(host.Rules.UseItem("coupon-prepare", "jackpot_coupon").Success, Is.True); host.Refresh();
            var random = host.AdventureState.RandomState;
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 5).Success, Is.False);
            Assert.That(host.AdventureState.RandomState, Is.EqualTo(random)); Assert.That(host.AdventureState.SettledRoundSequence, Is.Zero);
            table.Apply(JinxCasinoTableAction.Commit, 0, 6); host.StationGame = CasinoGameKind.Blackjack;
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 7).Success, Is.False); Assert.That(table.GetView().IsSlotsPrepared, Is.False);
            host.StationGame = CasinoGameKind.Slots;
            table.Apply(JinxCasinoTableAction.Commit, 0, 8);
            // 标准局达额推进到购物，准备不能穿过阶段；无隐式偿债或支付。
            var config = new CasinoAdventureConfig { Targets = new long[] { 900, 900, 900, 900 }, EventIntervalMilliseconds = 0 };
            host.ReplaceRules(CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1, config));
            table.Apply(JinxCasinoTableAction.Commit, 0, 9);
            Assert.That(host.Rules.CompleteStage("stage").Success, Is.True); host.Refresh();
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 10).Success, Is.False); Assert.That(table.GetView().IsSlotsPrepared, Is.False);
        }

        [Test]
        public void BlackjackLeavesAndReturnsWithoutRedealingAndOnlySameTableCanAct()
        {
            var host = new RuleHost(CasinoGameKind.Blackjack, "cards-a", 1);
            // 以真实规则找确定的非天然21种子，断言必须确有活动手牌，不能靠概率假设。
            uint selectedSeed = 0;
            for (uint seed = 1; seed < 100; seed++)
            {
                host.ReplaceRules(CasinoAdventureSession.Start(seed, CasinoAdventureMode.Practice, 1));
                host.Rules.BeginGame("seed-check", CasinoGameKind.Blackjack, 50, 0, "cards-a"); host.Refresh();
                if (!string.IsNullOrEmpty(host.AdventureState.ActiveRoundJson)) { selectedSeed = seed; break; }
            }
            Assert.That(selectedSeed, Is.GreaterThan(0));
            host.ReplaceRules(CasinoAdventureSession.Start(selectedSeed, CasinoAdventureMode.Practice, 1));
            var first = Table(host); first.Apply(JinxCasinoTableAction.ChipAdd, 50, 1);
            Assert.That(first.Apply(JinxCasinoTableAction.Commit, 0, 2).Success, Is.True);
            Assert.That(host.AdventureState.ActiveRoundJson, Is.Not.Empty);
            string round = host.AdventureState.ActiveRoundJson; uint random = host.AdventureState.RandomState;
            Assert.That(first.GetView().GetAvailability(JinxCasinoTableAction.Primary).IsAvailable, Is.True);
            first.Close();
            Assert.That(host.AdventureState.ActiveRoundJson, Is.EqualTo(round)); Assert.That(host.AdventureState.RandomState, Is.EqualTo(random));
            var returned = Table(host); Assert.That(returned.GetView().Presentation.IsComplete, Is.False);
            host.StationId = "cards-b"; var other = Table(host);
            Assert.That(other.GetView().Presentation, Is.Null); Assert.That(other.Apply(JinxCasinoTableAction.Secondary, 0, 1).Success, Is.False);
            host.StationId = "cards-a";
            Assert.That(returned.Apply(JinxCasinoTableAction.Primary, 0, 1).Success, Is.True);
            if (!string.IsNullOrEmpty(host.AdventureState.ActiveRoundJson)) Assert.That(returned.Apply(JinxCasinoTableAction.Secondary, 0, 2).Success, Is.True);
            Assert.That(host.AdventureState.SettledRoundSequence, Is.EqualTo(1)); Assert.That(host.AdventureState.LastStationId, Is.EqualTo("cards-a"));
        }

        [Test]
        public void LeversUseRealWindowAndNpcLeverCannotBePulledBySoloPlayer()
        {
            var host = new RuleHost(CasinoGameKind.CooperativeLevers, "levers-a", 6); var table = Table(host);
            table.Apply(JinxCasinoTableAction.ChipAdd, 50, 1); Assert.That(table.Apply(JinxCasinoTableAction.Commit, 0, 2).Success, Is.True);
            Assert.That(table.GetView().GetAvailability(JinxCasinoTableAction.Primary, 1).IsAvailable, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 1, 3).Success, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 4).Success, Is.True, "错时是游戏内失败计数，不应被装饰性按钮吞掉。");
            Assert.That(table.GetView().Presentation.Score, Is.EqualTo(1));
            host.Advance(300); Assert.That(table.GetView().LeverWindowOpen, Is.True);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 5).Success, Is.True);
            Assert.That(table.GetView().GetAvailability(JinxCasinoTableAction.Primary).IsAvailable, Is.False, "本周期不能重复拉。");
            host.Advance(200);
            Assert.That(host.AdventureState.ActiveRoundJson, Is.Null.Or.Empty); Assert.That(host.AdventureState.LastRoundPayout, Is.EqualTo(200));
            Assert.That(table.GetView().Presentation.IsObjectiveSuccess, Is.True);
        }

        [Test]
        public void LegacyClaimIsSeparateFromPlayingAndPublicProjectionCannotBeCorrupted()
        {
            var host = new RuleHost(CasinoGameKind.CooperativeLevers, "levers-a", 3);
            host.Rules.BeginGame("legacy", CasinoGameKind.CooperativeLevers, 50, 0); host.Refresh();
            var before = host.AdventureState; var table = Table(host);
            Assert.That(table.GetView().NeedsLegacyClaim, Is.True); Assert.That(table.GetView().Presentation, Is.Null);
            Assert.That(table.Apply(JinxCasinoTableAction.Commit, 0, 1).Success, Is.True);
            Assert.That(host.AdventureState.ActiveRoundJson, Is.EqualTo(before.ActiveRoundJson)); Assert.That(host.AdventureState.RandomState, Is.EqualTo(before.RandomState));
            Assert.That(host.AdventureState.Coins, Is.EqualTo(before.Coins)); Assert.That(host.AdventureState.ActiveStationId, Is.EqualTo("levers-a"));
            var view = table.GetView(); view.Presentation.SelectedValues[0] = 1;
            Assert.That(table.GetView().Presentation.SelectedValues[0], Is.Zero);
            var snapshot = host.Rules.ToSnapshotJson(); table.Close(); host.ReplaceRules(CasinoAdventureSession.Restore(snapshot));
            Assert.That(Table(host).GetView().NeedsLegacyClaim, Is.False);
        }

        [Test]
        public void OldSuccessfulRequestCannotBeRetriedAfterLoadingEarlierCheckpoint()
        {
            var host = new RuleHost(CasinoGameKind.Slots, "slots-a", 2); string checkpoint = host.Rules.ToSnapshotJson();
            var table = Table(host); table.Apply(JinxCasinoTableAction.ChipAdd, 50, 1); table.Apply(JinxCasinoTableAction.Commit, 0, 2);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 3).Success, Is.True);
            host.ReplaceRules(CasinoAdventureSession.Restore(checkpoint));
            var before = host.AdventureState;
            Assert.That(table.RetryLastCommand(4).Error, Is.EqualTo("RestoredEarlierState"));
            Assert.That(host.AdventureState.Coins, Is.EqualTo(before.Coins)); Assert.That(host.AdventureState.RandomState, Is.EqualTo(before.RandomState));
        }

        private static JinxCasinoTableSession Table(RuleHost host) => new JinxCasinoTableSession(host, host.StationId, host.StationGame);

        private sealed class RuleHost : IJinxCasinoTableOperations
        {
            public CasinoAdventureSession Rules { get; private set; }
            public CasinoAdventureState AdventureState { get; private set; }
            public string StationId { get; set; }
            public CasinoGameKind StationGame { get; set; }
            public bool IsStationAvailable => true;
            public string AdventureBetRules => Rules.NextBetDescription;
            public string ActiveRoundDescription => Rules.ActiveRoundDescription;
            public RuleHost(CasinoGameKind game, string id, uint seed) { StationGame = game; StationId = id; ReplaceRules(CasinoAdventureSession.Start(seed, CasinoAdventureMode.Practice, 1)); }
            public void Refresh() => AdventureState = Rules.CaptureState();
            public void ReplaceRules(CasinoAdventureSession value) { Rules = value; Refresh(); }
            public void Advance(int milliseconds) { Rules.Advance(milliseconds); Refresh(); }
            public CasinoMiniGamePresentation GetAdventurePresentation() => Rules.GetPresentation();
            public CasinoMiniGameActionDescriptor[] GetAdventureActions() => Rules.GetAvailableActions();
            public CasinoGameDefinition[] GetAvailableAdventureGames() => Rules.GetAvailableGames();
            public CasinoAdventureResult BeginAdventureGame(string id, CasinoGameKind game, long stake, int choice, string station)
            { var value = Rules.BeginGame(id, game, stake, choice, station); Refresh(); return value; }
            public CasinoAdventureResult ActInAdventure(string id, CasinoMiniGameAction action, int value, string station)
            { var result = Rules.Act(id, action, value, station); Refresh(); return result; }
            public CasinoAdventureResult BindAdventureStation(string id, string station, CasinoGameKind game)
            { var result = Rules.BindActiveStation(id, station, game); Refresh(); return result; }
        }
    }
}
