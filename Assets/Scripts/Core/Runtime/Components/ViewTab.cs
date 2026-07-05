using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class ViewTab : MonoBehaviour
    {
        [SerializeField] private UITab uiTab;
        [SerializeField] private GameObject[] viewInstances;
        [SerializeField] private Transform parent;
        [SerializeField] private int currentIndex;
        [SerializeField] private bool uiAnimation = true;
        [SerializeField] private bool isAsync;

        private Action<int> onSelected;
        private List<View> currentTabList;
        private View currentClickView;

        /// <summary>
        /// 当前驱动 View 切换的 Tab 组件。Prefab 可直接序列化引用，也可运行时赋值。
        /// </summary>
        public UITab UiTab
        {
            get => uiTab;
            set => uiTab = value;
        }

        /// <summary>
        /// View 实例化或本地内容显示的公共挂载根节点；未配置时回退到当前 Transform。
        /// </summary>
        public Transform Parent
        {
            get => parent != null ? parent : transform;
            set => parent = value;
        }

        public int Index => currentIndex;
        public View CurrentClickView => currentClickView;

        private void Awake()
        {
            EnsureParent();
            if (uiTab != null)
            {
                uiTab.Register(OnTabClick);
            }

            Refresh();
        }

        private void OnDestroy()
        {
            if (uiTab != null)
            {
                uiTab.Unregister(OnTabClick);
            }

            ReleaseViewList().Forget();
            onSelected = null;
        }

        /// <summary>
        /// 注册 ViewTab 选中回调。回调参数为当前 Tab 索引。
        /// </summary>
        public void Register(Action<int> action)
        {
            onSelected += action;
        }

        /// <summary>
        /// 取消注册 ViewTab 选中回调。
        /// </summary>
        public void Unregister(Action<int> action)
        {
            onSelected -= action;
        }

        /// <summary>
        /// 使用已有本地 GameObject 作为页面内容，适合验证页或不走 View 生命周期的轻量分页。
        /// </summary>
        public void Init(GameObject[] localViewInstances, int index = 0)
        {
            viewInstances = localViewInstances;
            currentTabList = null;
            if (index >= 0)
            {
                Select(index);
            }
            else
            {
                Refresh();
            }
        }

        /// <summary>
        /// 初始化 Tab 文案与对应 View 列表，View 会挂载到 Parent 下。
        /// </summary>
        public void Init(IList<string> desc, List<View> views, int index = 0, Action action = null, bool asyncLoad = false)
        {
            ReleaseViewList().Forget();
            currentTabList = views;
            isAsync = asyncLoad;

            if (uiTab == null)
            {
                Debug.LogError($"[ViewTab] {name} 缺少 UITab 引用。");
                return;
            }

            if (desc == null || views == null || desc.Count != views.Count)
            {
                Debug.LogError($"[ViewTab] {name} 初始化错误：Tab 和 View 数量不对应。");
                return;
            }

            uiTab.InitAsync(desc, index, true).Forget();
            action?.Invoke();
        }

        /// <summary>
        /// 初始化已有 Tab 与对应 View 列表，不重建 Tab 文案。
        /// </summary>
        public void Init(List<View> views, int index = 0)
        {
            ReleaseViewList().Forget();
            currentTabList = views;
            if (index >= 0)
            {
                Select(index);
            }
        }

        /// <summary>
        /// 选择指定索引，并驱动本地内容或 View 切换。
        /// </summary>
        public void Select(int index)
        {
            if (uiTab != null && uiTab.Index != index)
            {
                uiTab.SetIndex(index);
                return;
            }

            OnTabClick(index);
        }

        private async void OnTabClick(int index)
        {
            if (index < 0)
            {
                return;
            }

            currentIndex = index;
            Refresh();
            onSelected?.Invoke(index);

            if (currentTabList == null)
            {
                return;
            }

            if (index >= currentTabList.Count)
            {
                Debug.LogError($"[ViewTab] {name} 的 Tab 下标和 View 数据不匹配：{index}。");
                return;
            }

            var clickView = currentTabList[index];
            if (clickView == null)
            {
                return;
            }

            if (currentClickView != null && currentClickView != clickView)
            {
                await currentClickView.Hide(uiAnimation);
            }

            currentClickView = clickView;
            var viewName = currentClickView.Name;
            if ((currentClickView.State & ViewState.FirstInit) == 0)
            {
                await currentClickView.InitAsync(Parent);
            }

            if (viewName == currentClickView.Name)
            {
                await currentClickView.Show(uiAnimation);
            }
        }

        private void Refresh()
        {
            if (viewInstances == null)
            {
                return;
            }

            for (int i = 0; i < viewInstances.Length; i++)
            {
                if (viewInstances[i] != null)
                {
                    viewInstances[i].SetActive(i == currentIndex);
                }
            }
        }

        private void EnsureParent()
        {
            if (parent == null)
            {
                parent = transform;
            }
        }

        private async UniTaskVoid ReleaseViewList()
        {
            if (currentClickView != null)
            {
                await currentClickView.Hide(uiAnimation);
            }

            if (currentTabList != null)
            {
                foreach (var view in currentTabList)
                {
                    if (view != null)
                    {
                        await view.Destroy();
                    }
                }
            }

            currentTabList = null;
            currentClickView = null;
        }
    }
}
