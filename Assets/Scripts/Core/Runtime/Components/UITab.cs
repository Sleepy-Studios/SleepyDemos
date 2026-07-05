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

        public Func<int, bool> TrySelect { get; set; }
        public int Index => currentIndex;
        public int Count => items.Count;
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

        public void Register(Action<int> action)
        {
            onSelected += action;
        }

        public void Unregister(Action<int> action)
        {
            onSelected -= action;
        }

        public async UniTask<IReadOnlyList<GameObject>> InitAsync(
            IList<string> desc,
            int initIndex = 0,
            bool notify = true,
            bool splitFrames = false,
            IReadOnlyList<Sprite> itemSprites = null,
            IReadOnlyList<string> tmpLabels = null,
            IReadOnlyList<string> itemImages = null,
            IReadOnlyList<float> itemImageScales = null)
        {
            IReadOnlyList<string> activeTexts = desc != null ? (desc as IReadOnlyList<string> ?? new List<string>(desc)) : tmpLabels;
            if (activeTexts == null || initializing)
            {
                return items;
            }

            initializing = true;
            try
            {
                var requiredCount = Mathf.Max(desc != null ? desc.Count : 0, tmpLabels != null ? tmpLabels.Count : 0);
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
                    return items;
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

                    var itemText = tmpLabels != null && i < tmpLabels.Count
                        ? tmpLabels[i]
                        : activeTexts[i];

                    item.SetActive(true);
                    SetItemText(item, itemText);
                    var sprite = itemSprites != null && i < itemSprites.Count ? itemSprites[i] : null;
                    SetItemImage(item, sprite);
                    SetItemImage(
                        item,
                        itemImages != null && i < itemImages.Count ? itemImages[i] : null,
                        itemImageScales != null && i < itemImageScales.Count ? itemImageScales[i] : 1f);
                    InitItem(item, i);

                    if (splitFrames)
                    {
                        await UniTask.Yield();
                        if (this == null || gameObject == null)
                        {
                            return items;
                        }
                    }
                }

                for (int i = visibleCount; i < items.Count; i++)
                {
                    if (items[i] != null)
                    {
                        items[i].SetActive(false);
                    }
                }

                Select(Mathf.Clamp(initIndex, 0, visibleCount - 1), notify);
            }
            finally
            {
                initialized = true;
                initializing = false;
            }

            return items;
        }

        public UniTask<IReadOnlyList<GameObject>> InitSync(IList<string> desc, int initIndex = 0, Action action = null)
        {
            return InitWithCallback(desc, initIndex, action);
        }

        public void Select(int index)
        {
            Select(index, true);
        }

        public void SetIndex(int index, bool notify = true)
        {
            Select(index, notify);
        }

        public void ClearSelection()
        {
            if (currentIndex >= 0 && currentIndex < items.Count)
            {
                SetItemState(items[currentIndex], NormalStateId);
            }

            currentIndex = -1;
            Refresh();
        }

        public void ExecuteEvent()
        {
            if (currentIndex >= 0 && currentIndex < items.Count)
            {
                Select(currentIndex, true);
            }
        }

        public Transform GetItemChildByIndex(int itemIndex, int childIndex)
        {
            if (itemIndex < 0 || itemIndex >= items.Count || items[itemIndex] == null)
            {
                return null;
            }

            var itemTransform = items[itemIndex].transform;
            return childIndex >= 0 && childIndex < itemTransform.childCount ? itemTransform.GetChild(childIndex) : null;
        }

        public Transform GetItemChildByIndex(int itemIndex, string itemPath)
        {
            if (itemIndex < 0 || itemIndex >= items.Count || items[itemIndex] == null || string.IsNullOrEmpty(itemPath))
            {
                return null;
            }

            return items[itemIndex].transform.Find(itemPath);
        }

        public void SetItemState(int index, string stateName)
        {
            if (index >= 0 && index < items.Count)
            {
                SetItemState(items[index], stateName);
            }
        }

        private async UniTask<IReadOnlyList<GameObject>> InitWithCallback(IList<string> desc, int initIndex, Action action)
        {
            var result = await InitAsync(desc, initIndex);
            action?.Invoke();
            return result;
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

        private static void SetItemImage(GameObject item, Sprite sprite)
        {
            var imageLoader = FindItemImageLoader(item);
            if (imageLoader == null)
            {
                return;
            }

            if (sprite == null)
            {
                imageLoader.Clear();
                return;
            }

            imageLoader.SetImage(sprite);
        }

        private void SetItemImage(GameObject item, string imagePath, float scale)
        {
            var imageLoader = FindItemImageLoader(item);
            if (imageLoader == null)
            {
                if (!string.IsNullOrEmpty(imagePath))
                {
                    Debug.LogWarning($"[UITab] {item?.name} 未找到 ItemImageLoader，图片路径已忽略: {imagePath}");
                }

                return;
            }

            if (string.IsNullOrEmpty(imagePath))
            {
                imageLoader.Clear();
                return;
            }

            imageLoader.SetImage(imagePath, scale);
        }

        private static ItemImageLoader FindItemImageLoader(GameObject item)
        {
            if (item == null)
            {
                return null;
            }

            return item.GetComponentInChildren<ItemImageLoader>(true);
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
