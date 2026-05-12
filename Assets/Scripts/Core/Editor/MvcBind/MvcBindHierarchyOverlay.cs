using System;
using System.Collections.Generic;
using Core.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Core.Editor.MvcBind
{
    [InitializeOnLoad]
    public static class MvcBindHierarchyOverlay
    {
        private const float ToggleWidth = 18f;
        private const float MethodWidth = 18f;
        private const float ComponentWidth = 132f;

        private static readonly Dictionary<int, MvcBindNode> nodes = new Dictionary<int, MvcBindNode>();
        private static GameObject root;

        public static GameObject Root => root;
        public static IReadOnlyCollection<MvcBindNode> Nodes => nodes.Values;

        static MvcBindHierarchyOverlay()
        {
            EditorApplication.hierarchyWindowItemOnGUI += OnHierarchyGUI;
            PrefabStage.prefabStageOpened += _ => Refresh();
            PrefabStage.prefabStageClosing += _ => Clear();
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

        private static void DrawNode(MvcBindNode node, Rect selectionRect)
        {
            var xMax = Mathf.Min(selectionRect.xMax, EditorGUIUtility.currentViewWidth - 10f);
            var methodRect = new Rect(xMax - MethodWidth, selectionRect.y, MethodWidth, selectionRect.height);
            var componentRect = new Rect(methodRect.x - ComponentWidth - 2f, selectionRect.y, ComponentWidth, selectionRect.height);
            var toggleRect = new Rect(componentRect.x - ToggleWidth - 2f, selectionRect.y, ToggleWidth, selectionRect.height);

            var selected = node.selectedComponentType != null || node.selectedComponentTypes.Count > 0;
            var nextSelected = EditorGUI.Toggle(toggleRect, selected);
            if (nextSelected != selected)
            {
                if (!nextSelected)
                {
                    MvcBindComponentWindowBridge.ApplyComponentChoice(node, "None");
                }
                else
                {
                    var defaultChoice = node.componentTypes.Count > 0
                        ? MvcBindComponentWindowBridge.GetComponentDisplayName(node.componentTypes[0])
                        : "None";
                    MvcBindComponentWindowBridge.ApplyComponentChoice(node, defaultChoice);
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

            if (node.selectedComponentType != null && MvcCodeGenerator.GetRegisterMethods(node.selectedComponentType, node.name).Count > 0)
            {
                if (GUI.Button(methodRect, string.Empty, EditorStyles.popup))
                {
                    ShowMethodMenu(node, methodRect);
                }
            }
        }

        private static string GetSelectedLabel(MvcBindNode node)
        {
            if (node.selectedComponentTypes.Count > 1)
            {
                return "Mixed";
            }

            return node.selectedComponentType == null
                ? "None"
                : ObjectNames.NicifyVariableName(node.selectedComponentType.Name);
        }

        private static void ShowComponentMenu(MvcBindNode node, Rect rect)
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent("None"), node.selectedComponentType == null && node.selectedComponentTypes.Count == 0, () => ApplyAndRepaint(node, "None"));
            menu.AddItem(new GUIContent("Mixed"), node.selectedComponentTypes.Count > 1, () => ApplyAndRepaint(node, "Mixed"));
            foreach (var type in node.componentTypes)
            {
                var choice = MvcBindComponentWindowBridge.GetComponentDisplayName(type);
                menu.AddItem(new GUIContent(choice), node.selectedComponentType == type, () => ApplyAndRepaint(node, choice));
            }

            menu.DropDown(rect);
        }

        private static void ShowMethodMenu(MvcBindNode node, Rect rect)
        {
            var menu = new GenericMenu();
            var selectedMethods = GetSelectedMethodNames(node);
            menu.AddItem(new GUIContent("None"), selectedMethods.Count == 0, () =>
            {
                selectedMethods.Clear();
                EditorApplication.RepaintHierarchyWindow();
            });

            foreach (var method in MvcCodeGenerator.GetRegisterMethods(node.selectedComponentType, node.name))
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

        private static List<string> GetSelectedMethodNames(MvcBindNode node)
        {
            if (node.selectedComponentType != null &&
                node.selectedMethodNamesByComponentTypeName.TryGetValue(node.selectedComponentType.FullName, out var methods))
            {
                return methods;
            }

            return node.selectedMethodNames;
        }

        private static void ApplyAndRepaint(MvcBindNode node, string choice)
        {
            MvcBindComponentWindowBridge.ApplyComponentChoice(node, choice);
            EditorApplication.RepaintHierarchyWindow();
        }

        private static void Refresh()
        {
            nodes.Clear();
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            root = prefabStage?.prefabContentsRoot;
            if (root == null)
            {
                return;
            }

            foreach (var node in MvcPrefabScanner.Scan(root))
            {
                nodes[node.gameObject.GetInstanceID()] = node;
            }

            MvcBindComponentWindowBridge.RestoreComponentChoices(root, nodes.Values);
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
    }
}
