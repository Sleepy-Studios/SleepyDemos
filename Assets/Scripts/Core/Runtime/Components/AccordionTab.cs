using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    [Serializable]
    public sealed class AccordionTabData
    {
        /// 业务标识。组件本身只保存并在 GetCurrentId 中返回，不参与排序。
        public int Id;

        /// 页签显示文案。
        public string Desc;

        /// 页签图标 Sprite 资源路径；为空时清空图标。
        public string Image;

        /// 二级页签数据。为空时当前数据本身作为叶子页签。
        public List<AccordionTabData> Children = new List<AccordionTabData>();
    }

    public sealed class AccordionTab : MonoBehaviour
    {
        [SerializeField] private GameObject firstLevelPrefab;
        [SerializeField] private GameObject secondLevelPrefab;
        [SerializeField] private Transform parent;
        [SerializeField] private bool isNotRemoveBeforeListen;

        private readonly List<TabGroup> groups = new List<TabGroup>();
        private readonly List<LeafInfo> leafInfos = new List<LeafInfo>();
        private int currentIndex = -1;
        private bool isInitializing;
        private bool canCollapseFirstLevel;
        private TabGroup expandedGroup;

        /// 叶子页签选中前拦截回调。返回 false 时阻止本次选择和通知。
        public Func<int, bool> TryClick { get; set; }

        /// 叶子页签选中事件，参数为扁平化后的叶子索引。
        public event Action<int> OnClick;

        /// 当前选中的叶子索引；未选中时为 -1。
        public int Index => currentIndex;

        /// 当前叶子页签数量。
        public int Count => leafInfos.Count;

        /// <summary>
        /// 注册叶子页签点击回调。回调参数为扁平化后的叶子索引。
        /// </summary>
        /// <param name="onTabClick">需要追加的叶子页签点击回调。</param>
        public void Register(Action<int> onTabClick)
        {
            OnClick += onTabClick;
        }

        /// <summary>
        /// 取消注册叶子页签点击回调。
        /// </summary>
        /// <param name="onTabClick">需要移除的叶子页签点击回调。</param>
        public void Unregister(Action<int> onTabClick)
        {
            OnClick -= onTabClick;
        }

        /// <summary>
        /// 初始化两级手风琴 Tab；带子项的一级负责展开，最终选中和回调均落在叶子页签索引。
        /// </summary>
        /// <param name="data">手风琴数据；一级有 Children 时 Children 作为叶子，否则一级本身作为叶子。</param>
        /// <param name="initLeafIndex">初始化后选中的叶子索引；非法索引不会触发回调。</param>
        /// <param name="notify">初始化选中时是否触发 <see cref="OnClick"/>。</param>
        /// <param name="action">初始化完成回调；在项创建和初始选择后触发。</param>
        /// <param name="canCollapseFirstLevel">点击已展开的一级页签时是否允许收起。</param>
        /// <param name="isAsync">是否逐帧创建页签并异步加载图标。</param>
        public void Init(
            IList<AccordionTabData> data,
            int initLeafIndex = 0,
            bool notify = true,
            Action action = null,
            bool canCollapseFirstLevel = false,
            bool isAsync = false)
        {
            if (isAsync)
            {
                InitAsyncInternal(data, initLeafIndex, notify, action, canCollapseFirstLevel).Forget();
                return;
            }

            InitImmediate(data, initLeafIndex, notify, action, canCollapseFirstLevel);
        }

        private void InitImmediate(
            IList<AccordionTabData> data,
            int initLeafIndex,
            bool notify,
            Action action,
            bool canCollapseFirstLevel)
        {
            if (isInitializing)
            {
                Debug.LogWarning($"[AccordionTab] {name} 正在初始化，忽略重复请求。");
                return;
            }

            try
            {
                isInitializing = true;
                this.canCollapseFirstLevel = canCollapseFirstLevel;
                ClearGroups();

                parent = parent != null ? parent : transform;
                if (firstLevelPrefab == null)
                {
                    Debug.LogError($"[AccordionTab] {name} 缺少一级 Tab 模板。");
                    return;
                }

                if (data != null)
                {
                    for (int i = 0; i < data.Count; i++)
                    {
                        CreateGroup(data[i], i, false);
                    }
                }

                if (initLeafIndex >= 0 && initLeafIndex < leafInfos.Count)
                {
                    SetIndex(initLeafIndex, notify);
                }

                action?.Invoke();
            }
            finally
            {
                isInitializing = false;
            }
        }

        private async UniTaskVoid InitAsyncInternal(
            IList<AccordionTabData> data,
            int initLeafIndex,
            bool notify,
            Action action,
            bool canCollapseFirstLevel)
        {
            if (isInitializing)
            {
                Debug.LogWarning($"[AccordionTab] {name} 正在初始化，忽略重复请求。");
                return;
            }

            try
            {
                isInitializing = true;
                this.canCollapseFirstLevel = canCollapseFirstLevel;
                ClearGroups();

                parent = parent != null ? parent : transform;
                if (firstLevelPrefab == null)
                {
                    Debug.LogError($"[AccordionTab] {name} 缺少一级 Tab 模板。");
                    return;
                }

                if (data != null)
                {
                    for (int i = 0; i < data.Count; i++)
                    {
                        CreateGroup(data[i], i, true);
                        await UniTask.Yield();
                        if (this == null || gameObject == null)
                        {
                            return;
                        }
                    }
                }

                if (initLeafIndex >= 0 && initLeafIndex < leafInfos.Count)
                {
                    SetIndex(initLeafIndex, notify);
                }

                action?.Invoke();
            }
            finally
            {
                isInitializing = false;
            }
        }

        /// <summary>
        /// 选中指定叶子页签。
        /// </summary>
        /// <param name="leafIndex">目标叶子索引；非法索引会被忽略。</param>
        /// <param name="notify">是否触发 <see cref="OnClick"/>。</param>
        public void SetIndex(int leafIndex, bool notify = true)
        {
            SelectLeaf(leafIndex, notify);
        }

        /// 清空当前选中态，不触发回调。
        public void UnSetIndex()
        {
            if (currentIndex < 0 || currentIndex >= leafInfos.Count)
            {
                return;
            }

            var current = leafInfos[currentIndex];
            SetItemSelected(current.Item, false);
            SetItemSelected(current.Group.FirstItem, false);
            expandedGroup = null;
            currentIndex = -1;
        }

        /// 对当前选中叶子页签主动触发一次回调，不改变当前索引。
        public void ExecuteEvent()
        {
            if (currentIndex < 0 || currentIndex >= leafInfos.Count)
            {
                return;
            }

            OnClick?.Invoke(currentIndex);
            SelectLeaf(currentIndex, false);
        }

        /// <summary>
        /// 获取指定叶子索引对应的数据。
        /// </summary>
        /// <param name="leafIndex">扁平化叶子索引。</param>
        /// <returns>叶子数据；索引非法时返回 null。</returns>
        public AccordionTabData GetLeafData(int leafIndex)
        {
            return leafIndex >= 0 && leafIndex < leafInfos.Count ? leafInfos[leafIndex].Data : null;
        }

        /// 获取当前选中的叶子数据；未选中时返回 null。
        public AccordionTabData GetCurrentData()
        {
            return GetLeafData(currentIndex);
        }

        /// 获取当前选中叶子的业务 Id；未选中时返回 0。
        public int GetCurrentId()
        {
            return GetCurrentData()?.Id ?? 0;
        }

        private void CreateGroup(AccordionTabData data, int groupIndex, bool isAsync)
        {
            data = data ?? new AccordionTabData();
            var groupRoot = new GameObject($"AccordionGroup{groupIndex + 1}", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            groupRoot.transform.SetParent(parent, false);
            ConfigureVerticalLayout(groupRoot.GetComponent<VerticalLayoutGroup>());
            ConfigureContentSizeFitter(groupRoot.GetComponent<ContentSizeFitter>());

            var firstItem = Instantiate(firstLevelPrefab, groupRoot.transform);
            firstItem.name = $"FirstLevelTab{groupIndex + 1}";
            firstItem.SetActive(true);
            InitItem(firstItem, data, isAsync);

            var subRoot = new GameObject("SubRoot", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            subRoot.transform.SetParent(groupRoot.transform, false);
            ConfigureVerticalLayout(subRoot.GetComponent<VerticalLayoutGroup>());
            ConfigureContentSizeFitter(subRoot.GetComponent<ContentSizeFitter>());

            var group = new TabGroup
            {
                Data = data,
                Root = groupRoot,
                FirstItem = firstItem,
                SubRoot = subRoot
            };
            groups.Add(group);

            if (data.Children != null && data.Children.Count > 0)
            {
                if (secondLevelPrefab == null)
                {
                    Debug.LogError($"[AccordionTab] {name} 缺少二级 Tab 模板。");
                    subRoot.SetActive(false);
                    return;
                }

                RegisterClick(firstItem, () =>
                {
                    if (group.LeafIndices.Count <= 0)
                    {
                        return;
                    }

                    if (canCollapseFirstLevel && expandedGroup == group)
                    {
                        CollapseGroup(group);
                        return;
                    }

                    if (IsCurrentLeafInGroup(group))
                    {
                        ExpandCurrentGroup(group);
                        return;
                    }

                    SelectLeaf(group.LeafIndices[0], true);
                });

                for (int i = 0; i < data.Children.Count; i++)
                {
                    var childData = data.Children[i] ?? new AccordionTabData();
                    var childItem = Instantiate(secondLevelPrefab, subRoot.transform);
                    childItem.name = $"SecondLevelTab{i + 1}";
                    childItem.SetActive(true);
                    InitItem(childItem, childData, isAsync);

                    var leafIndex = leafInfos.Count;
                    group.SecondItems.Add(childItem);
                    group.LeafIndices.Add(leafIndex);
                    leafInfos.Add(new LeafInfo
                    {
                        Data = childData,
                        Group = group,
                        Item = childItem
                    });

                    RegisterClick(childItem, () => SelectLeaf(leafIndex, true));
                }
            }
            else
            {
                var leafIndex = leafInfos.Count;
                group.LeafIndices.Add(leafIndex);
                leafInfos.Add(new LeafInfo
                {
                    Data = data,
                    Group = group,
                    Item = firstItem
                });

                RegisterClick(firstItem, () => SelectLeaf(leafIndex, true));
            }

            subRoot.SetActive(false);
        }

        private bool SelectLeaf(int leafIndex, bool notify)
        {
            if (leafIndex < 0 || leafIndex >= leafInfos.Count)
            {
                return false;
            }

            if (TryClick != null && !TryClick.Invoke(leafIndex))
            {
                return false;
            }

            currentIndex = leafIndex;
            var selected = leafInfos[leafIndex];
            expandedGroup = selected.Group.HasChildren ? selected.Group : null;
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var expanded = group == expandedGroup && group.HasChildren;
                group.SubRoot.SetActive(expanded);
                SetItemSelected(group.FirstItem, group == selected.Group && group.HasChildren);
                for (int j = 0; j < group.SecondItems.Count; j++)
                {
                    SetItemSelected(group.SecondItems[j], false);
                }
            }

            SetItemSelected(selected.Item, true);
            if (notify)
            {
                OnClick?.Invoke(leafIndex);
            }

            return true;
        }

        private bool IsCurrentLeafInGroup(TabGroup group)
        {
            return currentIndex >= 0 && currentIndex < leafInfos.Count && leafInfos[currentIndex].Group == group;
        }

        private void ExpandCurrentGroup(TabGroup group)
        {
            if (group == null || !IsCurrentLeafInGroup(group))
            {
                return;
            }

            expandedGroup = group;
            for (int i = 0; i < groups.Count; i++)
            {
                var item = groups[i];
                var expanded = item == group && item.HasChildren;
                item.SubRoot.SetActive(expanded);
                SetItemSelected(item.FirstItem, item == group);
                for (int j = 0; j < item.SecondItems.Count; j++)
                {
                    SetItemSelected(item.SecondItems[j], false);
                }
            }

            SetItemSelected(leafInfos[currentIndex].Item, true);
        }

        private void CollapseGroup(TabGroup group)
        {
            if (group == null || expandedGroup != group)
            {
                return;
            }

            expandedGroup = null;
            group.SubRoot.SetActive(false);
            if (currentIndex >= 0 && currentIndex < leafInfos.Count)
            {
                SetItemSelected(group.FirstItem, leafInfos[currentIndex].Group == group);
            }
        }

        private static void ConfigureVerticalLayout(VerticalLayoutGroup layout)
        {
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        private static void ConfigureContentSizeFitter(ContentSizeFitter fitter)
        {
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void InitItem(GameObject item, AccordionTabData data, bool isAsync)
        {
            if (!string.IsNullOrEmpty(data.Desc))
            {
                var text = item.GetComponentInChildren<Text>(true);
                if (text != null)
                {
                    text.text = data.Desc;
                    text.raycastTarget = false;
                }
            }

            SetItemImage(item, data?.Image, isAsync);
            SetItemSelected(item, false);
        }

        private void SetItemImage(GameObject item, string imagePath, bool isAsync)
        {
            if (item == null)
            {
                return;
            }

            var imageLoader = FindUIImageLoader(item);
            if (imageLoader == null)
            {
                if (!string.IsNullOrEmpty(imagePath))
                {
                    Debug.LogWarning($"[AccordionTab] {item.name} 未找到 UIImageLoader，图片路径已忽略: {imagePath}");
                }

                return;
            }

            if (string.IsNullOrEmpty(imagePath))
            {
                imageLoader.Clear();
                return;
            }

            imageLoader.SetImage(imagePath, true, isAsync);
        }

        private static UIImageLoader FindUIImageLoader(GameObject item)
        {
            if (item == null)
            {
                return null;
            }

            return item.GetComponentInChildren<UIImageLoader>(true);
        }

        private void RegisterClick(GameObject item, Action action)
        {
            var button = item.GetComponentInChildren<Button>(true);
            if (button != null)
            {
                if (!isNotRemoveBeforeListen)
                {
                    button.onClick.RemoveAllListeners();
                }

                button.onClick.AddListener(() => action?.Invoke());
                return;
            }

            var toggle = item.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                toggle.group = null;
                if (!isNotRemoveBeforeListen)
                {
                    toggle.onValueChanged.RemoveAllListeners();
                }

                toggle.onValueChanged.AddListener(isOn =>
                {
                    if (isOn)
                    {
                        action?.Invoke();
                    }
                });
            }
        }

        private static void SetItemSelected(GameObject item, bool selected)
        {
            if (item == null)
            {
                return;
            }

            var uiState = item.GetComponent<UIState>();
            if (uiState != null)
            {
                uiState.SetState(selected ? "Selected" : "Normal");
            }

            var toggle = item.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                toggle.SetIsOnWithoutNotify(selected);
            }
        }

        private void ClearGroups()
        {
            currentIndex = -1;
            expandedGroup = null;
            leafInfos.Clear();
            for (int i = 0; i < groups.Count; i++)
            {
                DestroyObject(groups[i].Root);
            }

            groups.Clear();
        }

        private static void DestroyObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void OnDestroy()
        {
            isInitializing = false;
            ClearGroups();
            OnClick = null;
            TryClick = null;
        }

        private sealed class TabGroup
        {
            public AccordionTabData Data;
            public GameObject Root;
            public GameObject FirstItem;
            public GameObject SubRoot;
            public readonly List<GameObject> SecondItems = new List<GameObject>();
            public readonly List<int> LeafIndices = new List<int>();

            public bool HasChildren => SecondItems.Count > 0;
        }

        private sealed class LeafInfo
        {
            public AccordionTabData Data;
            public TabGroup Group;
            public GameObject Item;
        }
    }
}
