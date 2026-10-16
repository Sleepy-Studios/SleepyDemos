using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Core.Runtime;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;
using Debug = UnityEngine.Debug;

namespace Core.Editor.Hotfix
{
    public sealed class HotfixBuildWindow : EditorWindow
    {
        private const string ConfigAssetPath = "Assets/LoadResources/Config/HotfixConfig.asset";
        private const string AotGenericReferencesPath = "Assets/HybridCLRGenerate/AOTGenericReferences.cs";

        private HotfixConfig config;
        private BuildTarget buildTarget;
        private Vector2 scrollPosition;
        private string resourceVersion = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");

        private bool showGeneralSection = true;
        private bool showHybridClrSection = true;
        private bool showBuildSection = true;
        private bool showMockServerSection = true;
        private bool showRemoteSection = true;
        private bool showDeploymentSection = true;
        private bool showAdvancedPaths;
        private bool showManualAssemblyEdit;

        private enum UploadMethod
        {
            Ssh,
            GitHubPages
        }

        private static readonly string[] UploadMethodNames = { "SSH", "GitHub Pages" };
        private UploadMethod uploadMethod;
        private string pagesRepositoryPath;
        private string pagesSiteUrl;
        private string pagesProjectName;
        private string sshServerUrl;
        private string sshHostKeySha256;
        private static bool isDeploying;
        private static string deploymentStatus;
        private bool initializationPending;

        private static bool SharedConfigurationLocked => EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SLEEPY_BUILD_REQUEST"));

        private string DeploymentSettingsKey => $"HotfixBuildWindow.{Application.dataPath}.{AssetDatabase.GetAssetPath(config)}.";

        [MenuItem("Tools/UI Framework/Hotfix Build")]
        public static void Open()
        {
            GetWindow<HotfixBuildWindow>("Hotfix Build");
        }

        [MenuItem("Tools/一键打包工具")]
        public static void OpenChinese()
        {
            Open();
        }

        public static void CreateHotfixConfigFromProject()
        {
            string path = EditorUtility.SaveFilePanelInProject(
                "Create HotfixConfig",
                "HotfixConfig",
                "asset",
                "选择 HotfixConfig 保存位置",
                "Assets/LoadResources/Config");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var asset = CreateInstance<HotfixConfig>();
            ApplyPlatformDefaults(asset);
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }

        private void OnEnable()
        {
            initializationPending = true;
            if (SharedConfigurationLocked)
            {
                config = AssetDatabase.LoadAssetAtPath<HotfixConfig>(ConfigAssetPath);
                buildTarget = EditorUserBuildSettings.activeBuildTarget;
                return;
            }
            LoadOrCreateConfig();
            LoadDeploymentSettings();
            SyncPlatformConfig();
            initializationPending = false;
        }

        private void SyncPlatformConfig()
        {
            if (SharedConfigurationLocked || config == null) return;
            buildTarget = EditorUserBuildSettings.activeBuildTarget;
            ApplyPlatformDefaults(config);
            RefreshLocalBundlePathToLatest();
            EnsureDefaults();
            SyncBaseServerUrlFromMode(true);

            if (config.UseLocalMockServer)
            {
                TryStartLocalMockServerSilently();
            }
        }

        private void OnGUI()
        {
            if (SharedConfigurationLocked)
            {
                EditorGUILayout.HelpBox("运行、编译或自动化构建期间配置只读，窗口不会覆盖构建请求。", MessageType.Info);
                if (config != null)
                {
                    EditorGUILayout.LabelField("发布游戏", config.GameId);
                    EditorGUILayout.LabelField("当前生效 URL", config.BaseServerURL);
                }
                return;
            }
            if (initializationPending) OnEnable();
            if (config != null && !isDeploying && buildTarget != EditorUserBuildSettings.activeBuildTarget)
            {
                SyncPlatformConfig();
            }

            using (new EditorGUI.DisabledScope(isDeploying))
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
        }

