using System;
using System.Linq;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;

namespace Tests.Demo
{
    /// 秘密竞价不通过操作下限、公开分数或展示描述泄露对手报价，付款仍以实际成交为准。
    public sealed class CasinoSecretAuctionTests
    {
        [Test]
        public void BidRangeAndAcceptanceDependOnOwnPreviousBidAcrossHiddenOpponentQuotes()
        {
            for (uint seed = 1; seed <= 20; seed++)
            {
                var round = CasinoMiniGameRound.Create(CasinoGameKind.BlindAuction, seed, 100, 0);
                Assert.That(Bid(round).Minimum, Is.EqualTo(1));
                round.Advance(9000); // 对手已经逐秒加价，但自己的合法最低价仍是一筹码。
                var view = round.GetPresentation();
                Assert.That(view.Score, Is.Zero); Assert.That(view.NumberValues, Is.Empty); Assert.That(view.SecondaryValues, Is.Empty);
                Assert.That(Bid(round).Minimum, Is.EqualTo(1)); Assert.That(Bid(round).Maximum, Is.EqualTo(100));
                Assert.That(round.Description, Does.Not.Contain("NPC出价"));
                Assert.That(round.TryAct(CasinoMiniGameAction.Bid, 1), Is.True, "低于未知对手报价也接受合法密封出价");
                Assert.That(round.GetPresentation().Score, Is.EqualTo(1)); Assert.That(Bid(round).Minimum, Is.EqualTo(2));
                string before = round.ToSnapshotJson();
                Assert.That(round.TryAct(CasinoMiniGameAction.Bid, 1), Is.False, "自己的报价必须严格增加");
                Assert.That(round.ToSnapshotJson(), Is.EqualTo(before));
                var restored = CasinoMiniGameRound.Restore(before);
                Assert.That(Bid(restored).Minimum, Is.EqualTo(2)); Assert.That(restored.GetPresentation().Score, Is.EqualTo(1));
                round.Advance(1000); restored.Advance(1000);
                Assert.That(round.IsComplete && restored.IsComplete, Is.True);
                Assert.That(round.Cost, Is.Zero); Assert.That(round.Payout, Is.Zero);
                Assert.That(restored.ToSnapshotJson(), Is.EqualTo(round.ToSnapshotJson()));
            }
        }

        [Test]
        public void XrayShowsPrizeButNeverCompetingBidOrOpponentCeiling()
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.BlindAuction, 42, 100, 0, preparedItems: new[] { "xray" });
            round.Advance(4000);
            var view = round.GetPresentation();
            Assert.That(view.NumberValues.Length, Is.EqualTo(1)); Assert.That(view.NumberValues[0], Is.InRange(50, 200));
            Assert.That(view.Score, Is.Zero); Assert.That(view.SecondaryValues, Is.Empty);
            Assert.That(Bid(round).Minimum, Is.EqualTo(1));
            Assert.That(round.Description, Does.Contain("暗奖精确价值 " + view.NumberValues[0]));
            Assert.That(round.Description, Does.Not.Contain("NPC出价"));
            Assert.That(round.TryAct(CasinoMiniGameAction.Bid, 9), Is.True);
            Assert.That(round.GetPresentation().Score, Is.EqualTo(9)); Assert.That(Bid(round).Minimum, Is.EqualTo(10));
        }

        [Test]
        public void WinningAuctionPaysItsAcceptedPriceOnceAcrossActiveAndCompletedRestore()
        {
            var session = CasinoAdventureSession.Start(43, CasinoAdventureMode.Practice, 1, CasinoAdventureTests.QuietConfig());
            Assert.That(session.BeginGame("auction", CasinoGameKind.BlindAuction, 100, 0).Success, Is.True);
            Assert.That(session.Act("offer", CasinoMiniGameAction.Bid, 90).Success, Is.True);
            Assert.That(session.State.Coins, Is.EqualTo(1000)); Assert.That(session.State.LockedCoins, Is.EqualTo(100));
            Assert.That(session.GetPresentation().Cost, Is.Zero, "未成交不能让演出提前支付预算或报价");
            var active = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(active.Act("offer", CasinoMiniGameAction.Bid, 90).Changed, Is.False);
            Assert.That(active.Advance(10000).Success, Is.True);
            Assert.That(active.State.LastRoundCost, Is.EqualTo(90)); Assert.That(active.State.SettledRoundSequence, Is.EqualTo(1));
            Assert.That(active.State.Coins, Is.EqualTo(1000 - 90 + active.State.LastRoundPayout)); Assert.That(active.State.LockedCoins, Is.Zero);
            long balance = active.State.Coins;
            var completed = CasinoAdventureSession.Restore(active.ToSnapshotJson());
            completed.Advance(10000); completed.BeginGame("auction", CasinoGameKind.BlindAuction, 100, 0); completed.Act("offer", CasinoMiniGameAction.Bid, 90);
            Assert.That(completed.State.Coins, Is.EqualTo(balance)); Assert.That(completed.State.SettledRoundSequence, Is.EqualTo(1));
            Assert.That(completed.GetPresentation().Cost, Is.EqualTo(90)); Assert.That(completed.GetPresentation().Payout, Is.EqualTo(completed.State.LastRoundPayout));
        }

        /// <summary>成交展示不公开对手内部封顶价，失败也保留竞价秘密。</summary>
        /// <param name="win">选择必胜的高价或必败的低价。</param>
        [TestCase(false)]
        [TestCase(true)]
        public void CompletedAuctionPublishesOnlyPrizeWithoutInternalOpponentCeiling(bool win)
        {
            var round = CasinoMiniGameRound.Create(CasinoGameKind.BlindAuction, 46, 100, 0);
            Assert.That(round.TryAct(CasinoMiniGameAction.Bid, win ? 90 : 1), Is.True);
            round.Advance(10000);
            Assert.That(round.IsComplete, Is.True);
            var view = round.GetPresentation();
            Assert.That(view.NumberValues.Length, Is.EqualTo(1), "完成也仅公开暗奖，NPC封顶价不属于机台演出协议");
            Assert.That(view.SecondaryValues, Is.Empty); Assert.That(view.Score, Is.EqualTo(win ? 90 : 1));
            Assert.That(view.Cost, Is.EqualTo(win ? 90 : 0));
            var restored = CasinoMiniGameRound.Restore(round.ToSnapshotJson());
            Assert.That(restored.GetPresentation().NumberValues, Is.EqualTo(view.NumberValues));
        }

        private static CasinoMiniGameActionDescriptor Bid(CasinoMiniGameRound round)
            => round.GetAvailableActions().Single(action => action.Kind == CasinoMiniGameAction.Bid);
    }
}
