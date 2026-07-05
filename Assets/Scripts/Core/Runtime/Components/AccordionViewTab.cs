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

        /// <summary>
        /// 驱动 View 切换的手风琴 Tab。
        /// </summary>
        public AccordionTab AccordionTab
        {
            get => accordionTab;
            set => accordionTab = value;
        }

        /// <summary>
        /// View 实例化或本地内容显示的公共挂载根节点；未配置时回退到当前 Transform。
        /// </summary>
        public Transform Parent
        {
            get => parent != null ? parent : transform;
            set => parent = value;
        }

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
        /// 初始化手风琴 Tab 与对应 View，叶子页签数量必须和 View 数量一一对应。
        /// </summary>
        public void Init(
            IList<AccordionTabData> data,
            List<View> views,
            int initLeafIndex = 0,
            Action action = null,
            bool canCollapseFirstLevel = false,
            bool asyncLoad = false)
        {
            ReleaseViewList().Forget();

            if (accordionTab == null)
            {
                Debug.LogError($"[AccordionViewTab] {name} 缺少 AccordionTab 引用。");
                return;
            }

            var leafCount = CountLeaves(data);
            if (views == null || leafCount != views.Count)
            {
                Debug.LogError($"[AccordionViewTab] {name} 初始化错误：Tab 叶子数量和 View 数量不对应 tab:{leafCount} view:{views?.Count ?? 0}。");
                return;
            }

            currentTabList = views;
            isAsync = asyncLoad;
            accordionTab.Init(data, initLeafIndex, action, canCollapseFirstLevel);
        }

        /// <summary>
        /// 注册叶子页签选中回调。
        /// </summary>
        public void Register(Action<int> onSelect)
        {
            if (accordionTab != null)
            {
                accordionTab.Register(onSelect);
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
            if ((currentClickView.State & ViewState.FirstInit) == 0)
            {
                await currentClickView.InitAsync(Parent);
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
