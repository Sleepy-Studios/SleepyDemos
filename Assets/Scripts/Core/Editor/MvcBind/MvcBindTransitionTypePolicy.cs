using System;
using System.Collections.Generic;
using System.IO;
using Core.Runtime;
using UnityEditor;
using UnityEditor.Compilation;

namespace Core.Editor.MvcBind
{
    internal static class MvcBindTransitionTypePolicy
    {
        internal static List<string> GetTypeChoices()
        {
            var typeNames = new List<string>();
            var playerAssemblyNames = GetPlayerAssemblyNames();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IUITransition>())
            {
                if (IsSupported(type, playerAssemblyNames))
                {
                    typeNames.Add(type.FullName);
                }
            }

            typeNames.Sort(StringComparer.Ordinal);
            typeNames.Insert(0, "null");
            return typeNames;
        }

        /// <summary>
        /// 解析可由生成代码直接构造的 UI Transition 类型名。
        /// </summary>
        /// <param name="configuredTypeName">MvcBind 设置中保存的完整类型名。</param>
        /// <returns>可直接写入 C# 源码的完整类型名。</returns>
        /// <exception cref="InvalidDataException">类型不存在或不满足生成约束。</exception>
        internal static string ResolveCSharpTypeName(string configuredTypeName)
        {
            var playerAssemblyNames = GetPlayerAssemblyNames();
            foreach (var type in TypeCache.GetTypesDerivedFrom<IUITransition>())
            {
                if (string.Equals(type.FullName, configuredTypeName, StringComparison.Ordinal) &&
                    IsSupported(type, playerAssemblyNames))
                {
                    return type.FullName;
                }
            }

            throw new InvalidDataException(
                $"MvcBind 生成失败：UI Transition 类型 '{configuredTypeName}' 不存在或不可生成。" +
                "类型必须是 Player 可用的顶级 public 非泛型 class，并提供 public 无参构造函数。");
        }

        private static bool IsSupported(Type type, HashSet<string> playerAssemblyNames)
        {
            return type != null &&
                   typeof(IUITransition).IsAssignableFrom(type) &&
                   type.IsClass &&
                   !type.IsAbstract &&
                   type.IsPublic &&
                   !type.IsNested &&
                   !type.IsGenericTypeDefinition &&
                   !type.ContainsGenericParameters &&
                   type.GetConstructor(Type.EmptyTypes) != null &&
                   playerAssemblyNames.Contains(type.Assembly.GetName().Name);
        }

        private static HashSet<string> GetPlayerAssemblyNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies);
            foreach (var assembly in assemblies)
            {
                names.Add(assembly.name);
            }

            return names;
        }
    }
}
