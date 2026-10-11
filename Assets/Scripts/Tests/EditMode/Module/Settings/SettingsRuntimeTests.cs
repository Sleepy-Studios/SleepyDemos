using System;
using Core.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Module
{
    public sealed class SettingsRuntimeTests
    {
        [TearDown]
        public void RestoreAudio() => AudioSettingsRuntime.Apply(new AudioPreferences());

        [Test]
        public void AudioGainMultipliesCategoriesAndMuteRetainsPreference()
        {
            var preferences = new AudioPreferences { Master = 0.5f, Music = 0.4f, Effects = 0.8f, Ui = 0.2f };
            AudioSettingsRuntime.Apply(preferences);
            Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Music), Is.EqualTo(0.2f).Within(0.00001f));
            Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Effects), Is.EqualTo(0.4f).Within(0.00001f));
            Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Ui), Is.EqualTo(0.1f).Within(0.00001f));

            preferences.Muted = true;
            AudioSettingsRuntime.Apply(preferences);
            Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Music), Is.Zero);
            preferences.Muted = false;
            AudioSettingsRuntime.Apply(preferences);
            preferences.Music = 0;
            Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Music), Is.EqualTo(0.2f).Within(0.00001f), "应用者持有独立副本");
            Assert.Throws<ArgumentException>(() => AudioSettingsRuntime.Apply(new AudioPreferences { Master = float.NaN }));
            Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Music), Is.EqualTo(0.2f).Within(0.00001f));
        }

        [Test]
        public void AudioObserverFailureKeepsCommittedGainAndNotifiesLaterObservers()
        {
            int notifications = 0;
            Action failing = () => throw new InvalidOperationException("Expected audio observer failure");
            Action later = () => notifications++;
            AudioSettingsRuntime.Changed += failing;
            AudioSettingsRuntime.Changed += later;
            try
            {
                LogAssert.Expect(LogType.Exception, "InvalidOperationException: Expected audio observer failure");
                Assert.DoesNotThrow(() => AudioSettingsRuntime.Apply(new AudioPreferences { Master = 0.3f }));
                Assert.That(AudioSettingsRuntime.GetGain(AudioBus.Effects), Is.EqualTo(0.3f).Within(0.00001f));
                Assert.That(notifications, Is.EqualTo(1));
            }
            finally
            {
                AudioSettingsRuntime.Changed -= failing;
                AudioSettingsRuntime.Changed -= later;
            }
        }

        [Test]
        public void DisplayConfirmationOnlyCoversExplicitWindowChanges()
        {
            var previous = new GraphicsPreferences { Width = 1920, Height = 1080, WindowMode = (int)FullScreenMode.Windowed };
            Assert.That(DisplaySettingsRuntime.RequiresConfirmation(previous, new GraphicsPreferences()), Is.False);
            Assert.That(DisplaySettingsRuntime.RequiresConfirmation(previous, new GraphicsPreferences
            {
                Width = 1920, Height = 1080, WindowMode = (int)FullScreenMode.Windowed,
                Quality = GraphicsQualityLevel.Low, VSync = 1
            }), Is.False);
            Assert.That(DisplaySettingsRuntime.RequiresConfirmation(previous, new GraphicsPreferences { Width = 1280, Height = 720 }),
                Is.EqualTo(DisplaySettingsRuntime.SupportsDesktopDisplay));
        }

        [Test]
        public void QualityPresetRejectsInvalidCandidateWithoutReplacingValues()
        {
            var preset = ScriptableObject.CreateInstance<GraphicsQualityPreset>();
            try
            {
                preset.Configure(GraphicsQualityLevel.Medium, 0.8f, 30, 2, 1, 1.2f);
                Assert.Throws<ArgumentException>(() => preset.Configure(GraphicsQualityLevel.High, float.NaN, 40, 1, 0, 2));
                Assert.That(preset.Level, Is.EqualTo(GraphicsQualityLevel.Medium));
                Assert.That(preset.RenderScale, Is.EqualTo(0.8f));
                Assert.Throws<ArgumentException>(() => preset.Configure(GraphicsQualityLevel.Current, 1, 40, 1, 0, 2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(preset);
            }
        }
    }
}
