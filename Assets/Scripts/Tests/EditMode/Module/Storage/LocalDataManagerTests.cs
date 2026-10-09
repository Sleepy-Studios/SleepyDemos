using System;
using System.Collections.Generic;
using System.IO;
using Core.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Module
{
    public sealed class LocalDataManagerTests
    {
        private string key;
        private string directory;

        [SetUp]
        public void Prepare()
        {
            key = "SleepyDemos.Tests.LocalData." + Guid.NewGuid().ToString("N");
            directory = Path.Combine(Path.GetTempPath(), "SleepyDemosLocalDataTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void Cleanup()
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }

        [Test]
        public void PreferencesRoundTripScalarsCollectionsAndIndependentObjects()
        {
            LocalDataManager.SaveData(key, true);
            Assert.That(LocalDataManager.LoadData(key, false), Is.True);
            LocalDataManager.SaveData(key, new List<int> { 1, 2, 3 });
            Assert.That(LocalDataManager.LoadData<List<int>>(key), Is.EqualTo(new[] { 1, 2, 3 }));
            LocalDataManager.SaveData(key, new Dictionary<string, int> { ["coins"] = 15 });
            var first = LocalDataManager.LoadData<Dictionary<string, int>>(key);
            first["coins"] = 2;
            Assert.That(LocalDataManager.LoadData<Dictionary<string, int>>(key)["coins"], Is.EqualTo(15));
        }

        [Test]
        public void MissingAndCorruptPreferencesReturnDefaultWithoutWriting()
        {
            Assert.That(LocalDataManager.LoadData(key, 7), Is.EqualTo(7));
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
            PlayerPrefs.SetString(key, "{broken");
            Assert.That(LocalDataManager.LoadData(key, 7, out var warning), Is.EqualTo(7));
            Assert.That(warning, Is.Not.Empty);
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo("{broken"));
        }

        [Test]
        public void InvalidPreferenceCandidateDoesNotReplaceValidValue()
        {
            LocalDataManager.SaveData(key, 15);
            Assert.Throws<ArgumentException>(() => LocalDataManager.SaveData(key, -1, value =>
            {
                if (value < 0)
                    throw new ArgumentException("非法候选。");
            }));
            Assert.That(LocalDataManager.LoadData(key, 0), Is.EqualTo(15));
        }

        [Test]
        public void FileWritesAreIndependentAndInvalidWritesPreservePrimary()
        {
            LocalDataManager.SaveFile("First.json", "{\"coins\":15}", directory);
            LocalDataManager.SaveFile("Second.json", "{\"coins\":2}", directory);
            Assert.That(LocalDataManager.LoadFile("First.json", directory), Does.Contain("15"));
            Assert.Catch<Exception>(() => LocalDataManager.SaveFile("First.json", "{broken", directory));
            Assert.That(LocalDataManager.LoadFile("First.json", directory), Does.Contain("15"));
            Assert.That(Directory.GetFiles(directory, "*.tmp-*").Length, Is.Zero);
        }

        [Test]
        public void FailedReplacementKeepsPrimaryAndRemovesTemporary()
        {
            LocalDataManager.SaveFile("Slot.json", "{\"coins\":15}", directory);
            using (var locked = new FileStream(Path.Combine(directory, "Slot.json"), FileMode.Open, FileAccess.Read, FileShare.Read))
                Assert.Throws<IOException>(() => LocalDataManager.SaveFile("Slot.json", "{\"coins\":2}", directory));
            Assert.That(LocalDataManager.LoadFile("Slot.json", directory), Does.Contain("15"));
            Assert.That(Directory.GetFiles(directory, "*.tmp-*").Length, Is.Zero);
        }

        [Test]
        public void MissingAndCorruptFilesReturnNullAndPathsCannotEscapeRoot()
        {
            Assert.That(LocalDataManager.LoadFile("Missing.json", directory), Is.Null);
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "Corrupt.json"), "{broken");
            Assert.That(LocalDataManager.LoadFile("Corrupt.json", directory), Is.Null);
            Assert.Throws<ArgumentException>(() => LocalDataManager.LoadFile("../Outside.json", directory));
            Assert.Throws<ArgumentException>(() => LocalDataManager.SaveFile(Path.GetFullPath("Outside.json"), "{}", directory));
        }
    }
}
