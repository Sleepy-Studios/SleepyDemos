using System;
using System.Globalization;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作 HUD 的显示换算，不推进比赛规则或消耗比赛随机数。
    public static class KagitKartHudMotion
    {
        /// <summary>反解 CSS cubic-bezier 的时间轴；保留弹性曲线的超出值。</summary>
        /// <param name="time">归一化动画时间。</param>
        /// <param name="curve">两个控制点 x1、y1、x2、y2。</param>
        public static float Ease(float time, Vector4 curve)
        {
            time = Mathf.Clamp01(time);
            float lo = 0, hi = 1;
            for (int i = 0; i < 20; i++)
            {
                float mid = (lo + hi) * 0.5f;
                if (Bezier(mid, curve.x, curve.z) < time) lo = mid;
                else hi = mid;
            }
            return Bezier((lo + hi) * 0.5f, curve.y, curve.w);
        }

        private static float Bezier(float t, float first, float second) =>
            3 * (1 - t) * (1 - t) * t * first + 3 * (1 - t) * t * t * second + t * t * t;

        /// <summary>原作轮盘逐帧转速，临近结果时减速。</summary>
        /// <param name="left">当前剩余抽取时间。</param>
        /// <param name="duration">本次抽取总时长。</param>
        /// <param name="dt">暂停感知的 HUD 秒数。</param>
        public static double ReelStep(double left, double duration, double dt)
        {
            double k = Math.Clamp(1 - left / (duration > 0 ? duration : 1.6), 0, 1);
            return (2.5 + 17 * Math.Pow(1 - k, 1.6)) * dt;
        }

        /// <summary>原作按百分秒向下截取，中文使用小数点。</summary>
        /// <param name="seconds">全程或单圈秒数。</param>
        public static string TimeText(double seconds)
        {
            if (!(seconds >= 0) || double.IsInfinity(seconds)) seconds = 0;
            return $"{Math.Floor(seconds / 60):0}:{Math.Floor(seconds % 60):00}.{Math.Floor(seconds * 100 % 100):00}";
        }

        /// <summary>原作检查点差值保留符号与两位小数。</summary>
        /// <param name="seconds">当前成绩减最佳幽灵成绩。</param>
        public static string SplitText(double seconds) =>
            (seconds < 0 ? "−" : "+") + Math.Abs(seconds).ToString("F2", CultureInfo.InvariantCulture);
    }
}
