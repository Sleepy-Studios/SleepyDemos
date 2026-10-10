using System;

namespace Hotfix.KagitKart
{
    /// 直接迁移 sfx.js boost 配方；持续时间由本次规则事件给出，不用样本变速替代。
    public static class KagitKartBoostSynth
    {
        /// <summary>按原作连续参数生成一次加速声，噪声来自源码 createBank 的实际采样。</summary>
        /// <param name="source">drift、pad、start、trick、draft 或其它原作加速来源。</param>
        /// <param name="duration">本次事件持续秒数；0 沿用原作默认 0.8，不读取叠加后的剩余计时。</param>
        /// <param name="white">原作单声道白噪声，不能为空。</param>
        /// <param name="crackle">原作单声道纸质碎响，不能为空。</param>
        /// <param name="sampleRate">噪声与输出采样率。</param>
        /// <param name="seed">本次音频独立种子，不消耗比赛随机流。</param>
        /// <returns>尚未经过总线动力学的单声道样本，包含原作 SFX 总线 0.85 增益。</returns>
        public static float[] Render(string source, float duration, float[] white, float[] crackle, int sampleRate, int seed)
        {
            if (white == null || white.Length == 0 || crackle == null || crackle.Length == 0 || sampleRate <= 0 || duration < 0 || float.IsNaN(duration) || float.IsInfinity(duration))
            {
                throw new ArgumentException("加速配方采样或持续时间无效。");
            }
            source ??= "drift";
            double s = Math.Min(1.5, duration == 0 ? 0.8 : duration) / 1.4;
            double d = 0.35 + 0.45 * s + (source == "start" ? 0.3 : 0);
            double soft = source == "draft" ? 0.6 : 1;
            var result = new float[(int)Math.Ceiling((d + 0.5) * sampleRate)];
            var random = new KagitKartRandom(unchecked((uint)seed));
            Noise(result, white, sampleRate, random, 0, d + 0.3, "bandpass", source == "draft" ? 900 : 350,
                source == "draft" ? 4200 : 2800, d * 0.7, 1.1, 0.04, 0.5 * soft * (0.7 + 0.3 * s), d * 0.3, d * 0.25);
            Noise(result, crackle, sampleRate, random, 0, d + 0.2, "bandpass", 1800, 1800, 0, 0.8, 0.03, 0.28 * soft, d * 0.35);
            if (source != "draft")
            {
                Tone(result, sampleRate, 0, 0.42, 120, 58, 0.084, "sine", 0.003, 0.45 * (0.6 + 0.4 * s), 0.06);
            }
            if (source == "pad")
            {
                Tone(result, sampleRate, 0, 0.3, 300, 1400, 0.16, "sawtooth", 0.005, 0.12, 0.06, filter: 3000);
            }
            if (source == "start")
            {
                Noise(result, white, sampleRate, random, 0, 1.2, "lowpass", 420, 420, 0, 0.8, 0.05, 0.35, 0.25);
                Tone(result, sampleRate, 0, 1.2, 70, 70, 0, "sine", 0.03, 0.3, 0.2);
            }
            if (source == "trick")
            {
                Ping(result, sampleRate, 0.02, 2093, 0.12);
                Ping(result, sampleRate, 0.07, 2637, 0.1);
            }
            for (int i = 0; i < result.Length; i++)
            {
                result[i] *= 0.85f;
            }
            return result;
        }

        private static double Envelope(double t, double attack, double peak, double tau, double hold)
        {
            if (t < attack)
            {
                return peak * t / attack;
            }
            return peak * Math.Exp(-Math.Max(0, t - attack - hold) / tau);
        }

        private static void Noise(float[] output, float[] noise, int sr, KagitKartRandom random, double at, double duration,
            string type, double from, double to, double sweep, double q, double attack, double peak, double tau, double hold = 0)
        {
            // 原作 BufferSource 在 rate=1 时将噪声起点取最近采样，不能截断。
            int offset = (int)Math.Round(random.Next() * Math.Max(1, noise.Length - sr * 0.8), MidpointRounding.AwayFromZero);
            var filter = new KagitKartAudioDsp.Biquad();
            int first = (int)Math.Round(at * sr);
            int count = Math.Min((int)Math.Round(duration * sr), output.Length - first);
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / sr;
                double frequency = sweep > 0 ? from * Math.Pow(to / from, Math.Min(1, t / sweep)) : from;
                filter.Set(type, frequency, q, sr);
                output[first + i] += filter.Tick(noise[(offset + i) % noise.Length]) * (float)Envelope(t, attack, peak, tau, hold);
            }
        }

        private static void Tone(float[] output, int sr, double at, double duration, double from, double to, double sweep,
            string type, double attack, double peak, double tau, double filter = 0)
        {
            var lowpass = filter > 0 ? new KagitKartAudioDsp.Biquad() : null;
            lowpass?.Set("lowpass", filter, 0.7, sr);
            int first = (int)Math.Round(at * sr);
            int count = Math.Min((int)Math.Round(duration * sr), output.Length - first);
            double phase = 0;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / sr;
                double frequency = sweep > 0 ? from * Math.Pow(to / from, Math.Min(1, t / sweep)) : from;
                double dt = frequency / sr;
                double value = type == "sawtooth" ? 2 * phase - 1 - PolyBlep(phase, dt)
                    : type == "triangle" ? 2 / Math.PI * Math.Asin(Math.Sin(2 * Math.PI * phase)) : Math.Sin(2 * Math.PI * phase);
                if (lowpass != null)
                {
                    value = lowpass.Tick((float)value);
                }
                output[first + i] += (float)(value * Envelope(t, attack, peak, tau, 0));
                phase = (phase + dt) % 1;
            }
        }

        private static double PolyBlep(double phase, double dt)
        {
            if (phase < dt)
            {
                double t = phase / dt;
                return 2 * t - t * t - 1;
            }
            if (phase > 1 - dt)
            {
                double t = (phase - 1) / dt;
                return t * t + 2 * t + 1;
            }
            return 0;
        }

        private static void Ping(float[] output, int sr, double at, double frequency, double peak)
        {
            Tone(output, sr, at, 0.5, frequency, frequency, 0, "sine", 0.002, peak, 0.1);
            Tone(output, sr, at, 0.5, frequency * 2, frequency * 2, 0, "triangle", 0.002, peak * 0.18, 0.07);
        }
    }
}
