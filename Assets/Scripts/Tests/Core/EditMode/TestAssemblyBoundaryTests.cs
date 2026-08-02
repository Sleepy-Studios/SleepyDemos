using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Core.Tests.Assemblies
{
    public sealed class TestAssemblyBoundaryTests
    {
        private const string HotfixConfigPath = "Assets/LoadResources/Config/HotfixConfig.asset";
        private const string HotfixCodePath = "Assets/LoadResources/Codes/Hotfix";

        [Test]
        public void PlayerAssemblyQuery_DoesNotContainEditModeOrEditorTestFramework()
        {
            var playerAssemblyNames = CompilationPipeline
                .GetAssemblies(AssembliesType.Player)
                .Select(assembly => assembly.name)
                .ToArray();
            var forbiddenNames = new[]
            {
                "Core.Tests.EditMode",
                "Hotfix.Tests.EditMode",
                "nunit.framework",
                "UnityEditor.TestRunner"
            };

            foreach (var forbiddenName in forbiddenNames)
            {
                Assert.That(playerAssemblyNames, Does.Not.Contain(forbiddenName));
            }
        }

        [Test]
        public void HotfixConfig_DoesNotContainTestAssemblies()
        {
            var config = AssetDatabase.LoadAssetAtPath<HotfixConfig>(HotfixConfigPath);

            Assert.That(config, Is.Not.Null);
            Assert.That(
                config.HotfixAssemblies,
                Has.None.EndsWith(".Tests.dll").IgnoreCase);
        }

        [Test]
        public void HotfixCodeDirectory_DoesNotContainTestAssemblies()
        {
            if (!Directory.Exists(HotfixCodePath))
            {
                Assert.Pass();
            }

            var testAssemblies = Directory
                .GetFiles(HotfixCodePath, "*", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).Contains(".Tests.dll", StringComparison.OrdinalIgnoreCase))
                .Select(NormalizePath)
                .ToArray();

            Assert.That(testAssemblies, Is.Empty);
        }

        [Test]
        public void TestAssemblyDefinitions_StayInsideTestsAndUseExpectedModeConfiguration()
        {
            var violations = new List<string>();
            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { "Assets/Scripts" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var definition = JsonUtility.FromJson<AssemblyDefinitionData>(File.ReadAllText(path));
                if (!IsTestAssembly(definition))
                {
                    continue;
                }

                if (!NormalizePath(path).StartsWith("Assets/Scripts/Tests/", StringComparison.Ordinal))
                {
                    violations.Add($"{path}: Test Assembly 必须位于 Assets/Scripts/Tests。 ");
                }

                if (definition.autoReferenced)
                {
                    violations.Add($"{path}: autoReferenced 必须为 false。 ");
                }

                var assemblyName = definition.name ?? string.Empty;
                var isEditModeAssembly = assemblyName.EndsWith(".EditMode", StringComparison.Ordinal);
                var isPlayModeAssembly = assemblyName.EndsWith(".PlayMode", StringComparison.Ordinal);
                if (!isEditModeAssembly && !isPlayModeAssembly)
                {
                    violations.Add($"{path}: Test Assembly 名称必须以 .EditMode 或 .PlayMode 结尾。 ");
                }

                var hasEditorOnlyPlatform = definition.includePlatforms != null &&
                                            definition.includePlatforms.Length == 1 &&
                                            definition.includePlatforms[0].Equals(
                                                "Editor",
                                                StringComparison.OrdinalIgnoreCase);
                if (isEditModeAssembly != hasEditorOnlyPlatform)
                {
                    violations.Add($"{path}: EditMode 必须限制为 Editor，PlayMode 必须使用标准 PlayMode 平台配置。 ");
                }
            }

            Assert.That(violations, Is.Empty, string.Join("\n", violations));
        }

        private static bool IsTestAssembly(AssemblyDefinitionData definition)
        {
            return definition != null &&
                   definition.optionalUnityReferences != null &&
                   definition.optionalUnityReferences.Any(
                       reference => reference.Equals("TestAssemblies", StringComparison.OrdinalIgnoreCase));
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            public string name;
            public bool autoReferenced = true;
            public string[] optionalUnityReferences;
            public string[] includePlatforms;
        }
    }
}