        private void DrawConfigSection()
        {
            HotfixConfig previousConfig = config;
            EditorGUILayout.LabelField("Hotfix 配置", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                config = (HotfixConfig)EditorGUILayout.ObjectField(new GUIContent("Config", "当前编辑的热更新配置资源"), config, typeof(HotfixConfig), false);
                if (GUILayout.Button(new GUIContent("新建/定位默认配置", "创建或选中默认 HotfixConfig 资源"), GUILayout.Width(130)))
                {
                    LoadOrCreateConfig();
                    Selection.activeObject = config;
                }
            }

            if (config == null)
            {
                EditorGUILayout.HelpBox("没有 HotfixConfig。点击上方按钮创建默认配置。", MessageType.Warning);
                return;
            }

            if (config != previousConfig)
            {
                LoadDeploymentSettings();
                SyncPlatformConfig();
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
            EditorGUILayout.LabelField("Unity 当前构建平台", buildTarget.ToString());
            using (new EditorGUILayout.HorizontalScope())
            {
                bool isPc = buildTarget == BuildTarget.StandaloneWindows64;
                bool isAndroid = buildTarget == BuildTarget.Android;
                bool isWeb = buildTarget == BuildTarget.WebGL;
                bool nextPc = GUILayout.Toggle(isPc, "PC 平台", "Button", GUILayout.Width(120), GUILayout.Height(28));
                bool nextAndroid = GUILayout.Toggle(isAndroid, "Android 平台", "Button", GUILayout.Width(120), GUILayout.Height(28));
                bool nextWeb = GUILayout.Toggle(isWeb, "WebGL / TapTap", "Button", GUILayout.Width(140), GUILayout.Height(28));
                if ((nextPc && !isPc) || (nextAndroid && !isAndroid) || (nextWeb && !isWeb))
                {
                    string platform = nextWeb && !isWeb ? "WebGL" : nextAndroid && !isAndroid ? "Android" : "Windows (64-bit)";
                    EditorUtility.DisplayDialog(
                        "请先切换 Unity 构建平台",
                        $"请在 File > Build Profiles 中将当前平台切换为 {platform}。\n切换完成后，此窗口会自动更新平台状态及 DLL、Bundle 路径。",
                        "知道了");
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
            var profiles = ReleaseProfiles.All;
            int currentProfile = Array.FindIndex(profiles, item => item.GameId == config.GameId);
            int selectedProfile = EditorGUILayout.Popup("发布游戏", Mathf.Max(0, currentProfile), profiles.Select(item => item.GameId + " / " + item.Group).ToArray());
            if (selectedProfile != currentProfile)
            {
                string previous = config.GameId;
                config.GameId = profiles[selectedProfile].GameId;
                if (!PrepareRelease())
                {
                    config.GameId = previous;
                }
                else
                {
                    SyncPlatformConfig();
                }
            }
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

                if (config.GameId == "hub" && GUILayout.Button(new GUIContent("校验并打开 YooAssets 打包窗口", "应用当前发布目标并检查跨游戏依赖，然后打开 YooAssets 官方资源打包窗口")) && PrepareRelease(true))
                {
                    BundleBuilderWindow.OpenWindow();
                }
            }

            if (config.GameId != "hub")
            {
                resourceVersion = EditorGUILayout.TextField("本地资源版本", resourceVersion);
                if (GUILayout.Button("构建当前游戏本地资源") && PrepareRelease())
                {
                    // 从当前游戏的隔离归档刷新公共暂存区，不能复用上一次游戏的 DLL。
                    StripAotAssemblies();
                    ReplaceAssemblies(true);
                    var result = ReleaseProfiles.BuildResources(config, buildTarget, resourceVersion,
                        ResolvePath(GetBundleOutputRoot(config)), EBundledCopyOption.None);
                    config.LocalBundlePath = ConvertToRelativePathIfPossible(result.OutputPackageDirectory);
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                    RevealPath(result.OutputPackageDirectory);
                }
                EditorGUILayout.HelpBox("本地资源按 GameId 隔离，不写入首包 StreamingAssets。正式整包、基线与远端发布请使用对应 Jenkins 任务。", MessageType.Info);
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
                config.HotfixSourcePath,
                ResolvePath(config.HotfixSourcePath),
                () => BrowseFolderPath(value => config.HotfixSourcePath = value, ResolvePath(config.HotfixSourcePath), "选择热更源目录"));

            DrawFolderPathSelector(
                new GUIContent("热更目标路径", "热更 dll.bytes 拷贝到项目资源目录的位置。"),
                config.HotfixTargetPath,
                ResolvePath(config.HotfixTargetPath),
                () => BrowseFolderPath(value => config.HotfixTargetPath = value, ResolvePath(config.HotfixTargetPath), "选择热更目标目录"));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("自动填写热更列表", "根据项目里的热更 asmdef 自动推断热更新程序集。目前会优先识别 Assets/Scripts/Hotfix 下的程序集。")))
                {
                    AutoFillHotfixAssemblies();
                }

                if (GUILayout.Button(new GUIContent("清空程序集列表", "清空当前 AOT 与热更程序集列表，便于重新自动生成")))
                {
                    config.AotAssemblies = Array.Empty<string>();
                    config.HotfixAssemblies = Array.Empty<string>();
                    EditorUtility.SetDirty(config);
                }
            }

