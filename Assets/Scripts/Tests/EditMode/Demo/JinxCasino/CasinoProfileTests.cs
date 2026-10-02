using System;
using System.Linq;
using System.Text.RegularExpressions;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /// 永久成长仅从真实领域命令产生的结束局统计，不手工伪造输赢或奖励摘要。
    public sealed class CasinoProfileTests
    {
        [Test]
        public void CatalogDefaultsAndReturnedDataAreIndependent()
        {
            var definitions = CasinoProfileCatalog.Definitions;
            Assert.That(definitions.Count(entry => entry.Kind == CasinoCosmeticKind.Color), Is.EqualTo(5));
            Assert.That(definitions.Count(entry => entry.Kind == CasinoCosmeticKind.Hat), Is.EqualTo(6));
            Assert.That(definitions.Count(entry => entry.Kind == CasinoCosmeticKind.Emote), Is.EqualTo(8));
            Assert.That(definitions.Select(entry => entry.Id).Distinct().Count(), Is.EqualTo(definitions.Length));
            definitions[0].Threshold = 999;
            Assert.That(CasinoProfileCatalog.Find("color_blue").Threshold, Is.Zero);
            var profile = CasinoProfile.Create(); string before = profile.ToJson();
            var detached = profile.Data; detached.UnlockedIds.Clear(); detached.EquippedColor = "unknown";
            Assert.That(profile.ToJson(), Is.EqualTo(before));
            Assert.That(profile.Level, Is.EqualTo(1));
            CollectionAssert.AreEquivalent(new[] { "color_blue", "hat_none", "emote_wave", "title_newcomer" }, profile.Data.UnlockedIds);
        }

        [Test]
        public void PracticeAndUnfinishedRunsDoNotRewardAndFinishedRunIdSurvivesRestore()
        {
            var profile = CasinoProfile.Create(); string before = profile.ToJson();
            var active = CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1);
            Assert.That(profile.RecordFinishedRun(active.State), Is.False);
            var practice = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1);
            Assert.That(practice.ChooseEnding("leave", CasinoAdventureEnding.Withdraw).Success, Is.True);
            Assert.That(profile.RecordFinishedRun(practice.State), Is.False);
            Assert.That(profile.ToJson(), Is.EqualTo(before));
            var ended = FinishedRun();
            Assert.That(profile.RecordFinishedRun(ended), Is.True);
            Assert.That(profile.Data.FinishedRuns, Is.EqualTo(1));
            Assert.That(profile.Data.StandardRuns, Is.EqualTo(1));
            Assert.That(profile.Data.SubmittedBets, Is.EqualTo(1));
            Assert.That(profile.Data.Fame, Is.EqualTo(35));
            Assert.That(profile.Data.BestEndingCoins, Is.EqualTo(ended.Coins));
            string once = profile.ToJson(); var restored = CasinoProfile.Restore(once);
            Assert.That(restored.RecordFinishedRun(ended), Is.False);
            Assert.That(restored.ToJson(), Is.EqualTo(once));
        }

        [Test]
        public void EquipRejectsLockedOrWrongKindAtomicallyAndUnlockedSetPersists()
        {
            var profile = CasinoProfile.Create(); string before = profile.ToJson();
            Assert.That(profile.Equip("color_blue", "hat_crown", "emote_wave"), Is.False);
            Assert.That(profile.Equip(hatId: "color_blue"), Is.False);
            Assert.That(profile.ToJson(), Is.EqualTo(before));
            profile.RecordFinishedRun(FinishedRun());
            Assert.That(profile.Equip("color_pink", "hat_party", "emote_clap", "title_story"), Is.True);
            var restored = CasinoProfile.Restore(profile.ToJson());
            Assert.That(restored.Data.EquippedColor, Is.EqualTo("color_pink"));
            Assert.That(restored.Data.EquippedHat, Is.EqualTo("hat_party"));
            Assert.That(restored.Data.EquippedEmote, Is.EqualTo("emote_clap"));
            Assert.That(restored.Data.EquippedTitle, Is.EqualTo("title_story"));
        }

        [Test]
        public void SuccessfulCommandsAndUniqueEffectsCountActualPrankRescueAndTask()
        {
            var config = new CasinoAdventureConfig { StartingCoins = 10000, Targets = new long[] { 50000 }, StageCount = 1, StageDurationMilliseconds = 60000, EventIntervalMilliseconds = 1000 };
            OnlyEvent(config, "chip_rain");
            var run = CasinoAdventureSession.Start(7, CasinoAdventureMode.Standard, 1, config);
            Assert.That(run.Purchase("buy-bubble-1", "bubble_gun").Success, Is.True);
            Assert.That(run.Purchase("buy-bubble-2", "bubble_gun").Success, Is.True);
            Assert.That(run.Purchase("buy-rescue", "rescue_whistle").Success, Is.True);
            Assert.That(run.UseItem("bubble", "bubble_gun", "buddy").Success, Is.True);
            Assert.That(run.UseItem("blocked", "bubble_gun", "buddy").Error, Is.EqualTo("TargetProtected"));
            Assert.That(run.BeginGame("bet", CasinoGameKind.CoinFlip, 10, 0).Success, Is.True);
            Assert.That(run.BeginGame("bet", CasinoGameKind.CoinFlip, 10, 0).Success, Is.True);
            Assert.That(run.BeginGame("bad-bet", CasinoGameKind.CoinFlip, 1000001, 0).Success, Is.False);
            Assert.That(run.Advance(1000).Success, Is.True);
            var mission = run.State.ActiveMission;
            for (int i = 0; i < mission.TargetCount; i++) Assert.That(run.AdvanceTask("collect-" + i, mission.Id, CasinoTaskAction.Collect, i).Success, Is.True);
            Assert.That(run.Advance(run.State.RemainingMilliseconds).Success, Is.True);
            Assert.That(run.State.Phase, Is.EqualTo(CasinoAdventurePhase.Failed));
            Assert.That(run.UseItem("rescue", "rescue_whistle").Success, Is.True);
            Assert.That(run.ChooseEnding("end", CasinoAdventureEnding.Withdraw).Success, Is.True);
            var profile = CasinoProfile.Create(); Assert.That(profile.RecordFinishedRun(run.State), Is.True);
            var data = profile.Data;
            Assert.That(data.SubmittedBets, Is.EqualTo(1)); Assert.That(data.ItemActions, Is.EqualTo(2));
            Assert.That(data.Pranks, Is.EqualTo(1), "效果同时位于请求与累计列表时只能计一次。");
            Assert.That(data.Tasks, Is.EqualTo(1)); Assert.That(data.Rescues, Is.EqualTo(1));
            Assert.That(data.Fame, Is.EqualTo(16));
            Assert.That(data.DiscoveredGames, Does.Contain(CasinoGameKind.CoinFlip));
            Assert.That(data.DiscoveredItems, Does.Contain("bubble_gun"));
            Assert.That(data.DiscoveredEvents, Does.Contain("chip_rain"));
            Assert.That(data.UnlockedIds, Does.Contain("title_rescuer"));
        }

        [Test]
        public void ShieldProtectedActualEventAndEndlessCompletionAreRecorded()
        {
            var config = new CasinoAdventureConfig { StageCount = 1, Targets = new long[] { 1 }, EventIntervalMilliseconds = 1000 };
            OnlyEvent(config, "bubble_leak");
            var run = CasinoAdventureSession.Start(5, CasinoAdventureMode.Endless, 1, config);
            Assert.That(run.Purchase("shield-buy", "shared_shield").Success, Is.True);
            Assert.That(run.UseItem("shield-use", "shared_shield").Success, Is.True);
            Assert.That(run.Advance(1000).Success, Is.True);
            Assert.That(run.CompleteStage("stage").Success, Is.True);
            Assert.That(run.ChooseEnding("end", CasinoAdventureEnding.Withdraw).Success, Is.True);
            var profile = CasinoProfile.Create(); profile.RecordFinishedRun(run.State);
            Assert.That(profile.Data.EndlessRuns, Is.EqualTo(1));
            Assert.That(profile.Data.ProtectionBlocks, Is.EqualTo(1));
            Assert.That(profile.Data.CompletedStages, Is.EqualTo(1));
            Assert.That(profile.Data.DiscoveredEvents, Does.Contain("bubble_leak"));
        }

        [Test]
        public void FullDiscoveryUnlocksCollectionRewardThroughRealBetsPurchasesEventsAndEndings()
        {
            var profile = CasinoProfile.Create();
            var config = new CasinoAdventureConfig { StartingCoins = 1000000, Targets = new long[] { 1,1,1,1 }, StageDurationMilliseconds = 3600000, EventIntervalMilliseconds = 0 };
            var run = CasinoAdventureSession.Start(19, CasinoAdventureMode.Standard, 1, config);
            for (int area = 0; area < 4; area++)
            {
                foreach (var game in CasinoContentCatalog.Games.Where(entry => entry.AreaIndex == area))
                {
                    Assert.That(run.BeginGame("bet-" + game.Kind, game.Kind, 1, 0).Success, Is.True, game.Name);
                    CompleteRound(run, game.Kind);
                    Assert.That(run.HasActiveRound, Is.False, game.Name);
                }
                if (area == 3) foreach (var item in CasinoContentCatalog.Items) Assert.That(run.Purchase("buy-" + item.Id, item.Id).Success, Is.True);
                Assert.That(run.CompleteStage("stage-" + area).Success, Is.True);
                if (area < 3) Assert.That(run.BeginNextStage("next-" + area).Success, Is.True);
            }
            Assert.That(run.ChooseEnding("end", CasinoAdventureEnding.LeaveWithDignity).Success, Is.True);
            Assert.That(profile.RecordFinishedRun(run.State), Is.True);
            foreach (var entry in CasinoContentCatalog.Events)
            {
                var eventConfig = new CasinoAdventureConfig { EventIntervalMilliseconds = 1 }; OnlyEvent(eventConfig, entry.Id);
                var eventRun = CasinoAdventureSession.Start(31, CasinoAdventureMode.Standard, 1, eventConfig);
                Assert.That(eventRun.Advance(1).Success, Is.True);
                Assert.That(eventRun.State.CurrentEventId, Is.EqualTo(entry.Id));
                Assert.That(eventRun.ChooseEnding("end", CasinoAdventureEnding.Withdraw).Success, Is.True);
                Assert.That(profile.RecordFinishedRun(eventRun.State), Is.True);
            }
            var takeover = CasinoAdventureSession.Start(13, CasinoAdventureMode.Standard, 1,
                new CasinoAdventureConfig { StageCount = 1, Targets = new long[] { 1 }, EventIntervalMilliseconds = 0 });
            Assert.That(takeover.CompleteStage("stage").Success, Is.True);
            Assert.That(takeover.BeginGame("vault", CasinoGameKind.CooperativeVault, 1, 0).Success, Is.True);
            OpenVault(takeover);
            Assert.That(takeover.State.TakeOverUnlocked, Is.True);
            Assert.That(takeover.ChooseEnding("end", CasinoAdventureEnding.TakeOver).Success, Is.True);
            profile.RecordFinishedRun(takeover.State);
            var data = profile.Data;
            Assert.That(data.DiscoveredGames.Count, Is.EqualTo(17)); Assert.That(data.DiscoveredItems.Count, Is.EqualTo(24));
            Assert.That(data.DiscoveredEvents.Count, Is.EqualTo(20)); Assert.That(data.DiscoveredEndings.Count, Is.EqualTo(3));
            Assert.That(profile.Equip("color_violet", "hat_crown", "emote_fireworks", "title_collector"), Is.True);
            Assert.That(CasinoProfile.Restore(profile.ToJson()).Data.EquippedEmote, Is.EqualTo("emote_fireworks"));
        }

        [Test]
        public void RestoreRejectsTamperedTotalsDuplicateRunAndLockedEquipment()
        {
            var profile = CasinoProfile.Create(); profile.RecordFinishedRun(FinishedRun());
            var value = profile.Data; value.Fame++;
            Assert.Throws<ArgumentException>(() => CasinoProfile.Restore(JsonUtility.ToJson(value)));
            value = profile.Data; value.FinishedRunRecords.Add(value.FinishedRunRecords[0]);
            Assert.Throws<ArgumentException>(() => CasinoProfile.Restore(JsonUtility.ToJson(value)));
            value = profile.Data; value.EquippedHat = "hat_crown";
            Assert.Throws<ArgumentException>(() => CasinoProfile.Restore(JsonUtility.ToJson(value)));
            value = profile.Data; value.DiscoveredEvents.Add("unknown-event");
            Assert.Throws<ArgumentException>(() => CasinoProfile.Restore(JsonUtility.ToJson(value)));
            Assert.Throws<ArgumentException>(() => CasinoProfile.Restore("{}"));
        }

        internal static CasinoAdventureState FinishedRun(uint seed = 1)
        {
            var run = CasinoAdventureSession.Start(seed, CasinoAdventureMode.Standard, 1,
                new CasinoAdventureConfig { StageCount = 1, Targets = new long[] { 1 }, EventIntervalMilliseconds = 0 });
            Assert.That(run.BeginGame("bet", CasinoGameKind.CoinFlip, 10, 0).Success, Is.True);
            Assert.That(run.CompleteStage("stage").Success, Is.True);
            Assert.That(run.ChooseEnding("end", CasinoAdventureEnding.LeaveWithDignity).Success, Is.True); return run.State;
        }
        private static void OnlyEvent(CasinoAdventureConfig config, string id)
            => config.EventWeights = CasinoContentCatalog.Events.Select(entry => new CasinoEventWeight { EventId = entry.Id, Weight = entry.Id == id ? 1 : 0 }).ToArray();
        private static void CompleteRound(CasinoAdventureSession run, CasinoGameKind game)
        {
            string prefix = "act-" + game;
            switch (game)
            {
                case CasinoGameKind.Blackjack: run.Act(prefix, CasinoMiniGameAction.Stand); break;
                case CasinoGameKind.HighLow:
                    run.Act(prefix, CasinoMiniGameAction.GuessHigher); if (run.HasActiveRound) run.Act(prefix + "-stop", CasinoMiniGameAction.CashOut); break;
                case CasinoGameKind.LuckyDraw: run.Act(prefix, CasinoMiniGameAction.PickPrize, 0); break;
                case CasinoGameKind.Bingo: run.Act(prefix, CasinoMiniGameAction.SelectNumber, 1); run.Act(prefix + "-draw", CasinoMiniGameAction.DrawNumber); run.Advance(10000); break;
                case CasinoGameKind.Plinko: run.Act(prefix, CasinoMiniGameAction.DropBall, 0); run.Advance(800); break;
                case CasinoGameKind.CooperativeLevers: run.Advance(300); run.Act(prefix, CasinoMiniGameAction.PullLever, 0); run.Advance(200); break;
                case CasinoGameKind.PushYourLuckDice: run.Act(prefix, CasinoMiniGameAction.RollDice); if (run.HasActiveRound) run.Act(prefix + "-stop", CasinoMiniGameAction.CashOut); break;
                case CasinoGameKind.PassingBag: run.Advance(9000); break;
                case CasinoGameKind.BlindAuction: run.Act(prefix, CasinoMiniGameAction.CashOut); break;
                case CasinoGameKind.MechanicalRace: run.Advance(30000); break;
                case CasinoGameKind.CooperativeVault: OpenVault(run); break;
                case CasinoGameKind.ChickenElevator: run.Act(prefix, CasinoMiniGameAction.Climb, 0); if (run.HasActiveRound) run.Act(prefix + "-stop", CasinoMiniGameAction.CashOut); break;
            }
        }
        private static void OpenVault(CasinoAdventureSession run)
        {
            for (int i = 0; i < 3; i++) Assert.That(run.Act("clue-" + i, CasinoMiniGameAction.InspectClue, i).Success, Is.True);
            var match = Regex.Match(run.ActiveRoundDescription, "互补线索：([1-9]{3})"); Assert.That(match.Success, Is.True);
            Assert.That(run.Act("password", CasinoMiniGameAction.EnterCode, int.Parse(match.Groups[1].Value)).Success, Is.True);
        }
    }
}
