using System;
using System.Collections.Generic;

namespace Hotfix.KagitKart
{
    /// 原作起步判定。
    public enum KagitKartStartGrade
    {
        None,
        Perfect,
        Good,
        Burnout
    }

    /// 用整数模拟步计数，避免浮点残差重复起跑。
    public sealed class KagitKartCountdown
    {
        private int remaining = 360;
        private bool wasThrottleHeld;
        private double lastThrottlePress = -99;

        /// 倒计时；零表示已起跑。
        public int Number => (remaining + 119) / 120;
        internal double SecondsToGo => remaining * KagitKartSimulationClock.Step;
        /// 起步结果，只在起跑时计算一次。
        public KagitKartStartGrade Grade { get; private set; }

        /// <summary>采样油门并推进一个固定步。</summary>
        /// <param name="throttleHeld">油门超过 0.5。</param>
        /// <returns>仅起跑的那个模拟步为 true。</returns>
        public bool Advance(bool throttleHeld)
        {
            if (remaining == 0)
            {
                return false;
            }
            remaining--;
            if (throttleHeld && !wasThrottleHeld)
                lastThrottlePress = -remaining * KagitKartSimulationClock.Step;
            wasThrottleHeld = throttleHeld;
            if (remaining != 0)
            {
                return false;
            }
            if (throttleHeld)
            {
                if (lastThrottlePress >= -1 && lastThrottlePress <= -0.65)
                    Grade = KagitKartStartGrade.Perfect;
                else if (lastThrottlePress > -0.65 && lastThrottlePress <= -0.3)
                    Grade = KagitKartStartGrade.Good;
                else if (lastThrottlePress >= -3 && lastThrottlePress < -2)
                    Grade = KagitKartStartGrade.Burnout;
            }
            return true;
        }
    }

    /// 原作检查点与圈线规则；位置来自赛道查询，不由 UI 或碰撞触发器计圈。
    public sealed class KagitKartRaceProgress
    {
        private static readonly double[] Checkpoints = { 0.25, 0.5, 0.75 };
        private readonly double length;
        private readonly int laps;
        private double previousDistance;
        private double lapStartedAt;
        private int crossings;
        private int nextCheckpoint;
        private double wrongSeconds;
        private double rightSeconds;
        private readonly List<double> lapTimes = new();

        /// 当前圈；零表示尚未穿过起跑线。
        public int Lap { get; private set; }
        /// 相对起跑线的累计进度，排名使用该值。
        public double Progress { get; private set; }
        /// 限制瞬移增长的最大已到达进度。
        public double MaxProgress { get; private set; }
        /// 已通过本圈的检查点数。
        public int PassedCheckpoints => nextCheckpoint;
        /// 完成后不再计圈。
        public bool Finished { get; private set; }
        /// 实际冲线时间。
        public double FinishTime { get; private set; }
        /// 每圈用时。
        public IReadOnlyList<double> LapTimes => lapTimes;
        /// 持续逆行达到阈值后的警告状态。
        public bool WrongWay { get; private set; }
        public event Action<int, int, double> CheckpointCrossed;

        /// <summary>创建一辆赛车的进度，网格应放在起跑线后。</summary>
        /// <param name="length">赛道米数，必须超过原作圈线检测区的 120 米。</param>
        /// <param name="laps">比赛圈数。</param>
        /// <param name="initialDistance">起跑线相对距离，范围 [0,length)。</param>
        public KagitKartRaceProgress(double length, int laps, double initialDistance)
        {
            if (!Finite(length) || length <= 120)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (laps < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(laps));
            }
            ValidateDistance(initialDistance, length);
            this.length = length;
            this.laps = laps;
            previousDistance = initialDistance;
            Progress = MaxProgress = initialDistance - length;
        }

        /// <summary>迁移原作赛道距离、最大进度限速与顺序检查点判定。</summary>
        /// <param name="distance">相对起跑线的当前距离。</param>
        /// <param name="speed">当前有符号速度，米每秒。</param>
        /// <param name="seconds">固定步长秒数。</param>
        /// <param name="raceTime">起跑后的比赛时间。</param>
        public void Advance(double distance, double speed, double seconds, double raceTime)
        {
            ValidateDistance(distance, length);
            if (!Finite(speed) || !Finite(seconds) || seconds < 0 || !Finite(raceTime) || raceTime < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (previousDistance > length - 60 && distance < 60)
            {
                crossings++;
            }
            else if (previousDistance < 60 && distance > length - 60)
            {
                crossings--;
            }
            previousDistance = distance;
            Progress = (crossings - 1) * length + distance;
            MaxProgress = Math.Min(Math.Max(MaxProgress, Progress), MaxProgress + 2 * Math.Abs(speed) * seconds + 1);
            if (Finished)
            {
                return;
            }
            if (Lap >= 1)
            {
                while (nextCheckpoint < Checkpoints.Length &&
                       MaxProgress >= (Lap - 1) * length + Checkpoints[nextCheckpoint] * length)
                {
                    int checkpoint = nextCheckpoint++;
                    CheckpointCrossed?.Invoke(checkpoint, Lap, raceTime);
                }
            }
            if (crossings <= Lap)
            {
                return;
            }
            bool valid = Lap == 0 || nextCheckpoint == Checkpoints.Length &&
                MaxProgress >= (Lap - 1) * length + Checkpoints[Checkpoints.Length - 1] * length;
            if (!valid)
            {
                return;
            }
            if (Lap >= 1)
            {
                lapTimes.Add(raceTime - lapStartedAt);
            }
            Lap++;
            lapStartedAt = raceTime;
            nextCheckpoint = 0;
            if (Lap > laps)
            {
                Finished = true;
                FinishTime = raceTime;
            }
        }

        /// <summary>迁移原作逆行显示门闩，不影响实际运动。</summary>
        /// <param name="forwardDot">车头与赛道切线的点积。</param>
        /// <param name="speed">当前有符号速度。</param>
        /// <param name="respawning">救援期间不显示逆行。</param>
        /// <param name="seconds">固定步长秒数。</param>
        public void UpdateWrongWay(double forwardDot, double speed, bool respawning, double seconds)
        {
            if (!Finite(forwardDot) || !Finite(speed) || !Finite(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            if (!Finished && !respawning && forwardDot < -0.3 && Math.Abs(speed) > 2)
            {
                wrongSeconds += seconds;
                rightSeconds = 0;
                if (wrongSeconds >= 1.5)
                {
                    WrongWay = true;
                }
            }
            else
            {
                rightSeconds += seconds;
                wrongSeconds = 0;
                if (rightSeconds >= 0.5)
                {
                    WrongWay = false;
                }
            }
        }

        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static void ValidateDistance(double distance, double length)
        {
            if (!Finite(distance) || distance < 0 || distance >= length)
                throw new ArgumentOutOfRangeException(nameof(distance));
        }
    }
}
