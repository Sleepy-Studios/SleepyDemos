using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Core.Editor.HotUpdate
{
    internal static class HotUpdateAssemblyDefinitionFilter
    {
        internal static bool TryGetDllNameFromFile(string asmdefPath, out string dllName)
        {
            dllName = string.Empty;
            if (string.IsNullOrWhiteSpace(asmdefPath) || !File.Exists(asmdefPath))
            {
                return false;
            }

            try
            {
                return TryGetDllNameFromJson(File.ReadAllText(asmdefPath), out dllName);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        internal static bool TryGetDllNameFromJson(string json, out string dllName)
        {
            dllName = string.Empty;
            if (string.IsNullOrWhiteSpace(json))
            {
                return false;
            }

            AssemblyDefinitionData definition;
            try
            {
                definition = JsonUtility.FromJson<AssemblyDefinitionData>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (definition == null || string.IsNullOrWhiteSpace(definition.name))
            {
                return false;
            }

            var assemblyName = definition.name.Trim();
            if (assemblyName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
                ContainsTestAssemblyReference(definition.optionalUnityReferences) ||
                IsEditorOnly(definition.includePlatforms))
            {
                return false;
            }

            dllName = $"{assemblyName}.dll";
            return true;
        }

        private static bool ContainsTestAssemblyReference(string[] optionalUnityReferences)
        {
            return optionalUnityReferences != null && optionalUnityReferences.Any(
                reference => reference.Equals("TestAssemblies", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsEditorOnly(string[] includePlatforms)
        {
            return includePlatforms != null &&
                   includePlatforms.Length == 1 &&
                   includePlatforms[0].Equals("Editor", StringComparison.OrdinalIgnoreCase);
        }

        [Serializable]
        private sealed class AssemblyDefinitionData
        {
            public string name;
            public string[] references;
            public string[] optionalUnityReferences;
            public string[] includePlatforms;
        }
    }
}
