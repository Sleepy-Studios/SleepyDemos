using Hotfix.JinxCasino.Interaction;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Hotfix.JinxCasino;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;

namespace Tests.Demo
{
    /// 使用真实Game和机台测试桌面交互，不把筹码草稿或拉柄演出当第二套开奖。
    public sealed class JinxCasinoTableSessionTests
    {
        [Test]
        public void SlotsRequirePreparationAndRealLeverCommitsExactlyOnce()
        {
            var host = CreateRig(CasinoGameKind.Slots, "slots-a", 2);
            var table = Table(host); var before = host.Game.State;
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 1).Success, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 50, 2).Success, Is.True);
            Assert.That(table.Apply(JinxCasinoTableAction.Commit, 0, 3).Success, Is.True);
            Assert.That(table.GetView().IsSlotsPrepared, Is.True); Assert.That(table.GetView().Presentation, Is.Null);
            Assert.That(host.Game.State.RandomState, Is.EqualTo(before.RandomState)); Assert.That(host.Game.State.Coins, Is.EqualTo(before.Coins));
            Assert.That(host.Game.State.LockedCoins, Is.Zero); Assert.That(host.Game.State.SettledRoundSequence, Is.Zero);
            var result = table.Apply(JinxCasinoTableAction.Primary, 0, 4); Assert.That(result.Success, Is.True);
            var committed = host.Game.State;
            Assert.That(committed.LastStationId, Is.EqualTo("slots-a")); Assert.That(committed.SettledRoundSequence, Is.EqualTo(1));
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 4).Changed, Is.False);
            Assert.That(table.RetryLastCommand(5).RequestId, Is.EqualTo(result.RequestId));
            Assert.That(host.Game.State.Coins, Is.EqualTo(committed.Coins)); Assert.That(host.Game.State.RandomState, Is.EqualTo(committed.RandomState));
            Assert.That(host.Game.State.SettledRoundSequence, Is.EqualTo(1));
            Assert.That(table.GetView().Presentation.Payout, Is.EqualTo(committed.LastRoundPayout));
        }

        [Test]
        public void DenominationsAndSameFrameGateNeverChangeRulesUntilCommit()
        {
            var host = CreateRig(CasinoGameKind.Blackjack, "cards-a", 1); var table = Table(host);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 10, 1).Success, Is.True);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 10, 1).Changed, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 50, 1).Error, Is.EqualTo("FrameBusy"));
            Assert.That(table.GetView().DraftStake, Is.EqualTo(10));
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 50, 2).Success, Is.True);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipAdd, 100, 3).Success, Is.True);
            Assert.That(table.GetView().DraftStake, Is.EqualTo(160));
            Assert.That(table.GetView().GetAvailability(JinxCasinoTableAction.ChipAdd, 7).IsAvailable, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.ChipClear, 0, 4).Success, Is.True);
            Assert.That(table.GetView().DraftStake, Is.Zero); Assert.That(host.Game.State.Coins, Is.EqualTo(1000));
            Assert.That(host.Game.State.ProcessedRequests, Is.Empty);
        }

        [Test]
        public void SlotRulesAmountStageAndWorldGameChangesInvalidatePreparedCommit()
        {
            var host = CreateRig(CasinoGameKind.Slots, "slots-a", 2); var table = Table(host);
            table.Apply(JinxCasinoTableAction.ChipAdd, 50, 1); table.Apply(JinxCasinoTableAction.Commit, 0, 2);
            table.Apply(JinxCasinoTableAction.ChipAdd, 10, 3); Assert.That(table.GetView().IsSlotsPrepared, Is.False);
            table.Apply(JinxCasinoTableAction.Commit, 0, 4);
            Assert.That(host.Game.PurchaseItem("jackpot_coupon", 40).Success, Is.True);
            Assert.That(host.Game.UseItem("jackpot_coupon", "team", 41).Success, Is.True);
            var random = host.Game.State.RandomState;
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 5).Success, Is.False);
            Assert.That(host.Game.State.RandomState, Is.EqualTo(random)); Assert.That(host.Game.State.SettledRoundSequence, Is.Zero);
            table.Apply(JinxCasinoTableAction.Commit, 0, 6); host.Station.Configure(CasinoGameKind.Blackjack, 0, host.Station.transform);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 7).Success, Is.False); Assert.That(table.GetView().IsSlotsPrepared, Is.False);
            host.Station.Configure(CasinoGameKind.Slots, 0, host.Station.transform);
            table.Apply(JinxCasinoTableAction.Commit, 0, 8);
            // 标准局达额推进到购物，准备不能穿过阶段；无隐式偿债或支付。
            var config = new CasinoAdventureConfig { Targets = new long[] { 900, 900, 900, 900 }, EventIntervalMilliseconds = 0 };
            host.Game.StartAdventure(CasinoAdventureMode.Standard, config, 2);
            table.Apply(JinxCasinoTableAction.Commit, 0, 9);
            Assert.That(host.Game.CompleteStage(40).Success, Is.True);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 10).Success, Is.False); Assert.That(table.GetView().IsSlotsPrepared, Is.False);
        }

        [Test]
        public void BlackjackLeavesAndReturnsWithoutRedealingAndOnlySameTableCanAct()
        {
            var host = CreateRig(CasinoGameKind.Blackjack, "cards-a", 1);
            // 以真实规则找确定的非天然21种子，断言必须确有活动手牌，不能靠概率假设。
            uint selectedSeed = 0;
            for (uint seed = 1; seed < 100; seed++)
            {
                host.Game.StartAdventure(CasinoAdventureMode.Practice, new CasinoAdventureConfig(), seed);
                host.Game.BeginGame("seed-check", CasinoGameKind.Blackjack, 50, 0, "cards-a", 1);
                if (!string.IsNullOrEmpty(host.Game.State.ActiveRoundJson)) { selectedSeed = seed; break; }
            }
            Assert.That(selectedSeed, Is.GreaterThan(0));
            host.Game.StartAdventure(CasinoAdventureMode.Practice, new CasinoAdventureConfig(), selectedSeed);
            var first = Table(host); first.Apply(JinxCasinoTableAction.ChipAdd, 50, 1);
            Assert.That(first.Apply(JinxCasinoTableAction.Commit, 0, 2).Success, Is.True);
            Assert.That(host.Game.State.ActiveRoundJson, Is.Not.Empty);
            string round = host.Game.State.ActiveRoundJson; uint random = host.Game.State.RandomState;
            Assert.That(first.GetView().GetAvailability(JinxCasinoTableAction.Primary).IsAvailable, Is.True);
            first.Close();
            Assert.That(host.Game.State.ActiveRoundJson, Is.EqualTo(round)); Assert.That(host.Game.State.RandomState, Is.EqualTo(random));
            var returned = Table(host); Assert.That(returned.GetView().Presentation.IsComplete, Is.False);
            var other = new JinxCasinoTableSession(host.Game, CreateStation(CasinoGameKind.Blackjack, "cards-b"));
            Assert.That(other.GetView().Presentation, Is.Null); Assert.That(other.Apply(JinxCasinoTableAction.Secondary, 0, 1).Success, Is.False);
            Assert.That(returned.Apply(JinxCasinoTableAction.Primary, 0, 3).Success, Is.True);
            if (!string.IsNullOrEmpty(host.Game.State.ActiveRoundJson)) Assert.That(returned.Apply(JinxCasinoTableAction.Secondary, 0, 4).Success, Is.True);
            Assert.That(host.Game.State.SettledRoundSequence, Is.EqualTo(1)); Assert.That(host.Game.State.LastStationId, Is.EqualTo("cards-a"));
        }

        [Test]
        public void LeversUseRealWindowAndNpcLeverCannotBePulledBySoloPlayer()
        {
            var host = CreateRig(CasinoGameKind.CooperativeLevers, "levers-a", 6); var table = Table(host);
            table.Apply(JinxCasinoTableAction.ChipAdd, 50, 1); Assert.That(table.Apply(JinxCasinoTableAction.Commit, 0, 2).Success, Is.True);
            Assert.That(table.GetView().GetAvailability(JinxCasinoTableAction.Primary, 1).IsAvailable, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 1, 3).Success, Is.False);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 4).Success, Is.True, "错时是游戏内失败计数，不应被装饰性按钮吞掉。");
            Assert.That(table.GetView().Presentation.Score, Is.EqualTo(1));
            host.Game.Advance(300); Assert.That(table.GetView().LeverWindowOpen, Is.True);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 5).Success, Is.True);
            Assert.That(table.GetView().GetAvailability(JinxCasinoTableAction.Primary).IsAvailable, Is.False, "本周期不能重复拉。");
            host.Game.Advance(200);
            Assert.That(host.Game.State.ActiveRoundJson, Is.Null.Or.Empty); Assert.That(host.Game.State.LastRoundPayout, Is.EqualTo(200));
            Assert.That(table.GetView().Presentation.IsObjectiveSuccess, Is.True);
        }

        [Test]
        public void PublicProjectionCannotMutatePaidRoundAcrossLeavingAndRestoring()
        {
            var host = CreateRig(CasinoGameKind.CooperativeLevers, "levers-a", 3);
            var table = Table(host);
            table.Apply(JinxCasinoTableAction.ChipAdd, 50, 1);
            Assert.That(table.Apply(JinxCasinoTableAction.Commit, 0, 2).Success, Is.True);
            var before = host.Game.State;
            var view = table.GetView(); view.Presentation.SelectedValues[0] = 1;
            Assert.That(table.GetView().Presentation.SelectedValues[0], Is.Zero);
            var snapshot = JsonUtility.ToJson(host.Game.State); table.Close(); host.Game.InstallAdventure(CasinoAdventureSession.Restore(snapshot), "恢复测试");
            Assert.That(Table(host).GetView().HasOwnActiveRound, Is.True);
            Assert.That(host.Game.State.ActiveRoundJson, Is.EqualTo(before.ActiveRoundJson)); Assert.That(host.Game.State.RandomState, Is.EqualTo(before.RandomState));
            Assert.That(host.Game.State.Coins, Is.EqualTo(before.Coins)); Assert.That(host.Game.State.ActiveStationId, Is.EqualTo("levers-a"));
        }

        [Test]
        public void OldSuccessfulRequestCannotBeRetriedAfterLoadingEarlierCheckpoint()
        {
            var host = CreateRig(CasinoGameKind.Slots, "slots-a", 2); string checkpoint = JsonUtility.ToJson(host.Game.State);
            var table = Table(host); table.Apply(JinxCasinoTableAction.ChipAdd, 50, 1); table.Apply(JinxCasinoTableAction.Commit, 0, 2);
            Assert.That(table.Apply(JinxCasinoTableAction.Primary, 0, 3).Success, Is.True);
            host.Game.InstallAdventure(CasinoAdventureSession.Restore(checkpoint), "恢复较早测试");
            var before = host.Game.State;
            Assert.That(table.RetryLastCommand(4).Error, Is.EqualTo("RestoredEarlierState"));
            Assert.That(host.Game.State.Coins, Is.EqualTo(before.Coins)); Assert.That(host.Game.State.RandomState, Is.EqualTo(before.RandomState));
        }

        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var owner in objects) if (owner != null) UnityEngine.Object.DestroyImmediate(owner);
            objects.Clear();
        }

        private TableFixture CreateRig(CasinoGameKind kind, string id, uint seed)
        {
            var flow = new JinxCasinoGame();
            flow.StartAdventure(CasinoAdventureMode.Practice, new CasinoAdventureConfig(), seed);
            return new TableFixture { Game = flow, Station = CreateStation(kind, id) };
        }

        private JinxCasinoStation CreateStation(CasinoGameKind kind, string id)
        {
            var owner = new GameObject("Table rule fixture"); objects.Add(owner);
            var pose = new GameObject("FocusPose").transform; pose.SetParent(owner.transform);
            var targetOwner = new GameObject("Target"); targetOwner.transform.SetParent(owner.transform);
            var target = targetOwner.AddComponent<JinxCasinoTableTarget>();
            target.Configure("primary", JinxCasinoTableAction.Primary, 0, 0, Array.Empty<Renderer>(), null);
            var station = owner.AddComponent<JinxCasinoStation>();
            station.Configure(kind, 0, owner.transform);
            station.ConfigureTable(id, pose, 48, new[] { target });
            return station;
        }

        private static JinxCasinoTableSession Table(TableFixture fixture) => new JinxCasinoTableSession(fixture.Game, fixture.Station);

        private sealed class TableFixture
        {
            public JinxCasinoGame Game;
            public JinxCasinoStation Station;
        }
    }
}
