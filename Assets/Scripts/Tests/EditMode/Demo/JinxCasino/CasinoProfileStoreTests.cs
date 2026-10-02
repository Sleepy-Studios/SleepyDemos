using System;
using System.IO;
using System.Reflection;
using Hotfix.JinxCasino.Adapters.Persistence;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class CasinoProfileStoreTests
    {
        private string directory;
        private CasinoProfileStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.GetFullPath(Path.Combine("Library/JinxCasino/ProfileTests", Guid.NewGuid().ToString("N")));
            store = new CasinoProfileStore(directory);
        }
        [TearDown]
        public void Cleanup()
        {
            string allowed = Path.GetFullPath("Library/JinxCasino/ProfileTests") + Path.DirectorySeparatorChar;
            Assert.That(directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase), Is.True);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void EmptyProfileDoesNotCreateFilesUntilExplicitSaveAndRunIdSurvivesLoad()
        {
            var profile = store.LoadOrCreate(); Assert.That(profile.Data.FinishedRuns, Is.Zero);
            Assert.That(Directory.Exists(directory), Is.False);
            var run = CasinoProfileTests.FinishedRun(); profile.RecordFinishedRun(run); store.Save(profile);
            Assert.That(File.Exists(Path.Combine(directory, "profile.json")), Is.True);
            Assert.That(File.Exists(Path.Combine(directory, "save-1.json")), Is.False);
            var loaded = store.LoadOrCreate(); Assert.That(loaded.RecordFinishedRun(run), Is.False);
            Assert.That(loaded.ToJson(), Is.EqualTo(profile.ToJson()));
            Assert.That(store.UsesBackup, Is.False);
        }

        [Test]
        public void CorruptPrimaryRecoversAndRepairSavePreservesValidBackup()
        {
            var profile = CasinoProfile.Create(); profile.RecordFinishedRun(CasinoProfileTests.FinishedRun(1)); store.Save(profile);
            string first = profile.ToJson(); profile.RecordFinishedRun(CasinoProfileTests.FinishedRun(2)); store.Save(profile);
            string primary = Path.Combine(directory, "profile.json"), backup = primary + ".bak";
            string backupText = File.ReadAllText(backup);
            File.WriteAllText(primary, "{broken}");
            var recovered = store.LoadOrCreate(); Assert.That(store.UsesBackup, Is.True); Assert.That(recovered.ToJson(), Is.EqualTo(first));
            recovered.RecordFinishedRun(CasinoProfileTests.FinishedRun(3)); store.Save(recovered);
            Assert.That(File.ReadAllText(backup), Is.EqualTo(backupText));
            File.WriteAllText(primary, "{broken-again}");
            Assert.That(store.LoadOrCreate().ToJson(), Is.EqualTo(first));
            Assert.That(store.UsesBackup, Is.True);
        }

        [Test]
        public void IdenticalPayloadDoesNotRollPreviousDifferentBackup()
        {
            var profile = CasinoProfile.Create(); store.Save(profile);
            profile.RecordFinishedRun(CasinoProfileTests.FinishedRun()); store.Save(profile);
            string primary = Path.Combine(directory, "profile.json"), backup = primary + ".bak";
            string beforePrimary = File.ReadAllText(primary), beforeBackup = File.ReadAllText(backup);
            store.Save(profile); store.Save(CasinoProfile.Restore(profile.ToJson()));
            Assert.That(File.ReadAllText(primary), Is.EqualTo(beforePrimary));
            Assert.That(File.ReadAllText(backup), Is.EqualTo(beforeBackup));
        }

        [Test]
        public void InvalidCandidateCannotReplacePriorProfileOrBackupAndTemporaryIsCleaned()
        {
            var profile = CasinoProfile.Create(); store.Save(profile);
            profile.RecordFinishedRun(CasinoProfileTests.FinishedRun()); store.Save(profile);
            string primary = Path.Combine(directory, "profile.json"), backup = primary + ".bak";
            string beforePrimary = File.ReadAllText(primary), beforeBackup = File.ReadAllText(backup);
            // 故障注入只破坏测试对象内的摘要，证明临时文件验证发生在原子替换之前。
            var invalid = profile.Data; invalid.Fame++;
            typeof(CasinoProfile).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, invalid);
            Assert.Throws<ArgumentException>(() => store.Save(profile));
            Assert.That(File.ReadAllText(primary), Is.EqualTo(beforePrimary)); Assert.That(File.ReadAllText(backup), Is.EqualTo(beforeBackup));
            Assert.That(Directory.GetFiles(directory, "*.new-*").Length, Is.Zero);
            Assert.That(store.LoadOrCreate().Data.FinishedRuns, Is.EqualTo(1));
        }

        [Test]
        public void BothInvalidFilesFailExplicitlyInsteadOfResettingPermanentProgress()
        {
            var profile = CasinoProfile.Create(); store.Save(profile);
            profile.RecordFinishedRun(CasinoProfileTests.FinishedRun()); store.Save(profile);
            string primary = Path.Combine(directory, "profile.json");
            File.WriteAllText(primary, "{bad-main}"); File.WriteAllText(primary + ".bak", "{bad-backup}");
            Assert.Throws<InvalidDataException>(() => store.LoadOrCreate());
            Assert.That(File.ReadAllText(primary), Is.EqualTo("{bad-main}"));
            Assert.That(File.ReadAllText(primary + ".bak"), Is.EqualTo("{bad-backup}"));
        }

        [Test]
        public void MissingPrimaryUsesVerifiedBackupAndChecksumChangesAreRejected()
        {
            var profile = CasinoProfile.Create(); profile.RecordFinishedRun(CasinoProfileTests.FinishedRun()); store.Save(profile);
            string first = profile.ToJson(); profile.Equip("color_pink"); store.Save(profile);
            string primary = Path.Combine(directory, "profile.json"); File.Delete(primary);
            Assert.That(store.LoadOrCreate().ToJson(), Is.EqualTo(first)); Assert.That(store.UsesBackup, Is.True);
            string backup = primary + ".bak";
            File.WriteAllText(backup, File.ReadAllText(backup).Replace("\"Checksum\":\"", "\"Checksum\":\"changed"));
            Assert.Throws<InvalidDataException>(() => store.LoadOrCreate());
        }
    }
}
