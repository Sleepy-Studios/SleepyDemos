using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 本机输入偏好，与航程槽和收藏解锁隔离；编辑时使用独立副本。
    [Serializable]
    public sealed class HowToFishLocalPreferences
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public float MouseSensitivity = .1f;
        public float GamepadSensitivity = 135;
        public float DeadZone = .15f;
        public bool InvertY;
        public string Bindings = "";

        /// 供设置面板保存进入前快照或编辑草稿。
        public HowToFishLocalPreferences Copy() => (HowToFishLocalPreferences)MemberwiseClone();

        /// 拒绝非有限参数、越界参数及损坏的绑定覆盖；不隐式修正或写盘。
        public bool IsValid => Version == CurrentVersion && FiniteInRange(MouseSensitivity, .01f, 1) &&
            FiniteInRange(GamepadSensitivity, 15, 360) && FiniteInRange(DeadZone, 0, .9f) && Bindings != null && AreBindingsValid(Bindings);

        /// <summary>检查 Input System 覆盖 JSON 的基本结构；空值代表默认绑定。</summary>
        /// <param name="json">SaveBindingOverridesAsJson 导出的路径覆盖，仅校验数据格式，不加载输入设备。</param>
        public static bool AreBindingsValid(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return true;
            if (json.Length > 65536) return false;
            try
            {
                var envelope = JsonUtility.FromJson<BindingOverrides>(json);
                if (envelope?.bindings == null || envelope.bindings.Length > 256) return false;
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in envelope.bindings)
                    if (entry == null || !Guid.TryParse(entry.id, out _) || !ids.Add(entry.id) ||
                        string.IsNullOrWhiteSpace(entry.action) || !IsControlPath(entry.path) ||
                        entry.interactions != "null" || entry.processors != "null") return false;
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        private static bool IsControlPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 512) return false;
            int separator = path.IndexOf(">/", StringComparison.Ordinal);
            return path[0] == '<' && separator > 1 && separator + 2 < path.Length;
        }

        private static bool FiniteInRange(float value, float minimum, float maximum)
            => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;

        // 使用 Input System 自身的 JSON 字段名；不引入另一个绑定存档格式。
        [Serializable]
        private sealed class BindingOverrides { public BindingOverride[] bindings; }
        [Serializable]
        private sealed class BindingOverride
        {
            public string action;
            public string id;
            public string path;
            public string interactions;
            public string processors;
        }
    }

    /// 单个本机偏好键；损坏记录保留，只有用户明确保存才覆盖。
    public sealed class HowToFishLocalPreferencesStore
    {
        public const string DefaultKey = "HowToFish.LocalPreferences";
        private readonly string key;
        public string LastLoadWarning { get; private set; }

        /// <summary>绑定本机偏好键，不读取或写入记录。</summary>
        /// <param name="preferenceKey">正式键使用默认值；测试可传 HowToFish 前缀的独立键。</param>
        public HowToFishLocalPreferencesStore(string preferenceKey = DefaultKey)
        {
            if (string.IsNullOrWhiteSpace(preferenceKey) || !preferenceKey.StartsWith("HowToFish.", StringComparison.Ordinal))
                throw new ArgumentException("本机偏好须使用 HowToFish 命名空间。", nameof(preferenceKey));
            key = preferenceKey;
        }

        /// 加载合法副本；不存在或损坏时返回默认值，损坏记录保留并通过 LastLoadWarning 提示。
        public HowToFishLocalPreferences Load()
        {
            LastLoadWarning = null;
            if (!PlayerPrefs.HasKey(key)) return new HowToFishLocalPreferences();
            try
            {
                string json = PlayerPrefs.GetString(key);
                if (json.Length > 131072) throw new ArgumentException("偏好记录过大。");
                var value = new HowToFishLocalPreferences
                {
                    Version = 0, MouseSensitivity = float.NaN, GamepadSensitivity = float.NaN,
                    DeadZone = float.NaN, Bindings = null
                };
                JsonUtility.FromJsonOverwrite(json, value);
                if (value.Bindings == null || !value.IsValid) throw new ArgumentException("本机偏好无效。");
                return value;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                LastLoadWarning = "输入设置损坏，已使用默认值；点击保存后才会替换原设置。";
                return new HowToFishLocalPreferences();
            }
        }

        /// <summary>明确保存完整记录；失败恢复 PlayerPrefs 内存中的原记录并抛出，供面板保持打开和提示。</summary>
        /// <param name="preferences">本次确认的合法四项参数及绑定覆盖。</param>
        public void Save(HowToFishLocalPreferences preferences)
        {
            if (preferences == null || !preferences.IsValid) throw new ArgumentException("输入设置无效。", nameof(preferences));
            string json = JsonUtility.ToJson(preferences);
            bool existed = PlayerPrefs.HasKey(key);
            string previous = existed ? PlayerPrefs.GetString(key) : null;
            try
            {
                PlayerPrefs.SetString(key, json);
                PlayerPrefs.Save();
                LastLoadWarning = null;
            }
            catch
            {
                if (existed) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key);
                throw;
            }
        }
    }
}
