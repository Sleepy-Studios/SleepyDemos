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
        private bool refreshScheduled;

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
            EditorApplication.hierarchyChanged += ScheduleRefreshFromHierarchy;
            BuildUI();
            RefreshFromSelection();
        }

        private void OnDisable()
        {
            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
            EditorApplication.hierarchyChanged -= ScheduleRefreshFromHierarchy;
            EditorApplication.delayCall -= DelayedRefreshFromHierarchy;
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
            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            element.Clear();
            var node = nodes[index];
            var name = new Label { name = "name", style = { minWidth = 180, flexGrow = 1 } };
            element.Add(name);
            name.text = $"{new string(' ', node.depth * 2)}{node.name}";

            var choices = new List<string> { MvcBindComponentWindowBridge.NoneChoice, MvcBindComponentWindowBridge.MixedChoice };
            foreach (var type in node.componentTypes)
            {
                choices.Add(MvcBindComponentWindowBridge.GetComponentDisplayName(type));
            }

            var component = new PopupField<string>(choices, MvcBindComponentWindowBridge.GetComponentChoiceIndex(node, choices))
            {
                name = "component",
                style = { minWidth = 260, flexGrow = 1 }
            };
            element.Add(component);
            component.RegisterValueChangedCallback(evt =>
            {
                MvcBindComponentWindowBridge.ApplyComponentChoice(node, evt.newValue);
                if (MvcBindComponentWindowBridge.IsMixedSelected(node))
                {
                    component.SetValueWithoutNotify(MvcBindComponentWindowBridge.MixedChoice);
                }
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

            if (!MvcBindComponentWindowBridge.GenerateAndBind(targetPrefabRoot, settings, nodes, true, out var path, out var message))
            {
                ShowNotification(new GUIContent(message));
                Debug.LogWarning(message);
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
            var oldMethod = element.Q<PopupField<string>>("method");
            oldMethod?.RemoveFromHierarchy();

            var choices = new List<string> { MvcBindComponentWindowBridge.NoneChoice };
            var componentType = node.selectedComponentType;
            if (componentType != null)
            {
                foreach (var item in MvcCodeGenerator.GetRegisterMethods(componentType, node.name))
                {
                    choices.Add(item.registerMethodName);
                }
            }

            var selectedIndex = node.selectedMethodNames.Count == 0 ? 0 : Mathf.Max(0, choices.IndexOf(node.selectedMethodNames[0]));
            var method = new PopupField<string>(choices, selectedIndex)
            {
                name = "method",
                style = { minWidth = 180, flexGrow = 1 }
            };
            element.Add(method);
            method.RegisterValueChangedCallback(evt =>
            {
                var selectedMethods = GetSelectedMethodNames(node);
                selectedMethods.Clear();
                if (evt.newValue != MvcBindComponentWindowBridge.NoneChoice)
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

        private void ScheduleRefreshFromHierarchy()
        {
            if (refreshScheduled || PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                return;
            }

            refreshScheduled = true;
            EditorApplication.delayCall += DelayedRefreshFromHierarchy;
        }

        private void DelayedRefreshFromHierarchy()
        {
            refreshScheduled = false;
            if (this == null || PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                return;
            }

            RefreshFromSelection();
        }
    }

    public static class MvcBindComponentWindowBridge
    {
        public const string NoneChoice = "None";
        public const string MixedChoice = "Mixed";

        private static bool suppressAutoGenerateOnPrefabSave;

        public static bool SuppressAutoGenerateOnPrefabSave => suppressAutoGenerateOnPrefabSave;

        public static void Open(MvcBindSettings settings)
        {
            MvcComponentBindWindow.OpenWindow(settings);
        }

        public static void BindPrefabComponents(GameObject targetPrefabRoot, IReadOnlyList<MvcBindNode> nodes)
        {
            BindPrefabComponents(targetPrefabRoot, MvcCodeGenerator.CollectComponents(nodes));
        }

        public static bool GenerateAndBind(
            GameObject targetPrefabRoot,
            MvcBindSettings settings,
            IReadOnlyList<MvcBindNode> nodes,
            bool savePrefabStage,
            out string generatedPath,
            out string message)
        {
            generatedPath = string.Empty;
            message = string.Empty;

            var components = MvcCodeGenerator.CollectComponents(nodes);
            if (components.Count == 0)
            {
                message = "MvcBind 生成失败：请先勾选至少一个要绑定的组件。";
                return false;
            }

            BindPrefabComponents(targetPrefabRoot, components);
            if (savePrefabStage)
            {
                SavePrefabStageIfOpen(targetPrefabRoot);
            }

            try
            {
                generatedPath = MvcCodeGenerator.Generate(settings, nodes);
                return true;
            }
            catch (InvalidDataException exception)
            {
                message = exception.Message;
                return false;
            }
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

        public static void SavePrefabStageIfOpen(GameObject targetPrefabRoot)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null ||
                prefabStage.prefabContentsRoot == null ||
                targetPrefabRoot != prefabStage.prefabContentsRoot ||
                string.IsNullOrEmpty(prefabStage.assetPath))
            {
                return;
            }

            try
            {
                suppressAutoGenerateOnPrefabSave = true;
                PrefabUtility.SaveAsPrefabAsset(prefabStage.prefabContentsRoot, prefabStage.assetPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                suppressAutoGenerateOnPrefabSave = false;
            }
        }

        public static string GetComponentDisplayName(Type type)
        {
            if (type == null)
            {
                return NoneChoice;
            }

            return type.Name;
        }

        public static int GetComponentChoiceIndex(MvcBindNode node, List<string> choices)
        {
            if (IsMixedSelected(node))
            {
                return Mathf.Max(0, choices.IndexOf(MixedChoice));
            }

            if (node.selectedComponentType == null)
            {
                return Mathf.Max(0, choices.IndexOf(NoneChoice));
            }

            var selected = GetComponentDisplayName(node.selectedComponentType);
            var index = choices.IndexOf(selected);
            return index >= 0 ? index : 0;
        }

        public static void ApplyComponentChoice(MvcBindNode node, string choice)
        {
            if (IsMixedSelected(node) && choice != NoneChoice && choice != MixedChoice)
            {
                ToggleMixedComponentChoice(node, choice);
                return;
            }

            var previousSelectedComponentType = node.selectedComponentType;
            var previousSelectedComponentTypes = node.selectedComponentTypes.ToArray();
            node.selectedMethodNames.Clear();
            node.selectedComponentTypes.Clear();
            node.selectedMethodNamesByComponentTypeName.Clear();
            if (choice == NoneChoice)
            {
                node.selectedComponentType = null;
                node.selectedComponentTypeName = null;
                return;
            }

            if (choice == MixedChoice)
            {
                node.selectedComponentType = null;
                node.selectedComponentTypeName = MixedChoice;
                if (previousSelectedComponentType != null)
                {
                    node.selectedComponentTypes.Add(previousSelectedComponentType);
                    return;
                }

                foreach (var type in previousSelectedComponentTypes)
                {
                    if (type != null && node.componentTypes.Contains(type) && !node.selectedComponentTypes.Contains(type))
                    {
                        node.selectedComponentTypes.Add(type);
                    }
                }

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
                node.selectedComponentTypeName = MixedChoice;
            }
        }

        public static bool IsMixedSelected(MvcBindNode node)
        {
            return node != null &&
                   (node.selectedComponentTypeName == MixedChoice || node.selectedComponentTypes.Count > 1);
        }

        public static bool IsComponentSelected(MvcBindNode node, Type componentType)
        {
            if (node == null || componentType == null)
            {
                return false;
            }

            return IsMixedSelected(node)
                ? node.selectedComponentTypes.Contains(componentType)
                : node.selectedComponentType == componentType;
        }

        private static void ToggleMixedComponentChoice(MvcBindNode node, string choice)
        {
            var componentType = node.componentTypes.Find(type => GetComponentDisplayName(type) == choice);
            if (componentType == null)
            {
                return;
            }

            node.selectedComponentType = null;
            node.selectedComponentTypeName = MixedChoice;
            node.selectedMethodNames.Clear();
            node.selectedMethodNamesByComponentTypeName.Remove(componentType.FullName);

            if (node.selectedComponentTypes.Contains(componentType))
            {
                node.selectedComponentTypes.Remove(componentType);
            }
            else
            {
                node.selectedComponentTypes.Add(componentType);
            }

            if (node.selectedComponentTypes.Count == 0)
            {
                node.selectedComponentTypeName = null;
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
