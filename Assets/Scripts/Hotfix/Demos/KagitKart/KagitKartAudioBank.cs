using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 固定原作版本离线生成的曲目、纸质音效和引擎分层。
    public sealed class KagitKartAudioBank : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips;
        private Dictionary<string, AudioClip> lookup;

        private void OnValidate() => lookup = null;

        /// <summary>读取原作配方名对应的音频；资产缺失直接报错，避免无声占位。</summary>
        /// <param name="name">manifest 中的完整名称，不包含扩展名。</param>
        /// <returns>此 Bank 持有的共享 AudioClip。</returns>
        public AudioClip GetClip(string name)
        {
            if (lookup == null)
            {
                lookup = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
                foreach (var clip in clips)
                {
                    if (clip == null)
                    {
                        throw new InvalidOperationException("纸艺卡丁车音频引用缺失。");
                    }
                    string[] segments = clip.name.Split('_');
                    for (int i = 0; i < segments.Length; i++)
                    {
                        segments[i] = int.TryParse(segments[i], out int variant)
                            ? variant.ToString() : char.ToLowerInvariant(segments[i][0]) + segments[i].Substring(1);
                    }
                    lookup.Add(string.Join("_", segments), clip);
                }
            }
            return lookup.TryGetValue(name, out var result)
                ? result : throw new ArgumentException("纸艺卡丁车原作音频缺失：" + name, nameof(name));
        }
    }
}
