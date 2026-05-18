using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class ViewList : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private Transform parent;
        [SerializeField] private List<Button> staticItemButtons = new List<Button>();

        private readonly List<View> currentShowList = new List<View>();
        private readonly Dictionary<Button, UnityAction> buttonHandlers = new Dictionary<Button, UnityAction>();
        private Action<int> onClick;
        private UIState lastState;
        private int currentIndex = -1;
        private bool initializing;

        public Func<int, bool> TryClick { get; set; }
        public int TotalCount => currentShowList.Count;
        public int Index => currentIndex;
        public View this[int index] => currentShowList[index];

        private void Awake()
        {
            RegisterStaticButtons();
        }

        private void OnDestroy()
        {
            ClearButtonHandlers();
            ReleaseList().Forget();
            onClick = null;
            TryClick = null;
            lastState = null;
        }

        public void Register(Action<int> action)
        {
            onClick += action;
            RegisterStaticButtons();
        }

        public void Unregister(Action<int> action)
        {
            onClick -= action;
        }

        public async UniTask InitAsync<TView, TData>(
            IList<TData> data,
            Action<TView, TData, int> onInit,
            bool autoShow = true,
            int initSelectIndex = -1,
            bool splitFrames = false)
            where TView : View, new()
        {
            if (data == null || initializing)
            {
                return;
            }

            initializing = true;
            try
            {
                parent = parent != null ? parent : transform;
                int i = 0;
                for (; i < data.Count; i++)
                {
                    var view = await GetOrCreateViewAsync<TView>(i);
                    if (autoShow)
                    {
                        await view.Show(false);
                    }
                    else
                    {
                        await view.Hide(false);
                    }

                    onInit?.Invoke(view, data[i], i);
                    RegisterItemClick(view.gameObject, i);

                    if (splitFrames)
                    {
                        await UniTask.Yield();
                        if (this == null || gameObject == null)
                        {
                            return;
                        }
                    }
                }

                for (; i < currentShowList.Count; i++)
                {
                    await currentShowList[i].Hide(false);
                }

                if (initSelectIndex >= 0 && initSelectIndex < data.Count)
                {
                    SetIndex(initSelectIndex, true);
                }
            }
            finally
            {
                initializing = false;
            }
        }

        public void Init<TView, TData>(
            IList<TData> data,
            Action<TView, TData> onInit,
            bool autoShow = true,
            int initSelectIndex = -1,
            bool splitFrames = false)
            where TView : View, new()
        {
            InitAsync<TView, TData>(
                data,
                (view, item, _) => onInit?.Invoke(view, item),
                autoShow,
                initSelectIndex,
                splitFrames).Forget();
        }

        public void SetIndex(int index, bool notify = true)
        {
            if (index < 0 || index >= currentShowList.Count)
            {
                return;
            }

            if (TryClick != null && !TryClick(index))
            {
                return;
            }

            currentIndex = index;
            RefreshSelectedState();
            if (notify)
            {
                onClick?.Invoke(index);
            }
        }

        public void HideAll()
        {
            for (int i = 0; i < currentShowList.Count; i++)
            {
                currentShowList[i].Hide(false).Forget();
            }
        }

        public void SetBtnsEnable(bool isEnable)
        {
            foreach (var pair in buttonHandlers)
            {
                if (pair.Key != null)
                {
                    pair.Key.enabled = isEnable;
                }
            }
        }

        private async UniTask<TView> GetOrCreateViewAsync<TView>(int index) where TView : View, new()
        {
            if (currentShowList.Count > index)
            {
                return (TView)currentShowList[index];
            }

            var view = new TView();
            currentShowList.Add(view);
            if (prefab != null)
            {
                view.InitWithGameObject(Instantiate(prefab, parent));
            }
            else
            {
                await view.InitAsync(parent);
            }

            return view;
        }

        private void RegisterStaticButtons()
        {
            for (int i = 0; i < staticItemButtons.Count; i++)
            {
                RegisterButton(staticItemButtons[i], i);
            }
        }

        private void RegisterItemClick(GameObject item, int index)
        {
            if (item == null)
            {
                return;
            }

            RegisterButton(item.GetComponentInChildren<Button>(true), index);
        }

        private void RegisterButton(Button button, int index)
        {
            if (button == null)
            {
                return;
            }

            if (buttonHandlers.TryGetValue(button, out var oldHandler))
            {
                button.onClick.RemoveListener(oldHandler);
            }

            UnityAction handler = () => SetIndex(index, true);
            buttonHandlers[button] = handler;
            button.onClick.AddListener(handler);
        }

        private void RefreshSelectedState()
        {
            for (int i = 0; i < currentShowList.Count; i++)
            {
                var view = currentShowList[i];
                if (view?.gameObject == null)
                {
                    continue;
                }

                var state = view.gameObject.GetComponent<UIState>();
                if (state == null)
                {
                    continue;
                }

                if (i == currentIndex)
                {
                    if (lastState != null && lastState != state)
                    {
                        lastState.SetState("Normal");
                    }

                    lastState = state;
                    state.SetState("Selected");
                }
                else
                {
                    state.SetState("Normal");
                }
            }
        }

        private void ClearButtonHandlers()
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

        private async UniTask ReleaseList()
        {
            for (int i = 0; i < currentShowList.Count; i++)
            {
                await currentShowList[i].Destroy();
            }

            currentShowList.Clear();
        }
    }
}
