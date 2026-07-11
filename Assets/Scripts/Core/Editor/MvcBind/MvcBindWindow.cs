using System.Collections.Generic;
using System.IO;
using System.Linq;
using Core.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;

namespace Core.Editor.MvcBind
{
    public sealed class MvcBindWindow : EditorWindow
    {
        private readonly List<MvcBindViewRecord> viewRecords = new List<MvcBindViewRecord>();
        private readonly MvcBindSettings settings = new MvcBindSettings();
        private VisualElement bindPanel;
        private TextField folderField;
        private TextField searchField;
        private MvcBindModuleTreeView moduleTree;
        private IMGUIContainer moduleTreeContainer;
        private GameObject targetPrefabRoot;
        private static MvcBindWindow activeWindow;

        [MenuItem("Tools/UI Framework/MvcBind")]
        public static void Open()
        {
            GetWindow<MvcBindWindow>("MvcBind");
        }

        private void OnEnable()
        {
            activeWindow = this;
            PrefabStage.prefabStageOpened += OnPrefabStageOpened;
            PrefabStage.prefabStageClosing += OnPrefabStageClosing;
            BuildUI();
            RefreshFromSelection();
            RefreshModuleList();
        }

        private void OnDisable()
        {
            if (activeWindow == this)
            {
                activeWindow = null;
            }

            PrefabStage.prefabStageOpened -= OnPrefabStageOpened;
            PrefabStage.prefabStageClosing -= OnPrefabStageClosing;
        }

        private void BuildUI()
        {
            rootVisualElement.Clear();

            bindPanel = new VisualElement { style = { paddingLeft = 4, paddingRight = 4, paddingTop = 4, paddingBottom = 4 } };
            rootVisualElement.Add(bindPanel);

            folderField = CreateTextField("Module", settings.moduleName, value =>
            {
                settings.moduleName = value;
                settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings.moduleName, settings.viewName);
            });
            bindPanel.Add(folderField);

            bindPanel.Add(CreateToggle("HotFix", settings.isHotfix, value => settings.isHotfix = value));
            bindPanel.Add(CreateToggle("Async", settings.isAsync, value => settings.isAsync = value));
            bindPanel.Add(CreateToggle("EnableOnInit", settings.enableOnInit, value => settings.enableOnInit = value));
            bindPanel.Add(CreateToggle("DestroyOnHide", settings.destroyOnHide, value => settings.destroyOnHide = value));

            var typeField = new EnumField("Type", settings.viewType);
            typeField.RegisterValueChangedCallback(evt => settings.viewType = (ViewType)evt.newValue);
            bindPanel.Add(typeField);

            var viewModeField = new EnumField("ViewMode", settings.viewMode);
            viewModeField.RegisterValueChangedCallback(evt => settings.viewMode = (UIViewMode)evt.newValue);
            bindPanel.Add(viewModeField);

            var layerField = new EnumField("Level", settings.layer);
            layerField.RegisterValueChangedCallback(evt => settings.layer = (UILayer)evt.newValue);
            bindPanel.Add(layerField);

            var maskField = new EnumField("MaskType", settings.mask);
            maskField.RegisterValueChangedCallback(evt => settings.mask = (MaskType)evt.newValue);
            bindPanel.Add(maskField);

            bindPanel.Add(CreatePopup("UI Transition", settings.uiTransitionType, GetTypeChoices<IUITransition>(), value => settings.uiTransitionType = value));
            bindPanel.Add(CreateTextField("World Transition Key", settings.worldTransitionKey, value => settings.worldTransitionKey = value));

            var createRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            createRow.Add(new Button(CreateFromHierarchy) { text = "Create", style = { width = 70 } });
            bindPanel.Add(createRow);

            searchField = new TextField("Search") { value = string.Empty };
            searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            rootVisualElement.Add(searchField);

            moduleTree = new MvcBindModuleTreeView(new TreeViewState(), OnModuleTreeItemActivated);
            moduleTree.ReloadRecords(viewRecords);
            moduleTreeContainer = new IMGUIContainer(DrawModuleTree)
            {
                style = { flexGrow = 1 }
            };
            rootVisualElement.Add(moduleTreeContainer);

