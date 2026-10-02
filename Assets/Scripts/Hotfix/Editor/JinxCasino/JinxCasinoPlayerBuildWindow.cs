using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.JinxCasino
{
    /// 分阶段离线Player构建入口；S1独立启动，P0/P4保留历史验证内容。
    public sealed class JinxCasinoPlayerBuildWindow : EditorWindow
    {
        [MenuItem("Tools/SleepyDemos/整蛊赌场/P0离线Player构建")]
        public static void Open()
        {
            GetWindow<JinxCasinoPlayerBuildWindow>("赌场离线构建");
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

        /// 在当前Editor构建直达赌场主菜单的Windows S1样板，不覆盖历史产物。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/构建Windows S1独立样板")]
        public static void BuildImmersionWindows() => JinxCasinoPlayerBuildPipeline.Start(BuildTarget.StandaloneWindows64, "S1");

        /// 在当前Editor构建ARM64 Android S1样板，仍需后续安装与真机验收。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/构建Android S1独立样板")]
        public static void BuildImmersionAndroid() => JinxCasinoPlayerBuildPipeline.Start(BuildTarget.Android, "S1");

        /// 为最近已生成运行说明的 Windows Player 重打包分享 ZIP，不重新编译。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/重打包最近Windows P0分享包")]
        public static void RepackageLastWindowsPlayer()
            => RepackageLastWindowsPlayer("P0");

        /// 为最近完成的Windows S1 Player重打包ZIP，保留原包和排障目录。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/重打包最近Windows S1分享包")]
        public static void RepackageLastImmersionWindowsPlayer()
            => RepackageLastWindowsPlayer("S1");

        private static void RepackageLastWindowsPlayer(string stage)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/JinxCasino/" + stage + "/StandaloneWindows64"));
            string stem = "JinxCasino" + stage + "-Offline";
            if (!Directory.Exists(root)) throw new InvalidOperationException("尚无Windows " + stage + "产物。");
            string player = Directory.GetDirectories(root).OrderByDescending(path => path, StringComparer.Ordinal)
                .Select(path => Path.Combine(path, "Player"))
                .FirstOrDefault(path => File.Exists(Path.Combine(path, stage + "-Offline-README.txt")) &&
                    File.Exists(Path.Combine(path, stem + ".exe")) && File.Exists(Path.Combine(path, "GameAssembly.dll")));
            if (player == null) throw new InvalidOperationException("没有找到已完成的Windows " + stage + " Player。");
            string zip = JinxCasinoPlayerBuildPipeline.RepackageWindowsPlayer(player, stage);
            Debug.Log("[JinxCasinoBuild] Windows " + stage + "分享包已生成：" + zip);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("S1为一区三游戏沉浸样板，版本0.5.0，启动直达赌场主菜单；S1整体尚未验收。P0/P4历史内容单独保存。保持AppEntrance为唯一Player入口，构建后恢复编辑器原平台与配置。", MessageType.Info);
            EditorGUILayout.LabelField(JinxCasinoPlayerBuildPipeline.Status, EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(JinxCasinoPlayerBuildPipeline.IsBusy))
            {
                if (GUILayout.Button("构建 Windows S1 独立样板 ZIP", GUILayout.Height(36)))
                    BuildImmersionWindows();
                if (GUILayout.Button("构建 Android S1 独立样板 APK", GUILayout.Height(36)))
                    BuildImmersionAndroid();
                if (GUILayout.Button("构建 Windows P0 离线验证 ZIP", GUILayout.Height(36)))
                    JinxCasinoPlayerBuildPipeline.Start(BuildTarget.StandaloneWindows64);
                if (GUILayout.Button("构建 Android P0 离线验证 APK", GUILayout.Height(36)))
                    JinxCasinoPlayerBuildPipeline.Start(BuildTarget.Android);
            }
            if (JinxCasinoPlayerBuildPipeline.IsBusy && GUILayout.Button("取消后续步骤并恢复编辑器配置"))
                JinxCasinoPlayerBuildPipeline.RequestRestore();
            EditorGUILayout.LabelField("输出：Builds/JinxCasino/{P0|P4|S1}/{platform}/{version}", EditorStyles.wordWrappedLabel);
        }

        private void OnInspectorUpdate() => Repaint();
    }
}
