using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Core.Runtime;
using HybridCLR.Editor;
using HybridCLR.Editor.Commands;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using YooAsset.Editor;

namespace Hotfix.Editor.JinxCasino
{
    /// 当前Editor内的S1单机构建事务；平台进度保存在SessionState，重编译后继续。
    [InitializeOnLoad]
    public static class JinxCasinoPlayerBuildPipeline
    {
        public const string EntranceScene = "Assets/Scenes/AppEntrance.unity";
        private const string JobKey = "JinxCasino.PlayerBuild.Job";
        private const string StatusKey = "JinxCasino.PlayerBuild.Status";
        private const string CodeRoot = "Assets/LoadResources/Codes/JinxCasino";
        private const string ConfigRoot = "Assets/Settings/JinxCasino";
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private const string StreamingRoot = "Assets/StreamingAssets/yoo";
        private const string PlayerFileStem = "JinxCasinoS1-Offline";
        private const string ReadmeFileName = "S1-Offline-README.txt";
        private static bool isExecuting;

        /// 是否存在尚未恢复结束的构建事务。
        public static bool IsBusy => !string.IsNullOrEmpty(SessionState.GetString(JobKey, string.Empty));
        /// 最近构建进度或恢复结果。
        public static string Status => SessionState.GetString(StatusKey, "等待构建S1单机沉浸样板。");

        static JinxCasinoPlayerBuildPipeline()
        {
            EditorApplication.update += ContinueJob;
        }

