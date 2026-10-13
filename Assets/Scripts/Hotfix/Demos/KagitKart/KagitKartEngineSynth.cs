using System;
using Unity.Profiling;

namespace Hotfix.KagitKart
{
    /// engine.js 的实时节点迁移；噪声时钟、滤波频率和振幅调制独立，禁止用循环变速代替。
    internal sealed class KagitKartEngineSynth
    {
        private static readonly ProfilerMarker ControlSync = new("KagitKart.Engine.ControlSync");
        private static readonly ProfilerMarker AudioBlock = new("KagitKart.Engine.AudioBlock");
        private readonly object gate = new();
        private readonly int rate;
        private readonly float[] white, pink;
        private readonly int noiseRate;
        private double noiseCursor;
        private readonly Param active, volume;
        private readonly Param body, flutter, rattle, squeak, whine, roar, wind, shimmer;
        private readonly Param frequency, bodyCutoff, puttFrequency, puttDepth, flutterFrequency;
        private readonly Param rattleCutoff, squeakCutoff, whineFrequency, roarCutoff, windCutoff, pan;
        private readonly Param[] parameters;
        private readonly Oscillator sawA = new(), sawB = new(), sub = new(), putt = new();
        private readonly Oscillator flutterLfo = new(), rattleLfo = new(), squeakLfo = new();
        private readonly Oscillator whineOsc = new(), whineLfo = new(), shimmerLfo = new();
        private readonly OversampledClipper clipper = new();
        private readonly KagitKartAudioDsp.Biquad bodyFilter = new(), flutterFilter = new(), rattleFilter = new();
        private readonly KagitKartAudioDsp.Biquad squeakFilter = new(), roarHigh = new(), roarLow = new();
        private readonly KagitKartAudioDsp.Biquad windFilter = new(), shimmerFilter = new();
        private readonly Cpu[] cpus = new Cpu[3];

        private sealed class Param
        {
            internal double Value;
            private double target;
            private double step;
            private double pendingTarget, pendingStep, pendingValue;
            private bool resetPending;
            private readonly int rate;

            internal Param(int rate, double value = 0)
            {
                this.rate = rate;
                Value = target = pendingTarget = value;
            }

            internal void Set(double value, double tau, bool immediate = false)
            {
                pendingTarget = value;
                pendingStep = 1 - Math.Exp(-1 / (rate * tau));
                if (immediate)
                {
                    ResetValue(value);
                }
            }

            internal void ResetValue(double value)
            {
                pendingValue = value;
                resetPending = true;
            }

            internal void Apply()
            {
                target = pendingTarget;
                step = pendingStep;
                if (resetPending)
                {
                    Value = pendingValue;
                    resetPending = false;
                }
            }

            internal double Tick() => Value += (target - Value) * step;
        }

        private sealed class Oscillator
        {
            private double phase;

            internal double Tick(string type, double frequency, int rate)
            {
                double dt = frequency / rate;
                double value;
                if (type == "sine")
                {
                    value = Math.Sin(2 * Math.PI * phase);
                }
                else if (type == "triangle")
                {
                    value = 2 / Math.PI * Math.Asin(Math.Sin(2 * Math.PI * phase));
                }
                else if (type == "square")
                {
                    value = (phase < 0.5 ? 1 : -1) + Blep(phase, dt) - Blep((phase + 0.5) % 1, dt);
                }
                else
                {
                    // WebAudio saw 的正弦级数从零相位起，与从 -1 起的普通斜坡相差半周期。
                    double p = (phase + 0.5) % 1;
                    value = 2 * p - 1 - Blep(p, dt);
                }
                phase = (phase + dt) % 1;
                return value;
            }

            private static double Blep(double t, double dt)
            {
                if (t < dt)
                {
                    t /= dt;
                    return t + t - t * t - 1;
                }
                if (t > 1 - dt)
                {
                    t = (t - 1) / dt;
                    return t * t + t + t + 1;
                }
                return 0;
            }
        }

        private sealed class Cpu
        {
            internal readonly Param Frequency, Cutoff, Gain, Pan;
            internal readonly Oscillator Saw = new(), Square = new();
            internal readonly KagitKartAudioDsp.Biquad Filter = new();

