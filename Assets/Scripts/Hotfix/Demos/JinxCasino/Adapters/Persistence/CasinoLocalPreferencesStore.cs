using System;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters.Persistence
{
    /// 本机输入/音量偏好；不属于旅程快照或正式成长档案。
    [Serializable]
    public sealed class CasinoLocalPreferences
    {
        public int SchemaVersion = 1;
        public float PcLookMultiplier = 1;
        public float TouchLookMultiplier = 1;
        public float Volume = 0.7f;
        public bool Muted;
        public bool LeftHanded;

        /// 独立副本供设置面板预览，避免编辑当前已保存的值。
        public CasinoLocalPreferences Copy() => (CasinoLocalPreferences)MemberwiseClone();

        /// 有限数及明确版本验证；坏旧值必须退回完整默认配置。
        public bool IsValid => SchemaVersion == 1 && FiniteInRange(PcLookMultiplier, 0.25f, 3)
            && FiniteInRange(TouchLookMultiplier, 0.25f, 3) && FiniteInRange(Volume, 0, 1);

        private static bool FiniteInRange(float value, float minimum, float maximum)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
    }

    /// 独立PlayerPrefs键，损坏数据只读回退；只有用户明确保存才覆写。
    public sealed class CasinoLocalPreferencesStore
    {
        public const string DefaultKey = "JinxCasino.LocalPreferences.v1";
        private readonly string key;
        public string LastLoadWarning { get; private set; }

        // 读取记录故意没有字段默认值：JsonUtility无论是否运行字段初始化，缺失版本/倍率都不能被误认作合法保存。
        [Serializable]
        private sealed class PreferencesRecord
        {
            public int SchemaVersion;
            public float PcLookMultiplier;
            public float TouchLookMultiplier;
            public float Volume;
            public bool Muted;
            public bool LeftHanded;
        }

        /// <summary>绑定本机偏好键；测试使用独立JinxCasino前缀UUID键。</summary>
        /// <param name="preferenceKey">与旅程及档案隔离的PlayerPrefs键。</param>
        public CasinoLocalPreferencesStore(string preferenceKey = DefaultKey)
        {
            if (string.IsNullOrWhiteSpace(preferenceKey) || !preferenceKey.StartsWith("JinxCasino.", StringComparison.Ordinal))
                throw new ArgumentException("本地偏好须使用JinxCasino命名空间。", nameof(preferenceKey));
            key = preferenceKey;
        }

        /// 不存在或损坏时返回完整默认偏好，不自动保存或清理已有键。
        public CasinoLocalPreferences Load()
        {
            LastLoadWarning = null;
            if (!PlayerPrefs.HasKey(key)) return new CasinoLocalPreferences();
            try
            {
                string json = PlayerPrefs.GetString(key);
                if (json.Length > 4096) throw new ArgumentException("偏好数据超出长度限制。");
                var record = JsonUtility.FromJson<PreferencesRecord>(json);
                var value = record == null ? null : new CasinoLocalPreferences { SchemaVersion = record.SchemaVersion,
                    PcLookMultiplier = record.PcLookMultiplier, TouchLookMultiplier = record.TouchLookMultiplier,
                    Volume = record.Volume, Muted = record.Muted, LeftHanded = record.LeftHanded };
                if (value == null || !value.IsValid) throw new ArgumentException("偏好数据版本或参数无效。");
                return value.Copy();
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                LastLoadWarning = "本地偏好无效，已使用默认值；明确点击保存后才替换原偏好。";
                return new CasinoLocalPreferences();
            }
        }

        /// <summary>保存用户确认的有限值偏好；写盘失败恢复本键内存值。</summary>
        /// <param name="preferences">版本1、输入倍率0.25至3、音量0至1。</param>
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