        /// <summary>在当前 Editor 建立构建事务并异步切换目标平台，不另起 Unity 实例。</summary>
        /// <param name="target">只支持 StandaloneWindows64 或 Android；已有任务和未保存场景时拒绝。</param>
        public static void Start(BuildTarget target)
        {
            const string scenePath = "Assets/LoadResources/Demos/jinx_casino/Scenes/Immersion.unity";
            const string hudPath = "Assets/LoadResources/Demos/jinx_casino/Prefabs/UI/JinxCasinoImmersionHudView.prefab";
            if (!File.Exists(scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null ||
                !File.Exists(hudPath) || AssetDatabase.LoadAssetAtPath<GameObject>(hudPath) == null)
                throw new InvalidOperationException("S1构建需要已保存并导入的Immersion场景和沉浸HUD。");
            if (target != BuildTarget.StandaloneWindows64 && target != BuildTarget.Android)
                throw new ArgumentOutOfRangeException(nameof(target));
            if (IsBusy) throw new InvalidOperationException("已有离线构建任务，请先等待或恢复。");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("请在非 Play 模式且编译/导入完成后开始构建。");
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)
                    throw new InvalidOperationException("存在未保存的场景；构建不会代为保存或丢弃它。");
            string[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (enabled.Length != 1 || enabled[0] != EntranceScene)
                throw new InvalidOperationException("构建要求 Build Settings 只启用 AppEntrance；工具不会修改列表。");
            if (!UnityEditor.BuildPipeline.IsBuildTargetSupported(UnityEditor.BuildPipeline.GetBuildTargetGroup(target), target))
                throw new BuildFailedException("未安装目标平台构建模块：" + target);

            string version = DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var job = new BuildJob
            {
                Target = (int)target, OriginalTarget = (int)EditorUserBuildSettings.activeBuildTarget,
                Version = version, Phase = "WaitingTarget",
                BackupRoot = "Library/JinxCasinoBuild/" + version + "/Backup",
                OutputRoot = "Builds/JinxCasino/S1/" + target + "/" + version,
                WorkRoot = "Library/JX/" + Guid.NewGuid().ToString("N").Substring(0, 8),
                PackageName = target == BuildTarget.StandaloneWindows64 ? "JinxW" : "JinxA",
                ConfigPath = ConfigRoot + "/PlayerBuild/S1/" + target + "/OfflineHotfix.asset"
            };
            CaptureSettings(job);
            CaptureRenderingAssets(job);
            CaptureCompilerHelper(job);
            CaptureNativePluginSettings(job);
            var collector = BundleCollectorSettingData.Setting;
            job.CollectorJson = EditorJsonUtility.ToJson(collector);
            job.CollectorWasDirty = EditorUtility.IsDirty(collector);
            BackupTree(job, CodeRoot);
            BackupFile(job, CodeRoot + ".meta");
            // 保留这个已存在的父目录本身，只恢复其子内容，不执行删除父目录。
            BackupTree(job, ConfigRoot, true);
            BackupFile(job, ConfigRoot + ".meta");
            BackupFile(job, ProjectSettingsPath);
            BackupTree(job, StreamingRoot);
            BackupFile(job, StreamingRoot + ".meta");
            // HybridCLR 的桥接/native 文件是共享目录，生成前备份，整个平台完成后才恢复。
            BackupTree(job, SettingsUtil.GeneratedCppDir);
            job.AotReferencePath = Path.Combine(Application.dataPath, SettingsUtil.HybridCLRSettings.outputAOTGenericReferenceFile);
            BackupFile(job, job.AotReferencePath);
            BackupFile(job, job.AotReferencePath + ".meta");
            string linkPath = Path.Combine(Application.dataPath, SettingsUtil.HybridCLRSettings.outputLinkFile);
            BackupFile(job, linkPath);
            BackupFile(job, linkPath + ".meta");
            Save(job, "等待切换目标平台：" + target);
        }

        /// 取消后续构建阶段，并在下一次安全 Editor update 恢复原配置。
        public static void RequestRestore()
        {
            var job = ReadJob();
            if (job == null) return;
            job.Error = "用户取消后续构建步骤。";
            job.Phase = "Restoring";
            Save(job, "等待恢复编辑器配置。");
        }

        /// <summary>仅重打包已有Windows离线Player，不重新编译，也不删除原包或本机排障目录。</summary>
        /// <param name="playerRoot">当前项目Builds/JinxCasino/S1目录下的Windows Player目录，支持绝对或项目相对路径。</param>
        /// <returns>新生成的 Playable ZIP 绝对路径；已有同名 ZIP 时使用唯一后缀保留旧证据。</returns>
        public static string RepackageWindowsPlayer(string playerRoot)
        {
            if (IsBusy || UnityEditor.BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("构建事务仍在执行，不能重打包。");
            string root = WorkspacePath(playerRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string outputRoot = WorkspacePath("Builds/JinxCasino/S1/StandaloneWindows64").TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!root.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(root) != "Player")
                throw new ArgumentException("必须选择当前项目已保存的Windows S1 Player目录。", nameof(playerRoot));
            string stem = PlayerFileStem;
            string archive = Path.Combine(Path.GetDirectoryName(root), stem + "-Windows-Playable.zip");
            if (File.Exists(archive))
                archive = Path.Combine(Path.GetDirectoryName(root), stem + "-Windows-Playable-" +
                    DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".zip");
            ZipWindowsPayload(root, archive, stem);
            Debug.Log("[JinxCasinoBuild] 已仅重打包Windows S1运行文件，原ZIP与排障目录保留：" + archive);
            return archive;
        }

        /// <summary>供构建场景处理器读取当前事务的平台配置，普通构建返回 false。</summary>
        /// <param name="config">仅本次 Generating/Building 阶段使用的配置资源。</param>
        /// <returns>是否正在执行本构建事务的 Player/HybridCLR 临时 Player。</returns>
        public static bool TryGetBuildConfig(out HotfixConfig config)
        {
            var job = ReadJob();
            config = null;
            if (job == null || (job.Phase != "Generating" && job.Phase != "Building")) return false;
            config = AssetDatabase.LoadAssetAtPath<HotfixConfig>(job.ConfigPath);
            if (config == null) throw new BuildFailedException("S1平台配置丢失：" + job.ConfigPath);
            return true;
        }

        private static void ContinueJob()
        {
            if (isExecuting || !IsBusy || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.BuildPipeline.isBuildingPlayer) return;
            var job = ReadJob();
            if (job == null) return;
            if (job.Phase == "RecoveryRequired") return;
            isExecuting = true;
            try
            {
                var target = (BuildTarget)job.Target;
                switch (job.Phase)
                {
                    case "WaitingTarget":
                        if (EditorUserBuildSettings.activeBuildTarget != target)
                        {
                            if (job.SwitchRequested) return;
                            job.SwitchRequested = true;
                            WriteJob(job);
                            if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(UnityEditor.BuildPipeline.GetBuildTargetGroup(target), target))
                                throw new BuildFailedException("目标平台切换失败：" + target);
                            return;
                        }
                        job.SwitchRequested = false;
                        ConfigureOfflineBuild(job);
                        CreateConfig(job, Array.Empty<string>());
                        job.Phase = "ReadyGenerate";
                        Save(job, "目标平台已就绪，准备 HybridCLR：" + target);
                        break;
                    case "ReadyGenerate":
                        job.Phase = "Generating";
                        Save(job, "生成本平台 HybridCLR AOT、DLL 与桥接文件：" + target);
                        PrebuildCommand.GenerateAll();
                        job.Phase = "WaitingGeneratedImport";
                        Save(job, "等待 HybridCLR 生成文件导入和编译完成。");
                        break;
                    case "WaitingGeneratedImport":
                        StagePlatformAssemblies(job);
                        job.Phase = "ReadyBuild";
                        Save(job, "平台 DLL 已隔离，等待资源导入后构建内置包与 Player。");
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                        break;
                    case "ReadyBuild":
                        job.Phase = "Building";
                        Save(job, "构建S1离线资源与Player：" + target);
                        BuildResourcesAndPlayer(job);
                        job.Succeeded = true;
                        job.Phase = "Restoring";
                        Save(job, "离线包构建成功，恢复编辑器配置。");
                        break;
                    case "Restoring":
                        Restore(job);
                        job.SwitchRequested = false;
                        job.Phase = "WaitingOriginalTarget";
                        Save(job, "配置与生成物已恢复，等待恢复原编辑器平台。");
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                        break;
                    case "WaitingOriginalTarget":
                        var originalTarget = (BuildTarget)job.OriginalTarget;
                        if (EditorUserBuildSettings.activeBuildTarget != originalTarget)
                        {
                            if (job.SwitchRequested) return;
                            job.SwitchRequested = true;
                            WriteJob(job);
                            if (!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(UnityEditor.BuildPipeline.GetBuildTargetGroup(originalTarget), originalTarget))
                                throw new BuildFailedException("原编辑器平台恢复失败：" + originalTarget);
                            return;
                        }
                        Finish(job);
                        break;
                    // 若域重载真的打断原子生成/构建，不猜测半成品可用，恢复后报告失败。
                    case "Generating":
                    case "Building":
                        throw new BuildFailedException("域重载中断了生成/构建阶段，需恢复后重试。");
                    default:
                        throw new BuildFailedException("未知构建阶段：" + job.Phase);
                }
            }
            catch (Exception exception)
            {
                job.Error = exception.ToString();
                if (job.Phase == "Restoring" || job.Phase == "WaitingOriginalTarget")
                {
                    SessionState.SetString(StatusKey, "恢复失败，备份保留在 " + job.BackupRoot + "；" + exception.Message);
                    // 暂停自动重试，避免每帧重放失败恢复。保留 job，窗口可再次请求恢复。
                    job.Phase = "RecoveryRequired";
                    WriteJob(job);
                }
                else
                {
                    job.Phase = "Restoring";
                    Save(job, "构建失败，准备恢复：" + exception.Message);
                }
                Debug.LogException(exception);
            }
            finally { isExecuting = false; }
        }

        private static void StagePlatformAssemblies(BuildJob job)
        {
            var target = (BuildTarget)job.Target;
            if (!File.Exists(job.AotReferencePath)) throw new BuildFailedException("缺少本次 AOT 引用文件。");
            var match = Regex.Match(File.ReadAllText(job.AotReferencePath), @"PatchedAOTAssemblyList\s*=\s*new\s+List<string>\s*\{(?<body>[\s\S]*?)\}");
            if (!match.Success) throw new BuildFailedException("无法读取本次 PatchedAOTAssemblyList。");
            string[] aot = Regex.Matches(match.Groups["body"].Value, "\"([^\"]+\\.dll)\"").Cast<Match>()
                .Select(item => item.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
            string[] hotfix = SettingsUtil.HotUpdateAssemblyFilesExcludePreserved.ToArray();
            if (aot.Length == 0 || hotfix.Length == 0) throw new BuildFailedException("AOT/Hotfix 清单不能为空。");
            string root = CodeRoot + "/" + target;
            CopyAssemblies(SettingsUtil.GetAssembliesPostIl2CppStripDir(target), root + "/Aot", aot);
            CopyAssemblies(SettingsUtil.GetHotUpdateDllsOutputDirByTarget(target), root + "/Hotfix", hotfix);
            CreateConfig(job, aot);
        }

        private static void CopyAssemblies(string source, string destination, IEnumerable<string> files)
        {
            EnsureAssetFolder(destination);
            foreach (string name in files)
            {
                if (Path.GetFileName(name) != name) throw new BuildFailedException("程序集名称必须是文件名：" + name);
                string path = Path.Combine(source, name);
                if (!File.Exists(path)) throw new BuildFailedException("缺少目标平台程序集：" + path);
                File.Copy(path, destination + "/" + name + ".bytes", true);
            }
        }

        private static void CreateConfig(BuildJob job, string[] aot)
        {
            EnsureAssetFolder(Path.GetDirectoryName(job.ConfigPath).Replace('\\', '/'));
            var config = AssetDatabase.LoadAssetAtPath<HotfixConfig>(job.ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<HotfixConfig>();
                AssetDatabase.CreateAsset(config, job.ConfigPath);
            }
            config.PlayMode = ResourcePlayMode.OfflinePlayMode;
            config.PackageName = job.PackageName;
            config.StartupScene = "JinxCasino";
            config.AotAssemblies = aot;
            config.HotfixAssemblies = SettingsUtil.HotUpdateAssemblyFilesExcludePreserved.ToArray();
            config.AotSourcePath = SettingsUtil.GetAssembliesPostIl2CppStripDir((BuildTarget)job.Target);
            config.HotfixSourcePath = SettingsUtil.GetHotUpdateDllsOutputDirByTarget((BuildTarget)job.Target);
            config.AotTargetPath = CodeRoot + "/" + (BuildTarget)job.Target + "/Aot";
            config.HotfixTargetPath = CodeRoot + "/" + (BuildTarget)job.Target + "/Hotfix";
            config.LocalBundlePath = job.OutputRoot + "/Bundles";
            // 不读默认配置，也不继承 SSH、服务器或私钥设置。
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }

        private static void BuildResourcesAndPlayer(BuildJob job)
        {
            var setting = BundleCollectorSettingData.Setting;
            try
            {
                var original = setting.Packages.Single(package => package.PackageName == ResourceInitializeOptions.DefaultPackageName);
                var package = JsonUtility.FromJson<BundleCollectorPackage>(JsonUtility.ToJson(original));
                package.PackageName = job.PackageName;
                foreach (var group in package.Groups)
                    group.Collectors.RemoveAll(collector => collector.CollectPath.StartsWith("Assets/LoadResources/Codes", StringComparison.Ordinal));
                var codes = new BundleCollectorGroup { GroupName = "JinxPlatformCodes", ActiveRuleName = nameof(EnableGroup) };
                foreach (string leaf in new[] { "Aot", "Hotfix" })
                {
                    string path = CodeRoot + "/" + (BuildTarget)job.Target + "/" + leaf;
                    codes.Collectors.Add(new BundleCollector
                    {
                        CollectPath = path, CollectorGUID = AssetDatabase.AssetPathToGUID(path),
                        AddressRuleName = nameof(AddressByFileName), PackRuleName = nameof(PackDirectory), FilterRuleName = nameof(CollectAll)
                    });
                }
                package.Groups.Add(codes);
                setting.Packages.Add(package);
                string builtin = job.WorkRoot + "/i";
                var parameters = new ScriptableBuildParameters
                {
                    // SBP 的 OutputCache 使用逻辑 BundleName；必须缩短根目录，HashName 只缩短最终文件名。
                    BuildOutputRoot = job.WorkRoot + "/b", BundledFileRoot = builtin,
                    BuildPipeline = nameof(ScriptableBuildPipeline), BuildBundleType = (int)YooAsset.EBundleType.AssetBundle,
                    BuildTarget = (BuildTarget)job.Target, PackageName = job.PackageName, PackageVersion = job.Version,
                    EnableSharePackRule = true, VerifyBuildingResult = true, CompressOption = ECompressOption.LZ4,
                    BundledCopyOption = EBundledCopyOption.ClearAndCopyAll,
                    FileNameStyle = YooAsset.EFileNameStyle.HashName,
                    BuiltinShadersBundleName = DefaultBundlePackRule.CreateShadersPackRuleResult().GetBundleName(job.PackageName, setting.UniqueBundleName)
                };
                var result = new ScriptableBuildPipeline().Run(parameters, true);
                if (!result.Success) throw new BuildFailedException(result.FailedTask + ": " + result.ErrorInfo + "\n" + result.ErrorStack);
                // 交付目录只保存短哈希最终包，长逻辑名称的管线缓存留在 Library 工作目录。
                CopyTree(result.OutputPackageDirectory, job.OutputRoot + "/Bundles");
                DeleteTree(StreamingRoot);
                CopyTree(builtin, StreamingRoot);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string playerRoot = job.OutputRoot + "/Player";
                Directory.CreateDirectory(playerRoot);
                string stem = PlayerFileStem;
                string location = playerRoot + "/" + stem + ((BuildTarget)job.Target == BuildTarget.Android ? ".apk" : ".exe");
                var report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { EntranceScene }, target = (BuildTarget)job.Target,
                    targetGroup = UnityEditor.BuildPipeline.GetBuildTargetGroup((BuildTarget)job.Target),
                    locationPathName = location, options = BuildOptions.Development
                });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                    throw new BuildFailedException("S1 Player构建失败：" + report.summary.result);
                File.WriteAllText(playerRoot + "/" + ReadmeFileName, ReadmeText());
                if ((BuildTarget)job.Target == BuildTarget.StandaloneWindows64)
                    ZipWindowsPayload(playerRoot, job.OutputRoot + "/" + stem + "-Windows-Playable.zip", stem);
            }
            finally { RestoreCollector(job); }
        }

