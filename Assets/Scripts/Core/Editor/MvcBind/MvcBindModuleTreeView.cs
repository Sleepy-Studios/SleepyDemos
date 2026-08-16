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

namespace Core.Editor.MvcBind
{
    public sealed class MvcBindModuleTreeItem : TreeViewItem
    {
        public MvcBindModuleTreeItem(
            int id,
            int depth,
            string displayName,
            MvcBindTreeItemKind kind,
            MvcBindViewRecord record,
            string assetPath)
            : base(id, depth, displayName)
        {
            this.kind = kind;
            this.record = record;
            this.assetPath = assetPath;
        }

        public readonly MvcBindTreeItemKind kind;
        public readonly MvcBindViewRecord record;
        public readonly string assetPath;
    }

    public sealed class MvcBindModuleTreeView : TreeView
    {
        private readonly Action<MvcBindModuleTreeItem> itemActivated;
        private readonly List<MvcBindViewRecord> records = new List<MvcBindViewRecord>();
        private readonly GUIContent warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml");
        private readonly GUIContent prefabIcon = EditorGUIUtility.IconContent("Prefab Icon");
        private readonly GUIContent scriptIcon = EditorGUIUtility.IconContent("cs Script Icon");
        private int nextId;

        public MvcBindModuleTreeView(TreeViewState state, Action<MvcBindModuleTreeItem> itemActivated)
            : base(state)
        {
            this.itemActivated = itemActivated;
            rowHeight = 20f;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            Reload();
        }

        public void ReloadRecords(IEnumerable<MvcBindViewRecord> newRecords)
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
            var moduleHeader = NewItem(0, "Module", MvcBindTreeItemKind.Root, null, string.Empty);
            root.AddChild(moduleHeader);

            foreach (var moduleGroup in records
                         .GroupBy(item => item.moduleName ?? string.Empty)
                         .OrderBy(group => group.Key))
            {
                var moduleName = string.IsNullOrEmpty(moduleGroup.Key) ? "[Module]" : $"[{moduleGroup.Key}]";
                var moduleItem = NewItem(1, moduleName, MvcBindTreeItemKind.Module, null, string.Empty);
                moduleHeader.AddChild(moduleItem);

                foreach (var record in moduleGroup.OrderBy(item => item.viewName))
                {
                    var displayName = record.isValid || string.IsNullOrEmpty(record.validationMessage)
                        ? record.viewName
                        : $"{record.viewName}  —  {record.validationMessage}";
                    var viewItem = NewItem(2, displayName, MvcBindTreeItemKind.View, record, string.Empty);
                    moduleItem.AddChild(viewItem);

                    if (record.hasViewScript)
                    {
                        viewItem.AddChild(NewItem(3, Path.GetFileNameWithoutExtension(record.viewScriptPath), MvcBindTreeItemKind.Code, record, record.viewScriptPath));
                    }

                    if (record.hasComponentScript)
                    {
                        viewItem.AddChild(NewItem(3, Path.GetFileNameWithoutExtension(record.componentScriptPath), MvcBindTreeItemKind.Code, record, record.componentScriptPath));
                    }

                    if (record.hasPrefab)
                    {
                        viewItem.AddChild(NewItem(3, "GameObject", MvcBindTreeItemKind.Prefab, record, record.prefabPath));
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

            if (item is not MvcBindModuleTreeItem mvcItem)
            {
                return base.DoesItemMatchSearch(item, search);
            }

            return ContainsSearch(mvcItem.displayName, search) ||
                   ContainsSearch(mvcItem.record?.address, search) ||
                   ContainsSearch(mvcItem.assetPath, search);
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            if (args.item is not MvcBindModuleTreeItem item)
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

            using (new EditorGUI.DisabledScope(item.kind == MvcBindTreeItemKind.Root))
            {
                var style = item.kind == MvcBindTreeItemKind.Module || item.kind == MvcBindTreeItemKind.View || item.kind == MvcBindTreeItemKind.Root
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
            itemActivated?.Invoke(FindItem(id, rootItem) as MvcBindModuleTreeItem);
        }

        protected override void SingleClickedItem(int id)
        {
            itemActivated?.Invoke(FindItem(id, rootItem) as MvcBindModuleTreeItem);
        }

        private MvcBindModuleTreeItem NewItem(
            int depth,
            string displayName,
            MvcBindTreeItemKind kind,
            MvcBindViewRecord record,
            string assetPath)
        {
            return new MvcBindModuleTreeItem(nextId++, depth, displayName, kind, record, assetPath);
        }

        private GUIContent GetIcon(MvcBindModuleTreeItem item)
        {
            return item.kind switch
            {
                MvcBindTreeItemKind.Prefab => prefabIcon,
                MvcBindTreeItemKind.Code => scriptIcon,
                _ => null
            };
        }

        private static bool ShouldWarn(MvcBindModuleTreeItem item)
        {
            return item.kind == MvcBindTreeItemKind.View && item.record != null && !item.record.isValid;
        }

        private static bool ContainsSearch(string value, string search)
        {
            return !string.IsNullOrEmpty(value) &&
                   !string.IsNullOrEmpty(search) &&
                   value.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
