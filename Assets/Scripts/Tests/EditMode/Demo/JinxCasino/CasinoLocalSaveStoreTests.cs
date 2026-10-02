using System;
using System.IO;
using Hotfix.JinxCasino.Adapters.Persistence;
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
        public void CorruptPrimaryRecoversPreviousValidVersion()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1);
            store.Save(1, session);
            long previous = session.State.Coins;
            session.BeginGame("next", CasinoGameKind.CoinFlip, 100, 0);
            store.Save(1, session);
            File.WriteAllText(Path.Combine(directory, "save-1.json"), "{broken}");
            Assert.That(store.GetInfo(1).UsesBackup, Is.True);
            var recovered = store.Load(1);
            Assert.That(recovered.State.Coins, Is.EqualTo(previous));
            recovered.BeginGame("after-recovery", CasinoGameKind.CoinFlip, 100, 0);
            store.Save(1, recovered);
            File.WriteAllText(Path.Combine(directory, "save-1.json"), "{broken-again}");
            Assert.That(store.GetInfo(1).UsesBackup, Is.True);
            Assert.That(store.Load(1).State.Coins, Is.EqualTo(previous), "新保存不能把损坏主文件移入有效备份。");
        }

        [Test]
        public void RepeatedSaveDoesNotReplacePreviousDifferentCheckpoint()
        {
            var session = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1);
            store.Save(1, session);
            long firstCoins = session.State.Coins;
            Assert.That(session.BeginGame("changed", CasinoGameKind.CoinFlip, 100, 0).Success, Is.True);
            store.Save(1, session);
            string backupPath = Path.Combine(directory, "save-1.json.bak");
            string backup = File.ReadAllText(backupPath);
            string primary = File.ReadAllText(Path.Combine(directory, "save-1.json"));
            store.Save(1, session);
            Assert.That(File.ReadAllText(backupPath), Is.EqualTo(backup));
            Assert.That(File.ReadAllText(Path.Combine(directory, "save-1.json")), Is.EqualTo(primary));
            File.WriteAllText(Path.Combine(directory, "save-1.json"), "{broken}");
            Assert.That(store.Load(1).State.Coins, Is.EqualTo(firstCoins));
        }

        [Test]
        public void ChangedPayloadAndEmptySlotsAreReportedWithoutLoadingOtherSlots()
        {
            store.Save(2, CasinoAdventureSession.Start(1, CasinoAdventureMode.Standard, 1));
            string path = Path.Combine(directory, "save-2.json");
            File.WriteAllText(path, File.ReadAllText(path).Replace("\"Checksum\":\"", "\"Checksum\":\"changed"));
            Assert.That(store.GetInfo(2).Error, Is.Not.Empty);
            Assert.Throws<InvalidDataException>(() => store.Load(2));
            Assert.That(store.GetInfo(1).IsEmpty, Is.True);
            Assert.Throws<FileNotFoundException>(() => store.Load(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.GetInfo(4));
        }
    }
}
