using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public class View
    {
        private IResourceLoader loader;
        private readonly List<IDisposable> bindings = new List<IDisposable>();
        private readonly List<View> subViews = new List<View>();
        private readonly object loadWaiterGate = new object();
        private UniTask<bool> loadingTask;
        private UniTask destroyingTask;
        private UniTask<Exception> ownedCleanupTask;
        private UniTaskCompletionSource<bool> loadingCompletionSource;
        private UniTaskCompletionSource destroyingCompletionSource;
        private UniTaskCompletionSource<Exception> ownedCleanupCompletionSource;
        private CancellationTokenSource loadLifetimeCancellation;
        private bool hasLoadingTask;
        private bool hasDestroyingTask;
        private bool hasOwnedCleanupTask;
        private bool loadOperationCompleted;
        private bool loadLifetimeCancellationRequested;
        private int activeLoadWaiters;
        private bool instanceReleased;
        private bool loaderDisposed;
        private bool transitionDisposed;

        public virtual string Address => string.Empty;
        public virtual UILayer Level => UILayer.Base;
        public virtual UIViewMode ViewMode => UIViewModeResolver.Resolve(Level);
        public virtual string WorldTransitionKey => string.Empty;
        public virtual bool EnableOnInit => true;
        public virtual bool IsAsync => true;
        public virtual string Name => GetType().Name;
        public virtual MaskType Mask => MaskType.None;
        public virtual bool DestroyOnHide => true;

        public IResourceLoader Loader
        {
            get => loader ??= ResourceServices.CreateLoader();
            set => loader = value;
        }

        public GameObject gameObject { get; private set; }
        public Transform transform { get; private set; }
        public ViewState State { get; private set; } = ViewState.Created;

        /// View 是否持有已完成初始化且尚未销毁的根对象。
        public bool IsLoaded => State == ViewState.LoadedHidden ||
                                State == ViewState.Entering ||
                                State == ViewState.Visible ||
                                State == ViewState.Exiting;

        /// 兼容旧调用方的启用态判断；进入中与可见态均视为启用。
        public bool IsEnable => State == ViewState.Entering || State == ViewState.Visible;

        /// 当前 View 生命周期内稳定持有的 UI Transition。
        public IUITransition UITransition { get; private set; }

        public int Reference { get; set; }
        public bool ForceDisable { get; set; }
        public virtual ICameraAnimation CameraAnimation { get; set; }
        public virtual IUIAnimation UIAnimation { get; set; }
        public bool IsWidget => Level >= UILayer.Decorate;

        /// 创建当前 View 使用的 UI Transition。
        protected virtual IUITransition CreateUITransition()
        {
            return new EmptyUITransition();
        }

        public IDisposable AddBinding(IDisposable binding)
        {
            if (binding != null && !bindings.Contains(binding))
            {
                bindings.Add(binding);
            }

            return binding;
        }

        /// <summary>
        /// 注册由当前 View 持有生命周期的子 View；重复注册同一实例会被忽略。
        /// </summary>
        /// <param name="view">需要随当前 View 一起销毁的子 View。</param>
        /// <exception cref="ArgumentException"><paramref name="view"/> 是当前 View 自身。</exception>
        /// <exception cref="InvalidOperationException">注册后会形成直接或间接所有权环。</exception>
        public void AddSubView(View view)
        {
            if (view == null)
            {
                return;
            }

            if (ReferenceEquals(view, this))
            {
                throw new ArgumentException("View 不能将自身注册为 subView。", nameof(view));
            }

            if (subViews.Contains(view))
            {
                return;
            }

            if (ContainsSubView(view, this, new HashSet<View>()))
            {
                throw new InvalidOperationException("添加 subView 会形成生命周期所有权环。");
            }

            subViews.Add(view);
        }

        /// <summary>
        /// 加载 View 资源并完成组件与 Transition 初始化。重复调用会等待同一加载任务。
        /// </summary>
        /// <param name="parent">View 根对象挂载父节点。</param>
        /// <param name="cancellationToken">取消令牌；取消会继续抛给导航协调层。</param>
        /// <returns>成功完成初始化时返回 true；资源无效或加载失败时返回 false。</returns>
        public async UniTask<bool> LoadAsync(Transform parent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsLoaded)
            {
                return true;
            }

            if (State == ViewState.Destroying || State == ViewState.Destroyed || State == ViewState.Faulted)
            {
                return false;
            }

            var shouldStart = RegisterLoadWaiterAndPrepareOperation(
                out var completionSource,
                out var lifetimeCancellation);
            var waiterTask = WaitForRegisteredLoadAsync(completionSource, cancellationToken);
            if (shouldStart)
            {
                StartLoadOperation(parent, completionSource, lifetimeCancellation);
            }

            return await waiterTask;
        }

        /// <summary>
        /// 同步初始化 View 资源并挂载到指定父节点；兼容旧调用方，迁移方向为 LoadAsync。
        /// </summary>
        /// <param name="parent">View 根对象挂载父节点。</param>
        public void Init(Transform parent)
        {
            if (State != ViewState.Created)
            {
                return;
            }

            State = ViewState.Loading;
            try
            {
                OnBeforeInit();
                if (string.IsNullOrEmpty(Address))
                {
                    BeginSynchronousFailureCleanup();
                    return;
                }

                var instance = Loader.Instantiate(Address, parent);
                if (instance == null)
                {
                    BeginSynchronousFailureCleanup();
                    return;
                }

                CompleteLoad(instance);
            }
            catch (Exception)
            {
                BeginSynchronousFailureCleanup(gameObject);
                throw;
            }
        }

        public virtual void OnBeforeInit()
        {
        }

        /// <summary>
        /// 异步初始化 View；兼容旧调用方，底层复用可取消的 LoadAsync 生命周期。
        /// </summary>
        /// <param name="parent">View 根对象挂载父节点。</param>
        /// <returns>初始化异步任务。</returns>
        public async UniTask InitAsync(Transform parent)
        {
            await LoadAsync(parent, CancellationToken.None);
        }

        /// <summary>
        /// 使用已有根对象完成 View 初始化；兼容列表组件创建的本地实例。
        /// </summary>
        /// <param name="target">由调用方创建并交给 View 生命周期管理的根对象。</param>
        public void InitWithGameObject(GameObject target)
        {
            if (State != ViewState.Created)
            {
                return;
            }

            State = ViewState.Loading;
            try
            {
                OnBeforeInit();
                if (target == null)
                {
                    BeginSynchronousFailureCleanup();
                    return;
                }

                CompleteLoad(target);
            }
            catch (Exception)
            {
                BeginSynchronousFailureCleanup(target);
                throw;
            }
        }

        /// <summary>
        /// 显示 View；兼容旧调用方，底层转发到 EnterAsync。
        /// </summary>
        /// <param name="animation">是否播放 Transition 与旧 IUIAnimation。</param>
        /// <returns>显示异步任务。</returns>
        public async UniTask Show(bool animation = true)
        {
            var context = new UITransitionContext(0, UINavigationAction.Push, this, null, animation);
            await EnterAsync(context, CancellationToken.None);
            if (State == ViewState.Visible && animation && UIAnimation != null)
            {
                await UIAnimation.Show();
            }
        }

        /// <summary>
        /// 隐藏 View；兼容旧调用方，底层转发到 ExitAsync。
        /// </summary>
        /// <param name="animation">是否播放 Transition 与旧 IUIAnimation。</param>
        /// <returns>隐藏异步任务。</returns>
        public async UniTask Hide(bool animation = true)
        {
            if (State == ViewState.Visible && animation && UIAnimation != null)
            {
                await UIAnimation.Hide();
            }

            var context = new UITransitionContext(0, UINavigationAction.Close, null, this, animation);
            await ExitAsync(context, CancellationToken.None);
        }

        /// 销毁 View；兼容旧调用方，底层复用 DestroyAsync 的同一清理任务。
        public UniTask Destroy()
        {
            return DestroyAsync();
        }

        /// 幂等销毁 View，并释放其持有的全部生命周期资源。
        public UniTask DestroyAsync()
        {
            if (!hasDestroyingTask)
            {
                var completionSource = new UniTaskCompletionSource();
                destroyingCompletionSource = completionSource;
                hasDestroyingTask = true;
                var task = DestroyCoreAsync().Preserve();
                destroyingTask = task;
                PublishDestroyResultAsync(task, completionSource).Forget();
            }

            return destroyingCompletionSource.Task;
        }

        internal async UniTask EnterAsync(
            UITransitionContext context,
            CancellationToken cancellationToken)
        {
            if (!IsLoaded || State == ViewState.Visible)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            State = ViewState.Entering;
            try
            {
                ForceDisable = false;
                gameObject.SetActive(true);
                OnShow();
                cancellationToken.ThrowIfCancellationRequested();
                if (context.Animated)
                {
                    await UITransition.EnterAsync(context, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                State = ViewState.Visible;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                State = ViewState.Faulted;
                throw;
            }
        }

        internal async UniTask ExitAsync(
            UITransitionContext context,
            CancellationToken cancellationToken)
        {
            if (!IsLoaded || State == ViewState.LoadedHidden)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return;
            }

            State = ViewState.Exiting;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (context.Animated)
                {
                    await UITransition.ExitAsync(context, cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                gameObject.SetActive(false);
                OnHide();
                State = ViewState.LoadedHidden;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                State = ViewState.Faulted;
                throw;
            }
        }

        // 导航事务回滚专用：过渡异常后恢复快照中原本可见的 View。
        internal void RestoreVisibleAfterNavigationFailure()
        {
            RestoreAfterNavigationFailure(ViewState.Visible, true);
        }

        // 导航事务根据表现快照恢复稳定状态，不重复触发生命周期 Hook。
        internal void RestoreAfterNavigationFailure(ViewState snapshotState, bool active)
        {
            if (gameObject == null || State == ViewState.Destroying || State == ViewState.Destroyed)
            {
                return;
            }

            try
            {
                var direction = snapshotState == ViewState.Visible
                    ? UITransitionDirection.Enter
                    : UITransitionDirection.Exit;
                UITransition?.CompleteImmediately(direction);
            }
            finally
            {
                ForceDisable = false;
                gameObject.SetActive(active);
                State = snapshotState == ViewState.Visible
                    ? ViewState.Visible
                    : ViewState.LoadedHidden;
            }
        }

        protected virtual void InitComponent()
        {
        }

        protected virtual void OnGameObjectInitialize()
        {
        }

        protected virtual void OnShow()
        {
        }

        protected virtual void OnHide()
        {
        }

        protected virtual void OnDestroy()
        {
        }

        private async UniTask<bool> LoadCoreAsync(Transform parent, CancellationToken cancellationToken)
        {
            State = ViewState.Loading;
            GameObject instance = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                OnBeforeInit();
                if (string.IsNullOrEmpty(Address))
                {
                    await CleanupFailedLoadAsync();
                    return false;
                }

                instance = await Loader.InstantiateAsync(Address, parent);
                cancellationToken.ThrowIfCancellationRequested();
                if (State == ViewState.Destroying || State == ViewState.Destroyed)
                {
                    ReleaseInstanceOnce(instance);
                    return false;
                }

                if (instance == null)
                {
                    await CleanupFailedLoadAsync();
                    return false;
                }

                CompleteLoad(instance);
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }
            catch (OperationCanceledException)
            {
                await CleanupFailedLoadAsync(instance);
                throw;
            }
            catch (Exception)
            {
                await CleanupFailedLoadAsync(instance);
                throw;
            }
        }

        private void CompleteLoad(GameObject instance)
        {
            gameObject = instance;
            transform = instance.transform;
            gameObject.SetActive(false);
            State = ViewState.LoadedHidden;
            InitComponent();
            UIAnimation?.Init(transform);
            UITransition = CreateUITransition() ?? new EmptyUITransition();
            UITransition.Initialize(transform);
            OnGameObjectInitialize();
        }

        private void BeginSynchronousFailureCleanup(GameObject instance = null)
        {
            State = ViewState.Faulted;
            CleanupSynchronousFailureAsync(instance).Forget();
        }

        private async UniTask DestroyCoreAsync()
        {
            if (State == ViewState.Destroyed)
            {
                return;
            }

            State = ViewState.Destroying;
            CancelLoadLifetime();
            Exception cleanupException = null;
            try
            {
                OnDestroy();
            }
            catch (Exception exception)
            {
                cleanupException = exception;
            }

            if (hasLoadingTask)
            {
                try
                {
                    await loadingCompletionSource.Task;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception)
                {
                    // LoadAsync 已负责传播加载主异常；DestroyAsync 只等待其清理完成。
                }
            }

            var ownedCleanupException = await WaitForOwnedCleanupAsync();
            cleanupException ??= ownedCleanupException;
            State = ViewState.Destroyed;
            if (cleanupException != null)
            {
                throw cleanupException;
            }
        }

        private async UniTask CleanupFailedLoadAsync(GameObject instance = null)
        {
            if (State != ViewState.Destroying && State != ViewState.Destroyed)
            {
                State = ViewState.Faulted;
            }

            var cleanupException = await WaitForOwnedCleanupAsync(instance);
            if (cleanupException != null)
            {
                Debug.LogException(cleanupException);
            }
        }

        private async UniTask CleanupSynchronousFailureAsync(GameObject instance)
        {
            var cleanupException = await WaitForOwnedCleanupAsync(instance);
            if (cleanupException != null)
            {
                Debug.LogException(cleanupException);
            }
        }

        private async UniTask<Exception> WaitForOwnedCleanupAsync(GameObject instance = null)
        {
            try
            {
                return await GetOrStartOwnedCleanupAsync(instance);
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private UniTask<Exception> GetOrStartOwnedCleanupAsync(GameObject instance)
        {
            if (!hasOwnedCleanupTask)
            {
                var completionSource = new UniTaskCompletionSource<Exception>();
                ownedCleanupCompletionSource = completionSource;
                hasOwnedCleanupTask = true;
                var task = CleanupOwnedResourcesCoreAsync(instance).Preserve();
                ownedCleanupTask = task;
                PublishOwnedCleanupResultAsync(task, completionSource).Forget();
            }

            return ownedCleanupCompletionSource.Task;
        }

        private async UniTask<Exception> CleanupOwnedResourcesCoreAsync(GameObject instance)
        {
            Exception cleanupException = null;
            var ownedSubViews = subViews.ToArray();
            subViews.Clear();
            foreach (var subView in ownedSubViews)
            {
                try
                {
                    await subView.DestroyAsync();
                }
                catch (Exception exception)
                {
                    cleanupException ??= exception;
                }
            }

            var ownedBindings = bindings.ToArray();
            bindings.Clear();
            foreach (var binding in ownedBindings)
            {
                try
                {
                    binding.Dispose();
                }
                catch (Exception exception)
                {
                    cleanupException ??= exception;
                }
            }
            try
            {
                DisposeTransitionOnce();
            }
            catch (Exception exception)
            {
                cleanupException ??= exception;
            }

            try
            {
                ReleaseInstanceOnce(instance ?? gameObject);
            }
            catch (Exception exception)
            {
                cleanupException ??= exception;
            }

            try
            {
                DisposeLoaderOnce();
            }
            catch (Exception exception)
            {
                cleanupException ??= exception;
            }

            gameObject = null;
            transform = null;
            UITransition = null;
            return cleanupException;
        }

        private async UniTask PublishOwnedCleanupResultAsync(
            UniTask<Exception> task,
            UniTaskCompletionSource<Exception> completionSource)
        {
            try
            {
                completionSource.TrySetResult(await task);
            }
            catch (Exception exception)
            {
                completionSource.TrySetException(exception);
            }
        }

        private async UniTask PublishLoadingResultAsync(
            UniTask<bool> task,
            UniTaskCompletionSource<bool> completionSource,
            CancellationTokenSource lifetimeCancellation)
        {
            try
            {
                var result = await task;
                MarkLoadOperationCompleted();
                completionSource.TrySetResult(result);
            }
            catch (OperationCanceledException exception)
            {
                MarkLoadOperationCompleted();
                completionSource.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                MarkLoadOperationCompleted();
                completionSource.TrySetException(exception);
            }
            finally
            {
                lifetimeCancellation.Dispose();
            }
        }

        private async UniTask PublishDestroyResultAsync(
            UniTask task,
            UniTaskCompletionSource completionSource)
        {
            try
            {
                await task;
                completionSource.TrySetResult();
            }
            catch (OperationCanceledException exception)
            {
                completionSource.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                completionSource.TrySetException(exception);
            }
        }

        private bool RegisterLoadWaiterAndPrepareOperation(
            out UniTaskCompletionSource<bool> completionSource,
            out CancellationTokenSource lifetimeCancellation)
        {
            lock (loadWaiterGate)
            {
                activeLoadWaiters++;
                if (hasLoadingTask)
                {
                    completionSource = loadingCompletionSource;
                    lifetimeCancellation = null;
                    return false;
                }

                completionSource = new UniTaskCompletionSource<bool>();
                lifetimeCancellation = new CancellationTokenSource();
                loadingCompletionSource = completionSource;
                loadLifetimeCancellation = lifetimeCancellation;
                hasLoadingTask = true;
                return true;
            }
        }

        private void StartLoadOperation(
            Transform parent,
            UniTaskCompletionSource<bool> completionSource,
            CancellationTokenSource lifetimeCancellation)
        {
            var task = LoadCoreAsync(parent, lifetimeCancellation.Token).Preserve();
            loadingTask = task;
            PublishLoadingResultAsync(task, completionSource, lifetimeCancellation).Forget();
        }

        private async UniTask<bool> WaitForRegisteredLoadAsync(
            UniTaskCompletionSource<bool> completionSource,
            CancellationToken cancellationToken)
        {
            try
            {
                return await completionSource.Task.AttachExternalCancellation(cancellationToken);
            }
            finally
            {
                UnregisterLoadWaiter();
            }
        }

        private void UnregisterLoadWaiter()
        {
            CancellationTokenSource cancellationToSignal = null;
            lock (loadWaiterGate)
            {
                activeLoadWaiters--;
                if (activeLoadWaiters == 0 &&
                    !loadOperationCompleted &&
                    !loadLifetimeCancellationRequested)
                {
                    loadLifetimeCancellationRequested = true;
                    cancellationToSignal = loadLifetimeCancellation;
                }
            }

            SignalLoadCancellation(cancellationToSignal);
        }

        private void CancelLoadLifetime()
        {
            CancellationTokenSource cancellationToSignal = null;
            lock (loadWaiterGate)
            {
                if (!loadOperationCompleted && !loadLifetimeCancellationRequested)
                {
                    loadLifetimeCancellationRequested = true;
                    cancellationToSignal = loadLifetimeCancellation;
                }
            }

            SignalLoadCancellation(cancellationToSignal);
        }

        private void MarkLoadOperationCompleted()
        {
            lock (loadWaiterGate)
            {
                loadOperationCompleted = true;
            }
        }

        private static void SignalLoadCancellation(CancellationTokenSource cancellation)
        {
            if (cancellation == null)
            {
                return;
            }

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Publisher 已完成并释放生命周期 CTS 时，无需再次发送取消信号。
            }
        }

        private static bool ContainsSubView(View root, View target, HashSet<View> visited)
        {
            if (ReferenceEquals(root, target))
            {
                return true;
            }

            if (!visited.Add(root))
            {
                return false;
            }

            foreach (var child in root.subViews)
            {
                if (child != null && ContainsSubView(child, target, visited))
                {
                    return true;
                }
            }

            return false;
        }

        private void DisposeTransitionOnce()
        {
            if (transitionDisposed || UITransition == null)
            {
                return;
            }

            transitionDisposed = true;
            UITransition.Dispose();
        }

        private void ReleaseInstanceOnce(GameObject instance)
        {
            if (instanceReleased || instance == null)
            {
                return;
            }

            instanceReleased = true;
            Loader.ReleaseInstance(instance);
        }

        private void DisposeLoaderOnce()
        {
            if (loaderDisposed || loader == null)
            {
                return;
            }

            loaderDisposed = true;
            loader.Dispose();
        }
    }
}
