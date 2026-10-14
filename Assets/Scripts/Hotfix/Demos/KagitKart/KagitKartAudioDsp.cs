using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 本 Demo 的独立立体声混音；在汇总音乐、音效、引擎之后处理原作两级动力学。
    [RequireComponent(typeof(AudioSource))]
    public sealed class KagitKartAudioDsp : MonoBehaviour
    {
        private const int PcmPageBits = 18;
        private const int PcmPageLength = 1 << PcmPageBits;
        private const int PcmPageMask = PcmPageLength - 1;
        private readonly object gate = new();
        private readonly List<Voice> voices = new();
        private readonly Dictionary<AudioClip, Samples> loaded = new();
        private static readonly ProfilerMarker BlockSync = new("KagitKart.Audio.BlockSync");
        private static readonly ProfilerMarker AudioBlock = new("KagitKart.Audio.AudioBlock");
        private static readonly ProfilerMarker PreparePcm = new("KagitKart.Audio.PreparePcm");
        private Voice[] blockVoices = Array.Empty<Voice>();
        private Compressor glue;
        private Compressor limiter;
        private AudioClip carrier;
        private int sampleRate;
        internal volatile KagitKartEngineSynth Engine;

        internal long PreparedPcmBytes
        {
            get
            {
                long bytes = 0;
                foreach (var samples in loaded.Values)
                {
                    if (samples.IsPrepared)
                    {
                        bytes += (long)samples.Frames * samples.Channels * sizeof(float);
                    }
                }
                return bytes;
            }
        }

        internal sealed class Samples
        {
            internal AudioClip Asset;
            internal float[] Data;
            internal float[][] Pages;
            internal int Channels;
            internal int Rate;
            internal int References;
            internal bool IsEffect;
            internal bool IsPrepared;
            internal int Frames;
        }

        internal sealed class Voice
        {
            private readonly KagitKartAudioDsp owner;
            internal Samples samples;
            private double cursor;
            private double start;
            private double end = double.PositiveInfinity;
            private bool active;
            private int revision;
            private int readRevision;
            private volatile int completedRevision = -1;
            private Samples blockSamples;
            private double blockStart, blockEnd;
            private int blockRevision;
            private bool blockActive, blockLoop, blockFilter;
            private float blockVolume, blockPitch, blockCutoff, blockResonance, blockPan;
            private float leftGain, rightGain, crossLeftGain, crossRightGain;
            private readonly Biquad left = new();
            private readonly Biquad right = new();
            private readonly Biquad boostFilter = new();
            private readonly Samples waveA = new() { Channels = 1, Rate = 44100, IsEffect = true };
            private readonly Samples waveB = new() { Channels = 1, Rate = 44100, IsEffect = true };
            private readonly Samples waveC = new() { Channels = 1, Rate = 44100, IsEffect = true };
            internal volatile float Volume;
            internal volatile float Pitch = 1;
            internal volatile float Pan;
            internal bool Loop;
            internal volatile float Cutoff = 20000;
            internal volatile float Resonance = 0.5f;
            internal bool FilterEnabled;
            internal float Duration => samples == null ? 0 : (float)samples.Frames / samples.Rate;

            internal AudioClip Clip
            {
                get => samples?.Asset;
                set => owner.Assign(this, value);
            }

            internal Voice(KagitKartAudioDsp owner) => this.owner = owner;

            internal void PrepareBoost()
            {
                int frames = KagitKartBoostSynth.FrameCount("start", 1.5f, 44100);
                waveA.Data = new float[frames];
                waveB.Data = new float[frames];
                waveC.Data = new float[frames];
            }

            internal void SetBoost(string source, float duration, float[] white, float[] crackle, int seed)
            {
                Samples next;
                lock (owner.gate)
                {
                    // 块内在读和下一块待播可能不同；第三份缓冲避免连续事件覆盖其中任意一份。
                    next = samples != waveA && blockSamples != waveA ? waveA
                        : samples != waveB && blockSamples != waveB ? waveB : waveC;
                }
                next.Frames = KagitKartBoostSynth.RenderInto(source, duration, white, crackle, 44100, seed, next.Data, boostFilter);
                lock (owner.gate)
                {
                    owner.Release(samples);
                    samples = next;
                }
            }

            internal void Play() => PlayScheduled(AudioSettings.dspTime);

            internal void PlayScheduled(double at)
            {
                lock (owner.gate)
                {
                    start = at;
                    end = double.PositiveInfinity;
                    revision++;
                    active = true;
                }
            }

            internal void Stop()
            {
                lock (owner.gate)
                {
                    active = false;
                    revision++;
                }
            }

            internal void SetScheduledEndTime(double at)
            {
                lock (owner.gate)
                {
                    end = at;
                }
            }

            internal void Capture()
            {
                blockSamples = samples;
                blockStart = start;
                blockEnd = end;
                blockRevision = revision;
                blockLoop = Loop;
                blockFilter = FilterEnabled;
                blockVolume = Volume;
                blockPitch = Pitch;
                blockCutoff = Cutoff;
                blockResonance = Resonance;
                blockPan = Pan;
                if (readRevision != revision)
                {
                    blockActive = active;
                }
            }

            internal void Prepare()
            {
                if (!blockActive)
                {
                    completedRevision = blockRevision;
                }
                if (blockFilter)
                {
                    left.Set("lowpass", blockCutoff, blockResonance, owner.sampleRate);
                    right.Set("lowpass", blockCutoff, blockResonance, owner.sampleRate);
                }
                leftGain = rightGain = 1;
                crossLeftGain = crossRightGain = 0;
                if (blockSamples == null || blockSamples.IsEffect && blockPan == 0)
                {
                    return;
                }
                float panning = blockPan;
                if (blockSamples.IsEffect || blockSamples.Channels == 1)
                {
                    double angle = (panning + 1) * Math.PI / 4;
                    leftGain = (float)Math.Cos(angle);
                    rightGain = (float)Math.Sin(angle);
                }
                else if (panning <= 0)
                {
                    double angle = (panning + 1) * Math.PI / 2;
                    crossLeftGain = (float)Math.Cos(angle);
                    rightGain = (float)Math.Sin(angle);
                }
                else
                {
                    double angle = panning * Math.PI / 2;
                    leftGain = (float)Math.Cos(angle);
                    crossRightGain = (float)Math.Sin(angle);
                }
            }

            internal void ReleaseFinishedSamples()
            {
                if (samples != null && completedRevision == revision)
                {
                    owner.Release(samples);
                    samples = null;
                }
            }

            internal void Mix(double now, ref float l, ref float r)
            {
                if (!blockActive || blockSamples == null || now < blockStart)
                {
                    return;
                }
                if (now >= blockEnd)
                {
                    blockActive = false;
                    completedRevision = blockRevision;
                    return;
                }
                if (readRevision != blockRevision)
                {
                    readRevision = blockRevision;
                    cursor = Math.Max(0, now - blockStart) * blockSamples.Rate * blockPitch;
                    left.Reset();
                    right.Reset();
                }
                if (cursor >= blockSamples.Frames)
                {
                    if (!blockLoop)
                    {
                        blockActive = false;
                        completedRevision = blockRevision;
                        return;
                    }
                    cursor %= blockSamples.Frames;
                }
                int frame = (int)cursor;
                int next = frame + 1 < blockSamples.Frames ? frame + 1 : blockLoop ? 0 : frame;
                float fraction = (float)(cursor - frame);
                int aIndex = frame * blockSamples.Channels;
                int bIndex = next * blockSamples.Channels;
                float[] aData = blockSamples.Data;
                float[] bData = aData;
                var pages = blockSamples.Pages;
                if (pages != null)
                {
                    aData = pages[aIndex >> PcmPageBits];
                    bData = pages[bIndex >> PcmPageBits];
                    aIndex &= PcmPageMask;
                    bIndex &= PcmPageMask;
                }
                float a = aData[aIndex];
                float b = bData[bIndex];
                float sl = a + (b - a) * fraction;
                float sr = sl;
                if (blockSamples.Channels > 1)
                {
                    a = aData[aIndex + 1];
                    b = bData[bIndex + 1];
                    sr = a + (b - a) * fraction;
                }
                if (blockFilter)
                {
                    sl = left.Tick(sl);
                    sr = right.Tick(sr);
                }
                // 声像在块内不变；只缓存系数，保留原作等功率与立体声交叉混音。
                l += (sl * leftGain + sr * crossLeftGain) * blockVolume;
                r += (sr * rightGain + sl * crossRightGain) * blockVolume;
                cursor += (double)blockSamples.Rate / owner.sampleRate * blockPitch;
            }
        }

        internal void Initialize()
        {
            sampleRate = AudioSettings.outputSampleRate;
            glue = new Compressor(sampleRate, -18, 10, 3, 0.005, 0.2);
            limiter = new Compressor(sampleRate, -3, 0, 20, 0.001, 0.1);
            carrier = AudioClip.Create("纸艺卡丁车混音载体", 1024, 2, sampleRate, false);
            var source = GetComponent<AudioSource>();
            source.clip = carrier;
            source.loop = true;
            source.playOnAwake = false;
            source.ignoreListenerPause = true;
            source.priority = 0;
            source.volume = 1;
            source.Play();
        }

        internal Voice CreateVoice()
        {
            var voice = new Voice(this);
            lock (gate)
            {
                voices.Add(voice);
                blockVoices = voices.ToArray();
            }
            return voice;
        }

        private void Assign(Voice voice, AudioClip clip)
        {
            if (voice.Clip == clip)
            {
                return;
            }
            Samples next = null;
            if (clip != null)
            {
                next = PrepareClip(clip);
            }
            lock (gate)
            {
                Release(voice.samples);
                voice.samples = next;
                if (next != null)
                {
                    next.References++;
                }
            }
        }

        internal void PrepareRaceClip(AudioClip clip)
        {
            PrepareClip(clip).IsPrepared = true;
        }

        internal async UniTask PrepareRaceClipAsync(AudioClip clip, CancellationToken cancellationToken, Func<bool> isCurrent = null)
        {
            RequireCurrentPreparation(cancellationToken, isCurrent);
            if (loaded.TryGetValue(clip, out var cached))
            {
                cached.IsPrepared = true;
                return;
            }
            bool copyOwnsUnload = false;
            try
            {
                if (!clip.LoadAudioData() && clip.loadState != AudioDataLoadState.Loading)
                {
                    throw new InvalidOperationException("原作音频加载失败：" + clip.name);
                }
                // Native 请求不能中途取消；串行准备仍占有门，等待完成后才卸载或交给下一首。
                while (clip != null && clip.loadState == AudioDataLoadState.Loading)
                {
                    await UniTask.NextFrame(PlayerLoopTiming.Update);
                }
                RequireCurrentPreparation(cancellationToken, isCurrent);
                if (clip == null || clip.loadState != AudioDataLoadState.Loaded)
                {
                    throw new InvalidOperationException("原作音频未完成解码。");
                }
                if (clip.name.StartsWith("Music_", StringComparison.Ordinal)
                    && !clip.name.StartsWith("Music_Title_", StringComparison.Ordinal))
                {
                    int length = clip.samples * clip.channels;
                    var next = new Samples
                    {
                        Asset = clip,
                        Rate = clip.frequency,
                        Channels = clip.channels,
                        Frames = clip.samples,
                        Pages = new float[(length + PcmPageLength - 1) / PcmPageLength][]
                    };
                    for (int i = 0; i < next.Pages.Length; i++)
                    {
                        RequireCurrentPreparation(cancellationToken, isCurrent);
                        using (PreparePcm.Auto())
                        {
                            int offset = i * PcmPageLength;
                            var page = new float[Math.Min(PcmPageLength, length - offset)];
                            // GetData 的偏移单位是帧；原作立体声页按完整左右声道对齐。
                            if (!clip.GetData(page, offset / next.Channels))
                            {
                                throw new InvalidOperationException("原作音乐 PCM 分页读取失败：" + clip.name);
                            }
                            next.Pages[i] = page;
                        }
                        await UniTask.NextFrame(PlayerLoopTiming.Update, cancellationToken);
                    }
                    RequireCurrentPreparation(cancellationToken, isCurrent);
                    next.IsPrepared = true;
                    loaded.Add(clip, next);
                }
                else
                {
                    PrepareRaceClip(clip);
                    copyOwnsUnload = true;
                }
            }
            finally
            {
                if (!copyOwnsUnload && clip != null)
                {
                    clip.UnloadAudioData();
                }
            }
        }

        private static void RequireCurrentPreparation(CancellationToken cancellationToken, Func<bool> isCurrent)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (isCurrent != null && !isCurrent())
            {
                throw new OperationCanceledException("章节音频准备已被新的选择替代。", cancellationToken);
            }
        }

        internal void ClearPreparedClips(IReadOnlyList<AudioClip> keep = null)
        {
            var unused = new List<AudioClip>();
            foreach (var pair in loaded)
            {
                bool retained = false;
                if (keep != null)
                {
                    for (int i = 0; i < keep.Count; i++)
                    {
                        if (keep[i] == pair.Key)
                        {
                            retained = true;
                            break;
                        }
                    }
                }
                if (retained)
                {
                    continue;
                }
                pair.Value.IsPrepared = false;
                if (!pair.Value.IsEffect && pair.Value.References == 0)
                {
                    unused.Add(pair.Key);
                }
            }
            foreach (var clip in unused)
            {
                loaded.Remove(clip);
            }
        }

        internal void ReleaseFinishedVoices()
        {
            lock (gate)
            {
                foreach (var voice in voices)
                {
                    voice.ReleaseFinishedSamples();
                }
            }
        }

        internal void PrepareEffects(IReadOnlyList<AudioClip> clips)
        {
            for (int i = 0; i < clips.Count; i++)
            {
                if (clips[i].name.StartsWith("Sfx_", StringComparison.Ordinal))
                {
                    PrepareClip(clips[i]);
                }
            }
        }

        private Samples PrepareClip(AudioClip clip)
        {
            if (!loaded.TryGetValue(clip, out var next))
            {
                using var sample = PreparePcm.Auto();
                next = new Samples
                {
                    Asset = clip,
                    Rate = clip.frequency,
                    Channels = clip.channels,
                    Frames = clip.samples,
                    Data = new float[clip.samples * clip.channels],
                    IsEffect = clip.name.StartsWith("Sfx_", StringComparison.Ordinal)
                };
                try
                {
                    if (!clip.LoadAudioData() || !clip.GetData(next.Data, 0))
                    {
                        throw new InvalidOperationException("原作音频必须使用 DecompressOnLoad：" + clip.name);
                    }
                    loaded.Add(clip, next);
                }
                finally
                {
                    // DSP 只读自己的 PCM；复制完成即释放 Unity 解码副本，不保留两份长期缓存。
                    clip.UnloadAudioData();
                }
            }
            return next;
        }

        private void Release(Samples samples)
        {
            if (samples?.Asset != null && --samples.References == 0 && !samples.IsEffect && !samples.IsPrepared)
            {
                loaded.Remove(samples.Asset);
            }
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (glue == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            double now = AudioSettings.dspTime;
            Voice[] current;
            using (BlockSync.Auto())
            {
                lock (gate)
                {
                    current = blockVoices;
                    foreach (var voice in current)
                    {
                        voice.Capture();
                    }
                }
            }
            using (AudioBlock.Auto())
            {
                foreach (var voice in current)
                {
                    voice.Prepare();
                }
                var engine = Engine;
                engine?.Begin();
                try
                {
                    for (int frame = 0; frame < data.Length / channels; frame++)
                    {
                        float l = 0, r = 0;
                        double at = now + (double)frame / sampleRate;
                        foreach (var voice in current)
                        {
                            voice.Mix(at, ref l, ref r);
                        }
                        engine?.Mix(ref l, ref r);
                        l *= 1.25f;
                        r *= 1.25f;
                        glue.Process(ref l, ref r);
                        limiter.Process(ref l, ref r);
                        for (int channel = 0; channel < channels; channel++)
                        {
                            data[frame * channels + channel] = channels == 1 ? (l + r) * 0.5f : channel == 0 ? l : channel == 1 ? r : 0;
                        }
                    }
                }
                finally
                {
                    engine?.End();
                }
            }
        }

        private void OnDestroy()
        {
            var source = GetComponent<AudioSource>();
            if (source != null)
            {
                source.Stop();
            }
            lock (gate)
            {
                voices.Clear();
                blockVoices = Array.Empty<Voice>();
            }
            loaded.Clear();
            if (carrier != null)
            {
                Destroy(carrier);
            }
        }

        /// 原作 WebAudio 双二阶滤波器状态；低通与高通的 Q 按 dB 解读。
        internal sealed class Biquad
        {
            private double b0 = 1, b1, b2, a1, a2, x1, x2, y1, y2;
            private double lastFrequency = -1, lastQ = -1;
            private string lastType;

            internal void Set(string type, double frequency, double q, int sr)
            {
                if (frequency == lastFrequency && q == lastQ && type == lastType)
                {
                    return;
                }
                lastFrequency = frequency;
                lastQ = q;
                lastType = type;
                double w = 2 * Math.PI * Math.Min(frequency, sr * 0.49) / sr;
                // WebAudio 低通/高通 Q 是 dB，bandpass 才是线性值。
                double linearQ = type == "lowpass" || type == "highpass" ? Math.Pow(10, q / 20) : q;
                double cs = Math.Cos(w), alpha = Math.Sin(w) / (2 * linearQ), a0 = 1 + alpha;
                b0 = type == "bandpass" ? alpha : type == "highpass" ? (1 + cs) / 2 : (1 - cs) / 2;
                b1 = type == "bandpass" ? 0 : type == "highpass" ? -(1 + cs) : 1 - cs;
                b2 = type == "bandpass" ? -alpha : b0;
                b0 /= a0;
                b1 /= a0;
                b2 /= a0;
                a1 = -2 * cs / a0;
                a2 = (1 - alpha) / a0;
            }

            internal float Tick(float value)
            {
                double result = b0 * value + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1;
                x1 = value;
                y2 = y1;
                y1 = result;
                return (float)result;
            }

            internal void Reset() => x1 = x2 = y1 = y2 = 0;
        }

        /// Blink/WebAudio 动力学算法的 C# 迁移；许可见 Data/AudioNotices.txt。
        public sealed class Compressor
        {
            private readonly double threshold, knee, ratio, attackFrames, releaseFrames, saturationFrames;
            private readonly float[] delayLeft, delayRight;
            private readonly double linearThreshold, kneeEnd, kneeFactor, kneeOutputDb, makeup;
            private int position;
            private int division;
            private double detector;
            private double gain = 1;
            private double maximumAttack = -1;
            private double desired;
            private double envelopeRate;

            /// <summary>创建原作参数的左右声道联动 WebAudio 动力学处理器。</summary>
            /// <param name="sampleRate">输出采样率。</param>
            /// <param name="threshold">压缩阈值，dB。</param>
            /// <param name="knee">阈值上方软膝宽度，dB；0 为硬膝。</param>
            /// <param name="ratio">阈值以上压缩比，大于等于 1。</param>
            /// <param name="attack">WebAudio 攻击时间，秒。</param>
            /// <param name="release">WebAudio 自适应恢复时间，秒。</param>
            public Compressor(int sampleRate, double threshold, double knee, double ratio, double attack, double release)
            {
                if (sampleRate <= 0 || knee < 0 || ratio < 1 || attack < 0 || release <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(sampleRate));
                }
                this.threshold = threshold;
                this.knee = knee;
                this.ratio = ratio;
                attackFrames = Math.Max(0.001, attack) * sampleRate;
                releaseFrames = release * sampleRate;
                saturationFrames = 0.0025 * sampleRate;
                linearThreshold = Linear(threshold);
                kneeEnd = Linear(threshold + knee);
                double low = 0.1, high = 10000, factor = 5;
                for (int i = 0; i < 15; i++)
                {
                    double slope = (Db(Knee(kneeEnd * 1.001, factor)) - Db(Knee(kneeEnd, factor))) / (Db(kneeEnd * 1.001) - Db(kneeEnd));
                    if (slope < 1 / ratio)
                    {
                        high = factor;
                    }
                    else
                    {
                        low = factor;
                    }
                    factor = Math.Sqrt(low * high);
                }
                kneeFactor = factor;
                kneeOutputDb = Db(Knee(kneeEnd, factor));
                makeup = Math.Pow(1 / Saturate(1), 0.6);
                delayLeft = new float[Math.Max(1, (int)(0.006 * sampleRate))];
                delayRight = new float[delayLeft.Length];
            }

            private static double Db(double value) => 20 * Math.Log10(Math.Max(1e-30, value));

            private static double Linear(double value) => Math.Pow(10, value / 20);

            private double Knee(double value, double factor) => value < linearThreshold
                ? value : linearThreshold + (1 - Math.Exp(-factor * (value - linearThreshold))) / factor;

            private double Saturate(double value) => value < kneeEnd ? Knee(value, kneeFactor)
                : Linear(kneeOutputDb + (Db(value) - threshold - knee) / ratio);

            /// <summary>读取原作软膝传输曲线，返回非正的衰减 dB。</summary>
            /// <param name="levelDb">输入峰值，dBFS。</param>
            /// <returns>原作曲线的增益衰减。</returns>
            public double CurveReduction(double levelDb) => Db(Saturate(Linear(levelDb))) - levelDb;

            private void BeginDivision()
            {
                desired = Math.Asin(Math.Max(0, Math.Min(1, detector))) / (Math.PI / 2);
                bool releasing = desired > gain;
                double difference = desired == 0 ? releasing ? -1 : 1 : Db(gain / desired);
                if (releasing)
                {
                    maximumAttack = -1;
                    double x = 0.25 * (Math.Max(-12, Math.Min(0, difference)) + 12);
                    double frames = releaseFrames * (0.09 + x * (0.005988750244256685 + x * (0.050409513441085324 + x * (0.012655416910923124 + x * 0.0009463194037350403))));
                    envelopeRate = Linear(5 / frames);
                }
                else
                {
                    maximumAttack = Math.Max(maximumAttack, difference);
                    envelopeRate = 1 - Math.Pow(0.25 / Math.Max(0.5, maximumAttack), 1 / attackFrames);
                }
            }

            /// <summary>处理一个立体声采样，保留原作前瞻、32 帧包络与自动补偿增益。</summary>
            /// <param name="left">输入左声道；返回带 6ms 前瞻的压缩输出。</param>
            /// <param name="right">输入右声道；返回带 6ms 前瞻的压缩输出。</param>
            public void Process(ref float left, ref float right)
            {
                if (division == 0)
                {
                    BeginDivision();
                }
                double peak = Math.Max(Math.Abs(left), Math.Abs(right));
                double attenuation = peak <= 0.0001 ? 1 : Saturate(peak) / peak;
                double detectorRate = attenuation > detector ? Linear(Math.Max(2, -Db(attenuation)) / saturationFrames) - 1 : 1;
                detector = Math.Min(1, detector + (attenuation - detector) * detectorRate);
                gain = envelopeRate < 1 ? gain + (desired - gain) * envelopeRate : Math.Min(1, gain * envelopeRate);
                double multiplier = makeup * Math.Sin(Math.PI / 2 * gain);
                float a = delayLeft[position], b = delayRight[position];
                delayLeft[position] = left;
                delayRight[position] = right;
                position = (position + 1) % delayLeft.Length;
                division = (division + 1) % 32;
                left = (float)(a * multiplier);
                right = (float)(b * multiplier);
            }
        }
    }
}
