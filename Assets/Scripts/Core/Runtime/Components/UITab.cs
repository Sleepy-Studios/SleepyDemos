using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class UITab : MonoBehaviour
    {
        [SerializeField] private List<GameObject> items = new List<GameObject>();
        [SerializeField] private GameObject prefab;
        [SerializeField] private Transform parent;
        [SerializeField] private List<string> labels = new List<string>();
        [SerializeField] private List<GameObject> selectedStates = new List<GameObject>();
        [SerializeField] private int currentIndex;
        [SerializeField] private bool initializeHiddenItems;
        [SerializeField] private bool clearExistingButtonListeners;

        private Action<int> onSelected;
        private readonly Dictionary<Button, UnityAction> buttonHandlers = new Dictionary<Button, UnityAction>();
        private readonly Dictionary<Toggle, UnityAction<bool>> toggleHandlers = new Dictionary<Toggle, UnityAction<bool>>();
        private UIState lastState;
        private ToggleGroup toggleGroup;
        private bool initialized;
        private bool initializing;

        public Func<int, bool> TrySelect { get; set; }
        public int Index => currentIndex;
        public int Count => items.Count;
        public IReadOnlyList<GameObject> Items => items;

        private void Awake()
        {
            toggleGroup = GetComponentInChildren<ToggleGroup>(true);
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
            lastState = null;
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
            bool splitFrames = false)
        {
            if (desc == null || initializing)
            {
                return items;
            }

            initializing = true;
            try
            {
                EnsureItemList(desc.Count);
                if (desc.Count == 0)
                {
                    labels.Clear();
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

                if (items.Count < desc.Count)
                {
                    Debug.LogWarning($"[UITab] {name} 可用 Item 数量不足：需要 {desc.Count}，实际 {items.Count}。请配置 prefab 或至少一个模板项。");
                }

                labels.Clear();
                var visibleCount = Mathf.Min(desc.Count, items.Count);
                for (int i = 0; i < visibleCount; i++)
                {
                    labels.Add(desc[i]);
                    var item = items[i];
                    if (item == null)
                    {
                        continue;
                    }

                    item.SetActive(true);
                    SetItemText(item, desc[i]);
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
                SetItemState(items[currentIndex], "Normal");
            }

            currentIndex = -1;
            lastState = null;
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
                RefreshToggleValues();
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
            for (int i = 0; i < selectedStates.Count; i++)
            {
                if (selectedStates[i] != null)
                {
                    selectedStates[i].SetActive(i == currentIndex);
                }
            }

            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                {
                    SetItemState(items[i], i == currentIndex ? "Selected" : "Normal");
                }
            }

            RefreshToggleValues();
        }

        private void RefreshToggleValues()
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                var toggle = item.GetComponentInChildren<Toggle>(true);
                if (toggle != null)
                {
                    toggle.SetIsOnWithoutNotify(i == currentIndex);
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
                return;
            }

            var toggle = item.GetComponentInChildren<Toggle>(true);
            if (toggle != null)
            {
                if (toggleGroup != null)
                {
                    toggle.group = toggleGroup;
                }

                RegisterToggle(toggle, index);
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

        private void RegisterToggle(Toggle toggle, int index)
        {
            if (toggleHandlers.TryGetValue(toggle, out var oldHandler))
            {
                toggle.onValueChanged.RemoveListener(oldHandler);
            }

            UnityAction<bool> handler = isOn =>
            {
                if (isOn)
                {
                    Select(index, true);
                }
            };
            toggleHandlers[toggle] = handler;
            toggle.onValueChanged.AddListener(handler);
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

            foreach (var pair in toggleHandlers)
            {
                if (pair.Key != null)
                {
                    pair.Key.onValueChanged.RemoveListener(pair.Value);
                }
            }
            toggleHandlers.Clear();
        }

        private static void SetItemText(GameObject item, string textValue)
        {
            if (string.IsNullOrEmpty(textValue))
            {
                return;
            }

            var text = item.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.text = textValue;
            }
        }

        private void SetItemState(GameObject item, string stateName)
        {
            var state = item.GetComponent<UIState>();
            if (state == null)
            {
                return;
            }

            if (stateName == "Selected")
            {
                if (lastState != null && lastState != state)
                {
                    lastState.SetState("Normal");
                }

                lastState = state;
            }

            state.SetState(stateName);
        }
    }
}
