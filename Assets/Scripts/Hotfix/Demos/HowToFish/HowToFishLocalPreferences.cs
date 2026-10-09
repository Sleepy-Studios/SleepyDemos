using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Hotfix.HowToFish
{
    /// 本机输入偏好，与航程槽和收藏解锁隔离；编辑时使用独立副本。
    [Serializable]
    public sealed class HowToFishLocalPreferences
    {
        public const int CurrentVersion = 1;
        [JsonProperty(Required = Required.Always)]
        public int Version = CurrentVersion;
        [JsonProperty(Required = Required.Always)]
        public float MouseSensitivity = .1f;
        [JsonProperty(Required = Required.Always)]
        public float GamepadSensitivity = 135;
        [JsonProperty(Required = Required.Always)]
        public float DeadZone = .15f;
        public bool InvertY;
        [JsonProperty(Required = Required.Always)]
        public string Bindings = "";
        /// 供设置面板保存进入前快照或编辑草稿。
        public HowToFishLocalPreferences Copy() => (HowToFishLocalPreferences)MemberwiseClone();
        /// 拒绝非有限参数、越界参数及损坏的绑定覆盖；不隐式修正或写盘。
        [JsonIgnore]
        public bool IsValid => Version == CurrentVersion && FiniteInRange(MouseSensitivity, .01f, 1) && FiniteInRange(GamepadSensitivity, 15, 360) && FiniteInRange(DeadZone, 0, .9f) && Bindings != null && AreBindingsValid(Bindings);

        /// <summary>校验待读写的完整输入偏好。</summary>
        /// <param name="value">当前版本的完整参数与绑定覆盖。</param>
        public static void Validate(HowToFishLocalPreferences value)
        {
            if (value == null || !value.IsValid)
                throw new ArgumentException("输入设置无效。", nameof(value));
        }

        /// <summary>检查 Input System 覆盖 JSON 的基本结构；空值代表默认绑定。</summary>
        /// <param name="json">SaveBindingOverridesAsJson 导出的路径覆盖，仅校验数据格式，不加载输入设备。</param>
        public static bool AreBindingsValid(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return true;
            if (json.Length > 65536)
                return false;
            try
            {
                var envelope = JsonUtility.FromJson<BindingOverrides>(json);
                if (envelope?.bindings == null || envelope.bindings.Length > 256)
                    return false;
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in envelope.bindings)
                    if (entry == null || !Guid.TryParse(entry.id, out _) || !ids.Add(entry.id) || string.IsNullOrWhiteSpace(entry.action) || !IsControlPath(entry.path) || entry.interactions != "null" || entry.processors != "null")
                        return false;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static bool IsControlPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 512)
                return false;
            int separator = path.IndexOf(">/", StringComparison.Ordinal);
            return path[0] == '<' && separator > 1 && separator + 2 < path.Length;
        }

        private static bool FiniteInRange(float value, float minimum, float maximum) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= minimum && value <= maximum;
        // 使用 Input System 自身的 JSON 字段名；不引入另一个绑定存档格式。
        [Serializable]
        private sealed class BindingOverrides
        {
            public BindingOverride[] bindings;
        }

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
}
