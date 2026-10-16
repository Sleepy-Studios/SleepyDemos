using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 原作短效与引擎持有采样；音乐只记录地址，由音频宿主按曲持有句柄。
    public sealed class KagitKartAudioBank : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips;
        [SerializeField] private string[] musicAddresses;
        private Dictionary<string, AudioClip> lookup;
        private Dictionary<string, string> musicLookup;
        internal IReadOnlyList<AudioClip> Clips => clips;
        internal IReadOnlyList<string> MusicAddresses => musicAddresses;

        private void OnValidate()
        {
            lookup = null;
            musicLookup = null;
        }

        internal void CopyEffectsTo(Dictionary<string, AudioClip> destination)
        {
            EnsureLookup();
            foreach (var pair in lookup)
            {
                if (pair.Key.StartsWith("sfx_", StringComparison.Ordinal))
                {
                    destination.Add(pair.Key.Substring(4), pair.Value);
                }
            }
        }

        /// <summary>读取常驻短效或引擎；音乐通过地址异步加载，不保留整曲硬引用。</summary>
        /// <param name="name">manifest 中的非 music 配方名，不包含扩展名。</param>
        /// <returns>此 Bank 持有的共享短音频。</returns>
        public AudioClip GetClip(string name)
        {
            EnsureLookup();
            return lookup.TryGetValue(name, out var result)
                ? result : throw new ArgumentException("纸艺卡丁车原作音频缺失：" + name, nameof(name));
        }

        /// <summary>解析原作音乐名对应的正式资源地址；不触发资源加载。</summary>
        /// <param name="name">manifest 中以 music_ 开头的完整名称。</param>
        /// <returns>由当前音频宿主 Loader 异步加载的音乐地址。</returns>
        public string GetMusicAddress(string name)
        {
            EnsureLookup();
            return musicLookup.TryGetValue(name, out var address)
                ? address : throw new ArgumentException("纸艺卡丁车音乐地址缺失：" + name, nameof(name));
        }

        internal static string GetClipName(string path)
        {
            string[] segments = Path.GetFileNameWithoutExtension(path).Split('_');
            for (int i = 0; i < segments.Length; i++)
            {
                segments[i] = int.TryParse(segments[i], out int variant)
                    ? variant.ToString() : char.ToLowerInvariant(segments[i][0]) + segments[i].Substring(1);
            }
            return string.Join("_", segments);
        }

        private void EnsureLookup()
        {
            if (lookup != null)
            {
                return;
            }
            var shortClips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            var addresses = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var clip in clips)
            {
                if (clip == null || clip.name.StartsWith("Music_", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("短音频 Bank 存在缺失或整曲硬引用。");
                }
                shortClips.Add(GetClipName(clip.name), clip);
            }
            foreach (string address in musicAddresses)
            {
                if (string.IsNullOrWhiteSpace(address) || !GetClipName(address).StartsWith("music_", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("纸艺卡丁车音乐地址无效。");
                }
                addresses.Add(GetClipName(address), address);
            }
            lookup = shortClips;
            musicLookup = addresses;
        }
    }
}
