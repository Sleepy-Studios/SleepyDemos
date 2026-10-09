using System;
using Core.Runtime.Inputs;
using UnityEngine;
using Newtonsoft.Json;

namespace Hotfix.JinxCasino.Persistence
{
    /// 本机输入/音量偏好；当前完整记录包含手柄参数，不属于旅程快照或正式成长档案。
    [Serializable]
    public sealed class CasinoLocalPreferences
    {
        public const int CurrentSchemaVersion = 2;
        [JsonProperty(Required = Required.Always)]
        public int SchemaVersion = CurrentSchemaVersion;
        [JsonProperty(Required = Required.Always)]
        public float PcLookMultiplier = 1;
        [JsonProperty(Required = Required.Always)]
        public float TouchLookMultiplier = 1;
        [JsonProperty(Required = Required.Always)]
        public float Volume = 0.7f;
        public bool Muted;
        public bool LeftHanded;
        [JsonProperty(Required = Required.Always)]
        public float GamepadDeadzone = 0.2f;
        [JsonProperty(Required = Required.Always)]
        public float GamepadMaximum = 1;
        [JsonProperty(Required = Required.Always)]
        public float GamepadLookDegreesPerSecond = 90;
        [JsonProperty(Required = Required.Always)]
        public float GamepadLookMultiplier = 1;
        public bool GamepadInvertY;
        public bool RumbleEnabled = true;
        [JsonProperty(Required = Required.Always)]
        public float RumbleStrength = 1;
        /// 独立副本供设置面板预览，避免编辑当前已保存的值。
        public CasinoLocalPreferences Copy() => (CasinoLocalPreferences)MemberwiseClone();
        /// 当前版本有限值校验；手柄范围统一复用公共输入参数，坏值退回完整默认配置。
        [JsonIgnore]
        public bool IsValid => SchemaVersion == CurrentSchemaVersion && FiniteInRange(Volume, 0, 1) && ToInputSettings().IsValid;

        /// <summary>校验待读写的本机偏好，不修改输入或文件。</summary>
        /// <param name="value">当前版本完整的输入与音量参数。</param>
        public static void Validate(CasinoLocalPreferences value)
        {
            if (value == null || !value.IsValid)
                throw new ArgumentException("本地偏好无效。", nameof(value));
        }

        /// 给Router的独立参数副本；输入DTO版本与偏好存储版本分别管理，不共享可变对象。
        public GameplayInputSettings ToInputSettings() => new GameplayInputSettings
        {
            MouseLookMultiplier = PcLookMultiplier,
            TouchLookMultiplier = TouchLookMultiplier,
            GamepadDeadzone = GamepadDeadzone,
            GamepadMaximum = GamepadMaximum,
            GamepadLookDegreesPerSecond = GamepadLookDegreesPerSecond,
            GamepadLookMultiplier = GamepadLookMultiplier,
            GamepadInvertY = GamepadInvertY,
            RumbleEnabled = RumbleEnabled,
            RumbleStrength = RumbleStrength
        };
        private static bool FiniteInRange(float value, float minimum, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
    }
}