        private static void ConfigureOfflineBuild(BuildJob job)
        {
            PlayerSettings.companyName = "SleepyStudio";
            PlayerSettings.productName = "JinxCasino S1 Offline";
            PlayerSettings.bundleVersion = "0.5.0";
            var named = NamedBuildTarget.FromBuildTargetGroup(UnityEditor.BuildPipeline.GetBuildTargetGroup((BuildTarget)job.Target));
            PlayerSettings.SetApplicationIdentifier(named, "com.sleepystudio.jinxcasino");
            PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.development = true;
            EditorUserBuildSettings.allowDebugging = false;
            EditorUserBuildSettings.connectProfiler = false;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = false;
            EditorUserBuildSettings.buildScriptsOnly = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.useCustomKeystore = false;
            if ((BuildTarget)job.Target == BuildTarget.Android)
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                PlayerSettings.allowedAutorotateToPortrait = false;
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
            }
            ExcludeNativePlugins(job);
            PrepareCompilerHelper(job);
            UnityEditor.WindowsStandalone.UserBuildSettings.createSolution = false;
        }

        private static string ReadmeText()
        {
            return "倒霉蛋俱乐部 · S1独立单人沉浸样板 0.5.0\n" +
                "启动后直接进入赌场主菜单，可选择互动教学、自由练习或正式冒险。暂停及结局可返回游戏主菜单。\n" +
                "当前仅一间大厅、一区及水果机、Blackjack、协作拉杆三款游戏；不是四区/17游戏完整版本，不包含互联网联机。\n" +
                "Windows：WASD移动、鼠标转向、E进入，点击桌面物件操作；Esc离桌，暂停菜单使用屏幕按钮。\n" +
                "手柄：左摇杆移动、右摇杆转向、A进入/操作、方向选择、X次要动作、Y规则、B返回、Start暂停。\n" +
                "Android：左侧移动、右侧转向、点击交互和桌面物件；可在设置中调整输入、音量与左右手布局。\n" +
                "本包用于S1阶段验证，S1整体尚未验收；Android真机性能、实际安装与设备适配需分别验证。\n";
        }