            var footer = new Button(RefreshModuleList) { text = "Refresh Cache" };
            rootVisualElement.Add(footer);

            UpdateModeVisibility();
        }

        private static TextField CreateTextField(string label, string value, System.Action<string> changed)
        {
            var field = new TextField(label) { value = value };
            field.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return field;
        }

        private static Toggle CreateToggle(string label, bool value, System.Action<bool> changed)
        {
            var toggle = new Toggle(label) { value = value };
            toggle.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return toggle;
        }

        private static PopupField<string> CreatePopup(string label, string value, List<string> choices, System.Action<string> changed)
        {
            var popup = new PopupField<string>(label, choices, Mathf.Max(0, choices.IndexOf(value)));
            popup.RegisterValueChangedCallback(evt => changed(evt.newValue));
            return popup;
        }

        private void RefreshModuleList()
        {
            viewRecords.Clear();
            if (Directory.Exists(MvcBindToolConfig.ModuleRoot))
            {
                viewRecords.AddRange(BuildViewRecords(MvcBindToolConfig.ModuleRoot));
            }

            moduleTree?.ReloadRecords(viewRecords);
            ApplyFilter();
        }

        private void DrawModuleTree()
        {
            if (moduleTree == null)
            {
                return;
            }

            var rect = GUILayoutUtility.GetRect(0, 100000, 0, 100000, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            moduleTree.OnGUI(rect);
        }

        private void OnModuleTreeItemActivated(MvcBindModuleTreeItem item)
        {
            if (item == null || string.IsNullOrEmpty(item.assetPath))
            {
                return;
            }

            if (item.kind == MvcBindTreeItemKind.Code)
            {
                OpenAsset(item.assetPath);
                return;
            }

            if (item.kind == MvcBindTreeItemKind.Prefab)
            {
                PingAsset(item.assetPath);
            }
        }

        private static void PingAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning($"MvcBind 找不到资源：{assetPath}");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        private static void OpenAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
            {
                Debug.LogWarning($"MvcBind 找不到资源：{assetPath}");
                return;
            }

            AssetDatabase.OpenAsset(asset);
        }

        private void ApplyFilter()
        {
            if (moduleTree == null)
            {
                return;
            }

            moduleTree.searchString = searchField?.value ?? string.Empty;
            moduleTree.Reload();
            moduleTreeContainer?.MarkDirtyRepaint();
        }

