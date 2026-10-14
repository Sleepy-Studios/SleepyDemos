using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Core.Runtime;
using HybridCLR.Editor.Commands;
using HybridCLR.Editor.HotUpdate;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YooAsset;
using YooAsset.Editor;
using Debug = UnityEngine.Debug;

namespace Core.Editor.Hotfix
{
    /// Jenkins 的 Prepare、Build、Deploy 无交互构建入口。
    public static class HotfixBuildAutomation
    {
        private const string ConfigPath = "Assets/LoadResources/Config/HotfixConfig.asset";

        [Serializable]
        private sealed class Request
        {
            public string Platform;
            public string BuildKind;
            public string ResourceMode;
            public string AndroidFormat;
            public string AndroidSigning;
            public bool Development;
            public string PlayerVersion;
            public int AndroidVersionCode;
            public string ResourceVersion;
            public bool Publish;
            public string Provider;
            public string SourceCommit;
            public string ConfigCommit;
            public string OutputDirectory;
            public string BaselineDirectory;
            public string PagesRepositoryPath;
            public string PagesSiteUrl;
            public string PagesProjectName;
            public string SshHost;
            public int SshPort;
            public string SshUser;
            public string SshRoot;
            public string SshServerUrl;
            public string SshHostKeySha256;
        }

        [Serializable]
        private sealed class Baseline
        {
            public string Fingerprint;
            public string[] FingerprintInputs;
            public string Platform;
            public string ResourceMode;
            public bool Development;
            public string PlayerVersion;
            public string ResourceVersion;
            public string SourceCommit;
            public string ConfigCommit;
            public string PackageName;
            public string Architecture;
            public string UnityVersion;
            public string ScriptingBackend;
            public string ApiCompatibilityLevel;
            public string ServerUrl;
            public string StartupScene;
            public string[] AotAssemblies;
            public string[] HotfixAssemblies;
            public HotfixDeploymentVerification.DownloadRecord[] Files;
        }

        [Serializable]
        private sealed class Result
        {
            public bool Success;
            public string Stage;
            public string Failure;
            public string ApiCompatibilityLevel;
            public string ResourceVersion;
            public string PlayerVersion;
            public string SourceCommit;
            public string ConfigCommit;
            public string Platform;
            public string BuildKind;
            public bool Published;
            public bool ActivationAttempted;
            public bool Activated;
            public bool RolledBack;
            public bool RollbackFailed;
            public bool PreviousVersionAbsent;
            public HotfixDeploymentVerification.DownloadRecord[] Downloads;
        }

        [Serializable]
        private sealed class AndroidToolsSnapshot
        {
            public string[] PathKeys;
            public string[] EmbeddedKeys;
            public bool[] HadPaths;
            public bool[] HadEmbedded;
            public string[] PreviousPaths;
            public bool[] PreviousEmbedded;
        }

        /// 生成首包程序集，或按在线首包基线检查热更兼容性。
        public static void Prepare() => Execute("Prepare", PrepareBuild);

        /// 构建资源；Full 同时构建 IL2CPP Player 并归档候选基线。
        public static void Build() => Execute("Build", BuildArtifacts);

        /// 发布资源并逐文件验证 HTTP SHA256，成功后提升在线 Full 基线。
        public static void Deploy() => Execute("Deploy", request => DeployArtifactsAsync(request).GetAwaiter().GetResult());

        private static void Execute(string stage, Action<Request> action)
        {
            Request request = null;
            Result result = null;
            Action restoreAndroidTools = null;
            bool failed = false;
            try
            {
                string path = Environment.GetEnvironmentVariable("SLEEPY_BUILD_REQUEST");
                Require(!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path), "SLEEPY_BUILD_REQUEST 必须是请求 JSON 的绝对路径。");
                request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
                Require(request != null && Path.IsPathFullyQualified(request.OutputDirectory ?? string.Empty), "请求必须提供绝对输出目录。");
                Require(!IsWithin(request.OutputDirectory, Application.dataPath), "输出不能位于 Assets 内。");
                GitHubPagesDeployment.EnsureNoLinks(request.OutputDirectory);
                string resultPath = Path.Combine(request.OutputDirectory, "result.json");
                result = File.Exists(resultPath) ? JsonUtility.FromJson<Result>(File.ReadAllText(resultPath)) ?? new Result() : new Result();
                result.Stage = stage;
                result.Success = false;
                result.Failure = string.Empty;
                result.Platform = request.Platform;
                result.BuildKind = request.BuildKind;
                result.SourceCommit = request.SourceCommit;
                result.ConfigCommit = request.ConfigCommit;
                result.ResourceVersion = request.ResourceVersion;
                ValidateRequest(request);
                if (request.Platform == "Android" && request.BuildKind == "Full" && stage != "Deploy")
                {
                    UseBundledAndroidTools(request.OutputDirectory, out restoreAndroidTools);
                }
                action(request);
                if (stage == "Deploy")
                {
                    CopyPublicationResult(result, JsonUtility.FromJson<Result>(File.ReadAllText(resultPath)));
                }
                result.PlayerVersion = PlayerSettings.bundleVersion;
                var apiProfile = PlayerSettings.GetApiCompatibilityLevel(UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(
                    UnityEditor.BuildPipeline.GetBuildTargetGroup(Target(request))));
                result.ApiCompatibilityLevel = apiProfile == UnityEditor.ApiCompatibilityLevel.NET_Unity_4_8
                    ? nameof(UnityEditor.ApiCompatibilityLevel.NET_Unity_4_8) : apiProfile.ToString();
                result.Success = true;
                WriteJson(resultPath, result);
                Debug.Log("Sleepy build " + stage + " completed.");
            }
            catch (Exception exception)
            {
                failed = true;
                // 不转发第三方认证异常；自有校验消息也移除已注入的秘密值。
                string failure = stage + ": " + (exception is InvalidOperationException ? exception.Message : exception.GetType().Name);
                string stackTrace = exception.StackTrace ?? string.Empty;
                foreach (string name in new[] { "GH_TOKEN", "SLEEPY_KEYSTORE_PASSWORD", "SLEEPY_KEY_ALIAS_PASSWORD", "SLEEPY_SSH_KEY_PASSPHRASE" })
                {
                    string secret = Environment.GetEnvironmentVariable(name);
                    if (!string.IsNullOrEmpty(secret))
                    {
                        failure = failure.Replace(secret, "[redacted]");
                        stackTrace = stackTrace.Replace(secret, "[redacted]");
                    }
                }
                Debug.LogError(failure + Environment.NewLine + stackTrace);
                if (request != null && result != null)
                {
                    if (stage == "Deploy" && File.Exists(Path.Combine(request.OutputDirectory, "result.json")))
                    {
                        var persisted = JsonUtility.FromJson<Result>(File.ReadAllText(Path.Combine(request.OutputDirectory, "result.json")));
                        if (persisted != null)
                        {
                            CopyPublicationResult(result, persisted);
                        }
                    }
                    result.Failure = failure;
                    WriteJson(Path.Combine(request.OutputDirectory, "result.json"), result);
                }
                throw new InvalidOperationException(failure);
            }
            finally
            {
                try
                {
                    try
                    {
                        if (stage == "Build" && request?.Platform == "Android")
                        {
                            PlayerSettings.Android.keystorePass = string.Empty;
                            PlayerSettings.Android.keyaliasPass = string.Empty;
                        }
                    }
                    finally
                    {
                        restoreAndroidTools?.Invoke();
                    }
                }
                catch
                {
                    failed = true;
                    const string failure = "Android 工具偏好或签名密码清理失败，请检查当前用户设置。";
                    Debug.LogError(failure);
                    if (request != null && result != null)
                    {
                        result.Success = false;
                        result.Failure = string.IsNullOrEmpty(result.Failure) ? failure : result.Failure + "; " + failure;
                        WriteJson(Path.Combine(request.OutputDirectory, "result.json"), result);
                    }
                    throw new InvalidOperationException(failure);
                }
                finally
                {
                    if (failed && Application.isBatchMode)
                    {
                        EditorApplication.Exit(1);
                    }
                }
            }
        }

