using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class AccordionViewTab : MonoBehaviour
    {
        [SerializeField] private AccordionTab accordionTab;
        [SerializeField] private GameObject[] viewInstances;
        [SerializeField] private Transform parent;
        [SerializeField] private bool uiAnimation = true;
        [SerializeField] private bool isAsync;

        private List<View> currentTabList;
        private View currentClickView;

        /// 驱动 View 切换的手风琴 Tab。
        public AccordionTab AccordionTab
        {
            get => accordionTab;
            set => accordionTab = value;
        }

        /// View 实例化或本地内容显示的公共挂载根节点；未配置时回退到当前 Transform。
        public Transform Parent
        {
            get => parent != null ? parent : transform;
            set => parent = value;
        }

        /// 当前显示的 View；本地 GameObject 分页模式下始终为 null。
        public View CurrentClickView => currentClickView;

        private void Awake()
        {
            if (accordionTab != null)
            {
                accordionTab.Register(OnTabClick);
            }

            if (parent == null)
            {
                parent = transform;
            }
        }

        /// <summary>
        /// 初始化手风琴 Tab 与分页内容。传入 views 时走 View 生命周期；只传 localViewInstances 时仅切换本地对象显隐。
        /// </summary>
        /// <param name="data">手风琴数据，叶子数量必须与 views 或 localViewInstances 数量一致。</param>
        /// <param name="views">与叶子页签一一对应的 View 列表；为空时使用 localViewInstances。</param>
        /// <param name="localViewInstances">本地分页对象数组，不走 View 生命周期。</param>
        /// <param name="initLeafIndex">初始化后选中的叶子索引；负数表示不主动选择。</param>
        /// <param name="notify">初始化选中时是否触发回调。</param>
        /// <param name="action">AccordionTab 初始化完成回调。</param>
        /// <param name="canCollapseFirstLevel">点击已展开一级页签时是否允许收起。</param>
        /// <param name="enableAnimation">切换 View 时是否播放 UI 动画。</param>
        /// <param name="isAsync">是否异步初始化 Tab 图标和 View 资源。</param>
        public void Init(
            IList<AccordionTabData> data,
            List<View> views = null,
            GameObject[] localViewInstances = null,
            int initLeafIndex = 0,
            bool notify = true,
            Action action = null,
            bool canCollapseFirstLevel = false,
            bool enableAnimation = true,
            bool isAsync = false)
        {
            ReleaseViewList().Forget();

            if (accordionTab == null)
            {
                Debug.LogError($"[AccordionViewTab] {name} 缺少 AccordionTab 引用。");
                return;
            }

            var leafCount = CountLeaves(data);
            if (views != null && leafCount != views.Count)
            {
                Debug.LogError($"[AccordionViewTab] {name} 初始化错误：Tab 叶子数量和 View 数量不对应 tab:{leafCount} view:{views?.Count ?? 0}。");
                return;
            }

            viewInstances = localViewInstances ?? viewInstances;
            if (views == null && viewInstances != null && leafCount != viewInstances.Length)
            {
                Debug.LogError($"[AccordionViewTab] {name} 初始化错误：Tab 叶子数量和本地实例数量不对应 tab:{leafCount} view:{viewInstances.Length}。");
                return;
            }

            currentTabList = views;
            uiAnimation = enableAnimation;
            this.isAsync = isAsync;
            accordionTab.Init(data, initLeafIndex, notify, action, canCollapseFirstLevel, isAsync);
        }

        /// <summary>
        /// 注册叶子页签选中回调。
        /// </summary>
        /// <param name="onSelect">回调参数为扁平化叶子索引。</param>
        public void Register(Action<int> onSelect)
        {
            if (accordionTab != null)
            {
                accordionTab.Register(onSelect);
            }
        }

        /// <summary>
        /// 移除已注册的叶子页签选中回调。
        /// </summary>
        /// <param name="onSelect">需要移除的回调。</param>
        public void Unregister(Action<int> onSelect)
        {
            if (accordionTab != null)
            {
                accordionTab.Unregister(onSelect);
            }
        }

        private async void OnTabClick(int index)
        {
            if (currentTabList == null)
            {
                RefreshLocalInstances(index);
                return;
            }

            if (index < 0 || index >= currentTabList.Count)
            {
                Debug.LogError($"[AccordionViewTab] {name} 的 Tab 下标和 View 数据不匹配：{index}。");
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

        private void RefreshLocalInstances(int index)
        {
            if (viewInstances == null)
            {
                return;
            }

            for (int i = 0; i < viewInstances.Length; i++)
            {
                if (viewInstances[i] != null)
                {
                    viewInstances[i].SetActive(i == index);
                }
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

        private static int CountLeaves(IList<AccordionTabData> data)
        {
            if (data == null)
            {
                return 0;
            }

            var count = 0;
            for (int i = 0; i < data.Count; i++)
            {
                var item = data[i];
                if (item?.Children != null && item.Children.Count > 0)
                {
                    count += item.Children.Count;
                }
                else
                {
                    count++;
                }
            }

            return count;
        }

        private void OnDestroy()
        {
            if (accordionTab != null)
            {
                accordionTab.Unregister(OnTabClick);
            }

            ReleaseViewList().Forget();
        }
    }
}
