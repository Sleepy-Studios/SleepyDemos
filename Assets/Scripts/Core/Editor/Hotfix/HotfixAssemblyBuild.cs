using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        internal static string[] ReadHotfixAssemblies()
        {
            string[] files = Directory.GetFiles("Assets/Scripts/Hotfix", "*.asmdef", SearchOption.AllDirectories);
            return files.Select(file => HotfixAssemblyDefinitionFilter.TryGetDllNameFromFile(file, out string name) ? name : null)
                .Where(name => name != null).Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal static void CopyAssemblies(string source, string target, IEnumerable<string> files)
        {
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
            }

            PruneAssemblies(target, names);
            foreach (string name in names)
            {
                File.Copy(Path.Combine(source, name), Path.Combine(target, name + ".bytes"), true);
            }
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
