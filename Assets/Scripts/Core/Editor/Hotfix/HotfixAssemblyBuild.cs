using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Core.Runtime;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.Hotfix
{
    internal static class HotfixAssemblyBuild
    {
        internal static string[] ReadAotAssemblies()
        {
            string content = File.ReadAllText("Assets/HybridCLRGenerate/AOTGenericReferences.cs");
            Match block = Regex.Match(content, @"PatchedAOTAssemblyList\s*=\s*new\s+List<string>\s*\{(?<body>[\s\S]*?)\}");
            if (!block.Success)
            {
                throw new InvalidOperationException("无法解析 PatchedAOTAssemblyList。");
            }

            return Regex.Matches(block.Groups["body"].Value, "\"([^\"]+\\.dll)\"").Cast<Match>()
                .Select(match => match.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static string[] ReadHotfixAssemblies(string gameId, BuildTarget target)
        {
            string[] required = ReleaseProfiles.Get(gameId).GetAssemblies(target);
            string[] files = Directory.GetFiles("Assets/Scripts/Hotfix", "*.asmdef", SearchOption.AllDirectories);
            var available = new HashSet<string>(files.Select(file =>
                HotfixAssemblyDefinitionFilter.TryGetDllNameFromFile(file, out string name) ? name : null)
                .Where(name => name != null), StringComparer.OrdinalIgnoreCase);
            string[] missing = required.Where(name => !available.Contains(name)).ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException("发布配置缺少必需的运行时 asmdef：" + string.Join(", ", missing));
            }
            return required;
        }

        internal static void CaptureReleaseAssemblies(HotfixConfig config, BuildTarget target, bool includeAot)
        {
            if (config.GameId == "hub")
            {
                return;
            }
            Capture("HybridCLRData/HotUpdateDlls/" + target, config.HotfixSourcePath, config.HotfixAssemblies);
            if (includeAot)
            {
                Capture("HybridCLRData/AssembliesPostIl2CppStrip/" + target, config.AotSourcePath, config.AotAssemblies);
            }

            static void Capture(string source, string destination, string[] names)
            {
                GitHubPagesDeployment.EnsureNoLinks(destination);
                foreach (string name in names)
                {
                    string from = Path.Combine(source, name);
                    string to = Path.Combine(destination, name);
                    GitHubPagesDeployment.EnsureNoLinks(from);
                    GitHubPagesDeployment.EnsureNoLinks(to);
                    if (!File.Exists(from))
                    {
                        throw new FileNotFoundException("缺少本次编译的程序集。", from);
                    }
                }
                Directory.CreateDirectory(destination);
                foreach (string name in names)
                {
                    File.Copy(Path.Combine(source, name), Path.Combine(destination, name), true);
                }
            }
        }

        internal static void CopyAssemblies(string source, string target, IEnumerable<string> files)
        {
            string assetsRoot = Path.GetFullPath(Application.dataPath).Replace('\\', '/').TrimEnd('/') + "/";
            string fullTarget = Path.GetFullPath(target).Replace('\\', '/');
            if (!fullTarget.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("程序集资源目录必须位于当前项目 Assets 内。");
            }
            target = "Assets/" + fullTarget.Substring(assetsRoot.Length);
            string[] names = files.Where(item => !string.IsNullOrWhiteSpace(item)).ToArray();
            foreach (string name in names)
            {
                if (Path.GetFileName(name) != name || !name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("程序集名称必须是 DLL 文件名。");
                }
                if (!File.Exists(Path.Combine(source, name)))
                {
                    throw new FileNotFoundException("缺少待同步程序集。", Path.Combine(source, name));
                }
                GitHubPagesDeployment.EnsureNoLinks(Path.Combine(source, name));
                GitHubPagesDeployment.EnsureNoLinks(Path.Combine(target, name + ".bytes"));
                string metaPath = Path.Combine(target, name + ".bytes.meta");
                GitHubPagesDeployment.EnsureNoLinks(metaPath);
                if (File.Exists(metaPath))
                {
                    ValidateAssemblyMeta(metaPath);
                }
            }

            PruneAssemblies(target, names);
            foreach (string name in names)
            {
                File.Copy(Path.Combine(source, name), Path.Combine(target, name + ".bytes"), true);
                EnsureAssemblyMeta(Path.Combine(target, name + ".bytes"));
            }
        }

        private static void EnsureAssemblyMeta(string assetPath)
        {
            string metaPath = assetPath + ".meta";
            GitHubPagesDeployment.EnsureNoLinks(metaPath);
            if (File.Exists(metaPath))
            {
                return;
            }
            string guid = GetAssemblyAssetGuid(assetPath.Replace('\\', '/'));
            File.WriteAllText(metaPath, "fileFormatVersion: 2\nguid: " + guid +
                "\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n", new UTF8Encoding(false));
        }

        private static void ValidateAssemblyMeta(string metaPath)
        {
            MatchCollection matches = Regex.Matches(File.ReadAllText(metaPath), @"(?m)^guid: ([0-9a-fA-F]{32})\r?$");
            if (matches.Count != 1 || matches[0].Groups[1].Value == "00000000000000000000000000000000")
            {
                throw new InvalidOperationException("程序集资源 meta 的 GUID 无效：" + metaPath);
            }
        }

        private static string GetAssemblyAssetGuid(string projectPath)
        {
            // 保留已有资源身份；新程序集按规范路径生成，不受游戏切换或构建机器影响。
            string existing = projectPath switch
            {
                "Assets/LoadResources/Codes/Aot/Core.Runtime.dll.bytes" => "340bbc896af0c354b98286a30214a5c4",
                "Assets/LoadResources/Codes/Aot/DOTween.dll.bytes" => "475eed58680eb4a448ef961644827345",
                "Assets/LoadResources/Codes/Aot/mscorlib.dll.bytes" => "695a0b06149c75b4990e37e18fd532f1",
                "Assets/LoadResources/Codes/Aot/Newtonsoft.Json.dll.bytes" => "d5484fc7ba8171744896906671488ae9",
                "Assets/LoadResources/Codes/Aot/SleepyStudios.LoopScroll.dll.bytes" => "268331a1bbfa4ab48910c09fbcb03ecf",
                "Assets/LoadResources/Codes/Aot/System.Core.dll.bytes" => "c3b2f9c099eb08f42bb3ed667fb10187",
                "Assets/LoadResources/Codes/Aot/UniTask.dll.bytes" => "f4df0f1cc11434048831bdb942422954",
                "Assets/LoadResources/Codes/Aot/Unity.InputSystem.dll.bytes" => "7faae0fd11affc144a4fec99949c8fe9",
                "Assets/LoadResources/Codes/Aot/Unity.RenderPipelines.Core.Runtime.dll.bytes" => "51a2d18419587b04281928b9cb04a518",
                "Assets/LoadResources/Codes/Aot/UnityEngine.CoreModule.dll.bytes" => "b045459075cc42442ae737f38bf9444a",
                "Assets/LoadResources/Codes/Aot/UnityEngine.JSONSerializeModule.dll.bytes" => "4b14410dd9b01474c84df6205a16455b",
                "Assets/LoadResources/Codes/Hotfix/Hotfix.dll.bytes" => "0188873585e7e91418fb3503232199ec",
                "Assets/LoadResources/Codes/Hotfix/Hotfix.KagitKart.dll.bytes" => "4868a2bd3af0ce04d8633eaa060b5b1b",
                _ => null
            };
            if (existing != null)
            {
                return existing;
            }
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes("SleepyDemos.GeneratedAssemblyAsset:v1\n" + projectPath));
            return BitConverter.ToString(hash, 0, 16).Replace("-", string.Empty).ToLowerInvariant();
        }

        internal static void PruneAssemblies(string target, string[] names)
        {
            Directory.CreateDirectory(target);
            GitHubPagesDeployment.EnsureNoLinks(target);
            foreach (string existing in Directory.GetFiles(target, "*.dll.bytes"))
            {
                if (!names.Contains(Path.GetFileName(existing).Substring(0, Path.GetFileName(existing).Length - ".bytes".Length), StringComparer.OrdinalIgnoreCase))
                {
                    GitHubPagesDeployment.EnsureNoLinks(existing);
                    if (!AssetDatabase.DeleteAsset(existing.Replace('\\', '/')))
                    {
                        throw new InvalidOperationException("无法删除过时的程序集资源：" + Path.GetFileName(existing));
                    }
                }
            }
        }

        internal static void StripAotAssemblies(HotfixConfig config)
        {
            string[] files = Directory.GetFiles(config.AotSourcePath, "*.dll");
            if (files.Length == 0)
            {
                throw new InvalidOperationException("AOT 源目录没有程序集。");
            }

            Directory.CreateDirectory(config.AotStrippedSourcePath);
            foreach (string source in files)
            {
                HybridCLR.Editor.AOT.AOTAssemblyMetadataStripper.Strip(source,
                    Path.Combine(config.AotStrippedSourcePath, Path.GetFileName(source)));
            }
        }

        internal static void Sync(HotfixConfig config, bool stripped)
        {
            CopyAssemblies(stripped ? config.AotStrippedSourcePath : config.AotSourcePath, config.AotTargetPath, config.AotAssemblies);
            CopyAssemblies(config.HotfixSourcePath, config.HotfixTargetPath, config.HotfixAssemblies);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
