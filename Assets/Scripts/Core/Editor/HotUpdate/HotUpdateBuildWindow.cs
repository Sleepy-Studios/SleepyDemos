using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Runtime;
using HybridCLR.Editor.Commands;
using Renci.SshNet;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;
using Debug = UnityEngine.Debug;

namespace Core.Editor.HotUpdate
{
    public sealed class HotUpdateBuildWindow : EditorWindow
    {
        private const string ConfigAssetPath = "Assets/LoadResources/Config/HotUpdateConfig.asset";
        private const string AotGenericReferencesPath = "Assets/HybridCLRGenerate/AOTGenericReferences.cs";

        private HotUpdateConfig config;
        private bool isPcPlatform = true;
        private Vector2 scrollPosition;

        private bool showGeneralSection = true;
        private bool showHybridClrSection = true;
        private bool showBuildSection = true;
        private bool showMockServerSection = true;
        private bool showRemoteSection = true;
        private bool showDeploymentSection = true;
        private bool showAdvancedPaths;
        private bool showManualAssemblyEdit;

        [MenuItem("Tools/UI Framework/HotUpdate Build")]
        public static void Open()
        {
            GetWindow<HotUpdateBuildWindow>("HotUpdate Build");
        }

        [MenuItem("Tools/一键打包工具")]
        public static void OpenChinese()
        {
            Open();
        }

        public static void CreateHotUpdateConfigFromProject()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create HotUpdateConfig",
                "HotUpdateConfig",
                "asset",
                "选择 HotUpdateConfig 保存位置",
                "Assets/LoadResources/Config");
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
            EnsureDefaults();
            SyncBaseServerUrlFromMode(false);

