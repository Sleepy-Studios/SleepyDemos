using System;
using System.Threading;
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
        [SerializeField] private bool isAsync = true;

        private Action<int> onSelected;
        private List<View> currentTabList;
        private View currentClickView;
        private CancellationTokenSource selectionCancellation;
        private UniTask selectionTask;


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

            ReleaseViewListAsync().Forget();
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
        /// <param name="action">Tab 初始化完成回调，不等待图标加载或分页 View 显示。</param>
        /// <param name="enableAnimation">切换 View 时是否播放 UI 动画。</param>
        /// <param name="isAsync">默认 true；是否异步初始化 Tab 图标和 View 资源。</param>
        public void Init(
            IList<string> desc = null,
            List<View> views = null,
            GameObject[] localViewInstances = null,
            IReadOnlyList<string> itemImages = null,
            int index = 0,
            Action action = null,
            bool enableAnimation = true,
            bool isAsync = true)
        {
            ReleaseViewListAsync(views).Forget();
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

        private void OnTabClick(int index)
        {
            if (index < 0) return;
            currentIndex = index;
            Refresh();
            onSelected?.Invoke(index);
            if (currentTabList == null || index < 0 || index >= currentTabList.Count) return;
            var target = currentTabList[index];
            if (target == null) return;
            selectionCancellation?.Cancel();
            selectionCancellation?.Dispose();
            selectionCancellation = new CancellationTokenSource();
            var previous = selectionTask;
            // 下一次切换和释放都可能等待本轮，使用支持多个等待者的完成源。
            var completion = new UniTaskCompletionSource();
            selectionTask = completion.Task;
            SelectViewAsync(previous, target, selectionCancellation.Token, completion).Forget();
        }

        private async UniTask SelectViewAsync(UniTask previous, View target, CancellationToken token, UniTaskCompletionSource completion)
        {
            try
            {
                await previous;
                token.ThrowIfCancellationRequested();
                if (currentClickView != null && currentClickView != target)
                    await currentClickView.HideAsync(uiAnimation, token);
                token.ThrowIfCancellationRequested();
                currentClickView = target;
                if (!target.IsLoaded)
                {
                    // 切换取消只停止显示交付；缓存页的加载由 View 自身销毁负责取消。
                    if (isAsync) { if (!await target.LoadAsync(Parent)) return; }
                    else target.Init(Parent);
                }
                token.ThrowIfCancellationRequested();
                if (target.IsLoaded) await target.ShowAsync(uiAnimation, token);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { completion.TrySetResult(); }
        }

        private void OnDisable()
        {
            selectionCancellation?.Cancel();
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

        private async UniTask ReleaseViewListAsync(List<View> retained = null)
        {
            selectionCancellation?.Cancel();
            selectionCancellation?.Dispose();
            selectionCancellation = null;
            // 等待前移交旧列表所有权，防止旧清理清空新一轮 Init 的字段。
            var previous = selectionTask;
            var views = currentTabList;
            currentTabList = null;
            // 复用同一列表时保留旧可见页，下一次选择仍需先隐藏它。
            if (retained == null || !retained.Contains(currentClickView)) currentClickView = null;
            if (views == null) return;
            foreach (var view in views)
                if (view != null && (retained == null || !retained.Contains(view)))
                    await view.DestroyAsync();
            await previous;
        }
    }
}