            DrawReadonlyAssemblyList(
                new GUIContent("热更文件列表", "运行时需要动态加载的热更新 DLL 列表。当前项目默认通常包含 Hotfix.dll。"),
                config.HotfixAssemblies);

            showManualAssemblyEdit = EditorGUILayout.Foldout(showManualAssemblyEdit, "手动编辑程序集列表", true);
            if (showManualAssemblyEdit)
            {
                DrawStringArray("AOT 文件列表（手动）", ref config.AotAssemblies);
                EditorGUILayout.Space(4);
                DrawStringArray("热更文件列表（手动）", ref config.HotfixAssemblies);
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
                if (GUILayout.Button(new GUIContent("生成并编译全部 DLL", "执行 HybridCLR 全量生成与编译流程")) && PrepareRelease())
                {
                    GenerateAllAssemblies();
                    AutoFillAotAssemblies();
                    AutoFillHotfixAssemblies();
                    HotfixAssemblyBuild.CaptureReleaseAssemblies(config, buildTarget, true);
                }

                if (GUILayout.Button(new GUIContent("仅编译热更 DLL", "只编译热更新程序集，不重新生成全部 AOT 相关内容")) && PrepareRelease())
                {
                    CompileDllCommand.CompileDllActiveBuildTarget();
                    AssetDatabase.Refresh();
                    AutoFillHotfixAssemblies();
                    HotfixAssemblyBuild.CaptureReleaseAssemblies(config, buildTarget, false);
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

            if (GUILayout.Button(new GUIContent("一键编译并同步", "执行全量编译，自动刷新 AOT/热更列表，剥离 AOT，再将剥离后的 AOT DLL 和热更 DLL 同步到资源目录。")) && PrepareRelease())
            {
                GenerateAllAssemblies();
                AutoFillAotAssemblies();
                AutoFillHotfixAssemblies();
                HotfixAssemblyBuild.CaptureReleaseAssemblies(config, buildTarget, true);
                StripAotAssemblies();
                ReplaceAssemblies(true);
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
                EditorGUILayout.HelpBox("开启后才会显示本地模拟服务器配置。关闭后恢复所选上传方式的真实远端 URL。", MessageType.None);
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
            if (config.GameId != "hub")
            {
                EditorGUILayout.HelpBox("独立游戏远端配置由对应 Jenkins 请求按渠道、GameId 与平台管理，此窗口保留当前请求 URL。", MessageType.Info);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("真实远端 URL", config.RemoteBaseServerURL);
                }
                return;
            }
            showRemoteSection = EditorGUILayout.Foldout(showRemoteSection, "真实远端服务器 / 上传方式", true);
            if (!showRemoteSection)
            {
                return;
            }

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            var nextMethod = (UploadMethod)EditorGUILayout.Popup("上传方式", (int)uploadMethod, UploadMethodNames);
            if (nextMethod != uploadMethod)
            {
                if (uploadMethod == UploadMethod.Ssh)
                {
                    sshServerUrl = config.RemoteBaseServerURL;
                }

                uploadMethod = nextMethod;
                if (uploadMethod == UploadMethod.Ssh)
                {
                    config.RemoteBaseServerURL = sshServerUrl;
                }
            }

            if (config.UseLocalMockServer)
            {
                EditorGUILayout.HelpBox("当前启用了本地模拟模式，真实远端部署按钮不可用。", MessageType.None);
            }

            if (uploadMethod == UploadMethod.GitHubPages)
            {
                DrawFolderPathSelector(
                    new GUIContent("Pages 本地仓库", "网站仓库的本地检出目录，必须位于 Unity 工程之外。"),
                    pagesRepositoryPath,
                    ResolvePath(pagesRepositoryPath),
                    () => BrowseFolderPath(value => pagesRepositoryPath = value, ResolvePath(pagesRepositoryPath), "选择 Pages 仓库"));
                pagesSiteUrl = EditorGUILayout.TextField(new GUIContent("网站根 URL", "例如 https://h-bole.github.io/H-Bole.io，勿追加资源目录。"), pagesSiteUrl);
                pagesProjectName = EditorGUILayout.TextField(new GUIContent("Unity 项目目录", "只使用字母、数字、短横线或下划线；自动追加 hotupdate 和当前平台。"), pagesProjectName);
                if (!TryGetPagesRemoteUrl(out _))
                {
                    EditorGUILayout.HelpBox("请填写有效的 HTTPS 网站根 URL 和 Unity 项目目录名。", MessageType.Warning);
                }

                EditorGUILayout.HelpBox("资源目录：unity/<项目>/hotupdate/<平台>。PC 对应 Windows64，其他平台使用 Unity 构建平台名。需要 git、gh，并已通过 gh auth login 登录。", MessageType.None);
            }
            else
            {
                config.RemoteBaseServerURL = EditorGUILayout.TextField(
                    new GUIContent("真实远端 URL", "关闭本地模拟后，运行时会使用这个地址拉取版本与资源。"),
                    config.RemoteBaseServerURL);
                sshServerUrl = config.RemoteBaseServerURL;
            }

            if (EditorGUI.EndChangeCheck())
            {
                SyncBaseServerUrlFromMode(true);
                SaveDeploymentSettings();
                EditorUtility.SetDirty(config);
            }

            if (uploadMethod == UploadMethod.GitHubPages)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.TextField("真实远端 URL", config.RemoteBaseServerURL);
                }

                EditorGUI.indentLevel--;
                return;
            }

