using System;
using Core.Runtime;
using Core.Runtime.Inputs;
using Hotfix;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartSettingsTests
    {
        [Test]
        public void DefaultsAndChangesKeepCurrentSettingsIndependent()
        {
            var original = new KagitKartSettings();
            Assert.That(original.IsValid(), Is.True);
            Assert.That(original.MusicVolume, Is.EqualTo(0.7f));
            Assert.That(original.SfxVolume, Is.EqualTo(0.9f));
            Assert.That(original.Muted || original.AutoAccelerate || original.ShowFps || original.SeenTutorial, Is.False);
            Assert.That(original.CameraShake, Is.True);
            Assert.That(original.ResolveReducedMotion(true), Is.True);
            Assert.That(original.ResolveReducedMotion(false), Is.False);
            Assert.That(original.ResolveTouch(InputDeviceKind.Touch), Is.True);
            Assert.That(original.ResolveTouch(InputDeviceKind.Gamepad), Is.False);
            var changed = original.WithValue("MusicVolume", 0).WithValue("TouchControls", 1)
                .WithValue("ReducedMotion", 2).WithValue("SeenTutorial", 1);
            Assert.That(changed.MusicVolume, Is.Zero);
            Assert.That(changed.ResolveTouch(InputDeviceKind.KeyboardMouse), Is.True);
            Assert.That(changed.ResolveReducedMotion(true), Is.False);
            Assert.That(changed.SeenTutorial, Is.True);
            Assert.That(original.MusicVolume, Is.EqualTo(0.7f));
            Assert.That(original.SeenTutorial, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => original.WithValue("MusicVolume", 11));
            Assert.Throws<ArgumentOutOfRangeException>(() => original.WithValue("Muted", 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => original.WithValue("TouchControls", 3));
            Assert.Throws<ArgumentException>(() => original.WithValue("quality", 1));
        }

        [Test]
        public void PreferencesPersistAndCorruptionDoesNotOverwriteTheStoredValue()
        {
            string key = "KagitKart.SettingsTests." + Guid.NewGuid().ToString("N");
            try
            {
                var saved = new KagitKartSettings().WithValue("AutoAccelerate", 1).WithValue("Muted", 1);
                LocalDataManager.SaveData(key, saved);
                var loaded = LocalDataManager.LoadData(key, new KagitKartSettings());
                Assert.That(loaded.AutoAccelerate && loaded.Muted, Is.True);
                loaded.CameraShake = false;
                Assert.That(LocalDataManager.LoadData(key, new KagitKartSettings()).CameraShake, Is.True);
                PlayerPrefs.SetString(key, "{invalid");
                var fallback = LocalDataManager.LoadData(key, new KagitKartSettings(), out string warning);
                Assert.That(warning, Is.Not.Null);
                Assert.That(fallback.IsValid(), Is.True);
                Assert.That(PlayerPrefs.GetString(key), Is.EqualTo("{invalid"));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

    }
}
