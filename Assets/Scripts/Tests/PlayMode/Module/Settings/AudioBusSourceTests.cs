using System.Collections;
using Core.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Module
{
    public sealed class AudioBusSourceTests
    {
        [UnityTest]
        public IEnumerator GainChangesPreserveLocalVolumeAndPlayback()
        {
            var owner = new GameObject("Audio bus source test");
            var source = owner.AddComponent<AudioSource>();
            source.playOnAwake = false;
            var clip = AudioClip.Create("Bus test silence", 48000, 1, 48000, false);
            source.clip = clip;
            source.loop = true;
            var bus = owner.AddComponent<AudioBusSource>();
            try
            {
                bus.SetBus(AudioBus.Music);
                bus.SetLocalVolume(0.8f);
                source.Play();
                yield return null;
                AudioSettingsRuntime.Apply(new AudioPreferences { Master = 0.5f, Music = 0.5f });
                Assert.That(source.volume, Is.EqualTo(0.2f).Within(0.00001f));
                AudioSettingsRuntime.Apply(new AudioPreferences { Muted = true });
                Assert.That(source.volume, Is.Zero);
                Assert.That(source.isPlaying, Is.True, "静音不能停止播放");
                AudioSettingsRuntime.Apply(new AudioPreferences());
                Assert.That(source.volume, Is.EqualTo(0.8f).Within(0.00001f));
                bus.enabled = false;
                Assert.That(source.volume, Is.EqualTo(0.8f).Within(0.00001f));
            }
            finally
            {
                AudioSettingsRuntime.Apply(new AudioPreferences());
                Object.Destroy(owner);
                Object.Destroy(clip);
            }
        }
    }
}