            sshHostKeySha256 = EditorGUILayout.TextField(new GUIContent("主机 SHA256 指纹", "必填，使用通过可信渠道核对的 SSH 主机公钥 SHA256:... 指纹。"), sshHostKeySha256);
            if (GUI.changed)
            {
                EditorPrefs.SetString(DeploymentSettingsKey + "SshHostKeySha256", sshHostKeySha256);
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

            if (config.GameId != "hub")
            {
                EditorGUILayout.HelpBox("独立游戏的正式发布由 Jenkins 按渠道、GameId 与平台隔离。此窗口仅用于本地 DLL 和资源构建，不提供独立游戏远端发布。", MessageType.Info);
                return;
            }

            EditorGUI.indentLevel++;
            if (config.UseLocalMockServer)
            {
                EditorGUILayout.HelpBox("当前为本地模拟模式，不需要远端上传。模拟服务会直接读取你配置的模拟服务器目录。", MessageType.Info);
            }
            else if (uploadMethod == UploadMethod.GitHubPages)
            {
                EditorGUILayout.HelpBox("增量上传保留旧包，只同步当前构建的版本、清单、hash 和 Bundle。操作会提交并推送 main，Pages 部署完成后才可下载。请先核对当前 Bundle 路径是否为本轮最新构建。", MessageType.Info);
                using (new EditorGUI.DisabledScope(!TryGetPagesRemoteUrl(out _) || string.IsNullOrWhiteSpace(pagesRepositoryPath)))
                {
                    if (GUILayout.Button("增量上传到 GitHub Pages"))
                    {
                        DeployPagesAsync(false);
                    }

                    if (GUILayout.Button("清空当前平台目录"))
                    {
                        DeployPagesAsync(true);
                    }
                }
            }
            else if (GUILayout.Button(new GUIContent("开始上传到 SSH 服务器", "将当前 Bundle 目录下的所有文件上传到远端服务器")))
            {
                UploadToServer();
            }

            if (!string.IsNullOrEmpty(deploymentStatus))
            {
                EditorGUILayout.HelpBox(deploymentStatus, MessageType.Info);
            }

            EditorGUI.indentLevel--;
        }