        private void RefreshFromSelection()
        {
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            targetPrefabRoot = prefabStage != null ? prefabStage.prefabContentsRoot : null;
            UpdateModeVisibility();
            if (prefabStage == null)
            {
                return;
            }

            if (TryResolveCurrentPrefabPath(out var prefabPath))
            {
                settings.ApplyPrefabPath(prefabPath);
                settings.moduleName = ResolveModuleName(prefabPath, settings.address);
                settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings.moduleName, settings.viewName);
                folderField?.SetValueWithoutNotify(settings.moduleName);
            }
        }

        private void CreateFromHierarchy()
        {
            RefreshFromSelection();
            if (targetPrefabRoot == null)
            {
                Debug.LogWarning("MvcBind 需要在 Prefab Mode 中打开 Prefab，或选中一个 Prefab。");
                return;
            }

            var nodes = MvcBindHierarchyOverlay.Root == targetPrefabRoot
                ? MvcBindHierarchyOverlay.Nodes.ToList()
                : MvcPrefabScanner.Scan(targetPrefabRoot);

            if (MvcBindHierarchyOverlay.Root == targetPrefabRoot && nodes.Count == 0)
            {
                MvcBindHierarchyOverlay.ForceRefresh();
                nodes = MvcBindHierarchyOverlay.Nodes.ToList();
            }

            var components = MvcCodeGenerator.CollectComponents(nodes);
            if (components.Count == 0)
            {
                ShowNotification(new GUIContent("请先在 Hierarchy 勾选至少一个组件"));
                Debug.LogWarning("MvcBind 生成失败：请先在 Hierarchy 勾选至少一个要绑定的组件。");
                return;
            }

            if (!MvcBindComponentWindowBridge.GenerateAndBind(targetPrefabRoot, settings, nodes, true, out var path, out var message))
            {
                ShowNotification(new GUIContent(message));
                Debug.LogWarning(message);
                return;
            }

            Debug.Log($"MvcBind generated: {path}");
            RefreshModuleList();
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

        internal static string ResolveModuleName(string prefabPath, string address)
        {
            var records = BuildViewRecords(MvcBindToolConfig.ModuleRoot);
            var viewName = MvcBindPathUtility.ToViewClassName(prefabPath);
            var record = records.FirstOrDefault(item =>
                item.prefabPath == MvcBindPathUtility.NormalizeAssetPath(prefabPath) ||
                item.address == address ||
                item.viewName == viewName);
            return record != null ? record.moduleName : string.Empty;
        }

        private void OnPrefabStageOpened(PrefabStage stage)
        {
            targetPrefabRoot = stage.prefabContentsRoot;
            RefreshFromSelection();
            MvcBindHierarchyOverlay.ForceRefresh();
        }

        private void OnPrefabStageClosing(PrefabStage stage)
        {
            targetPrefabRoot = null;
            EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    RefreshFromSelection();
                }
            };
        }

        private void UpdateModeVisibility()
        {
            if (bindPanel == null)
            {
                return;
            }

            bindPanel.style.display = PrefabStageUtility.GetCurrentPrefabStage() != null
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        internal static List<MvcBindViewRecord> BuildViewRecords(string root)
        {
            var records = new Dictionary<string, MvcBindViewRecord>();
            foreach (var path in Directory.GetFiles(root, "*Component.cs", SearchOption.AllDirectories))
            {
                var normalized = path.Replace('\\', '/');
                var relative = normalized.Substring(root.Length).Trim('/');
                var firstSlash = relative.IndexOf('/');
                var module = firstSlash >= 0 ? relative.Substring(0, firstSlash) : MvcBindToolConfig.DefaultModuleName;
                var viewName = ResolveViewName(relative);
                var key = $"{module}/{viewName}";
                var record = GetOrCreateRecord(records, key, module, viewName);
                record.componentScriptPath = normalized;
                record.hasComponentScript = true;
                ApplyGeneratedMetadata(record, normalized);
                if (record.moduleName != module)
                {
                    records.Remove(key);
                    records[$"{record.moduleName}/{record.viewName}"] = record;
                }
            }

            foreach (var path in Directory.GetFiles(root, "*View.cs", SearchOption.AllDirectories))
            {
                if (path.EndsWith("Component.cs", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var normalized = path.Replace('\\', '/');
                var relative = normalized.Substring(root.Length).Trim('/');
                var firstSlash = relative.IndexOf('/');
                var module = firstSlash >= 0 ? relative.Substring(0, firstSlash) : MvcBindToolConfig.DefaultModuleName;
                var viewName = ResolveViewName(relative);
                var key = $"{module}/{viewName}";
                var record = GetOrCreateRecord(records, key, module, viewName);
                record.viewScriptPath = normalized;
                record.hasViewScript = true;
                ApplyGeneratedMetadata(record, normalized);
                if (record.moduleName != module)
                {
                    records.Remove(key);
                    records[$"{record.moduleName}/{record.viewName}"] = record;
                }
            }

            foreach (var prefabPath in AssetDatabase.FindAssets("t:Prefab", new[] { MvcBindPathUtility.DefaultUiPrefabRoot })
                         .Select(AssetDatabase.GUIDToAssetPath)
                         .Where(MvcBindPathUtility.IsPrefabAssetPath))
            {
                var address = MvcBindPathUtility.ToRuntimeAddress(prefabPath);
                var viewName = MvcBindPathUtility.ToViewClassName(prefabPath);
                var matched = records.Values.FirstOrDefault(record => record.address == address || record.viewName == viewName);
                if (matched == null)
                {
                    var module = MvcBindPathUtility.ToModuleName(address);
                    matched = GetOrCreateRecord(records, $"{module}/{viewName}", module, viewName);
                }

                matched.prefabPath = MvcBindPathUtility.NormalizeAssetPath(prefabPath);
                matched.address = address;
                matched.hasPrefab = true;
            }

            return records.Values.ToList();
        }

        private static string ResolveViewName(string relativePath)
        {
            var normalized = relativePath.Replace('\\', '/');
            var parts = normalized.Split('/');
            for (var i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "View" && i > 0)
                {
                    return parts[i - 1];
                }
            }

            return Path.GetFileNameWithoutExtension(normalized);
        }

        private static MvcBindViewRecord GetOrCreateRecord(Dictionary<string, MvcBindViewRecord> records, string key, string moduleName, string viewName)
        {
            if (records.TryGetValue(key, out var record))
            {
                return record;
            }

            record = new MvcBindViewRecord
            {
                moduleName = moduleName,
                viewName = viewName
            };
            records.Add(key, record);
            return record;
        }

        private static void ApplyGeneratedMetadata(MvcBindViewRecord record, string scriptPath)
        {
            if (!File.Exists(scriptPath))
            {
                return;
            }

            var source = File.ReadAllText(scriptPath, System.Text.Encoding.UTF8);
            var module = MatchAttributeValue(source, @"\[(?:Module|ModuleAttribute)\(""([^""]*)""\)\]");
            var address = MatchAttributeValue(source, @"\[(?:Source|SourceAttribute)\(""([^""]*)""\)\]");

            if (!string.IsNullOrEmpty(module))
            {
                record.moduleName = module;
            }

            if (!string.IsNullOrEmpty(address))
            {
                record.address = address;
                var prefabPath = $"Assets/{address}.prefab";
                if (File.Exists(prefabPath))
                {
                    record.prefabPath = prefabPath;
                    record.hasPrefab = true;
                }
            }

            if (string.IsNullOrEmpty(record.address))
            {
                record.address = MatchAttributeValue(source, @"Address\s*=>\s*""([^""]*)""");
                var prefabPath = $"Assets/{record.address}.prefab";
                if (!string.IsNullOrEmpty(record.address) && File.Exists(prefabPath))
                {
                    record.prefabPath = prefabPath;
                    record.hasPrefab = true;
                }
            }

            if (string.IsNullOrEmpty(record.moduleName) && !string.IsNullOrEmpty(record.address))
            {
                record.moduleName = MvcBindPathUtility.ToModuleName(record.address);
            }
        }

        private static string MatchAttributeValue(string source, string pattern)
        {
            var match = System.Text.RegularExpressions.Regex.Match(source, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static List<string> GetTypeChoices<T>()
        {
            var choices = new List<string> { "null" };
            foreach (var type in TypeCache.GetTypesDerivedFrom<T>())
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                choices.Add(type.FullName);
            }

            return choices;
        }

        internal static MvcBindSettings CreateSettingsForPrefabSave(GameObject prefabRoot, string prefabPath)
        {
            var source = activeWindow != null && activeWindow.targetPrefabRoot == prefabRoot
                ? activeWindow.settings
                : null;

            var result = source != null
                ? CopySettings(source)
                : new MvcBindSettings();

            result.ApplyPrefabPath(prefabPath);
            result.moduleName = source != null
                ? source.moduleName
                : ResolveModuleName(result.prefabPath, result.address);
            result.outputFolder = MvcBindPathUtility.ToOutputFolder(result.moduleName, result.viewName);
            return result;
        }

        private static MvcBindSettings CopySettings(MvcBindSettings source)
        {
            return new MvcBindSettings
            {
                prefabPath = source.prefabPath,
                moduleName = source.moduleName,
                viewName = source.viewName,
                namespaceName = source.namespaceName,
                outputFolder = source.outputFolder,
                address = source.address,
                viewType = source.viewType,
                layer = source.layer,
                viewMode = source.viewMode,
                mask = source.mask,
                isHotfix = source.isHotfix,
                isAsync = source.isAsync,
                enableOnInit = source.enableOnInit,
                destroyOnHide = source.destroyOnHide,
                uiTransitionType = source.uiTransitionType,
                worldTransitionKey = source.worldTransitionKey
            };
        }
    }
}
