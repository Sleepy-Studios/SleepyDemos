using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Runtime;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace Core.Editor.HotUpdate
{
    public sealed class HotUpdateBuildWindow : EditorWindow
    {
        private const string ConfigAssetPath = "Assets/LoadResources/Config/HotUpdateConfig.asset";

        private HotUpdateConfig config;
        private bool isPcPlatform = true;
        private Vector2 scrollPosition;

        [MenuItem("Tools/UI Framework/HotUpdate Build")]
        public static void Open()
        {
            GetWindow<HotUpdateBuildWindow>("HotUpdate Build");
        }

        [MenuItem("Assets/Create/Sleepy/Hot Update Config")]
        public static void CreateHotUpdateConfigFromProject()
        {
            var path = EditorUtility.SaveFilePanelInProject("Create HotUpdateConfig", "HotUpdateConfig", "asset", "选择 HotUpdateConfig 保存位置", "Assets/LoadResources/Config");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var asset = CreateInstance<HotUpdateConfig>();
            ApplyPlatformDefaults(asset, true);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }

        private void OnEnable()
        {
            LoadOrCreateConfig();
            RefreshLocalBundlePathToLatest();
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawConfigObject();
            DrawYooAssetSection();
            DrawAssemblySection();
            DrawBuildSection();
            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigObject()
        {
            EditorGUILayout.LabelField("HotUpdate 配置", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                config = (HotUpdateConfig)EditorGUILayout.ObjectField("Config", config, typeof(HotUpdateConfig), false);
                if (GUILayout.Button("新建/定位默认配置", GUILayout.Width(130)))
                {
                    LoadOrCreateConfig();
                    Selection.activeObject = config;
                }
            }

            if (config == null)
            {
                EditorGUILayout.HelpBox("没有 HotUpdateConfig。点击上方按钮创建默认配置。", MessageType.Warning);
                return;
            }

            if (GUILayout.Button("保存配置"))
            {
                SaveConfig();
            }
        }

        private void DrawYooAssetSection()
        {
            if (config == null)
            {
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("YooAssets", EditorStyles.boldLabel);
            config.PlayMode = (YooAssetPlayMode)EditorGUILayout.EnumPopup("PlayMode", config.PlayMode);
            config.PackageName = EditorGUILayout.TextField("Package", config.PackageName);
            config.BaseServerURL = EditorGUILayout.TextField("Base Server URL", config.BaseServerURL);
            config.LocalBundlePath = EditorGUILayout.TextField("Local Bundle Path", config.LocalBundlePath);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("刷新最新 Bundle 路径"))
                {
                    RefreshLocalBundlePathToLatest();
                }

                if (GUILayout.Button("打开 YooAssets 打包窗口"))
                {
                    AssetBundleBuilderWindow.OpenWindow();
                }
            }
        }

        private void DrawAssemblySection()
        {
            if (config == null)
            {
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("HybridCLR", EditorStyles.boldLabel);
            DrawPlatformSelector();
            config.AotSourcePath = EditorGUILayout.TextField("AOT 源路径", config.AotSourcePath);
            config.AotStrippedSourcePath = EditorGUILayout.TextField("AOT 优化路径", config.AotStrippedSourcePath);
            config.AotTargetPath = EditorGUILayout.TextField("AOT 目标路径", config.AotTargetPath);
            DrawStringArray("AOT 文件列表", ref config.AotAssemblies);

            EditorGUILayout.Space(6);
            config.HotUpdateSourcePath = EditorGUILayout.TextField("热更源路径", config.HotUpdateSourcePath);
            config.HotUpdateTargetPath = EditorGUILayout.TextField("热更目标路径", config.HotUpdateTargetPath);
            DrawStringArray("热更文件列表", ref config.HotUpdateAssemblies);
        }

        private void DrawBuildSection()
        {
            if (config == null)
            {
                return;
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("编译与替换", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("生成并编译全部 DLL"))
                {
                    PrebuildCommand.GenerateAll();
                    AssetDatabase.Refresh();
                }

                if (GUILayout.Button("仅编译热更 DLL"))
                {
                    CompileDllCommand.CompileDllActiveBuildTarget();
                    AssetDatabase.Refresh();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("替换 DLL 到 YooAssets 目录"))
                {
                    ReplaceAssemblies(false);
                }

                if (GUILayout.Button("剥离 AOT 并替换"))
                {
                    StripAotAssemblies();
                    ReplaceAssemblies(true);
                }
            }
        }

        private void DrawPlatformSelector()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var nextPc = GUILayout.Toggle(isPcPlatform, "PC 平台", "Button", GUILayout.Width(100));
                var nextAndroid = GUILayout.Toggle(!isPcPlatform, "Android 平台", "Button", GUILayout.Width(120));
                var newIsPc = nextPc || !nextAndroid;
                if (newIsPc == isPcPlatform)
                {
                    return;
                }

                isPcPlatform = newIsPc;
                ApplyPlatformDefaults(config, isPcPlatform);
                RefreshLocalBundlePathToLatest();
            }
        }

        private static void DrawStringArray(string title, ref string[] values)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            var list = values?.ToList() ?? new List<string>();
            EditorGUI.indentLevel++;
            for (var i = 0; i < list.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    list[i] = EditorGUILayout.TextField($"文件 {i + 1}", list[i]);
                    if (GUILayout.Button("x", GUILayout.Width(24)))
                    {
                        list.RemoveAt(i);
                        i--;
                    }
                }
            }

            if (GUILayout.Button("+ 添加", GUILayout.Width(80)))
            {
                list.Add(string.Empty);
            }

            EditorGUI.indentLevel--;
            values = list.ToArray();
        }

        private void ReplaceAssemblies(bool useStrippedAot)
        {
            var aotSource = useStrippedAot ? config.AotStrippedSourcePath : config.AotSourcePath;
            CopyAssemblies(aotSource, config.AotTargetPath, config.AotAssemblies);
            CopyAssemblies(config.HotUpdateSourcePath, config.HotUpdateTargetPath, config.HotUpdateAssemblies);
            AssetDatabase.Refresh();
            SaveConfig();
            ShowNotification(new GUIContent("DLL 替换完成"));
        }

        private static void CopyAssemblies(string source, string target, IEnumerable<string> files)
        {
            Directory.CreateDirectory(target);
            foreach (var file in files.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                var sourcePath = Path.Combine(source, file);
                var targetPath = Path.Combine(target, $"{Path.GetFileNameWithoutExtension(file)}.dll.bytes");
                if (!File.Exists(sourcePath))
                {
                    Debug.LogWarning($"热更文件不存在: {sourcePath}");
                    continue;
                }

                File.Copy(sourcePath, targetPath, true);
                Debug.Log($"热更文件已复制: {sourcePath} -> {targetPath}");
            }
        }

        private void StripAotAssemblies()
        {
            Directory.CreateDirectory(config.AotStrippedSourcePath);
            if (!Directory.Exists(config.AotSourcePath))
            {
                Debug.LogWarning($"AOT 源路径不存在: {config.AotSourcePath}");
                return;
            }

            foreach (var sourcePath in Directory.GetFiles(config.AotSourcePath, "*.dll"))
            {
                var targetPath = Path.Combine(config.AotStrippedSourcePath, Path.GetFileName(sourcePath));
                HybridCLR.Editor.AOT.AOTAssemblyMetadataStripper.Strip(sourcePath, targetPath);
            }
        }

        private void LoadOrCreateConfig()
        {
            EnsureFolder("Assets/LoadResources");
            EnsureFolder("Assets/LoadResources/Config");
            config = AssetDatabase.LoadAssetAtPath<HotUpdateConfig>(ConfigAssetPath);
            if (config != null)
            {
                return;
            }

            config = CreateInstance<HotUpdateConfig>();
            ApplyPlatformDefaults(config, isPcPlatform);
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ApplyPlatformDefaults(HotUpdateConfig target, bool pc)
        {
            var platform = pc ? "StandaloneWindows64" : "Android";
            target.PackageName = string.IsNullOrWhiteSpace(target.PackageName) ? YooAssetResourceSystem.DefaultPackageName : target.PackageName;
            target.AotSourcePath = $"HybridCLRData/AssembliesPostIl2CppStrip/{platform}";
            target.AotStrippedSourcePath = $"HybridCLRData/StrippedAOTAssembly2/{platform}";
            target.HotUpdateSourcePath = $"HybridCLRData/HotUpdateDlls/{platform}";
            target.LocalBundlePath = $"Bundles/{platform}/{target.PackageName}";
        }

        private void RefreshLocalBundlePathToLatest()
        {
            if (config == null)
            {
                return;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
            var basePath = Path.GetFullPath(Path.Combine(projectRoot, $"Bundles/{(isPcPlatform ? "StandaloneWindows64" : "Android")}/{config.PackageName}"));
            if (!Directory.Exists(basePath))
            {
                config.LocalBundlePath = Path.GetRelativePath(projectRoot, basePath).Replace('\\', '/');
                return;
            }

            var latest = Directory.GetDirectories(basePath)
                .OrderByDescending(path => new DirectoryInfo(path).CreationTimeUtc)
                .FirstOrDefault();
            config.LocalBundlePath = Path.GetRelativePath(projectRoot, latest ?? basePath).Replace('\\', '/');
        }

        private void SaveConfig()
        {
            if (config == null)
            {
                return;
            }

            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