        private async void DeployPagesAsync(bool clearDirectory)
        {
            if (config.GameId != "hub")
            {
                return;
            }
            if (isDeploying)
            {
                return;
            }

            bool reloadLocked = false;
            try
            {
                string resourcePath = GitHubPagesDeployment.GetResourcePath(pagesProjectName, GetPlatformFolder());
                string repositoryPath = ResolvePath(pagesRepositoryPath);
                if (clearDirectory && !EditorUtility.DisplayDialog(
                        "清空当前平台资源",
                        $"将清空并提交、推送以下目录中的文件：\n{Path.Combine(repositoryPath, resourcePath)}\n\n旧客户端可能仍依赖这些资源，请确认后再清空。其他项目和平台不受影响。",
                        "清空并发布", "取消"))
                {
                    return;
                }

                SaveConfig();
                EditorApplication.LockReloadAssemblies();
                reloadLocked = true;
                isDeploying = true;
                deploymentStatus = clearDirectory ? "正在清空并发布当前平台目录…" : "正在增量上传…";
                Repaint();
                deploymentStatus = clearDirectory
                    ? await GitHubPagesDeployment.ClearAsync(repositoryPath, resourcePath)
                    : await GitHubPagesDeployment.UploadAsync(repositoryPath, resourcePath, GetLocalBundleAbsolutePath(), config.PackageName);
                Debug.Log($"[GitHub Pages] {deploymentStatus}");
            }
            catch (Exception exception)
            {
                deploymentStatus = $"部署失败：{exception.Message}";
                Debug.LogError($"[GitHub Pages] {deploymentStatus}");
            }
            finally
            {
                if (reloadLocked)
                {
                    EditorApplication.UnlockReloadAssemblies();
                }

                isDeploying = false;
                if (this != null)
                {
                    Repaint();
                }
            }
        }

        private static string GetPlatformFolder()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            return target == BuildTarget.StandaloneWindows64 ? "PC" : target.ToString();
        }

        private void LoadDeploymentSettings()
        {
            if (SharedConfigurationLocked || config == null) return;
            string key = DeploymentSettingsKey;
            sshHostKeySha256 = EditorPrefs.GetString(key + "SshHostKeySha256", string.Empty);
            string projectName = new DirectoryInfo(GetProjectRootPath()).Name;
            string pagesMarker = $"/unity/{projectName}/hotupdate/";
            int markerIndex = config.RemoteBaseServerURL?.IndexOf(pagesMarker, StringComparison.Ordinal) ?? -1;
            string configuredSiteUrl = markerIndex > 0 ? config.RemoteBaseServerURL.Substring(0, markerIndex) : string.Empty;
            string repositoryName = Uri.TryCreate(configuredSiteUrl, UriKind.Absolute, out Uri siteUri)
                ? siteUri.AbsolutePath.Trim('/').Split('/').LastOrDefault()
                : string.Empty;
            string repositoryDefault = string.IsNullOrEmpty(repositoryName)
                ? string.Empty
                : Path.GetFullPath(Path.Combine(GetProjectRootPath(), "..", repositoryName));

            uploadMethod = (UploadMethod)EditorPrefs.GetInt(key + "UploadMethod", markerIndex > 0 ? (int)UploadMethod.GitHubPages : (int)UploadMethod.Ssh);
            pagesRepositoryPath = EditorPrefs.GetString(key + "PagesRepositoryPath", repositoryDefault);
            pagesSiteUrl = EditorPrefs.GetString(key + "PagesSiteUrl", configuredSiteUrl);
            pagesProjectName = EditorPrefs.GetString(key + "PagesProjectName", projectName);
            sshServerUrl = EditorPrefs.GetString(key + "SshServerUrl", markerIndex > 0 ? string.Empty : config.RemoteBaseServerURL);
            if (config.GameId == "hub" && uploadMethod == UploadMethod.Ssh)
            {
                config.RemoteBaseServerURL = sshServerUrl;
            }
        }

