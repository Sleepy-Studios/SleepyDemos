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
        private UniTask<bool> loadingTask;
        private UniTask destroyingTask;
        private UniTaskCompletionSource<bool> loadingCompletionSource;
        private UniTaskCompletionSource destroyingCompletionSource;
        private bool hasLoadingTask;
        private bool hasDestroyingTask;
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

        public void AddSubView(View view)
        {
            if (view != null && !subViews.Contains(view))
            {
                subViews.Add(view);
            }
        }

        /// <summary>
        /// 加载 View 资源并完成组件与 Transition 初始化。重复调用会等待同一加载任务。
        /// </summary>
        /// <param name="parent">View 根对象挂载父节点。</param>
        /// <param name="cancellationToken">取消令牌；取消会继续抛给导航协调层。</param>
        /// <returns>成功完成初始化时返回 true；资源无效或加载失败时返回 false。</returns>
        public async UniTask<bool> LoadAsync(Transform parent, CancellationToken cancellationToken)
        {
            if (IsLoaded)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }

            if (State == ViewState.Destroying || State == ViewState.Destroyed || State == ViewState.Faulted)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }

            if (!hasLoadingTask)
            {
                loadingTask = LoadCoreAsync(parent, cancellationToken).Preserve();
                loadingCompletionSource = new UniTaskCompletionSource<bool>();
                hasLoadingTask = true;
                PublishLoadingResultAsync().Forget();
            }

            var loaded = await loadingCompletionSource.Task;
            cancellationToken.ThrowIfCancellationRequested();
            return loaded;
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
            OnBeforeInit();
            if (string.IsNullOrEmpty(Address))
            {
                FailLoad();
                return;
            }

            try
            {
                var instance = Loader.Instantiate(Address, parent);
                if (instance == null)
                {
                    FailLoad();
                    return;
                }

                CompleteLoad(instance);
            }
            catch (Exception)
            {
                FailLoad(gameObject);
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
            OnBeforeInit();
            if (target == null)
            {
                FailLoad();
                return;
            }

            try
            {
                CompleteLoad(target);
            }
            catch (Exception)
            {
                FailLoad(target);
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
                destroyingTask = DestroyCoreAsync().Preserve();
                destroyingCompletionSource = new UniTaskCompletionSource();
                hasDestroyingTask = true;
                PublishDestroyResultAsync().Forget();
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
                    FailLoad();
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
                    FailLoad();
                    return false;
                }

                CompleteLoad(instance);
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }
            catch (OperationCanceledException)
            {
                DisposeTransitionOnce();
                ReleaseInstanceOnce(instance ?? gameObject);
                gameObject = null;
                transform = null;
                UITransition = null;
                if (State != ViewState.Destroying && State != ViewState.Destroyed)
                {
                    State = ViewState.Faulted;
                }

                DisposeLoaderOnce();
                throw;
            }
            catch (Exception)
            {
                DisposeTransitionOnce();
                ReleaseInstanceOnce(instance ?? gameObject);
                gameObject = null;
                transform = null;
                UITransition = null;
                if (State != ViewState.Destroying && State != ViewState.Destroyed)
                {
                    State = ViewState.Faulted;
                }

                DisposeLoaderOnce();
                return false;
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

        private void FailLoad(GameObject instance = null)
        {
            DisposeTransitionOnce();
            ReleaseInstanceOnce(instance ?? gameObject);
            gameObject = null;
            transform = null;
            UITransition = null;
            State = ViewState.Faulted;
            DisposeLoaderOnce();
        }

        private async UniTask DestroyCoreAsync()
        {
            if (State == ViewState.Destroyed)
            {
                return;
            }

            State = ViewState.Destroying;
            Exception cleanupException = null;
            if (hasLoadingTask)
            {
                try
                {
                    await loadingCompletionSource.Task;
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception exception)
                {
                    cleanupException = exception;
                }
            }

            foreach (var subView in subViews)
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
            subViews.Clear();

            foreach (var binding in bindings)
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
            bindings.Clear();

            try
            {
                OnDestroy();
            }
            catch (Exception exception)
            {
                cleanupException ??= exception;
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
                ReleaseInstanceOnce(gameObject);
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
            State = ViewState.Destroyed;
            if (cleanupException != null)
            {
                throw cleanupException;
            }
        }

        private async UniTask PublishLoadingResultAsync()
        {
            try
            {
                loadingCompletionSource.TrySetResult(await loadingTask);
            }
            catch (OperationCanceledException exception)
            {
                loadingCompletionSource.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                loadingCompletionSource.TrySetException(exception);
            }
        }

        private async UniTask PublishDestroyResultAsync()
        {
            try
            {
                await destroyingTask;
                destroyingCompletionSource.TrySetResult();
            }
            catch (OperationCanceledException exception)
            {
                destroyingCompletionSource.TrySetCanceled(exception.CancellationToken);
            }
            catch (Exception exception)
            {
                destroyingCompletionSource.TrySetException(exception);
            }
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