            internal Cpu(int rate)
            {
                Frequency = new Param(rate, 70);
                Cutoff = new Param(rate, 900);
                Gain = new Param(rate);
                Pan = new Param(rate);
            }
        }

        // 原作 WaveShaper 的 1024 点曲线及 Blink 2x Blackman 半带重采样。
        // 上采样 128 taps、下采样 256 taps（只保存非零奇数项），许可随 AudioNotices 保存。
        internal sealed class OversampledClipper
        {
            private readonly float[] up = new float[128], down = new float[128], curve = new float[1024];
            private readonly float[] input = new float[128], even = new float[128], odd = new float[256];
            private int position;

            internal OversampledClipper()
            {
                for (int i = 0; i < 128; i++)
                {
                    double s = Math.PI * (i - 63.5);
                    up[i] = (float)(Math.Sin(s) / s * Window((i + 0.5) / 128));
                    s = Math.PI * (2 * i + 1 - 128) * 0.5;
                    down[i] = (float)(Math.Sin(s) / s * 0.5 * Window((2d * i + 1) / 256));
                }
                for (int i = 0; i < curve.Length; i++)
                {
                    curve[i] = (float)(Math.Tanh((2d * i / (curve.Length - 1) - 1) * 2.2) / Math.Tanh(2.2));
                }
            }

            internal float Tick(float value)
            {
                input[position & 127] = value;
                float interpolated = 0;
                for (int k = 0; k < 128; k++)
                {
                    interpolated += up[k] * input[(position - k) & 127];
                }
                even[position & 127] = Shape(input[(position - 64) & 127]);
                odd[position & 255] = Shape(interpolated);
                float result = 0.5f * even[(position - 64) & 127];
                for (int k = 0; k < 128; k++)
                {
                    result += down[k] * odd[(position - k - 1) & 255];
                }
                position = (position + 1) & 255;
                return result;
            }

            private float Shape(float value)
            {
                double at = (Math.Max(-1, Math.Min(1, value)) + 1) * 0.5 * (curve.Length - 1);
                int index = (int)at;
                return curve[index] + (curve[Math.Min(index + 1, curve.Length - 1)] - curve[index]) * (float)(at - index);
            }

            private static double Window(double x) => 0.42 - 0.5 * Math.Cos(2 * Math.PI * x) + 0.08 * Math.Cos(4 * Math.PI * x);
        }

        internal KagitKartEngineSynth(int outputRate, float[] whiteNoise, float[] pinkNoise, int bankRate)
        {
            rate = outputRate;
            white = whiteNoise;
            pink = pinkNoise;
            noiseRate = bankRate;
            active = new Param(rate);
            volume = new Param(rate, 0.81);
            body = new Param(rate); flutter = new Param(rate); rattle = new Param(rate); squeak = new Param(rate);
            whine = new Param(rate); roar = new Param(rate); wind = new Param(rate); shimmer = new Param(rate);
            frequency = new Param(rate, 60); bodyCutoff = new Param(rate, 800);
            puttFrequency = new Param(rate, 15); puttDepth = new Param(rate, 0.3);
            flutterFrequency = new Param(rate, 30); rattleCutoff = new Param(rate, 420);
            squeakCutoff = new Param(rate, 1900); whineFrequency = new Param(rate, 820);
            roarCutoff = new Param(rate, 1100); windCutoff = new Param(rate, 2500); pan = new Param(rate);
            parameters = new[] { active, volume, body, flutter, rattle, squeak, whine, roar, wind, shimmer,
                frequency, bodyCutoff, puttFrequency, puttDepth, flutterFrequency, rattleCutoff, squeakCutoff,
                whineFrequency, roarCutoff, windCutoff, pan };
            flutterFilter.Set("bandpass", 1900, 1.3, rate);
            roarHigh.Set("highpass", 150, 0.7, rate);
            shimmerFilter.Set("bandpass", 7000, 3, rate);
            for (int i = 0; i < cpus.Length; i++)
            {
                cpus[i] = new Cpu(rate);
            }
        }

