using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Core.Editor.AssetNaming;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.MvcBind
{
    [Serializable]
    public sealed class MvcBindSettings
    {
        public string prefabPath = string.Empty;
        public string moduleName = MvcBindToolConfig.DefaultModuleName;
        public string viewName = "NewView";
        public string namespaceName = MvcBindToolConfig.DefaultNamespace;
        public string outputFolder = MvcBindToolConfig.ModuleRoot;
        public string address = string.Empty;
        public Core.Runtime.ViewType viewType = Core.Runtime.ViewType.View;
        public Core.Runtime.UILayer layer = Core.Runtime.UILayer.Base;
        public Core.Runtime.MaskType mask = Core.Runtime.MaskType.None;
        public bool isHotfix = true;
        public bool isAsync = true;
        public bool enableOnInit = true;
        public bool destroyOnHide = true;
        public string uiAnimationType = "null";
        public string cameraAnimationType = "null";

        public string componentScriptName => $"{viewName}Component.cs";
        public string viewScriptName => $"{viewName}.cs";

        public bool ApplyPrefabPath(string assetPath)
        {
            if (!MvcBindPathUtility.IsPrefabAssetPath(assetPath))
            {
                return false;
            }

            prefabPath = MvcBindPathUtility.NormalizeAssetPath(assetPath);
            address = MvcBindPathUtility.ToRuntimeAddress(prefabPath);
            viewName = MvcBindPathUtility.ToViewClassName(prefabPath);
            outputFolder = MvcBindPathUtility.ToOutputFolder(moduleName, viewName);
            return true;
        }
    }

    public sealed class MvcBindNode
    {
        public int id;
        public int depth;
        public string name;
        public string path;
        public GameObject gameObject;
        public Type selectedComponentType;
        public string selectedComponentTypeName;
        public readonly List<Type> selectedComponentTypes = new List<Type>();
        public readonly List<string> selectedMethodNames = new List<string>();
        public readonly Dictionary<string, List<string>> selectedMethodNamesByComponentTypeName = new Dictionary<string, List<string>>();
        public readonly List<Type> componentTypes = new List<Type>();
    }

    public sealed class MvcBindComponentInfo
    {
        public int index;
        public string fieldName;
        public Type componentType;
        public Component component;
        public readonly List<MvcBindMethodInfo> methods = new List<MvcBindMethodInfo>();
    }

    public sealed class MvcBindMethodInfo
    {
        public string registerMethodName;
        public string componentMethodName;
        public string parameterText;
        public List<Type> parameterTypes = new List<Type>();
    }

    public enum MvcBindTreeItemKind
    {
        Root,
        Module,
        View,
        Prefab,
        Code
    }

    public sealed class MvcBindViewRecord
    {
        public string moduleName = string.Empty;
        public string viewName = string.Empty;
        public string address = string.Empty;
        public string prefabPath = string.Empty;
        public string viewScriptPath = string.Empty;
        public string componentScriptPath = string.Empty;
        public bool hasPrefab;
        public bool hasViewScript;
        public bool hasComponentScript;
    }

    public static class MvcBindPathUtility
    {
        public const string DefaultUiPrefabRoot = "Assets/LoadResources/UI";

        public static bool TryGetPrefabAssetPath(GameObject target, out string assetPath)
        {
            assetPath = string.Empty;
            if (target == null)
            {
                return false;
            }

            var directPath = AssetDatabase.GetAssetPath(target);
            if (IsPrefabAssetPath(directPath))
            {
                assetPath = NormalizeAssetPath(directPath);
                return true;
            }

            var source = PrefabUtility.GetCorrespondingObjectFromSource(target);
            var sourcePath = AssetDatabase.GetAssetPath(source);
            if (IsPrefabAssetPath(sourcePath))
            {
                assetPath = NormalizeAssetPath(sourcePath);
                return true;
            }

            return false;
        }

        public static bool IsPrefabAssetPath(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath) &&
                   assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) ? string.Empty : assetPath.Replace('\\', '/');
        }

        public static string ToRuntimeAddress(string assetPath)
        {
            var normalized = NormalizeAssetPath(assetPath);
            var withoutExtension = Path.ChangeExtension(normalized, null)?.Replace('\\', '/') ?? string.Empty;
            const string assetsPrefix = "Assets/";
            return withoutExtension.StartsWith(assetsPrefix, StringComparison.OrdinalIgnoreCase)
                ? withoutExtension.Substring(assetsPrefix.Length)
                : withoutExtension;
        }

        public static string ToViewClassName(string assetPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(NormalizeAssetPath(assetPath));
            var semanticName = LoadResourcesAssetNamingRules.GetSemanticName(fileName);
            var className = ToPascalIdentifier(semanticName);
            return className.EndsWith("View", StringComparison.Ordinal) ? className : $"{className}View";
        }

        public static string ToModuleName(string address)
        {
            var folder = Path.GetDirectoryName(address)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folder))
            {
                return "UI";
            }

            var lastSlash = folder.LastIndexOf('/');
            return lastSlash >= 0 ? folder.Substring(lastSlash + 1) : folder;
        }

        public static string ToOutputFolder(string moduleName, string viewName)
        {
            var cleanModule = ToRelativeFolder(moduleName);
            var cleanView = ToPascalIdentifier(viewName);
            var moduleRoot = string.IsNullOrEmpty(cleanModule)
                ? MvcBindToolConfig.ModuleRoot
                : $"{MvcBindToolConfig.ModuleRoot}/{cleanModule}";
            return string.IsNullOrEmpty(cleanView)
                ? moduleRoot
                : $"{moduleRoot}/{cleanView}/View";
        }

        private static string ToRelativeFolder(string value)
        {
            return (value ?? string.Empty)
                .Replace('\\', '/')
                .Trim()
                .Trim('/');
        }

        private static string ToPascalIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "GeneratedView";
            }

            var builder = new StringBuilder(value.Length);
            var nextUpper = true;
            foreach (var ch in value)
            {
                if (!char.IsLetterOrDigit(ch))
                {
                    nextUpper = true;
                    continue;
                }

                if (builder.Length == 0 && char.IsDigit(ch))
                {
                    builder.Append('_');
                }

                builder.Append(nextUpper ? char.ToUpperInvariant(ch) : ch);
                nextUpper = false;
            }

            return builder.Length == 0 ? "GeneratedView" : builder.ToString();
        }
    }

    public static class MvcBindToolConfig
    {
        public const string ScriptRoot = "Assets/Scripts/Hotfix";
        public const string ModuleRoot = ScriptRoot + "/Module";
        public const string DefaultNamespace = "Hotfix";
        public const string DefaultModuleName = "";
    }
}
