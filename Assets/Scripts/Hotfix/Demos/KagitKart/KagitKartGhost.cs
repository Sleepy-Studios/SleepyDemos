using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 当前幽灵格式，保留原作 20 Hz 位置与弧度朝向。
    [Serializable]
    public sealed class KagitKartGhostData
    {
        public int hz = 20;
        public float[] frames;
        public double[] checkpoints;
        public double total;
        public double[] lapTimes;

        /// 检查存档领域数据，拒绝损坏或非有限数值。
        public bool IsValid()
        {
            if (hz != 20 || frames == null || frames.Length < 8 || frames.Length % 4 != 0 ||
                checkpoints == null || lapTimes == null || lapTimes.Length == 0 || !Finite(total) || total <= 0)
                return false;
            if (Math.Abs((frames.Length / 4 - 1) / (double)hz - total) > 0.15)
            {
                return false;
            }
            foreach (float value in frames)
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return false;
                }
            double previous = 0;
            foreach (double value in checkpoints)
            {
                // 原作分段按毫秒量化，末段可能比未量化的完赛时间高半毫秒。
                if (!Finite(value) || value < previous || value > total + 0.0005)
                {
                    return false;
                }
                previous = value;
            }
            double sum = 0;
            foreach (double value in lapTimes)
            {
                if (!Finite(value) || value <= 0)
                {
                    return false;
                }
                sum += value;
            }
            // 起跑网格在圈线后，首圈计时开始晚于比赛时钟。
            return sum <= total + 0.01;
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// 幽灵采样不驱动赛车规则，也不消耗比赛随机流。
    public sealed class KagitKartGhostRecorder
    {
        private readonly List<float> frames = new();
        private readonly List<double> checkpoints = new();
        private double next;
        private double previousTime = -1;

        /// <summary>从固定模拟步记录，不能用渲染帧插值结果代替。</summary>
        /// <param name="position">规则位置。</param>
        /// <param name="heading">弧度朝向。</param>
        /// <param name="raceTime">单调递增的比赛秒数。</param>
        public void Record(Vector3 position, float heading, double raceTime)
        {
            if (double.IsNaN(raceTime) || double.IsInfinity(raceTime) || raceTime < 0 || raceTime < previousTime ||
                !Finite(position.x) || !Finite(position.y) || !Finite(position.z) || !Finite(heading))
                throw new ArgumentOutOfRangeException(nameof(raceTime));
            previousTime = raceTime;
            if (raceTime < next)
            {
                return;
            }
            next += 1.0 / 20;
            frames.Add(position.x);
            frames.Add(position.y);
            frames.Add(position.z);
            frames.Add(heading);
        }

        /// <summary>记录检查点或圈线时间。</summary>
        /// <param name="raceTime">该规则步的比赛秒数。</param>
        public void Mark(double raceTime)
        {
            if (double.IsNaN(raceTime) || double.IsInfinity(raceTime) || raceTime < 0 ||
                checkpoints.Count > 0 && raceTime < checkpoints[checkpoints.Count - 1])
                throw new ArgumentOutOfRangeException(nameof(raceTime));
            checkpoints.Add(raceTime);
        }

        /// <summary>创建独立快照，按原作只在保存时进行厘米与毫弧度量化。</summary>
        /// <param name="total">实际完赛时间。</param>
        /// <param name="lapTimes">实际圈速。</param>
        /// <returns>可以交给领域校验与公共存储的数据。</returns>
        public KagitKartGhostData Snapshot(double total, IReadOnlyList<double> lapTimes)
        {
            if (lapTimes == null)
            {
                throw new ArgumentNullException(nameof(lapTimes));
            }
            var data = new KagitKartGhostData
            {
                total = total,
                frames = new float[frames.Count],
                checkpoints = new double[checkpoints.Count],
                lapTimes = new double[lapTimes.Count]
            };
            for (int i = 0; i < frames.Count; i++)
            {
                double precision = i % 4 == 3 ? 1000 : 100;
                // Math.Round 的银行家舍入与 JavaScript Math.round 不同。
                data.frames[i] = (float)(Math.Floor(frames[i] * precision + 0.5) / precision);
            }
            for (int i = 0; i < checkpoints.Count; i++)
                data.checkpoints[i] = Math.Floor(checkpoints[i] * 1000 + 0.5) / 1000;
            for (int i = 0; i < lapTimes.Count; i++)
            {
                data.lapTimes[i] = lapTimes[i];
            }
            if (!data.IsValid())
            {
                throw new ArgumentException("幽灵记录不完整，不能保存。");
            }
            return data;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }

    /// 幽灵只输出表现位置，不参加碰撞和排名。
    public static class KagitKartGhostPlayback
    {
        /// <summary>使用原作最短弧度差插值；结束后隐藏。</summary>
        /// <param name="data">已通过 IsValid 的记录。</param>
        /// <param name="raceTime">当前比赛秒数。</param>
        /// <param name="position">回放位置。</param>
        /// <param name="heading">回放朝向，弧度。</param>
        /// <returns>该时刻是否有可见幽灵。</returns>
        public static bool TrySample(KagitKartGhostData data, double raceTime, out Vector3 position, out float heading)
        {
            position = default;
            heading = 0;
            if (data == null || data.frames == null || data.frames.Length < 8 || data.frames.Length % 4 != 0 ||
                data.hz != 20 || double.IsNaN(raceTime) || double.IsInfinity(raceTime) || raceTime < 0)
                return false;
            double frame = raceTime * data.hz;
            if (frame >= data.frames.Length / 4 - 1)
            {
                return false;
            }
            int index = (int)frame;
            float weight = (float)(frame - index);
            int offset = index * 4;
            float[] values = data.frames;
            position = new Vector3(
                Mathf.LerpUnclamped(values[offset], values[offset + 4], weight),
                Mathf.LerpUnclamped(values[offset + 1], values[offset + 5], weight),
                Mathf.LerpUnclamped(values[offset + 2], values[offset + 6], weight));
            double delta = values[offset + 7] - values[offset + 3];
            heading = values[offset + 3] + (float)Math.Atan2(Math.Sin(delta), Math.Cos(delta)) * weight;
            return true;
        }
    }
}
