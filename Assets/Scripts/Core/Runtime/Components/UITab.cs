using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class UITab : MonoBehaviour
    {
        private const string NormalStateId = "Normal";
        private const string SelectedStateId = "Selected";

        [SerializeField] private List<GameObject> items = new List<GameObject>();
        [SerializeField] private GameObject prefab;
        [SerializeField] private Transform parent;
        [SerializeField] private int currentIndex;
        [SerializeField] private bool initializeHiddenItems;
        [SerializeField] private bool clearExistingButtonListeners;

        private Action<int> onSelected;
        private readonly Dictionary<Button, UnityAction> buttonHandlers = new Dictionary<Button, UnityAction>();
        private bool initialized;
        private bool initializing;

        /// 选择前拦截回调。返回 false 时阻止本次选择和通知。
        public Func<int, bool> TrySelect { get; set; }

        /// 当前选中索引；未选中时为 -1。
        public int Index => currentIndex;

        /// 当前可用 Tab 项数量。
        public int Count => items.Count;

        /// 当前 Tab 项对象列表。
        public IReadOnlyList<GameObject> Items => items;

        private void Awake()
        {
            InitializeExistingItems();
        }

        private void Start()
        {
            InitializeExistingItems();
        }

        private void OnDestroy()
        {
            ClearHandlers();
            onSelected = null;
            TrySelect = null;
            initialized = false;
            initializing = false;
        }

        /// <summary>
        /// 追加注册 Tab 选中回调。
        /// </summary>
        /// <param name="action">回调参数为选中索引；为空时不产生效果。</param>
        public void Register(Action<int> action)
        {
            onSelected += action;
        }

        /// <summary>
        /// 移除已注册的 Tab 选中回调。
        /// </summary>
        /// <param name="action">需要移除的回调。</param>
        public void Unregister(Action<int> action)
        {
            onSelected -= action;
        }

        /// <summary>
        /// 初始化 Tab 项。同步模式会立即完成；异步模式会逐帧初始化并异步加载图片。
        /// </summary>
        /// <param name="desc">Tab 文案列表，同时决定需要显示的项数量。</param>
        /// <param name="itemImages">每个 Tab 对应的 Sprite 资源路径；为空或越界时清空该项图片。</param>
        /// <param name="initIndex">初始化后选中的索引；非法索引不会触发回调。</param>
        /// <param name="notify">初始化选中时是否触发已注册回调。</param>
        /// <param name="action">初始化完成回调；在项创建和初始选择后触发。</param>
        /// <param name="isAsync">是否逐帧初始化并异步加载图片。</param>
        public void Init(
            IList<string> desc,
            IReadOnlyList<string> itemImages = null,
            int initIndex = 0,
            bool notify = true,
            Action action = null,
            bool isAsync = false)
        {
            if (isAsync)
            {
                InitAsyncInternal(desc, itemImages, initIndex, notify, action).Forget();
                return;
            }

            InitImmediate(desc, itemImages, initIndex, notify);
            action?.Invoke();
        }

        private void InitImmediate(
            IList<string> desc,
            IReadOnlyList<string> itemImages,
            int initIndex,
            bool notify)
        {
            if (desc == null || initializing)
            {
                return;
            }

            initializing = true;
            try
            {
                var requiredCount = desc.Count;
                EnsureItemList(requiredCount);
                if (requiredCount == 0)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i] != null)
                        {
                            items[i].SetActive(false);
                        }
                    }

                    ClearSelection();
                    return;
                }

                if (items.Count < requiredCount)
                {
                    Debug.LogWarning($"[UITab] {name} 可用 Item 数量不足：需要 {requiredCount}，实际 {items.Count}。请配置 prefab 或至少一个模板项。");
                }

                var visibleCount = Mathf.Min(requiredCount, items.Count);
                for (int i = 0; i < visibleCount; i++)
                {
                    var item = items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    item.SetActive(true);
                    SetItemText(item, desc[i]);
                    SetItemImage(
                        item,
                        itemImages != null && i < itemImages.Count ? itemImages[i] : null,
                        false);
                    InitItem(item, i);
                }

                for (int i = visibleCount; i < items.Count; i++)
                {
                    if (items[i] != null)
                    {
                        items[i].SetActive(false);
                    }
                }

                if (initIndex >= 0 && initIndex < visibleCount)
                {
                    Select(initIndex, notify);
                }
                else
                {
                    ClearSelection();
                }
            }
            finally
            {
                initialized = true;
                initializing = false;
            }
        }

        private async UniTaskVoid InitAsyncInternal(
            IList<string> desc,
            IReadOnlyList<string> itemImages,
            int initIndex,
            bool notify,
            Action action)
        {
            if (desc == null || initializing)
            {
                return;
            }

            initializing = true;
            try
            {
                var requiredCount = desc.Count;
                EnsureItemList(requiredCount);
                if (requiredCount == 0)
                {
                    for (int i = 0; i < items.Count; i++)
                    {
                        if (items[i] != null)
                        {
                            items[i].SetActive(false);
                        }
                    }

                    ClearSelection();
                    action?.Invoke();
                    return;
                }

                if (items.Count < requiredCount)
                {
                    Debug.LogWarning($"[UITab] {name} 可用 Item 数量不足：需要 {requiredCount}，实际 {items.Count}。请配置 prefab 或至少一个模板项。");
                }

                var visibleCount = Mathf.Min(requiredCount, items.Count);
                for (int i = 0; i < visibleCount; i++)
                {
                    var item = items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    item.SetActive(true);
                    SetItemText(item, desc[i]);
                    SetItemImage(
                        item,
                        itemImages != null && i < itemImages.Count ? itemImages[i] : null,
                        true);
                    InitItem(item, i);

                    await UniTask.Yield();
                    if (this == null || gameObject == null)
                    {
                        return;
                    }
                }

                for (int i = visibleCount; i < items.Count; i++)
                {
                    if (items[i] != null)
                    {
                        items[i].SetActive(false);
                    }
                }

                if (initIndex >= 0 && initIndex < visibleCount)
                {
                    Select(initIndex, notify);
                }
                else
                {
                    ClearSelection();
                }

                action?.Invoke();
            }
            finally
            {
                initialized = true;
                initializing = false;
            }
        }

        /// <summary>
        /// 触发一次选中，等价于带通知的 <see cref="SetIndex"/>。
        /// </summary>
        /// <param name="index">目标索引；非法索引会被忽略。</param>
        public void Select(int index)
        {
            Select(index, true);
        }

        /// <summary>
        /// 设置当前选中索引。
        /// </summary>
        /// <param name="index">目标索引；非法索引会被忽略。</param>
        /// <param name="notify">是否触发已注册回调。</param>
        public void SetIndex(int index, bool notify = true)
        {
            Select(index, notify);
        }

        /// 清空当前选中态，不触发回调。
        public void ClearSelection()
        {
            if (currentIndex >= 0 && currentIndex < items.Count)
            {
                SetItemState(items[currentIndex], NormalStateId);
            }

            currentIndex = -1;
            Refresh();
        }

        /// 对当前选中 Tab 主动触发一次选中回调。
        public void ExecuteEvent()
        {
            if (currentIndex >= 0 && currentIndex < items.Count)
            {
                Select(currentIndex, true);
            }
        }

        /// <summary>
        /// 按索引获取 Tab 项的直接子节点。
        /// </summary>
        /// <param name="itemIndex">Tab 项索引。</param>
        /// <param name="childIndex">子节点索引。</param>
        /// <returns>找到的子节点；索引非法时返回 null。</returns>
        public Transform GetItemChildByIndex(int itemIndex, int childIndex)
        {
            if (itemIndex < 0 || itemIndex >= items.Count || items[itemIndex] == null)
            {
                return null;
            }

            var itemTransform = items[itemIndex].transform;
            return childIndex >= 0 && childIndex < itemTransform.childCount ? itemTransform.GetChild(childIndex) : null;
        }

        /// <summary>
        /// 按路径获取 Tab 项子节点。
        /// </summary>
        /// <param name="itemIndex">Tab 项索引。</param>
        /// <param name="itemPath">相对 Tab 项根节点的路径。</param>
        /// <returns>找到的子节点；索引或路径非法时返回 null。</returns>
        public Transform GetItemChildByIndex(int itemIndex, string itemPath)
        {
            if (itemIndex < 0 || itemIndex >= items.Count || items[itemIndex] == null || string.IsNullOrEmpty(itemPath))
            {
                return null;
            }

            return items[itemIndex].transform.Find(itemPath);
        }

        /// <summary>
        /// 设置指定 Tab 项的 UIState 状态。
        /// </summary>
        /// <param name="index">Tab 项索引。</param>
        /// <param name="stateName">目标状态名。</param>
        public void SetItemState(int index, string stateName)
        {
            if (index >= 0 && index < items.Count)
            {
                SetItemState(items[index], stateName);
            }
        }

        private void Select(int index, bool notify)
        {
            InitializeExistingItems();
            if (index < 0 || index >= items.Count)
            {
                return;
            }

            if (TrySelect != null && !TrySelect(index))
            {
                return;
            }

            currentIndex = index;
            Refresh();
            if (notify)
            {
                onSelected?.Invoke(index);
            }
        }

        private void Refresh()
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    SetItemState(items[i], i == currentIndex ? SelectedStateId : NormalStateId);
                }
            }
        }

        private void InitializeExistingItems()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            items.RemoveAll(item => item == null);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].activeInHierarchy || initializeHiddenItems)
                {
                    InitItem(items[i], i);
                }
            }

            Refresh();
        }

        private void EnsureItemList(int count)
        {
            parent = parent != null ? parent : transform;
            while (items.Count < count)
            {
                GameObject item = null;
                if (prefab != null)
                {
                    item = Instantiate(prefab, parent);
                }
                else if (items.Count > 0 && items[0] != null)
                {
                    item = Instantiate(items[0], items[0].transform.parent);
                }

                if (item == null)
                {
                    break;
                }

                items.Add(item);
            }
        }

        private void InitItem(GameObject item, int index)
        {
            item.name = $"Tab{index + 1}";
            var button = item.GetComponentInChildren<Button>(true);
            if (button != null)
            {
                RegisterButton(button, index);
            }
        }

        private void RegisterButton(Button button, int index)
        {
            if (buttonHandlers.TryGetValue(button, out var oldHandler))
            {
                button.onClick.RemoveListener(oldHandler);
            }

            if (clearExistingButtonListeners)
            {
                button.onClick.RemoveAllListeners();
            }

            UnityAction handler = () => Select(index, true);
            buttonHandlers[button] = handler;
            button.onClick.AddListener(handler);
        }

        private void ClearHandlers()
        {
            foreach (var pair in buttonHandlers)
            {
                if (pair.Key != null)
                {
                    pair.Key.onClick.RemoveListener(pair.Value);
                }
            }

            buttonHandlers.Clear();
        }

        private static void SetItemText(GameObject item, string textValue)
        {
            if (string.IsNullOrEmpty(textValue))
            {
                return;
            }

            var tmpText = item.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tmpText != null)
            {
                tmpText.text = textValue;
                return;
            }

            var text = item.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = textValue;
            }
        }

        private void SetItemImage(GameObject item, string imagePath, bool isAsync)
        {
            var imageLoader = FindUIImageLoader(item);
            if (imageLoader == null)
            {
                if (!string.IsNullOrEmpty(imagePath))
                {
                    Debug.LogWarning($"[UITab] {item?.name} 未找到 UIImageLoader，图片路径已忽略: {imagePath}");
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

        private static void SetItemState(GameObject item, string stateName)
        {
            if (item == null)
            {
                return;
            }

            var state = item.GetComponentInChildren<UIState>(true);
            if (state != null)
            {
                state.SetState(stateName);
                return;
            }

            var selectedState = item.transform.Find("SelectedState");
            if (selectedState != null)
            {
                var isSelected = stateName == SelectedStateId;
                selectedState.gameObject.SetActive(isSelected);
            }
        }
    }
}
