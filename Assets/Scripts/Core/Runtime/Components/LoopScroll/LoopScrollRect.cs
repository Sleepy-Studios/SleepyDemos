using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public abstract class LoopScrollRect : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRect;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform viewport;
        [SerializeField] private GameObject itemPrefab;
        [SerializeField] private Vector2 defaultItemSize = new Vector2(100f, 100f);
        [SerializeField] private int visibleBuffer = 1;
        [SerializeField] private bool itemSnapEnable;

        private readonly List<LoopScrollItemRecord> shownRecords = new List<LoopScrollItemRecord>();
        private ILoopScrollItemSource itemSource;
        private Transform poolRoot;
        private IList currentList;
        private Action<ItemView, int> itemClick;
        private Action<ItemView, int> itemHide;
        private Action<ItemView, int> multiItemProvide;
        private Action<ItemView, int> multiItemReturn;
        private Action<ItemView, int> multiItemClick;
        private bool refreshWhenEnable;
        private int pendingIndex;
        private int currentFirstIndex;
        private int selectIndex = -1;
        private bool initialized;

        public int ItemTotalCount { get; private set; }
        public int TotalCount => ItemTotalCount;
        public int ShownItemCount => shownRecords.Count;
        public bool IsInfinite => ItemTotalCount < 0;
        public int SelectIndex => selectIndex;
        public int CurSnapNearestItemIndex { get; private set; } = -1;
        public bool ItemSnapEnable { get => itemSnapEnable; set => itemSnapEnable = value; }
        public bool SupportScrollBar { get; set; } = true;
        public bool IsDraging { get; private set; }
        public RectTransform ContainerTrans => content;
        public RectTransform ViewPortTrans => viewport;
        public ScrollRect ScrollRect => scrollRect;
        public Vector2 DefaultItemSize { get => defaultItemSize; set => defaultItemSize = value; }
        public int VisibleBuffer { get => visibleBuffer; set => visibleBuffer = Mathf.Max(0, value); }

        protected abstract bool IsVertical { get; }
        protected virtual int ContentConstraintCount => 1;

        /// <summary>
        /// 手动绑定 ScrollRect 结构。用于代码创建或测试场景；预制体上已序列化引用时无需调用。
        /// </summary>
        public void Bind(ScrollRect targetScrollRect, RectTransform targetContent, RectTransform targetViewport)
        {
            scrollRect = targetScrollRect;
            content = targetContent;
            viewport = targetViewport;
            initialized = false;
            EnsureInitialized();
        }

        /// <summary>
        /// 设置循环列表创建 Item GameObject 时使用的模板。
        /// </summary>
        public void SetItemPrefab(GameObject prefab)
        {
            itemPrefab = prefab;
        }

        /// <summary>
        /// 注册强类型 ItemView 数据填充回调。回调会在 item 进入可见区或刷新可见项时触发。
        /// </summary>
        public void RegisterItemSource<TView>(Action<TView, int> onProvide) where TView : ItemView, new()
        {
            var source = GetOrCreateSource<TView>();
            source.SetProvide(onProvide);
        }

        /// <summary>
        /// 注册非泛型 ItemView 数据填充回调，主要供 MvcBind 自动生成代码使用。
        /// </summary>
        public void RegisterItemSource(Action<ItemView, int> onProvide)
        {
            RegisterItemSource<ItemView>((view, index) => onProvide?.Invoke(view, index));
        }

        /// <summary>
        /// 注册多类型 item 数据填充回调。需配合 SetMultiListData 使用。
        /// </summary>
        public void RegisterMultiItemSource(Action<ItemView, int> onProvide)
        {
            multiItemProvide = onProvide;
            if (itemSource is LoopScrollMultiItemSource source)
            {
                source.SetProvide(onProvide);
            }
        }

        /// <summary>
        /// 注册多类型 item 回收隐藏回调。
        /// </summary>
        public void RegisterMultiItemHide(Action<ItemView, int> onReturn)
        {
            multiItemReturn = onReturn;
            if (itemSource is LoopScrollMultiItemSource source)
            {
                source.SetReturn(onReturn);
            }
        }

        /// <summary>
        /// 注册多类型 item 点击回调。
        /// </summary>
        public void RegisterMultiItemClick(Action<ItemView, int> onClick)
        {
            multiItemClick += onClick;
            if (itemSource is LoopScrollMultiItemSource source)
            {
                source.SetClick(InvokeMultiItemClick);
            }
        }

        /// <summary>
        /// 注册 item 回收隐藏回调。
        /// </summary>
        public void RegisterItemHide<TView>(Action<TView, int> onReturn) where TView : ItemView, new()
        {
            var source = GetOrCreateSource<TView>();
            source.SetReturn((view, index) =>
            {
                onReturn?.Invoke(view, index);
                itemHide?.Invoke(view, index);
            });
        }

        /// <summary>
        /// 注册 item 点击回调。
        /// </summary>
        public void RegisterItemClick<TView>(Action<TView, int> onClick) where TView : ItemView, new()
        {
            var source = GetOrCreateSource<TView>();
            source.SetClick((view, index) =>
            {
                SetSelectIndex(index);
                onClick?.Invoke(view, index);
                itemClick?.Invoke(view, index);
            });
        }

        /// <summary>
        /// 设置多预制体/多类型列表数据。itemTypeList 的每个元素表示对应 item 的类型 id。
        /// </summary>
        public void SetMultiListData(
            IList<int> itemTypeList,
            IDictionary<int, Type> itemTypeToViewType,
            IDictionary<int, GameObject> itemTypeToPrefab = null)
        {
            var source = GetOrCreateMultiSource();
            source.SetMultiListData(itemTypeList, itemTypeToViewType, itemTypeToPrefab);
            if (multiItemProvide != null)
            {
                source.SetProvide(multiItemProvide);
            }

            if (multiItemReturn != null)
            {
                source.SetReturn(multiItemReturn);
            }

            if (multiItemClick != null)
            {
                source.SetClick(InvokeMultiItemClick);
            }
        }

        /// <summary>
        /// 设置总数据。list 为 null 或空时清空可见项；itemCount 小于 0 时进入无限模式。
        /// </summary>
        public void SetTotalCount(
            IList list,
            int index = 0,
            bool async = false,
            bool refreshWhenDisable = false,
            bool animation = true,
            bool autoSelect = false,
            bool ignorePadding = false,
            bool isAdjustContentPos = false,
            bool forceRefill = false)
        {
            currentList = list;
            var nextCount = list == null ? 0 : list.Count;
            SetItemCountInternal(nextCount, index, refreshWhenDisable, autoSelect);
        }

        /// <summary>
        /// 设置 item 数量。传入负数表示无限模式。
        /// </summary>
        public void SetListItemCount(int itemCount, bool resetPos = true)
        {
            SetItemCountInternal(itemCount, resetPos ? 0 : currentFirstIndex, true, false);
        }

        /// <summary>
        /// 刷新当前可见 item，不清空池，也不改变滚动位置。
        /// </summary>
        public virtual void RefreshCells()
        {
            EnsureInitialized();
            for (int i = 0; i < shownRecords.Count; i++)
            {
                var index = currentFirstIndex + i;
                if (!IsValidIndex(index))
                {
                    continue;
                }

                BindRecord(shownRecords[i], index, i);
            }
        }

        /// <summary>
        /// 立即或按速度滚动到目标 item。speed 小于等于 0 时直接跳转。
        /// </summary>
        public virtual void ScrollToCell(
            int index,
            float speed,
            Action action = null,
            float xOffsetGap = 0f,
            float yOffsetGap = 0f,
            bool isReversDir = false)
        {
            if (!IsValidIndex(index))
            {
                action?.Invoke();
                return;
            }

            currentFirstIndex = ClampFirstIndex(index);
            ApplyContentPosition(currentFirstIndex, xOffsetGap, yOffsetGap);
            RebuildVisibleItems(currentFirstIndex);
            action?.Invoke();
        }

        /// <summary>
        /// 移动到指定 item，命名对齐 SuperScrollView 的 MovePanelToItemIndex。
        /// </summary>
        public void MovePanelToItemIndex(int itemIndex, float offset = 0f)
        {
            ScrollToCell(itemIndex, -1f, yOffsetGap: IsVertical ? offset : 0f, xOffsetGap: IsVertical ? 0f : offset);
        }

        /// <summary>
        /// 在指定时间内滚动到目标 item。当前实现先提供确定性跳转，后续可在此 API 内补间。
        /// </summary>
        public void ScrollToCellWithinTime(int index, float time, Action action = null, bool isReversDir = false)
        {
            ScrollToCell(index, time <= 0f ? -1f : 1f, action, isReversDir: isReversDir);
        }

        /// <summary>
        /// 设置 Snap 目标项。当前版本会立即定位到目标项。
        /// </summary>
        public void SetSnapTargetItemIndex(int itemIndex, float moveMaxAbsVec = -1f)
        {
            CurSnapNearestItemIndex = itemIndex;
            ScrollToCell(itemIndex, -1f);
        }

        /// <summary>
        /// 立即完成 Snap。当前版本 Snap 为同步定位，因此此方法只刷新最近项状态。
        /// </summary>
        public void FinishSnapImmediately()
        {
            ForceSnapUpdateCheck();
        }

        /// <summary>
        /// 强制刷新当前最近 Snap 项。
        /// </summary>
        public void ForceSnapUpdateCheck()
        {
            CurSnapNearestItemIndex = shownRecords.Count == 0 ? -1 : shownRecords[0].View.Index;
        }

        /// <summary>
        /// 设置选中项。目标不可见时会先跳转到目标项。
        /// </summary>
        public void SetSelectIndex(int index)
        {
            if (!IsValidIndex(index))
            {
                return;
            }

            selectIndex = index;
            if (itemSource == null || !itemSource.ContainsVisible(shownRecords, index))
            {
                ScrollToCell(index, -1f);
            }

            RefreshSelectedState();
        }

        /// <summary>
        /// 获取当前可见区内指定数据索引对应的 ItemView；不可见时返回 null。
        /// </summary>
        public ItemView GetShownItemByItemIndex(int itemIndex)
        {
            for (int i = 0; i < shownRecords.Count; i++)
            {
                if (shownRecords[i].View.Index == itemIndex)
                {
                    return shownRecords[i].View;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取当前第 index 个可见 ItemView。
        /// </summary>
        public ItemView GetShownItemByIndex(int index)
        {
            return index >= 0 && index < shownRecords.Count ? shownRecords[index].View : null;
        }

        /// <summary>
        /// 获取当前第一个可见项索引。
        /// </summary>
        public int GetFirstItem(out float offset)
        {
            offset = 0f;
            return shownRecords.Count == 0 ? -1 : shownRecords[0].View.Index;
        }

        /// <summary>
        /// 获取当前最后一个可见项索引。
        /// </summary>
        public int GetLastItem(out float offset)
        {
            offset = 0f;
            return shownRecords.Count == 0 ? -1 : shownRecords[^1].View.Index;
        }

        /// <summary>
        /// 重置列表位置，可选择是否回到首项。
        /// </summary>
        public void ResetListView(bool resetPos = true)
        {
            if (resetPos)
            {
                currentFirstIndex = 0;
                if (content != null)
                {
                    content.anchoredPosition = Vector2.zero;
                }
            }

            RebuildVisibleItems(currentFirstIndex);
        }

        /// <summary>
        /// 当指定 item 尺寸变化时刷新可见项布局。
        /// </summary>
        public void OnItemSizeChanged(int itemIndex)
        {
            if (GetShownItemByItemIndex(itemIndex) != null)
            {
                RefreshCells();
                UpdateContentSize();
            }
        }

        /// <summary>
        /// 刷新指定 item；不可见时不做处理。
        /// </summary>
        public void RefreshItemByItemIndex(int itemIndex)
        {
            var visibleIndex = -1;
            for (int i = 0; i < shownRecords.Count; i++)
            {
                if (shownRecords[i].View.Index == itemIndex)
                {
                    visibleIndex = i;
                    break;
                }
            }

            if (visibleIndex < 0)
            {
                return;
            }

            BindRecord(shownRecords[visibleIndex], itemIndex, visibleIndex);
        }

        public void ClearCells()
        {
            RecycleAllShownItems();
            ItemTotalCount = 0;
            currentList = null;
        }

        protected virtual void Awake()
        {
            EnsureInitialized();
        }

        protected virtual void OnEnable()
        {
            if (refreshWhenEnable)
            {
                refreshWhenEnable = false;
                RebuildVisibleItems(pendingIndex);
            }
        }

        protected virtual void OnDestroy()
        {
            RecycleAllShownItems();
            itemSource?.Destroy();
            if (poolRoot != null)
            {
                Destroy(poolRoot.gameObject);
            }
        }

        protected virtual void PositionRecord(LoopScrollItemRecord record, int index, int visibleSlot)
        {
            var rect = record.RectTransform;
            rect.sizeDelta = defaultItemSize;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            var line = visibleSlot / Mathf.Max(1, ContentConstraintCount);
            var cross = visibleSlot % Mathf.Max(1, ContentConstraintCount);
            if (IsVertical)
            {
                rect.anchoredPosition = new Vector2(cross * defaultItemSize.x, -line * defaultItemSize.y);
            }
            else
            {
                rect.anchoredPosition = new Vector2(line * defaultItemSize.x, -cross * defaultItemSize.y);
            }
        }

        protected virtual int GetVisibleItemCapacity()
        {
            EnsureInitialized();
            var itemMainSize = Mathf.Max(1f, IsVertical ? defaultItemSize.y : defaultItemSize.x);
            var viewMainSize = Mathf.Max(itemMainSize, IsVertical ? viewport.rect.height : viewport.rect.width);
            var lineCount = Mathf.CeilToInt(viewMainSize / itemMainSize) + visibleBuffer * 2;
            return Mathf.Max(1, lineCount * Mathf.Max(1, ContentConstraintCount));
        }

        protected int ClampFirstIndex(int index)
        {
            if (ItemTotalCount < 0)
            {
                return index;
            }

            var maxFirstIndex = Mathf.Max(0, ItemTotalCount - GetVisibleItemCapacity());
            return Mathf.Clamp(index, 0, maxFirstIndex);
        }

        protected bool IsValidIndex(int index)
        {
            return ItemTotalCount < 0 || (index >= 0 && index < ItemTotalCount);
        }

        protected void RebuildVisibleItems(int firstIndex)
        {
            EnsureInitialized();
            currentFirstIndex = ClampFirstIndex(firstIndex);
            if (ItemTotalCount == 0)
            {
                RecycleAllShownItems();
                return;
            }

            var capacity = GetVisibleItemCapacity();
            var visibleCount = ItemTotalCount < 0 ? capacity : Mathf.Min(capacity, Mathf.Max(0, ItemTotalCount - currentFirstIndex));
            while (shownRecords.Count > visibleCount)
            {
                var last = shownRecords[^1];
                shownRecords.RemoveAt(shownRecords.Count - 1);
                itemSource?.ReturnItem(last, poolRoot);
            }

            for (int i = 0; i < visibleCount; i++)
            {
                LoopScrollItemRecord record;
                if (i < shownRecords.Count)
                {
                    record = shownRecords[i];
                    if (!itemSource.CanReuse(record, currentFirstIndex + i))
                    {
                        itemSource.ReturnItem(record, poolRoot);
                        record = itemSource.GetItem(currentFirstIndex + i, content);
                        shownRecords[i] = record;
                    }
                }
                else
                {
                    record = itemSource.GetItem(currentFirstIndex + i, content);
                    shownRecords.Add(record);
                }

                BindRecord(record, currentFirstIndex + i, i);
            }

            UpdateContentSize();
            RefreshSelectedState();
        }

        protected virtual void UpdateContentSize()
        {
            if (content == null)
            {
                return;
            }

            var countForSize = ItemTotalCount < 0 ? GetVisibleItemCapacity() : ItemTotalCount;
            var lines = Mathf.CeilToInt((float)Mathf.Max(0, countForSize) / Mathf.Max(1, ContentConstraintCount));
            if (IsVertical)
            {
                content.sizeDelta = new Vector2(defaultItemSize.x * ContentConstraintCount, defaultItemSize.y * lines);
            }
            else
            {
                content.sizeDelta = new Vector2(defaultItemSize.x * lines, defaultItemSize.y * ContentConstraintCount);
            }
        }

        private LoopScrollItemSource<TView> GetOrCreateSource<TView>() where TView : ItemView, new()
        {
            if (itemSource is LoopScrollItemSource<TView> typed)
            {
                return typed;
            }

            itemSource?.Destroy();
            var source = new LoopScrollItemSource<TView>(itemPrefab);
            itemSource = source;
            return source;
        }

        private LoopScrollMultiItemSource GetOrCreateMultiSource()
        {
            if (itemSource is LoopScrollMultiItemSource typed)
            {
                return typed;
            }

            itemSource?.Destroy();
            var source = new LoopScrollMultiItemSource(Array.Empty<int>(), new Dictionary<int, Type>());
            itemSource = source;
            return source;
        }

        private void InvokeMultiItemClick(ItemView item, int index)
        {
            SetSelectIndex(index);
            multiItemClick?.Invoke(item, index);
        }

        private void SetItemCountInternal(int itemCount, int index, bool allowRefreshWhenDisable, bool autoSelect)
        {
            EnsureInitialized();
            ItemTotalCount = itemCount;
            if (scrollRect != null)
            {
                if (IsVertical)
                {
                    scrollRect.vertical = true;
                    scrollRect.horizontal = false;
                }
                else
                {
                    scrollRect.vertical = false;
                    scrollRect.horizontal = true;
                }

                if (itemCount < 0)
                {
                    scrollRect.horizontalScrollbar = null;
                    scrollRect.verticalScrollbar = null;
                }
            }

            if (!allowRefreshWhenDisable && !gameObject.activeInHierarchy)
            {
                refreshWhenEnable = true;
                pendingIndex = index;
                return;
            }

            RebuildVisibleItems(index);
            if (autoSelect)
            {
                SetSelectIndex(index);
            }
        }

        private void BindRecord(LoopScrollItemRecord record, int index, int visibleSlot)
        {
            record.RectTransform.SetParent(content, false);
            record.RectTransform.gameObject.SetActive(true);
            itemSource.ProvideData(record, index);
            PositionRecord(record, index, visibleSlot);
        }

        private void RecycleAllShownItems()
        {
            EnsureInitialized();
            for (int i = shownRecords.Count - 1; i >= 0; i--)
            {
                itemSource?.ReturnItem(shownRecords[i], poolRoot);
            }

            shownRecords.Clear();
        }

        private void RefreshSelectedState()
        {
            for (int i = 0; i < shownRecords.Count; i++)
            {
                var state = shownRecords[i].RectTransform.GetComponent<UIState>();
                if (state == null)
                {
                    continue;
                }

                state.SetState(shownRecords[i].View.Index == selectIndex ? "Selected" : "Normal");
            }
        }

        private void ApplyContentPosition(int firstIndex, float xOffsetGap, float yOffsetGap)
        {
            if (content == null)
            {
                return;
            }

            if (IsVertical)
            {
                content.anchoredPosition = new Vector2(xOffsetGap, firstIndex * defaultItemSize.y + yOffsetGap);
            }
            else
            {
                content.anchoredPosition = new Vector2(-firstIndex * defaultItemSize.x + xOffsetGap, yOffsetGap);
            }
        }

        private void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            scrollRect = scrollRect != null ? scrollRect : GetComponent<ScrollRect>();
            if (scrollRect != null)
            {
                content = content != null ? content : scrollRect.content;
                viewport = viewport != null ? viewport : scrollRect.viewport;
            }

            if (viewport == null)
            {
                viewport = transform as RectTransform;
            }

            if (content == null)
            {
                var contentObject = new GameObject("Content", typeof(RectTransform));
                content = contentObject.GetComponent<RectTransform>();
                content.SetParent(viewport != null ? viewport : transform, false);
                if (scrollRect != null)
                {
                    scrollRect.content = content;
                }
            }

            if (poolRoot == null)
            {
                poolRoot = new GameObject($"{name}_LoopScrollPool", typeof(RectTransform)).transform;
                poolRoot.SetParent(transform, false);
                poolRoot.gameObject.SetActive(false);
            }

            itemSource ??= new LoopScrollItemSource<ItemView>(itemPrefab);
            initialized = true;
        }
    }
}
