using System;
using System.IO;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class CasinoTutorialSaveStoreTests
    {
        [Test]
        public void ThreeSlotsCaptureTeachingAndPaidRoundTogetherWithoutCrossSlotOrRetryPayment()
        {
            string directory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TutorialSaveTests", Guid.NewGuid().ToString("N")));
            try
            {
                var store = new CasinoLocalSaveStore(directory); var session = CasinoTutorialTests.NewPractice(3);
                CasinoTutorialTests.ReachSlotsResult(session); store.Save(1, session);
                session.BeginGame("slots", CasinoGameKind.Slots, 10, 0, CasinoAdventureSession.TutorialSlotsStation); store.Save(2, session);
                CasinoTutorialTests.ReportResult(session, CasinoAdventureSession.TutorialSlotsStation); store.Save(3, session);
                Assert.That(store.Load(1).State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.AddChips));
                var paid = store.Load(2); long coins = paid.State.Coins; uint random = paid.State.RandomState;
                Assert.That(paid.State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.SlotsResult));
                Assert.That(paid.BeginGame("slots", CasinoGameKind.Slots, 10, 0, CasinoAdventureSession.TutorialSlotsStation).Changed, Is.False);
                Assert.That(paid.State.Coins, Is.EqualTo(coins)); Assert.That(paid.State.RandomState, Is.EqualTo(random));
                Assert.That(store.Load(3).State.Teaching.Step, Is.EqualTo(CasinoTutorialStep.LeaveSlots));
            }
            finally { RemoveOwnedDirectory(directory); }
        }
        private static void RemoveOwnedDirectory(string directory)
        {
            string allowed = Path.GetFullPath("Library/JinxCasino/TutorialSaveTests") + Path.DirectorySeparatorChar;
            Assert.That(directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Guid.TryParseExact(Path.GetFileName(directory), "N", out _), Is.True);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
