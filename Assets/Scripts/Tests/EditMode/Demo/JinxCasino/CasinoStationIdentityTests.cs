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
    public sealed class CasinoStationIdentityTests
    {
        [Test]
        public void ActiveTableSurvivesRestoreAndRejectsOtherTableWithoutChangingRound()
        {
            var session = CasinoAdventureSession.Start(6, CasinoAdventureMode.Practice, 1);
            Assert.That(session.BeginGame("start", CasinoGameKind.CooperativeLevers, 50, 0, "levers-a").Success, Is.True);
            session = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            var before = session.State;
            Assert.That(session.Act("wrong", CasinoMiniGameAction.PullLever, 0, "levers-b").Error, Is.EqualTo("WrongStation"));
            Assert.That(session.Act("legacy", CasinoMiniGameAction.PullLever).Error, Is.EqualTo("WrongStation"));
            Assert.That(session.State.ActiveRoundJson, Is.EqualTo(before.ActiveRoundJson));
            Assert.That(session.State.RandomState, Is.EqualTo(before.RandomState));
            Assert.That(session.State.Coins, Is.EqualTo(before.Coins));
            Assert.That(session.State.ActiveStationId, Is.EqualTo("levers-a"));
            Assert.That(session.BeginGame("start", CasinoGameKind.CooperativeLevers, 50, 0, "levers-a").Changed, Is.False);
            Assert.That(session.BeginGame("start", CasinoGameKind.CooperativeLevers, 50, 0, "levers-b").Error, Is.EqualTo("ConflictingRequest"));
        }

        [Test]
        public void CompletedResultRemainsAtOriginalTableAndRetryCannotReaward()
        {
            var session = CasinoAdventureSession.Start(2, CasinoAdventureMode.Practice, 1);
            Assert.That(session.BeginGame("spin", CasinoGameKind.Slots, 50, 0, "slots-a").Success, Is.True);
            Assert.That(session.HasActiveRound, Is.False);
            Assert.That(session.State.LastStationId, Is.EqualTo("slots-a"));
            var before = session.State;
            session = CasinoAdventureSession.Restore(session.ToSnapshotJson());
            Assert.That(session.BeginGame("spin", CasinoGameKind.Slots, 50, 0, "slots-a").Changed, Is.False);
            Assert.That(session.State.Coins, Is.EqualTo(before.Coins));
            Assert.That(session.State.SettledRoundSequence, Is.EqualTo(before.SettledRoundSequence));
            Assert.That(session.State.RandomState, Is.EqualTo(before.RandomState));
        }

        [Test]
        public void LegacyActiveRoundMigratesWithoutDrawingAndCanBeClaimedOnlyOnce()
        {
            var session = CasinoAdventureSession.Start(3, CasinoAdventureMode.Practice, 1);
            session.BeginGame("old", CasinoGameKind.CooperativeLevers, 50, 0);
            var legacy = session.State; legacy.SchemaVersion = 1;
            session = CasinoAdventureSession.Restore(JsonUtility.ToJson(legacy));
            Assert.That(session.State.SchemaVersion, Is.EqualTo(3));
            Assert.That(session.BindActiveStation("bad", "slots-a", CasinoGameKind.Slots).Success, Is.False);
            Assert.That(session.BindActiveStation("claim", "levers-a", CasinoGameKind.CooperativeLevers).Success, Is.True);
            Assert.That(session.BindActiveStation("claim", "levers-a", CasinoGameKind.CooperativeLevers).Changed, Is.False);
            Assert.That(session.BindActiveStation("other", "levers-b", CasinoGameKind.CooperativeLevers).Success, Is.False);
            Assert.That(session.State.ActiveRoundJson, Is.EqualTo(legacy.ActiveRoundJson));
            Assert.That(session.State.RandomState, Is.EqualTo(legacy.RandomState));
            Assert.That(session.State.LockedCoins, Is.EqualTo(legacy.LockedCoins));
        }

        [Test]
        public void RestoreRejectsDifferentGameInActiveRoundMetadata()
        {
            var session = CasinoAdventureSession.Start(3, CasinoAdventureMode.Practice, 1);
            session.BeginGame("start", CasinoGameKind.CooperativeLevers, 50, 0, "levers-a");
            var damaged = session.State;
            damaged.ActiveGame = CasinoGameKind.CooperativeVault;
            Assert.Throws<ArgumentException>(() => CasinoAdventureSession.Restore(JsonUtility.ToJson(damaged)));
        }

        [Test]
        public void MigrationPreservesOriginalFileAcrossLaterCheckpointSaves()
        {
            string directory = Path.GetFullPath(Path.Combine("Library/JinxCasino/StationSaveTests", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                var session = CasinoAdventureSession.Start(3, CasinoAdventureMode.Practice, 1);
                session.BeginGame("old", CasinoGameKind.CooperativeLevers, 50, 0);
                var state = session.State; state.SchemaVersion = 1;
                string payload = JsonUtility.ToJson(state);
                string checksum;
                using (var sha = SHA256.Create()) checksum = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(payload))).Replace("-", "");
                string original = JsonUtility.ToJson(new LegacyEnvelope { Version = 1, Payload = payload, Checksum = checksum,
                    Mode = state.Mode.ToString(), StageIndex = state.StageIndex, Coins = state.Coins });
                string path = Path.Combine(directory, "save-1.json"); File.WriteAllText(path, original);
                var store = new CasinoLocalSaveStore(directory);
                var restored = store.Load(1);
                Assert.That(File.ReadAllText(path), Is.EqualTo(original), "读取迁移不能隐式写文件。");
                store.Save(1, restored);
                restored.BindActiveStation("claim", "levers-a", CasinoGameKind.CooperativeLevers);
                store.Save(1, restored);
                Assert.That(File.ReadAllText(path + ".v1-" + checksum + ".bak"), Is.EqualTo(original));
                Assert.That(store.Load(1).State.ActiveStationId, Is.EqualTo("levers-a"));
                // 已有新版主档但普通备份仍是旧版时，下一次替换也必须保住唯一旧原件。
                string secondPath = Path.Combine(directory, "save-2.json");
                File.WriteAllText(secondPath, File.ReadAllText(path));
                File.WriteAllText(secondPath + ".bak", original);
                var second = store.Load(2);
                second.Advance(100);
                store.Save(2, second);
                Assert.That(File.ReadAllText(secondPath + ".v1-" + checksum + ".bak"), Is.EqualTo(original));
            }
            finally
            {
                string allowed = Path.GetFullPath("Library/JinxCasino/StationSaveTests") + Path.DirectorySeparatorChar;
                if (directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) Directory.Delete(directory, true);
            }
        }

        [Serializable]
        private sealed class LegacyEnvelope
        {
            public int Version;
            public string Payload;
            public string Checksum;
            public string Mode;
            public int StageIndex;
            public long Coins;
        }
    }
}
