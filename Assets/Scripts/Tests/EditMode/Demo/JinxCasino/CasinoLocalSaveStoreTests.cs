using System;
using System.IO;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class CasinoLocalSaveStoreTests
    {
        private string directory;
        private CasinoLocalSaveStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.GetFullPath(Path.Combine("Library/JinxCasino/SaveTests", Guid.NewGuid().ToString("N")));
            store = new CasinoLocalSaveStore(directory);
        }

        [TearDown]
        public void Cleanup()
        {
            string allowed = Path.GetFullPath("Library/JinxCasino/SaveTests") + Path.DirectorySeparatorChar;
            if (directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void ThreeSlotsRemainIndependentAndLoadedRunDoesNotReawardAcceptedBet()
        {
            var first = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1);
            Assert.That(first.BeginGame("one", CasinoGameKind.CoinFlip, 100, 0).Success, Is.True);
            long coins = first.State.Coins;
            store.Save(1, first);
            store.Save(2, CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1));
            Assert.That(store.GetInfo(3).IsEmpty, Is.True);
            var restored = store.Load(1);
            Assert.That(restored.State.Coins, Is.EqualTo(coins));
            restored.BeginGame("one", CasinoGameKind.CoinFlip, 100, 0);
            Assert.That(restored.State.Coins, Is.EqualTo(coins));
            Assert.That(store.Load(2).State.Mode, Is.EqualTo(CasinoAdventureMode.Standard));
        }

        [Test]
        public void CorruptPrimaryIsEmptyAndExplicitSaveStartsFresh()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1);
            store.Save(1, session);
            string path = Path.Combine(directory, "save-1.json");
            File.WriteAllText(path, "{broken}");
            Assert.That(store.GetInfo(1).IsEmpty, Is.True);
            Assert.That(store.Load(1), Is.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo("{broken}"));
            store.Save(1, CasinoAdventureSession.Start(2, CasinoAdventureMode.Standard, 1));
            Assert.That(store.Load(1).State.Mode, Is.EqualTo(CasinoAdventureMode.Standard));
        }

        [Test]
        public void RepeatedSaveKeepsSnapshotAndLeavesNoTemporaryOrBackup()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1);
            store.Save(1, session);
            Assert.That(session.BeginGame("changed", CasinoGameKind.CoinFlip, 100, 0).Success, Is.True);
            store.Save(1, session);
            string primary = Path.Combine(directory, "save-1.json");
            string before = File.ReadAllText(primary);
            store.Save(1, session);
            Assert.That(File.ReadAllText(primary), Is.EqualTo(before));
            Assert.That(File.Exists(primary + ".bak"), Is.False);
            Assert.That(Directory.GetFiles(directory, "*.tmp-*").Length, Is.Zero);
        }

        [Test]
        public void InvalidStructureAndEmptySlotsDoNotLoadOtherSlots()
        {
            store.Save(2, CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1));
            string path = Path.Combine(directory, "save-2.json");
            File.WriteAllText(path, "{\"SchemaVersion\":99}");
            Assert.That(store.GetInfo(2).IsEmpty, Is.True);
            Assert.That(store.Load(2), Is.Null);
            Assert.That(store.GetInfo(1).IsEmpty, Is.True);
            Assert.That(store.Load(1), Is.Null);
            Assert.Throws<ArgumentOutOfRangeException>(() => store.GetInfo(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Save(0, CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1)));
        }

    }
}
