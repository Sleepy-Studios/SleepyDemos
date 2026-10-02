using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.JinxCasino
{
    /// S1单机Player构建入口，复用原平台构建事务与恢复流程。
    public sealed class JinxCasinoPlayerBuildWindow : EditorWindow
    {
        [MenuItem("Tools/SleepyDemos/整蛊赌场/S1离线Player构建")]
        public static void Open()
        {
            GetWindow<JinxCasinoPlayerBuildWindow>("赌场离线构建");
        }

        /// 在当前Editor构建直达赌场主菜单的Windows S1样板，不覆盖历史产物。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/构建Windows S1独立样板")]
        public static void BuildImmersionWindows() => JinxCasinoPlayerBuildPipeline.Start(BuildTarget.StandaloneWindows64);

        /// 在当前Editor构建ARM64 Android S1样板，仍需后续安装与真机验收。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/构建Android S1独立样板")]
        public static void BuildImmersionAndroid() => JinxCasinoPlayerBuildPipeline.Start(BuildTarget.Android);

        /// 为最近完成的Windows S1 Player重打包ZIP，保留原包和排障目录。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/重打包最近Windows S1分享包")]
        public static void RepackageLastImmersionWindowsPlayer()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../Builds/JinxCasino/S1/StandaloneWindows64"));
            const string stem = "JinxCasinoS1-Offline";
            if (!Directory.Exists(root)) throw new InvalidOperationException("尚无Windows S1产物。");
            string player = Directory.GetDirectories(root).OrderByDescending(path => path, StringComparer.Ordinal)
                .Select(path => Path.Combine(path, "Player"))
                .FirstOrDefault(path => File.Exists(Path.Combine(path, "S1-Offline-README.txt")) &&
                    File.Exists(Path.Combine(path, stem + ".exe")) && File.Exists(Path.Combine(path, "GameAssembly.dll")));
            if (player == null) throw new InvalidOperationException("没有找到已完成的Windows S1 Player。");
            string zip = JinxCasinoPlayerBuildPipeline.RepackageWindowsPlayer(player);
            Debug.Log("[JinxCasinoBuild] Windows S1分享包已生成：" + zip);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("S1为一区三游戏沉浸样板，版本0.5.0，启动直达赌场主菜单；S1整体尚未验收。保持AppEntrance为唯一Player入口，构建后恢复编辑器原平台与配置。", MessageType.Info);
            EditorGUILayout.LabelField(JinxCasinoPlayerBuildPipeline.Status, EditorStyles.wordWrappedLabel);
            using (new EditorGUI.DisabledScope(JinxCasinoPlayerBuildPipeline.IsBusy))
            {
                if (GUILayout.Button("构建 Windows S1 独立样板 ZIP", GUILayout.Height(36)))
                    BuildImmersionWindows();
                if (GUILayout.Button("构建 Android S1 独立样板 APK", GUILayout.Height(36)))
                    BuildImmersionAndroid();
            }
            if (JinxCasinoPlayerBuildPipeline.IsBusy && GUILayout.Button("取消后续步骤并恢复编辑器配置"))
                JinxCasinoPlayerBuildPipeline.RequestRestore();
            EditorGUILayout.LabelField("输出：Builds/JinxCasino/S1/{platform}/{version}", EditorStyles.wordWrappedLabel);
        }

        private void OnInspectorUpdate() => Repaint();
    }
}
