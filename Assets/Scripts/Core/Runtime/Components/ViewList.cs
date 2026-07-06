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

        /// 点击前拦截回调。返回 false 时阻止本次选中和通知。
        public Func<int, bool> TryClick { get; set; }

        /// 当前已创建的 View 数量。
        public int TotalCount => currentShowList.Count;

        /// 当前选中索引；未选中时为 -1。
        public int Index => currentIndex;

        /// 按索引访问已创建的 View。
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

        /// <summary>
        /// 追加注册列表项点击回调。
        /// </summary>
        /// <param name="action">回调参数为列表索引。</param>
        public void Register(Action<int> action)
        {
            onClick += action;
            RegisterStaticButtons();
        }

        /// <summary>
        /// 移除已注册的列表项点击回调。
        /// </summary>
        /// <param name="action">需要移除的回调。</param>
        public void Unregister(Action<int> action)
        {
            onClick -= action;
        }

        /// <summary>
        /// 初始化有限 View 列表。同步模式立即创建/初始化 View；异步模式逐帧初始化并异步加载 View 资源。
        /// </summary>
        /// <typeparam name="TView">View 类型。</typeparam>
        /// <typeparam name="TData">数据类型。</typeparam>
        /// <param name="data">列表数据。</param>
        /// <param name="onInit">单项初始化回调，参数依次为 View、数据和索引。</param>
        /// <param name="autoShow">初始化后是否显示每一项；false 时隐藏。</param>
        /// <param name="initSelectIndex">初始化后选中的索引；负数表示不主动选择。</param>
        /// <param name="notify">初始选中时是否触发点击回调。</param>
        /// <param name="isAsync">是否异步初始化 View 资源并逐帧处理列表项。</param>
        public void Init<TView, TData>(
            IList<TData> data,
            Action<TView, TData, int> onInit,
            bool autoShow = true,
            int initSelectIndex = -1,
            bool notify = true,
            bool isAsync = false)
            where TView : View, new()
        {
            if (isAsync)
            {
                InitAsyncInternal(data, onInit, autoShow, initSelectIndex, notify).Forget();
                return;
            }

            InitImmediate(data, onInit, autoShow, initSelectIndex, notify);
        }

        private void InitImmediate<TView, TData>(
            IList<TData> data,
            Action<TView, TData, int> onInit,
            bool autoShow,
            int initSelectIndex,
            bool notify)
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
                    var view = GetOrCreateView<TView>(i);
                    if (autoShow)
                    {
                        view.Show(false).Forget();
                    }
                    else
                    {
                        view.Hide(false).Forget();
                    }

                    onInit?.Invoke(view, data[i], i);
                    RegisterItemClick(view.gameObject, i);
                }

                for (; i < currentShowList.Count; i++)
                {
                    currentShowList[i].Hide(false).Forget();
                }

                if (initSelectIndex >= 0 && initSelectIndex < data.Count)
                {
                    SetIndex(initSelectIndex, notify);
                }
            }
            finally
            {
                initializing = false;
            }
        }

        private async UniTaskVoid InitAsyncInternal<TView, TData>(
            IList<TData> data,
            Action<TView, TData, int> onInit,
            bool autoShow,
            int initSelectIndex,
            bool notify)
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

                    await UniTask.Yield();
                    if (this == null || gameObject == null)
                    {
                        return;
                    }
                }

                for (; i < currentShowList.Count; i++)
                {
                    await currentShowList[i].Hide(false);
                }

                if (initSelectIndex >= 0 && initSelectIndex < data.Count)
                {
                    SetIndex(initSelectIndex, notify);
                }
            }
            finally
            {
                initializing = false;
            }
        }

        /// <summary>
        /// 设置当前选中项。
        /// </summary>
        /// <param name="index">目标索引；非法索引会被忽略。</param>
        /// <param name="notify">是否触发已注册点击回调。</param>
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

        /// 隐藏当前列表内所有 View。
        public void HideAll()
        {
            for (int i = 0; i < currentShowList.Count; i++)
            {
                currentShowList[i].Hide(false).Forget();
            }
        }

        /// <summary>
        /// 设置已注册按钮是否可点击。
        /// </summary>
        /// <param name="isEnable">true 表示启用按钮，false 表示禁用。</param>
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

        private TView GetOrCreateView<TView>(int index) where TView : View, new()
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
                view.Init(parent);
            }

            return view;
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
