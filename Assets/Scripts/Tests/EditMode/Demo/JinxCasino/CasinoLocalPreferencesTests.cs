using System;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Persistence;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /// 使用UUID独立PlayerPrefs键，不触碰正式偏好、用户三槽或永久档案。
    public sealed class CasinoLocalPreferencesTests
    {
        private string key;
        private CasinoLocalPreferencesStore store;
        private GameObject owner;

        [SetUp]
        public void Prepare()
        { key = "JinxCasino.Tests.Preferences." + Guid.NewGuid().ToString("N"); store = new CasinoLocalPreferencesStore(key); }

        [TearDown]
        public void Cleanup()
        { if (owner != null) UnityEngine.Object.DestroyImmediate(owner); PlayerPrefs.DeleteKey(key); PlayerPrefs.Save(); }

        [Test]
        public void MissingPreferencesKeepOriginalInputAndDoNotCreateSavedKey()
        {
            var defaults = store.Load(); Assert.That(defaults.PcLookMultiplier, Is.EqualTo(1)); Assert.That(defaults.TouchLookMultiplier, Is.EqualTo(1));
            Assert.That(defaults.Volume, Is.EqualTo(0.7f)); Assert.That(defaults.Muted || defaults.LeftHanded, Is.False);
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
        }

        /// <summary>坏值只读回退，必须明确保存才替换原记录。</summary>
        /// <param name="json">损坏、缺版本或版本不兼容的记录。</param>
        [TestCase("not-json")]
        [TestCase("{}")]
        [TestCase("{\"SchemaVersion\":99,\"PcLookMultiplier\":1,\"TouchLookMultiplier\":1,\"Volume\":0.7}")]
        [TestCase("{\"SchemaVersion\":1,\"PcLookMultiplier\":100,\"TouchLookMultiplier\":1,\"Volume\":0.7}")]
        public void InvalidRecordFallsBackWithoutOverwritingUntilExplicitSave(string json)
        {
            PlayerPrefs.SetString(key, json);
            var restored = store.Load(); Assert.That(restored.IsValid, Is.True); Assert.That(restored.PcLookMultiplier, Is.EqualTo(1));
            Assert.That(store.LastLoadWarning, Is.Not.Empty); Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(json));
            store.Save(restored); Assert.That(store.LastLoadWarning, Is.Null); Assert.That(PlayerPrefs.GetString(key), Is.Not.EqualTo(json));
        }

        [Test]
        public void SavingNonfiniteOrUnsupportedPreferencesRejectsWithoutChangingExistingRecord()
        {
            store.Save(new CasinoLocalPreferences { PcLookMultiplier = 1.5f, Volume = 0.3f }); string before = PlayerPrefs.GetString(key);
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity, -1, 4 })
            {
                Assert.Throws<ArgumentException>(() => store.Save(new CasinoLocalPreferences { TouchLookMultiplier = invalid }));
                Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(before));
            }
            Assert.Throws<ArgumentException>(() => store.Save(new CasinoLocalPreferences { SchemaVersion = 99 }));
            Assert.Throws<ArgumentException>(() => store.Save(new CasinoLocalPreferences { Volume = float.NaN }));
            Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(before));
        }

        [Test]
        public void ReloadUsesIndependentPreferencesAndPreservesAudioAndLayoutFlags()
        {
            var saved = new CasinoLocalPreferences { PcLookMultiplier = 0.5f, TouchLookMultiplier = 2.5f, Volume = 0.2f, Muted = true, LeftHanded = true };
            store.Save(saved); saved.Volume = 1;
            var first = store.Load(); first.LeftHanded = false; first.PcLookMultiplier = 3;
            var second = new CasinoLocalPreferencesStore(key).Load();
            Assert.That(second.PcLookMultiplier, Is.EqualTo(0.5f)); Assert.That(second.TouchLookMultiplier, Is.EqualTo(2.5f));
            Assert.That(second.Volume, Is.EqualTo(0.2f)); Assert.That(second.Muted && second.LeftHanded, Is.True);
        }

        [Test]
        public void InputMultipliersScaleEachDeviceSeparatelyAndPreviewNeverWritesPreferences()
        {
            owner = new GameObject("CasinoPreferencesTest"); var controller = owner.AddComponent<JinxCasinoController>();
            controller.LoadLocalPreferences(store);
            var touch = new Vector2(4, -3); var mouse = new Vector2(-1, 6);
            Assert.That(controller.ScaleLocalLookInput(touch, mouse), Is.EqualTo(touch + mouse));
            var draft = controller.LocalPreferences; draft.PcLookMultiplier = 2; draft.TouchLookMultiplier = 0.5f;
            controller.ApplyLocalPreferences(draft);
            Assert.That(controller.ScaleLocalLookInput(touch, mouse), Is.EqualTo(touch * 0.5f + mouse * 2));
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
            controller.SaveLocalPreferences(draft);
            Assert.That(store.Load().PcLookMultiplier, Is.EqualTo(2));
            controller.ApplyLocalPreferences(new CasinoLocalPreferences());
            Assert.That(controller.ScaleLocalLookInput(touch, mouse), Is.EqualTo(touch + mouse));
            Assert.That(store.Load().PcLookMultiplier, Is.EqualTo(2), "撤销预览不能覆盖明确保存值");
        }
    }
}