            if (config != null && config.UseLocalMockServer)
            {
                TryStartLocalMockServerSilently();
            }
        }

        private void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            DrawConfigSection();
            if (config != null)
            {
                DrawGlobalPlatformSection();
                DrawGeneralSection();
                DrawHybridClrSection();
                DrawBuildSection();
                DrawMockServerSection();
                DrawRemoteSection();
                DrawDeploymentSection();
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfigSection()
        {
            EditorGUILayout.LabelField("HotUpdate 配置", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                config = (HotUpdateConfig)EditorGUILayout.ObjectField(new GUIContent("Config", "当前编辑的热更新配置资源"), config, typeof(HotUpdateConfig), false);
                if (GUILayout.Button(new GUIContent("新建/定位默认配置", "创建或选中默认 HotUpdateConfig 资源"), GUILayout.Width(130)))
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

            if (GUILayout.Button(new GUIContent("保存配置", "保存当前窗口里的配置修改")))
            {
                SaveConfig();
            }
        }

        private void DrawGlobalPlatformSection()
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("目标平台", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                bool nextPc = GUILayout.Toggle(isPcPlatform, "PC 平台", "Button", GUILayout.Width(120), GUILayout.Height(28));
                bool nextAndroid = GUILayout.Toggle(!isPcPlatform, "Android 平台", "Button", GUILayout.Width(120), GUILayout.Height(28));
                bool newIsPc = nextPc || !nextAndroid;
                if (newIsPc == isPcPlatform)
                {
                    return;
                }

                isPcPlatform = newIsPc;
                ApplyPlatformDefaults(config, isPcPlatform);
                RefreshLocalBundlePathToLatest();
                EnsureDefaults();
                SyncBaseServerUrlFromMode(true);
                if (config.UseLocalMockServer)
                {
                    TryStartLocalMockServerSilently();
                }
            }
        }

        private void DrawGeneralSection()
        {
            showGeneralSection = EditorGUILayout.Foldout(showGeneralSection, "YooAssets / 基础配置", true);
            if (!showGeneralSection)
            {
                return;
            }

            EditorGUI.indentLevel++;
            config.PlayMode = (ResourcePlayMode)EditorGUILayout.EnumPopup(
                new GUIContent("PlayMode", "资源运行模式。远端下载请选择 HostPlayMode，本地编辑器预览通常用 EditorSimulateMode。"),
                config.PlayMode);
            config.PackageName = EditorGUILayout.TextField(
                new GUIContent("Package", "YooAssets 资源包名称，默认一般保持 DefaultPackage。"),
                config.PackageName);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.TextField(new GUIContent("当前生效 URL", "运行时真正用于拉取版本与资源的地址，会随“本地模拟开关”自动切换。"), config.BaseServerURL);
            }

            DrawFolderPathSelector(
                new GUIContent("当前 Bundle 路径", "当前平台实际用于上传或模拟服务的 Bundle 目录。"),
                config.LocalBundlePath,
                GetLocalBundleAbsolutePath(),
                BrowseLocalBundlePath);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("刷新最新 Bundle 路径", "自动定位当前平台下最新生成的 Bundle 目录")))
                {
                    RefreshLocalBundlePathToLatest();
                    if (config.UseLocalMockServer && string.IsNullOrWhiteSpace(config.MockServerFolderPath))
                    {
                        TryStartLocalMockServerSilently();
                    }
                }

                if (GUILayout.Button(new GUIContent("打开 YooAssets 打包窗口", "打开 YooAssets 官方的资源打包窗口")))
                {
                    AssetBundleBuilderWindow.OpenWindow();
                }
            }

            showAdvancedPaths = EditorGUILayout.Foldout(showAdvancedPaths, "路径详情", true);
            if (showAdvancedPaths)
            {
                EditorGUILayout.LabelField("Bundle 相对路径");
                EditorGUILayout.SelectableLabel(config.LocalBundlePath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                EditorGUILayout.LabelField("Bundle 绝对路径");
                EditorGUILayout.SelectableLabel(GetLocalBundleAbsolutePath(), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }

            EditorGUI.indentLevel--;
        }

        private void DrawHybridClrSection()
        {
            showHybridClrSection = EditorGUILayout.Foldout(showHybridClrSection, "HybridCLR / 程序集配置", true);
            if (!showHybridClrSection)
            {
                return;
            }

            EditorGUI.indentLevel++;
            DrawFolderPathSelector(
                new GUIContent("AOT 源路径", "HybridCLR 编译后 AOT 程序集所在目录。通常是 AssembliesPostIl2CppStrip。"),
                config.AotSourcePath,
                ResolvePath(config.AotSourcePath),
                () => BrowseFolderPath(value => config.AotSourcePath = value, ResolvePath(config.AotSourcePath), "选择 AOT 源目录"));

            DrawFolderPathSelector(
                new GUIContent("AOT 优化路径", "AOT 元数据剥离后的输出目录。"),
                config.AotStrippedSourcePath,
                ResolvePath(config.AotStrippedSourcePath),
                () => BrowseFolderPath(value => config.AotStrippedSourcePath = value, ResolvePath(config.AotStrippedSourcePath), "选择 AOT 优化目录"));

            DrawFolderPathSelector(
                new GUIContent("AOT 目标路径", "AOT dll.bytes 拷贝到项目资源目录的位置。"),
                config.AotTargetPath,
                ResolvePath(config.AotTargetPath),
                () => BrowseFolderPath(value => config.AotTargetPath = value, ResolvePath(config.AotTargetPath), "选择 AOT 目标目录"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("自动填写 AOT 列表", "从 Assets/HybridCLRGenerate/AOTGenericReferences.cs 的 PatchedAOTAssemblyList 自动提取 AOT DLL 列表")))
                {
                    AutoFillAotAssemblies();
                }

                if (GUILayout.Button(new GUIContent("定位 AOT 引用文件", "打开 AOTGenericReferences.cs 所在位置，便于核对自动生成结果")))
                {
                    RevealPath(Path.GetFullPath(Path.Combine(GetProjectRootPath(), AotGenericReferencesPath)));
                }
            }

            DrawReadonlyAssemblyList(
                new GUIContent("AOT 文件列表", "运行时需要补充元数据的 AOT DLL 列表。优先使用自动填写。"),
                config.AotAssemblies);

            EditorGUILayout.Space(6);
            DrawFolderPathSelector(
                new GUIContent("热更源路径", "HybridCLR 输出的热更新程序集目录。"),
                config.HotUpdateSourcePath,
                ResolvePath(config.HotUpdateSourcePath),
                () => BrowseFolderPath(value => config.HotUpdateSourcePath = value, ResolvePath(config.HotUpdateSourcePath), "选择热更源目录"));

            DrawFolderPathSelector(
                new GUIContent("热更目标路径", "热更 dll.bytes 拷贝到项目资源目录的位置。"),
                config.HotUpdateTargetPath,
                ResolvePath(config.HotUpdateTargetPath),
                () => BrowseFolderPath(value => config.HotUpdateTargetPath = value, ResolvePath(config.HotUpdateTargetPath), "选择热更目标目录"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("自动填写热更列表", "根据项目里的热更 asmdef 自动推断热更新程序集。目前会优先识别 Assets/Scripts/Hotfix 下的程序集。")))
                {
                    AutoFillHotUpdateAssemblies();
                }

                if (GUILayout.Button(new GUIContent("清空程序集列表", "清空当前 AOT 与热更程序集列表，便于重新自动生成")))
                {
                    config.AotAssemblies = Array.Empty<string>();
                    config.HotUpdateAssemblies = Array.Empty<string>();
                    EditorUtility.SetDirty(config);
                }
            }

            DrawReadonlyAssemblyList(
                new GUIContent("热更文件列表", "运行时需要动态加载的热更新 DLL 列表。当前项目默认通常包含 Hotfix.dll。"),
                config.HotUpdateAssemblies);

            showManualAssemblyEdit = EditorGUILayout.Foldout(showManualAssemblyEdit, "手动编辑程序集列表", true);
            if (showManualAssemblyEdit)
            {
                DrawStringArray("AOT 文件列表（手动）", ref config.AotAssemblies);
                EditorGUILayout.Space(4);
                DrawStringArray("热更文件列表（手动）", ref config.HotUpdateAssemblies);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawBuildSection()
        {
            showBuildSection = EditorGUILayout.Foldout(showBuildSection, "编译与替换", true);
            if (!showBuildSection)
            {
                return;
            }

            EditorGUI.indentLevel++;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("生成并编译全部 DLL", "执行 HybridCLR 全量生成与编译流程")))
                {
                    PrebuildCommand.GenerateAll();
                    AssetDatabase.Refresh();
                    AutoFillAotAssemblies();
                    AutoFillHotUpdateAssemblies();
                }

                if (GUILayout.Button(new GUIContent("仅编译热更 DLL", "只编译热更新程序集，不重新生成全部 AOT 相关内容")))
                {
                    CompileDllCommand.CompileDllActiveBuildTarget();
                    AssetDatabase.Refresh();
                    AutoFillHotUpdateAssemblies();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("同步当前 DLL 到资源目录", "不做 AOT 剥离。直接把当前 AOT 源目录里的 DLL，以及热更 DLL，复制到目标资源目录并生成 .dll.bytes。适合已经完成编译、只想同步资源目录时使用。")))
                {
                    ReplaceAssemblies(false);
                }

                if (GUILayout.Button(new GUIContent("先剥离 AOT，再同步到资源目录", "先从 AOT 源目录生成剥离后的 AOT DLL，再把这些剥离结果和热更 DLL 一起复制到目标资源目录并生成 .dll.bytes。适合更新 AOT 元数据补充资源时使用。")))
                {
                    StripAotAssemblies();
                    ReplaceAssemblies(true);
                }
            }

            EditorGUILayout.HelpBox("左边: 直接同步当前编译结果，不做 AOT 剥离。右边: 先生成剥离后的 AOT 元数据资源，再同步到资源目录。", MessageType.None);

            if (GUILayout.Button(new GUIContent("一键编译并同步", "执行全量编译，自动刷新 AOT/热更列表，然后直接同步当前 DLL 到资源目录。不包含 AOT 剥离步骤。")))
            {
                PrebuildCommand.GenerateAll();
                AssetDatabase.Refresh();
                AutoFillAotAssemblies();
                AutoFillHotUpdateAssemblies();
                ReplaceAssemblies(false);
            }

            EditorGUI.indentLevel--;
        }

        private void DrawMockServerSection()
        {
            showMockServerSection = EditorGUILayout.Foldout(showMockServerSection, "本地模拟远端服务器", true);
            if (!showMockServerSection)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            bool useLocalMockServer = EditorGUILayout.ToggleLeft(
                new GUIContent("启用本地模拟远端服务器", "开启后自动启动本地静态文件服务，并将当前生效 URL 切换到本机端口。"),
                config.UseLocalMockServer);
            if (EditorGUI.EndChangeCheck())
            {
                SetLocalMockServerEnabled(useLocalMockServer);
            }

            if (!config.UseLocalMockServer)
            {
                EditorGUILayout.HelpBox("开启后才会显示本地模拟服务器配置。关闭后恢复真实远端 URL 与 SSH 上传流程。", MessageType.None);
                EditorGUI.indentLevel--;
                return;
            }

            config.LocalServerHost = EditorGUILayout.TextField(
                new GUIContent("模拟服务 Host", "PC 本机调试可用 127.0.0.1；手机或其他设备调试请改成你电脑的局域网 IP。"),
                config.LocalServerHost);
            config.LocalServerPort = EditorGUILayout.IntField(
                new GUIContent("模拟服务 Port", "本地静态文件服务监听端口，例如 18080。若 8080 被浏览器或其它工具占用，建议使用 18080。"),
                config.LocalServerPort);

            DrawFolderPathSelector(
                new GUIContent("模拟服务器目录", "本地 HTTP 服务实际暴露的目录。留空时默认使用当前 Bundle 目录。"),
                string.IsNullOrWhiteSpace(config.MockServerFolderPath) ? $"使用当前 Bundle 目录: {config.LocalBundlePath}" : config.MockServerFolderPath,
                GetMockServerRootAbsolutePath(),
                BrowseMockServerFolderPath,
                allowOpenOnly: true);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("使用当前 Bundle", "清空模拟目录配置，改为直接使用当前 Bundle 目录")))
                {
                    config.MockServerFolderPath = string.Empty;
                    EditorUtility.SetDirty(config);
                    TryStartLocalMockServerSilently();
                }

                if (GUILayout.Button(new GUIContent("填入 localhost", "将模拟服务 Host 设置为 127.0.0.1")))
                {
                    config.LocalServerHost = "127.0.0.1";
                    TryStartLocalMockServerSilently();
                }

                if (GUILayout.Button(new GUIContent("填入局域网 IP", "自动获取本机局域网 IPv4 地址，方便手机或其他设备访问")))
                {
                    config.LocalServerHost = LocalBundleHttpServer.GetLocalIPv4();
                    TryStartLocalMockServerSilently();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("重启模拟服务器", "按当前 Host、端口、目录重启本地模拟服务")))
                {
                    StartLocalMockServer(true);
                }

                if (GUILayout.Button(new GUIContent("停止模拟服务器", "停止本地静态文件服务")))
                {
                    StopLocalMockServer(true);
                }
            }

            string status = LocalBundleHttpServer.IsRunning
                ? $"运行中: {BuildBaseServerUrl(config.LocalServerHost, LocalBundleHttpServer.Port)}"
                : "未启动";
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(new GUIContent("模拟状态", "当前本地模拟服务器运行状态"));
                EditorGUILayout.SelectableLabel(status, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
            }
            EditorGUI.indentLevel--;
        }

        private void DrawRemoteSection()
        {
            showRemoteSection = EditorGUILayout.Foldout(showRemoteSection, "真实远端服务器 / SSH", true);
            if (!showRemoteSection)
            {
                return;
            }

            EditorGUI.indentLevel++;
            if (config.UseLocalMockServer)
            {
                EditorGUILayout.HelpBox("当前启用了本地模拟模式，本分组仅作配置保存展示，SSH 上传与测试不会生效。", MessageType.None);
            }

            config.RemoteBaseServerURL = EditorGUILayout.TextField(
                new GUIContent("真实远端 URL", "关闭本地模拟后，运行时会使用这个地址拉取版本与资源。"),
                config.RemoteBaseServerURL);
            if (!config.UseLocalMockServer)
            {
                SyncBaseServerUrlFromMode(true);
            }

            config.SshHost = EditorGUILayout.TextField(new GUIContent("服务器地址", "SSH 服务器主机名或 IP。"), config.SshHost);
            config.SshPort = EditorGUILayout.IntField(new GUIContent("SSH 端口", "SSH 服务端口，通常为 22。"), config.SshPort);
            config.SshUser = EditorGUILayout.TextField(new GUIContent("用户名", "SSH 登录用户名。"), config.SshUser);
            DrawFilePathSelector(
                new GUIContent("私钥路径", "用于 SSH 连接的私钥文件路径。"),
                config.KeyFilePath,
                GetKeyFileAbsolutePath(),
                () => BrowseFilePath(value => config.KeyFilePath = value, GetProjectRootPath(), "选择 SSH 私钥文件"));
            DrawFolderPathSelector(
                new GUIContent("远程根路径", "SSH 上传时远端资源根目录，实际会自动追加 /PC 或 /Android。"),
                config.ServerBasePath,
                config.ServerBasePath,
                null,
                allowBrowse: false,
                allowOpenOnly: false);

            using (new EditorGUI.DisabledScope(config.UseLocalMockServer))
            {
                if (GUILayout.Button(new GUIContent("测试 SSH 连接", "使用当前私钥配置测试 SSH 可达性")))
                {
                    TestSshConnection();
                }
            }

            EditorGUI.indentLevel--;
        }

        private void DrawDeploymentSection()
        {
            showDeploymentSection = EditorGUILayout.Foldout(showDeploymentSection, "资源部署", true);
            if (!showDeploymentSection)
            {
                return;
            }

            EditorGUI.indentLevel++;
            if (config.UseLocalMockServer)
            {
                EditorGUILayout.HelpBox("当前为本地模拟模式，不需要 SSH 上传。模拟服务会直接读取你配置的模拟服务器目录。", MessageType.Info);
            }
            else if (GUILayout.Button(new GUIContent("开始上传到 SSH 服务器", "将当前 Bundle 目录下的所有文件上传到远端服务器")))
            {
                UploadToServer();
            }

            EditorGUI.indentLevel--;
        }

        private void DrawFolderPathSelector(
            GUIContent label,
            string displayValue,
            string absolutePath,
            Action browseAction,
            bool allowBrowse = true,
            bool allowOpenOnly = true)
        {
            EditorGUILayout.LabelField(label);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(displayValue, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (allowBrowse && browseAction != null && GUILayout.Button("浏览", GUILayout.Width(60)))
                {
                    browseAction();
                }

                if (allowOpenOnly && GUILayout.Button("打开", GUILayout.Width(60)))
                {
                    RevealPath(absolutePath);
                }
            }
        }

        private void DrawFilePathSelector(GUIContent label, string displayValue, string absolutePath, Action browseAction)
        {
            EditorGUILayout.LabelField(label);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.SelectableLabel(displayValue, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                if (GUILayout.Button("浏览", GUILayout.Width(60)))
                {
                    browseAction();
                }

                if (GUILayout.Button("打开", GUILayout.Width(60)))
                {
                    RevealPath(absolutePath);
                }
            }
        }

        private void DrawReadonlyAssemblyList(GUIContent title, IReadOnlyList<string> items)
        {
            EditorGUILayout.LabelField(title);
            if (items == null || items.Count == 0)
            {
                EditorGUILayout.HelpBox("当前列表为空。可以点击上方自动填写按钮生成。", MessageType.Warning);
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField($"共 {items.Count} 项");
                foreach (string item in items)
                {
                    EditorGUILayout.LabelField($"- {item}");
                }
            }
        }

        private static void DrawStringArray(string title, ref string[] values)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            List<string> list = values?.ToList() ?? new List<string>();
            EditorGUI.indentLevel++;
            for (int i = 0; i < list.Count; i++)
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

        private void AutoFillAotAssemblies()
        {
            string absolutePath = Path.GetFullPath(Path.Combine(GetProjectRootPath(), AotGenericReferencesPath));
            if (!File.Exists(absolutePath))
            {
                EditorUtility.DisplayDialog("未找到文件", $"未找到 AOT 引用文件：\n{absolutePath}", "确定");
                return;
            }

            string content = File.ReadAllText(absolutePath);
            Match blockMatch = Regex.Match(
                content,
                @"PatchedAOTAssemblyList\s*=\s*new\s+List<string>\s*\{(?<body>[\s\S]*?)\}",
                RegexOptions.Multiline);
            if (!blockMatch.Success)
            {
                EditorUtility.DisplayDialog("解析失败", "未能从 AOTGenericReferences.cs 中解析 PatchedAOTAssemblyList。", "确定");
                return;
            }

            MatchCollection itemMatches = Regex.Matches(blockMatch.Groups["body"].Value, "\"([^\"]+\\.dll)\"");
            string[] items = itemMatches
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            config.AotAssemblies = items;
            EditorUtility.SetDirty(config);
            ShowNotification(new GUIContent($"已自动填写 {items.Length} 个 AOT DLL"));
        }

        private void AutoFillHotUpdateAssemblies()
        {
            string scriptsRoot = Path.Combine(GetProjectRootPath(), "Assets", "Scripts");
            string hotfixRoot = Path.Combine(scriptsRoot, "Hotfix");
            List<string> assemblyNames = new List<string>();

            if (Directory.Exists(hotfixRoot))
            {
                foreach (string asmdefPath in Directory.GetFiles(hotfixRoot, "*.asmdef", SearchOption.AllDirectories))
                {
                    string assemblyName = ParseAsmdefName(asmdefPath);
                    if (!string.IsNullOrWhiteSpace(assemblyName))
                    {
                        assemblyNames.Add($"{assemblyName}.dll");
                    }
                }
            }

            if (assemblyNames.Count == 0 && Directory.Exists(scriptsRoot))
            {
                foreach (string asmdefPath in Directory.GetFiles(scriptsRoot, "*.asmdef", SearchOption.AllDirectories))
                {
                    if (asmdefPath.IndexOf($"{Path.DirectorySeparatorChar}Editor{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        continue;
                    }

                    string assemblyName = ParseAsmdefName(asmdefPath);
                    if (string.IsNullOrWhiteSpace(assemblyName) ||
                        assemblyName.Equals("Core.Runtime", StringComparison.OrdinalIgnoreCase) ||
                        assemblyName.Equals("Core.Editor", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (assemblyName.Contains("Hotfix", StringComparison.OrdinalIgnoreCase) ||
                        assemblyName.Contains("HotUpdate", StringComparison.OrdinalIgnoreCase))
                    {
                        assemblyNames.Add($"{assemblyName}.dll");
                    }
                }
            }

            config.HotUpdateAssemblies = assemblyNames
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            EditorUtility.SetDirty(config);
            ShowNotification(new GUIContent($"已自动填写 {config.HotUpdateAssemblies.Length} 个热更 DLL"));
        }

        private static string ParseAsmdefName(string asmdefPath)
        {
            string content = File.ReadAllText(asmdefPath);
            Match match = Regex.Match(content, "\"name\"\\s*:\\s*\"([^\"]+)\"");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private void ReplaceAssemblies(bool useStrippedAot)
        {
            string aotSource = useStrippedAot ? config.AotStrippedSourcePath : config.AotSourcePath;
            CopyAssemblies(aotSource, config.AotTargetPath, config.AotAssemblies);
            CopyAssemblies(config.HotUpdateSourcePath, config.HotUpdateTargetPath, config.HotUpdateAssemblies);
            AssetDatabase.Refresh();
            SaveConfig();
            ShowNotification(new GUIContent("DLL 替换完成"));
        }

        private static void CopyAssemblies(string source, string target, IEnumerable<string> files)
        {
            Directory.CreateDirectory(target);
            foreach (string file in files.Where(item => !string.IsNullOrWhiteSpace(item)))
            {
                string sourcePath = Path.Combine(source, file);
                string targetPath = Path.Combine(target, $"{Path.GetFileNameWithoutExtension(file)}.dll.bytes");
                if (!File.Exists(sourcePath))
                {
                    Debug.LogWarning($"文件不存在: {sourcePath}");
                    continue;
                }

                File.Copy(sourcePath, targetPath, true);
                Debug.Log($"已复制: {sourcePath} -> {targetPath}");
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

            foreach (string sourcePath in Directory.GetFiles(config.AotSourcePath, "*.dll"))
            {
                string targetPath = Path.Combine(config.AotStrippedSourcePath, Path.GetFileName(sourcePath));
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
            string platform = pc ? "StandaloneWindows64" : "Android";
            target.PackageName = string.IsNullOrWhiteSpace(target.PackageName) ? ResourceInitializeOptions.DefaultPackageName : target.PackageName;
            target.AotSourcePath = $"HybridCLRData/AssembliesPostIl2CppStrip/{platform}";
            target.AotStrippedSourcePath = $"HybridCLRData/StrippedAOTAssembly2/{platform}";
            target.HotUpdateSourcePath = $"HybridCLRData/HotUpdateDlls/{platform}";
            target.LocalBundlePath = $"Bundles/{platform}/{target.PackageName}";

            if (string.IsNullOrWhiteSpace(target.KeyFilePath))
            {
                target.KeyFilePath = "Assets/LoadResources/Config/key";
            }
        }

        private void RefreshLocalBundlePathToLatest()
        {
            string projectRoot = GetProjectRootPath();
            string basePath = Path.GetFullPath(Path.Combine(projectRoot, $"Bundles/{(isPcPlatform ? "StandaloneWindows64" : "Android")}/{config.PackageName}"));
            if (!Directory.Exists(basePath))
            {
                config.LocalBundlePath = Path.GetRelativePath(projectRoot, basePath).Replace('\\', '/');
                return;
            }

            string latest = Directory.GetDirectories(basePath)
                .OrderByDescending(path => new DirectoryInfo(path).CreationTimeUtc)
                .FirstOrDefault();
            config.LocalBundlePath = Path.GetRelativePath(projectRoot, latest ?? basePath).Replace('\\', '/');
        }

        private void SaveConfig()
        {
            EnsureDefaults();
            SyncBaseServerUrlFromMode(false);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        private void SetLocalMockServerEnabled(bool enabled)
        {
            config.UseLocalMockServer = enabled;
            if (enabled)
            {
                StartLocalMockServer(true);
                return;
            }

            StopLocalMockServer(false);
            SyncBaseServerUrlFromMode(true);
            SaveConfig();
        }

        private bool StartLocalMockServer(bool showFeedback)
        {
            try
            {
                EnsureDefaults();
                string mockServerRoot = GetMockServerRootAbsolutePath();
                int requestedPort = config.LocalServerPort;
                int resolvedPort = LocalBundleHttpServer.FindAvailablePort(requestedPort);
                bool portChanged = resolvedPort != requestedPort;
                config.LocalServerPort = resolvedPort;

                LocalBundleHttpServer.Start(mockServerRoot, resolvedPort);
                SyncBaseServerUrlFromMode(true);
                SaveConfig();

                if (showFeedback)
                {
                    ShowNotification(new GUIContent(portChanged ? $"端口已切换到 {resolvedPort}" : "本地模拟服务器已启动"));
                    if (portChanged)
                    {
                        Debug.LogWarning($"本地模拟服务器请求端口 {requestedPort} 已被占用，已自动切换到可用端口 {resolvedPort}。");
                    }

                    Debug.Log($"本地模拟服务器已启动: {config.BaseServerURL} -> {mockServerRoot}");
                }

                return true;
            }
            catch (Exception exception)
            {
                config.UseLocalMockServer = false;
                SyncBaseServerUrlFromMode(true);
                if (showFeedback)
                {
                    EditorUtility.DisplayDialog("启动失败", exception.Message, "确定");
                }

                Debug.LogError($"启动本地模拟服务器失败: {exception}");
                return false;
            }
        }

        private void StopLocalMockServer(bool showFeedback)
        {
            LocalBundleHttpServer.Stop();
            if (showFeedback)
            {
                ShowNotification(new GUIContent("本地模拟服务器已停止"));
            }
        }

        private void TryStartLocalMockServerSilently()
        {
            StartLocalMockServer(false);
        }

        private void TestSshConnection()
        {
            try
            {
                ValidateSshConfig();
                using var client = new SshClient(BuildConnectionInfo());
                client.Connect();
                client.RunCommand("echo 'Connection test success!'");
                client.Disconnect();
                ShowNotification(new GUIContent("SSH 连接成功"));
            }
            catch (Exception exception)
            {
                HandleSshException(exception);
            }
        }

        private void UploadToServer()
        {
            string localPath = GetLocalBundleAbsolutePath();
            if (!Directory.Exists(localPath))
            {
                EditorUtility.DisplayDialog("路径错误", $"本地资源路径不存在：\n{localPath}", "确定");
                return;
            }

            string[] files = Directory.GetFiles(localPath, "*", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "没有找到待上传的资源文件。", "确定");
                return;
            }

            try
            {
                ValidateSshConfig();
                var connectionInfo = BuildConnectionInfo();
                string remotePath = BuildRemoteBundlePath();

                try
                {
                    EditorUtility.DisplayProgressBar("SSH 上传", "准备远程目录...", 0f);
                    using (var ssh = new SshClient(connectionInfo))
                    {
                        ssh.Connect();
                        ssh.RunCommand($"rm -rf \"{remotePath}\" && mkdir -p \"{remotePath}\"");
                        ssh.Disconnect();
                    }

                    using (var sftp = new SftpClient(connectionInfo))
                    {
                        sftp.Connect();
                        for (int i = 0; i < files.Length; i++)
                        {
                            string file = files[i];
                            string relativePath = Path.GetRelativePath(localPath, file).Replace('\\', '/');
                            string remoteFilePath = $"{remotePath}/{relativePath}";
                            EnsureRemoteDirectory(sftp, GetRemoteDirectory(remoteFilePath));

                            using var stream = File.OpenRead(file);
                            var stopwatch = Stopwatch.StartNew();
                            sftp.UploadFile(stream, remoteFilePath, true);
                            stopwatch.Stop();

                            double speed = stopwatch.Elapsed.TotalSeconds > 0
                                ? stream.Length / 1024d / 1024d / stopwatch.Elapsed.TotalSeconds
                                : 0d;
                            EditorUtility.DisplayProgressBar("SSH 上传", $"[{i + 1}/{files.Length}] {relativePath} {speed:F2} MB/s", (i + 1f) / files.Length);
                        }

                        sftp.Disconnect();
                    }
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                }

                ShowNotification(new GUIContent("上传完成"));
            }
            catch (Exception exception)
            {
                EditorUtility.ClearProgressBar();
                HandleSshException(exception);
            }
        }

        private ConnectionInfo BuildConnectionInfo()
        {
            var keyFile = new PrivateKeyFile(GetKeyFileAbsolutePath());
            var authMethod = new PrivateKeyAuthenticationMethod(config.SshUser, keyFile);
            return new ConnectionInfo(config.SshHost, config.SshPort, config.SshUser, authMethod);
        }

        private string BuildRemoteBundlePath()
        {
            string root = config.ServerBasePath.Trim().TrimEnd('/');
            string platformFolder = isPcPlatform ? "PC" : "Android";
            return $"{root}/{platformFolder}";
        }

        private void ValidateSshConfig()
        {
            if (string.IsNullOrWhiteSpace(config.SshHost) ||
                string.IsNullOrWhiteSpace(config.SshUser) ||
                string.IsNullOrWhiteSpace(config.KeyFilePath) ||
                string.IsNullOrWhiteSpace(config.ServerBasePath))
            {
                throw new InvalidOperationException("SSH 配置不完整，请检查服务器地址、用户名、私钥路径和远程根路径。");
            }

            string keyFileAbsolutePath = GetKeyFileAbsolutePath();
            if (!File.Exists(keyFileAbsolutePath))
            {
                throw new FileNotFoundException($"私钥文件不存在: {keyFileAbsolutePath}", keyFileAbsolutePath);
            }
        }

        private void HandleSshException(Exception exception)
        {
            string errorMsg = exception.Message;
            if (errorMsg.Contains("invalid private key", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("私钥错误", "私钥格式无效或路径错误。", "确定");
            }
            else if (errorMsg.Contains("Permission denied", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("权限错误", "服务器公钥未配置或用户无权限。", "确定");
            }
            else if (errorMsg.Contains("No such file or directory", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("路径错误", "服务器目录不存在或无法访问。", "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("连接错误", errorMsg, "确定");
            }

            Debug.LogError($"SSH Error: {exception}");
        }

        private void BrowseLocalBundlePath()
        {
            BrowseFolderPath(value => config.LocalBundlePath = value, GetLocalBundleAbsolutePath(), "选择 Bundle 目录");
        }

        private void BrowseMockServerFolderPath()
        {
            BrowseFolderPath(value => config.MockServerFolderPath = value, GetMockServerRootAbsolutePath(), "选择模拟服务器目录");
            if (config.UseLocalMockServer)
            {
                TryStartLocalMockServerSilently();
            }
        }

        private void BrowseFolderPath(Action<string> assignAction, string currentAbsolutePath, string title)
        {
            string initialPath = Directory.Exists(currentAbsolutePath) ? currentAbsolutePath : GetProjectRootPath();
            string selectedPath = EditorUtility.OpenFolderPanel(title, initialPath, string.Empty);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            assignAction(ConvertToRelativePathIfPossible(selectedPath));
            EditorUtility.SetDirty(config);
        }

        private void BrowseFilePath(Action<string> assignAction, string initialDirectory, string title)
        {
            string selectedPath = EditorUtility.OpenFilePanel(title, initialDirectory, string.Empty);
            if (string.IsNullOrEmpty(selectedPath))
            {
                return;
            }

            assignAction(ConvertToRelativePathIfPossible(selectedPath));
            EditorUtility.SetDirty(config);
        }

        private void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(config.LocalServerHost))
            {
                config.LocalServerHost = "127.0.0.1";
            }

            if (config.LocalServerPort <= 0)
            {
                config.LocalServerPort = 18080;
            }

            if (string.IsNullOrWhiteSpace(config.KeyFilePath))
            {
                config.KeyFilePath = "Assets/LoadResources/Config/key";
            }
        }

        private void SyncBaseServerUrlFromMode(bool markDirty)
        {
            config.BaseServerURL = config.UseLocalMockServer
                ? BuildBaseServerUrl(config.LocalServerHost, config.LocalServerPort)
                : config.RemoteBaseServerURL;

            if (markDirty)
            {
                EditorUtility.SetDirty(config);
            }
        }

        private string GetLocalBundleAbsolutePath()
        {
            return ResolvePath(config.LocalBundlePath);
        }

        private string GetMockServerRootAbsolutePath()
        {
            string configuredPath = string.IsNullOrWhiteSpace(config.MockServerFolderPath)
                ? config.LocalBundlePath
                : config.MockServerFolderPath;
            return ResolvePath(configuredPath);
        }

        private string GetKeyFileAbsolutePath()
        {
            return ResolvePath(config.KeyFilePath);
        }

        private string ResolvePath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
            {
                return GetProjectRootPath();
            }

            if (Path.IsPathRooted(configuredPath))
            {
                return configuredPath;
            }

            return Path.GetFullPath(Path.Combine(GetProjectRootPath(), configuredPath));
        }

        private string ConvertToRelativePathIfPossible(string absolutePath)
        {
            string projectRoot = GetProjectRootPath();
            string normalizedAbsolutePath = Path.GetFullPath(absolutePath);
            if (normalizedAbsolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                return Path.GetRelativePath(projectRoot, normalizedAbsolutePath).Replace('\\', '/');
            }

            return normalizedAbsolutePath;
        }

        private static void EnsureRemoteDirectory(SftpClient sftp, string remoteDirectory)
        {
            if (string.IsNullOrWhiteSpace(remoteDirectory) || remoteDirectory == "/")
            {
                return;
            }

            string[] segments = remoteDirectory.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            string current = remoteDirectory.StartsWith("/", StringComparison.Ordinal) ? "/" : string.Empty;
            foreach (string segment in segments)
            {
                current = current.EndsWith("/", StringComparison.Ordinal) || string.IsNullOrEmpty(current)
                    ? $"{current}{segment}"
                    : $"{current}/{segment}";

                if (!sftp.Exists(current))
                {
                    sftp.CreateDirectory(current);
                }
            }
        }

        private static string GetRemoteDirectory(string remoteFilePath)
        {
            int lastSlash = remoteFilePath.LastIndexOf('/');
            return lastSlash > 0 ? remoteFilePath.Substring(0, lastSlash) : "/";
        }

        private static string BuildBaseServerUrl(string host, int port)
        {
            return $"http://{host}:{port}";
        }

        private static void RevealPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!File.Exists(path) && !Directory.Exists(path))
            {
                EditorUtility.DisplayDialog("路径不存在", path, "确定");
                return;
            }

            EditorUtility.RevealInFinder(path);
        }

        private static string GetProjectRootPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../"));
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
