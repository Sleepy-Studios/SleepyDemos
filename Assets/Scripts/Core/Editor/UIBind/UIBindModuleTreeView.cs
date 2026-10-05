using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;

namespace Core.Editor.UIBind
{
    public sealed class UIBindModuleTreeItem : TreeViewItem
    {
        public UIBindModuleTreeItem(
            int id,
            int depth,
            string displayName,
            UIBindTreeItemKind kind,
            UIBindViewRecord record,
            string assetPath)
            : base(id, depth, displayName)
        {
            this.kind = kind;
            this.record = record;
            this.assetPath = assetPath;
        }

        public readonly UIBindTreeItemKind kind;
        public readonly UIBindViewRecord record;
        public readonly string assetPath;
    }

    public sealed class UIBindModuleTreeView : TreeView
    {
        private readonly Action<UIBindModuleTreeItem> itemActivated;
        private readonly List<UIBindViewRecord> records = new List<UIBindViewRecord>();
        private readonly GUIContent warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
        private readonly GUIContent prefabIcon = EditorGUIUtility.IconContent("Prefab Icon");
        private readonly GUIContent scriptIcon = EditorGUIUtility.IconContent("cs Script Icon");
        private int nextId;

        public UIBindModuleTreeView(TreeViewState state, Action<UIBindModuleTreeItem> itemActivated)
            : base(state)
        {
            this.itemActivated = itemActivated;
            rowHeight = 20f;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            Reload();
        }

        public void ReloadRecords(IEnumerable<UIBindViewRecord> newRecords)
        {
            records.Clear();
            if (newRecords != null)
            {
                records.AddRange(newRecords);
            }

            Reload();
            ExpandAll();
        }

        protected override TreeViewItem BuildRoot()
        {
            nextId = 1;
            var root = new TreeViewItem { id = 0, depth = -1, displayName = "Root" };
            var moduleHeader = NewItem(0, "Module", UIBindTreeItemKind.Root, null, string.Empty);
            root.AddChild(moduleHeader);

            foreach (var moduleGroup in records
                         .GroupBy(item => item.moduleName ?? string.Empty)
                         .OrderBy(group => group.Key))
            {
                var moduleName = string.IsNullOrEmpty(moduleGroup.Key) ? "[Module]" : $"[{moduleGroup.Key}]";
                var moduleItem = NewItem(1, moduleName, UIBindTreeItemKind.Module, null, string.Empty);
                moduleHeader.AddChild(moduleItem);

                foreach (var record in moduleGroup.OrderBy(item => item.viewName))
                {
                    var displayName = record.isValid || string.IsNullOrEmpty(record.validationMessage)
                        ? record.viewName
                        : $"{record.viewName}  —  {record.validationMessage}";
                    var viewItem = NewItem(2, displayName, UIBindTreeItemKind.View, record, string.Empty);
                    moduleItem.AddChild(viewItem);

                    if (record.hasViewScript)
                    {
                        viewItem.AddChild(NewItem(3, Path.GetFileNameWithoutExtension(record.viewScriptPath), UIBindTreeItemKind.Code, record, record.viewScriptPath));
                    }

                    if (record.hasComponentScript)
                    {
                        viewItem.AddChild(NewItem(3, Path.GetFileNameWithoutExtension(record.componentScriptPath), UIBindTreeItemKind.Code, record, record.componentScriptPath));
                    }

                    if (record.hasPrefab)
                    {
                        viewItem.AddChild(NewItem(3, "GameObject", UIBindTreeItemKind.Prefab, record, record.prefabPath));
                    }
                }
            }

            if (!root.hasChildren)
            {
                root.children = new List<TreeViewItem>();
            }

            SetupDepthsFromParentsAndChildren(root);
            return root;
        }

        protected override bool DoesItemMatchSearch(TreeViewItem item, string search)
        {
            if (string.IsNullOrEmpty(search))
            {
                return true;
            }

            if (item is not UIBindModuleTreeItem mvcItem)
            {
                return base.DoesItemMatchSearch(item, search);
            }

            return ContainsSearch(mvcItem.displayName, search) ||
                   ContainsSearch(mvcItem.record?.address, search) ||
                   ContainsSearch(mvcItem.assetPath, search);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (args.item is not UIBindModuleTreeItem item)
            {
                base.RowGUI(args);
                return;
            }

            var rowRect = args.rowRect;
            var iconRect = rowRect;
            iconRect.x += GetContentIndent(item);
            iconRect.width = 18f;

            var labelRect = rowRect;
            labelRect.x = iconRect.xMax + 2f;
            labelRect.xMax -= 22f;

            var icon = GetIcon(item);
            if (icon?.image != null)
            {
                GUI.Label(iconRect, icon);
            }

            using (new EditorGUI.DisabledScope(item.kind == UIBindTreeItemKind.Root))
            {
                var style = item.kind == UIBindTreeItemKind.Module || item.kind == UIBindTreeItemKind.View || item.kind == UIBindTreeItemKind.Root
                    ? EditorStyles.boldLabel
                    : EditorStyles.label;
                EditorGUI.LabelField(labelRect, item.displayName, style);
            }

            if (ShouldWarn(item))
            {
                var warnRect = rowRect;
                warnRect.x = warnRect.xMax - 18f;
                warnRect.width = 18f;
                GUI.Label(warnRect, warningIcon);
            }
        }

        protected override void DoubleClickedItem(int id)
        {
            itemActivated?.Invoke(FindItem(id, rootItem) as UIBindModuleTreeItem);
        }

        protected override void SingleClickedItem(int id)
        {
            itemActivated?.Invoke(FindItem(id, rootItem) as UIBindModuleTreeItem);
        }

        private UIBindModuleTreeItem NewItem(
            int depth,
            string displayName,
            UIBindTreeItemKind kind,
            UIBindViewRecord record,
            string assetPath)
        {
            return new UIBindModuleTreeItem(nextId++, depth, displayName, kind, record, assetPath);
        }

        private GUIContent GetIcon(UIBindModuleTreeItem item)
        {
            return item.kind switch
            {
                UIBindTreeItemKind.Prefab => prefabIcon,
                UIBindTreeItemKind.Code => scriptIcon,
                _ => null
            };
        }

        private static bool ShouldWarn(UIBindModuleTreeItem item)
        {
            return item.kind == UIBindTreeItemKind.View && item.record != null && !item.record.isValid;
        }

        private static bool ContainsSearch(string value, string search)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(search) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
