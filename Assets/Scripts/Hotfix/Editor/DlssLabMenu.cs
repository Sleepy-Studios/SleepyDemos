using Core.Runtime;
using Hotfix.SceneManagement;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Hotfix.Editor
{
    /// PC 实验室通过正式 AppEntrance 启动，不属于独立游戏发布目标。
    public static class DlssLabMenu
    {
        [MenuItem("Tools/Rendering/DLSS Lab")]
        public static void Open()
        {
            if (EditorApplication.isPlaying)
            {
                GameSceneNavigator.Instance?.SwitchAsync(GameSceneId.Dlss).Forget();
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            var config = AssetDatabase.LoadAssetAtPath<HotfixConfig>("Assets/LoadResources/Config/HotfixConfig.asset");
            if (config.GameId != "hub")
            {
                Debug.LogError("请先在 Hotfix Build 中选择 hub 发布配置，再启动 PC 实验室。");
                return;
            }
            SessionState.SetString("SleepyDemos.EditorStartupScene", nameof(GameSceneId.Dlss));
            EditorSceneManager.OpenScene("Assets/Scenes/AppEntrance.unity");
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Rendering/DLSS Lab", true)]
        private static bool CanOpen() => Application.platform == RuntimePlatform.WindowsEditor &&
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64 && !EditorApplication.isCompiling;
    }
}