        private static void ZipWindowsPayload(string playerRoot, string archivePath, string stem)
        {
            string root = WorkspacePath(playerRoot);
            string archive = WorkspacePath(archivePath);
            foreach (string required in new[] { stem + ".exe", "UnityPlayer.dll", "GameAssembly.dll", ReadmeFileName })
                if (!File.Exists(Path.Combine(root, required))) throw new InvalidOperationException("缺少Windows S1运行文件：" + required);
            if (!Directory.Exists(Path.Combine(root, stem + "_Data")))
                throw new InvalidOperationException("缺少Windows S1 _Data目录。");
            Directory.CreateDirectory(Path.GetDirectoryName(archive));
            using (var stream = new FileStream(archive, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                // 在遍历前就排除辅助目录，避免扫描巨大的 C++ 备份树和其中的长路径。
                var files = Directory.GetFiles(root).Concat(Directory.GetDirectories(root)
                    .Where(directory => IsWindowsRuntimePayload(Path.GetFileName(directory) + "/", stem))
                    .SelectMany(directory => Directory.GetFiles(directory, "*", SearchOption.AllDirectories)));
                foreach (string file in files.OrderBy(path => path, StringComparer.Ordinal))
                {
                    string relative = Path.GetRelativePath(root, file).Replace('\\', '/');
                    if (!IsWindowsRuntimePayload(relative, stem)) continue;
                    zip.CreateEntryFromFile(file, relative, System.IO.Compression.CompressionLevel.Optimal);
                }
            }
        }

        private static bool IsWindowsRuntimePayload(string relativePath, string stem)
        {
            string top = relativePath.Split('/')[0];
            // Unity 的这些顶层辅助目录供开发机排障；从分享包排除，原目录仍留在 Player 下。
            if (top.EndsWith("_BurstDebugInformation_DoNotShip", StringComparison.OrdinalIgnoreCase) ||
                top.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame", StringComparison.OrdinalIgnoreCase)) return false;
            string extension = Path.GetExtension(relativePath);
            // Unity 运行时目录按完整子树保留，不把业务数据文件误判为同扩展名的符号文件。
            if (relativePath.IndexOf('/') >= 0)
                return top == stem + "_Data" || top == "D3D12" || top == "MonoBleedingEdge" || top == "Plugins";
            if (extension.Equals(".pdb", StringComparison.OrdinalIgnoreCase) || extension.Equals(".mdb", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".ilk", StringComparison.OrdinalIgnoreCase) || extension.Equals(".map", StringComparison.OrdinalIgnoreCase)) return false;
            return top == stem + ".exe" || top == "UnityCrashHandler64.exe" || top == ReadmeFileName ||
                extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) || extension.Equals(".config", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".ini", StringComparison.OrdinalIgnoreCase);
        }

        private static void CaptureSettings(BuildJob job)
        {
            job.Company = PlayerSettings.companyName;
            var settingsObject = GetPlayerSettingsObject();
            job.PlayerSettingsJson = EditorJsonUtility.ToJson(settingsObject);
            job.PlayerSettingsWasDirty = EditorUtility.IsDirty(settingsObject);
            job.Product = PlayerSettings.productName;
            job.BundleVersion = PlayerSettings.bundleVersion;
            job.AndroidIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            job.StandaloneIdentifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone);
            job.AndroidBackend = (int)PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            job.StandaloneBackend = (int)PlayerSettings.GetScriptingBackend(NamedBuildTarget.Standalone);
            job.Development = EditorUserBuildSettings.development;
            job.Debugging = EditorUserBuildSettings.allowDebugging;
            job.Profiler = EditorUserBuildSettings.connectProfiler;
            job.DeepProfiling = EditorUserBuildSettings.buildWithDeepProfilingSupport;
            job.ScriptsOnly = EditorUserBuildSettings.buildScriptsOnly;
            job.AndroidExport = EditorUserBuildSettings.exportAsGoogleAndroidProject;
            job.AppBundle = EditorUserBuildSettings.buildAppBundle;
            job.CustomKeystore = PlayerSettings.Android.useCustomKeystore;
            job.AndroidMinimumSdk = (int)PlayerSettings.Android.minSdkVersion;
            job.AndroidArchitectures = (int)PlayerSettings.Android.targetArchitectures;
            job.CreateSolution = UnityEditor.WindowsStandalone.UserBuildSettings.createSolution;
            job.AndroidBuildLocation = EditorUserBuildSettings.GetBuildLocation(BuildTarget.Android);
            job.WindowsBuildLocation = EditorUserBuildSettings.GetBuildLocation(BuildTarget.StandaloneWindows64);
            job.Il2CppEnvironment = Environment.GetEnvironmentVariable("UNITY_IL2CPP_PATH");
            job.ProcessPathExt = Environment.GetEnvironmentVariable("PATHEXT");
        }

