using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 本 Demo 的独立立体声混音；在汇总音乐、音效、引擎之后处理原作两级动力学。
    [RequireComponent(typeof(AudioSource))]
    public sealed class KagitKartAudioDsp : MonoBehaviour
    {
        private readonly object gate = new();
        private readonly List<Voice> voices = new();
        private readonly Dictionary<AudioClip, Samples> loaded = new();
        private Compressor glue;
        private Compressor limiter;
        private AudioClip carrier;
        private int sampleRate;
        internal volatile KagitKartEngineSynth Engine;

        internal sealed class Samples
        {
            internal AudioClip Asset;
            internal float[] Data;
            internal int Channels;
            internal int Rate;
            internal int References;
            internal bool IsEffect;
            internal int Frames => Data.Length / Channels;
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
            private readonly Biquad left = new();
            private readonly Biquad right = new();
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

            internal void SetWave(float[] values, int rate)
            {
                AudioClip release;
                lock (owner.gate)
                {
                    release = owner.Release(samples);
                    samples = new Samples { Data = values, Channels = 1, Rate = rate, IsEffect = true };
                }
                release?.UnloadAudioData();
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
                }
            }

            internal void SetScheduledEndTime(double at)
            {
                lock (owner.gate)
                {
                    end = at;
                }
            }

            internal void Prepare()
            {
                if (FilterEnabled)
                {
                    left.Set("lowpass", Cutoff, Resonance, owner.sampleRate);
                    right.Set("lowpass", Cutoff, Resonance, owner.sampleRate);
                }
            }

            internal void Mix(double now, ref float l, ref float r)
            {
                if (!active || samples == null || now < start || now >= end)
                {
                    return;
                }
                if (readRevision != revision)
                {
                    readRevision = revision;
                    cursor = Math.Max(0, now - start) * samples.Rate * Pitch;
                    left.Reset();
                    right.Reset();
                }
                if (cursor >= samples.Frames)
                {
                    if (!Loop)
                    {
                        active = false;
                        return;
                    }
                    cursor %= samples.Frames;
                }
                int frame = (int)cursor;
                int next = frame + 1 < samples.Frames ? frame + 1 : Loop ? 0 : frame;
                float fraction = (float)(cursor - frame);
                float a = samples.Data[frame * samples.Channels];
                float b = samples.Data[next * samples.Channels];
                float sl = a + (b - a) * fraction;
                float sr = sl;
                if (samples.Channels > 1)
                {
                    a = samples.Data[frame * samples.Channels + 1];
                    b = samples.Data[next * samples.Channels + 1];
                    sr = a + (b - a) * fraction;
                }
                if (FilterEnabled)
                {
                    sl = left.Tick(sl);
                    sr = right.Tick(sr);
                }
                // StereoPanner 的等功率声像；单声道不被中心声像额外放大。
                if (samples.IsEffect && Pan == 0)
                {
                    l += sl * Volume;
                    r += sr * Volume;
                }
                else if (samples.IsEffect || samples.Channels == 1)
                {
                    double angle = (Pan + 1) * Math.PI / 4;
                    l += sl * Volume * (float)Math.Cos(angle);
                    r += sr * Volume * (float)Math.Sin(angle);
                }
                else if (Pan <= 0)
                {
                    double angle = (Pan + 1) * Math.PI / 2;
                    l += (sl + sr * (float)Math.Cos(angle)) * Volume;
                    r += sr * (float)Math.Sin(angle) * Volume;
                }
                else
                {
                    double angle = Pan * Math.PI / 2;
                    l += sl * (float)Math.Cos(angle) * Volume;
                    r += (sr + sl * (float)Math.Sin(angle)) * Volume;
                }
                cursor += (double)samples.Rate / owner.sampleRate * Pitch;
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
            if (clip != null && !loaded.TryGetValue(clip, out next))
            {
                next = new Samples { Asset = clip, Rate = clip.frequency, Channels = clip.channels, Data = new float[clip.samples * clip.channels], IsEffect = clip.name.StartsWith("Sfx_", StringComparison.Ordinal) };
                if (!clip.LoadAudioData() || !clip.GetData(next.Data, 0))
                {
                    throw new InvalidOperationException("原作音频必须使用 DecompressOnLoad：" + clip.name);
                }
                loaded.Add(clip, next);
            }
            AudioClip release;
            lock (gate)
            {
                release = Release(voice.samples);
                voice.samples = next;
                if (next != null)
                {
                    next.References++;
                }
            }
            release?.UnloadAudioData();
        }

        private AudioClip Release(Samples samples)
        {
            if (samples?.Asset != null && --samples.References == 0)
            {
                loaded.Remove(samples.Asset);
                return samples.Asset;
            }
            return null;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (glue == null)
            {
                Array.Clear(data, 0, data.Length);
                return;
            }
            double now = AudioSettings.dspTime;
            lock (gate)
            {
                foreach (var voice in voices)
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
                        foreach (var voice in voices)
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
                loaded.Clear();
            }
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
