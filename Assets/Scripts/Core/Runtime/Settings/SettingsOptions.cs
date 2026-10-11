using System;
using UnityEngine;

namespace Core.Runtime.Settings
{
    /// 制作侧允许的编辑范围；不扩大运行时快照的安全上限。
    [Serializable]
    public sealed class SettingsOptions
    {
        public Vector2 Brightness = new(0.5f, 2);
        public Vector2 LookMultiplier = new(0.25f, 3);
        public Vector2 GamepadAngularSpeed = new(30, 240);
        public Vector2 GamepadDeadzone = new(0, 0.45f);
        public Vector2 GamepadMaximum = new(0.65f, 1);
        public Vector2 RumbleStrength = new(0, 1);
        public int[] FrameLimits = { 0, 30, 60, 90, 120, 144, 165, 240, 360 };

        /// 返回独立的控件编辑范围。
        public SettingsOptions Copy()
        {
            var result = (SettingsOptions)MemberwiseClone();
            result.FrameLimits = FrameLimits == null ? null : (int[])FrameLimits.Clone();
            return result;
        }

        /// 制作范围必须保持在公共运行时安全范围内。
        public void Validate()
        {
            CheckRange(Brightness, 0.5f, 2);
            CheckRange(LookMultiplier, 0.25f, 3);
            CheckRange(GamepadAngularSpeed, 30, 240);
            CheckRange(GamepadDeadzone, 0, 0.45f);
            CheckRange(GamepadMaximum, 0.65f, 1);
            CheckRange(RumbleStrength, 0, 1);
            if (FrameLimits == null || FrameLimits.Length == 0)
            {
                throw new ArgumentException("缺少帧率选项。");
            }
            var unique = new System.Collections.Generic.HashSet<int>();
            foreach (int limit in FrameLimits)
            {
                if (limit < 0 || limit > 360 || !unique.Add(limit))
                {
                    throw new ArgumentException("帧率选项无效或重复。");
                }
            }
        }

        /// <summary>校验玩家候选遵守制作侧范围。</summary>
        /// <param name="value">已经通过基础类型及有限值校验的完整候选。</param>
        public void ValidateCandidate(GameSettingsSnapshot value)
        {
            Require(value.Graphics.Brightness, Brightness);
            Require(value.Input.MouseLookMultiplier, LookMultiplier);
            Require(value.Input.TouchLookMultiplier, LookMultiplier);
            Require(value.Input.GamepadLookMultiplier, LookMultiplier);
            Require(value.Input.GamepadLookDegreesPerSecond, GamepadAngularSpeed);
            Require(value.Input.GamepadDeadzone, GamepadDeadzone);
            Require(value.Input.GamepadMaximum, GamepadMaximum);
            Require(value.Input.RumbleStrength, RumbleStrength);
            if (value.Graphics.FrameLimit >= 0 && Array.IndexOf(FrameLimits, value.Graphics.FrameLimit) < 0)
            {
                throw new ArgumentException("候选帧率不在制作选项中。");
            }
        }

        private static void CheckRange(Vector2 range, float minimum, float maximum)
        {
            if (!GraphicsPreferences.FiniteRange(range.x, minimum, maximum)
                || !GraphicsPreferences.FiniteRange(range.y, minimum, maximum) || range.x > range.y)
            {
                throw new ArgumentException("制作侧设置范围无效。");
            }
        }

        private static void Require(float value, Vector2 range)
        {
            if (!GraphicsPreferences.FiniteRange(value, range.x, range.y))
            {
                throw new ArgumentException("候选超出制作侧允许范围。");
            }
        }
    }
}
