using System;

namespace Core.Runtime
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class ModuleAttribute : Attribute
    {
        public ModuleAttribute(string moduleName)
        {
            ModuleName = moduleName;
        }

        public string ModuleName { get; }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UIBindAttribute : Attribute
    {
        public UIBindAttribute(string viewName)
        {
            ViewName = viewName;
        }

        public string ViewName { get; }
    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    public sealed class SourceAttribute : Attribute
    {
        public SourceAttribute(string source)
        {
            Source = source;
        }

        public string Source { get; }
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class ComponentAttribute : Attribute
    {
        public ComponentAttribute(string methodFormat, bool generateByDefault = false)
        {
            MethodFormat = methodFormat;
            GenerateByDefault = generateByDefault;
        }

        public string MethodFormat { get; }
        public bool GenerateByDefault { get; }
    }
}
