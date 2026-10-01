using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SleepyStudios.LoopScroll.Samples
{
    /// <summary>Builder 写入导入后的实际路径；宿主可追加自己的接入示例。</summary>
    public sealed class LoopSampleCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        { public string id; public string titleKey; public string descriptionKey; public string scenePath; }
        [SerializeField] private List<Entry> entries = new List<Entry>();
        public IReadOnlyList<Entry> Entries => entries;
        public Entry Find(string id)
        { for (var i = 0; i < entries.Count; i++) if (entries[i].id == id) return entries[i]; return null; }
        public string ResolvePath(Entry entry)
        {
            if (entry.scenePath.StartsWith("Assets/", StringComparison.Ordinal)) return entry.scenePath;
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GetAssetPath(this);
            var marker = path.IndexOf("/Common/Resources/", StringComparison.Ordinal);
            if (marker < 0) throw new InvalidOperationException("示例目录不完整，请重新导入 Showcase。");
            return path.Substring(0, marker) + "/" + entry.scenePath;
#else
            string match = null;
            for (var i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                if (!path.EndsWith("/" + entry.scenePath, StringComparison.Ordinal)) continue;
                if (match != null) throw new InvalidOperationException("Showcase build contains duplicate scene paths: " + entry.scenePath);
                match = path;
            }
            return match ?? entry.scenePath;
#endif
        }
    }
}
