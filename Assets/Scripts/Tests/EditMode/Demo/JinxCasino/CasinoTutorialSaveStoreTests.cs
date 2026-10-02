using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Hotfix.JinxCasino.Adapters.Persistence;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class CasinoTutorialSaveStoreTests
    {
        [TestCase(1)]
        [TestCase(2)]
        public void ReadingOldSchemaNeverWritesAndLaterSaveArchivesExactSourceVersion(int version)
        {
            string directory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TutorialSaveTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                var oldSession = CasinoTutorialTests.NewPractice(3);
                oldSession.BeginGame("old", CasinoGameKind.CooperativeLevers, 10, 0, CasinoAdventureSession.TutorialLeversStation);
                var old = oldSession.State; old.SchemaVersion = version; old.Teaching = null;
                foreach (var receipt in old.ProcessedRequests) receipt.Revision = 0;
                string payload = JsonUtility.ToJson(old), checksum;
                using (var sha = SHA256.Create()) checksum = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", string.Empty);
                string bytes = JsonUtility.ToJson(new Envelope { Version = 1, Payload = payload, Checksum = checksum,
                    Mode = old.Mode.ToString(), StageIndex = old.StageIndex, Coins = old.Coins });
                string path = Path.Combine(directory, "save-1.json"); File.WriteAllText(path, bytes, new UTF8Encoding(false));
                byte[] originalBytes = File.ReadAllBytes(path);
                var store = new CasinoLocalSaveStore(directory); var migrated = store.Load(1);
                Assert.That(migrated.State.SchemaVersion, Is.EqualTo(3)); Assert.That(migrated.State.Teaching.Status, Is.EqualTo(CasinoTutorialStatus.None));
                Assert.That(File.ReadAllText(path), Is.EqualTo(bytes)); Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1), "读取不创建旁路备份或重写原件。");
                store.Save(1, migrated);
                string archive = path + ".v" + version + "-" + checksum + ".bak";
                Assert.That(File.ReadAllText(archive), Is.EqualTo(bytes));
                Assert.That(File.ReadAllBytes(archive), Is.EqualTo(originalBytes));
                migrated.Advance(100); store.Save(1, migrated); migrated.Advance(100); store.Save(1, migrated);
                Assert.That(File.ReadAllText(archive), Is.EqualTo(bytes), "不可滚动旧原件不能被后续检查点覆盖。");
                string secondPath = Path.Combine(directory, "save-2.json");
                File.WriteAllText(secondPath, File.ReadAllText(path)); File.WriteAllText(secondPath + ".bak", bytes);
                var second = store.Load(2); second.Advance(100); store.Save(2, second);
                Assert.That(File.ReadAllText(secondPath + ".v" + version + "-" + checksum + ".bak"), Is.EqualTo(bytes), "新版primary也必须保存.bak里的v1/v2原件。");
            }
            finally { RemoveOwnedDirectory(directory); }
        }

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
        [Serializable]
        private sealed class Envelope
        { public int Version; public string Payload; public string Checksum; public string Mode; public int StageIndex; public long Coins; }
    }
}
