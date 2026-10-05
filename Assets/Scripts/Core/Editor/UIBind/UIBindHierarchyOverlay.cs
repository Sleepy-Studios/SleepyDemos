using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Core.Editor.UIBind
{
    [InitializeOnLoad]
    public static class UIBindHierarchyOverlay
    {
        private const float ToggleWidth = 18f;
        private const float MethodWidth = 18f;
        private const float ComponentWidth = 132f;

        private static readonly Dictionary<int, UIBindNode> nodes = new Dictionary<int, UIBindNode>();
        private static GameObject root;
        private static bool refreshScheduled;
        private static bool generatingFromPrefabSave;

        public static GameObject Root => root;
        public static IReadOnlyCollection<UIBindNode> Nodes => nodes.Values;

        static UIBindHierarchyOverlay()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            EditorApplication.hierarchyChanged += ScheduleRefresh;
            PrefabStage.prefabStageOpened += _ => Refresh();
            PrefabStage.prefabStageClosing += _ => Clear();
            PrefabStage.prefabSaving += OnPrefabSaving;
        }

        private static void OnHierarchyGUI(int instanceId, Rect selectionRect)
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || prefabStage.prefabContentsRoot == null)
            {
                return;
            }

            if (root != prefabStage.prefabContentsRoot)
            {
                Refresh();
            }

            if (!nodes.TryGetValue(instanceId, out var node))
            {
                return;
            }

            DrawNode(node, selectionRect);
        }

        private static void DrawNode(UIBindNode node, Rect selectionRect)
        {
            var xMax = Mathf.Min(selectionRect.xMax, EditorGUIUtility.currentViewWidth - 10f);
            var methodRect = new Rect(xMax - MethodWidth, selectionRect.y, MethodWidth, selectionRect.height);
            var componentRect = new Rect(methodRect.x - ComponentWidth - 2f, selectionRect.y, ComponentWidth, selectionRect.height);
            var toggleRect = new Rect(componentRect.x - ToggleWidth - 2f, selectionRect.y, ToggleWidth, selectionRect.height);

            var selected = node.selectedComponentType != null ||
                           node.selectedComponentTypes.Count > 0 ||
                           UIBindComponentWindowBridge.IsMixedSelected(node);
            var nextSelected = EditorGUI.Toggle(toggleRect, selected);
            if (nextSelected != selected)
            {
                if (!nextSelected)
                {
                    UIBindComponentWindowBridge.ApplyComponentChoice(node, UIBindComponentWindowBridge.NoneChoice);
                }
                else
                {
                    var defaultChoice = node.componentTypes.Count > 0
                        ? UIBindComponentWindowBridge.GetComponentDisplayName(node.componentTypes[0])
                        : UIBindComponentWindowBridge.NoneChoice;
                    UIBindComponentWindowBridge.ApplyComponentChoice(node, defaultChoice);
                }
            }

            if (!nextSelected || node.componentTypes.Count == 0)
            {
                return;
            }

            if (GUI.Button(componentRect, GetSelectedLabel(node), EditorStyles.popup))
            {
                ShowComponentMenu(node, componentRect);
            }

            if (node.selectedComponentType != null && UIBindCodeGenerator.GetRegisterMethods(node.selectedComponentType, node.name).Count > 0)
            {
                if (GUI.Button(methodRect, string.Empty, EditorStyles.popup))
                {
                    ShowMethodMenu(node, methodRect);
                }
            }
        }

        private static string GetSelectedLabel(UIBindNode node)
        {
            if (UIBindComponentWindowBridge.IsMixedSelected(node))
            {
                return UIBindComponentWindowBridge.MixedChoice;
            }

            return node.selectedComponentType == null
                ? UIBindComponentWindowBridge.NoneChoice
                : UIBindComponentWindowBridge.GetComponentDisplayName(node.selectedComponentType);
        }

        private static void ShowComponentMenu(UIBindNode node, Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(UIBindComponentWindowBridge.NoneChoice), node.selectedComponentType == null && node.selectedComponentTypes.Count == 0, () => ApplyAndRepaint(node, UIBindComponentWindowBridge.NoneChoice));
            menu.AddItem(new GUIContent(UIBindComponentWindowBridge.MixedChoice), UIBindComponentWindowBridge.IsMixedSelected(node), () => ApplyAndRepaint(node, UIBindComponentWindowBridge.MixedChoice));
            foreach (var type in node.componentTypes)
            {
                var choice = UIBindComponentWindowBridge.GetComponentDisplayName(type);
                menu.AddItem(new GUIContent(choice), UIBindComponentWindowBridge.IsComponentSelected(node, type), () => ApplyAndRepaint(node, choice));
            }

            menu.DropDown(rect);
        }

        private static void ShowMethodMenu(UIBindNode node, Rect rect)
        {
            var menu = new GenericMenu();
            var selectedMethods = GetSelectedMethodNames(node);
            menu.AddItem(new GUIContent(UIBindComponentWindowBridge.NoneChoice), selectedMethods.Count == 0, () =>
            {
                selectedMethods.Clear();
                EditorApplication.RepaintHierarchyWindow();
            });

            foreach (var method in UIBindCodeGenerator.GetRegisterMethods(node.selectedComponentType, node.name))
            {
                menu.AddItem(new GUIContent(method.registerMethodName), selectedMethods.Contains(method.registerMethodName), () =>
                {
                    selectedMethods.Clear();
                    selectedMethods.Add(method.registerMethodName);
                    EditorApplication.RepaintHierarchyWindow();
                });
            }

            menu.DropDown(rect);
        }

        private static List<string> GetSelectedMethodNames(UIBindNode node)
        {
            if (node.selectedComponentType != null &&
                node.selectedMethodNamesByComponentTypeName.TryGetValue(node.selectedComponentType.FullName, out var methods))
            {
                return methods;
            }

            return node.selectedMethodNames;
        }

        private static void ApplyAndRepaint(UIBindNode node, string choice)
        {
            UIBindComponentWindowBridge.ApplyComponentChoice(node, choice);
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void Refresh()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var nextRoot = prefabStage?.prefabContentsRoot;
            var selectionSnapshot = root == nextRoot ? SnapshotSelections(nodes.Values) : new Dictionary<string, NodeSelectionSnapshot>();

            nodes.Clear();
            root = nextRoot;
            if (root == null)
            {
                return;
            }

            foreach (var node in UIBindPrefabScanner.Scan(root))
            {
                nodes[node.gameObject.GetInstanceID()] = node;
            }

            UIBindComponentWindowBridge.RestoreComponentChoices(root, nodes.Values);
            RestoreSelectionSnapshot(nodes.Values, selectionSnapshot);
        }

        public static void ForceRefresh()
        {
            Refresh();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void Clear()
        {
            root = null;
            nodes.Clear();
        }

        private static void ScheduleRefresh()
        {
            if (refreshScheduled || PrefabStageUtility.GetCurrentPrefabStage() == null)
            {
                return;
            }

            refreshScheduled = true;
            EditorApplication.delayCall += DelayedRefresh;
        }

        private static void DelayedRefresh()
        {
            refreshScheduled = false;
            Refresh();
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void OnPrefabSaving(GameObject prefabRoot)
        {
            if (prefabRoot == null ||
                generatingFromPrefabSave ||
                UIBindComponentWindowBridge.SuppressAutoGenerateOnPrefabSave)
            {
                return;
            }

            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage == null || prefabStage.prefabContentsRoot != prefabRoot)
            {
                return;
            }

            if (root != prefabRoot)
            {
                Refresh();
            }

            var targetNodes = root == prefabRoot
                ? nodes.Values.ToList()
                : UIBindPrefabScanner.Scan(prefabRoot);
            var components = UIBindCodeGenerator.CollectComponents(targetNodes);
            if (components.Count == 0)
            {
                return;
            }

            var settings = UIBindWindow.CreateSettingsForPrefabSave(prefabRoot, prefabStage.assetPath);
            generatingFromPrefabSave = true;
            try
            {
                if (UIBindComponentWindowBridge.GenerateAndBind(prefabRoot, settings, targetNodes, true, out var path, out var message))
                {
                    Debug.Log($"UIBind generated before prefab save: {path}");
                }
                else if (!string.IsNullOrEmpty(message))
                {
                    Debug.LogWarning(message);
                }
            }
            finally
            {
                generatingFromPrefabSave = false;
            }
        }

        private static Dictionary<string, NodeSelectionSnapshot> SnapshotSelections(IEnumerable<UIBindNode> sourceNodes)
        {
            var result = new Dictionary<string, NodeSelectionSnapshot>();
            foreach (var node in sourceNodes)
            {
                if (node == null || string.IsNullOrEmpty(node.path))
                {
                    continue;
                }

                result[node.path] = new NodeSelectionSnapshot(node);
            }

            return result;
        }

        private static void RestoreSelectionSnapshot(IEnumerable<UIBindNode> targetNodes, IReadOnlyDictionary<string, NodeSelectionSnapshot> snapshots)
        {
            if (snapshots == null || snapshots.Count == 0)
            {
                return;
            }

            foreach (var node in targetNodes)
            {
                if (node == null || string.IsNullOrEmpty(node.path) || !snapshots.TryGetValue(node.path, out var snapshot))
                {
                    continue;
                }

                snapshot.ApplyTo(node);
            }
        }

        private sealed class NodeSelectionSnapshot
        {
            private readonly Type selectedComponentType;
            private readonly string selectedComponentTypeName;
            private readonly List<Type> selectedComponentTypes;
            private readonly List<string> selectedMethodNames;
            private readonly Dictionary<string, List<string>> selectedMethodNamesByComponentTypeName;

            public NodeSelectionSnapshot(UIBindNode node)
            {
                selectedComponentType = node.selectedComponentType;
                selectedComponentTypeName = node.selectedComponentTypeName;
                selectedComponentTypes = node.selectedComponentTypes.ToList();
                selectedMethodNames = node.selectedMethodNames.ToList();
                selectedMethodNamesByComponentTypeName = node.selectedMethodNamesByComponentTypeName
                    .ToDictionary(item => item.Key, item => item.Value.ToList());
            }

            public void ApplyTo(UIBindNode node)
            {
                node.selectedComponentType = selectedComponentType != null && node.componentTypes.Contains(selectedComponentType)
                    ? selectedComponentType
                    : null;
                node.selectedComponentTypeName = node.selectedComponentType != null ? selectedComponentTypeName : null;

                node.selectedComponentTypes.Clear();
                foreach (var type in selectedComponentTypes.Where(type => node.componentTypes.Contains(type)))
                {
                    node.selectedComponentTypes.Add(type);
                }

                node.selectedMethodNames.Clear();
                node.selectedMethodNames.AddRange(selectedMethodNames);

                node.selectedMethodNamesByComponentTypeName.Clear();
                foreach (var item in selectedMethodNamesByComponentTypeName)
                {
                    if (node.componentTypes.Any(type => type.FullName == item.Key))
                    {
                        node.selectedMethodNamesByComponentTypeName[item.Key] = item.Value.ToList();
                    }
                }

                if (selectedComponentTypeName == UIBindComponentWindowBridge.MixedChoice &&
                    node.selectedComponentTypes.Count > 0)
                {
                    node.selectedComponentTypeName = UIBindComponentWindowBridge.MixedChoice;
                    return;
                }

                if (node.selectedComponentTypes.Count > 1)
                {
                    node.selectedComponentTypeName = UIBindComponentWindowBridge.MixedChoice;
                }
            }
        }
    }
}