        private static void Restore(BuildJob job)
        {
            // 在任何覆盖/删除前确认备份仍存在，避免失败恢复先清空共享生成物。
            foreach (var entry in job.Backups)
                if (entry.Existed && (entry.IsDirectory ? !Directory.Exists(entry.Backup) : !File.Exists(entry.Backup)))
                    throw new InvalidOperationException("构建备份缺失，恢复已暂停：" + entry.Backup);
            RestoreCollector(job);
            PlayerSettings.companyName = job.Company;
            PlayerSettings.productName = job.Product;
            PlayerSettings.bundleVersion = job.BundleVersion;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, job.AndroidIdentifier);
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, job.StandaloneIdentifier);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, (ScriptingImplementation)job.AndroidBackend);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, (ScriptingImplementation)job.StandaloneBackend);
            PlayerSettings.Android.useCustomKeystore = job.CustomKeystore;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)job.AndroidMinimumSdk;
            PlayerSettings.Android.targetArchitectures = (AndroidArchitecture)job.AndroidArchitectures;
            RestoreNativePlugins(job);
            EditorUserBuildSettings.development = job.Development;
            EditorUserBuildSettings.allowDebugging = job.Debugging;
            EditorUserBuildSettings.connectProfiler = job.Profiler;
            EditorUserBuildSettings.buildWithDeepProfilingSupport = job.DeepProfiling;
            EditorUserBuildSettings.buildScriptsOnly = job.ScriptsOnly;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = job.AndroidExport;
            EditorUserBuildSettings.buildAppBundle = job.AppBundle;
            UnityEditor.WindowsStandalone.UserBuildSettings.createSolution = job.CreateSolution;
            EditorUserBuildSettings.SetBuildLocation(BuildTarget.Android, job.AndroidBuildLocation);
            EditorUserBuildSettings.SetBuildLocation(BuildTarget.StandaloneWindows64, job.WindowsBuildLocation);
            Environment.SetEnvironmentVariable("UNITY_IL2CPP_PATH", job.Il2CppEnvironment);
            Environment.SetEnvironmentVariable("PATHEXT", job.ProcessPathExt);
            CleanupCompilerHelper(job);
            foreach (var entry in job.Backups)
            {
                if (entry.IsDirectory)
                {
                    if (entry.PreserveRoot) ClearDirectoryChildren(entry.Source);
                    else DeleteTree(entry.Source);
                    if (entry.Existed) CopyTree(entry.Backup, entry.Source);
                }
                else
                {
                    string source = WorkspacePath(entry.Source);
                    if (source == WorkspacePath(ProjectSettingsPath)) continue;
                    if (entry.Existed)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(source));
                        File.Copy(entry.Backup, source, true);
                    }
                    else if (File.Exists(source)) File.Delete(source);
                }
            }
            RestorePlayerSettingsFile(job);
            RestoreRenderingAssets(job);
        }

        private static void RestoreCollector(BuildJob job)
        {
            var setting = BundleCollectorSettingData.Setting;
            EditorJsonUtility.FromJsonOverwrite(job.CollectorJson, setting);
            if (job.CollectorWasDirty) EditorUtility.SetDirty(setting);
            else EditorUtility.ClearDirty(setting);
        }

        private static void CaptureRenderingAssets(BuildJob job)
        {
            string platform = NamedBuildTarget.FromBuildTargetGroup(UnityEditor.BuildPipeline.GetBuildTargetGroup((BuildTarget)job.Target)).TargetName;
            int[] qualityLevels = QualitySettings.GetActiveQualityLevelsForPlatform(platform);
            bool needsDefaultPipeline = qualityLevels.Length == 0;
            foreach (int qualityLevel in qualityLevels)
            {
                RenderPipelineAsset pipeline = QualitySettings.GetRenderPipelineAssetAt(qualityLevel);
                if (pipeline == null) needsDefaultPipeline = true;
                else if (pipeline is UniversalRenderPipelineAsset) CaptureRenderingAsset(job, pipeline);
            }
            if (needsDefaultPipeline && UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultPipeline)
                CaptureRenderingAsset(job, defaultPipeline);
            if (job.RenderingAssets.Count > 0)
                CaptureRenderingAsset(job, UnityEngine.Rendering.GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>());
        }

        private static void CaptureRenderingAsset(BuildJob job, UnityEngine.Object asset)
        {
            if (asset == null) return;
            string path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path) || !File.Exists(WorkspacePath(path)))
                throw new BuildFailedException("目标 URP 配置必须是已有项目资源：" + asset.name);
            if (job.RenderingAssets.Any(entry => entry.AssetPath == path)) return;
            job.RenderingAssets.Add(new RenderingAssetBackup
            {
                AssetPath = path, Json = EditorJsonUtility.ToJson(asset), WasDirty = EditorUtility.IsDirty(asset)
            });
            BackupFile(job, path);
            BackupFile(job, path + ".meta");
        }

        private static void RestoreRenderingAssets(BuildJob job)
        {
            foreach (var entry in job.RenderingAssets)
            {
                string source = WorkspacePath(entry.AssetPath);
                var backup = job.Backups.Single(item => !item.IsDirectory && item.Source == source);
                string metaSource = WorkspacePath(entry.AssetPath + ".meta");
                var metaBackup = job.Backups.Single(item => !item.IsDirectory && item.Source == metaSource);
                if (!backup.Existed || !File.Exists(backup.Backup))
                    throw new InvalidOperationException("URP 配置原始备份丢失：" + entry.AssetPath);
                if (!metaBackup.Existed || !File.Exists(metaBackup.Backup))
                    throw new InvalidOperationException("URP 配置 meta 备份丢失：" + entry.AssetPath);
                // 先还原磁盘后重载 managed-reference 容器，确保 GlobalSettings 被裁掉的 rid 也完整恢复。
                File.Copy(backup.Backup, source, true);
                File.Copy(metaBackup.Backup, metaSource, true);
                AssetDatabase.ImportAsset(entry.AssetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var asset = AssetDatabase.LoadMainAssetAtPath(entry.AssetPath);
                if (asset == null) throw new InvalidOperationException("URP 配置重载失败：" + entry.AssetPath);
                EditorJsonUtility.FromJsonOverwrite(entry.Json, asset);
                // 不 SaveAssets/SaveAssetIfDirty；构建副作用不能借缓存再次覆盖原字节。
                EditorUtility.ClearDirty(asset);
                File.Copy(backup.Backup, source, true);
                File.Copy(metaBackup.Backup, metaSource, true);
                if (!File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(backup.Backup)))
                    throw new IOException("URP 配置字节恢复校验失败：" + entry.AssetPath);
                if (!File.ReadAllBytes(metaSource).SequenceEqual(File.ReadAllBytes(metaBackup.Backup)))
                    throw new IOException("URP 配置 meta 字节恢复校验失败：" + entry.AssetPath);
                if (entry.WasDirty) EditorUtility.SetDirty(asset);
            }
        }

        private static void CaptureCompilerHelper(BuildJob job)
        {
            if ((BuildTarget)job.Target != BuildTarget.StandaloneWindows64) return;
            job.SystemChcpPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "chcp.com");
            if (!File.Exists(job.SystemChcpPath)) throw new BuildFailedException("本机 System32 缺少 chcp.com。");
            job.ChcpHash = FileHash(job.SystemChcpPath);
            VerifySystemChcpSignature(job.SystemChcpPath);
            if (FileHash(job.SystemChcpPath) != job.ChcpHash)
                throw new BuildFailedException("签名校验期间系统 chcp.com 已变化，停止构建。");
            string helper = WorkspacePath("chcp.com");
            job.ChcpInitiallyExisted = File.Exists(helper);
            if (job.ChcpInitiallyExisted && FileHash(helper) != job.ChcpHash)
                throw new BuildFailedException("项目根已有不同 chcp.com，构建不会覆盖该文件。");
        }

        private static void VerifySystemChcpSignature(string path)
        {
            // 直接启动系统 PowerShell 且禁用 Profile，避开 cmd AutoRun；只查询一个系统文件的签名。
            string powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell/v1.0/powershell.exe");
            string systemModules = Path.Combine(Path.GetDirectoryName(powershell), "Modules");
            string securityModule = Path.Combine(systemModules, "Microsoft.PowerShell.Security/Microsoft.PowerShell.Security.psd1");
            string literalPath = path.Replace("'", "''");
            string command = "$ErrorActionPreference='Stop';Import-Module -Name '" + securityModule.Replace("'", "''") +
                "';$s=Get-AuthenticodeSignature -LiteralPath '" + literalPath +
                "';if($s.Status -eq 'Valid' -and $s.SignerCertificate.Subject -match 'Microsoft'){exit 0}else{exit 1}";
            var options = new System.Diagnostics.ProcessStartInfo
            {
                FileName = powershell, Arguments = "-NoLogo -NoProfile -NonInteractive -Command \"" + command + "\"",
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardError = true
            };
            // Editor 可能从 PowerShell 7 启动并继承其模块路径；系统 PowerShell 只加载自己的系统模块。
            // 仅修改校验子进程环境，不更改 Editor 或用户的 PSModulePath。
            options.EnvironmentVariables["PSModulePath"] = systemModules;
            using (var process = System.Diagnostics.Process.Start(options))
            {
                if (process == null) throw new BuildFailedException("无法验证系统 chcp.com 签名。");
                if (!process.WaitForExit(30000))
                {
                    process.Kill();
                    throw new BuildFailedException("系统 chcp.com 签名校验超时。");
                }
                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new BuildFailedException("系统 chcp.com 必须具有有效 Microsoft 签名。" +
                        (string.IsNullOrWhiteSpace(error) ? "校验进程退出码：" + process.ExitCode : "校验进程：" + error.Trim()));
                }
            }
        }

        private static void PrepareCompilerHelper(BuildJob job)
        {
            if ((BuildTarget)job.Target != BuildTarget.StandaloneWindows64) return;
            string pathExt = Environment.GetEnvironmentVariable("PATHEXT");
            if (!(pathExt ?? string.Empty).Split(';').Contains(".COM", StringComparer.OrdinalIgnoreCase))
                Environment.SetEnvironmentVariable("PATHEXT", string.IsNullOrEmpty(pathExt) ? ".COM" : pathExt.TrimEnd(';') + ";.COM");
            string helper = WorkspacePath("chcp.com");
            if (File.Exists(helper))
            {
                if (FileHash(helper) != job.ChcpHash) throw new BuildFailedException("项目根 chcp.com 已被改变，拒绝覆盖。");
                return;
            }
            if (job.ChcpInitiallyExisted) throw new BuildFailedException("构建前已有的 chcp.com 已丢失，停止构建。");
            if (FileHash(job.SystemChcpPath) != job.ChcpHash) throw new BuildFailedException("系统 chcp.com 与已验证来源不一致。");
            // Bee 编译命令覆盖 PATH，但 cwd 是项目根；只有本事务新建的副本才允许结束时清理。
            File.Copy(job.SystemChcpPath, helper, false);
            job.ChcpCreated = true;
            WriteJob(job);
            if (FileHash(helper) != job.ChcpHash) throw new BuildFailedException("临时 chcp.com 与系统来源字节不一致。");
        }

        private static void CleanupCompilerHelper(BuildJob job)
        {
            if (!job.ChcpCreated) return;
            string helper = WorkspacePath("chcp.com");
            if (File.Exists(helper))
            {
                if (FileHash(helper) != job.ChcpHash)
                    throw new InvalidOperationException("临时 chcp.com 已被改变，拒绝自动删除，恢复已暂停。");
                File.Delete(helper);
            }
            job.ChcpCreated = false;
        }

        private static string FileHash(string path)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
            using (var stream = File.OpenRead(path))
                return Convert.ToBase64String(hash.ComputeHash(stream));
        }

        private static void CaptureNativePluginSettings(BuildJob job)
        {
            var target = (BuildTarget)job.Target;
            foreach (var importer in PluginImporter.GetAllImporters())
            {
                if (!importer.isNativePlugin || !importer.assetPath.StartsWith("Assets/Plugins/Streamline/", StringComparison.Ordinal) ||
                    !string.Equals(Path.GetExtension(importer.assetPath), ".dll", StringComparison.OrdinalIgnoreCase)) continue;
                job.NativePlugins.Add(new NativePluginBackup
                {
                    AssetPath = importer.assetPath, Compatible = importer.GetCompatibleWithPlatform(target),
                    ExcludedFromAny = importer.GetExcludeFromAnyPlatform(target), AnyPlatform = importer.GetCompatibleWithAnyPlatform()
                });
                BackupFile(job, importer.assetPath + ".meta");
            }
        }

        private static void ExcludeNativePlugins(BuildJob job)
        {
            var target = (BuildTarget)job.Target;
            foreach (var entry in job.NativePlugins)
            {
                if (entry.AnyPlatform ? entry.ExcludedFromAny : !entry.Compatible) continue;
                var importer = AssetImporter.GetAtPath(entry.AssetPath) as PluginImporter;
                if (importer == null) throw new BuildFailedException("原生插件导入器丢失：" + entry.AssetPath);
                // DLSS 体验后端只支持 Windows Editor；S1 Player 不复制或预加载其 Windows DLL。
                if (entry.AnyPlatform) importer.SetExcludeFromAnyPlatform(target, true);
                else importer.SetCompatibleWithPlatform(target, false);
                importer.SaveAndReimport();
            }
        }

        private static void RestoreNativePlugins(BuildJob job)
        {
            var target = (BuildTarget)job.Target;
            foreach (var entry in job.NativePlugins)
            {
                if (entry.AnyPlatform ? entry.ExcludedFromAny : !entry.Compatible) continue;
                var importer = AssetImporter.GetAtPath(entry.AssetPath) as PluginImporter;
                if (importer == null) throw new InvalidOperationException("恢复时原生插件导入器丢失：" + entry.AssetPath);
                if (entry.AnyPlatform) importer.SetExcludeFromAnyPlatform(target, entry.ExcludedFromAny);
                else importer.SetCompatibleWithPlatform(target, entry.Compatible);
                importer.SaveAndReimport();
            }
        }

        private static void Finish(BuildJob job)
        {
            // 恢复原目标平台可能再次序列化 PlayerSettings；结束前重新核对原始字节。
            RestorePlayerSettingsFile(job);
            RestoreRenderingAssets(job);
            string message = job.Succeeded && string.IsNullOrEmpty(job.Error)
                ? "S1离线构建成功，编辑器已恢复。输出：" + job.OutputRoot
                : "S1构建未完成，编辑器已恢复。" + job.Error;
            SessionState.EraseString(JobKey);
            SessionState.SetString(StatusKey, message);
            Debug.Log("[JinxCasinoBuild] " + message);
            // 备份保留在 Library，便于人工核对；不清理用户产物或失败构建目录。
        }

        private static void BackupTree(BuildJob job, string source, bool preserveRoot = false)
        {
            string absolute = WorkspacePath(source);
            string backup = WorkspacePath(job.BackupRoot + "/" + job.Backups.Count);
            bool existed = Directory.Exists(absolute);
            if (existed) CopyTree(absolute, backup);
            job.Backups.Add(new FileBackup { Source = absolute, Backup = backup, Existed = existed, IsDirectory = true, PreserveRoot = preserveRoot });
        }

        private static void BackupFile(BuildJob job, string source)
        {
            string absolute = WorkspacePath(source);
            string backup = WorkspacePath(job.BackupRoot + "/" + job.Backups.Count);
            bool existed = File.Exists(absolute);
            if (existed) { Directory.CreateDirectory(Path.GetDirectoryName(backup)); File.Copy(absolute, backup, true); }
            job.Backups.Add(new FileBackup { Source = absolute, Backup = backup, Existed = existed });
        }

        private static void CopyTree(string source, string destination)
        {
            string from = WorkspacePath(source);
            string to = WorkspacePath(destination);
            Directory.CreateDirectory(to);
            foreach (string path in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(to, Path.GetRelativePath(from, path)));
            foreach (string path in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(path, Path.Combine(to, Path.GetRelativePath(from, path)), true);
        }

        private static void DeleteTree(string path)
        {
            string absolute = WorkspacePath(path);
            if (Directory.Exists(absolute)) Directory.Delete(absolute, true);
        }

        private static void ClearDirectoryChildren(string path)
        {
            string absolute = WorkspacePath(path);
            Directory.CreateDirectory(absolute);
            foreach (string child in Directory.GetDirectories(absolute)) DeleteTree(child);
            foreach (string child in Directory.GetFiles(absolute)) File.Delete(WorkspacePath(child));
        }

        private static PlayerSettings GetPlayerSettingsObject()
        {
            var settings = Resources.FindObjectsOfTypeAll<PlayerSettings>();
            if (settings.Length != 1) throw new InvalidOperationException("无法唯一定位当前 PlayerSettings 对象，停止构建/恢复。");
            return settings[0];
        }

        private static void RestorePlayerSettingsFile(BuildJob job)
        {
            string source = WorkspacePath(ProjectSettingsPath);
            var backup = job.Backups.Single(entry => !entry.IsDirectory && entry.Source == source);
            if (!backup.Existed || !File.Exists(backup.Backup))
                throw new InvalidOperationException("原始 ProjectSettings 备份丢失，恢复已暂停。");
            var settings = GetPlayerSettingsObject();
            // 恢复完整内存对象及原始文件字节；已持久化对象不能交给 SaveToSerializedFileAndForget。
            // 不全局 SaveAssets，避免提交其它 dirty 资产；保留构建前的 dirty 状态。
            EditorJsonUtility.FromJsonOverwrite(job.PlayerSettingsJson, settings);
            EditorUtility.ClearDirty(settings);
            File.Copy(backup.Backup, source, true);
            if (!File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(backup.Backup)))
                throw new IOException("ProjectSettings 原始字节恢复校验失败。");
            if (job.PlayerSettingsWasDirty) EditorUtility.SetDirty(settings);
        }

        private static string WorkspacePath(string path)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string absolute = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
            if (!absolute.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("构建文件操作必须位于当前项目内：" + absolute);
            return absolute;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static BuildJob ReadJob()
        {
            string json = SessionState.GetString(JobKey, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<BuildJob>(json);
        }

        private static void WriteJob(BuildJob job) => SessionState.SetString(JobKey, JsonUtility.ToJson(job));
        private static void Save(BuildJob job, string status)
        {
            WriteJob(job);
            SessionState.SetString(StatusKey, status);
            Debug.Log("[JinxCasinoBuild] " + status);
        }

        [Serializable]
        private sealed class FileBackup
        {
            public string Source;
            public string Backup;
            public bool Existed;
            public bool IsDirectory;
            public bool PreserveRoot;
        }

        [Serializable]
        private sealed class NativePluginBackup
        {
            public string AssetPath;
            public bool Compatible, ExcludedFromAny, AnyPlatform;
        }

        [Serializable]
        private sealed class RenderingAssetBackup
        {
            public string AssetPath, Json;
            public bool WasDirty;
        }

        [Serializable]
        private sealed class BuildJob
        {
            public int Target, OriginalTarget, AndroidBackend, StandaloneBackend, AndroidMinimumSdk, AndroidArchitectures;
            public string Version, Phase, BackupRoot, OutputRoot, WorkRoot, PackageName, ConfigPath, AotReferencePath;
            public string CollectorJson, Company, Product, BundleVersion, AndroidIdentifier, StandaloneIdentifier, Error;
            public string AndroidBuildLocation, WindowsBuildLocation, Il2CppEnvironment;
            public string PlayerSettingsJson;
            public string ProcessPathExt, SystemChcpPath, ChcpHash;
            public bool CollectorWasDirty, Development, Debugging, Profiler, DeepProfiling, ScriptsOnly;
            public bool AndroidExport, AppBundle, CustomKeystore, CreateSolution, Succeeded, SwitchRequested;
            public bool PlayerSettingsWasDirty;
            public bool ChcpInitiallyExisted, ChcpCreated;
            public List<FileBackup> Backups = new List<FileBackup>();
            public List<NativePluginBackup> NativePlugins = new List<NativePluginBackup>();
            public List<RenderingAssetBackup> RenderingAssets = new List<RenderingAssetBackup>();
        }
    }
}