        internal void SetActive(bool on)
        {
            using var sample = ControlSync.Auto();
            lock (gate)
            {
                active.Set(on ? 1 : 0, on ? 0.08 : 0.05);
            }
        }

        internal void SetVolume(float gain, bool muted)
        {
            using var sample = ControlSync.Auto();
            lock (gate)
            {
                volume.Set(muted ? 0 : gain, muted ? 0.04 : 0.05);
            }
        }

        internal void SetPlayer(float rpm, float speed, float ratio, float throttle, float level, bool air,
            bool rescuing, string surface, bool drifting, int tier, float boostTime, float boostStrength,
            float draftCharge, bool invincible, float steering)
        {
            using var sample = ControlSync.Auto();
            lock (gate)
            {
                double f0 = 58 * Math.Pow(2, rpm * 2);
                double boost = boostTime > 0 ? 1 : 0;
                double ground = air || rescuing ? 0.15 : 1;
                frequency.Set(f0, 0.025);
                bodyCutoff.Set(300 + 1500 * (0.25 + 0.75 * throttle) * (0.4 + 0.6 * Math.Min(1.2, rpm)) + boost * 1300, 0.05);
                puttFrequency.Set(f0 / 4, 0.05);
                puttDepth.Set(0.05 + 0.4 * Math.Max(0, 1 - rpm * 1.2), 0.1);
                body.Set(level * (0.13 + 0.12 * throttle + 0.06 * boost) * (rescuing ? 0.25 : 1), 0.06);
                flutterFrequency.Set(Math.Max(4, Math.Min(95, speed * 2.4)), 0.05);
                flutter.Set(level * 0.1 * Math.Min(1.2, ratio) * ground, 0.08);
                rattleCutoff.Set(surface == "sand" ? 2200 : surface == "out" ? 300 : surface == "ice" ? 5000 : 420, 0.05);
                bool rough = surface == "offroad" || surface == "out" || surface == "sand";
                double roughLevel = rough ? surface == "out" ? 0.4 : 0.32 : surface == "ice" ? 0.06 : 0;
                rattle.Set(level * roughLevel * Math.Min(1, ratio * 1.5) * ground, 0.06);
                double pitch = tier == 1 ? 1.12 : tier == 2 ? 1.26 : tier == 3 ? 1.5 : 1;
                squeakCutoff.Set(1900 * pitch, 0.03);
                whineFrequency.Set(820 * pitch, 0.03);
                squeak.Set(drifting && !air ? level * 0.2 : 0, 0.04);
                whine.Set(drifting && !air ? level * 0.05 : 0, 0.05);
                roar.Set(boostTime > 0 ? level * 0.2 * Math.Min(1, boostTime * 3) : 0, 0.06);
                roarCutoff.Set(boostTime > 0 ? 900 + 600 * (boostStrength == 0 ? 1 : boostStrength) : 900, 0.1);
                wind.Set(level * 0.07 * draftCharge, 0.1);
                windCutoff.Set(2000 + 3000 * draftCharge, 0.1);
                shimmer.Set(invincible ? level * 0.06 : 0, 0.1);
                pan.Set(Math.Max(-0.3, Math.Min(0.3, steering)), 0.1);
            }
        }

        internal void SetCpu(int index, float frequency, float cutoff, float gain, float panning, bool fresh, bool present = true)
        {
            using var sample = ControlSync.Auto();
            lock (gate)
            {
                var cpu = cpus[index];
                if (!present)
                {
                    cpu.Gain.Set(0, 0.05);
                    return;
                }
                cpu.Frequency.Set(frequency, 0.04, fresh);
                cpu.Cutoff.Set(cutoff, 0.06);
                cpu.Gain.Set(gain, present ? 0.08 : 0.05);
                if (fresh)
                {
                    cpu.Gain.ResetValue(0);
                }
                cpu.Pan.Set(panning, 0.08);
            }
        }

