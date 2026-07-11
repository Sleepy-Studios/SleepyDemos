using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public class View
    {
        private IResourceLoader loader;
        private readonly List<IDisposable> bindings = new List<IDisposable>();
        private readonly List<View> subViews = new List<View>();
        private UniTask<GameObject> loadingTask;
        private bool isLoading;

        public virtual string Address => string.Empty;
        public virtual UILayer Level => UILayer.Base;
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
        public ViewState State { get; private set; }
        public bool IsEnable => (State & ViewState.Enabled) != 0;
        public int Reference { get; set; }
        public bool ForceDisable { get; set; }
        public virtual ICameraAnimation CameraAnimation { get; set; }
        public virtual IUIAnimation UIAnimation { get; set; }
        public bool IsWidget => Level >= UILayer.Decorate;

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
        /// 同步初始化 View 资源并挂载到指定父节点。资源已初始化时会直接返回；加载失败时保持未加载状态。
        /// </summary>
        /// <param name="parent">View 根对象挂载父节点。</param>
        public void Init(Transform parent)
        {
            if ((State & ViewState.FirstInit) != 0)
            {
                return;
            }

            State |= ViewState.FirstInit;
            if (string.IsNullOrEmpty(Address))
            {
                Debug.LogError($"{GetType().FullName} 的 Address 为空");
                State &= ~ViewState.FirstInit;
                return;
            }

            gameObject = Loader.Instantiate(Address, parent);
            if (gameObject == null)
            {
                State &= ~ViewState.FirstInit;
                return;
            }

            if ((State & ViewState.Destroyed) != 0)
            {
                Loader.ReleaseInstance(gameObject);
                gameObject = null;
                return;
            }

            CompleteInit();
        }

        public virtual void OnBeforeInit()
        {
        }

        /// <summary>
        /// 异步初始化 View 资源并挂载到指定父节点。重复调用会复用正在进行的加载任务。
        /// </summary>
        /// <param name="parent">View 根对象挂载父节点。</param>
        /// <returns>初始化异步任务。</returns>
        public async UniTask InitAsync(Transform parent)
        {
            if ((State & ViewState.FirstInit) != 0)
            {
                if (isLoading)
                {
                    await loadingTask;
                }

                return;
            }

            State |= ViewState.FirstInit;
            if (string.IsNullOrEmpty(Address))
            {
                Debug.LogError($"{GetType().FullName} 的 Address 为空");
                return;
            }

            isLoading = true;
            loadingTask = Loader.InstantiateAsync(Address, parent);
            gameObject = await loadingTask;
            isLoading = false;
            if (gameObject == null)
            {
                State &= ~ViewState.FirstInit;
                return;
            }

            if ((State & ViewState.Destroyed) != 0)
            {
                Loader.ReleaseInstance(gameObject);
                gameObject = null;

                return;
            }

            CompleteInit();
        }

        public void InitWithGameObject(GameObject target)
        {
            if ((State & ViewState.FirstInit) != 0 || target == null)
            {
                return;
            }

            State |= ViewState.FirstInit;
            gameObject = target;
            CompleteInit();
        }

        private void CompleteInit()
        {
            if (gameObject == null)
            {
                return;
            }

            State |= ViewState.Loaded;
            transform = gameObject.transform;
            gameObject.SetActive(EnableOnInit && !ForceDisable);
            InitComponent();
            UIAnimation?.Init(transform);
            OnGameObjectInitialize();
        }

        public async UniTask Show(bool animation = true)
        {
            if (gameObject == null || (State & ViewState.Destroyed) != 0)
            {
                return;
            }

            ForceDisable = false;
            gameObject.SetActive(true);
            State &= ~ViewState.Disabled;
            State |= ViewState.Enabled;
            OnShow();
            if (animation && UIAnimation != null)
            {
                await UIAnimation.Show();
            }
        }

        public async UniTask Hide(bool animation = true)
        {
            if (gameObject == null || (State & ViewState.Destroyed) != 0)
            {
                return;
            }

            if (animation && UIAnimation != null)
            {
                await UIAnimation.Hide();
            }

            gameObject.SetActive(false);
            State &= ~ViewState.Enabled;
            State |= ViewState.Disabled;
            OnHide();
        }

        public async UniTask Destroy()
        {
            if ((State & ViewState.Destroyed) != 0)
            {
                return;
            }

            State = ViewState.Destroyed;
            if (isLoading)
            {
                await loadingTask;
            }

            foreach (var subView in subViews)
            {
                await subView.Destroy();
            }
            subViews.Clear();

            foreach (var binding in bindings)
            {
                binding.Dispose();
            }
            bindings.Clear();

            OnDestroy();
            if (gameObject != null)
            {
                Loader.ReleaseInstance(gameObject);
            }

            Loader.Dispose();
            gameObject = null;
            transform = null;
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
    }
}
