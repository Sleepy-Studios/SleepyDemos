using System;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class CasinoTutorialTests
    {
        [Test]
        public void StartRequiresAvailableSoloPracticeAndObservationsNeverWritePerFrameRequests()
        {
            Assert.That(CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1).StartTutorial("start").Success, Is.False);
            var active = NewPractice(1); active.BeginGame("busy", CasinoGameKind.CooperativeLevers, 10, 0, CasinoAdventureSession.TutorialLeversStation);
            Assert.That(active.StartTutorial("start").Success, Is.False);
            var unavailableConfig = new CasinoAdventureConfig { AllowedGames = new[] { CasinoGameKind.Slots } };
            Assert.That(CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1, unavailableConfig).StartTutorial("start").Error, Is.EqualTo("TutorialUnavailable"));
            var session = NewPractice(1); Assert.That(session.StartTutorial("start").Success, Is.True);
            var before = session.State;
            Assert.That(session.StartTutorial("start").Changed, Is.False);
            for (int i = 0; i < 50; i++) session.ObserveTutorial(CasinoTutorialFact.LookApplied, 5000);
            Assert.That(session.State.ProcessedRequests.Count, Is.EqualTo(before.ProcessedRequests.Count));
            Assert.That(session.State.Revision, Is.EqualTo(before.Revision + 1));
            Assert.That(session.State.RandomState, Is.EqualTo(before.RandomState)); Assert.That(session.State.Coins, Is.EqualTo(before.Coins));
            session.ObserveTutorial(CasinoTutorialFact.LookApplied, 15000);
            session.ObserveTutorial(CasinoTutorialFact.WalkApplied, 600, "wrong-station");
            Assert.That(session.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.Walk));
            session.ObserveTutorial(CasinoTutorialFact.WalkApplied, 600, CasinoAdventureSession.TutorialSlotsStation);
            Assert.That(session.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.EnterSlots));
        }

        [Test]
        public void RealThreeGameFlowPaysNormallyAndRequiresExplicitReadyCompletion()
        {
            var session = NewPractice(3); ReachSlotsResult(session);
            long money = session.State.Coins;
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.RoundPresented, 1, CasinoAdventureSession.TutorialSlotsStation).Success, Is.False);
            Assert.That(session.BeginGame("slots", CasinoGameKind.Slots, 10, 0, CasinoAdventureSession.TutorialSlotsStation).Success, Is.True);
            Assert.That(session.State.Coins, Is.EqualTo(money - 10 + session.State.LastRoundPayout));
            ReportResult(session, CasinoAdventureSession.TutorialSlotsStation);
            int revision = session.State.Revision;
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.RoundPresented, session.State.SettledRoundSequence, CasinoAdventureSession.TutorialSlotsStation).Changed, Is.False);
            Assert.That(session.State.Revision, Is.EqualTo(revision));
            session.ObserveTutorial(CasinoTutorialFact.TableLeft, 1, CasinoAdventureSession.TutorialSlotsStation);
            Assert.That(session.BeginGame("cards", CasinoGameKind.Blackjack, 10, 0, CasinoAdventureSession.TutorialBlackjackStation).Success, Is.True);
            if (session.HasActiveRound) Assert.That(session.Act("stand", CasinoMiniGameAction.Stand, 0, CasinoAdventureSession.TutorialBlackjackStation).Success, Is.True);
            ReportResult(session, CasinoAdventureSession.TutorialBlackjackStation);
            Assert.That(session.Purchase("wrench", "duo_wrench").Success, Is.True);
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.ItemPurchased, 1).Changed, Is.True);
            Assert.That(session.UseItem("use", "duo_wrench", "local").Success, Is.True);
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.ItemUsed, 1).Changed, Is.True);
            Assert.That(session.BeginGame("levers", CasinoGameKind.CooperativeLevers, 10, 0, CasinoAdventureSession.TutorialLeversStation).Success, Is.True);
            session.Advance(300);
            Assert.That(session.Act("pull", CasinoMiniGameAction.PullLever, 0, CasinoAdventureSession.TutorialLeversStation).Success, Is.True);
            session.Advance(200); Assert.That(session.HasActiveRound, Is.False);
            ReportResult(session, CasinoAdventureSession.TutorialLeversStation);
            Assert.That(session.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.Ready));
            Assert.That(session.State.Teaching.Status, Is.EqualTo(CasinoTutorialStatus.Active));
            var before = session.State;
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.Continue, 1).Changed, Is.True);
            Assert.That(session.State.Teaching.Status, Is.EqualTo(CasinoTutorialStatus.Completed));
            Assert.That(session.State.Teaching.CompletedGameMask, Is.EqualTo(7));
            Assert.That(session.State.Coins, Is.EqualTo(before.Coins)); Assert.That(session.State.RandomState, Is.EqualTo(before.RandomState));
        }

        [Test]
        public void RestoreUnpaidDraftReturnsToAddChipsButSettledResultNeverReawards()
        {
            var session = NewPractice(3); ReachSlotsResult(session); var unpaid = session.State;
            var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.AddChips));
            Assert.That(restored.State.Coins, Is.EqualTo(unpaid.Coins)); Assert.That(restored.State.RandomState, Is.EqualTo(unpaid.RandomState));
            session.BeginGame("slots", CasinoGameKind.Slots, 10, 0, CasinoAdventureSession.TutorialSlotsStation);
            var paid = session.State; restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.SlotsResult));
            Assert.That(restored.BeginGame("slots", CasinoGameKind.Slots, 10, 0, CasinoAdventureSession.TutorialSlotsStation).Changed, Is.False);
            Assert.That(restored.State.Coins, Is.EqualTo(paid.Coins)); Assert.That(restored.State.RandomState, Is.EqualTo(paid.RandomState));
            ReportResult(restored, CasinoAdventureSession.TutorialSlotsStation);
        }

        [Test]
        public void OldPurchaseDoesNotCountAndReceiptRevisionSurvivesOldRecordPruning()
        {
            var session = NewPractice(3); ReachSlotsResult(session); session.Purchase("old-purchase", "duo_wrench"); ReachBuyWrench(session);
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.ItemPurchased, 1).Success, Is.False);
            var snapshot = session.State; snapshot.ProcessedRequests.Clear();
            session = CasinoAdventureSession.Restore(JsonUtility.ToJson(snapshot));
            Assert.That(session.Purchase("new-purchase", "duo_wrench").Success, Is.True);
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.ItemPurchased, 1).Changed, Is.True);
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.ItemUsed, 1).Success, Is.False);
            Assert.That(session.UseItem("use", "duo_wrench", "local").Success, Is.True);
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.ItemUsed, 1).Changed, Is.True);
        }

        [Test]
        public void ActiveLeverRestoreAndSkippingKeepOriginalCommittedRound()
        {
            var session = NewPractice(3); ReachBuyWrench(session);
            session.Purchase("wrench", "duo_wrench"); session.ObserveTutorial(CasinoTutorialFact.ItemPurchased, 1);
            session.UseItem("use", "duo_wrench", "local"); session.ObserveTutorial(CasinoTutorialFact.ItemUsed, 1);
            session.BeginGame("levers", CasinoGameKind.CooperativeLevers, 10, 0, CasinoAdventureSession.TutorialLeversStation); session.Advance(100);
            var before = session.State; var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(restored.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.Levers));
            Assert.That(restored.State.ActiveRoundJson, Is.EqualTo(before.ActiveRoundJson));
            Assert.That(restored.SkipTutorial("skip").Success, Is.True);
            Assert.That(restored.SkipTutorial("skip").Changed, Is.False);
            Assert.That(restored.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.Skipped));
            Assert.That(restored.State.ActiveRoundJson, Is.EqualTo(before.ActiveRoundJson)); Assert.That(restored.State.LockedCoins, Is.EqualTo(before.LockedCoins));
            Assert.That(restored.State.Coins, Is.EqualTo(before.Coins)); Assert.That(restored.State.RandomState, Is.EqualTo(before.RandomState));
        }

        [Test]
        public void ActiveBlackjackRestoresAtOwnTableAndCompletesWithoutAnotherBegin()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                var session = NewPractice(seed); ReachSlotsResult(session);
                session.BeginGame("slots", CasinoGameKind.Slots, 10, 0, CasinoAdventureSession.TutorialSlotsStation); ReportResult(session, CasinoAdventureSession.TutorialSlotsStation);
                session.ObserveTutorial(CasinoTutorialFact.TableLeft, 1, CasinoAdventureSession.TutorialSlotsStation);
                session.BeginGame("cards", CasinoGameKind.Blackjack, 10, 0, CasinoAdventureSession.TutorialBlackjackStation);
                if (!session.HasActiveRound) continue;
                var before = session.State; var restored = CasinoAdventureSession.Restore(session.ToSnapshotJson());
                Assert.That(restored.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.Blackjack));
                Assert.That(restored.State.ActiveStationId, Is.EqualTo(CasinoAdventureSession.TutorialBlackjackStation));
                Assert.That(restored.State.ActiveRoundJson, Is.EqualTo(before.ActiveRoundJson));
                Assert.That(restored.State.Coins, Is.EqualTo(before.Coins)); Assert.That(restored.State.RandomState, Is.EqualTo(before.RandomState));
                Assert.That(restored.Act("stand-after-restore", CasinoMiniGameAction.Stand, 0, CasinoAdventureSession.TutorialBlackjackStation).Success, Is.True);
                ReportResult(restored, CasinoAdventureSession.TutorialBlackjackStation);
                Assert.That(restored.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.BuyWrench)); return;
            }
            Assert.Fail("实际固定种子样本中应有需要主动停牌的活动BJ局。");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void RestoreRejectsUnsupportedSchemaWithoutChangingCurrentRun(int version)
        {
            var session = NewPractice(3); session.BeginGame("active", CasinoGameKind.CooperativeLevers, 10, 0, CasinoAdventureSession.TutorialLeversStation);
            string current = session.ToSnapshotJson();
            var incompatible = session.State; incompatible.SchemaVersion = version;
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(incompatible)));
            Assert.That(session.ToSnapshotJson(), Is.EqualTo(current));
        }

        [Test]
        public void RestoreRequiresExplicitSchemaVersion()
        {
            var session = NewPractice(3);
            string missingVersion = session.ToSnapshotJson().Replace("\"SchemaVersion\":4,", string.Empty);
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(missingVersion));
        }

        [Test]
        public void NaturalBlackjackReallySettledAtCreateDoesNotRequireImpossibleExtraChoice()
        {
            for (uint seed = 1; seed <= 128; seed++)
            {
                var session = NewPractice(seed); ReachSlotsResult(session);
                session.BeginGame("slots", CasinoGameKind.Slots, 10, 0, CasinoAdventureSession.TutorialSlotsStation); ReportResult(session, CasinoAdventureSession.TutorialSlotsStation);
                session.ObserveTutorial(CasinoTutorialFact.TableLeft, 1, CasinoAdventureSession.TutorialSlotsStation);
                session.BeginGame("cards", CasinoGameKind.Blackjack, 10, 0, CasinoAdventureSession.TutorialBlackjackStation);
                if (session.HasActiveRound) continue;
                Assert.That(session.GetPresentation().OperationCount, Is.Zero);
                ReportResult(session, CasinoAdventureSession.TutorialBlackjackStation);
                Assert.That(session.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.BuyWrench));
                return;
            }
            Assert.Fail("实际固定种子样本中应有天然自动结算，不能用假快照代替此分支。");
        }

        [Test]
        public void NpcOnlyTimeoutCannotFinishLeversButActualRetryCan()
        {
            var session = NewPractice(3); ReachBuyWrench(session);
            session.Purchase("wrench", "duo_wrench"); session.ObserveTutorial(CasinoTutorialFact.ItemPurchased, 1);
            session.UseItem("use", "duo_wrench", "local"); session.ObserveTutorial(CasinoTutorialFact.ItemUsed, 1);
            session.BeginGame("timeout", CasinoGameKind.CooperativeLevers, 10, 0, CasinoAdventureSession.TutorialLeversStation); session.Advance(15000);
            Assert.That(session.HasActiveRound, Is.False);
            Assert.That(session.ObserveTutorial(CasinoTutorialFact.RoundPresented, session.State.SettledRoundSequence, CasinoAdventureSession.TutorialLeversStation).Error, Is.EqualTo("TutorialCooperationRequired"));
            Assert.That(session.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.Levers));
            session.BeginGame("retry", CasinoGameKind.CooperativeLevers, 10, 0, CasinoAdventureSession.TutorialLeversStation); session.Advance(300);
            session.Act("pull", CasinoMiniGameAction.PullLever, 0, CasinoAdventureSession.TutorialLeversStation); session.Advance(200);
            ReportResult(session, CasinoAdventureSession.TutorialLeversStation);
            Assert.That(session.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.Ready));
        }

        [Test]
        public void CorruptTeachingEnumsCountersStationAndSkippedPrerequisitesAreRejected()
        {
            var session = NewPractice(3); ReachSlotsResult(session);
            var state = session.State; state.Teaching.LookMillidegrees = -1; Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(state)));
            state = session.State; state.Teaching.Step = (CasinoTutorialStep)999; Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(state)));
            state = session.State; state.Teaching.StationId = "wrong"; Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(state)));
            state = session.State; state.Teaching.CompletedGameMask = 7; Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(state)));
            state = session.State; state.ProcessedRequests[0].Revision = state.Revision + 1; Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(state)));
        }

        [Test]
        public void TutorialMustStartBeforePracticeClockPurchaseOrFirstSettlement()
        {
            var clocked = NewPractice(3); clocked.Advance(1);
            Assert.That(clocked.StartTutorial("late").Error, Is.EqualTo("TutorialFreshRunRequired"));
            var stocked = NewPractice(3); stocked.Purchase("buy", "duo_wrench"); var before = stocked.State;
            Assert.That(stocked.StartTutorial("late").Error, Is.EqualTo("TutorialFreshRunRequired"));
            Assert.That(stocked.State.Coins, Is.EqualTo(before.Coins)); Assert.That(stocked.State.Inventory[0].Count, Is.EqualTo(before.Inventory[0].Count));
            Assert.That(stocked.State.RandomState, Is.EqualTo(before.RandomState)); Assert.That(stocked.State.Teaching.Status, Is.EqualTo(CasinoTutorialStatus.None));
            var played = NewPractice(3); played.BeginGame("spin", CasinoGameKind.Slots, 10, 0, CasinoAdventureSession.TutorialSlotsStation);
            Assert.That(played.StartTutorial("late").Error, Is.EqualTo("TutorialFreshRunRequired"));
            Assert.That(played.State.SettledRoundSequence, Is.EqualTo(1));
        }

        internal static CasinoAdventureSession NewPractice(uint seed)
        {
            var games = new[] { CasinoGameKind.Slots, CasinoGameKind.Blackjack, CasinoGameKind.CooperativeLevers };
            return CasinoAdventureSession.Start(seed, CasinoAdventureMode.Practice, 1, new CasinoAdventureConfig { StageCount = 1, Targets = new long[] { 1200 }, MaximumStake = 100,
                AllowedGames = games, InitiallyAvailableGames = games, ShopItemIds = new[] { "duo_wrench", "redraw_card", "stop_loss" }, EventIntervalMilliseconds = 0 });
        }
        internal static void ReachSlotsResult(CasinoAdventureSession session)
        {
            Assert.That(session.StartTutorial("teach").Success, Is.True);
            session.ObserveTutorial(CasinoTutorialFact.LookApplied, 15000); session.ObserveTutorial(CasinoTutorialFact.WalkApplied, 600, CasinoAdventureSession.TutorialSlotsStation);
            session.ObserveTutorial(CasinoTutorialFact.TableEntered, 1, CasinoAdventureSession.TutorialSlotsStation);
            session.ObserveTutorial(CasinoTutorialFact.ChipsAdded, 10, CasinoAdventureSession.TutorialSlotsStation);
            session.ObserveTutorial(CasinoTutorialFact.ChipsConfirmed, 1, CasinoAdventureSession.TutorialSlotsStation);
            Assert.That(session.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.SlotsResult));
        }
        internal static void ReachBuyWrench(CasinoAdventureSession session)
        {
            if (session.State.Teaching.Status == CasinoTutorialStatus.None) ReachSlotsResult(session);
            Assert.That(session.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.SlotsResult));
            session.BeginGame("slots", CasinoGameKind.Slots, 10, 0, CasinoAdventureSession.TutorialSlotsStation); ReportResult(session, CasinoAdventureSession.TutorialSlotsStation);
            session.ObserveTutorial(CasinoTutorialFact.TableLeft, 1, CasinoAdventureSession.TutorialSlotsStation);
            session.BeginGame("cards", CasinoGameKind.Blackjack, 10, 0, CasinoAdventureSession.TutorialBlackjackStation);
            if (session.HasActiveRound) session.Act("stand", CasinoMiniGameAction.Stand, 0, CasinoAdventureSession.TutorialBlackjackStation);
            ReportResult(session, CasinoAdventureSession.TutorialBlackjackStation);
            Assert.That(session.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.BuyWrench));
        }
        internal static void ReportResult(CasinoAdventureSession session, string station)
        { Assert.That(session.ObserveTutorial(CasinoTutorialFact.RoundPresented, session.State.SettledRoundSequence, station).Changed, Is.True); }
    }
}
