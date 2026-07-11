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

        /// 当前驱动 View 切换的 Tab 组件。Prefab 可直接序列化引用，也可运行时赋值。
        public UITab UiTab
        {
            get => uiTab;
            set => uiTab = value;
        }

        /// View 实例化或本地内容显示的公共挂载根节点；未配置时回退到当前 Transform。
        public Transform Parent
        {
            get => parent != null ? parent : transform;
            set => parent = value;
        }

        /// 当前选中的 Tab 索引。
        public int Index => currentIndex;

        /// 当前显示的 View；本地 GameObject 分页模式下始终为 null。
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
        /// 初始化 Tab 与分页内容。传入 views 时走 View 生命周期；只传 localViewInstances 时仅切换本地对象显隐。
        /// </summary>
        /// <param name="desc">Tab 文案；为空时不重建内部 UITab。</param>
        /// <param name="views">与 Tab 一一对应的 View 列表；为空时使用 localViewInstances。</param>
        /// <param name="localViewInstances">本地分页对象数组，不走 View 生命周期。</param>
        /// <param name="itemImages">Tab 图标 Sprite 资源路径；为空时清空图标。</param>
        /// <param name="index">初始化后选中的索引；负数表示不主动选择。</param>
        /// <param name="action">Tab 初始化完成回调。</param>
        /// <param name="enableAnimation">切换 View 时是否播放 UI 动画。</param>
        /// <param name="isAsync">是否异步初始化 Tab 图标和 View 资源。</param>
        public void Init(
            IList<string> desc = null,
            List<View> views = null,
            GameObject[] localViewInstances = null,
            IReadOnlyList<string> itemImages = null,
            int index = 0,
            Action action = null,
            bool enableAnimation = true,
            bool isAsync = false)
        {
            ReleaseViewList().Forget();
            viewInstances = localViewInstances ?? viewInstances;
            currentTabList = views;
            uiAnimation = enableAnimation;
            this.isAsync = isAsync;

            if (views != null && desc != null && desc.Count != views.Count)
            {
                Debug.LogError($"[ViewTab] {name} 初始化错误：Tab 和 View 数量不对应。");
                return;
            }

            if (views != null && viewInstances != null && viewInstances.Length > 0 && viewInstances.Length != views.Count)
            {
                Debug.LogError($"[ViewTab] {name} 初始化错误：本地实例和 View 数量不对应。");
                return;
            }

            if (desc != null)
            {
                if (uiTab == null)
                {
                    Debug.LogError($"[ViewTab] {name} 缺少 UITab 引用。");
                    return;
                }

                uiTab.Init(desc, itemImages, index, true, action, isAsync);
                return;
            }

            action?.Invoke();
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
            if (!currentClickView.IsLoaded)
            {
                if (isAsync)
                {
                    await currentClickView.InitAsync(Parent);
                }
                else
                {
                    currentClickView.Init(Parent);
                }
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
