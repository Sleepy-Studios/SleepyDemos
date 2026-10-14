using System.IO;
using System;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class KagitKartAudioTests
    {

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

        [Test]
        public void PendingBoostChangesLeaveCurrentAudioBlockIntact()
        {
            var host = new GameObject("加速声块切换测试");
            try
            {
                var dsp = host.AddComponent<KagitKartAudioDsp>();
                // 隔离实际音频设备，只固定声部重采样的输出速率。
                typeof(KagitKartAudioDsp).GetField("sampleRate", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(dsp, 44100);
                var actual = dsp.CreateVoice();
                var expected = dsp.CreateVoice();
                actual.PrepareBoost();
                expected.PrepareBoost();
                actual.Volume = expected.Volume = 1;
                var white = new float[512];
                var crackle = new float[512];
                for (int i = 0; i < white.Length; i++)
                {
                    white[i] = i % 31 / 15f - 1;
                    crackle[i] = i % 7 / 3f - 1;
                }
                actual.SetBoost("pad", 0.37f, white, crackle, 11);
                expected.SetBoost("pad", 0.37f, white, crackle, 11);
                actual.PlayScheduled(0);
                expected.PlayScheduled(0);
                actual.Capture();
                expected.Capture();
                actual.Prepare();
                expected.Prepare();

                actual.SetBoost("pad", 0.71f, white, crackle, 22);
                actual.PlayScheduled(0);
                actual.SetBoost("pad", 1.14f, white, crackle, 33);
                actual.PlayScheduled(0);
                for (int i = 0; i < 512; i++)
                {
                    float left = 0, right = 0, expectedLeft = 0, expectedRight = 0;
                    actual.Mix((double)i / 44100, ref left, ref right);
                    expected.Mix((double)i / 44100, ref expectedLeft, ref expectedRight);
                    Assert.That(left, Is.EqualTo(expectedLeft).Within(0.000001f), "待播事件不能覆盖当前块 PCM");
                    Assert.That(right, Is.EqualTo(expectedRight).Within(0.000001f));
                }

                expected.SetBoost("pad", 1.14f, white, crackle, 33);
                expected.PlayScheduled(0);
                actual.Capture();
                expected.Capture();
                actual.Prepare();
                expected.Prepare();
                for (int i = 0; i < 512; i++)
                {
                    float left = 0, right = 0, expectedLeft = 0, expectedRight = 0;
                    actual.Mix((double)i / 44100, ref left, ref right);
                    expected.Mix((double)i / 44100, ref expectedLeft, ref expectedRight);
                    Assert.That(left, Is.EqualTo(expectedLeft).Within(0.000001f), "下一块应采用最后一次实际加速事件");
                    Assert.That(right, Is.EqualTo(expectedRight).Within(0.000001f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ChapterReplacementRetainsSharedPcmAndReleasesOldReservations()
        {
            var bank = AssetDatabase.LoadAssetAtPath<KagitKartAudioBank>("Assets/LoadResources/Demos/kagit_kart/Data/AudioBank.asset");
            var oldClip = bank.GetClip("sting_finalLap_meadow");
            var nextClip = bank.GetClip("sting_finalLap_bosphorus");
            var sharedClip = bank.GetClip("sting_finishWin");
            var host = new GameObject("章节PCM预留切换测试");
            try
            {
                var dsp = host.AddComponent<KagitKartAudioDsp>();
                var oldVoice = dsp.CreateVoice();
                var sharedVoice = dsp.CreateVoice();
                var nextVoice = dsp.CreateVoice();
                dsp.PrepareRaceClip(oldClip);
                dsp.PrepareRaceClip(sharedClip);
                oldVoice.Clip = oldClip;
                sharedVoice.Clip = sharedClip;
                var oldPcm = oldVoice.samples.Data;
                var sharedPcm = sharedVoice.samples.Data;

                dsp.ClearPreparedClips(new[] { sharedClip, nextClip });
                Assert.That(dsp.PreparedPcmBytes, Is.EqualTo((long)sharedPcm.Length * sizeof(float)), "旧章解除预留，公共短曲继续预留");
                Assert.That(oldVoice.samples.Data, Is.SameAs(oldPcm), "解除预留不撤走声部持有的PCM");
                sharedVoice.Clip = null;
                sharedVoice.Clip = sharedClip;
                Assert.That(sharedVoice.samples.Data, Is.SameAs(sharedPcm), "公共PCM没有声部引用时仍跨章复用");
                oldVoice.Clip = null;
                oldVoice.Clip = oldClip;
                Assert.That(oldVoice.samples.Data, Is.Not.SameAs(oldPcm), "旧章最后一个声部引用释放后不再占用缓存");

                dsp.PrepareRaceClip(nextClip);
                nextVoice.Clip = nextClip;
                Assert.That(dsp.PreparedPcmBytes,
                    Is.EqualTo(((long)sharedPcm.Length + nextVoice.samples.Data.Length) * sizeof(float)), "预算仅含公共曲与新章预留");
                dsp.ClearPreparedClips();
                Assert.That(dsp.PreparedPcmBytes, Is.Zero, "失败、取消和清理仍可解除全部预留");
                sharedVoice.Clip = null;
                sharedVoice.Clip = sharedClip;
                Assert.That(sharedVoice.samples.Data, Is.Not.SameAs(sharedPcm), "公共曲清除预留后按声部引用安全释放");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
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

    }
}