        private static void UseBundledAndroidTools(string outputDirectory, out Action restore)
        {
            restore = null;
#if UNITY_ANDROID
            string player = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer");
            string[] paths =
            {
                Path.Combine(player, "SDK"), Path.Combine(player, "NDK"),
                Path.Combine(player, "OpenJDK"), Path.Combine(player, "Tools", "gradle")
            };
            Require(File.Exists(Path.Combine(player, "UnityEditor.Android.Extensions.dll")) && paths.All(Directory.Exists),
                "当前 Unity 缺少配套 Android Build Support、SDK、NDK、OpenJDK 或 Gradle。");
            Require(File.Exists(Path.Combine(paths[0], "platform-tools", "adb.exe")) &&
                Directory.Exists(Path.Combine(paths[0], "cmdline-tools")) &&
                Directory.GetFiles(Path.Combine(paths[0], "cmdline-tools"), "sdkmanager.bat", SearchOption.AllDirectories).Length > 0 &&
                Directory.Exists(Path.Combine(paths[0], "build-tools")) &&
                Directory.Exists(Path.Combine(paths[0], "platforms")) &&
                Directory.GetDirectories(Path.Combine(paths[0], "platforms")).Length > 0 &&
                File.Exists(Path.Combine(paths[1], "toolchains", "llvm", "prebuilt", "windows-x86_64", "bin", "clang.exe")) &&
                File.Exists(Path.Combine(paths[2], "bin", "java.exe")) && File.Exists(Path.Combine(paths[2], "bin", "javac.exe")) &&
                Directory.Exists(Path.Combine(paths[3], "lib")) &&
                Directory.GetFiles(Path.Combine(paths[3], "lib"), "gradle-launcher-*.jar").Length > 0,
                "Unity 配套 Android 工具不完整，请通过 Hub 修复当前 Editor 的模块。");

            // 来自项目锁定的 Unity 6000.3 Android 扩展；升级 Editor 时重新核对这四组键。
            string[] pathKeys = { "AndroidSdkRoot", "AndroidNdkRootR27C", "Jdk17Path", "GradlePath" };
            string[] embeddedKeys = { "SdkUseEmbedded", "NdkUseEmbedded", "JdkUseEmbedded", "GradleUseEmbedded" };
            bool[] hadPaths = pathKeys.Select(EditorPrefs.HasKey).ToArray();
            bool[] hadEmbedded = embeddedKeys.Select(EditorPrefs.HasKey).ToArray();
            string[] previousPaths = pathKeys.Select(key => EditorPrefs.GetString(key)).ToArray();
            bool[] previousEmbedded = embeddedKeys.Select(key => EditorPrefs.GetBool(key)).ToArray();
            WriteJson(Path.Combine(outputDirectory, "android-tools-before.json"), new AndroidToolsSnapshot
            {
                PathKeys = pathKeys,
                EmbeddedKeys = embeddedKeys,
                HadPaths = hadPaths,
                HadEmbedded = hadEmbedded,
                PreviousPaths = previousPaths,
                PreviousEmbedded = previousEmbedded
            });
            restore = () =>
            {
                var failures = new List<string>();
                for (int i = 0; i < pathKeys.Length; i++)
                {
                    try
                    {
                        if (hadPaths[i])
                        {
                            EditorPrefs.SetString(pathKeys[i], previousPaths[i]);
                        }
                        else
                        {
                            EditorPrefs.DeleteKey(pathKeys[i]);
                        }
                    }
                    catch
                    {
                        failures.Add(pathKeys[i]);
                    }
                    try
                    {
                        if (hadEmbedded[i])
                        {
                            EditorPrefs.SetBool(embeddedKeys[i], previousEmbedded[i]);
                        }
                        else
                        {
                            EditorPrefs.DeleteKey(embeddedKeys[i]);
                        }
                    }
                    catch
                    {
                        failures.Add(embeddedKeys[i]);
                    }
                }
                Require(failures.Count == 0, "恢复 Android 工具偏好失败：" + string.Join(", ", failures));
            };
            UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath = paths[2];
            UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = paths[0];
            UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath = paths[1];
            UnityEditor.Android.AndroidExternalToolsSettings.gradlePath = paths[3];
            string[] effectivePaths =
            {
                UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath,
                UnityEditor.Android.AndroidExternalToolsSettings.ndkRootPath,
                UnityEditor.Android.AndroidExternalToolsSettings.jdkRootPath,
                UnityEditor.Android.AndroidExternalToolsSettings.gradlePath
            };
            for (int i = 0; i < paths.Length; i++)
            {
                Require(string.Equals(Path.GetFullPath(paths[i]).TrimEnd('\\', '/'),
                    Path.GetFullPath(effectivePaths[i]).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase),
                    "Android 工具未实际切换到 Unity 配套路径。");
            }
            Debug.Log("Android SDK/NDK/OpenJDK/Gradle 已固定为当前 Unity 配套工具；阶段退出前恢复用户偏好。");
#else
            throw new InvalidOperationException("Android 构建模块未加载，不能设置配套工具。");
#endif
        }

        private static void ValidateRequest(Request request)
        {
            Require(request != null, "请求为空。");
            Require(request.Platform == "PC" || request.Platform == "Android", "仅支持 PC / Android。");
            Require(request.BuildKind == "Full" || request.BuildKind == "Hotfix", "BuildKind 必须是 Full / Hotfix。");
            Require(new[] { "Offline", "OnlineFull", "OnlineDownload" }.Contains(request.ResourceMode), "资源模式无效。");
            Require(request.BuildKind != "Hotfix" || request.ResourceMode != "Offline", "离线首包不能发布热更。");
            Require(Regex.IsMatch(request.ResourceVersion ?? string.Empty, @"\A[A-Za-z0-9_-][A-Za-z0-9_.-]*\z"), "资源版本无效。");
            Require(request.Provider == "Pages" || request.Provider == "Ssh", "Provider 无效。");
            Require(Path.IsPathFullyQualified(request.OutputDirectory ?? string.Empty), "输出必须是绝对路径。");
            Require(Path.IsPathFullyQualified(request.BaselineDirectory ?? string.Empty), "基线必须是绝对路径。");
            GitHubPagesDeployment.EnsureNoLinks(request.OutputDirectory);
            GitHubPagesDeployment.EnsureNoLinks(request.BaselineDirectory);
            Require(!IsWithin(request.OutputDirectory, Application.dataPath) && !IsWithin(request.BaselineDirectory, Application.dataPath), "输出及基线不能位于 Assets 内。");
            Require(!IsWithin(request.OutputDirectory, request.BaselineDirectory) && !IsWithin(request.BaselineDirectory, request.OutputDirectory), "输出与持久基线不能互相包含。");
            Require(EditorUserBuildSettings.activeBuildTarget == Target(request), "Unity 当前构建平台与请求不符。");
            if (request.Platform == "Android" && request.BuildKind == "Full")
            {
                Require(request.AndroidFormat == "Apk" || request.AndroidFormat == "Aab", "AndroidFormat 无效。");
                Require(request.AndroidSigning == "Debug" || request.AndroidSigning == "Custom", "AndroidSigning 无效。");
                Require(request.AndroidVersionCode > 0, "AndroidVersionCode 必须大于零。");
            }
            Directory.CreateDirectory(request.OutputDirectory);
        }

