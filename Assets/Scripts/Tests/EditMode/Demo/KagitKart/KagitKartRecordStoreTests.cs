using System;
using System.IO;
using Hotfix;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartRecordStoreTests
    {
        private string directory;
        private KagitKartRecordStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "KagitKartTests-" + Guid.NewGuid().ToString("N"));
            store = new KagitKartRecordStore(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        [Test]
        public void MissingRecordDoesNotCreateFileAndKeysIsolateIdentity()
        {
            Assert.That(store.Load("meadow", 120, "tilki").bestRace, Is.Null);
            Assert.That(Directory.Exists(directory), Is.False);
            store.SaveBest("meadow", 120, "tilki", Run(9, 3, 3, 3));
            Assert.That(store.Load("meadow", 80, "tilki").bestRace, Is.Null);
            Assert.That(store.Load("meadow", 120, "kedi").bestRace, Is.Null);
            Assert.That(store.Load("desk", 120, "tilki").bestRace, Is.Null);
            Assert.Throws<ArgumentException>(() => store.Load("../other", 120, "tilki"));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Load("meadow", 100, "tilki"));
            Assert.Throws<ArgumentException>(() => store.Load("meadow", 120, "../other"));
        }

        [Test]
        public void BestLapAndRaceUpdateSeparatelyAndGhostReplaysSavedPositions()
        {
            store.SaveBest("meadow", 120, "tilki", Run(9, 3, 3, 3));
            var slower = store.SaveBest("meadow", 120, "tilki", Run(10, 2, 4, 4));
            Assert.That(slower.bestLap, Is.EqualTo(2));
            Assert.That(slower.bestRace, Is.EqualTo(9));
            Assert.That(slower.ghost.total, Is.EqualTo(9));
            store.SaveBest("meadow", 120, "tilki", Run(8, 2.6, 2.6, 2.8));
            var loaded = store.Load("meadow", 120, "tilki");
            Assert.That(loaded.bestLap, Is.EqualTo(2));
            Assert.That(loaded.bestRace, Is.EqualTo(8));
            Assert.That(loaded.ghost.IsValid(), Is.True);
            Assert.That(KagitKartGhostPlayback.TrySample(loaded.ghost, 1.025, out var position, out _), Is.True);
            Assert.That(position.x, Is.EqualTo(1.025).Within(0.01));
            loaded.ghost.frames[0] = 999;
            Assert.That(store.Load("meadow", 120, "tilki").ghost.frames[0], Is.Zero);
        }

        [Test]
        public void CorruptRecordReturnsNoDataAndReadDoesNotOverwrite()
        {
            string path = Path.Combine(directory, LocalDataKeys.KagitKartRecord("meadow", 120, "tilki"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            const string json = "{\"bestLap\":2,\"bestRace\":9,\"ghost\":{\"hz\":20,\"frames\":[1]}}";
            File.WriteAllText(path, json);
            var record = store.Load("meadow", 120, "tilki");
            Assert.That(record.bestLap, Is.Null);
            Assert.That(record.bestRace, Is.Null);
            Assert.That(record.ghost, Is.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo(json));
            File.WriteAllText(path, "invalid-json");
            Assert.That(store.Load("meadow", 120, "tilki").bestRace, Is.Null);
            Assert.That(File.ReadAllText(path), Is.EqualTo("invalid-json"));
        }

        [Test]
        public void InvalidRunCannotReplacePreviousRecord()
        {
            store.SaveBest("meadow", 120, "tilki", Run(9, 3, 3, 3));
            var invalid = Run(8, 2, 3, 3);
            invalid.frames[0] = float.NaN;
            Assert.Throws<ArgumentException>(() => store.SaveBest("meadow", 120, "tilki", invalid));
            invalid = Run(8, 2, 3, 3);
            invalid.checkpoints = Array.Empty<double>();
            Assert.Throws<ArgumentException>(() => store.SaveBest("meadow", 120, "tilki", invalid));
            Assert.That(store.Load("meadow", 120, "tilki").bestRace, Is.EqualTo(9));
        }

        [Test]
        public void FailedAtomicReplacePreservesPreviousBest()
        {
            store.SaveBest("meadow", 120, "tilki", Run(9, 3, 3, 3));
            string path = Path.Combine(directory, LocalDataKeys.KagitKartRecord("meadow", 120, "tilki"));
            string before = File.ReadAllText(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() => store.SaveBest("meadow", 120, "tilki", Run(8, 2, 3, 3)));
            }
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
            Assert.That(store.Load("meadow", 120, "tilki").bestRace, Is.EqualTo(9));
        }

        private static KagitKartGhostData Run(double total, params double[] laps)
        {
            var recorder = new KagitKartGhostRecorder();
            for (int i = 0; i < total * 120; i++)
            {
                recorder.Record(new Vector3(i / 120f, 0, 0), 0, i / 120.0);
            }
            for (int i = 1; i <= 12; i++)
            {
                recorder.Mark(total * i / 12);
            }
            return recorder.Snapshot(total, laps);
        }
    }
}
