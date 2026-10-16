using System;
using System.IO;
using System.Linq;
using Core.Runtime;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace Core.Editor.Hotfix
{
    /// 编辑器与 CI 共用的独立游戏发布白名单，不在 Core 引用业务类型。
    public static class ReleaseProfiles
    {
        public const string Path = "Assets/Settings/ReleaseProfiles.json";

        [Serializable]
        public sealed class Profile
        {
            public string GameId;
            public string Group;
            public string StartupScene;
            public string ApplicationId;
            public string[] Assemblies;
            public string[] PcAssemblies = Array.Empty<string>();

            internal string[] GetAssemblies(BuildTarget target) => target == BuildTarget.StandaloneWindows64
                ? Assemblies.Concat(PcAssemblies ?? Array.Empty<string>()).ToArray() : Assemblies;
        }

        [Serializable]
        private sealed class Catalog
        {
            public Profile[] Profiles;
        }

        /// <summary>读取正式发布目标；空值使用 Hub，未知标识直接拒绝。</summary>
        /// <param name="gameId">ReleaseProfiles.json 中的稳定游戏标识。</param>
        /// <returns>该目标的完整发布配置。</returns>
        public static Profile Get(string gameId)
        {
            gameId = string.IsNullOrWhiteSpace(gameId) ? "hub" : gameId;
            var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path));
            return catalog.Profiles.SingleOrDefault(item => item.GameId == gameId)
                ?? throw new ArgumentException("未登记的发布目标：" + gameId, nameof(gameId));
        }

        internal static Profile Current => Get(AssetDatabase.LoadAssetAtPath<HotfixConfig>(
            "Assets/LoadResources/Config/HotfixConfig.asset")?.GameId);

        internal static Profile[] All => JsonUtility.FromJson<Catalog>(File.ReadAllText(Path)).Profiles;

        internal static void Apply(HotfixConfig config, BuildTarget target, bool tapTap)
        {
            var profile = Get(config.GameId);
            if (tapTap && (target != BuildTarget.WebGL || profile.GameId == "hub"))
            {
                throw new InvalidOperationException("TapTap 发布必须选择 WebGL 与一个独立游戏。");
            }
            config.StartupScene = profile.StartupScene;
            config.HotfixAssemblies = HotfixAssemblyBuild.ReadHotfixAssemblies(profile.GameId, target);
            var namedTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            PlayerSettings.SetApplicationIdentifier(namedTarget, profile.ApplicationId);
            PlayerSettings.productName = profile.GameId == "hub" ? "SleepyDemos" : "SleepyDemos-" + profile.GameId;
            var symbols = PlayerSettings.GetScriptingDefineSymbols(namedTarget).Split(';')
                .Where(symbol => symbol != "SLEEPY_RELEASE" && symbol != "SLEEPY_TAPTAP" &&
                    !symbol.StartsWith("SLEEPY_DEMO_", StringComparison.Ordinal)).ToList();
            symbols.Add("SLEEPY_RELEASE");
            if (profile.GameId == "hub")
            {
                symbols.AddRange(All.Where(item => item.GameId != "hub").Select(item => "SLEEPY_DEMO_" + item.GameId.ToUpperInvariant()));
            }
            else
            {
                symbols.Add("SLEEPY_DEMO_" + profile.GameId.ToUpperInvariant());
            }
            if (tapTap)
            {
                symbols.Add("SLEEPY_TAPTAP");
            }
            PlayerSettings.SetScriptingDefineSymbols(namedTarget, string.Join(";", symbols.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct()));
            var hybrid = HybridCLR.Editor.Settings.HybridCLRSettings.Instance;
            hybrid.hotUpdateAssemblyDefinitions = Directory.GetFiles("Assets/Scripts/Hotfix", "*.asmdef", SearchOption.AllDirectories)
                .Where(path => HotfixAssemblyDefinitionFilter.TryGetDllNameFromFile(path, out string name) && config.HotfixAssemblies.Contains(name))
                .Select(AssetDatabase.LoadAssetAtPath<UnityEditorInternal.AssemblyDefinitionAsset>).ToArray();
            hybrid.hotUpdateAssemblies = Array.Empty<string>();
            HybridCLR.Editor.Settings.HybridCLRSettings.Save();
            EditorUtility.SetDirty(config);
            ConfigureCollectors(config, target);
        }

        internal static bool Includes(string path, Profile profile, BuildTarget target)
        {
            path = path.Replace('\\', '/');
            if (target == BuildTarget.WebGL && path.StartsWith("Assets/LoadResources/Demos/kagit_kart/Prefabs/UI/", StringComparison.Ordinal) &&
                System.IO.Path.GetFileName(path) is "KagitKartPlayModeView.prefab" or "KagitKartOnlineView.prefab" or
                    "KagitKartCreateRoomView.prefab" or "KagitKartJoinRoomView.prefab" or "KagitKartRoomView.prefab" or "KagitKartReconnectView.prefab")
            {
                return false;
            }
            const string demoScripts = "Assets/Scripts/Hotfix/Demos/";
            if (path.StartsWith(demoScripts, StringComparison.Ordinal))
            {
                string name = path.Substring(demoScripts.Length).Split('/')[0];
                return profile.GetAssemblies(target).Contains("Hotfix." + name + ".dll");
            }
            const string demos = "Assets/LoadResources/Demos/";
            if (path.StartsWith(demos, StringComparison.Ordinal))
            {
                string id = path.Substring(demos.Length).Split('/')[0];
                return id != "dlss" && (profile.GameId == "hub" || id == profile.GameId);
            }
            if (path.StartsWith("Assets/LoadResources/Diagnostics/", StringComparison.Ordinal) ||
                path.StartsWith("Assets/Scripts/Hotfix/Module/Diagnostics/", StringComparison.Ordinal))
            {
                return profile.GameId == "hub" && target == BuildTarget.StandaloneWindows64;
            }
            if (path.StartsWith("Assets/LoadResources/Prefabs/Networking/", StringComparison.Ordinal))
            {
                return target != BuildTarget.WebGL && (profile.GameId == "hub" || profile.GameId == "kagit_kart");
            }
            if (path.StartsWith("Assets/LoadResources/Codes/Hotfix/", StringComparison.Ordinal))
            {
                return profile.GetAssemblies(target).Contains(System.IO.Path.GetFileName(path).Replace(".bytes", string.Empty));
            }
            return true;
        }

        internal static void ConfigureCollectors(HotfixConfig config, BuildTarget target)
        {
            // YooAsset 缓存规则实例；同一 Editor 连续构建不同游戏时必须显式刷新。
            ((ReleaseAssetFilter)BundleCollectorSettingData.GetAssetFilterRuleInstance(nameof(ReleaseAssetFilter))).Configure(config, target);
            var setting = BundleCollectorSettingData.Setting;
            foreach (var package in setting.Packages)
            {
                foreach (var collector in package.Groups.SelectMany(item => item.Collectors))
                {
                    collector.FilterRuleName = nameof(ReleaseAssetFilter);
                    if (collector.CollectPath == "Assets/LoadResources/Codes")
                    {
                        collector.PackRuleName = "PackSeparately";
                    }
                    else if (collector.CollectPath == "Assets/LoadResources/Demos")
                    {
                        collector.PackRuleName = nameof(ReleaseDemoPackRule);
                    }
                }
            }
            EditorUtility.SetDirty(setting);
            AssetDatabase.SaveAssets();
        }

        internal static void ValidateDependencies(HotfixConfig config, BuildTarget target)
        {
            var profile = Get(config.GameId);
            var roots = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/LoadResources/", StringComparison.Ordinal)
                && !AssetDatabase.IsValidFolder(path) && Includes(path, profile, target)).ToArray();
            foreach (string path in AssetDatabase.GetDependencies(roots, true))
            {
                if (!Includes(path, profile, target))
                {
                    throw new InvalidOperationException("发布目标依赖了未选择的内容：" + path);
                }
            }
        }

        internal static BuildResult BuildResources(HotfixConfig config, BuildTarget target, string version,
            string outputRoot, EBundledCopyOption copyOption)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(version ?? string.Empty, @"\A[A-Za-z0-9_-][A-Za-z0-9_.-]*\z"))
            {
                throw new InvalidOperationException("资源版本只能使用字母、数字、下划线、短横线与非首位的点。");
            }
            ConfigureCollectors(config, target);
            ValidateDependencies(config, target);
            string streaming = BundleBuilderHelper.GetStreamingAssetsRoot();
            GitHubPagesDeployment.EnsureNoLinks(streaming);
            GitHubPagesDeployment.EnsureNoLinks(outputRoot);
            var parameters = new ScriptableBuildParameters
            {
                BuildOutputRoot = outputRoot,
                BundledFileRoot = streaming,
                BuildPipeline = nameof(YooAsset.Editor.ScriptableBuildPipeline),
                BuildBundleType = (int)YooAsset.EBundleType.AssetBundle,
                BuildTarget = target,
                PackageName = config.PackageName,
                PackageVersion = version,
                VerifyBuildingResult = true,
                FileNameStyle = EFileNameStyle.HashName,
                CompressOption = ECompressOption.LZ4,
                BuiltinShadersBundleName = DefaultBundlePackRule.CreateShadersPackRuleResult()
                    .GetBundleName(config.PackageName, BundleCollectorSettingData.Setting.UniqueBundleName),
                BundledCopyOption = copyOption
            };
            var result = new YooAsset.Editor.ScriptableBuildPipeline().Run(parameters, true);
            if (!result.Success)
            {
                throw new InvalidOperationException("YooAsset 构建失败：" + result.FailedTask + " " + result.ErrorInfo);
            }
            return result;
        }
    }

    [DisplayName("发布目标 Demo 分包")]
    public sealed class ReleaseDemoPackRule : IBundlePackRule
    {
        private const string KartRoot = "Assets/LoadResources/Demos/kagit_kart/";
        private static readonly string[] ChapterFiles = { "Data/Tracks/", "Data/AiPlans/", "Data/Worlds/", "Prefabs/Tracks/" };
        private static readonly string[] ChapterDirectories = { "Data/TrackMeshes/", "Art/Materials/Tracks/", "Art/Textures/Tracks/" };
        private readonly IBundlePackRule separately = new PackSeparately();
        private readonly IBundlePackRule directory = new PackDirectory();

        /// <summary>同章资源共包，音乐逐 Clip；其它 Demo 保持原目录打包规则。</summary>
        /// <param name="data">YooAsset 提供的资源与收集器信息。</param>
        /// <returns>不改变资源地址的 Bundle 分组。</returns>
        public BundlePackRuleResult GetPackRuleResult(BundlePackRuleData data)
        {
            string path = data.AssetPath.Replace('\\', '/');
            if (!path.StartsWith(KartRoot, StringComparison.Ordinal))
            {
                return directory.GetPackRuleResult(data);
            }
            string relative = path.Substring(KartRoot.Length);
            if (relative.StartsWith("Audio/SFX/Music/", StringComparison.Ordinal))
            {
                return separately.GetPackRuleResult(data);
            }
            foreach (string prefix in ChapterFiles)
            {
                if (relative.StartsWith(prefix, StringComparison.Ordinal))
                {
                    return Chapter(System.IO.Path.GetFileNameWithoutExtension(path));
                }
            }
            foreach (string prefix in ChapterDirectories)
            {
                if (relative.StartsWith(prefix, StringComparison.Ordinal))
                {
                    string nested = relative.Substring(prefix.Length);
                    int separator = nested.IndexOf('/');
                    if (separator <= 0)
                    {
                        throw new InvalidOperationException("章节资源必须放在章节子目录：" + path);
                    }
                    return Chapter(nested.Substring(0, separator));
                }
            }
            return directory.GetPackRuleResult(data);
        }

        private static BundlePackRuleResult Chapter(string name) => new(KartRoot + "Chapters/" + name,
            DefaultBundlePackRule.AssetBundleFileExtension);
    }

    [DisplayName("发布目标资源")]
    public sealed class ReleaseAssetFilter : IAssetFilterRule
    {
        private HotfixConfig config;
        private ReleaseProfiles.Profile profile;
        private BuildTarget target;

        public string FindAssetType => EAssetFilterType.All.ToString();

        internal void Configure(HotfixConfig source, BuildTarget buildTarget)
        {
            config = source != null ? source : throw new InvalidOperationException("缺少资源发布配置。");
            profile = ReleaseProfiles.Get(config.GameId);
            target = buildTarget;
        }

        /// <summary>按当前正式发布配置过滤资源，不改变资源地址。</summary>
        /// <param name="data">YooAsset 提供的候选资源。</param>
        /// <returns>该资源是否属于本次发布。</returns>
        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            if (config == null)
            {
                Configure(AssetDatabase.LoadAssetAtPath<HotfixConfig>("Assets/LoadResources/Config/HotfixConfig.asset"),
                    EditorUserBuildSettings.activeBuildTarget);
            }
            else if (profile.GameId != (string.IsNullOrWhiteSpace(config.GameId) ? "hub" : config.GameId) ||
                target != EditorUserBuildSettings.activeBuildTarget)
            {
                Configure(config, EditorUserBuildSettings.activeBuildTarget);
            }
            return ReleaseProfiles.Includes(data.AssetPath, profile, target);
        }
    }
}