        private static BuildTarget Target(Request request) => request.Platform == "PC" ? BuildTarget.StandaloneWindows64 : BuildTarget.Android;

        private static HotfixConfig Configure(Request request, bool player)
        {
            var config = AssetDatabase.LoadAssetAtPath<HotfixConfig>(ConfigPath);
            Require(config != null, "缺少正式 HotfixConfig。");
            string platform = Target(request).ToString();
            config.AotSourcePath = "HybridCLRData/AssembliesPostIl2CppStrip/" + platform;
            config.AotStrippedSourcePath = "HybridCLRData/StrippedAOTAssembly2/" + platform;
            config.HotfixSourcePath = "HybridCLRData/HotUpdateDlls/" + platform;
            config.UseLocalMockServer = false;
            config.PlayMode = request.ResourceMode == "Offline" ? ResourcePlayMode.OfflinePlayMode : ResourcePlayMode.HostPlayMode;
            config.BaseServerURL = config.RemoteBaseServerURL = request.ResourceMode == "Offline" ? string.Empty : ServerUrl(request);
            var group = UnityEditor.BuildPipeline.GetBuildTargetGroup(Target(request));
            var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
            PlayerSettings.SetScriptingBackend(namedTarget, ScriptingImplementation.IL2CPP);
            // HybridCLR 热更 DLL 必须使用 Framework，避免 Player 裁掉 netstandard Facade 后无法解析引用。
            PlayerSettings.SetApiCompatibilityLevel(namedTarget, UnityEditor.ApiCompatibilityLevel.NET_Unity_4_8);
            Require(PlayerSettings.GetApiCompatibilityLevel(namedTarget) == UnityEditor.ApiCompatibilityLevel.NET_Unity_4_8,
                "无法固定 HybridCLR 所需的 .NET Framework API 配置。");
            PlayerSettings.SetEditorAssembliesCompatibilityLevel(EditorAssembliesCompatibilityLevel.NET_Unity_4_8);
            Require(PlayerSettings.GetEditorAssembliesCompatibilityLevel() == EditorAssembliesCompatibilityLevel.NET_Unity_4_8,
                "无法固定 CI Editor 程序集的 .NET Framework API 配置。");
            EditorUserBuildSettings.development = request.Development;
            if (!string.IsNullOrWhiteSpace(request.PlayerVersion))
            {
                PlayerSettings.bundleVersion = request.PlayerVersion;
            }
            if (request.Platform == "Android")
            {
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                if (request.BuildKind == "Full")
                {
                    PlayerSettings.Android.bundleVersionCode = request.AndroidVersionCode;
                    EditorUserBuildSettings.buildAppBundle = request.AndroidFormat == "Aab";
                    EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
                    PlayerSettings.Android.useCustomKeystore = request.AndroidSigning == "Custom";
                    if (player && request.AndroidSigning == "Custom")
                    {
                        PlayerSettings.Android.keystoreName = RequiredEnvironment("SLEEPY_KEYSTORE_FILE");
                        Require(File.Exists(PlayerSettings.Android.keystoreName), "Android keystore 文件不存在。");
                        PlayerSettings.Android.keystorePass = RequiredEnvironment("SLEEPY_KEYSTORE_PASSWORD");
                        PlayerSettings.Android.keyaliasName = RequiredEnvironment("SLEEPY_KEY_ALIAS");
                        PlayerSettings.Android.keyaliasPass = RequiredEnvironment("SLEEPY_KEY_ALIAS_PASSWORD");
                    }
                }
            }
            // Player 构建会写回真实宏定义及平台批处理；Prepare 同样具体化，避免 Full/Hotfix 指纹不同。
            string symbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget);
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, symbols);
            Require(PlayerSettings.GetScriptingDefineSymbols(namedTarget) == symbols, "无法固定当前平台的真实宏定义。");
            var coreType = Type.GetType("UnityEditor.Rendering.CoreBuildData, Unity.RenderPipelines.Core.Editor", true);
            var coreInstance = coreType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
            var pipelineAssets = coreType.GetProperty("renderPipelineAssets", BindingFlags.Public | BindingFlags.Instance);
            Require(coreInstance != null && pipelineAssets?.PropertyType == typeof(List<RenderPipelineAsset>),
                "锁定 URP 包的平台资源集合接口不可用。");
            ((IDisposable)coreInstance.GetValue(null)).Dispose();
            using var buildData = (IDisposable)coreInstance.GetValue(null);
            var assets = pipelineAssets.GetValue(buildData) as List<RenderPipelineAsset>;
            Require(assets != null && assets.Count > 0 && assets.All(asset => asset is UniversalRenderPipelineAsset),
                "当前平台没有完整有效的 URP 构建资源集合。");
            bool dynamicBatching = assets.Cast<UniversalRenderPipelineAsset>().Any(asset => asset.supportsDynamicBatching);
            PlayerSettings.SetDynamicBatchingForPlatform(Target(request), dynamicBatching);
            Require(PlayerSettings.GetDynamicBatchingForPlatform(Target(request)) == dynamicBatching,
                "无法固定当前平台 URP 的动态批处理配置。");
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }

        private static string ServerUrl(Request request)
        {
            string url = request.Provider == "Pages"
                ? (request.PagesSiteUrl ?? string.Empty).TrimEnd('/') + "/" + GitHubPagesDeployment.GetResourcePath(request.PagesProjectName, request.Platform)
                : request.SshServerUrl;
            Require(Uri.TryCreate(url, UriKind.Absolute, out Uri uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || (request.Provider == "Ssh" && uri.Scheme == Uri.UriSchemeHttp)) &&
                string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment), "远端 URL 必须是有效的无凭据 HTTP(S) 平台根。");
            return url.TrimEnd('/');
        }

        private static void PrepareBuild(Request request)
        {
            HotfixConfig config = Configure(request, false);
            if (request.BuildKind == "Full")
            {
                EnsureHybridClrInstalled();
                PrebuildCommand.GenerateAll();
                // GenerateAll 内部构建 Player 会卸载资产，不能继续持有构建前的 native 对象。
                config = Configure(request, false);
                config.AotAssemblies = HotfixAssemblyBuild.ReadAotAssemblies();
                config.HotfixAssemblies = HotfixAssemblyBuild.ReadHotfixAssemblies();
                Require(config.HotfixAssemblies.Length > 0, "没有热更程序集。");
                HotfixAssemblyBuild.StripAotAssemblies(config);
                HotfixAssemblyBuild.Sync(config, true);
            }
            else
            {
                Baseline baseline = LoadBaseline(request, config);
                CompileDllCommand.CompileDll(Target(request), request.Development);
                config.AotAssemblies = baseline.AotAssemblies;
                config.HotfixAssemblies = baseline.HotfixAssemblies;
                CheckMetadata(config, Path.Combine(request.BaselineDirectory, "aot"));
                HotfixAssemblyBuild.PruneAssemblies(config.AotTargetPath, config.AotAssemblies);
                CopyDirectory(Path.Combine(request.BaselineDirectory, "metadata"), config.AotTargetPath);
                HotfixAssemblyBuild.CopyAssemblies(config.HotfixSourcePath, config.HotfixTargetPath, config.HotfixAssemblies);
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            WriteJson(Path.Combine(request.OutputDirectory, "prepared.json"), Snapshot(request, config));
        }

        private static void EnsureHybridClrInstalled()
        {
            var installer = new HybridCLR.Editor.Installer.InstallerController();
            Require(Regex.IsMatch(installer.HybridclrLocalVersion ?? string.Empty, @"\Av[0-9][A-Za-z0-9_.-]*\z") &&
                Regex.IsMatch(installer.Il2cppPlusLocalVersion ?? string.Empty, @"\Av[0-9][A-Za-z0-9_.-]*\z"),
                "锁定的 HybridCLR 包没有当前 Unity 对应的发布版本，拒绝安装浮动分支。");
            GitHubPagesDeployment.EnsureNoLinks(HybridCLR.Editor.SettingsUtil.HybridCLRDataDir);
            if (!installer.HasInstalledHybridCLR() || installer.InstalledLibil2cppVersion != installer.PackageVersion)
            {
                // 使用 packages-lock 对应 UPM 包的版本清单；只初始化专用 CI 工作区，不读取日常项目缓存。
                installer.InstallDefaultHybridCLR();
            }
            Require(installer.HasInstalledHybridCLR() && installer.InstalledLibil2cppVersion == installer.PackageVersion,
                "HybridCLR 初始化失败，不能继续生成首包。");
        }

        private static void BuildArtifacts(Request request)
        {
            Require(!File.Exists(Path.Combine(request.OutputDirectory, "built.json")), "本次资源产物已冻结；允许重试 Deploy，重新构建请使用新的输出目录和资源版本。");
            HotfixConfig config = Configure(request, request.BuildKind == "Full");
            var prepared = JsonUtility.FromJson<Baseline>(File.ReadAllText(Path.Combine(request.OutputDirectory, "prepared.json")));
            Require(prepared != null, "Prepare 记录为空。");
            VerifyFingerprint(prepared, "Prepare 后构建指纹已改变，请重新 Prepare。");
            Require(prepared.ResourceVersion == request.ResourceVersion &&
                prepared.Platform == request.Platform && prepared.Development == request.Development && prepared.ResourceMode == request.ResourceMode &&
                prepared.SourceCommit == request.SourceCommit,
                "Prepare 后构建输入已改变，请重新 Prepare。");
            if (request.BuildKind == "Hotfix")
            {
                LoadBaseline(request, config);
                CheckMetadata(config, Path.Combine(request.BaselineDirectory, "aot"));
                BuildResources(request, config);
                RecordBuiltResources(request, config);
                return;
            }

            string[] aotAssemblies = config.AotAssemblies;
            string[] hotfixAssemblies = config.HotfixAssemblies;
            string before = AotHash(config);
            BuildResources(request, config);
            BuildPlayer(request);
            config = Configure(request, false);
            config.AotAssemblies = aotAssemblies;
            config.HotfixAssemblies = hotfixAssemblies;
            if (before != AotHash(config))
            {
                // Player 的实际裁剪结果才是基线；资源 link.xml 变化时重配元数据并再构建一次。
                HotfixAssemblyBuild.StripAotAssemblies(config);
                HotfixAssemblyBuild.Sync(config, true);
                before = AotHash(config);
                BuildResources(request, config, true);
                BuildPlayer(request);
                config = Configure(request, false);
                config.AotAssemblies = aotAssemblies;
                config.HotfixAssemblies = hotfixAssemblies;
                Require(before == AotHash(config), "Player AOT 裁剪结果仍在变化，拒绝归档不配套的基线。");
            }
            CheckMetadata(config, config.AotSourcePath);
            ArchiveBaseline(request, config);
            RecordBuiltResources(request, config);
        }

        private static void BuildResources(Request request, HotfixConfig config, bool finalPass = false)
        {
            string streaming = BundleBuilderHelper.GetStreamingAssetsRoot();
            GitHubPagesDeployment.EnsureNoLinks(streaming);
            var parameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = Path.Combine(request.OutputDirectory, finalPass ? "bundles-final" : "bundles"),
                BundledFileRoot = streaming,
                BuildPipeline = nameof(YooAsset.Editor.ScriptableBuildPipeline),
                BuildBundleType = (int)YooAsset.EBundleType.AssetBundle,
                BuildTarget = Target(request),
                PackageName = config.PackageName,
                PackageVersion = request.ResourceVersion,
                VerifyBuildingResult = true,
                FileNameStyle = EFileNameStyle.HashName,
                CompressOption = ECompressOption.LZ4,
                BuiltinShadersBundleName = DefaultBundlePackRule.CreateShadersPackRuleResult().GetBundleName(config.PackageName, BundleCollectorSettingData.Setting.UniqueBundleName),
                BundledCopyOption = request.BuildKind == "Hotfix" || request.ResourceMode == "OnlineDownload"
                    ? EBundledCopyOption.None : EBundledCopyOption.ClearAndCopyAll
            };
            var result = new YooAsset.Editor.ScriptableBuildPipeline().Run(parameters, true);
            Require(result.Success, "YooAsset 构建失败：" + result.FailedTask);
            string source = result.OutputPackageDirectory;
            string[] sourceFiles = GitHubPagesDeployment.GetSourceFiles(source, config.PackageName);
            string reportPath = Path.Combine(source, YooAssetConfiguration.GetBuildReportFileName(config.PackageName, request.ResourceVersion));
            GitHubPagesDeployment.EnsureNoLinks(reportPath);
            var report = YooAsset.Editor.BuildReport.Deserialize(File.ReadAllText(reportPath));
            Require(report?.Summary != null && report.Summary.BuildPackageName == config.PackageName &&
                report.Summary.BuildPackageVersion == request.ResourceVersion && report.BundleInfos?.Count > 0,
                "YooAsset 构建报告与本次资源不匹配。");
            var sourceNames = new HashSet<string>(sourceFiles.Select(Path.GetFileName), StringComparer.Ordinal);
            Require(report.BundleInfos.All(info => info != null && sourceNames.Contains(info.FileName)),
                "YooAsset 清单引用了未纳入发布输出的 Bundle，请检查标准打包命名。");
            string output = Path.Combine(request.OutputDirectory, "resources");
            GitHubPagesDeployment.EnsureNoLinks(output);
            Directory.CreateDirectory(output);
            Require(Directory.GetDirectories(output).Length == 0, "本次资源输出包含未知子目录。");
            string[] oldFiles = Directory.GetFiles(output);
            foreach (string file in oldFiles)
            {
                GitHubPagesDeployment.EnsureNoLinks(file);
                Require(new[] { ".bundle", ".version", ".bytes", ".hash" }.Contains(Path.GetExtension(file)), "本次资源输出包含未知文件。");
            }
            foreach (string file in oldFiles)
            {
                File.Delete(file);
            }
            foreach (string file in sourceFiles)
            {
                File.Copy(file, Path.Combine(output, Path.GetFileName(file)), true);
            }
            if (request.BuildKind == "Full" && request.ResourceMode == "OnlineDownload")
            {
                string packageDirectory = Path.Combine(streaming, config.PackageName);
                GitHubPagesDeployment.EnsureNoLinks(packageDirectory);
                if (Directory.Exists(packageDirectory))
                {
                    Require(Directory.GetDirectories(packageDirectory).Length == 0, "StreamingAssets 包目录包含未知子目录。");
                    foreach (string file in Directory.GetFiles(packageDirectory))
                    {
                        GitHubPagesDeployment.EnsureNoLinks(file);
                    }
                    foreach (string file in Directory.GetFiles(packageDirectory))
                    {
                        GitHubPagesDeployment.EnsureNoLinks(file);
                        File.Delete(file);
                    }
                }
                Directory.CreateDirectory(packageDirectory);
                string version = config.PackageName + ".version";
                foreach (string file in GitHubPagesDeployment.GetSourceFiles(source, config.PackageName).Where(file => !file.EndsWith(".bundle", StringComparison.Ordinal)))
                {
                    File.Copy(file, Path.Combine(packageDirectory, Path.GetFileName(file)), true);
                }
                Require(File.Exists(Path.Combine(packageDirectory, version)), "首包缺少版本信息。");
                // Host 默认内置文件系统必须有目录表；空表表示所有 Bundle 从远端下载。
                var catalogHelper = typeof(ResourcePackage).Assembly.GetType("YooAsset.BuiltinCatalogHelper", true);
                var createEmpty = catalogHelper.GetMethod("CreateEmptyFile", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(string), typeof(string), typeof(string) }, null);
                Require(createEmpty != null && createEmpty.ReturnType == typeof(bool), "锁定 YooAsset 包的空目录表接口不可用。");
                Require((bool)createEmpty.Invoke(null, new object[] { config.PackageName, request.ResourceVersion, packageDirectory }),
                    "生成在线下载包的空内置目录表失败。");
                Require(File.Exists(Path.Combine(packageDirectory, "BuiltinCatalog.bytes")) &&
                    Directory.GetFiles(packageDirectory, "*.bundle").Length == 0, "在线下载首包缺目录表或意外内置 Bundle。");
                AssetDatabase.Refresh();
            }
        }

        private static void BuildPlayer(Request request)
        {
            if (request.Platform == "Android" && request.AndroidSigning == "Custom")
            {
                PlayerSettings.Android.keystorePass = RequiredEnvironment("SLEEPY_KEYSTORE_PASSWORD");
                PlayerSettings.Android.keyaliasPass = RequiredEnvironment("SLEEPY_KEY_ALIAS_PASSWORD");
            }
            string directory = Path.Combine(request.OutputDirectory, "player");
            string playerPath = request.Platform == "PC" ? Path.Combine(directory, "PC", "SleepyDemos.exe")
                : Path.Combine(directory, "SleepyDemos." + (request.AndroidFormat == "Aab" ? "aab" : "apk"));
            Directory.CreateDirectory(Path.GetDirectoryName(playerPath));
            UnityEditor.Build.Reporting.BuildReport report;
            try
            {
                report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { "Assets/Scenes/AppEntrance.unity" },
                    target = Target(request),
                    locationPathName = playerPath,
                    options = request.Development ? BuildOptions.Development : BuildOptions.None
                });
            }
            finally
            {
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
            }
            Require(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded, "IL2CPP Player 构建失败。");
            Require(File.Exists(playerPath) && new FileInfo(playerPath).Length > 0,
                "Player 未生成实际安装包或可执行文件，拒绝归档和发布。");
        }

        private static Baseline Snapshot(Request request, HotfixConfig config)
        {
            string fingerprint = Fingerprint(out string[] inputs);
            return new Baseline
            {
                Fingerprint = fingerprint, FingerprintInputs = inputs, Platform = request.Platform, Development = request.Development,
                ResourceMode = request.ResourceMode, PlayerVersion = PlayerSettings.bundleVersion,
                ResourceVersion = request.ResourceVersion, SourceCommit = request.SourceCommit, ConfigCommit = request.ConfigCommit,
                PackageName = config.PackageName, ServerUrl = config.RemoteBaseServerURL, StartupScene = config.StartupScene,
                Architecture = request.Platform == "PC" ? "x64" : "ARM64", UnityVersion = Application.unityVersion, ScriptingBackend = "IL2CPP",
                ApiCompatibilityLevel = nameof(UnityEditor.ApiCompatibilityLevel.NET_Unity_4_8),
                AotAssemblies = config.AotAssemblies, HotfixAssemblies = config.HotfixAssemblies
            };
        }

        private static Baseline LoadBaseline(Request request, HotfixConfig config)
        {
            string path = Path.Combine(request.BaselineDirectory, "baseline.json");
            GitHubPagesDeployment.EnsureNoLinks(path);
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(path));
            Require(baseline != null, "当前在线首包基线为空，必须构建并发布新的 Full。");
            VerifyFingerprint(baseline, "当前在线首包构建指纹不兼容，必须构建并发布新的 Full。");
            Require(baseline.ResourceMode != "Offline" && baseline.Platform == request.Platform &&
                baseline.Development == request.Development && baseline.ResourceMode == request.ResourceMode &&
                baseline.ApiCompatibilityLevel == nameof(UnityEditor.ApiCompatibilityLevel.NET_Unity_4_8) &&
                baseline.PackageName == config.PackageName &&
                baseline.PlayerVersion == PlayerSettings.bundleVersion &&
                baseline.ServerUrl == config.RemoteBaseServerURL && baseline.StartupScene == config.StartupScene &&
                baseline.HotfixAssemblies.SequenceEqual(HotfixAssemblyBuild.ReadHotfixAssemblies()),
                "当前在线首包基线不兼容，必须构建并发布新的 Full。");
            VerifyArchive(request.BaselineDirectory, baseline.Files);
            return baseline;
        }

        private static void VerifyArchive(string directory, HotfixDeploymentVerification.DownloadRecord[] files)
        {
            Require(files != null && files.Length > 0, "归档没有完整性记录。");
            foreach (var file in files)
            {
                string path = Path.GetFullPath(Path.Combine(directory, file.File));
                Require(IsWithin(path, directory), "归档文件越界。");
                GitHubPagesDeployment.EnsureNoLinks(path);
                Require(HotfixDeploymentVerification.HashFile(path) == file.Sha256, "归档内容已改变。");
            }
        }

        private static void RecordBuiltResources(Request request, HotfixConfig config)
        {
            string resources = Path.Combine(request.OutputDirectory, "resources");
            Baseline built = Snapshot(request, config);
            built.Files = GitHubPagesDeployment.GetSourceFiles(resources, config.PackageName).Select(file =>
                new HotfixDeploymentVerification.DownloadRecord
                {
                    File = Path.GetFileName(file), Sha256 = HotfixDeploymentVerification.HashFile(file), Bytes = new FileInfo(file).Length
                }).ToArray();
            WriteJson(Path.Combine(request.OutputDirectory, "built.json"), built);
        }

        private static void CheckMetadata(HotfixConfig config, string aotDirectory)
        {
            var checker = new MissingMetadataChecker(aotDirectory, config.HotfixAssemblies.Select(Path.GetFileNameWithoutExtension));
            foreach (string dll in config.HotfixAssemblies)
            {
                Require(checker.Check(Path.Combine(config.HotfixSourcePath, dll)), "热更引用了基线缺失的 AOT 类型或成员，必须构建 Full。");
            }
            var names = config.HotfixAssemblies.Select(Path.GetFileNameWithoutExtension).ToList();
            var collector = new HybridCLR.Editor.Meta.AssemblyReferenceDeepCollector(
                new HybridCLR.Editor.Meta.PathAssemblyResolver(config.HotfixSourcePath, aotDirectory), names);
            var analyzer = new HybridCLR.Editor.AOT.Analyzer(new HybridCLR.Editor.AOT.Analyzer.Options
            {
                Collector = collector,
                MaxIterationCount = Math.Min(20, HybridCLR.Editor.SettingsUtil.HybridCLRSettings.maxGenericReferenceIteration)
            });
            analyzer.Run();
            string[] required = analyzer.AotGenericTypes.Select(type => type.Type.Module.Name.ToString())
                .Concat(analyzer.AotGenericMethods.Select(method => method.Method.Module.Name.ToString()))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            Require(required.All(name => config.AotAssemblies.Contains(name, StringComparer.OrdinalIgnoreCase)),
                "热更新增了首包未补充的 AOT 泛型元数据程序集，必须构建 Full。");
        }

        private static string AotHash(HotfixConfig config) => string.Join("\n", config.AotAssemblies.Select(name => name + ":" +
            HotfixDeploymentVerification.HashFile(Path.Combine(config.AotSourcePath, name))));

        private static void ArchiveBaseline(Request request, HotfixConfig config)
        {
            string output = Path.Combine(request.OutputDirectory, "baseline");
            CopyDirectory(config.AotSourcePath, Path.Combine(output, "aot"));
            CopyDirectory(config.AotTargetPath, Path.Combine(output, "metadata"));
            CopyDirectory(config.HotfixSourcePath, Path.Combine(output, "hotfix"));
            Baseline baseline = Snapshot(request, config);
            baseline.Files = Directory.GetFiles(output, "*", SearchOption.AllDirectories)
                .Where(file => Path.GetFileName(file) != "baseline.json").Select(file =>
                new HotfixDeploymentVerification.DownloadRecord
                {
                    File = Path.GetRelativePath(output, file).Replace('\\', '/'),
                    Sha256 = HotfixDeploymentVerification.HashFile(file), Bytes = new FileInfo(file).Length
                }).ToArray();
            WriteJson(Path.Combine(output, "baseline.json"), baseline);
        }

        private static async Task DeployArtifactsAsync(Request request)
        {
            using var budget = new CancellationTokenSource(TimeSpan.FromMinutes(22));
            CancellationToken token = budget.Token;
            Require(request.Publish, "Deploy 仅用于获准发布的资源。");
            string resultPath = Path.Combine(request.OutputDirectory, "result.json");
            Result result = JsonUtility.FromJson<Result>(File.ReadAllText(resultPath));
            Baseline built = JsonUtility.FromJson<Baseline>(File.ReadAllText(Path.Combine(request.OutputDirectory, "built.json")));
            Require(result != null && built != null && built.ResourceVersion == request.ResourceVersion &&
                built.Platform == request.Platform && result.BuildKind == request.BuildKind && built.SourceCommit == request.SourceCommit &&
                built.ConfigCommit == request.ConfigCommit && built.ResourceMode == request.ResourceMode && built.Development == request.Development,
                "资源未构建成功或发布请求已改变，拒绝发布。");
            var config = AssetDatabase.LoadAssetAtPath<HotfixConfig>(ConfigPath);
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string package = config.PackageName;
            string resources = Path.Combine(request.OutputDirectory, "resources");
            VerifyArchive(resources, built.Files);
            Require(GitHubPagesDeployment.GetSourceFiles(resources, config.PackageName).Length == built.Files.Length,
                "发布资源目录含冻结清单之外的下载文件。");
            if (request.BuildKind == "Hotfix")
            {
                LoadBaseline(request, config);
            }
            string serverUrl = ServerUrl(request);
            string versionName = package + ".version";
            result.Published = result.ActivationAttempted = result.Activated = result.RolledBack = result.RollbackFailed = false;
            result.Downloads = Array.Empty<HotfixDeploymentVerification.DownloadRecord>();
            WriteJson(resultPath, result);
            if (request.Provider == "Pages")
            {
                RequiredEnvironment("GH_TOKEN");
                string resourcePath = GitHubPagesDeployment.GetResourcePath(request.PagesProjectName, request.Platform);
                string versionPath = Path.Combine(request.PagesRepositoryPath, resourcePath, versionName);
                GitHubPagesDeployment.EnsureNoLinks(versionPath);
                Require(!File.Exists(versionPath) || new FileInfo(versionPath).Length <= 1024, "原版本入口过大。");
                byte[] previousVersion = File.Exists(versionPath) ? File.ReadAllBytes(versionPath) : null;
                RecordPreviousVersion(request, result, previousVersion);
                await GitHubPagesDeployment.UploadAsync(request.PagesRepositoryPath,
                    resourcePath, resources, package, false, token).ConfigureAwait(false);
                await HotfixDeploymentVerification.WaitForPagesAsync(request.PagesRepositoryPath, cancellationToken: token).ConfigureAwait(false);
                result.Downloads = await HotfixDeploymentVerification.VerifyAsync(resources, package, serverUrl,
                    file => Path.GetFileName(file) != versionName, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                result.ActivationAttempted = true;
                WriteJson(resultPath, result);
                try
                {
                    await GitHubPagesDeployment.UploadAsync(request.PagesRepositoryPath, resourcePath, resources, package, true, token).ConfigureAwait(false);
                    result.Activated = true;
                    WriteJson(resultPath, result);
                    await HotfixDeploymentVerification.WaitForPagesAsync(request.PagesRepositoryPath, cancellationToken: token).ConfigureAwait(false);
                    var version = await HotfixDeploymentVerification.VerifyAsync(resources, package, serverUrl,
                        file => Path.GetFileName(file) == versionName, token).ConfigureAwait(false);
                    result.Downloads = result.Downloads.Concat(version).ToArray();
                }
                catch (Exception exception)
                {
                    if (exception.Data.Contains("SleepyProcessStopUnconfirmed"))
                    {
                        result.RollbackFailed = true;
                        WriteJson(resultPath, result);
                        throw new InvalidOperationException(exception.Message);
                    }
                    using var recoveryBudget = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                    CancellationToken recoveryToken = recoveryBudget.Token;
                    try
                    {
                        string recoveryCommit = await GitHubPagesDeployment.RestoreVersionWithCancellationAsync(request.PagesRepositoryPath, resourcePath, package,
                            request.ResourceVersion, previousVersion, projectRoot, recoveryToken).ConfigureAwait(false);
                        await HotfixDeploymentVerification.WaitForPagesAsync(request.PagesRepositoryPath, recoveryCommit, recoveryToken).ConfigureAwait(false);
                        await HotfixDeploymentVerification.VerifyRestoredVersionAsync(serverUrl, package, previousVersion, recoveryToken).ConfigureAwait(false);
                        result.RolledBack = true;
                        WriteJson(resultPath, result);
                    }
                    catch
                    {
                        result.RollbackFailed = true;
                        WriteJson(resultPath, result);
                        throw new InvalidOperationException("Pages 激活或校验失败，旧版本入口恢复未确认；保留发布与恢复提交，需要人工处理。");
                    }
                    throw new InvalidOperationException("Pages 激活或校验失败，已恢复旧版本入口。");
                }
            }
            else
            {
                SshBundleDeployment.Upload(request.SshHost, request.SshPort, request.SshUser,
                    RequiredEnvironment("SLEEPY_SSH_KEY_FILE"), request.SshHostKeySha256, request.SshRoot,
                    request.Platform, resources, package,
                    previous =>
                    {
                        token.ThrowIfCancellationRequested();
                        RecordPreviousVersion(request, result, previous);
                        result.Downloads = HotfixDeploymentVerification.VerifyAsync(resources, package, serverUrl,
                            file => Path.GetFileName(file) != versionName, token).GetAwaiter().GetResult();
                        token.ThrowIfCancellationRequested();
                        result.ActivationAttempted = true;
                        WriteJson(resultPath, result);
                        token.ThrowIfCancellationRequested();
                    },
                    () =>
                    {
                        result.Activated = true;
                        WriteJson(resultPath, result);
                        var version = HotfixDeploymentVerification.VerifyAsync(resources, package, serverUrl,
                            file => Path.GetFileName(file) == versionName, token).GetAwaiter().GetResult();
                        result.Downloads = result.Downloads.Concat(version).ToArray();
                    },
                    (restored, previous) =>
                    {
                        result.RollbackFailed = !restored;
                        if (restored)
                        {
                            using var recoveryBudget = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                            HotfixDeploymentVerification.VerifyRestoredVersionAsync(serverUrl, package, previous,
                                recoveryBudget.Token).GetAwaiter().GetResult();
                            result.RolledBack = true;
                        }
                        WriteJson(resultPath, result);
                    });
            }
            result.Published = true;
            WriteJson(resultPath, result);
            if (request.BuildKind == "Full" && request.ResourceMode != "Offline")
            {
                PromoteBaseline(request);
            }
        }

        private static void CopyPublicationResult(Result target, Result source)
        {
            target.Published = source.Published;
            target.Downloads = source.Downloads;
            target.ActivationAttempted = source.ActivationAttempted;
            target.Activated = source.Activated;
            target.RolledBack = source.RolledBack;
            target.RollbackFailed = source.RollbackFailed;
            target.PreviousVersionAbsent = source.PreviousVersionAbsent;
        }

        private static void RecordPreviousVersion(Request request, Result result, byte[] previous)
        {
            result.PreviousVersionAbsent = previous == null;
            string path = Path.Combine(request.OutputDirectory, "previous.version");
            GitHubPagesDeployment.EnsureNoLinks(path);
            if (previous != null)
            {
                File.WriteAllBytes(path, previous);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        private static void PromoteBaseline(Request request)
        {
            string candidate = Path.Combine(request.OutputDirectory, "baseline");
            Require(File.Exists(Path.Combine(candidate, "baseline.json")), "候选基线不存在。");
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(Path.Combine(candidate, "baseline.json")));
            Require(baseline != null && baseline.ResourceMode != "Offline" && baseline.ResourceVersion == request.ResourceVersion &&
                baseline.Platform == request.Platform && baseline.SourceCommit == request.SourceCommit, "候选基线与发布请求不一致。");
            VerifyArchive(candidate, baseline.Files);
            string current = Path.GetFullPath(request.BaselineDirectory);
            string next = current + ".next-" + Guid.NewGuid().ToString("N");
            string previous = current + ".previous-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(next);
            foreach (string file in Directory.GetFiles(candidate, "*", SearchOption.AllDirectories))
            {
                GitHubPagesDeployment.EnsureNoLinks(file);
                string target = Path.Combine(next, Path.GetRelativePath(candidate, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target);
            }
            bool moved = Directory.Exists(current);
            if (moved)
            {
                Directory.Move(current, previous);
            }
            try
            {
                Directory.Move(next, current);
            }
            catch
            {
                if (moved)
                {
                    Directory.Move(previous, current);
                }
                throw;
            }
            // previous 保留已发布首包，避免基线失败时丢失恢复证据。
        }

        private static void VerifyFingerprint(Baseline expected, string message)
        {
            string actual = Fingerprint(out string[] inputs);
            if (expected.Fingerprint == actual)
            {
                return;
            }
            string changed = expected.FingerprintInputs == null ? "旧记录没有逐文件指纹" : string.Join(", ",
                expected.FingerprintInputs.Except(inputs).Concat(inputs.Except(expected.FingerprintInputs))
                    .Select(line => line.Substring(0, line.LastIndexOf(':'))).Distinct().OrderBy(path => path, StringComparer.Ordinal).Take(12));
            throw new InvalidOperationException(message + " 变化项：" + changed + "；指纹 " + expected.Fingerprint + " -> " + actual);
        }

        private static string Fingerprint(out string[] inputs)
        {
            var files = new List<string>();
            foreach (string root in new[] { "Assets/Scripts/Core/Runtime", "Assets/Plugins", "Assets/Native", "Assets/Settings", "Native", "scripts/streamline", "Packages", "ProjectSettings" })
            {
                if (Directory.Exists(root))
                {
                    files.AddRange(Directory.GetFiles(root, "*", SearchOption.AllDirectories));
                }
            }
            // 所有非 Hotfix 的运行时脚本与预编译插件都属于 AOT；包内原生插件不可漏掉。
            files.AddRange(Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Replace('\\', '/').StartsWith("Assets/Scripts/Hotfix/", StringComparison.Ordinal) &&
                    !file.Replace('\\', '/').Contains("/Editor/") && !file.Replace('\\', '/').Contains("/Tests/") &&
                    !file.Replace('\\', '/').StartsWith("Assets/HybridCLRGenerate/", StringComparison.Ordinal)));
            files.AddRange(Directory.GetFiles("Assets", "*.dll", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles("Assets", "*.so", SearchOption.AllDirectories));
            files.AddRange(Directory.GetFiles("Assets", "*.asmdef", SearchOption.AllDirectories)
                .Where(file => !file.Replace('\\', '/').StartsWith("Assets/Scripts/Hotfix/", StringComparison.Ordinal) &&
                    !file.Replace('\\', '/').Contains("/Editor/") && !file.Replace('\\', '/').Contains("/Tests/")));
            var lines = new List<string> { "Unity:" + Application.unityVersion, "Architecture:PC64/AndroidARM64" };
            foreach (string file in files.Distinct().OrderBy(file => file, StringComparer.Ordinal))
            {
                if (file.Replace('\\', '/').StartsWith("Assets/Settings/Hotfix/", StringComparison.Ordinal))
                {
                    continue;
                }
                GitHubPagesDeployment.EnsureNoLinks(file);
                string hash;
                if (file.Replace('\\', '/') == "ProjectSettings/ProjectSettings.asset")
                {
                    string content = string.Join("\n", File.ReadAllLines(file).Where(line => !Regex.IsMatch(line,
                        @"^\s*(bundleVersion|AndroidBundleVersionCode|AndroidKeystoreName|AndroidKeyaliasName|AndroidUseCustomKeystore):", RegexOptions.IgnoreCase)));
                    using var sha = SHA256.Create();
                    hash = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(content)));
                }
                else
                {
                    hash = FingerprintFile(file);
                }
                lines.Add(file.Replace('\\', '/') + ":" + hash);
            }
            inputs = lines.ToArray();
            using var digest = SHA256.Create();
            return Convert.ToBase64String(digest.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
        }

        private static string FingerprintFile(string file)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            bool text = new[] { ".json", ".meta", ".asmdef", ".asmref" }.Contains(extension);
            if (extension == ".asset")
            {
                using var stream = File.OpenRead(file);
                byte[] header = new byte[5];
                text = stream.Read(header, 0, header.Length) == header.Length && Encoding.ASCII.GetString(header) == "%YAML";
            }
            if (!text)
            {
                return HotfixDeploymentVerification.HashFile(file);
            }
            // Unity/ProBuilder 退出时可能仅把 checkout 的 CRLF 保存为 LF；配置值仍全部参与校验。
            string content = File.ReadAllText(file, new UTF8Encoding(false, true)).Replace("\r\n", "\n");
            if (file.Replace('\\', '/') == "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset")
            {
                content = FingerprintRenderPipelineRuntimeSettings(file, content);
            }
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "").ToLowerInvariant();
        }

        // Full 的 Player 序列化会按平台剥离此缓存；Hotfix 只重算哈希输入，源设置和引用仍全部校验。
        private static string FingerprintRenderPipelineRuntimeSettings(string file, string content)
        {
            var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineGlobalSettings>(file);
            Require(asset != null && asset == GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>(), "URP 全局设置资产未正确注册。");
            using var serialized = new SerializedObject(asset);
            var list = serialized.FindProperty("m_Settings.m_SettingsList.m_List");
            Require(list != null && list.isArray, "URP 源设置列表不存在。");
            var settings = new List<IRenderPipelineGraphicsSettings>();
            var references = new Dictionary<IRenderPipelineGraphicsSettings, long>();
            for (int index = 0; index < list.arraySize; index++)
            {
                var item = list.GetArrayElementAtIndex(index);
                if (item.managedReferenceValue == null)
                {
                    Require(item.managedReferenceId == -2, "URP 源设置存在失效引用。");
                    continue;
                }
                var setting = item.managedReferenceValue as IRenderPipelineGraphicsSettings;
                Require(setting != null && item.managedReferenceId >= 0 && !references.ContainsKey(setting), "URP 源设置引用不合法。");
                settings.Add(setting);
                references.Add(setting, item.managedReferenceId);
            }
            var matches = Regex.Matches(content, @"(?m)^    m_RuntimeSettings:\n      m_List:(?: \[\]\n|\n(?:      - rid: -?\d+\n)*)(?=  \S)");
            Require(matches.Count == 1 && settings.Count > 0, "URP 运行时缓存列表格式不唯一或源设置为空。");
            var stripper = Type.GetType("UnityEngine.Rendering.RenderPipelineGraphicsSettingsStripper, Unity.RenderPipelines.Core.Runtime", true);
            var perform = stripper.GetMethod("PerformStripping", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(List<IRenderPipelineGraphicsSettings>), typeof(List<IRenderPipelineGraphicsSettings>) }, null);
            var core = Type.GetType("UnityEditor.Rendering.CoreBuildData, Unity.RenderPipelines.Core.Editor", true).GetProperty("instance");
            var urp = Type.GetType("UnityEditor.Rendering.Universal.URPBuildData, Unity.RenderPipelines.Universal.Editor", true);
            var buildingUrp = urp.GetProperty("buildingPlayerForUniversalRenderPipeline");
            Require(perform != null && perform.ReturnType == typeof(void) && core != null && buildingUrp?.PropertyType == typeof(bool),
                "锁定 URP 包的构建剥离接口不可用。");
            ((IDisposable)core.GetValue(null)).Dispose();
            IDisposable buildData = null;
            try
            {
                buildData = (IDisposable)Activator.CreateInstance(urp, EditorUserBuildSettings.activeBuildTarget, EditorUserBuildSettings.development);
                Require((bool)buildingUrp.GetValue(buildData), "当前平台没有有效 URP 构建配置。");
                var runtime = new List<IRenderPipelineGraphicsSettings>();
                perform.Invoke(null, new object[] { settings, runtime });
                Require(runtime.All(setting => setting != null && references.ContainsKey(setting)), "URP 剥离结果包含未知引用。");
                string block = "    m_RuntimeSettings:\n      m_List:" + (runtime.Count == 0 ? " []\n" : "\n" +
                    string.Concat(runtime.Select(setting => "      - rid: " + references[setting] + "\n")));
                var match = matches[0];
                return content.Substring(0, match.Index) + block + content.Substring(match.Index + match.Length);
            }
            finally
            {
                if (buildData != null)
                {
                    buildData.Dispose();
                }
                else
                {
                    ((IDisposable)core.GetValue(null)).Dispose();
                }
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            GitHubPagesDeployment.EnsureNoLinks(source);
            GitHubPagesDeployment.EnsureNoLinks(destination);
            Directory.CreateDirectory(destination);
            foreach (string file in Directory.GetFiles(source).Where(file => file.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".dll.bytes", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)))
            {
                GitHubPagesDeployment.EnsureNoLinks(file);
                File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
            }
        }

        private static bool IsWithin(string path, string directory) => Path.GetFullPath(path).TrimEnd('\\', '/')
            .StartsWith(Path.GetFullPath(directory).TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Path.GetFullPath(path).TrimEnd('\\', '/'), Path.GetFullPath(directory).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

        private static string RequiredEnvironment(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            Require(!string.IsNullOrWhiteSpace(value), "缺少 Jenkins 凭据环境变量 " + name + "。");
            return value;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private static void WriteJson(string path, object value)
        {
            GitHubPagesDeployment.EnsureNoLinks(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(value, true), new UTF8Encoding(false));
        }
    }
}
