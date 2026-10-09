using System;
using System.IO;
using System.Reflection;
using Hotfix.JinxCasino.Persistence;
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
        }

        [Test]
        public void CorruptProfileStartsFreshOnlyAfterExplicitSave()
        {
            var profile = CasinoProfile.Create();
            profile.RecordFinishedRun(CasinoProfileTests.FinishedRun(1));
            store.Save(profile);
            string primary = Path.Combine(directory, "profile.json");
            File.WriteAllText(primary, "{broken}");
            Assert.That(store.LoadOrCreate().Data.FinishedRuns, Is.Zero);
            Assert.That(File.ReadAllText(primary), Is.EqualTo("{broken}"));
            store.Save(CasinoProfile.Create());
            Assert.That(store.LoadOrCreate().Data.FinishedRuns, Is.Zero);
        }

        [Test]
        public void RepeatedSaveKeepsCompleteProfileWithoutBackup()
        {
            var profile = CasinoProfile.Create();
            profile.RecordFinishedRun(CasinoProfileTests.FinishedRun());
            store.Save(profile);
            string primary = Path.Combine(directory, "profile.json");
            string before = File.ReadAllText(primary);
            store.Save(CasinoProfile.Restore(profile.ToJson()));
            Assert.That(File.ReadAllText(primary), Is.EqualTo(before));
            Assert.That(File.Exists(primary + ".bak"), Is.False);
        }

        [Test]
        public void InvalidCandidateCannotReplaceValidProfile()
        {
            var profile = CasinoProfile.Create();
            profile.RecordFinishedRun(CasinoProfileTests.FinishedRun());
            store.Save(profile);
            string primary = Path.Combine(directory, "profile.json");
            string before = File.ReadAllText(primary);
            var invalid = profile.Data;
            invalid.Fame++;
            typeof(CasinoProfile).GetField("data", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(profile, invalid);
            Assert.Throws<ArgumentException>(() => store.Save(profile));
            Assert.That(File.ReadAllText(primary), Is.EqualTo(before));
            Assert.That(Directory.GetFiles(directory, "*.tmp-*").Length, Is.Zero);
            Assert.That(store.LoadOrCreate().Data.FinishedRuns, Is.EqualTo(1));
        }

        [Test]
        public void CorruptProfileDoesNotReadOldBackup()
        {
            store.Save(CasinoProfile.Create());
            string primary = Path.Combine(directory, "profile.json");
            File.WriteAllText(primary, "{bad-main}");
            File.WriteAllText(primary + ".bak", CasinoProfile.Create().ToJson());
            Assert.That(store.LoadOrCreate().Data.FinishedRuns, Is.Zero);
            Assert.That(File.ReadAllText(primary), Is.EqualTo("{bad-main}"));
        }

        [Test]
        public void MissingProfileDoesNotReadOldBackup()
        {
            Directory.CreateDirectory(directory);
            var profile = CasinoProfile.Create();
            profile.RecordFinishedRun(CasinoProfileTests.FinishedRun());
            File.WriteAllText(Path.Combine(directory, "profile.json.bak"), profile.ToJson());
            Assert.That(store.LoadOrCreate().Data.FinishedRuns, Is.Zero);
            Assert.That(File.Exists(Path.Combine(directory, "profile.json")), Is.False);
        }

    }
}