        internal void Silence()
        {
            lock (gate)
            {
                body.Set(0, 0.05);
                flutter.Set(0, 0.05);
                rattle.Set(0, 0.05);
                squeak.Set(0, 0.05);
                whine.Set(0, 0.05);
                roar.Set(0, 0.05);
                wind.Set(0, 0.05);
                shimmer.Set(0, 0.05);
                foreach (var cpu in cpus)
                {
                    cpu.Gain.Set(0, 0.03);
                }
                active.Set(0, 0.05);
            }
        }

        internal void Begin()
        {
            AudioBlock.Begin();
            // 块起点只复制参数；滤波历史与逐采样平滑仅由音频线程修改，不让主线程等待整块 DSP。
            lock (gate)
            {
                foreach (var parameter in parameters)
                {
                    parameter.Apply();
                }
                foreach (var cpu in cpus)
                {
                    cpu.Frequency.Apply();
                    cpu.Cutoff.Apply();
                    cpu.Gain.Apply();
                    cpu.Pan.Apply();
                }
            }
        }

        internal void End()
        {
            AudioBlock.End();
        }

        internal void Mix(ref float left, ref float right)
        {
            float w = ReadNoise(white), p = ReadNoise(pink);
            noiseCursor = (noiseCursor + (double)noiseRate / rate) % white.Length;
            double f = frequency.Tick();
            float input = (float)(0.5 * sawA.Tick("sawtooth", f, rate)
                + 0.45 * sawB.Tick("sawtooth", f * 1.007, rate) + 0.3 * sub.Tick("square", f * 0.5, rate));
            float shaped = clipper.Tick(input);
            bodyFilter.Set("lowpass", bodyCutoff.Tick(), 1.4, rate);
            double value = bodyFilter.Tick((float)shaped) * (1 + putt.Tick("sine", puttFrequency.Tick(), rate) * puttDepth.Tick()) * body.Tick();
            value += flutterFilter.Tick(w) * (0.5 + 0.5 * flutterLfo.Tick("square", flutterFrequency.Tick(), rate)) * flutter.Tick();
            rattleFilter.Set("bandpass", rattleCutoff.Tick(), 0.9, rate);
            value += rattleFilter.Tick(p) * (0.5 + 0.5 * rattleLfo.Tick("square", 17, rate)) * rattle.Tick();
            squeakFilter.Set("bandpass", squeakCutoff.Tick(), 9, rate);
            value += squeakFilter.Tick(w) * (0.5 + 0.5 * squeakLfo.Tick("sawtooth", 33, rate)) * squeak.Tick();
            double detune = whineLfo.Tick("sine", 7, rate) * 25;
            value += whineOsc.Tick("triangle", whineFrequency.Tick() * Math.Pow(2, detune / 1200), rate) * whine.Tick();
            roarLow.Set("lowpass", roarCutoff.Tick(), 0.7, rate);
            value += roarLow.Tick(roarHigh.Tick(p)) * roar.Tick();
            windFilter.Set("bandpass", windCutoff.Tick(), 0.7, rate);
            value += windFilter.Tick(w) * wind.Tick();
            value += shimmerFilter.Tick(w) * (0.5 + 0.5 * shimmerLfo.Tick("sine", 11, rate)) * shimmer.Tick();
            double a = active.Tick() * volume.Tick() * 0.42;
            Pan(value * a, pan.Tick(), ref left, ref right);
            foreach (var cpu in cpus)
            {
                double hz = cpu.Frequency.Tick();
                float cpuInput = (float)(cpu.Saw.Tick("sawtooth", hz, rate) + 0.35 * cpu.Square.Tick("square", hz * 0.5, rate));
                cpu.Filter.Set("lowpass", cpu.Cutoff.Tick(), 1.2, rate);
                Pan(cpu.Filter.Tick(cpuInput) * cpu.Gain.Tick() * a, cpu.Pan.Tick(), ref left, ref right);
            }
        }

        private float ReadNoise(float[] noise)
        {
            int index = (int)noiseCursor;
            return noise[index] + (noise[(index + 1) % noise.Length] - noise[index]) * (float)(noiseCursor - index);
        }

        private static void Pan(double value, double position, ref float left, ref float right)
        {
            double angle = (position + 1) * Math.PI / 4;
            left += (float)(value * Math.Cos(angle));
            right += (float)(value * Math.Sin(angle));
        }
    }
}
