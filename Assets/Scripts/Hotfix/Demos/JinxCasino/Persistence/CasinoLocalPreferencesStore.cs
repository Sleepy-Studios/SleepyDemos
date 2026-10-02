using System;
using Core.Runtime.Inputs;
using UnityEngine;

namespace Hotfix.JinxCasino.Persistence
{
    /// 本机输入/音量偏好；当前完整记录包含手柄参数，不属于旅程快照或正式成长档案。
    [Serializable]
    public sealed class CasinoLocalPreferences
    {
        public const int CurrentSchemaVersion = 2;
        public int SchemaVersion = CurrentSchemaVersion;
        public float PcLookMultiplier = 1;
        public float TouchLookMultiplier = 1;
        public float Volume = 0.7f;
        public bool Muted;
        public bool LeftHanded;
        public float GamepadDeadzone = 0.2f;
        public float GamepadMaximum = 1;
        public float GamepadLookDegreesPerSecond = 90;
        public float GamepadLookMultiplier = 1;
        public bool GamepadInvertY;
        public bool RumbleEnabled = true;
        public float RumbleStrength = 1;

        /// 独立副本供设置面板预览，避免编辑当前已保存的值。
        public CasinoLocalPreferences Copy() => (CasinoLocalPreferences)MemberwiseClone();

        /// 当前版本有限值校验；手柄范围统一复用公共输入参数，坏值退回完整默认配置。
        public bool IsValid => SchemaVersion == CurrentSchemaVersion && FiniteInRange(Volume, 0, 1) && ToInputSettings().IsValid;

        /// 给Router的独立参数副本；输入DTO版本与偏好存储版本分别管理，不共享可变对象。
        public GameplayInputSettings ToInputSettings() => new GameplayInputSettings
        {
            MouseLookMultiplier = PcLookMultiplier, TouchLookMultiplier = TouchLookMultiplier,
            GamepadDeadzone = GamepadDeadzone, GamepadMaximum = GamepadMaximum,
            GamepadLookDegreesPerSecond = GamepadLookDegreesPerSecond, GamepadLookMultiplier = GamepadLookMultiplier,
            GamepadInvertY = GamepadInvertY, RumbleEnabled = RumbleEnabled, RumbleStrength = RumbleStrength
        };

        private static bool FiniteInRange(float value, float minimum, float maximum)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
    }

    /// 新原型独立偏好键，只读取完整当前记录；损坏回退不自动写盘，明确保存才替换。
    public sealed class CasinoLocalPreferencesStore
    {
        public const string DefaultKey = "JinxCasino.PrototypeV2.LocalPreferences";
        private readonly string key;
        public string LastLoadWarning { get; private set; }

        /// <summary>绑定本机偏好键；测试使用独立JinxCasino前缀UUID键。</summary>
        /// <param name="preferenceKey">与旅程及档案隔离的PlayerPrefs键。</param>
        public CasinoLocalPreferencesStore(string preferenceKey = DefaultKey)
        {
            if (string.IsNullOrWhiteSpace(preferenceKey) || !preferenceKey.StartsWith("JinxCasino.", StringComparison.Ordinal))
                throw new ArgumentException("本地偏好须使用JinxCasino命名空间。", nameof(preferenceKey));
            key = preferenceKey;
        }

        /// 不存在或损坏时返回完整默认值；不搜索旧键、不补齐旧参数、不隐式保存。
        public CasinoLocalPreferences Load()
        {
            LastLoadWarning = null;
            if (!PlayerPrefs.HasKey(key)) return new CasinoLocalPreferences();
            try
            {
                string json = PlayerPrefs.GetString(key);
                if (json.Length > 4096) throw new ArgumentException("偏好数据超出长度限制。");
                // 缺失版本或数值必须拒绝，不能把缺失死区/震动强度读成合法的0。
                var value = new CasinoLocalPreferences
                {
                    SchemaVersion = 0,
                    PcLookMultiplier = float.NaN, TouchLookMultiplier = float.NaN, Volume = float.NaN,
                    GamepadDeadzone = float.NaN, GamepadMaximum = float.NaN, GamepadLookDegreesPerSecond = float.NaN,
                    GamepadLookMultiplier = float.NaN, RumbleStrength = float.NaN
                };
                JsonUtility.FromJsonOverwrite(json, value);
                if (!value.IsValid) throw new ArgumentException("偏好参数无效。");
                return value.Copy();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                LastLoadWarning = "本地偏好无效，已使用默认值；明确点击保存后才替换原偏好。";
                return new CasinoLocalPreferences();
            }
        }

        /// <summary>用户明确确认后保存完整当前记录；写盘失败恢复本键内存值。</summary>
        /// <param name="preferences">当前版本、有限合法的输入/音量/左右手参数。</param>
        public void Save(CasinoLocalPreferences preferences)
        {
            if (preferences == null || !preferences.IsValid) throw new ArgumentException("无法保存无效本地偏好。", nameof(preferences));
            bool existed = PlayerPrefs.HasKey(key); string previous = existed ? PlayerPrefs.GetString(key) : null;
            try { PlayerPrefs.SetString(key, JsonUtility.ToJson(preferences)); PlayerPrefs.Save(); LastLoadWarning = null; }
            catch
            {
                if (existed) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                throw;
            }
        }
    }
}
