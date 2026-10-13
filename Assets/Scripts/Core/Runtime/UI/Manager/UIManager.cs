using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
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
        private readonly object worldTransitionProviderGate = new object();
        private IUIWorldTransitionProvider worldTransitionProvider;

        private UICache cacheStack;
        public string LastCloseName { get; private set; }
        public string CurrentUIName { get; private set; }
        public int StackCount => layerStack?.TotalCount ?? 0;
        public bool HasCloseAllBarrier => navigationCoordinator.HasCloseAllBarrier;

        /// 当前导航操作使用的世界过渡解析器；未注册时为 null。
        public IUIWorldTransitionProvider WorldTransitionProvider
        {
            get
            {
                lock (worldTransitionProviderGate)
                {
                    return worldTransitionProvider;
                }
            }
        }

        public event Action<View> OnBeginOpen;
        public event Action<View> OnOpen;
        public event Action<View> OnClose;
        public event Action<View> OnBehind;
        public Func<View, UniTask> OnBeforeOpen;

        protected override void OnSingletonInit()
        {
            cacheStack = new UICache();
            navigationCoordinator = new UINavigationCoordinator(
                ExecuteAsync,
                UIRootManager.Instance.InteractionGate);
        }

        public async UniTask InitializeAsync()
        {
            await UIRootManager.Instance.BuildUIRootAsync();
            layerStack ??= new UIStack();
            ConfigureMask();
            RefreshMaskFromTopModal();
        }

        /// <summary>
        /// 注册世界过渡解析器；传入 null 会清除当前解析器并恢复空过渡。
        /// </summary>
        /// <param name="provider">Hotfix 提供的解析器；为 null 时清除注册。</param>
        public void RegisterWorldTransitionProvider(IUIWorldTransitionProvider provider)
        {
            lock (worldTransitionProviderGate)
            {
                worldTransitionProvider = provider;
            }
        }

        /// <summary>日常业务打开入口；成功回调收到已初始化且进入完成的 View。</summary>
        /// <typeparam name="T">具体页面类型。</typeparam>
        /// <param name="setData">组件初始化后、OnShow 前交付数据；可省略。</param>
        /// <param name="asyncLoad">默认异步加载；false 使用真实同步资源入口，排队和过渡仍可能跨帧。</param>
        /// <param name="animation">是否播放进入和退出过渡，与资源加载方式独立。</param>
        /// <param name="hidePrevious">是否隐藏同层上一页面。</param>
        /// <param name="onOpened">本次导航完成后调用一次；失败或取消不调用。</param>
        /// <param name="onFailed">错误回调；省略时同步完成的错误直接抛出，稍后错误记录 Console。</param>
        /// <param name="cancellationToken">取消本次请求；正常取消不调用成功或失败回调。</param>
        public void Show<T>(Action<T> setData = null, bool asyncLoad = true, bool animation = true,
            bool hidePrevious = true, Action<T> onOpened = null, Action<Exception> onFailed = null,
            CancellationToken cancellationToken = default) where T : View
        {
            var options = new UIOpenOptions(animation, hidePrevious, asyncLoad);
            var operation = setData == null ? OpenAsync<T>(options, cancellationToken)
                : OpenAsync(setData, options, cancellationToken);
            if (!asyncLoad && operation.Status != UniTaskStatus.Pending)
            {
                T view;
                try
                {
                    // 只读取已完成任务，不阻塞排队或异步资源加载。
                    view = operation.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception exception)
                {
                    if (onFailed == null)
                    {
                        throw;
                    }

                    onFailed(exception);
                    return;
                }

                onOpened?.Invoke(view);
                return;
            }

            ObserveRequestAsync(operation, onOpened, onFailed).Forget(LogOperationFailure);
        }

        /// <summary>日常业务按类型关闭；等待资源释放的流程使用 CloseAsync。</summary>
        /// <typeparam name="T">目标页面类型。</typeparam>
        /// <param name="animation">是否播放退出过渡。</param>
        /// <param name="onClosed">关闭完成后的回调；空目标也视为完成。</param>
        /// <param name="onFailed">失败回调；省略时记录 Console。</param>
        /// <param name="cancellationToken">取消本次请求。</param>
        public void Close<T>(bool animation = true, Action onClosed = null, Action<Exception> onFailed = null,
            CancellationToken cancellationToken = default) where T : View
        {
            ObserveRequestAsync(CloseAsync<T>(animation, cancellationToken), onClosed, onFailed).Forget(LogOperationFailure);
        }

        /// <summary>日常业务关闭具体实例；过期或空实例幂等完成。</summary>
        /// <param name="view">由管理器持有的实例。</param>
        /// <param name="animation">是否播放退出过渡。</param>
        /// <param name="onClosed">关闭完成后的回调。</param>
        /// <param name="onFailed">失败回调；省略时记录 Console。</param>
        /// <param name="cancellationToken">取消本次请求。</param>
        public void Close(View view, bool animation = true, Action onClosed = null,
            Action<Exception> onFailed = null, CancellationToken cancellationToken = default)
        {
            ObserveRequestAsync(CloseAsync(view, animation, cancellationToken), onClosed, onFailed).Forget(LogOperationFailure);
        }

        private static async UniTask ObserveRequestAsync<T>(UniTask<T> operation, Action<T> completed,
            Action<Exception> failed)
        {
            T view;
            try
            {
                view = await operation;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                if (failed == null)
                {
                    LogOperationFailure(exception);
                }
                else
                {
                    failed(exception);
                }

                return;
            }

            // 已提交之后的业务回调异常交给观察者，不能当成导航取消吞掉。
            completed?.Invoke(view);
        }

        private static async UniTask ObserveRequestAsync(UniTask operation, Action completed, Action<Exception> failed)
        {
            try
            {
                await operation;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                if (failed == null)
                {
                    LogOperationFailure(exception);
                }
                else
                {
                    failed(exception);
                }

                return;
            }

            completed?.Invoke();
        }

        /// <summary>排队打开页面；完成初始化与进入后返回具体实例。</summary>
        /// <typeparam name="T">目标页面类型。</typeparam>
        /// <param name="options">默认播放过渡并隐藏同层上一页面。</param>
        /// <param name="cancellationToken">取消排队或当前导航；取消抛出 OperationCanceledException。</param>
        /// <returns>已打开的实例；重复打开返回同一实例，失败保留原异常。</returns>
        public UniTask<T> OpenAsync<T>(UIOpenOptions options = default,
            CancellationToken cancellationToken = default) where T : View
        {
            return CompleteViewAsync<T>(navigationCoordinator.EnqueueAsync(
                UINavigationAction.Push, typeof(T), options.Animated, cancellationToken,
                hidePrevious: options.HidePrevious, asyncLoad: options.AsyncLoad), cancellationToken);
        }

        /// <summary>打开页面，在组件初始化后、OnShow 前交付一次业务数据。</summary>
        /// <typeparam name="T">具体页面类型，不要求继承泛型 View。</typeparam>
        /// <param name="setData">例如 view => view.SetData(data)；加载失败或取消时不调用。</param>
        /// <param name="options">默认播放过渡并隐藏同层上一页面。</param>
        /// <param name="cancellationToken">取消本次导航，不影响后续独立请求。</param>
        /// <returns>具体实例；已经可见时仍交付本次数据，但不重复 OnShow。</returns>
        public UniTask<T> OpenAsync<T>(Action<T> setData, UIOpenOptions options = default,
            CancellationToken cancellationToken = default) where T : View
        {
            if (setData == null)
            {
                throw new ArgumentNullException(nameof(setData));
            }

            return CompleteViewAsync<T>(navigationCoordinator.EnqueueAsync(
                UINavigationAction.Push, typeof(T), options.Animated, cancellationToken,
                target => setData((T)target), hidePrevious: options.HidePrevious, asyncLoad: options.AsyncLoad), cancellationToken);
        }

        /// <summary>替换当前 Page；失败恢复原页面，不能用来替换 Modal 或 Widget。</summary>
        /// <typeparam name="T">目标 Page 类型。</typeparam>
        /// <param name="animated">默认播放退出和进入过渡。</param>
        /// <param name="cancellationToken">取消本次替换并恢复已提交表现。</param>
        /// <returns>替换后的具体实例；失败抛原异常。</returns>
        public UniTask<T> ReplaceAsync<T>(bool animated = true,
            CancellationToken cancellationToken = default) where T : View
        {
            return CompleteViewAsync<T>(navigationCoordinator.EnqueueAsync(
                UINavigationAction.Replace, typeof(T), animated, cancellationToken), cancellationToken);
        }

        /// <summary>替换 Page，并在初始化后、进入前交付业务数据。</summary>
        /// <typeparam name="T">目标 Page 类型。</typeparam>
        /// <param name="setData">由本次导航持有的数据交付回调。</param>
        /// <param name="animated">默认播放过渡；替换始终退出旧 Page。</param>
        /// <param name="cancellationToken">取消本次替换。</param>
        /// <returns>已进入的实例；失败恢复原页面并抛原异常。</returns>
        public UniTask<T> ReplaceAsync<T>(Action<T> setData, bool animated = true,
            CancellationToken cancellationToken = default) where T : View
        {
            if (setData == null)
            {
                throw new ArgumentNullException(nameof(setData));
            }

            return CompleteViewAsync<T>(navigationCoordinator.EnqueueAsync(
                UINavigationAction.Replace, typeof(T), animated, cancellationToken,
                target => setData((T)target)), cancellationToken);
        }

        /// <summary>关闭当前缓存中的指定类型；目标不存在时幂等完成。</summary>
        /// <typeparam name="T">目标页面类型。</typeparam>
        /// <param name="animated">默认播放退出过渡。</param>
        /// <param name="cancellationToken">取消本次关闭；真正取消抛出 OperationCanceledException。</param>
        public UniTask CloseAsync<T>(bool animated = true,
            CancellationToken cancellationToken = default) where T : View
        {
            return CompleteAsync(EnqueueCloseAsync(typeof(T), animated, cancellationToken),
                cancellationToken).AsUniTask();
        }

        /// <summary>仅关闭仍由管理器持有的具体实例；过期或外部持有的实例不被销毁。</summary>
        /// <param name="expectedView">Open/Preload 返回的实例；null 幂等完成。</param>
        /// <param name="animated">默认播放退出过渡。</param>
        /// <param name="cancellationToken">取消本次关闭。</param>
        public UniTask CloseAsync(View expectedView, bool animated = true,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromCanceled(cancellationToken);
            }

            if (expectedView == null || !ReferenceEquals(cacheStack.GetView(expectedView.GetType()), expectedView))
            {
                return UniTask.CompletedTask;
            }

            return CompleteAsync(navigationCoordinator.EnqueueAsync(
                UINavigationAction.Close, expectedView.GetType(), animated, cancellationToken,
                targetView: expectedView), cancellationToken).AsUniTask();
        }

        /// <summary>关闭最上层 Modal，否则关闭当前 Page；空栈幂等完成。</summary>
        /// <param name="animated">默认播放退出过渡。</param>
        /// <param name="cancellationToken">取消本次返回。</param>
        public UniTask BackAsync(bool animated = true, CancellationToken cancellationToken = default)
        {
            return CompleteAsync(navigationCoordinator.EnqueueAsync(
                UINavigationAction.Back, null, animated, cancellationToken), cancellationToken).AsUniTask();
        }

        /// <summary>取消旧导航并完整释放所有页面；取消只在必要清理完成后报告。</summary>
        /// <param name="cancellationToken">即使预先取消仍执行资源清理，完成后抛出取消异常。</param>
        public UniTask CloseAllAsync(CancellationToken cancellationToken = default)
        {
            return CompleteAsync(navigationCoordinator.EnqueueAsync(
                UINavigationAction.CloseAll, null, false, cancellationToken), cancellationToken).AsUniTask();
        }

        /// <summary>查询已有缓存，不创建、加载或显示页面。</summary>
        /// <typeparam name="T">目标页面类型。</typeparam>
        /// <returns>缓存实例或 null；隐藏实例仍可返回。</returns>
        public T Get<T>() where T : View => cacheStack.GetView(typeof(T)) as T;

        /// <summary>按注册名查询已有缓存，不创建或显示页面。</summary>
        /// <param name="uiName">View 类名或正式 UIBind 别名。</param>
        /// <returns>缓存实例或 null。</returns>
        public View Get(string uiName)
        {
            var type = UITypeReflection.Get(uiName);
            return type == null ? null : cacheStack.GetView(type);
        }

        /// <summary>隐藏准备页面，不入栈；已经入栈时只返回实例，不调用配置回调。</summary>
        /// <typeparam name="T">目标页面类型。</typeparam>
        /// <param name="configure">隐藏实例初始化完成后执行；可省略。</param>
        /// <param name="cancellationToken">取消本次准备；不释放此前已准备的实例。</param>
        /// <returns>准备好的实例；失败抛原异常。</returns>
        public UniTask<T> PreloadAsync<T>(Action<T> configure = null,
            CancellationToken cancellationToken = default) where T : View
        {
            return CompleteViewAsync<T>(navigationCoordinator.EnqueueAsync(
                UINavigationAction.Preload, typeof(T), false, cancellationToken,
                configure == null ? null : target => configure((T)target)), cancellationToken);
        }

        /// 已提交的顶层 Modal，其次为当前 Page。
        public View TopView => layerStack?.StackTopView;

        /// 缓存实例的只读快照，不提供缓存修改权限。
        public IReadOnlyList<View> GetAllViews() => cacheStack.GetAllViews().AsReadOnly();

        private static async UniTask<T> CompleteViewAsync<T>(UniTask<UIOperationResult> operation,
            CancellationToken cancellationToken) where T : View
        {
            var result = await CompleteAsync(operation, cancellationToken);
            return (T)result.View;
        }

        private static async UniTask<UIOperationResult> CompleteAsync(UniTask<UIOperationResult> operation,
            CancellationToken cancellationToken)
        {
            var result = await operation;
            if (result.Status == UIOperationStatus.Canceled)
            {
                throw new OperationCanceledException("UI 导航已取消。", cancellationToken);
            }

            if (result.Status == UIOperationStatus.Failed)
            {
                ExceptionDispatchInfo.Capture(result.Exception).Throw();
            }

            return result;
        }

        private UniTask<UIOperationResult> EnqueueCloseAsync(
            Type type,
            bool animated,
            CancellationToken cancellationToken)
        {
            var targetView = cacheStack.GetView(type);
            return navigationCoordinator.EnqueueAsync(
                UINavigationAction.Close,
                type,
                animated,
                cancellationToken,
                targetView: targetView);
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
            var created = false;
            var view = operation.TargetView;
            if (view != null &&
                (view.State == ViewState.Destroying || view.State == ViewState.Destroyed))
            {
                cacheStack.Remove(view);
                view = null;
            }
            else if (view?.State == ViewState.Faulted)
            {
                await TryCleanupAndRemoveAsync(view);
                view = null;
            }

            view ??= cacheStack.GetOrCreateView(operation.TargetType, out created);
            if (view == null)
            {
                return UIOperationResult.Failed(operation.OperationId, operation.Action, null,
                    new InvalidOperationException($"无法创建 View: {operation.TargetType}"));
            }

            var snapshot = layerStack.Capture();
            var presentation = CapturePresentation();
            var worldTransitions = new UIWorldTransitionTransaction(WorldTransitionProvider);

            if (replace && view.ViewMode != UIViewMode.Page)
            {
                var exception = new InvalidOperationException("Replace 首版仅支持 Page View。");
                if (created)
                {
                    await TryCleanupAndRemoveAsync(view);
                }

                RefreshMaskFromTopModal();

                return UIOperationResult.Failed(operation.OperationId, operation.Action, view, exception);
            }

            View previous = null;
            try
            {
                previous = GetPreviousView(view);
                if (previous == view && view.State == ViewState.Visible)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    operation.Configure?.Invoke(view);
                    cancellationToken.ThrowIfCancellationRequested();
                    RefreshMaskFromTopModal();
                    return UIOperationResult.Ignored(operation.OperationId, operation.Action, view);
                }

                // 新页面的输入作用域可能在加载时清焦点，先由底页保存自己的控件。
                previous?.CaptureMenuSelection();
                OnBeginOpen?.Invoke(view);
                var loaded = view.IsLoaded || await view.LoadAsync(
                    UIRootManager.Instance.GetViewRoot(view.Level),
                    cancellationToken, operation.AsyncLoad);
                if (!loaded)
                {
                    throw new InvalidOperationException($"View 加载失败: {view.Name}");
                }

                cancellationToken.ThrowIfCancellationRequested();
                operation.Configure?.Invoke(view);
                cancellationToken.ThrowIfCancellationRequested();
                await InvokeBeforeOpenAsync(view, cancellationToken);

                worldTransitions.Resolve(view, UITransitionDirection.Exit);
                if (operation.HidePrevious && previous != null && previous != view)
                {
                    worldTransitions.Resolve(previous, UITransitionDirection.Enter);
                }

                if (operation.HidePrevious && previous != null && previous != view)
                {
                    await ExitViewAsync(previous, new UITransitionContext(
                        operation.OperationId,
                        operation.Action,
                        view,
                        previous,
                        operation.Animated), worldTransitions, cancellationToken);
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

                await EnterViewAsync(view, new UITransitionContext(
                    operation.OperationId,
                    operation.Action,
                    view,
                    previous,
                    operation.Animated), worldTransitions, cancellationToken);
                PlaceOnTop(view);
                RefreshMaskFromTopModal();

                if (replace && previous != null && previous != view)
                {
                    previous.Reference = Math.Max(0, previous.Reference - 1);
                }
            }
            catch (OperationCanceledException)
            {
                await TryRollbackAsync(snapshot, presentation, view, worldTransitions);
                return UIOperationResult.Canceled(operation.OperationId, operation.Action, view);
            }
            catch (Exception exception)
            {
                await TryRollbackAsync(snapshot, presentation, view, worldTransitions);
                return UIOperationResult.Failed(operation.OperationId, operation.Action, view, exception);
            }

            // Commit point 之后的旧 View 销毁是 best-effort，不再回滚已提交的新栈。
            if (replace && previous != null && previous != view && previous.DestroyOnHide)
            {
                await TryPostCommitDestroyAndRemoveAsync(previous);
            }

            if (operation.HidePrevious && previous != null && previous != view)
            {
                LastCloseName = previous.Name;
                SafeInvoke(OnBehind, previous);
            }

            if (view.ViewMode != UIViewMode.Widget)
            {
                CurrentUIName = view.Name;
            }

            SafeInvoke(OnOpen, view);
            return UIOperationResult.Succeeded(operation.OperationId, operation.Action, view);
        }

        private async UniTask<UIOperationResult> ExecuteCloseAsync(
            QueuedUIOperation operation,
            CancellationToken cancellationToken)
        {
            // 实例关闭必须优先使用排队时捕获的对象。若同类型的新会话 View 已进入缓存，
            // 反过来按类型查询会让旧场景误关新场景 UI。
            var view = operation.TargetView ?? cacheStack.GetView(operation.TargetType);
            if (view == null)
            {
                return UIOperationResult.Ignored(operation.OperationId, operation.Action, null);
            }

            if (!ReferenceEquals(cacheStack.GetView(view.GetType()), view))
            {
                return UIOperationResult.Ignored(operation.OperationId, operation.Action, view);
            }

            if (!layerStack.Contains(view))
            {
                try
                {
                    await view.DestroyAsync();
                    return UIOperationResult.Succeeded(operation.OperationId, operation.Action, view);
                }
                catch (Exception exception)
                {
                    return UIOperationResult.Failed(
                        operation.OperationId,
                        operation.Action,
                        view,
                        exception);
                }
                finally
                {
                    cacheStack.Remove(view);
                }
            }

            var snapshot = layerStack.Capture();
            var presentation = CapturePresentation();
            var worldTransitions = new UIWorldTransitionTransaction(WorldTransitionProvider);
            try
            {
                worldTransitions.Resolve(view, UITransitionDirection.Enter);
                await ExitViewAsync(view, new UITransitionContext(
                    operation.OperationId,
                    operation.Action,
                    null,
                    view,
                    operation.Animated), worldTransitions, cancellationToken);
                layerStack.CommitClose(view);
                if (view.ViewMode != UIViewMode.Widget)
                {
                    view.Reference = Math.Max(0, view.Reference - 1);
                }

                var revealed = layerStack.StackTopView;
                if (revealed != null)
                {
                    worldTransitions.Resolve(revealed, UITransitionDirection.Exit);
                    await EnterViewAsync(revealed, new UITransitionContext(
                        operation.OperationId,
                        operation.Action,
                        revealed,
                        view,
                        operation.Animated), worldTransitions, cancellationToken);
                    PlaceOnTop(revealed);
                    CurrentUIName = revealed.Name;
                }
                else
                {
                    CurrentUIName = null;
                }

                RefreshMaskFromTopModal();

            }
            catch (OperationCanceledException)
            {
                await TryRollbackAsync(
                    snapshot,
                    presentation,
                    view,
                    worldTransitions);
                return UIOperationResult.Canceled(operation.OperationId, operation.Action, view);
            }
            catch (Exception exception)
            {
                await TryRollbackAsync(
                    snapshot,
                    presentation,
                    view,
                    worldTransitions);
                return UIOperationResult.Failed(operation.OperationId, operation.Action, view, exception);
            }

            // Commit point 之后的关闭目标销毁失败只记录，不复活已移栈 View。
            if (view.DestroyOnHide && view.Reference <= 0)
            {
                await TryPostCommitDestroyAndRemoveAsync(view);
            }

            LastCloseName = view.Name;
            SafeInvoke(OnClose, view);
            return UIOperationResult.Succeeded(operation.OperationId, operation.Action, view);
        }

        private UniTask<UIOperationResult> ExecuteBackAsync(
            QueuedUIOperation operation,
            CancellationToken cancellationToken)
        {
            var target = layerStack.TopModal ?? layerStack.CurrentPage;
            return target == null
                ? UniTask.FromResult(UIOperationResult.Ignored(operation.OperationId, operation.Action, null))
                : ExecuteCloseAsync(new QueuedUIOperation(
                    operation.OperationId,
                    UINavigationAction.Back,
                    target.GetType(),
                    operation.Animated,
                    cancellationToken,
                    null,
                    target,
                    true), cancellationToken);
        }

        private async UniTask<UIOperationResult> ExecutePreloadAsync(
            QueuedUIOperation operation,
            CancellationToken cancellationToken)
        {
            var view = cacheStack.GetView(operation.TargetType);
            if (view?.State == ViewState.Faulted || view?.State == ViewState.Destroying)
            {
                await TryCleanupAndRemoveAsync(view);
                view = null;
            }

            view ??= cacheStack.GetOrCreateView(operation.TargetType);
            if (layerStack.Contains(view))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return UIOperationResult.Ignored(operation.OperationId, operation.Action, view);
            }

            bool preparingHere = !view.IsLoaded;
            try
            {
                if (!view.IsLoaded && !await view.LoadAsync(
                        UIRootManager.Instance.GetViewRoot(view.Level), cancellationToken))
                {
                    throw new InvalidOperationException($"View 预加载失败: {view.Name}");
                }

                cancellationToken.ThrowIfCancellationRequested();
                operation.Configure?.Invoke(view);
                cancellationToken.ThrowIfCancellationRequested();
                return UIOperationResult.Succeeded(operation.OperationId, operation.Action, view);
            }
            catch (OperationCanceledException)
            {
                if (preparingHere)
                {
                    await TryCleanupAndRemoveAsync(view);
                }

                return UIOperationResult.Canceled(operation.OperationId, operation.Action, view);
            }
            catch (Exception exception)
            {
                if (preparingHere)
                {
                    await TryCleanupAndRemoveAsync(view);
                }

                return UIOperationResult.Failed(operation.OperationId, operation.Action, view, exception);
            }
        }

        private async UniTask<UIOperationResult> ExecuteCloseAllAsync(
            QueuedUIOperation operation,
            CancellationToken cancellationToken)
        {
            var views = cacheStack.GetAllViews();
            var exceptions = new System.Collections.Generic.List<Exception>();
            View failedView = null;
            var cancellationRequested = cancellationToken.IsCancellationRequested;
            try
            {
                foreach (var view in views)
                {
                    cancellationRequested |= cancellationToken.IsCancellationRequested;
                    view.Reference = 0;
                    try
                    {
                        await view.DestroyAsync();
                    }
                    catch (Exception exception)
                    {
                        failedView ??= view;
                        exceptions.Add(exception);
                    }

                    cancellationRequested |= cancellationToken.IsCancellationRequested;
                }
            }
            finally
            {
                cacheStack.Clear();
                layerStack.Clear();
                RefreshMaskFromTopModal();
                CurrentUIName = null;
                LastCloseName = null;
            }

            cancellationRequested |= cancellationToken.IsCancellationRequested;
            if (cancellationRequested)
            {
                foreach (var exception in exceptions)
                {
                    LogOperationFailure(exception);
                }

                return UIOperationResult.Canceled(operation.OperationId, operation.Action, null);
            }

            if (exceptions.Count > 0)
            {
                return UIOperationResult.Failed(
                    operation.OperationId,
                    operation.Action,
                    failedView,
                    new AggregateException(exceptions));
            }

            return UIOperationResult.Succeeded(operation.OperationId, operation.Action, null);
        }

        private static async UniTask EnterViewAsync(
            View view,
            UITransitionContext context,
            UIWorldTransitionTransaction worldTransitions,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (view.State == ViewState.Visible)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await RunTransitionPhaseAsync(
                token => view.EnterAsync(context, token),
                token => worldTransitions.EnterAsync(view, context, token),
                cancellationToken);
        }

        private static async UniTask ExitViewAsync(
            View view,
            UITransitionContext context,
            UIWorldTransitionTransaction worldTransitions,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!view.IsLoaded || view.State == ViewState.LoadedHidden)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await RunTransitionPhaseAsync(
                token => view.ExitAsync(context, token),
                token => worldTransitions.ExitAsync(view, context, token),
                cancellationToken);
        }

        private static async UniTask RunTransitionPhaseAsync(
            Func<CancellationToken, UniTask> runUI,
            Func<CancellationToken, UniTask> runWorld,
            CancellationToken cancellationToken)
        {
            using var phaseCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var phaseState = new TransitionPhaseState();
            var uiTask = RunTransitionMemberAsync(
                runUI,
                phaseCancellation,
                cancellationToken,
                phaseState).Preserve();
            var worldTask = RunTransitionMemberAsync(
                runWorld,
                phaseCancellation,
                cancellationToken,
                phaseState).Preserve();
            try
            {
                await UniTask.WhenAll(uiTask, worldTask);
                cancellationToken.ThrowIfCancellationRequested();
                phaseState.ThrowPrimaryException();
            }
            finally
            {
                TryCancelPhase(phaseCancellation);
            }
        }

        private static async UniTask RunTransitionMemberAsync(
            Func<CancellationToken, UniTask> run,
            CancellationTokenSource phaseCancellation,
            CancellationToken callerCancellation,
            TransitionPhaseState phaseState)
        {
            try
            {
                await run(phaseCancellation.Token);
            }
            catch (Exception exception)
            {
                var isInternalCancellation = exception is OperationCanceledException &&
                                             phaseCancellation.IsCancellationRequested &&
                                             !callerCancellation.IsCancellationRequested;
                phaseState.Record(exception, isInternalCancellation);
                TryCancelPhase(phaseCancellation);
            }
        }

        private static void TryCancelPhase(CancellationTokenSource phaseCancellation)
        {
            try
            {
                phaseCancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
            catch (AggregateException exception)
            {
                LogOperationFailure(exception);
            }
        }

        private sealed class TransitionPhaseState
        {
            private readonly object exceptionGate = new object();
            private Exception primaryException;
            private Exception fallbackException;

            internal void Record(Exception exception, bool isInternalCancellation)
            {
                lock (exceptionGate)
                {
                    fallbackException ??= exception;
                    if (!isInternalCancellation)
                    {
                        primaryException ??= exception;
                    }
                }
            }

            internal void ThrowPrimaryException()
            {
                Exception exception;
                lock (exceptionGate)
                {
                    exception = primaryException ?? fallbackException;
                }

                if (exception != null)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }
            }
        }

        private async UniTask InvokeBeforeOpenAsync(View view, CancellationToken cancellationToken)
        {
            var handlers = OnBeforeOpen;
            if (handlers == null)
            {
                return;
            }

            foreach (Func<View, UniTask> handler in handlers.GetInvocationList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await handler(view);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        private async UniTask TryRollbackAsync(
            UIStackSnapshot snapshot,
            UINavigationPresentationSnapshot presentation,
            View failedView,
            UIWorldTransitionTransaction worldTransitions)
        {
            try
            {
                layerStack.Restore(snapshot);
            }
            catch (Exception exception)
            {
                LogOperationFailure(exception);
            }

            var removeFailedView = failedView != null &&
                                   (failedView.State == ViewState.Faulted ||
                                    failedView.State == ViewState.Destroying ||
                                    failedView.State == ViewState.Destroyed ||
                                    !snapshot.Contains(failedView));
            if (removeFailedView)
            {
                try
                {
                    layerStack.CommitClose(failedView);
                }
                catch (Exception exception)
                {
                    LogOperationFailure(exception);
                }

            }

            presentation.Restore(removeFailedView ? failedView : null, LogOperationFailure);
            CurrentUIName = presentation.CurrentUIName;
            LastCloseName = presentation.LastCloseName;
            RefreshMaskFromTopModal();
            worldTransitions.Restore(LogOperationFailure);

            if (removeFailedView)
            {
                try
                {
                    await failedView.DestroyAsync();
                }
                catch (Exception exception)
                {
                    LogOperationFailure(exception);
                }
                finally
                {
                    cacheStack.Remove(failedView);
                }
            }
        }

        private async UniTask TryPostCommitDestroyAndRemoveAsync(View view)
        {
            if (view == null)
            {
                return;
            }

            try
            {
                await view.DestroyAsync();
            }
            catch (Exception exception)
            {
                LogOperationFailure(exception);
            }
            finally
            {
                cacheStack.Remove(view);
            }
        }

        private async UniTask TryCleanupAndRemoveAsync(View view)
        {
            if (view == null)
            {
                return;
            }

            try
            {
                layerStack?.CommitClose(view);
                RefreshMaskFromTopModal();
                await view.DestroyAsync();
            }
            catch (Exception exception)
            {
                LogOperationFailure(exception);
            }
            finally
            {
                cacheStack.Remove(view);
            }
        }

        private UINavigationPresentationSnapshot CapturePresentation()
        {
            return new UINavigationPresentationSnapshot(
                cacheStack.GetAllViews(),
                UIRootManager.Instance.Mask?.transform,
                maskButton,
                CurrentUIName,
                LastCloseName);
        }

        private static void SafeInvoke(Action<View> handlers, View view)
        {
            if (handlers == null)
            {
                return;
            }

            foreach (Action<View> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(view);
                }
                catch (Exception exception)
                {
                    LogOperationFailure(exception);
                }
            }
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

        private static void LogOperationFailure(Exception exception)
        {
            Debug.LogException(exception);
        }

        private void OnMaskClick() => BackAsync().Forget(exception =>
        {
            if (exception is not OperationCanceledException)
            {
                LogOperationFailure(exception);
            }
        });

        private void ConfigureMask()
        {
            var nextButton = UIRootManager.Instance.Mask?.GetComponent<Button>();
            if (maskButton == nextButton)
            {
                return;
            }

            if (maskButton != null)
            {
                maskButton.onClick.RemoveListener(OnMaskClick);
            }

            maskButton = nextButton;
            if (maskButton != null)
            {
                maskButton.onClick.AddListener(OnMaskClick);
            }
        }

        private void PlaceOnTop(View view)
        {
            if (view.transform != null)
            {
                view.transform.SetAsLastSibling();
            }

            UIRootManager.Instance.InteractionGate.EnsureOnTop();
        }

        private void RefreshMaskFromTopModal()
        {
            var modal = layerStack?.TopModal;
            if (modal == null || modal.Mask == MaskType.None)
            {
                HideMask();
                return;
            }

            ApplyMask(modal);
        }

        private void ApplyMask(View view)
        {
            var mask = UIRootManager.Instance.Mask;
            if (mask == null)
            {
                return;
            }

            if (view.transform == null)
            {
                return;
            }

            mask.transform.SetParent(view.transform.parent, false);
            var viewSiblingIndex = view.transform.GetSiblingIndex();
            var maskSiblingIndex = mask.transform.GetSiblingIndex();
            var targetSiblingIndex = maskSiblingIndex < viewSiblingIndex
                ? viewSiblingIndex - 1
                : viewSiblingIndex;
            mask.transform.SetSiblingIndex(Mathf.Max(0, targetSiblingIndex));
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

            if (maskButton != null)
            {
                maskButton.interactable = false;
            }
        }
    }
}
