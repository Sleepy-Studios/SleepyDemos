using System;
using System.Collections.Generic;
using System.Reflection;

namespace Core.Runtime
{
    /// 启动时从指定程序集发现页面；运行中的名称查找只访问缓存。
    public static class UITypeReflection
    {
        private static readonly Dictionary<string, Type> nameToTypes = new Dictionary<string, Type>();
        private static readonly HashSet<Assembly> scannedAssemblies = new HashSet<Assembly>();

        /// <summary>开始本轮页面发现，清理上一次启动的类型和扫描记录。</summary>
        /// <param name="assemblies">已加载的项目程序集，不遍历整个 AppDomain。</param>
        public static void Init(params Assembly[] assemblies)
        {
            nameToTypes.Clear();
            scannedAssemblies.Clear();
            foreach (var assembly in assemblies) Scan(assembly);
        }

        /// <summary>补充扫描一个程序集；同一轮初始化内重复调用不再枚举类型。</summary>
        /// <param name="assembly">明确指定的程序集；为空时忽略。</param>
        public static void Scan(Assembly assembly)
        {
            if (assembly == null || !scannedAssemblies.Add(assembly)) return;
            // ponytail: 每轮只扫描指定程序集；Profiler 证实开销后再考虑编辑器生成。
            foreach (var type in assembly.GetTypes())
            {
                if (!type.IsClass || type == typeof(View) || !typeof(View).IsAssignableFrom(type)
                    || type.IsAbstract || type.ContainsGenericParameters) continue;

                var mvcAttribute = type.GetCustomAttribute<MvcAttribute>();
                // 保持原有命名规则：Mvc 别名与类名均可导航，同名保留先发现的类型。
                if (mvcAttribute != null && !nameToTypes.ContainsKey(mvcAttribute.MvcName))
                    nameToTypes.Add(mvcAttribute.MvcName, type);
                if (!nameToTypes.ContainsKey(type.Name)) nameToTypes.Add(type.Name, type);
            }
        }

        /// <summary>查找已经发现的页面，未知名称不触发额外扫描。</summary>
        /// <param name="viewName">类名或 Mvc 别名。</param>
        /// <returns>已发现的页面类型；未知或空名称返回 null。</returns>
        public static Type Get(string viewName)
        {
            return !string.IsNullOrEmpty(viewName) && nameToTypes.TryGetValue(viewName, out var type) ? type : null;
        }
    }
}
