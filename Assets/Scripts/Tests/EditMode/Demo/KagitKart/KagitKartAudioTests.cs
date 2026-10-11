using System.IO;
using System;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartAudioTests
    {
        [Serializable]
        private sealed class Manifest
        {
            public int sampleRate;
            public string sourceRevision;
            public ClipInfo[] clips;
        }

        [Serializable]
        private sealed class ClipInfo
        {
            public string name;
            public double duration;
            public int channels;
            public float rms;
        }

        [Serializable]
        private sealed class DynamicsReference
        {
            public int sampleRate;
            public int step;
            public float[] left;
            public float[] right;
            public float[] outputLeft;
            public float[] outputRight;
        }

        [Test]
        public void MixedBusDynamicsMatchNativeWebAudio()
        {
            var reference = JsonUtility.FromJson<DynamicsReference>(File.ReadAllText(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalAudioDynamics.json"));
            var glue = new KagitKartAudioDsp.Compressor(reference.sampleRate, -18, 10, 3, 0.005, 0.2);
            var limiter = new KagitKartAudioDsp.Compressor(reference.sampleRate, -3, 0, 20, 0.001, 0.1);
            double maximumError = 0;
            for (int i = 0; i < reference.left.Length; i++)
            {
                float left = reference.left[i], right = reference.right[i];
                glue.Process(ref left, ref right);
                limiter.Process(ref left, ref right);
                if (i % reference.step == 0)
                {
                    maximumError = Math.Max(maximumError, Math.Abs(left - reference.outputLeft[i / reference.step]));
                    maximumError = Math.Max(maximumError, Math.Abs(right - reference.outputRight[i / reference.step]));
                }
            }
            Assert.That(maximumError, Is.LessThan(0.0003), "实际 WebAudio 两级混音动力学对照误差");
        }

        [Serializable]
        private sealed class BoostReference
        {
            public float duration;
            public int sampleRate;
            public int seed;
            public int step;
            public float[] expected;
        }

        [Test]
        public void ContinuousBoostMatchesNativeWebAudioRecipe()
        {
            var reference = JsonUtility.FromJson<BoostReference>(File.ReadAllText(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalAudioBoost.json"));
            var bank = AssetDatabase.LoadAssetAtPath<KagitKartAudioBank>("Assets/LoadResources/Demos/kagit_kart/Data/AudioBank.asset");
            var whiteClip = bank.GetClip("bank_white");
            var crackleClip = bank.GetClip("bank_crackle");
            whiteClip.LoadAudioData();
            crackleClip.LoadAudioData();
            var white = new float[whiteClip.samples];
            var crackle = new float[crackleClip.samples];
            Assert.That(whiteClip.GetData(white, 0), Is.True);
            Assert.That(crackleClip.GetData(crackle, 0), Is.True);
            var result = KagitKartBoostSynth.Render("start", reference.duration, white, crackle, reference.sampleRate, reference.seed);
            double maximumError = 0;
            for (int i = 0; i < reference.expected.Length; i++)
            {
                maximumError = Math.Max(maximumError, Math.Abs(result[i * reference.step] - reference.expected[i]));
            }
            Assert.That(maximumError, Is.LessThan(0.003), "原作动态加速配方采样误差");
        }

        [Test]
        public void BoostUsesActualContinuousEventDuration()
        {
            var bank = AssetDatabase.LoadAssetAtPath<KagitKartAudioBank>("Assets/LoadResources/Demos/kagit_kart/Data/AudioBank.asset");
            var whiteClip = bank.GetClip("bank_white");
            var crackleClip = bank.GetClip("bank_crackle");
            whiteClip.LoadAudioData();
            crackleClip.LoadAudioData();
            var white = new float[whiteClip.samples];
            var crackle = new float[crackleClip.samples];
            Assert.That(whiteClip.GetData(white, 0), Is.True);
            Assert.That(crackleClip.GetData(crackle, 0), Is.True);
            var shortBoost = KagitKartBoostSynth.Render("pad", 0.37f, white, crackle, 44100, 1234);
            var longBoost = KagitKartBoostSynth.Render("pad", 1.14f, white, crackle, 44100, 1234);
            Assert.That(shortBoost.Length, Is.EqualTo((int)Math.Ceiling((0.85 + 0.45 * 0.37f / 1.4) * 44100)));
            Assert.That(longBoost.Length, Is.EqualTo((int)Math.Ceiling((0.85 + 0.45 * 1.14f / 1.4) * 44100)));
            Assert.That(shortBoost[8820], Is.Not.EqualTo(longBoost[8820]), "持续时间改变滤波扫频与包络，不是变速播放同一个音效");
            Assert.That(() => KagitKartBoostSynth.Render("pad", float.NaN, white, crackle, 44100, 1), Throws.ArgumentException);
        }

        [TestCase(0, 0, 0.2f)]
        [TestCase(0, 1, 0.7f)]
        [TestCase(0.24f, 1, 0.41f)]
        [TestCase(0.48f, 1, 0.5f)]
        [TestCase(0.72f, 1, 0.59f)]
        [TestCase(1.17f, 1, 1f)]
        public void EngineRpmPreservesOriginalGearDrops(float speed, float throttle, float expected)
        {
            Assert.That(KagitKartAudio.Rpm(speed, throttle), Is.EqualTo(expected).Within(0.0001f));
        }

        [Serializable]
        private sealed class ShaperReference
        {
            public float[] input;
            public float[] expected;
        }

        [Test]
        public void EngineSoftClipMatchesNativeTwoTimesOversampling()
        {
            var reference = JsonUtility.FromJson<ShaperReference>(File.ReadAllText(
                "Assets/Scripts/Tests/EditMode/Demo/KagitKart/Fixtures/OriginalEngineSoftClip.json"));
            var clipper = new KagitKartEngineSynth.OversampledClipper();
            double maximumError = 0;
            for (int i = 0; i < reference.input.Length; i++)
            {
                maximumError = Math.Max(maximumError, Math.Abs(clipper.Tick(reference.input[i]) - reference.expected[i]));
            }
            Assert.That(maximumError, Is.LessThan(0.000005), "原作引擎 2x 软削波滤波及延迟采样误差");
        }

        [Test]
        public void OriginalAudioManifestResolvesCompletePlayableBank()
        {
            const string root = "Assets/LoadResources/Demos/kagit_kart/Data/";
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(root + "AudioManifest.json"));
            Assert.That(manifest.sourceRevision, Is.EqualTo("da100b21ae1c5332d2843d21a9bb7b68b97e9c2a"));
            var bank = AssetDatabase.LoadAssetAtPath<KagitKartAudioBank>(root + "AudioBank.asset");
            Assert.That(bank, Is.Not.Null);
            foreach (var item in manifest.clips)
            {
                string name = item.name;
                var clip = bank.GetClip(name);
                Assert.That(clip.length, Is.EqualTo(item.duration).Within(0.01f), name);
                Assert.That(clip.channels, Is.EqualTo(item.channels), name);
                Assert.That(item.rms, Is.GreaterThan(0.00001f), name);
                if (name.StartsWith("music_", StringComparison.Ordinal))
                {
                    string path = AssetDatabase.GetAssetPath(clip);
                    Assert.That(Path.GetExtension(path), Is.EqualTo(".ogg"), name);
                    Assert.That(clip.frequency, Is.EqualTo(manifest.sampleRate), name);
                    // Unity 导入 WAV/OGG 均可能有一个采样的长度舍入，不能容忍编码器整块补帧。
                    Assert.That(clip.samples, Is.EqualTo((int)Math.Round(item.duration * manifest.sampleRate)).Within(1), name);
                    var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                    Assert.That(importer.defaultSampleSettings.loadType, Is.EqualTo(AudioClipLoadType.DecompressOnLoad), name);
                    Assert.That(clip.LoadAudioData(), Is.True, name);
                    Assert.That(clip.GetData(new float[4096 * clip.channels], clip.samples / 2), Is.True, name);
                    clip.UnloadAudioData();
                }
            }
            foreach (string chapter in new[] { "meadow", "bosphorus", "glacier", "desk" })
            {
                foreach (string prefix in new[] { "music_", "music_intro_", "music_final_" })
                {
                    Assert.That(bank.GetClip(prefix + chapter + "_start"), Is.Not.Null);
                    Assert.That(bank.GetClip(prefix + chapter + "_loop"), Is.Not.Null);
                }
                Assert.That(bank.GetClip("sting_finalLap_" + chapter), Is.Not.Null);
            }
            foreach (string cue in new[] { "title", "results", "podium" })
            {
                Assert.That(bank.GetClip("music_" + cue + "_start"), Is.Not.Null);
                Assert.That(bank.GetClip("music_" + cue + "_loop"), Is.Not.Null);
            }
            Assert.That(() => bank.GetClip("missing_original_recipe"), Throws.ArgumentException);
        }
    }
}
