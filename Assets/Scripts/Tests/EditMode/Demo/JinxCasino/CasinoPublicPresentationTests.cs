using System;
using System.Linq;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /// 公开表现协议回归；秘密信息只能经游戏内授权揭示，快照属于领域恢复而非展示数据。
    public sealed class CasinoPublicPresentationTests
    {
        /// <summary>牌类局只能公开当前可见牌，透视必须显式授权。</summary>
        /// <param name="game">二十一点或比大小。</param>
        [TestCase(CasinoGameKind.Blackjack)]
        [TestCase(CasinoGameKind.HighLow)]
        public void UnrevealedCardsStayHiddenUntilXrayWithoutChangingOriginalRound(CasinoGameKind game)
        {
            var round = CreateActiveRound(game);
            string before = round.ToSnapshotJson();
            var hidden = round.GetPresentation();
            var revealedCopy = CasinoMiniGameRound.Restore(before);
            Assert.That(revealedCopy.ApplyRuleItem("xray"), Is.True);
            var revealed = revealedCopy.GetPresentation();
            if (game == CasinoGameKind.Blackjack)
            {
                Assert.That(hidden.NumberValues.Length, Is.EqualTo(2));
                Assert.That(hidden.SecondaryValues.Length, Is.EqualTo(1), "未透视只展示庄家的明牌");
                Assert.That(revealed.SecondaryValues.Length, Is.EqualTo(2));
                Assert.That(hidden.SecondaryValues[0], Is.EqualTo(revealed.SecondaryValues[0]));
            }
            else
            {
                Assert.That(hidden.NumberValues.Length, Is.EqualTo(1), "下一张牌不能用于猜大小前的公开表现");
                Assert.That(revealed.NumberValues.Length, Is.EqualTo(2));
                Assert.That(hidden.NumberValues[0], Is.EqualTo(revealed.NumberValues[0]));
            }
            Assert.That(round.ToSnapshotJson(), Is.EqualTo(before), "查看公开数据或授权复制局不能改变原局随机/操作状态");
            string publicJson = JsonUtility.ToJson(hidden);
            Assert.That(publicJson, Does.Not.Contain("\"Deck\""));
            Assert.That(publicJson, Does.Not.Contain("\"RandomState\""));
            Assert.That(publicJson, Does.Not.Contain("\"Target\""));
        }

        [Test]
        public void VaultOnlyExposesInspectedDigitAndRestoresItsRemainingSecrets()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeVault, 73, 100, 0, playerCount: 3);
            Assert.That(round.GetPresentation().NumberValues, Is.EqualTo(new[] { 0, 0, 0 }));
            Assert.That(round.TryAct(CasinoMiniGameAction.InspectClue, 1), Is.True);
            string snapshot = round.ToSnapshotJson();
            var view = round.GetPresentation();
            Assert.That(view.NumberValues[0], Is.Zero); Assert.That(view.NumberValues[1], Is.InRange(1, 9)); Assert.That(view.NumberValues[2], Is.Zero);
            Assert.That(view.SelectedValues, Is.EqualTo(new[] { 0, 1, 0 }));
            Assert.That(round.Description, Does.Contain("互补线索：?" + view.NumberValues[1] + "?"));
            var restored = CasinoMiniGameRound.Restore(snapshot);
            Assert.That(restored.GetPresentation().NumberValues, Is.EqualTo(view.NumberValues));
            restored.Advance(500);
            Assert.That(restored.GetPresentation().NumberValues, Is.EqualTo(view.NumberValues), "多人局不因表现刷新自动揭示额外密码位");
            var authorized = CasinoMiniGameRound.Restore(snapshot);
            Assert.That(authorized.ApplyRuleItem("xray"), Is.True);
            Assert.That(authorized.GetPresentation().NumberValues.All(value => value >= 1 && value <= 9), Is.True);
            Assert.That(round.ToSnapshotJson(), Is.EqualTo(snapshot));
        }

        /// <summary>透视只揭示本玩法允许公开的奖值。</summary>
        /// <param name="game">存在隐藏奖值的签筒或盲拍。</param>
        /// <param name="revealedCount">授权后应公开的奖值个数。</param>
        [TestCase(CasinoGameKind.LuckyDraw, 6)]
        [TestCase(CasinoGameKind.BlindAuction, 1)]
        public void HiddenPrizeArraysRequireAuthorizedXray(CasinoGameKind game, int revealedCount)
        {
            var round = CasinoMiniGameRound.Create(game, 74, 100, 0);
            Assert.That(round.GetPresentation().NumberValues, Is.Empty);
            Assert.That(round.GetPresentation().SecondaryValues, Is.Empty);
            var restored = CasinoMiniGameRound.Restore(round.ToSnapshotJson());
            Assert.That(restored.ApplyRuleItem("xray"), Is.True);
            Assert.That(restored.GetPresentation().NumberValues.Length, Is.EqualTo(revealedCount), "盲拍透视仅能揭示暗奖，不能揭示竞争对手报价");
            Assert.That(round.GetPresentation().NumberValues, Is.Empty);
        }

        [Test]
        public void ElevatorChoiceCannotReadWhetherEitherLiftWillCrash()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.ChickenElevator, 78, 100, 0);
            Assert.That(round.GetPresentation().NumberValues, Is.Empty);
            Assert.That(round.GetPresentation().SecondaryValues, Is.Empty);
            var restored = CasinoMiniGameRound.Restore(round.ToSnapshotJson());
            Assert.That(restored.GetPresentation().NumberValues, Is.Empty);
            Assert.That(restored.TryAct(CasinoMiniGameAction.Climb, 1), Is.EqualTo(round.TryAct(CasinoMiniGameAction.Climb, 1)));
            Assert.That(restored.ToSnapshotJson(), Is.EqualTo(round.ToSnapshotJson()));
            if (!round.IsComplete) Assert.That(round.GetPresentation().NumberValues, Is.Empty, "选择下一层前也不能看到预生成事故");
        }

        /// <summary>数组持有者不能通过表现副本修改领域局。</summary>
        /// <param name="game">覆盖手牌、选中号码、赛事和密码三种数组来源的玩法。</param>
        [TestCase(CasinoGameKind.Blackjack)]
        [TestCase(CasinoGameKind.Bingo)]
        [TestCase(CasinoGameKind.MechanicalRace)]
        [TestCase(CasinoGameKind.CooperativeVault)]
        public void PublicArraysAreDeepCopiesAndPollingCannotAdvanceRules(CasinoGameKind game)
        {
            var round = CreateActiveRound(game);
            if (game == CasinoGameKind.Bingo) Assert.That(round.TryAct(CasinoMiniGameAction.SelectNumber, 7), Is.True);
            if (game == CasinoGameKind.CooperativeVault) Assert.That(round.TryAct(CasinoMiniGameAction.InspectClue, 0), Is.True);
            string before = round.ToSnapshotJson();
            var first = round.GetPresentation(); var second = round.GetPresentation();
            if (first.NumberValues.Length > 0) { Assert.That(second.NumberValues, Is.Not.SameAs(first.NumberValues)); first.NumberValues[0] = int.MaxValue; }
            if (first.SecondaryValues.Length > 0) { Assert.That(second.SecondaryValues, Is.Not.SameAs(first.SecondaryValues)); first.SecondaryValues[0] = int.MaxValue; }
            if (first.SelectedValues.Length > 0) { Assert.That(second.SelectedValues, Is.Not.SameAs(first.SelectedValues)); first.SelectedValues[0] = int.MaxValue; }
            first.Cost = first.Payout = long.MaxValue; first.OperationCount = int.MaxValue;
            for (int index = 0; index < 40; index++) round.GetPresentation();
            Assert.That(round.ToSnapshotJson(), Is.EqualTo(before));
            Assert.That(round.GetPresentation().NumberValues, Is.EqualTo(second.NumberValues));
            Assert.That(round.GetPresentation().SecondaryValues, Is.EqualTo(second.SecondaryValues));
            Assert.That(round.GetPresentation().SelectedValues, Is.EqualTo(second.SelectedValues));
        }

        [Test]
        public void CompletedAdventurePresentationRestoresWithoutRepayingOrRepeatingSequence()
        {
            var session = CasinoAdventureSession.Start(81, CasinoAdventureMode.Practice, 1, CasinoAdventureTests.QuietConfig());
            Assert.That(session.GetPresentation(), Is.Null);
            Assert.That(session.BeginGame("vault-bet", CasinoGameKind.CooperativeVault, 100, 0).Success, Is.True);
            for (int digit = 0; digit < 3; digit++) Assert.That(session.Act("clue-" + digit, CasinoMiniGameAction.InspectClue, digit).Success, Is.True);
            var digits = session.GetPresentation().NumberValues;
            int code = digits[0] * 100 + digits[1] * 10 + digits[2];
            Assert.That(session.Act("unlock", CasinoMiniGameAction.EnterCode, code).Success, Is.True);
            Assert.That(session.State.Coins, Is.EqualTo(1200)); Assert.That(session.State.SettledRoundSequence, Is.EqualTo(1));
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            var view = restored.GetPresentation(); Assert.That(view.IsComplete, Is.True);
            Assert.That(view.Game, Is.EqualTo(CasinoGameKind.CooperativeVault)); Assert.That(view.Payout, Is.EqualTo(300));
            Assert.That(view.IsObjectiveSuccess, Is.True);
            Assert.That(restored.BeginGame("vault-bet", CasinoGameKind.CooperativeVault, 100, 0).Changed, Is.False);
            Assert.That(restored.Act("unlock", CasinoMiniGameAction.EnterCode, code).Changed, Is.False);
            Assert.That(restored.Advance(10000).Success, Is.True);
            Assert.That(restored.State.Coins, Is.EqualTo(1200)); Assert.That(restored.State.SettledRoundSequence, Is.EqualTo(1));
            Assert.That(restored.HasActiveRound, Is.False);
            Assert.That(restored.BeginGame("next-bet", CasinoGameKind.CoinFlip, 10, 0).Success, Is.True);
            Assert.That(restored.State.SettledRoundSequence, Is.EqualTo(2));
            string latest = restored.State.LastRoundJson; long balance = restored.State.Coins;
            restored.Act("unlock", CasinoMiniGameAction.EnterCode, code); restored.Advance(1000);
            Assert.That(restored.State.LastRoundJson, Is.EqualTo(latest)); Assert.That(restored.State.Coins, Is.EqualTo(balance));
            Assert.That(restored.State.SettledRoundSequence, Is.EqualTo(2));
        }

        [Test]
        public void CompletedPresentationUsesActualEventAdjustedPayoutBeforeAndAfterRestore()
        {
            var session = CasinoAdventureSession.Start(83, CasinoAdventureMode.Standard, 1, CasinoAdventureTests.SingleEventConfig("crazy_multiplier"));
            Assert.That(session.Purchase("buy-insurance", "stop_loss").Success, Is.True);
            Assert.That(session.UseItem("prepare-insurance", "stop_loss").Success, Is.True);
            Assert.That(session.Advance(1000).Success, Is.True); Assert.That(session.State.EventPayoutBonusPercent, Is.EqualTo(50));
            long balance = session.State.Coins;
            Assert.That(session.BeginGame("bet", CasinoGameKind.CoinFlip, 100, 0).Success, Is.True);
            var underlying = CasinoMiniGameRound.Restore(session.State.LastRoundJson);
            Assert.That(underlying.Payout, Is.GreaterThan(0), "保险使固定验证不依赖随机输赢");
            long actual = underlying.Payout + underlying.Payout / 2;
            Assert.That(session.State.LastRoundPayout, Is.EqualTo(actual));
            Assert.That(session.GetPresentation().Payout, Is.EqualTo(actual));
            Assert.That(session.GetPresentation().Cost, Is.EqualTo(100)); Assert.That(session.State.Coins, Is.EqualTo(balance - 100 + actual));
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.GetPresentation().Payout, Is.EqualTo(actual));
            long restoredBalance = restored.State.Coins;
            Assert.That(restored.BeginGame("bet", CasinoGameKind.CoinFlip, 100, 0).Changed, Is.False);
            Assert.That(restored.State.SettledRoundSequence, Is.EqualTo(1)); Assert.That(restored.State.Coins, Is.EqualTo(restoredBalance));
            var external = restored.GetPresentation(); external.Payout = long.MaxValue; external.NumberValues[0] = int.MaxValue;
            Assert.That(restored.GetPresentation().Payout, Is.EqualTo(actual));
            Assert.That(restored.State.Coins, Is.EqualTo(restoredBalance));
        }

        [Test]
        public void InsuranceProfitCannotPresentFailedVaultAsUnlockedAfterRestore()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.CooperativeVault, 86, 100, 0,
                preparedItems: new[] { "stop_loss", "jackpot_coupon" });
            round.Advance(20000);
            Assert.That(round.IsComplete, Is.True); Assert.That(round.Payout, Is.EqualTo(150));
            var view = round.GetPresentation();
            Assert.That(view.Payout, Is.GreaterThan(view.Cost)); Assert.That(view.IsObjectiveSuccess, Is.False);
            var restored = CasinoMiniGameRound.Restore(round.ToSnapshotJson());
            Assert.That(restored.GetPresentation().IsObjectiveSuccess, Is.False, "获得保险补偿的闭锁金库仍应演出失败");
        }

        private static CasinoMiniGameRound CreateActiveRound(CasinoGameKind game)
        {
            // 21点天然牌可能瞬时结算，明确寻找活动局才能断言其暗牌边界。
            for (uint seed = 1; seed <= 100; seed++)
            {
                var round = CasinoMiniGameRound.Create(game, seed, 100, 0);
                if (!round.IsComplete) return round;
            }
            Assert.Fail("没有找到用于公开表现验证的活动局：" + game); return null;
        }
    }
}
