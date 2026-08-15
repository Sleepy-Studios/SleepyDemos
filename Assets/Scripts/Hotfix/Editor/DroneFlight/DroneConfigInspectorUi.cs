using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.DroneFlight
{
    /// <summary>DroneFlight 三套配置 Inspector 共享的语言状态与字段标签。</summary>
    internal static class DroneConfigInspectorUi
    {
        private static readonly string[] LanguageOptions = { "中文", "English" };

        /// <summary>
        /// 绘制互斥语言工具栏并在选择变化时持久化本机偏好。
        /// </summary>
        /// <param name="useChinese">当前是否使用中文。</param>
        /// <param name="preferenceKey">保存到 EditorPrefs 的配置键。</param>
        /// <returns>本帧最终语言状态。</returns>
        internal static bool DrawLanguageToolbar(bool useChinese, string preferenceKey)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label(useChinese ? "配置语言" : "Inspector Language", GUILayout.Width(110f));
                var selectedIndex = GUILayout.Toolbar(
                    useChinese ? 0 : 1,
                    LanguageOptions,
                    EditorStyles.toolbarButton);
                var nextChinese = IsChineseSelection(selectedIndex);
                if (nextChinese != useChinese)
                {
                    EditorPrefs.SetBool(preferenceKey, nextChinese);
                }

                return nextChinese;
            }
        }

        /// <summary>
        /// 把互斥工具栏索引转换为语言状态。
        /// </summary>
        /// <param name="selectedIndex">中文为 0，English 为 1。</param>
        internal static bool IsChineseSelection(int selectedIndex) => selectedIndex == 0;
    }

    /// <summary>一个序列化字段的完整中英文名称和提示。</summary>
    internal readonly struct DroneInspectorLabel
    {
        internal DroneInspectorLabel(
            string chinese,
            string english,
            string chineseTooltip,
            string englishTooltip)
        {
            Chinese = chinese;
            English = english;
            ChineseTooltip = chineseTooltip;
            EnglishTooltip = englishTooltip;
        }

        internal string Chinese { get; }
        internal string English { get; }
        internal string ChineseTooltip { get; }
        internal string EnglishTooltip { get; }

        /// <summary>
        /// 按当前语言生成 Unity Inspector 标签。
        /// </summary>
        /// <param name="useChinese">是否返回中文名称和提示。</param>
        internal GUIContent Content(bool useChinese)
        {
            return useChinese
                ? new GUIContent(Chinese, ChineseTooltip)
                : new GUIContent(English, EnglishTooltip);
        }
    }
}
