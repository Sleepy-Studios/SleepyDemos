using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Core.Editor.Hotfix;
using Core.Runtime;
using Hotfix.KagitKart;
using HybridCLR.Editor.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Hotfix.Editor
{
    /// 纸艺卡丁车独立服务器构建入口；使用正式宿主与纯规则数据，不经过客户端热更启动。
    public static class KagitKartServerBuild
    {
        private const string ServerScene = "Assets/Scenes/KagitKartServer.unity";
        private const string HostPrefab = "Assets/LoadResources/Prefabs/Networking/SessionHost.prefab";
        private const string RulesSource = "Assets/LoadResources/Demos/kagit_kart/Data";
        private const string HotfixConfigPath = "Assets/LoadResources/Config/HotfixConfig.asset";
        private static string PendingRestoreKey => typeof(KagitKartServerBuild).FullName + ".HotfixRestore." + Application.dataPath;

        [InitializeOnLoadMethod]
        private static void RestoreHotfixAfterReload()
        {
            if (!string.IsNullOrEmpty(SessionState.GetString(PendingRestoreKey, string.Empty)))
            {
                // 平台切换的 DomainReload 发生在菜单返回之后；等窗口 OnEnable 完成再还原一次。
                EditorApplication.delayCall += RestorePendingHotfixConfig;
            }
        }

        private static void RestorePendingHotfixConfig()
        {
            string json = SessionState.GetString(PendingRestoreKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return;
            }
            try
            {
                var snapshot = JsonUtility.FromJson<HotfixConfigSnapshot>(json);
                RestoreHotfixConfig(snapshot);
                SynchronizeHotfixWindowTarget((BuildTarget)snapshot.activeBuildTarget);
                if (EditorUserBuildSettings.activeBuildTarget != (BuildTarget)snapshot.activeBuildTarget)
                {
                    throw new InvalidOperationException("服务器构建后的活动平台尚未恢复。");
                }
                Debug.Log("服务器构建后的 DomainReload 配置已恢复：HotfixConfig SHA256=" + Hash(Convert.FromBase64String(snapshot.fileBase64)));
            }
            finally
            {
                // 仅属于刚结束的构建，不允许未来重载覆盖用户之后的修改。
                SessionState.EraseString(PendingRestoreKey);
            }
        }

        /// 构建 Windows x64 Dedicated Mono 服务器，结束后恢复客户端构建设置。
        [MenuItem("Tools/KagitKart/Server/Build Windows")]
        public static void BuildWindows()
        {
            Build(BuildTarget.StandaloneWindows64, "win-x64", "KagitKartServer.exe");
        }

        /// 构建 Linux x64 Dedicated Mono 服务器，结束后恢复客户端构建设置。
        [MenuItem("Tools/KagitKart/Server/Build Linux")]
        public static void BuildLinux()
        {
            Build(BuildTarget.StandaloneLinux64, "linux-x64", "KagitKartServer");
        }

        /// 原生创建缺失的服务器入口场景，不改当前场景或复制启动中的网络宿主。
        [MenuItem("Tools/KagitKart/Server/Create Server Scene")]
        public static void CreateServerScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ServerScene) != null)
            {
                return;
            }
            var host = AssetDatabase.LoadAssetAtPath<GameObject>(HostPrefab);
            if (host == null)
            {
                throw new InvalidOperationException("正式网络宿主不存在：" + HostPrefab);
            }
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("KagitKartDedicatedServer");
                SceneManager.MoveGameObjectToScene(root, scene);
                var server = root.AddComponent<KagitKartDedicatedServer>();
                var serialized = new SerializedObject(server);
                serialized.FindProperty("networkHost").objectReferenceValue = host;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene, ServerScene))
                {
                    throw new InvalidOperationException("独立服务器场景保存失败。");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid() && previous.isLoaded)
                {
                    SceneManager.SetActiveScene(previous);
                }
            }
        }

        private static void Build(BuildTarget target, string platform, string executable)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            {
                throw new InvalidOperationException("请等待运行或其它构建结束后再构建服务器。");
            }
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target))
            {
                throw new InvalidOperationException("当前 Editor 尚未安装目标平台支持模块。");
            }
            CreateServerScene();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string output = Path.GetFullPath(Path.Combine(projectRoot, "../SleepyDemos-Server/artifacts/game-server", platform));
            string[] ruleFiles = new[] { "SourceCatalog.json" }
                .Concat(Directory.GetFiles(Path.Combine(RulesSource, "Tracks"), "*.json").Select(path => "Tracks/" + Path.GetFileName(path)))
                .Concat(Directory.GetFiles(Path.Combine(RulesSource, "AiPlans"), "*.json").Select(path => "AiPlans/" + Path.GetFileName(path)))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            var manifest = new ServerManifest
            {
                platform = platform,
                protocolVersion = KagitKartOnlineSettings.ProtocolVersion,
                contentVersion = KagitKartOnlineSettings.ContentVersion,
                gitCommit = ReadGitCommit(projectRoot),
                builtAtUtc = DateTime.UtcNow.ToString("O"),
                files = ruleFiles.Select(path => new RuleFile { path = path, sha256 = Hash(File.ReadAllBytes(Path.Combine(RulesSource, path))) }).ToArray()
            };
            manifest.rulesHash = Hash(Encoding.UTF8.GetBytes(string.Join("\n", manifest.files.Select(file => file.path + ":" + file.sha256))));
            string hybridSettingsPath = Path.Combine(projectRoot, "ProjectSettings/HybridCLRSettings.asset");
            string playerSettingsPath = Path.Combine(projectRoot, "ProjectSettings/ProjectSettings.asset");
            byte[] previousHybridSettings = File.ReadAllBytes(hybridSettingsPath);
            byte[] previousPlayerSettings = File.ReadAllBytes(playerSettingsPath);
            var playerSettings = Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings");
            if (playerSettings == null)
            {
                throw new InvalidOperationException("无法保存当前 PlayerSettings 内存状态。");
            }
            string previousPlayerJson = EditorJsonUtility.ToJson(playerSettings);
            bool previousEnabled = HybridCLRSettings.LoadOrCreate().enable;
            StandaloneBuildSubtarget previousSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;
            var hotfixConfig = AssetDatabase.LoadAssetAtPath<HotfixConfig>(HotfixConfigPath);
            if (hotfixConfig == null)
            {
                throw new InvalidOperationException("无法保存当前 HotfixConfig 状态：" + HotfixConfigPath);
            }
            var previousHotfix = new HotfixConfigSnapshot
            {
                fileBase64 = Convert.ToBase64String(File.ReadAllBytes(HotfixConfigPath)),
                nativeJson = EditorJsonUtility.ToJson(hotfixConfig),
                dirty = EditorUtility.IsDirty(hotfixConfig),
                activeBuildTarget = (int)previousTarget
            };
            try
            {
                // 禁用过滤 Hotfix DLL 的 HybridCLR 构建处理器，服务器直接使用完整 Mono 程序集。
                HybridCLRSettings.Instance.enable = false;
                HybridCLRSettings.Save();
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.Mono2x);
                EditorUserBuildSettings.standaloneBuildSubtarget = StandaloneBuildSubtarget.Server;
                Directory.CreateDirectory(output);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ServerScene }, target = target, subtarget = (int)StandaloneBuildSubtarget.Server,
                    locationPathName = Path.Combine(output, executable), options = BuildOptions.None
                });
                if (report.summary.result != BuildResult.Succeeded)
                {
                    throw new InvalidOperationException("独立服务器构建失败：" + report.summary.result);
                }
                string data = Path.Combine(output, "KagitKartServer_Data");
                if (!File.Exists(Path.Combine(data, "Managed/Hotfix.dll")))
                {
                    throw new InvalidOperationException("服务器缺少直接编入的 Hotfix.dll。");
                }
                string streaming = Path.GetFullPath(Path.Combine(data, "StreamingAssets"));
                if (!streaming.StartsWith(output + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("服务器生成资源目录越过输出边界。");
                }
                // Unity 会复制客户端 StreamingAssets；这里只清理本次服务器产物中的生成目录。
                if (Directory.Exists(streaming))
                {
                    Directory.Delete(streaming, true);
                }
                string rules = Path.Combine(streaming, "KagitKartRules");
                Directory.CreateDirectory(rules);
                foreach (var file in manifest.files)
                {
                    string destination = Path.Combine(rules, file.path);
                    Directory.CreateDirectory(Path.GetDirectoryName(destination));
                    File.Copy(Path.Combine(RulesSource, file.path), destination, true);
                    if (Hash(File.ReadAllBytes(destination)) != file.sha256)
                    {
                        throw new InvalidOperationException("规则文件在构建期间发生变化：" + file.path);
                    }
                }
                File.WriteAllText(Path.Combine(output, "server-manifest.json"), JsonUtility.ToJson(manifest, true), new UTF8Encoding(false));
                Debug.Log("独立服务器构建完成：" + Path.Combine(output, executable));
            }
            finally
            {
                try
                {
                    // 同平台构建也可能因 Server 子目标切换触发重载；无重载时由本次 delayCall 收口。
                    SessionState.SetString(PendingRestoreKey, JsonUtility.ToJson(previousHotfix));
                    EditorApplication.delayCall += RestorePendingHotfixConfig;
                    if (EditorUserBuildSettings.activeBuildTarget != previousTarget)
                    {
                        // 先保存跨重载快照，目标切换即使提前失败，其余设置仍必须走下面的 finally。
                        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(previousTarget), previousTarget))
                        {
                            throw new InvalidOperationException("服务器构建后无法恢复活动平台：" + previousTarget);
                        }
                    }
                }
                finally
                {
                    try
                    {
                        EditorUserBuildSettings.standaloneBuildSubtarget = previousSubtarget;
                        // 构建可能重新创建设置对象；恢复当前单例，且不会留下原先不存在的 Server backend 键。
                        EditorJsonUtility.FromJsonOverwrite(previousPlayerJson,
                            Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings"));
                    }
                    finally
                    {
                        // 保留本次构建前完整字节和未保存的资产内存状态，不从 Git 或旧单例反推配置。
                        File.WriteAllBytes(playerSettingsPath, previousPlayerSettings);
                        File.WriteAllBytes(hybridSettingsPath, previousHybridSettings);
                        HybridCLRSettings.LoadOrCreate();
                        RestoreHotfixConfig(previousHotfix);
                        SynchronizeHotfixWindowTarget(previousTarget);
                    }
                }
                if (EditorUserBuildSettings.activeBuildTarget != previousTarget ||
                    EditorUserBuildSettings.standaloneBuildSubtarget != previousSubtarget ||
                    HybridCLRSettings.Instance.enable != previousEnabled ||
                    EditorJsonUtility.ToJson(Unsupported.GetSerializedAssetInterfaceSingleton("PlayerSettings")) != previousPlayerJson ||
                    !File.ReadAllBytes(playerSettingsPath).SequenceEqual(previousPlayerSettings) ||
                    !File.ReadAllBytes(hybridSettingsPath).SequenceEqual(previousHybridSettings))
                {
                    throw new InvalidOperationException("服务器构建设置未完整恢复，请停止后续构建并检查 ProjectSettings。");
                }
                Debug.Log("服务器构建设置已恢复：HybridCLR=" + previousEnabled +
                    "，ActiveBuildTarget=" + previousTarget + "，PlayerSettings SHA256=" + Hash(previousPlayerSettings) +
                    "，HybridCLRSettings SHA256=" + Hash(previousHybridSettings) + "，HotfixConfig SHA256=" + Hash(Convert.FromBase64String(previousHotfix.fileBase64)));
            }
        }

        private static void RestoreHotfixConfig(HotfixConfigSnapshot snapshot)
        {
            byte[] bytes = Convert.FromBase64String(snapshot.fileBase64);
            File.WriteAllBytes(HotfixConfigPath, bytes);
            AssetDatabase.ImportAsset(HotfixConfigPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var current = AssetDatabase.LoadAssetAtPath<HotfixConfig>(HotfixConfigPath);
            if (current == null)
            {
                throw new InvalidOperationException("无法恢复当前 HotfixConfig 资产对象。");
            }
            EditorJsonUtility.FromJsonOverwrite(snapshot.nativeJson, current);
            if (snapshot.dirty)
            {
                EditorUtility.SetDirty(current);
            }
            else
            {
                EditorUtility.ClearDirty(current);
            }
            if (EditorJsonUtility.ToJson(current) != snapshot.nativeJson || EditorUtility.IsDirty(current) != snapshot.dirty
                || !File.ReadAllBytes(HotfixConfigPath).SequenceEqual(bytes))
            {
                throw new InvalidOperationException("HotfixConfig 的文件或内存状态未完整恢复。");
            }
        }

        private static void SynchronizeHotfixWindowTarget(BuildTarget target)
        {
            FieldInfo field = typeof(HotfixBuildWindow).GetField("buildTarget", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
            {
                throw new MissingFieldException(typeof(HotfixBuildWindow).FullName, "buildTarget");
            }
            foreach (var window in Resources.FindObjectsOfTypeAll<HotfixBuildWindow>())
            {
                // 避免窗口下次 OnGUI 因缓存平台不一致，再次把客户端路径改成默认值。
                field.SetValue(window, target);
            }
        }

        private static string ReadGitCommit(string projectRoot)
        {
            using var process = Process.Start(new ProcessStartInfo("git", "rev-parse HEAD")
            {
                WorkingDirectory = projectRoot, UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true
            });
            string commit = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode != 0 || commit.Length != 40)
            {
                throw new InvalidOperationException("无法读取服务器构建对应的 Git commit。");
            }
            return commit;
        }

        private static string Hash(byte[] bytes)
        {
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable]
        private sealed class HotfixConfigSnapshot
        {
            public string fileBase64, nativeJson;
            public bool dirty;
            public int activeBuildTarget;
        }

        [Serializable]
        private sealed class ServerManifest
        {
            public string platform, contentVersion, gitCommit, builtAtUtc, rulesHash;
            public int protocolVersion;
            public RuleFile[] files;
        }

        [Serializable]
        private sealed class RuleFile
        {
            public string path, sha256;
        }
    }
}
