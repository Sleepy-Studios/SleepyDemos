using System;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /// 同原键的版本迁移、取消预览与坏记录保护；全部使用UUID独立测试键。
    public sealed class CasinoLocalPreferencesMigrationTests
    {
        private const string LegacyRecord = "{\"SchemaVersion\":1,\"PcLookMultiplier\":1.5,\"TouchLookMultiplier\":2.5,\"Volume\":0.2,\"Muted\":true,\"LeftHanded\":true}";
        private string key;
        private CasinoLocalPreferencesStore store;
        private GameObject owner;

        [SetUp]
        public void Setup()
        { key = "JinxCasino.Tests.PreferencesMigration." + Guid.NewGuid().ToString("N"); store = new CasinoLocalPreferencesStore(key); }

        [TearDown]
        public void Cleanup()
        { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }

        [Test]
        public void LegacyLoadPreservesAllOldPreferencesAndDoesNotRewrite()
        {
            PlayerPrefs.SetString(key, LegacyRecord);
            var migrated = store.Load();
            Assert.That(migrated.SchemaVersion, Is.EqualTo(2)); Assert.That(migrated.IsValid, Is.True);
            Assert.That(migrated.PcLookMultiplier, Is.EqualTo(1.5f)); Assert.That(migrated.TouchLookMultiplier, Is.EqualTo(2.5f));
            Assert.That(migrated.Volume, Is.EqualTo(0.2f)); Assert.That(migrated.Muted && migrated.LeftHanded, Is.True);
            AssertGamepadDefaults(migrated);
            Assert.That(store.LastLoadWarning, Is.Null); Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(LegacyRecord));
            migrated.GamepadLookDegreesPerSecond = 150;
            store.Save(migrated);
            var stored = JsonUtility.FromJson<CasinoLocalPreferences>(PlayerPrefs.GetString(key));
            Assert.That(stored.SchemaVersion, Is.EqualTo(2)); Assert.That(stored.GamepadLookDegreesPerSecond, Is.EqualTo(150));
            Assert.That(new CasinoLocalPreferencesStore(key).Load().Muted, Is.True);
        }

        [Test]
        public void VersionTwoRoundTripAndRouterProjectionAreIndependent()
        {
            var draft = new CasinoLocalPreferences
            {
                PcLookMultiplier = 0.5f, TouchLookMultiplier = 2, Volume = 0.3f, LeftHanded = true,
                GamepadDeadzone = 0.1f, GamepadMaximum = 0.9f, GamepadLookDegreesPerSecond = 120,
                GamepadLookMultiplier = 1.5f, GamepadInvertY = true, RumbleEnabled = false, RumbleStrength = 0.4f
            };
            store.Save(draft); draft.GamepadDeadzone = 0.4f;
            var loaded = store.Load(); var input = loaded.ToInputSettings();
            Assert.That(input.IsValid, Is.True); Assert.That(input.SchemaVersion, Is.EqualTo(1), "输入DTO与持久化记录独立版本。");
            Assert.That(input.MouseLookMultiplier, Is.EqualTo(0.5f)); Assert.That(input.TouchLookMultiplier, Is.EqualTo(2));
            Assert.That(input.GamepadDeadzone, Is.EqualTo(0.1f)); Assert.That(input.GamepadMaximum, Is.EqualTo(0.9f));
            Assert.That(input.GamepadLookDegreesPerSecond, Is.EqualTo(120)); Assert.That(input.GamepadLookMultiplier, Is.EqualTo(1.5f));
            Assert.That(input.GamepadInvertY, Is.True); Assert.That(input.RumbleEnabled, Is.False); Assert.That(input.RumbleStrength, Is.EqualTo(0.4f));
            input.GamepadDeadzone = 0.3f; loaded.GamepadDeadzone = 0.2f;
            Assert.That(store.Load().GamepadDeadzone, Is.EqualTo(0.1f)); Assert.That(store.Load().LeftHanded, Is.True);
            var zeroSettings = loaded.Copy(); zeroSettings.GamepadDeadzone = 0; zeroSettings.RumbleStrength = 0; zeroSettings.GamepadInvertY = false;
            store.Save(zeroSettings); var zeroRestored = new CasinoLocalPreferencesStore(key).Load();
            Assert.That(zeroRestored.GamepadDeadzone, Is.Zero); Assert.That(zeroRestored.RumbleStrength, Is.Zero);
            Assert.That(zeroRestored.GamepadInvertY || zeroRestored.RumbleEnabled, Is.False);
        }

        [Test]
        public void BrokenVersionTwoOrLegacyReturnsWholeDefaultsAndPreservesOriginalBytes()
        {
            var invalidDeadzone = new CasinoLocalPreferences { GamepadDeadzone = 0.8f, Volume = 0.2f, LeftHanded = true };
            var invalidRumble = new CasinoLocalPreferences { RumbleStrength = 2, Muted = true };
            foreach (string json in new[]
            {
                JsonUtility.ToJson(invalidDeadzone), JsonUtility.ToJson(invalidRumble),
                "{\"SchemaVersion\":2,\"PcLookMultiplier\":1,\"TouchLookMultiplier\":1,\"Volume\":0.2}",
                "{\"SchemaVersion\":2,\"PcLookMultiplier\":1,\"TouchLookMultiplier\":1,\"Volume\":0.2,\"GamepadMaximum\":1,\"GamepadLookDegreesPerSecond\":90,\"GamepadLookMultiplier\":1,\"RumbleEnabled\":false,\"RumbleStrength\":0.5}",
                LegacyRecord.Replace("1.5", "100"), LegacyRecord.Replace("\"SchemaVersion\":1", "\"SchemaVersion\":99"), "not-json", "{}"
            })
            {
                PlayerPrefs.SetString(key, json); var loaded = store.Load();
                Assert.That(loaded.IsValid, Is.True); Assert.That(loaded.Volume, Is.EqualTo(0.7f)); Assert.That(loaded.PcLookMultiplier, Is.EqualTo(1));
                Assert.That(loaded.Muted || loaded.LeftHanded, Is.False); AssertGamepadDefaults(loaded);
                Assert.That(store.LastLoadWarning, Is.Not.Empty); Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(json));
            }
        }

        [Test]
        public void GamepadPreviewAndCancelCannotPersistTheInMemoryMigration()
        {
            PlayerPrefs.SetString(key, LegacyRecord);
            owner = new GameObject("Casino input preference migration test");
            var controller = owner.AddComponent<JinxCasinoController>(); controller.LoadLocalPreferences(store);
            var before = controller.LocalPreferences; var preview = before.Copy();
            preview.GamepadLookDegreesPerSecond = 200; preview.GamepadInvertY = true; preview.RumbleEnabled = false;
            controller.ApplyLocalPreferences(preview);
            Assert.That(controller.LocalPreferences.GamepadLookDegreesPerSecond, Is.EqualTo(200));
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(LegacyRecord));
            // 与保存Presenter.CancelPreview一致：重新应用进入面板前副本，不调用Save。
            controller.ApplyLocalPreferences(before);
            AssertGamepadDefaults(controller.LocalPreferences); Assert.That(controller.LocalPreferences.LeftHanded, Is.True);
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(LegacyRecord));
            controller.SaveLocalPreferences(preview);
            Assert.That(store.Load().GamepadLookDegreesPerSecond, Is.EqualTo(200)); Assert.That(store.Load().GamepadInvertY, Is.True);
        }

        [Test]
        public void InvalidNewParametersRejectSaveWithoutChangingExistingBlob()
        {
            store.Save(new CasinoLocalPreferences { Volume = 0.3f }); string existing = PlayerPrefs.GetString(key);
            var invalid = new[]
            {
                new CasinoLocalPreferences { GamepadDeadzone = float.NaN },
                new CasinoLocalPreferences { GamepadMaximum = float.PositiveInfinity },
                new CasinoLocalPreferences { GamepadLookDegreesPerSecond = 0 },
                new CasinoLocalPreferences { GamepadLookMultiplier = float.NegativeInfinity },
                new CasinoLocalPreferences { RumbleStrength = -1 }, new CasinoLocalPreferences { SchemaVersion = 1 }
            };
            foreach (var value in invalid)
            {
                Assert.Throws<ArgumentException>(() => store.Save(value));
                Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(existing));
            }
        }

        private static void AssertGamepadDefaults(CasinoLocalPreferences value)
        {
            Assert.That(value.GamepadDeadzone, Is.EqualTo(0.2f)); Assert.That(value.GamepadMaximum, Is.EqualTo(1));
            Assert.That(value.GamepadLookDegreesPerSecond, Is.EqualTo(90)); Assert.That(value.GamepadLookMultiplier, Is.EqualTo(1));
            Assert.That(value.GamepadInvertY, Is.False); Assert.That(value.RumbleEnabled, Is.True); Assert.That(value.RumbleStrength, Is.EqualTo(1));
        }
    }
}
