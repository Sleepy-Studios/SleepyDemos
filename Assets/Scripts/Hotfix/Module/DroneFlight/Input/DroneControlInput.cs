using System;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// 与具体输入设备无关的归一化飞行指令。
    /// </summary>
    internal readonly struct DroneControlInput
    {
        private DroneControlInput(float lift, float yaw, float forward, float right, bool hadInvalidValue)
        {
            Lift = lift;
            Yaw = yaw;
            Forward = forward;
            Right = right;
            HadInvalidValue = hadInvalidValue;
        }

        /// <summary>升降输入，范围为 [-1, 1]。</summary>
        internal float Lift { get; }

        /// <summary>偏航输入，范围为 [-1, 1]。</summary>
        internal float Yaw { get; }

        /// <summary>前后输入，范围为 [-1, 1]。</summary>
        internal float Forward { get; }

        /// <summary>左右输入，范围为 [-1, 1]。</summary>
        internal float Right { get; }

        /// <summary>原始输入是否包含非有限值。</summary>
        internal bool HadInvalidValue { get; }

        // 输入边界只在这里收口，后续控制器始终接收有限的归一化数值。
        internal static DroneControlInput Create(float lift, float yaw, float forward, float right)
        {
            var hadInvalidValue = false;
            return new DroneControlInput(
                SanitizeAxis(lift, ref hadInvalidValue),
                SanitizeAxis(yaw, ref hadInvalidValue),
                SanitizeAxis(forward, ref hadInvalidValue),
                SanitizeAxis(right, ref hadInvalidValue),
                hadInvalidValue);
        }

        private static float SanitizeAxis(float value, ref bool hadInvalidValue)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                hadInvalidValue = true;
                return 0f;
            }

            return Math.Max(-1f, Math.Min(1f, value));
        }
    }
}
