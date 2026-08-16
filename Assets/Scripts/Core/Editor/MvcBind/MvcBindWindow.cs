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
        private VisualElement customOutputRow;
        private TextField customOutputField;
        private TextField searchField;
        private Toggle customOutputToggle;
        private Label refreshStatusLabel;
        private TwoPaneSplitView splitView;
        private ScrollView configurationPane;
        private MvcBindModuleTreeView moduleTree;
        private IMGUIContainer moduleTreeContainer;
        private GameObject targetPrefabRoot;
        private bool isRefreshingIndex;
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
            RefreshModuleList();
            RefreshFromSelection();
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
            rootVisualElement.style.flexGrow = 1;

            splitView = new TwoPaneSplitView(0, 340f, TwoPaneSplitViewOrientation.Vertical)
            {
                style = { flexGrow = 1 }
            };
            rootVisualElement.Add(splitView);

            configurationPane = new ScrollView(ScrollViewMode.Vertical)
            {
                style = { minHeight = 120, flexGrow = 1 }
            };
            splitView.Add(configurationPane);

            bindPanel = new VisualElement
            {
                style =
                {
                    paddingLeft = 4,
                    paddingRight = 4,
                    paddingTop = 4,
                    paddingBottom = 4,
                    flexShrink = 0
                }
            };
            configurationPane.Add(bindPanel);

            folderField = CreateTextField("Module", settings.moduleName, value =>
            {
                settings.moduleName = value;
                settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings);
            });
            bindPanel.Add(folderField);

            customOutputToggle = CreateToggle(
                "自定义 Module 输出目录",
                settings.useCustomModuleOutputDirectory,
                value =>
                {
                    settings.useCustomModuleOutputDirectory = value;
                    settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings);
                    UpdateCustomOutputDirectoryVisibility();
                });
            customOutputToggle.tooltip =
                "勾选后，所选目录会替代默认的 Assets/Scripts/Hotfix/Module/{Module}；生成时仍会自动追加 Prefab 名称和 View 子目录。";
            bindPanel.Add(customOutputToggle);

            customOutputRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row }
            };
            customOutputField = new TextField("输出目录")
            {
                value = settings.customModuleOutputDirectory,
                isReadOnly = true,
                tooltip = string.IsNullOrEmpty(settings.customModuleOutputDirectory)
                    ? "只能通过右侧按钮选择当前项目 Assets 目录内的文件夹。"
                    : settings.customModuleOutputDirectory
            };
            customOutputField.style.flexGrow = 1;
            customOutputField.style.flexShrink = 1;
            customOutputField.style.minWidth = 0;
            customOutputField.labelElement.style.minWidth = 60;
            customOutputField.labelElement.style.width = 60;
            customOutputRow.Add(customOutputField);
            var selectFolderButton = new Button(SelectCustomModuleOutputDirectory)
            {
                text = "选择文件夹",
                tooltip = "选择当前 Module 的代码输出目录；不需要手写路径。",
                style = { width = 100, flexShrink = 0, marginLeft = 4 }
            };
            customOutputRow.Add(selectFolderButton);
            bindPanel.Add(customOutputRow);

            var hotfixToggle = CreateToggle("HotFix", settings.isHotfix, value => settings.isHotfix = value);
            var asyncToggle = CreateToggle("Async", settings.isAsync, value => settings.isAsync = value);
            var enableOnInitToggle = CreateToggle("EnableOnInit", settings.enableOnInit, value => settings.enableOnInit = value);
            var destroyOnHideToggle = CreateToggle("DestroyOnHide", settings.destroyOnHide, value => settings.destroyOnHide = value);
            bindPanel.Add(hotfixToggle);
            bindPanel.Add(asyncToggle);
            bindPanel.Add(enableOnInitToggle);
            bindPanel.Add(destroyOnHideToggle);

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

            var uiTransitionField = CreatePopup("UI Transition", settings.uiTransitionType, GetUITransitionTypeChoices(), value => settings.uiTransitionType = value);
            var worldTransitionKeyField = CreateTextField("World Transition Key", settings.worldTransitionKey, value => settings.worldTransitionKey = value);
            bindPanel.Add(uiTransitionField);
            bindPanel.Add(worldTransitionKeyField);

            var createRow = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.FlexEnd } };
            createRow.Add(new Button(CreateFromHierarchy) { text = "Create", style = { width = 70 } });
            bindPanel.Add(createRow);

            var indexPane = new VisualElement
            {
                style = { minHeight = 120, flexGrow = 1 }
            };
            splitView.Add(indexPane);

            searchField = new TextField("Search") { value = string.Empty };
            searchField.RegisterValueChangedCallback(_ => ApplyFilter());
            indexPane.Add(searchField);

            moduleTree = new MvcBindModuleTreeView(new TreeViewState(), OnModuleTreeItemActivated);
            moduleTree.ReloadRecords(viewRecords);
            moduleTreeContainer = new IMGUIContainer(DrawModuleTree)
            {
                style = { flexGrow = 1 }
            };
            indexPane.Add(moduleTreeContainer);

            var footer = new VisualElement { style = { flexDirection = FlexDirection.Row, paddingTop = 2, paddingBottom = 2 } };
            footer.Add(new Button(RefreshModuleList)
            {
                text = "刷新绑定索引",
                tooltip = "重新扫描 Assets/LoadResources 中带 ComponentItemIndex 的 Prefab。"
            });
            refreshStatusLabel = new Label("尚未刷新") { style = { marginLeft = 8, unityTextAlign = TextAnchor.MiddleLeft } };
            footer.Add(refreshStatusLabel);
            indexPane.Add(footer);

            UpdateModeVisibility();
            UpdateCustomOutputDirectoryVisibility();
        }

        private void SelectCustomModuleOutputDirectory()
        {
            var initialDirectory = MvcBindPathUtility.IsValidCustomModuleOutputDirectory(
                settings.customModuleOutputDirectory)
                ? Path.GetFullPath(settings.customModuleOutputDirectory)
                : Application.dataPath;
            var selectedDirectory = EditorUtility.OpenFolderPanel(
                "选择自定义 Module 输出目录",
                initialDirectory,
                string.Empty);
            if (string.IsNullOrEmpty(selectedDirectory))
            {
                return;
            }

            if (!TryConvertToAssetFolder(selectedDirectory, out var assetFolder))
            {
                ShowNotification(new GUIContent("只能选择当前项目 Assets 内的目录"));
                Debug.LogWarning("MvcBind 自定义 Module 输出目录必须位于当前项目 Assets 内。");
                return;
            }

            settings.customModuleOutputDirectory = assetFolder;
            settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings);
            customOutputField?.SetValueWithoutNotify(assetFolder);
            if (customOutputField != null)
            {
                customOutputField.tooltip = assetFolder;
            }
        }

        internal static bool TryConvertToAssetFolder(string selectedDirectory, out string assetFolder)
        {
            assetFolder = string.Empty;
            if (string.IsNullOrWhiteSpace(selectedDirectory))
            {
                return false;
            }

            var assetsFullPath = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var selectedFullPath = Path.GetFullPath(selectedDirectory)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (string.Equals(selectedFullPath, assetsFullPath, System.StringComparison.OrdinalIgnoreCase))
            {
                assetFolder = "Assets";
                return true;
            }

            var assetsPrefix = assetsFullPath + Path.DirectorySeparatorChar;
            if (!selectedFullPath.StartsWith(assetsPrefix, System.StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            assetFolder = "Assets/" + selectedFullPath.Substring(assetsPrefix.Length).Replace('\\', '/');
            return true;
        }

        private void UpdateCustomOutputDirectoryVisibility()
        {
            if (customOutputRow != null)
            {
                customOutputRow.style.display = settings.useCustomModuleOutputDirectory
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
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
            if (isRefreshingIndex)
            {
                return;
            }

            isRefreshingIndex = true;
            try
            {
                var refreshedRecords = MvcBindIndexDiscovery.BuildViewRecords();
                viewRecords.Clear();
                viewRecords.AddRange(refreshedRecords);
                moduleTree?.ReloadRecords(viewRecords);
                ApplyFilter();

                var validCount = viewRecords.Count(record => record.isValid);
                var invalidCount = viewRecords.Count - validCount;
                if (refreshStatusLabel != null)
                {
                    refreshStatusLabel.text = $"有效 {validCount}，异常 {invalidCount}";
                }
            }
            catch (System.Exception exception)
            {
                if (refreshStatusLabel != null)
                {
                    refreshStatusLabel.text = "刷新失败，已保留上次结果";
                }
                Debug.LogError($"MvcBind 刷新绑定索引失败：{exception}");
            }
            finally
            {
                isRefreshingIndex = false;
            }
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
            if (item == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(item.assetPath))
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
                var record = viewRecords.FirstOrDefault(item =>
                    string.Equals(item.prefabPath, prefabPath, System.StringComparison.OrdinalIgnoreCase)) ??
                    MvcBindIndexDiscovery.FindRecordForPrefab(prefabPath);
                settings.ApplyPrefabPath(prefabPath);
                ApplyPrefabGenerationLocation(settings, record);
                folderField?.SetValueWithoutNotify(settings.moduleName);
                customOutputToggle?.SetValueWithoutNotify(settings.useCustomModuleOutputDirectory);
                customOutputField?.SetValueWithoutNotify(settings.customModuleOutputDirectory);
                if (customOutputField != null)
                {
                    customOutputField.tooltip = settings.customModuleOutputDirectory;
                }
                UpdateCustomOutputDirectoryVisibility();
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
            var records = MvcBindIndexDiscovery.BuildViewRecords();
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

            var isPrefabMode = PrefabStageUtility.GetCurrentPrefabStage() != null;
            bindPanel.style.display = isPrefabMode ? DisplayStyle.Flex : DisplayStyle.None;
            if (splitView == null)
            {
                return;
            }

            if (isPrefabMode)
            {
                splitView.UnCollapse();
            }
            else
            {
                splitView.CollapseChild(0);
            }
        }

        internal static List<MvcBindViewRecord> BuildViewRecords(string root)
        {
            return MvcBindIndexDiscovery.BuildViewRecords(root, MvcBindToolConfig.LoadResourcesRoot);
        }

        /// 刷新当前已打开的 MvcBind 窗口索引；窗口未打开时不执行扫描。
        public static void RefreshActiveIndex()
        {
            activeWindow?.RefreshModuleList();
        }

        internal static void ApplyPrefabGenerationLocation(MvcBindSettings target, MvcBindViewRecord record)
        {
            target.moduleName = record != null && record.isValid ? record.moduleName : string.Empty;
            target.useCustomModuleOutputDirectory = record != null && record.usesCustomModuleOutputDirectory;
            target.customModuleOutputDirectory = record?.moduleOutputDirectory ?? string.Empty;
            target.outputFolder = MvcBindPathUtility.ToOutputFolder(target);
        }

        internal static List<string> GetUITransitionTypeChoices()
        {
            return MvcBindTransitionTypePolicy.GetTypeChoices();
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
            result.outputFolder = MvcBindPathUtility.ToOutputFolder(result);
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
                useCustomModuleOutputDirectory = source.useCustomModuleOutputDirectory,
                customModuleOutputDirectory = source.customModuleOutputDirectory,
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
