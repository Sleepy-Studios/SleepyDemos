using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class UIManager : Singleton<UIManager>
    {
        private UIStack layerStack;

        public UICache cacheStack { get; private set; }
        public string LastCloseName { get; private set; }
        public string CurrentUIName { get; private set; }
        public int StackCount => layerStack.TotalCount;

        public event Action<View> OnBeginOpen;
        public event Action<View> OnOpen;
        public event Action<View> OnClose;
        public event Action<View> OnBehind;
        public event Action<string, bool> OnStackChange;
        public Func<View, UniTask> OnBeforeOpen;

        protected override void OnSingletonInit()
        {
            cacheStack = new UICache();
        }

        public async UniTask InitializeAsync()
        {
            await UIRootManager.Instance.BuildUIRoot();
            layerStack ??= new UIStack();
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

            OnBeginOpen?.Invoke(view);
            ShowAsync(view, hidePrevious).Forget();
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

        public T Show<T, TData1, TData2>(TData1 data1, TData2 data2, bool hidePrevious = true) where T : View<TData1, TData2>
        {
            var view = cacheStack.GetOrCreateView<T>();
            view.SetData(data1, data2);
            Show(typeof(T), hidePrevious);
            return view;
        }

        public T Get<T>() where T : View
        {
            return cacheStack.GetView(typeof(T)) as T;
        }

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

        public void Close<T>() where T : View
        {
            Close(typeof(T));
        }

        public void Close(Type type, bool animation = true)
        {
            var view = cacheStack.GetView(type);
            if (view == null || layerStack == null || !layerStack.PrepareRemove(view))
            {
                return;
            }

            HideAsync(view, animation).Forget();
        }

        public void Back()
        {
            if (layerStack?.StackTopView != null)
            {
                Close(layerStack.StackTopView.GetType());
            }
        }

        public void NewStack(string stackName)
        {
            layerStack?.NewStack(stackName);
            OnStackChange?.Invoke(stackName, true);
        }

        public void RemoveStack()
        {
            var name = layerStack?.currentStack.customName;
            layerStack?.RemoveStack();
            OnStackChange?.Invoke(name, false);
        }

        public int LayerStackCount(UILayer layer)
        {
            return layerStack?.GetLayerCount(layer) ?? 0;
        }

        public bool CurrentStackName(string stackName)
        {
            return layerStack != null && layerStack.CurrentStack(stackName);
        }

        private async UniTaskVoid ShowAsync(View view, bool hidePrevious)
        {
            await InitializeAsync();

            if (layerStack.StackTopView == view)
            {
                return;
            }

            view.OnBeforeInit();
            var lastView = layerStack.Add(view);
            if (lastView != null && lastView != view)
            {
                LastCloseName = lastView.Name;
                view.ForceDisable = hidePrevious;
                OnBehind?.Invoke(lastView);
            }

            var beforeOpen = OnBeforeOpen?.Invoke(view);
            if ((view.State & ViewState.Loaded) == 0)
            {
                await view.InitAsync(UIRootManager.Instance.GetRoot(view.Level));
            }

            if (beforeOpen.HasValue)
            {
                await beforeOpen.Value;
            }

            if (hidePrevious && lastView != null && lastView != view)
            {
                await lastView.Hide();
            }

            if (view.CameraAnimation != null)
            {
                await view.CameraAnimation.Show(view);
            }

            await view.Show();
            if (!view.IsWidget)
            {
                CurrentUIName = view.Name;
            }

            if (view.transform != null)
            {
                view.transform.SetAsLastSibling();
            }
            layerStack.CheckNeedMask(view);
            OnOpen?.Invoke(view);
        }

        private async UniTaskVoid HideAsync(View view, bool animation)
        {
            if (view.CameraAnimation != null)
            {
                await view.CameraAnimation.Hide(view);
            }

            await view.Hide(animation);
            await layerStack.Remove(view);
            OnClose?.Invoke(view);

            if (view.DestroyOnHide && view.Reference <= 0)
            {
                await view.Destroy();
                cacheStack.Remove(view.GetType());
            }
        }
    }
}
