using System;
using System.Linq;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class CasinoContentTests
    {
        [Test]
        public void ApprovedContentHasUniqueIdsAndOriginalAreaNames()
        {
            Assert.That(CasinoContentCatalog.Items.Length, Is.EqualTo(24));
            Assert.That(CasinoContentCatalog.Events.Length, Is.EqualTo(20));
            Assert.That(CasinoContentCatalog.Games.Length, Is.EqualTo(17));
            Assert.That(CasinoContentCatalog.Items.Select(item => item.Id).Distinct().Count(), Is.EqualTo(24));
            Assert.That(CasinoContentCatalog.Events.Select(entry => entry.Id).Distinct().Count(), Is.EqualTo(20));
            Assert.That(CasinoContentCatalog.Games.Select(entry => entry.Kind).Distinct().Count(), Is.EqualTo(17));
            Assert.That(CasinoContentCatalog.Areas.Select(area => area.Name), Is.EqualTo(new[] { "街角幸运厅", "霓虹夜市", "机械奇术馆", "空中金库" }));
            Assert.That(CasinoContentCatalog.Items.Count(item => item.IsPrank), Is.EqualTo(8));
            Assert.That(CasinoContentCatalog.Items.Where(item => item.Behavior == CasinoItemBehavior.Rule).Select(item => item.Id),
                Is.EquivalentTo(new[] { "reroll_dice", "redraw_card", "lock_reel", "xray", "stop_loss", "jackpot_coupon" }));
            foreach (var item in CasinoContentCatalog.Items)
            {
                Assert.That(item.Price, Is.GreaterThanOrEqualTo(0));
                Assert.That(item.Description, Is.Not.Empty);
                if (item.Behavior == CasinoItemBehavior.SceneEffect) Assert.That(item.EffectKind, Is.Not.Empty);
                if (item.IsPrank) Assert.That(item.Value, Is.InRange(1, 3000));
            }
        }

        [Test]
        public void DefaultStandardConfigPreservesFourMinutesAndPlayerScaling()
        {
            var solo = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1);
            var six = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 6);
            Assert.That(solo.State.Config.StageCount, Is.EqualTo(4));
            Assert.That(solo.State.Config.Targets, Is.EqualTo(new long[] { 1200, 2000, 3500, 5000 }));
            Assert.That(solo.State.Coins, Is.EqualTo(1000));
            Assert.That(solo.State.RemainingMilliseconds, Is.EqualTo(240000));
            Assert.That(six.CurrentTarget, Is.EqualTo(2700));
            var catalog = CasinoContentCatalog.Items; catalog[0].Price = long.MaxValue;
            Assert.That(CasinoContentCatalog.Items[0].Price, Is.LessThan(long.MaxValue));
        }

        [Test]
        public void EverySceneItemProducesExplicitReplayableHostEffect()
        {
            foreach (var item in CasinoContentCatalog.Items.Where(item => item.Behavior == CasinoItemBehavior.SceneEffect || item.Behavior == CasinoItemBehavior.RevealMap || item.Behavior == CasinoItemBehavior.Shortcut))
            {
                var session = CasinoAdventureSession.Start(12, CasinoAdventureMode.Standard, 1, CasinoAdventureTests.QuietConfig());
                Assert.That(session.Purchase("buy", item.Id).Success, Is.True, item.Id);
                var result = session.UseItem("use", item.Id, "local");
                Assert.That(result.Success, Is.True, item.Id + ":" + result.Description);
                Assert.That(result.Effects.Length, Is.EqualTo(1), item.Id);
                var effect = result.Effects[0];
                Assert.That(effect.EffectKind, Is.EqualTo(item.EffectKind));
                Assert.That(effect.Id, Is.Not.Empty); Assert.That(effect.TargetId, Is.EqualTo("local"));
                Assert.That(session.State.Inventory, Is.Empty);
                var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
                var retry = restored.UseItem("use", item.Id, "local");
                Assert.That(retry.Changed, Is.False);
                Assert.That(retry.Effects[0].Id, Is.EqualTo(effect.Id));
                Assert.That(restored.State.Effects.Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void EveryEventCreatesExecutableRuleChoiceTaskOrSceneRequest()
        {
            foreach (var entry in CasinoContentCatalog.Events)
            {
                var session = CasinoAdventureSession.Start(8, CasinoAdventureMode.Standard, 1, CasinoAdventureTests.SingleEventConfig(entry.Id));
                var result = session.Advance(1000);
                Assert.That(result.Success, Is.True, entry.Id);
                Assert.That(session.State.CurrentEventId, Is.EqualTo(entry.Id));
                switch (entry.Behavior)
                {
                    case CasinoEventBehavior.StakeCap: Assert.That(session.State.EventStakeCap, Is.GreaterThan(0)); break;
                    case CasinoEventBehavior.PayoutBonus:
                    case CasinoEventBehavior.Multiplier: Assert.That(session.State.EventPayoutBonusPercent, Is.GreaterThan(0)); break;
                    case CasinoEventBehavior.DiceBias: Assert.That(session.State.NextDiceBias, Is.EqualTo(6)); break;
                    case CasinoEventBehavior.RotateGames: Assert.That(session.State.GameRotationOffset, Is.GreaterThan(0)); break;
                    case CasinoEventBehavior.TeamChoice:
                        Assert.That(session.GetEventActions(), Has.Length.EqualTo(2));
                        Assert.That(session.ResolveEvent("decline", 0).Success, Is.True); break;
                    case CasinoEventBehavior.Mission:
                    case CasinoEventBehavior.RepairMission:
                        Assert.That(session.State.ActiveMission, Is.Not.Null);
                        Assert.That(session.State.ActiveMission.Progress, Is.Zero); break;
                    case CasinoEventBehavior.SceneEffect: Assert.That(result.Effects.Any(effect => effect.EffectKind == entry.EffectKind), Is.True); break;
                    default: Assert.Fail("事件目录存在未实现的行为：" + entry.Id); break;
                }
            }
        }

        [Test]
        public void TimeShieldRescueAndRelayItemsChangeDomainOnlyWhenApplicable()
        {
            var session = CasinoAdventureSession.Start(8, CasinoAdventureMode.Standard, 1, CasinoAdventureTests.SingleEventConfig("power_relay"));
            session.Purchase("buy-time", "extra_time");
            Assert.That(session.UseItem("time", "extra_time").Success, Is.True);
            Assert.That(session.State.RemainingMilliseconds, Is.EqualTo(270000));
            session.Purchase("buy-battery", "relay_battery");
            Assert.That(session.UseItem("early-battery", "relay_battery").Success, Is.False);
            Assert.That(session.State.Inventory.Find(item => item.ItemId == "relay_battery").Count, Is.EqualTo(1));
            session.Advance(1000);
            Assert.That(session.UseItem("battery", "relay_battery").Success, Is.True);
            Assert.That(session.State.ActiveMission.Progress, Is.EqualTo(1));

            var shield = CasinoAdventureSession.Start(8, CasinoAdventureMode.Standard, 1, CasinoAdventureTests.SingleEventConfig("bubble_leak"));
            shield.Purchase("buy-shield", "shared_shield"); shield.UseItem("shield", "shared_shield");
            var blocked = shield.Advance(1000);
            Assert.That(blocked.Effects.Any(effect => effect.EffectKind == "ShieldBlocked"), Is.True);
            Assert.That(blocked.Effects.Any(effect => effect.EffectKind == "BubbleStorm"), Is.False);
            Assert.That(shield.State.EventShieldCharges, Is.Zero);

            var config = CasinoAdventureTests.QuietConfig(); config.StageDurationMilliseconds = 1;
            var rescue = CasinoAdventureSession.Start(8, CasinoAdventureMode.Standard, 1, config);
            rescue.Purchase("buy-rescue", "rescue_whistle"); rescue.Advance(1);
            Assert.That(rescue.State.Phase, Is.EqualTo(CasinoAdventurePhase.Failed));
            Assert.That(rescue.UseItem("rescue", "rescue_whistle").Success, Is.True);
            Assert.That(rescue.State.RemainingMilliseconds, Is.EqualTo(30000));
            Assert.That(rescue.CurrentTarget, Is.EqualTo(1100));
        }

        [Test]
        public void ExchangeItemsAndRemoteAreAtomicAndDoNotOverwriteOpenTasks()
        {
            var config = CasinoAdventureTests.SingleEventConfig("chip_rain");
            var session = CasinoAdventureSession.Start(20, CasinoAdventureMode.Standard, 1, config);
            session.Purchase("buy-coupon", "mystery_coupon");
            Assert.That(session.UseItem("coupon", "mystery_coupon").Success, Is.True);
            Assert.That(session.State.Inventory.Sum(item => item.Count), Is.EqualTo(1));
            Assert.That(session.State.Inventory.Any(item => item.ItemId == "mystery_coupon"), Is.False);
            session.Purchase("buy-remote", "event_remote");
            Assert.That(session.UseItem("remote", "event_remote").Success, Is.True);
            string taskId = session.State.ActiveMission.Id;
            session.Purchase("buy-remote2", "event_remote");
            Assert.That(session.UseItem("busy-remote", "event_remote").Error, Is.EqualTo("EventBusy"));
            Assert.That(session.State.ActiveMission.Id, Is.EqualTo(taskId));
            Assert.That(session.State.Inventory.Find(item => item.ItemId == "event_remote").Count, Is.EqualTo(1));
        }

        [Test]
        public void ShopFilterAndDisabledEventsAreRespected()
        {
            var config = CasinoAdventureTests.SingleEventConfig("chip_rain");
            config.ShopItemIds = new[] { "extra_time" };
            foreach (var weight in config.EventWeights) weight.Weight = 0;
            var session = CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1, config);
            Assert.That(session.Purchase("no", "bubble_gun").Error, Is.EqualTo("ItemUnavailable"));
            Assert.That(session.Purchase("yes", "extra_time").Success, Is.True);
            session.Advance(1000);
            Assert.That(session.State.CurrentEventId, Is.Null.Or.Empty);
        }

        [Test]
        public void P1GameFilterSurvivesRestoreAndCannotBeBypassedByPracticeOrFinalChallenge()
        {
            var config = CasinoAdventureTests.QuietConfig();
            config.Targets = new long[] { 0 };
            config.AllowedGames = new[] { CasinoGameKind.Slots, CasinoGameKind.Roulette, CasinoGameKind.CoinFlip };
            var session = CasinoAdventureSession.Start(2, CasinoAdventureMode.Practice, 1, config);
            Assert.That(session.GetAvailableGames().Select(game => game.Kind), Is.EquivalentTo(config.AllowedGames));
            Assert.That(session.BeginGame("unassembled", CasinoGameKind.Blackjack, 10, 0).Success, Is.False);
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.BeginGame("other", CasinoGameKind.CooperativeVault, 10, 0).Success, Is.False);
            Assert.That(restored.State.Coins, Is.EqualTo(1000));
            Assert.That(restored.HasActiveRound, Is.False);

            var standard = CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1, config);
            standard.CompleteStage("finale");
            Assert.That(standard.BeginGame("unassembled-challenge", CasinoGameKind.CooperativeVault, 10, 0).Success, Is.False);
            Assert.That(standard.State.TakeOverUnlocked, Is.False);

            config.AllowedGames = new[] { CasinoGameKind.Slots, CasinoGameKind.Slots };
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1, config));
            config.AllowedGames = new[] { (CasinoGameKind)999 };
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1, config));
        }

        [Test]
        public void DefaultInitialGamesKeepFirstAreaUnlocksAfterRestore()
        {
            var session = CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1, CasinoAdventureTests.QuietConfig());
            Assert.That(session.State.Config.InitiallyAvailableGames, Is.Empty);
            string snapshot = session.ToSnapshotJson();
            var restored = CasinoAdventureSession.Restore(snapshot);
            var firstArea = new[] { CasinoGameKind.Slots, CasinoGameKind.Roulette, CasinoGameKind.CoinFlip,
                CasinoGameKind.Blackjack, CasinoGameKind.HighLow, CasinoGameKind.LuckyDraw };
            foreach (var candidate in new[] { session, restored })
            {
                Assert.That(candidate.State.Config.InitiallyAvailableGames, Is.Empty);
                Assert.That(candidate.GetAvailableGames().Select(game => game.Kind), Is.EquivalentTo(firstArea));
                uint random = candidate.State.RandomState;
                Assert.That(candidate.BeginGame("locked-levers", CasinoGameKind.CooperativeLevers, 10, 0).Error, Is.EqualTo("GameLocked"));
                Assert.That(candidate.State.RandomState, Is.EqualTo(random));
                Assert.That(candidate.State.Coins, Is.EqualTo(1000));
                Assert.That(candidate.HasActiveRound, Is.False);
            }
        }

        [Test]
        public void ExplicitInitialS1GamesOpenRealLeverRoundAndSurviveRestore()
        {
            var config = CasinoAdventureTests.QuietConfig();
            config.AllowedGames = new[] { CasinoGameKind.Slots, CasinoGameKind.Blackjack, CasinoGameKind.CooperativeLevers };
            config.InitiallyAvailableGames = (CasinoGameKind[])config.AllowedGames.Clone();
            var session = CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1, config);
            Assert.That(session.State.StageIndex, Is.Zero);
            Assert.That(session.GetAvailableGames().Select(game => game.Kind), Is.EquivalentTo(config.AllowedGames));
            Assert.That(session.BeginGame("initial-levers", CasinoGameKind.CooperativeLevers, 10, 0, "s1-levers").Success, Is.True);
            Assert.That(session.HasActiveRound, Is.True, "起始开放必须允许实际投入，不能仅改变目录展示。");
            var before = session.State;
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.GetAvailableGames().Select(game => game.Kind), Is.EquivalentTo(config.AllowedGames));
            Assert.That(restored.State.ActiveStationId, Is.EqualTo("s1-levers"));
            Assert.That(restored.State.ActiveRoundJson, Is.EqualTo(before.ActiveRoundJson));
            Assert.That(restored.State.LockedCoins, Is.EqualTo(10));
            Assert.That(restored.BeginGame("initial-levers", CasinoGameKind.CooperativeLevers, 10, 0, "s1-levers").Changed, Is.False);
            Assert.That(restored.State.RandomState, Is.EqualTo(before.RandomState));
            Assert.That(restored.State.Coins, Is.EqualTo(before.Coins));
        }

        [Test]
        public void InitialGamesCannotOverrideAllowedGamesInStandardOrPracticeAfterRestore()
        {
            var config = CasinoAdventureTests.QuietConfig();
            config.AllowedGames = new[] { CasinoGameKind.Slots, CasinoGameKind.Blackjack };
            config.InitiallyAvailableGames = new[] { CasinoGameKind.Slots, CasinoGameKind.Blackjack, CasinoGameKind.CooperativeLevers };
            foreach (var mode in new[] { CasinoAdventureMode.Standard, CasinoAdventureMode.Practice })
            {
                var session = CasinoAdventureSession.Restore(CasinoAdventureSession.Start(2, mode, 1, config).ToSnapshotJson());
                Assert.That(session.GetAvailableGames().Select(game => game.Kind), Is.EquivalentTo(config.AllowedGames));
                uint random = session.State.RandomState;
                Assert.That(session.BeginGame("unassembled-levers", CasinoGameKind.CooperativeLevers, 10, 0).Error, Is.EqualTo("GameLocked"));
                Assert.That(session.State.RandomState, Is.EqualTo(random));
                Assert.That(session.State.Coins, Is.EqualTo(1000));
                Assert.That(session.State.LockedCoins, Is.Zero);
                Assert.That(session.HasActiveRound, Is.False);
            }
        }

        [Test]
        public void P1MerchantAwardsOnlyConfiguredUsableItemsAcrossDifferentSeeds()
        {
            var config = CasinoAdventureTests.SingleEventConfig("mystery_merchant");
            config.ShopItemIds = new[] { "bubble_gun", "extra_time", "stop_loss", "redraw_card", "reroll_dice", "xray" };
            config.AllowedGames = new[] { CasinoGameKind.Slots, CasinoGameKind.Roulette, CasinoGameKind.CoinFlip };
            for (uint seed = 1; seed <= 32; seed++)
            {
                var session = CasinoAdventureSession.Start(seed, CasinoAdventureMode.Standard, 1, config);
                session.Advance(1000);
                Assert.That(session.ResolveEvent("buy", 1).Success, Is.True);
                Assert.That(session.State.Coins, Is.EqualTo(920));
                Assert.That(session.State.Inventory, Has.Count.EqualTo(1));
                Assert.That(new[] { "bubble_gun", "extra_time", "stop_loss" }, Does.Contain(session.State.Inventory[0].ItemId));
            }
        }

        [Test]
        public void EmptyMerchantRewardPoolKeepsWalletRandomAndPendingChoice()
        {
            var config = CasinoAdventureTests.SingleEventConfig("mystery_merchant");
            config.ShopItemIds = new[] { "redraw_card", "reroll_dice", "xray" };
            config.AllowedGames = new[] { CasinoGameKind.CoinFlip };
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1, config);
            session.Advance(1000); uint random = session.State.RandomState;
            Assert.That(session.ResolveEvent("buy", 1).Error, Is.EqualTo("NoRewardItem"));
            Assert.That(session.State.Coins, Is.EqualTo(1000));
            Assert.That(session.State.RandomState, Is.EqualTo(random));
            Assert.That(session.State.Inventory, Is.Empty);
            Assert.That(session.State.EventChoicePending, Is.True);
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.ResolveEvent("buy", 1).Error, Is.EqualTo("NoRewardItem"));
            Assert.That(restored.ResolveEvent("decline", 0).Success, Is.True);
        }

        [Test]
        public void EmptyExchangePoolPreservesVoucherAndExistingTradeItem()
        {
            var config = CasinoAdventureTests.QuietConfig(); config.ShopItemIds = new[] { "mystery_coupon" };
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1, config);
            session.Purchase("buy", "mystery_coupon"); uint random = session.State.RandomState;
            Assert.That(session.UseItem("exchange", "mystery_coupon").Error, Is.EqualTo("NoRewardItem"));
            Assert.That(session.State.Inventory.Single().Count, Is.EqualTo(1));
            Assert.That(session.State.RandomState, Is.EqualTo(random));

            var tradeConfig = CasinoAdventureTests.SingleEventConfig("item_exchange"); tradeConfig.ShopItemIds = new[] { "bubble_gun" };
            var trade = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1, tradeConfig);
            trade.Purchase("buy", "bubble_gun"); trade.Advance(1000);
            Assert.That(trade.ResolveEvent("swap", 1, "bubble_gun").Error, Is.EqualTo("NoRewardItem"));
            Assert.That(trade.State.Inventory.Single().ItemId, Is.EqualTo("bubble_gun"));
            Assert.That(trade.State.Inventory.Single().Count, Is.EqualTo(1));
            Assert.That(trade.State.EventChoicePending, Is.True);
        }
    }
}
