using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.JinxCasino
{
    /// P0 离线包构建入口；联网构建将在 SDK 与真实房间验证完成后另行接入。
    public sealed class JinxCasinoPlayerBuildWindow : EditorWindow
    {
        [MenuItem("Tools/SleepyDemos/整蛊赌场/P0离线Player构建")]
        public static void Open()
        {
            GetWindow<JinxCasinoPlayerBuildWindow>("赌场 P0 离线构建");
        }

        /// 在当前 Editor 启动 Windows P0 离线构建事务。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/构建Windows P0离线验证包")]
        public static void BuildWindows() => JinxCasinoPlayerBuildPipeline.Start(BuildTarget.StandaloneWindows64);

        /// 在当前 Editor 启动 Android P0 离线构建事务。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/构建Android P0离线验证包")]
        public static void BuildAndroid() => JinxCasinoPlayerBuildPipeline.Start(BuildTarget.Android);

        /// 在当前Editor构建正式离线内容，保留原P0产物。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/构建Windows P4离线试玩包")]
        public static void BuildFormalWindows() => JinxCasinoPlayerBuildPipeline.Start(BuildTarget.StandaloneWindows64, "P4");

        /// 在当前Editor构建ARM64正式离线内容，平台资源独立保存。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/构建Android P4离线试玩包")]
        public static void BuildFormalAndroid() => JinxCasinoPlayerBuildPipeline.Start(BuildTarget.Android, "P4");

        /// 为最近已生成运行说明的 Windows Player 重打包分享 ZIP，不重新编译。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/重打包最近Windows P0分享包")]
        public static void RepackageLastWindowsPlayer()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/JinxCasino/P0/StandaloneWindows64"));
            if (!Directory.Exists(root)) throw new InvalidOperationException("尚无 Windows P0 产物。");
            string player = Directory.GetDirectories(root).OrderByDescending(path => path, StringComparer.Ordinal)
                .Select(path => Path.Combine(path, "Player"))
                .FirstOrDefault(path => File.Exists(Path.Combine(path, "P0-Offline-README.txt")) &&
                    File.Exists(Path.Combine(path, "JinxCasinoP0-Offline.exe")) && File.Exists(Path.Combine(path, "GameAssembly.dll")));
            if (player == null) throw new InvalidOperationException("没有找到已完成的 Windows P0 Player。");
            string zip = JinxCasinoPlayerBuildPipeline.RepackageWindowsPlayer(player);
            Debug.Log("[JinxCasinoBuild] Windows P0 分享包已生成：" + zip);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("生成单人离线验证包。P0 只有三款小游戏与场景/UI，不代表互联网联机或完整版本。保持 AppEntrance 为唯一 Player 入口，构建后恢复编辑器原平台与配置。", MessageType.Info);
            EditorGUILayout.LabelField(JinxCasinoPlayerBuildPipeline.Status, EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(JinxCasinoPlayerBuildPipeline.IsBusy))
            {
                if (GUILayout.Button("构建 Windows P0 离线验证 ZIP", GUILayout.Height(36)))
                    JinxCasinoPlayerBuildPipeline.Start(BuildTarget.StandaloneWindows64);
                if (GUILayout.Button("构建 Android P0 离线验证 APK", GUILayout.Height(36)))
                    JinxCasinoPlayerBuildPipeline.Start(BuildTarget.Android);
            }
            if (JinxCasinoPlayerBuildPipeline.IsBusy && GUILayout.Button("取消后续步骤并恢复编辑器配置"))
                JinxCasinoPlayerBuildPipeline.RequestRestore();
            EditorGUILayout.LabelField("输出：Builds/JinxCasino/P0/{platform}", EditorStyles.wordWrappedLabel);
        }

        private void OnInspectorUpdate() => Repaint();
    }
}
