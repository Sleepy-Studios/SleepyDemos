using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class UIManager : Singleton<UIManager>
    {
        private UIStack layerStack;
        private Button maskButton;
        private UINavigationCoordinator navigationCoordinator;

        public UICache cacheStack { get; private set; }
        public string LastCloseName { get; private set; }
        public string CurrentUIName { get; private set; }
        public int StackCount => layerStack?.TotalCount ?? 0;

        public event Action<View> OnBeginOpen;
        public event Action<View> OnOpen;
        public event Action<View> OnClose;
        public event Action<View> OnBehind;
        public Func<View, UniTask> OnBeforeOpen;

        protected override void OnSingletonInit()
        {
            cacheStack = new UICache();
            navigationCoordinator = new UINavigationCoordinator(ExecuteAsync);
        }

        public async UniTask InitializeAsync()
        {
            await UIRootManager.Instance.BuildUIRoot();
            layerStack ??= new UIStack();
            ConfigureMask();
        }

        /// <summary>
        /// 将指定类型的 View 显示操作加入 FIFO 导航队列。
        /// </summary>
        /// <typeparam name="T">目标 View 类型。</typeparam>
        /// <param name="options">显示选项；默认播放动画。</param>
        /// <param name="cancellationToken">调用方取消令牌；排队或执行中取消均返回 Canceled。</param>
        /// <returns>导航操作结果。</returns>
        public UniTask<UIOperationResult> ShowAsync<T>(
            UIShowOptions options = default,
            CancellationToken cancellationToken = default) where T : View
        {
            return navigationCoordinator.Enqueue(
                UINavigationAction.Push,
                typeof(T),
                options.Animated,
                cancellationToken);
        }

        /// <summary>
        /// 将 Page 替换操作加入 FIFO 导航队列。
        /// </summary>
        /// <typeparam name="T">目标 Page 类型。</typeparam>
        /// <param name="options">显示选项；默认播放动画。</param>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <returns>导航操作结果；非 Page 类型返回 Failed。</returns>
        public UniTask<UIOperationResult> ReplaceAsync<T>(
            UIShowOptions options = default,
            CancellationToken cancellationToken = default) where T : View
        {
            return navigationCoordinator.Enqueue(
                UINavigationAction.Replace,
                typeof(T),
                options.Animated,
                cancellationToken);
        }

        /// <summary>
        /// 将指定类型 View 的关闭操作加入 FIFO 导航队列。
        /// </summary>
        /// <typeparam name="T">目标 View 类型。</typeparam>
        /// <param name="animated">是否播放退出动画。</param>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <returns>导航操作结果。</returns>
        public UniTask<UIOperationResult> CloseAsync<T>(
            bool animated = true,
            CancellationToken cancellationToken = default) where T : View
        {
            return EnqueueClose(typeof(T), animated, cancellationToken);
        }

        /// <summary>
        /// 关闭最上层 Modal；没有 Modal 时关闭当前 Page。
        /// </summary>
        /// <param name="animated">是否播放退出动画。</param>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <returns>导航操作结果。</returns>
        public UniTask<UIOperationResult> BackAsync(
            bool animated = true,
            CancellationToken cancellationToken = default)
        {
            return navigationCoordinator.Enqueue(
                UINavigationAction.Back,
                null,
                animated,
                cancellationToken);
        }

        /// <summary>
        /// 取消当前及待执行导航，并串行清空全部 View、栈与遮罩。
        /// </summary>
        /// <param name="cancellationToken">调用方取消令牌。</param>
        /// <returns>清理操作结果。</returns>
        public UniTask<UIOperationResult> CloseAllAsync(CancellationToken cancellationToken = default)
        {
            return navigationCoordinator.Enqueue(
                UINavigationAction.CloseAll,
                null,
                false,
                cancellationToken);
        }

        public View Show(string uiName, bool hidePrevious = true)
        {
            var type = UITypeReflection.Get(uiName);
            return type == null ? null : Show(type, hidePrevious);
        }

        public View Show(Type type, bool hidePrevious = true)
        {
            var view = cacheStack.GetOrCreateView(type);
            if (view == null)
            {
                return null;
            }

            ObserveOperationAsync(navigationCoordinator.Enqueue(
                UINavigationAction.Push,
                type,
                true,
                CancellationToken.None)).Forget(LogOperationFailure);
            return view;
        }

        public T Show<T>(bool hidePrevious = true) where T : View
        {
            return Show(typeof(T), hidePrevious) as T;
        }

        public T Show<T, TData>(TData data, bool hidePrevious = true) where T : View<TData>
        {
            var view = cacheStack.GetOrCreateView<T>();
            view.SetData(data);
            Show(typeof(T), hidePrevious);
            return view;
        }

        public T Show<T, TData1, TData2>(TData1 data1, TData2 data2, bool hidePrevious = true)
            where T : View<TData1, TData2>
        {
            var view = cacheStack.GetOrCreateView<T>();
            view.SetData(data1, data2);
            Show(typeof(T), hidePrevious);
            return view;
        }

        public T Get<T>() where T : View => cacheStack.GetView(typeof(T)) as T;

        public View Get(string uiName)
        {
            var type = UITypeReflection.Get(uiName);
            return type == null ? null : cacheStack.GetView(type);
        }

        public void Close(string uiName)
        {
            var type = UITypeReflection.Get(uiName);
            if (type != null)
            {
                Close(type);
            }
        }

        public void Close<T>() where T : View => Close(typeof(T));

        public void Close(Type type, bool animation = true)
        {
            ObserveOperationAsync(EnqueueClose(type, animation, CancellationToken.None))
                .Forget(LogOperationFailure);
        }

        public void Back()
        {
            ObserveOperationAsync(BackAsync()).Forget(LogOperationFailure);
        }

        public async UniTask Preload<T>() where T : View
        {
            await navigationCoordinator.Enqueue(
                UINavigationAction.Preload,
                typeof(T),
                false,
                CancellationToken.None);
        }

        public async UniTask Preload<T, TData>(TData data) where T : View<TData>
        {
            var view = cacheStack.GetOrCreateView<T>();
            view.SetData(data);
            await Preload<T>();
        }

        public async UniTask CloseAll(bool animation = false)
        {
            await CloseAllAsync();
        }

        public View GetStackTopView() => layerStack?.StackTopView;
        public Type GetStackTopViewType() => layerStack?.StackTopView?.GetType();

        private UniTask<UIOperationResult> EnqueueClose(
            Type type,
            bool animated,
            CancellationToken cancellationToken)
        {
            return navigationCoordinator.Enqueue(
                UINavigationAction.Close,
                type,
                animated,
                cancellationToken);
        }

        private async UniTask<UIOperationResult> ExecuteAsync(
            QueuedUIOperation operation,
            CancellationToken cancellationToken)
        {
            await InitializeAsync();
            return operation.Action switch
            {
                UINavigationAction.Push => await ExecuteShowAsync(operation, false, cancellationToken),
                UINavigationAction.Replace => await ExecuteShowAsync(operation, true, cancellationToken),
                UINavigationAction.Close => await ExecuteCloseAsync(operation, cancellationToken),
                UINavigationAction.Back => await ExecuteBackAsync(operation, cancellationToken),
                UINavigationAction.Preload => await ExecutePreloadAsync(operation, cancellationToken),
                UINavigationAction.CloseAll => await ExecuteCloseAllAsync(operation, cancellationToken),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private async UniTask<UIOperationResult> ExecuteShowAsync(
            QueuedUIOperation operation,
            bool replace,
            CancellationToken cancellationToken)
        {
            var snapshot = layerStack.Capture();
            var view = cacheStack.GetOrCreateView(operation.TargetType);
            if (view == null)
            {
                return UIOperationResult.Failed(operation.OperationId, operation.Action, null,
                    new InvalidOperationException($"无法创建 View: {operation.TargetType}"));
            }

            if (replace && view.ViewMode != UIViewMode.Page)
            {
                return UIOperationResult.Failed(operation.OperationId, operation.Action, view,
                    new InvalidOperationException("Replace 首版仅支持 Page View。"));
            }

            var previous = GetPreviousView(view);
            if (previous == view && view.State == ViewState.Visible)
            {
                return UIOperationResult.Ignored(operation.OperationId, operation.Action, view);
            }

            try
            {
                OnBeginOpen?.Invoke(view);
                var loaded = view.IsLoaded || await view.LoadAsync(
                    UIRootManager.Instance.GetRoot(view.Level),
                    cancellationToken);
                if (!loaded)
                {
                    throw new InvalidOperationException($"View 加载失败: {view.Name}");
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (OnBeforeOpen != null)
                {
                    await OnBeforeOpen(view);
                }

                if (previous != null && previous != view)
                {
                    OnBehind?.Invoke(previous);
                    await previous.ExitAsync(new UITransitionContext(
                        operation.OperationId,
                        operation.Action,
                        view,
                        previous,
                        operation.Animated), cancellationToken);
                }

                if (replace && previous != null && previous != view)
                {
                    layerStack.CommitClose(previous);
                }

                layerStack.CommitShow(view);
                if (view.ViewMode != UIViewMode.Widget && !snapshot.Contains(view))
                {
                    view.Reference++;
                }

                await view.EnterAsync(new UITransitionContext(
                    operation.OperationId,
                    operation.Action,
                    view,
                    previous,
                    operation.Animated), cancellationToken);
                PlaceOnTopAndApplyMask(view);

                if (previous != null && previous != view)
                {
                    LastCloseName = previous.Name;
                }

                if (view.ViewMode != UIViewMode.Widget)
                {
                    CurrentUIName = view.Name;
                }

                OnOpen?.Invoke(view);
                if (replace && previous != null && previous != view && previous.DestroyOnHide)
                {
                    previous.Reference = Math.Max(0, previous.Reference - 1);
                    await DestroyAndRemoveAsync(previous);
                }

                return UIOperationResult.Succeeded(operation.OperationId, operation.Action, view);
            }
            catch (OperationCanceledException)
            {
                await RollbackAndCleanupAsync(snapshot, view);
                return UIOperationResult.Canceled(operation.OperationId, operation.Action, view);
            }
            catch (Exception exception)
            {
                await RollbackAndCleanupAsync(snapshot, view);
                return UIOperationResult.Failed(operation.OperationId, operation.Action, view, exception);
            }
        }

        private async UniTask<UIOperationResult> ExecuteCloseAsync(
            QueuedUIOperation operation,
            CancellationToken cancellationToken)
        {
            var view = cacheStack.GetView(operation.TargetType);
            if (view == null)
            {
                return UIOperationResult.Canceled(operation.OperationId, operation.Action, null);
            }

            if (!layerStack.Contains(view))
            {
                await DestroyAndRemoveAsync(view);
                return UIOperationResult.Succeeded(operation.OperationId, operation.Action, view);
            }

            var snapshot = layerStack.Capture();
            try
            {
                await view.ExitAsync(new UITransitionContext(
                    operation.OperationId,
                    operation.Action,
                    null,
                    view,
                    operation.Animated), cancellationToken);
                layerStack.CommitClose(view);
                if (view.ViewMode != UIViewMode.Widget)
                {
                    view.Reference = Math.Max(0, view.Reference - 1);
                }

                var revealed = layerStack.StackTopView;
                if (revealed != null)
                {
                    await revealed.EnterAsync(new UITransitionContext(
                        operation.OperationId,
                        operation.Action,
                        revealed,
                        view,
                        operation.Animated), cancellationToken);
                    PlaceOnTopAndApplyMask(revealed);
                    CurrentUIName = revealed.Name;
                }
                else
                {
                    HideMask();
                    CurrentUIName = null;
                }

                LastCloseName = view.Name;
                OnClose?.Invoke(view);
                if (view.DestroyOnHide && view.Reference <= 0)
                {
                    await DestroyAndRemoveAsync(view);
                }

                return UIOperationResult.Succeeded(operation.OperationId, operation.Action, view);
            }
            catch (OperationCanceledException)
            {
                await RestoreSnapshotAsync(snapshot);
                return UIOperationResult.Canceled(operation.OperationId, operation.Action, view);
            }
            catch (Exception exception)
            {
                await RestoreSnapshotAsync(snapshot);
                return UIOperationResult.Failed(operation.OperationId, operation.Action, view, exception);
            }
        }

        private UniTask<UIOperationResult> ExecuteBackAsync(
            QueuedUIOperation operation,
            CancellationToken cancellationToken)
        {
            var target = layerStack.TopModal ?? layerStack.CurrentPage;
            return target == null
                ? UniTask.FromResult(UIOperationResult.Canceled(operation.OperationId, operation.Action, null))
                : ExecuteCloseAsync(new QueuedUIOperation(
                    operation.OperationId,
                    UINavigationAction.Back,
                    target.GetType(),
                    operation.Animated,
                    cancellationToken), cancellationToken);
        }

        private async UniTask<UIOperationResult> ExecutePreloadAsync(
            QueuedUIOperation operation,
            CancellationToken cancellationToken)
        {
            var view = cacheStack.GetOrCreateView(operation.TargetType);
            try
            {
                if (!view.IsLoaded && !await view.LoadAsync(
                        UIRootManager.Instance.GetRoot(view.Level), cancellationToken))
                {
                    throw new InvalidOperationException($"View 预加载失败: {view.Name}");
                }

                return UIOperationResult.Succeeded(operation.OperationId, operation.Action, view);
            }
            catch (OperationCanceledException)
            {
                await DestroyAndRemoveAsync(view);
                return UIOperationResult.Canceled(operation.OperationId, operation.Action, view);
            }
            catch (Exception exception)
            {
                await DestroyAndRemoveAsync(view);
                return UIOperationResult.Failed(operation.OperationId, operation.Action, view, exception);
            }
        }

        private async UniTask<UIOperationResult> ExecuteCloseAllAsync(
            QueuedUIOperation operation,
            CancellationToken cancellationToken)
        {
            var views = cacheStack.GetAllViews();
            foreach (var view in views)
            {
                view.Reference = 0;
                await view.DestroyAsync();
                cacheStack.Remove(view);
            }

            layerStack.Clear();
            HideMask();
            CurrentUIName = null;
            LastCloseName = null;
            return UIOperationResult.Canceled(operation.OperationId, operation.Action, null);
        }

        private async UniTask RollbackAndCleanupAsync(UIStackSnapshot snapshot, View failedView)
        {
            layerStack.Restore(snapshot);
            if (!snapshot.Contains(failedView))
            {
                await DestroyAndRemoveAsync(failedView);
            }

            await RestoreSnapshotAsync(snapshot);
        }

        private async UniTask RestoreSnapshotAsync(UIStackSnapshot snapshot)
        {
            layerStack.Restore(snapshot);
            var visible = layerStack.StackTopView;
            if (visible != null && visible.State == ViewState.Faulted)
            {
                visible.RestoreVisibleAfterNavigationFailure();
            }
            else if (visible != null && visible.IsLoaded && visible.State != ViewState.Visible)
            {
                await visible.EnterAsync(new UITransitionContext(
                    0,
                    UINavigationAction.Back,
                    visible,
                    null,
                    false), CancellationToken.None);
            }

            if (visible != null)
            {
                PlaceOnTopAndApplyMask(visible);
            }
            else
            {
                HideMask();
            }
        }

        private async UniTask DestroyAndRemoveAsync(View view)
        {
            if (view == null)
            {
                return;
            }

            layerStack?.CommitClose(view);
            await view.DestroyAsync();
            cacheStack.Remove(view);
        }

        private View GetPreviousView(View view)
        {
            return view.ViewMode switch
            {
                UIViewMode.Page => layerStack.CurrentPage,
                UIViewMode.Modal => layerStack.TopModal,
                UIViewMode.Widget => null,
                _ => null
            };
        }

        private static async UniTask ObserveOperationAsync(UniTask<UIOperationResult> task)
        {
            var result = await task;
            if (result.Status == UIOperationStatus.Failed)
            {
                throw result.Exception;
            }
        }

        private static void LogOperationFailure(Exception exception)
        {
            Debug.LogException(exception);
        }

        private void ConfigureMask()
        {
            var nextButton = UIRootManager.Instance.Mask?.GetComponent<Button>();
            if (maskButton == nextButton)
            {
                return;
            }

            if (maskButton != null)
            {
                maskButton.onClick.RemoveListener(Back);
            }

            maskButton = nextButton;
            if (maskButton != null)
            {
                maskButton.onClick.AddListener(Back);
            }
        }

        private void PlaceOnTopAndApplyMask(View view)
        {
            if (view.transform != null)
            {
                view.transform.SetAsLastSibling();
            }

            ApplyMask(view, true);
        }

        private void ApplyMask(View view, bool autoHide)
        {
            var mask = UIRootManager.Instance.Mask;
            if (mask == null)
            {
                return;
            }

            if (view.Mask == MaskType.None)
            {
                if (autoHide)
                {
                    HideMask();
                }

                return;
            }

            if (view.transform == null)
            {
                return;
            }

            mask.transform.SetParent(view.transform.parent, false);
            mask.transform.SetSiblingIndex(Mathf.Max(0, view.transform.GetSiblingIndex()));
            mask.transform.localScale = Vector3.one;
            mask.transform.localPosition = Vector3.zero;
            if (maskButton != null)
            {
                maskButton.interactable = view.Mask == MaskType.CloseRaycast;
            }
        }

        private void HideMask()
        {
            var mask = UIRootManager.Instance.Mask;
            if (mask != null)
            {
                mask.transform.localScale = Vector3.zero;
            }
        }
    }
}
