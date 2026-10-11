using System;
using UnityEngine;

namespace Core.Runtime.TimeControls
{
    /// 世界速度渐变的制作预设；运行时克隆曲线，不改写正式配置。
    [CreateAssetMenu(menuName = "SleepyDemos/Settings/Game Time Preset")]
    public sealed class GameTimePreset : ScriptableObject
    {
        [SerializeField, Min(0)] private float targetSpeed = 1;
        [SerializeField, Min(0)] private float durationSeconds = 0.25f;
        [SerializeField] private AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        public float TargetSpeed => targetSpeed;
        public float DurationSeconds => durationSeconds;
        /// 返回独立曲线，调用方不能通过它改变制作预设。
        public AnimationCurve Curve => CloneCurve(curve);

        /// <summary>制作工具显式配置目标速度和归一化渐变曲线。</summary>
        /// <param name="target">非负有限的目标时间倍率。</param>
        /// <param name="seconds">非负有限真实时长，零立即应用目标。</param>
        /// <param name="value">按零到一的归一化时间取样，输出将钳制为零到一。</param>
        public void Configure(float target, float seconds, AnimationCurve value)
        {
            ValidateValues(target, seconds);
            var copy = CloneCurve(value);
            targetSpeed = target;
            durationSeconds = seconds;
            curve = copy;
        }

        /// 验证目标参数和正式制作曲线。
        public void Validate()
        {
            ValidateValues(targetSpeed, durationSeconds);
            ValidateCurve(curve);
        }

        internal static AnimationCurve CloneCurve(AnimationCurve value)
        {
            ValidateCurve(value);
            return new AnimationCurve(value.keys)
            {
                preWrapMode = value.preWrapMode,
                postWrapMode = value.postWrapMode
            };
        }

        private static void ValidateValues(float target, float seconds)
        {
            if (!float.IsFinite(target) || target < 0 || !float.IsFinite(seconds) || seconds < 0)
            {
                throw new ArgumentException("世界时间预设参数必须为非负有限值。");
            }
        }

        private static void ValidateCurve(AnimationCurve value)
        {
            if (value == null || value.length < 2)
            {
                throw new ArgumentException("世界时间预设需要至少两个曲线关键帧。");
            }
            foreach (var key in value.keys)
            {
                if (!float.IsFinite(key.time) || !float.IsFinite(key.value)
                    || float.IsNaN(key.inTangent) || float.IsNaN(key.outTangent))
                {
                    throw new ArgumentException("速度渐变曲线包含非有限关键帧。");
                }
            }
        }
    }
}
