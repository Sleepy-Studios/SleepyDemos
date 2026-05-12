using System;
using System.Collections.Generic;
using System.Reflection;

namespace Core.Runtime
{
    public static class UITypeReflection
    {
        private static readonly Dictionary<string, Type> nameToTypes = new Dictionary<string, Type>();

        public static void Init(params Assembly[] assemblies)
        {
            nameToTypes.Clear();
            foreach (var assembly in assemblies)
            {
                Scan(assembly);
            }
        }

        public static void Scan(Assembly assembly)
        {
            if (assembly == null)
            {
                return;
            }

            foreach (var type in assembly.GetTypes())
            {
                if (!typeof(View).IsAssignableFrom(type) || type.IsAbstract)
                {
                    continue;
                }

                var mvcAttribute = type.GetCustomAttribute<MvcAttribute>();
                if (mvcAttribute != null && !nameToTypes.ContainsKey(mvcAttribute.MvcName))
                {
                    nameToTypes.Add(mvcAttribute.MvcName, type);
                }

                if (!nameToTypes.ContainsKey(type.Name))
                {
                    nameToTypes.Add(type.Name, type);
                }
            }
        }

        public static Type Get(string viewName)
        {
            if (string.IsNullOrEmpty(viewName))
            {
                return null;
            }

            if (nameToTypes.Count == 0)
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Scan(assembly);
                }
            }

            return nameToTypes.TryGetValue(viewName, out var type) ? type : null;
        }
    }
}
