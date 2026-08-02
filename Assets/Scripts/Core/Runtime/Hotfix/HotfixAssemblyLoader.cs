using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public static class HotfixAssemblyLoader
    {
        public static async UniTask<List<Assembly>> LoadAsync(IEnumerable<string> hotfixAssemblies)
        {
            var assemblies = new List<Assembly>();
            if (hotfixAssemblies == null)
            {
                return assemblies;
            }

#if UNITY_EDITOR
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            foreach (var assemblyName in hotfixAssemblies)
            {
                if (string.IsNullOrWhiteSpace(assemblyName))
                {
                    continue;
                }

#if UNITY_EDITOR
                var editorAssembly = FindLoadedAssembly(loadedAssemblies, assemblyName);
                if (editorAssembly != null)
                {
                    assemblies.Add(editorAssembly);
                    Debug.Log($"[Hotfix] 编辑器复用已加载程序集: {editorAssembly.GetName().Name}");
                    continue;
                }
#endif
                var result = await ResourceServices.Default.LoadTextAssetAsync(assemblyName);
                if (!result.Success)
                {
                    Debug.LogWarning($"[Hotfix] 跳过热更程序集，未找到资源: {assemblyName}");
                    continue;
                }

                var bytes = result.Asset.bytes;
                Assembly assembly;
                try
                {
                    assembly = Assembly.Load(bytes);
                }
                finally
                {
                    ResourceServices.Default.ReleaseAsset(result.Asset);
                }

                assemblies.Add(assembly);
                Debug.Log($"[Hotfix] 加载热更程序集: {assembly.GetName().Name}");
            }

            return assemblies;
        }

#if UNITY_EDITOR
        private static Assembly FindLoadedAssembly(IEnumerable<Assembly> assemblies, string assemblyName)
        {
            var normalized = NormalizeAssemblyName(assemblyName);
            foreach (var assembly in assemblies)
            {
                if (assembly.GetName().Name == normalized)
                {
                    return assembly;
                }
            }

            return null;
        }
#endif

        private static string NormalizeAssemblyName(string assemblyName)
        {
            return assemblyName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                ? assemblyName.Substring(0, assemblyName.Length - 4)
                : assemblyName;
        }
    }
}
