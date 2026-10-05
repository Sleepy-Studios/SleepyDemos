using System;
using UnityEngine;

namespace Hotfix.BlockPorters
{
    /// 按地址登记主题，不使所有大图成为场景的直接依赖。
    [CreateAssetMenu(menuName = "SleepyDemos/小小搬豆工/主题目录")]
    public sealed class BlockPortersThemeCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Theme
        {
            public string Id;
            public string DisplayName;
            public string BackgroundAddress;
        }
        public Theme[] Themes = Array.Empty<Theme>();

        /// <summary>使用独立随机源，排除上次成功应用的主题。</summary>
        public Theme Choose(string previousId, System.Random random)
        {
            int count = 0;
            foreach (var theme in Themes) if (theme.Id != previousId) count++;
            if (count == 0) return Themes.Length == 0 ? null : Themes[0];
            int selected = random.Next(count);
            foreach (var theme in Themes)
                if (theme.Id != previousId && selected-- == 0) return theme;
            return null;
        }
    }
}
