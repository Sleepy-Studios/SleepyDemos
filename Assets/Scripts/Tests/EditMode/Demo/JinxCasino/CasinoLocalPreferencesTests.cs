using System;
using Core.Runtime;
using Hotfix.JinxCasino.UI;
using Hotfix.JinxCasino.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /// 使用UUID独立PlayerPrefs键，不触碰正式偏好、用户三槽或永久档案。
    public sealed class CasinoLocalPreferencesTests
    {
        private string key;
        private string warning;

        private CasinoLocalPreferences Load()
            => LocalDataManager.LoadData(key, new CasinoLocalPreferences(), out warning, CasinoLocalPreferences.Validate);

        private void Save(CasinoLocalPreferences value)
        {
            LocalDataManager.SaveData(key, value, CasinoLocalPreferences.Validate);
            warning = null;
        }

        [SetUp]
        public void Prepare()
        { key = "JinxCasino.Tests.Preferences." + Guid.NewGuid().ToString("N"); }

        [TearDown]
        public void Cleanup()
        { PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }

        [Test]
        public void SettingsConstructionDoesNotLoadAndWarningSurvivesPreviewUntilExplicitSave()
        {
            const string invalid = "not-json";
            PlayerPrefs.SetString(key, invalid);
            var settings = new JinxCasinoLocalSettings(key);
            Assert.That(settings.Warning, Is.Null, "构造只绑定存储；坏记录应在显式Load时才产生警告。");
            Assert.That(settings.Value.Volume, Is.EqualTo(0.7f));
            settings.Load();
            Assert.That(settings.Warning, Is.Not.Empty);
            var draft = settings.Value; draft.Volume = 0.2f;
            settings.Apply(draft);
            Assert.That(settings.Warning, Is.Not.Empty, "预览不能消除尚未替换的损坏记录提示。");
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(invalid));
            settings.Save(draft);
            Assert.That(settings.Warning, Is.Null);
            Assert.That(Load().Volume, Is.EqualTo(0.2f));
        }

        [Test]
        public void SettingsOwnCopiesAndNotifyOnlyForSuccessfulChanges()
        {
            var settings = new JinxCasinoLocalSettings(key);
            int changes = 0;
            settings.Changed += () => changes++;
            settings.Load();
            Assert.That(changes, Is.EqualTo(1));
            var draft = settings.Value; draft.Volume = 0.2f;
            settings.Apply(draft); draft.Volume = 0.9f;
            var exposed = settings.Value; exposed.LeftHanded = true;
            Assert.That(settings.Value.Volume, Is.EqualTo(0.2f));
            Assert.That(settings.Value.LeftHanded, Is.False);
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
            Assert.Throws<ArgumentException>(() => settings.Apply(new CasinoLocalPreferences { Volume = float.NaN }));
            Assert.Throws<ArgumentException>(() => settings.Save(null));
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(settings.Value.Volume, Is.EqualTo(0.2f));
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
            settings.Save(settings.Value);
            Assert.That(changes, Is.EqualTo(3));
            Assert.That(Load().Volume, Is.EqualTo(0.2f));
        }

        [Test]
        public void MissingPreferencesKeepOriginalInputAndDoNotCreateSavedKey()
        {
            var defaults = Load(); Assert.That(defaults.PcLookMultiplier, Is.EqualTo(1)); Assert.That(defaults.TouchLookMultiplier, Is.EqualTo(1));
            Assert.That(defaults.Volume, Is.EqualTo(0.7f)); Assert.That(defaults.Muted || defaults.LeftHanded, Is.False);
            Assert.That(defaults.GamepadDeadzone, Is.EqualTo(0.2f)); Assert.That(defaults.GamepadLookDegreesPerSecond, Is.EqualTo(90));
            Assert.That(defaults.RumbleEnabled, Is.True); Assert.That(defaults.RumbleStrength, Is.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
        }

        /// <summary>坏值只读回退，必须明确保存才替换原记录。</summary>
        /// <param name="json">损坏、缺版本或版本不兼容的记录。</param>
        [TestCase("not-json")]
        [TestCase("{}")]
        [TestCase("{\"SchemaVersion\":99,\"PcLookMultiplier\":1,\"TouchLookMultiplier\":1,\"Volume\":0.7}")]
        [TestCase("{\"SchemaVersion\":1,\"PcLookMultiplier\":1.5,\"TouchLookMultiplier\":1,\"Volume\":0.2}")]
        [TestCase("{\"SchemaVersion\":2,\"PcLookMultiplier\":1,\"TouchLookMultiplier\":1,\"Volume\":0.2}")]
        [TestCase("{\"SchemaVersion\":2,\"PcLookMultiplier\":1,\"TouchLookMultiplier\":1,\"Volume\":0.2,\"GamepadMaximum\":1,\"GamepadLookDegreesPerSecond\":90,\"GamepadLookMultiplier\":1,\"RumbleEnabled\":false,\"RumbleStrength\":0.5}")]
        public void InvalidRecordFallsBackWithoutOverwritingUntilExplicitSave(string json)
        {
            PlayerPrefs.SetString(key, json);
            var restored = Load(); Assert.That(restored.IsValid, Is.True); Assert.That(restored.PcLookMultiplier, Is.EqualTo(1));
            Assert.That(restored.Volume, Is.EqualTo(0.7f)); Assert.That(restored.GamepadDeadzone, Is.EqualTo(0.2f));
            Assert.That(warning, Is.Not.Empty); Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(json));
            Save(restored); Assert.That(warning, Is.Null); Assert.That(PlayerPrefs.GetString(key), Is.Not.EqualTo(json));
        }

        [Test]
        public void SavingNonfiniteOrUnsupportedPreferencesRejectsWithoutChangingExistingRecord()
        {
            Save(new CasinoLocalPreferences { PcLookMultiplier = 1.5f, Volume = 0.3f }); string before = PlayerPrefs.GetString(key);
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1, 4 })
            {
                Assert.Throws<ArgumentException>(() => Save(new CasinoLocalPreferences { TouchLookMultiplier = invalid }));
                Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(before));
            }
            Assert.Throws<ArgumentException>(() => Save(new CasinoLocalPreferences { SchemaVersion = 99 }));
            Assert.Throws<ArgumentException>(() => Save(new CasinoLocalPreferences { Volume = float.NaN }));
            foreach (var invalid in new[]
            {
                new CasinoLocalPreferences { GamepadDeadzone = float.NaN },
                new CasinoLocalPreferences { GamepadDeadzone = 0.8f },
                new CasinoLocalPreferences { GamepadMaximum = float.PositiveInfinity },
                new CasinoLocalPreferences { GamepadLookDegreesPerSecond = 0 },
                new CasinoLocalPreferences { GamepadLookMultiplier = float.NegativeInfinity },
                new CasinoLocalPreferences { RumbleStrength = -1 },
                new CasinoLocalPreferences { SchemaVersion = 1 }
            })
                Assert.Throws<ArgumentException>(() => Save(invalid));
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(before));
        }

        [Test]
        public void ReloadUsesIndependentPreferencesAndPreservesAudioAndLayoutFlags()
        {
            var saved = new CasinoLocalPreferences
            {
                PcLookMultiplier = 0.5f, TouchLookMultiplier = 2.5f, Volume = 0.2f, Muted = true, LeftHanded = true,
                GamepadDeadzone = 0.1f, GamepadMaximum = 0.9f, GamepadLookDegreesPerSecond = 120,
                GamepadLookMultiplier = 1.5f, GamepadInvertY = true, RumbleEnabled = false, RumbleStrength = 0.4f
            };
            Save(saved); saved.Volume = 1;
            var first = Load(); first.LeftHanded = false; first.PcLookMultiplier = 3;
            var second = Load();
            Assert.That(second.PcLookMultiplier, Is.EqualTo(0.5f)); Assert.That(second.TouchLookMultiplier, Is.EqualTo(2.5f));
            Assert.That(second.Volume, Is.EqualTo(0.2f)); Assert.That(second.Muted && second.LeftHanded, Is.True);
            var input = second.ToInputSettings();
            Assert.That(input.IsValid, Is.True); Assert.That(input.SchemaVersion, Is.EqualTo(1), "公共输入参数与偏好存储版本独立。");
            Assert.That(input.GamepadDeadzone, Is.EqualTo(0.1f)); Assert.That(input.GamepadMaximum, Is.EqualTo(0.9f));
            Assert.That(input.GamepadLookDegreesPerSecond, Is.EqualTo(120)); Assert.That(input.GamepadLookMultiplier, Is.EqualTo(1.5f));
            Assert.That(input.GamepadInvertY, Is.True); Assert.That(input.RumbleEnabled, Is.False); Assert.That(input.RumbleStrength, Is.EqualTo(0.4f));
            input.GamepadDeadzone = 0.3f; second.GamepadDeadzone = 0.2f;
            Assert.That(Load().GamepadDeadzone, Is.EqualTo(0.1f));
            second.GamepadDeadzone = 0; second.RumbleStrength = 0; second.GamepadInvertY = false;
            Save(second); var zeroRestored = Load();
            Assert.That(zeroRestored.GamepadDeadzone, Is.Zero); Assert.That(zeroRestored.RumbleStrength, Is.Zero);
            Assert.That(zeroRestored.GamepadInvertY || zeroRestored.RumbleEnabled, Is.False);
        }

        [Test]
        public void PreviewPreservesInputSettingsWithoutWritingUntilExplicitSave()
        {
            var settings = new JinxCasinoLocalSettings(key);
            settings.Load();
            Assert.That(settings.Value.ToInputSettings().MouseLookMultiplier, Is.EqualTo(1));
            var draft = settings.Value; draft.PcLookMultiplier = 2; draft.TouchLookMultiplier = 0.5f;
            draft.GamepadLookDegreesPerSecond = 200; draft.GamepadInvertY = true; draft.RumbleEnabled = false;
            settings.Apply(draft);
            var input = settings.Value.ToInputSettings();
            Assert.That(input.MouseLookMultiplier, Is.EqualTo(2));
            Assert.That(input.TouchLookMultiplier, Is.EqualTo(0.5f));
            Assert.That(input.GamepadLookDegreesPerSecond, Is.EqualTo(200));
            Assert.That(input.GamepadInvertY, Is.True);
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
            settings.Apply(new CasinoLocalPreferences());
            Assert.That(settings.Value.GamepadLookDegreesPerSecond, Is.EqualTo(90));
            Assert.That(PlayerPrefs.HasKey(key), Is.False, "取消预览不能创建偏好键。");
            settings.Save(draft);
            Assert.That(Load().PcLookMultiplier, Is.EqualTo(2));
            Assert.That(Load().GamepadLookDegreesPerSecond, Is.EqualTo(200));
            settings.Apply(new CasinoLocalPreferences());
            Assert.That(settings.Value.ToInputSettings().MouseLookMultiplier, Is.EqualTo(1));
            Assert.That(Load().PcLookMultiplier, Is.EqualTo(2), "撤销预览不能覆盖明确保存值");
            Assert.That(settings.Value.GamepadInvertY, Is.False);
        }
    }
}
