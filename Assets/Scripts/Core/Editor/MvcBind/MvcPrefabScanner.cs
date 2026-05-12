using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Core.Editor.MvcBind
{
    public static class MvcPrefabScanner
    {
        public static List<MvcBindNode> Scan(GameObject root)
        {
            var nodes = new List<MvcBindNode>();
            if (root == null)
            {
                return nodes;
            }

            AddNode(root.transform, string.Empty, 0, nodes);
            return nodes;
        }

        private static void AddNode(Transform transform, string parentPath, int depth, List<MvcBindNode> nodes)
        {
            var path = string.IsNullOrEmpty(parentPath) ? transform.name : $"{parentPath}/{transform.name}";
            var node = new MvcBindNode
            {
                id = nodes.Count + 1,
                depth = depth,
                name = transform.name,
                path = path,
                gameObject = transform.gameObject
            };

            CollectSupportedComponents(node);
            nodes.Add(node);

            for (int i = 0; i < transform.childCount; i++)
            {
                AddNode(transform.GetChild(i), path, depth + 1, nodes);
            }
        }

        private static void CollectSupportedComponents(MvcBindNode node)
        {
            foreach (var component in node.gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                var type = component.GetType();
                if (!node.componentTypes.Contains(type))
                {
                    node.componentTypes.Add(type);
                }
            }
        }
    }

    public sealed class MvcComponentBindWindow : EditorWindow
    {
        private MvcBindSettings settings = new MvcBindSettings();
        private readonly List<MvcBindNode> nodes = new List<MvcBindNode>();
        private ListView listView;
        private GameObject targetPrefabRoot;

        public static void OpenWindow(MvcBindSettings bindSettings)
        {
            var window = GetWindow<MvcComponentBindWindow>("MvcBind Components");
            window.settings = bindSettings ?? new MvcBindSettings();
            window.RefreshFromSelection();
        }

        private void OnEnable()
        {
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            BuildUI();
            RefreshFromSelection();
        }

        private void OnDisable()
        {
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingLeft = 8, paddingRight = 8, paddingTop = 8, paddingBottom = 8 } };
            rootVisualElement.Add(header);
            header.Add(new UnityEngine.UIElements.Button(RefreshFromSelection) { text = "Refresh" });
            header.Add(new UnityEngine.UIElements.Button(CreateCode) { text = "Create" });

            listView = new ListView(nodes, 22, MakeItem, BindItem)
            {
                selectionType = SelectionType.Single,
                style = { flexGrow = 1 }
            };
            rootVisualElement.Add(listView);
        }

        private VisualElement MakeItem()
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            row.Add(new Label { name = "name", style = { minWidth = 180, flexGrow = 1 } });
            row.Add(new PopupField<string> { name = "component", style = { minWidth = 260, flexGrow = 1 } });
            row.Add(new PopupField<string> { name = "method", style = { minWidth = 180, flexGrow = 1 } });
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            var node = nodes[index];
            var name = element.Q<Label>("name");
            name.text = $"{new string(' ', node.depth * 2)}{node.name}";

            var component = element.Q<PopupField<string>>("component");
            var choices = new List<string> { "None", "Mixed" };
            foreach (var type in node.componentTypes)
            {
                choices.Add(MvcBindComponentWindowBridge.GetComponentDisplayName(type));
            }
            component.choices = choices;
            component.index = MvcBindComponentWindowBridge.GetComponentChoiceIndex(node, choices);
            component.RegisterValueChangedCallback(evt =>
            {
                MvcBindComponentWindowBridge.ApplyComponentChoice(node, evt.newValue);
                BindMethodPopup(element, node);
            });

            BindMethodPopup(element, node);
        }

        private void RefreshFromSelection()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            targetPrefabRoot = prefabStage != null ? prefabStage.prefabContentsRoot : Selection.activeGameObject;
            nodes.Clear();
            nodes.AddRange(MvcPrefabScanner.Scan(targetPrefabRoot));
            MvcBindComponentWindowBridge.RestoreComponentChoices(targetPrefabRoot, nodes);
            listView?.Rebuild();

            if (TryResolveCurrentPrefabPath(out var prefabPath))
            {
                settings.ApplyPrefabPath(prefabPath);
            }
            else
            {
                Debug.LogWarning("MvcBind 需要选中 Prefab 资源，或在 Prefab Mode 中打开 Prefab。");
            }
        }

        private void CreateCode()
        {
            if (TryResolveCurrentPrefabPath(out var prefabPath))
            {
                settings.ApplyPrefabPath(prefabPath);
            }

            var components = MvcCodeGenerator.CollectComponents(nodes);
            if (components.Count == 0)
            {
                ShowNotification(new GUIContent("请先勾选至少一个组件"));
                Debug.LogWarning("MvcBind 生成失败：请先勾选至少一个要绑定的组件。");
                return;
            }

            BindPrefabComponents(components);
            string path;
            try
            {
                path = MvcCodeGenerator.Generate(settings, nodes);
            }
            catch (InvalidDataException exception)
            {
                ShowNotification(new GUIContent(exception.Message));
                Debug.LogWarning(exception.Message);
                return;
            }

            Debug.Log($"MvcBind generated: {path}");
        }

        private bool TryResolveCurrentPrefabPath(out string prefabPath)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && MvcBindPathUtility.IsPrefabAssetPath(prefabStage.assetPath))
            {
                prefabPath = MvcBindPathUtility.NormalizeAssetPath(prefabStage.assetPath);
                return true;
            }

            return MvcBindPathUtility.TryGetPrefabAssetPath(targetPrefabRoot, out prefabPath);
        }

        private void BindMethodPopup(VisualElement element, MvcBindNode node)
        {
            var method = element.Q<PopupField<string>>("method");
            var choices = new List<string> { "None" };
            var componentType = node.selectedComponentType;
            if (componentType != null)
            {
                foreach (var item in MvcCodeGenerator.GetRegisterMethods(componentType, node.name))
                {
                    choices.Add(item.registerMethodName);
                }
            }

            method.choices = choices;
            method.index = node.selectedMethodNames.Count == 0 ? 0 : Mathf.Max(0, choices.IndexOf(node.selectedMethodNames[0]));
            method.RegisterValueChangedCallback(evt =>
            {
                var selectedMethods = GetSelectedMethodNames(node);
                selectedMethods.Clear();
                if (evt.newValue != "None")
                {
                    selectedMethods.Add(evt.newValue);
                }
            });
            method.style.display = choices.Count > 1 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static List<string> GetSelectedMethodNames(MvcBindNode node)
        {
            if (node.selectedComponentType != null &&
                node.selectedMethodNamesByComponentTypeName.TryGetValue(node.selectedComponentType.FullName, out var methods))
            {
                return methods;
            }

            return node.selectedMethodNames;
        }

        private void BindPrefabComponents(IReadOnlyList<MvcBindComponentInfo> components)
        {
            MvcBindComponentWindowBridge.BindPrefabComponents(targetPrefabRoot, components);
        }

        private void OnPrefabStageOpened(PrefabStage stage)
        {
            targetPrefabRoot = stage.prefabContentsRoot;
            RefreshFromSelection();
        }

        private void OnPrefabStageClosing(PrefabStage stage)
        {
            nodes.Clear();
            listView?.Rebuild();
        }
    }

    public static class MvcBindComponentWindowBridge
    {
        public static void Open(MvcBindSettings settings)
        {
            MvcComponentBindWindow.OpenWindow(settings);
        }

        public static void BindPrefabComponents(GameObject targetPrefabRoot, IReadOnlyList<MvcBindNode> nodes)
        {
            BindPrefabComponents(targetPrefabRoot, MvcCodeGenerator.CollectComponents(nodes));
        }

        public static void BindPrefabComponents(GameObject targetPrefabRoot, IReadOnlyList<MvcBindComponentInfo> components)
        {
            if (targetPrefabRoot == null)
            {
                return;
            }

            var itemIndex = targetPrefabRoot.GetComponent<ComponentItemIndex>() ?? targetPrefabRoot.AddComponent<ComponentItemIndex>();
            itemIndex.Components = components.Select(item => item.component).ToArray();
            itemIndex.ComponentTypes = components.Select(item => item.componentType.FullName).ToArray();
            itemIndex.BindingKeys = components.Select(CreateBindingKey).ToArray();
            itemIndex.BindingMethods = components.Select(item => string.Join("|", item.methods.Select(method => method.registerMethodName))).ToArray();
            EditorUtility.SetDirty(itemIndex);
            EditorUtility.SetDirty(targetPrefabRoot);

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                EditorSceneManager.MarkSceneDirty(prefabStage.scene);
                return;
            }

            if (PrefabUtility.IsPartOfPrefabAsset(targetPrefabRoot))
            {
                AssetDatabase.SaveAssets();
            }
            else
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(itemIndex);
            }
        }

        public static string GetComponentDisplayName(Type type)
        {
            if (type == null)
            {
                return "None";
            }

            return string.IsNullOrEmpty(type.Namespace) ? type.Name : $"{type.Name} ({type.Namespace})";
        }

        public static int GetComponentChoiceIndex(MvcBindNode node, List<string> choices)
        {
            if (node.selectedComponentType == null)
            {
                return node.selectedComponentTypes.Count > 1 ? 1 : 0;
            }

            var selected = GetComponentDisplayName(node.selectedComponentType);
            var index = choices.IndexOf(selected);
            return index >= 0 ? index : 0;
        }

        public static void ApplyComponentChoice(MvcBindNode node, string choice)
        {
            node.selectedMethodNames.Clear();
            node.selectedComponentTypes.Clear();
            node.selectedMethodNamesByComponentTypeName.Clear();
            if (choice == "None")
            {
                node.selectedComponentType = null;
                node.selectedComponentTypeName = null;
                return;
            }

            if (choice == "Mixed")
            {
                node.selectedComponentType = null;
                node.selectedComponentTypeName = "Mixed";
                node.selectedComponentTypes.AddRange(node.componentTypes);
                return;
            }

            node.selectedComponentType = node.componentTypes.Find(type => GetComponentDisplayName(type) == choice);
            node.selectedComponentTypeName = node.selectedComponentType?.FullName;
        }

        public static void RestoreComponentChoices(GameObject targetPrefabRoot, IEnumerable<MvcBindNode> targetNodes)
        {
            if (targetPrefabRoot == null)
            {
                return;
            }

            var itemIndex = targetPrefabRoot.GetComponent<ComponentItemIndex>();
            var savedEntries = LoadSavedEntries(targetPrefabRoot, itemIndex);
            if (savedEntries.Count == 0)
            {
                return;
            }

            foreach (var node in targetNodes)
            {
                RestoreNode(node, savedEntries);
            }
        }

        private static void RestoreNode(MvcBindNode node, IReadOnlyDictionary<string, List<SavedBindEntry>> savedEntries)
        {
            if (node == null)
            {
                return;
            }

            node.selectedComponentType = null;
            node.selectedComponentTypeName = null;
            node.selectedComponentTypes.Clear();
            node.selectedMethodNames.Clear();
            node.selectedMethodNamesByComponentTypeName.Clear();

            foreach (var componentType in node.componentTypes)
            {
                var key = CreateBindingKey(node.path, componentType.FullName);
                if (!TryGetSavedEntries(savedEntries, node.path, componentType, out var entries))
                {
                    continue;
                }

                if (!node.selectedComponentTypes.Contains(componentType))
                {
                    node.selectedComponentTypes.Add(componentType);
                }

                var methods = entries
                    .SelectMany(entry => entry.methods)
                    .Where(method => !string.IsNullOrEmpty(method))
                    .Distinct()
                    .ToList();
                if (methods.Count > 0)
                {
                    node.selectedMethodNamesByComponentTypeName[componentType.FullName] = methods;
                }
            }

            if (node.selectedComponentTypes.Count == 1)
            {
                node.selectedComponentType = node.selectedComponentTypes[0];
                node.selectedComponentTypeName = node.selectedComponentType.FullName;
                node.selectedComponentTypes.Clear();

                if (node.selectedMethodNamesByComponentTypeName.TryGetValue(node.selectedComponentTypeName, out var methods))
                {
                    node.selectedMethodNames.AddRange(methods);
                }
                return;
            }

            if (node.selectedComponentTypes.Count > 1)
            {
                node.selectedComponentTypeName = "Mixed";
            }
        }

        private static bool TryGetSavedEntries(
            IReadOnlyDictionary<string, List<SavedBindEntry>> savedEntries,
            string nodePath,
            Type componentType,
            out List<SavedBindEntry> entries)
        {
            if (componentType != null)
            {
                var typeNames = new[]
                {
                    componentType.FullName,
                    componentType.AssemblyQualifiedName,
                    componentType.Name
                };

                foreach (var typeName in typeNames)
                {
                    var key = CreateBindingKey(nodePath, typeName);
                    if (savedEntries.TryGetValue(key, out entries))
                    {
                        return true;
                    }
                }
            }

            entries = null;
            return false;
        }

        private static Dictionary<string, List<SavedBindEntry>> LoadSavedEntries(GameObject targetPrefabRoot, ComponentItemIndex itemIndex)
        {
            var entries = new Dictionary<string, List<SavedBindEntry>>();

            if (itemIndex != null && itemIndex.Components != null)
            {
                var components = itemIndex.Components;
                var types = itemIndex.ComponentTypes;
                var keys = itemIndex.BindingKeys;
                var methods = itemIndex.BindingMethods;
                for (int i = 0; i < components.Length; i++)
                {
                    var component = components[i];
                    if (component == null)
                    {
                        continue;
                    }

                    var typeName = GetAt(types, i);
                    if (string.IsNullOrEmpty(typeName))
                    {
                        typeName = component.GetType().FullName;
                    }

                    var key = GetAt(keys, i);
                    if (string.IsNullOrEmpty(key))
                    {
                        key = CreateBindingKey(GetTransformPath(targetPrefabRoot.transform, component.transform), typeName);
                    }

                    var splitMethods = SplitMethods(GetAt(methods, i));
                    AddEntry(entries, key, splitMethods);
                    AddLegacyEntries(entries, targetPrefabRoot, component, splitMethods);
                }
            }

            return entries;
        }

        private static void AddLegacyEntries(
            Dictionary<string, List<SavedBindEntry>> entries,
            GameObject targetPrefabRoot,
            Component component,
            List<string> methods)
        {
            var componentType = component.GetType();
            var path = GetTransformPath(targetPrefabRoot.transform, component.transform);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            AddEntry(entries, CreateBindingKey(path, componentType.FullName), methods);
            AddEntry(entries, CreateBindingKey(path, componentType.AssemblyQualifiedName), methods);
            AddEntry(entries, CreateBindingKey(path, componentType.Name), methods);
        }

        private static string CreateBindingKey(MvcBindComponentInfo componentInfo)
        {
            if (componentInfo.component == null)
            {
                return CreateBindingKey(componentInfo.fieldName, componentInfo.componentType.FullName);
            }

            return CreateBindingKey(GetTransformPath(componentInfo.component.transform.root, componentInfo.component.transform), componentInfo.componentType.FullName);
        }

        private static string CreateBindingKey(string transformPath, string componentTypeName)
        {
            return $"{transformPath}|{componentTypeName}";
        }

        private static string GetTransformPath(Transform root, Transform target)
        {
            if (root == null || target == null)
            {
                return string.Empty;
            }

            var stack = new Stack<string>();
            var current = target;
            while (current != null)
            {
                stack.Push(current.name);
                if (current == root)
                {
                    break;
                }

                current = current.parent;
            }

            return string.Join("/", stack);
        }

        private static string GetAt(string[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length ? values[index] : string.Empty;
        }

        private static List<string> SplitMethods(string value)
        {
            return string.IsNullOrEmpty(value)
                ? new List<string>()
                : value.Split(new[] { '|', ',' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        }

        private static void AddEntry(Dictionary<string, List<SavedBindEntry>> entries, string key, List<string> methods)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!entries.TryGetValue(key, out var list))
            {
                list = new List<SavedBindEntry>();
                entries.Add(key, list);
            }

            list.Add(new SavedBindEntry { methods = methods ?? new List<string>() });
        }

        private sealed class SavedBindEntry
        {
            public List<string> methods = new List<string>();
        }
    }
}