        private void SaveDeploymentSettings()
        {
            string key = DeploymentSettingsKey;
            EditorPrefs.SetInt(key + "UploadMethod", (int)uploadMethod);
            EditorPrefs.SetString(key + "PagesRepositoryPath", pagesRepositoryPath);
            EditorPrefs.SetString(key + "PagesSiteUrl", pagesSiteUrl);
            EditorPrefs.SetString(key + "PagesProjectName", pagesProjectName);
            EditorPrefs.SetString(key + "SshServerUrl", sshServerUrl);
        }

        private bool TryGetPagesRemoteUrl(out string remoteUrl)
        {
            remoteUrl = string.Empty;
            if (!Uri.TryCreate(pagesSiteUrl?.Trim().TrimEnd('/'), UriKind.Absolute, out Uri siteUri) ||
                siteUri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(siteUri.Query) ||
                !string.IsNullOrEmpty(siteUri.Fragment) || !string.IsNullOrEmpty(siteUri.UserInfo))
            {
                return false;
            }

            try
            {
                remoteUrl = $"{siteUri.AbsoluteUri.TrimEnd('/')}/{GitHubPagesDeployment.GetResourcePath(pagesProjectName, GetPlatformFolder())}";
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
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
            try
            {
                config.AotAssemblies = HotfixAssemblyBuild.ReadAotAssemblies();
                EditorUtility.SetDirty(config);
                ShowNotification(new GUIContent($"已自动填写 {config.AotAssemblies.Length} 个 AOT DLL"));
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("解析失败", exception.Message, "确定");
            }
        }

        private void AutoFillHotfixAssemblies()
        {
            config.HotfixAssemblies = HotfixAssemblyBuild.ReadHotfixAssemblies(config.GameId, buildTarget);
            EditorUtility.SetDirty(config);
            ShowNotification(new GUIContent($"已自动填写 {config.HotfixAssemblies.Length} 个热更 DLL"));
        }

        private void GenerateAllAssemblies()
        {
            string configPath = AssetDatabase.GetAssetPath(config);
            HotfixBuildAutomation.EnsureHybridClrInstalled();
            PrebuildCommand.GenerateAll();
            AssetDatabase.Refresh();
            // GenerateAll 构建内部 Player 后可能卸载原 native 对象。
            config = AssetDatabase.LoadAssetAtPath<HotfixConfig>(configPath)
                ?? throw new InvalidOperationException("全量生成后无法重新加载 HotfixConfig。");
        }

        private bool PrepareRelease(bool validateDependencies = false)
        {
            if (SharedConfigurationLocked) return false;
            try
            {
                ReleaseProfiles.Apply(config, buildTarget, buildTarget == BuildTarget.WebGL);
                if (buildTarget == BuildTarget.WebGL)
                {
                    TapTapBuildAdapter.ConfigurePlayerSettings();
                }
                if (validateDependencies)
                {
                    ReleaseProfiles.ValidateDependencies(config, buildTarget);
                }
                return true;
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("发布配置无效", exception.Message, "确定");
                return false;
            }
        }

        private void ReplaceAssemblies(bool useStrippedAot)
        {
            HotfixAssemblyBuild.Sync(config, useStrippedAot);
            ShowNotification(new GUIContent("DLL 替换完成"));
        }

        private void StripAotAssemblies()
        {
            HotfixAssemblyBuild.StripAotAssemblies(config);
        }

        private void LoadOrCreateConfig()
        {
            if (SharedConfigurationLocked) return;
            EnsureFolder("Assets/LoadResources");
            EnsureFolder("Assets/LoadResources/Config");
            config = AssetDatabase.LoadAssetAtPath<HotfixConfig>(ConfigAssetPath);
            if (config != null)
            {
                return;
            }

            config = CreateInstance<HotfixConfig>();
            ApplyPlatformDefaults(config);
            AssetDatabase.CreateAsset(config, ConfigAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ApplyPlatformDefaults(HotfixConfig target)
        {
            string platform = EditorUserBuildSettings.activeBuildTarget.ToString();
            target.PackageName = string.IsNullOrWhiteSpace(target.PackageName) ? ResourceInitializeOptions.DefaultPackageName : target.PackageName;
            target.AotSourcePath = $"HybridCLRData/AssembliesPostIl2CppStrip/{platform}";
            target.AotStrippedSourcePath = $"HybridCLRData/StrippedAOTAssembly2/{platform}";
            target.HotfixSourcePath = $"HybridCLRData/HotUpdateDlls/{platform}";
            if (target.GameId != "hub")
            {
                string root = $"HybridCLRData/ReleaseTargets/{ReleaseProfiles.Get(target.GameId).GameId}/{platform}";
                target.AotSourcePath = root + "/Aot";
                target.AotStrippedSourcePath = root + "/StrippedAot";
                target.HotfixSourcePath = root + "/Hotfix";
            }
            target.LocalBundlePath = $"{GetBundleOutputRoot(target)}/{platform}/{target.PackageName}";

            if (string.IsNullOrWhiteSpace(target.KeyFilePath))
            {
                target.KeyFilePath = "Assets/Settings/Hotfix/key";
            }
        }

        private void RefreshLocalBundlePathToLatest()
        {
            string projectRoot = GetProjectRootPath();
            string basePath = Path.GetFullPath(Path.Combine(projectRoot,
                $"{GetBundleOutputRoot(config)}/{EditorUserBuildSettings.activeBuildTarget}/{config.PackageName}"));
            if (!Directory.Exists(basePath))
            {
                config.LocalBundlePath = Path.GetRelativePath(projectRoot, basePath).Replace('\\', '/');
                return;
            }

            string latest = Directory.GetDirectories(basePath).Where(path => File.Exists(Path.Combine(path, config.PackageName + ".version")))
                .OrderByDescending(path => new DirectoryInfo(path).CreationTimeUtc)
                .FirstOrDefault();
            config.LocalBundlePath = Path.GetRelativePath(projectRoot, latest ?? basePath).Replace('\\', '/');
        }

        private static string GetBundleOutputRoot(HotfixConfig source) => source.GameId == "hub"
            ? "Bundles" : "Bundles/ReleaseTargets/" + ReleaseProfiles.Get(source.GameId).GameId;

        private void SaveConfig()
        {
            if (SharedConfigurationLocked || config == null) return;
            EnsureDefaults();
            SyncBaseServerUrlFromMode(false);
            SaveDeploymentSettings();
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
                SshBundleDeployment.Test(config.SshHost, config.SshPort, config.SshUser, GetKeyFileAbsolutePath(), sshHostKeySha256);
                ShowNotification(new GUIContent("SSH 连接成功"));
            }
            catch (Exception exception)
            {
                HandleSshException(exception);
            }
        }

        private void UploadToServer()
        {
            if (config.GameId != "hub")
            {
                return;
            }
            try
            {
                SshBundleDeployment.Upload(config.SshHost, config.SshPort, config.SshUser,
                    GetKeyFileAbsolutePath(), sshHostKeySha256, config.ServerBasePath, GetPlatformFolder(),
                    GetLocalBundleAbsolutePath(), config.PackageName,
                    previous => HotfixDeploymentVerification.VerifyAsync(GetLocalBundleAbsolutePath(), config.PackageName,
                        config.RemoteBaseServerURL, file => Path.GetFileName(file) != config.PackageName + ".version").GetAwaiter().GetResult(),
                    () => HotfixDeploymentVerification.VerifyAsync(GetLocalBundleAbsolutePath(), config.PackageName,
                        config.RemoteBaseServerURL, file => Path.GetFileName(file) == config.PackageName + ".version").GetAwaiter().GetResult(),
                    (restored, previous) =>
                    {
                        if (restored)
                        {
                            HotfixDeploymentVerification.VerifyRestoredVersionAsync(config.RemoteBaseServerURL,
                                config.PackageName, previous).GetAwaiter().GetResult();
                        }
                    });
                ShowNotification(new GUIContent("上传完成"));
            }
            catch (Exception exception)
            {
                HandleSshException(exception);
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
                config.KeyFilePath = "Assets/Settings/Hotfix/key";
            }
        }

        private void SyncBaseServerUrlFromMode(bool markDirty)
        {
            if (SharedConfigurationLocked || config == null) return;
            if (config.GameId == "hub" && uploadMethod == UploadMethod.GitHubPages && TryGetPagesRemoteUrl(out string pagesUrl))
            {
                config.RemoteBaseServerURL = pagesUrl;
            }

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
