using System;

namespace Hotfix.KagitKart
{
    /// Demo 独立固定步长时钟，不修改项目 Time 设置。
    public sealed class KagitKartSimulationClock
    {
        /// 原作每秒 120 次模拟。
        public const double Step = 1.0 / 120;
        private double accumulator;

        /// 当前渲染插值比例。
        public double Alpha => accumulator / Step;

        /// <summary>推进一帧，按原作限制单帧时间和追赶次数；暂停时丢弃积压。</summary>
        /// <param name="frameSeconds">有限且非负的帧时间。</param>
        /// <param name="paused">暂停时不执行规则。</param>
        /// <param name="simulate">每个固定步长执行一次的规则入口。</param>
        /// <returns>本帧执行的模拟步数。</returns>
        public int Advance(double frameSeconds, bool paused, Action<double> simulate)
        {
            if (double.IsNaN(frameSeconds) || double.IsInfinity(frameSeconds) || frameSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(frameSeconds));
            if (simulate == null)
            {
                throw new ArgumentNullException(nameof(simulate));
            }
            if (paused)
            {
                accumulator = 0;
                return 0;
            }
            accumulator += Math.Min(frameSeconds, 0.1);
            int count = 0;
            while (accumulator >= Step && count < 8)
            {
                simulate(Step);
                accumulator -= Step;
                count++;
            }
            if (accumulator > Step)
            {
                accumulator = Step * 0.999;
            }
            return count;
        }
    }
}
