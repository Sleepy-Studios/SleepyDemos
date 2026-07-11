using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public sealed class UIManager : Singleton<UIManager>
    {
        private UIStack layerStack;
        private Button maskButton;
        private readonly HashSet<Type> openingTypes = new HashSet<Type>();
        private readonly HashSet<Type> closingTypes = new HashSet<Type>();

        public UICache cacheStack { get; private set; }
        public string LastCloseName { get; private set; }
        public string CurrentUIName { get; private set; }
        public int StackCount => layerStack?.TotalCount ?? 0;

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
            ConfigureMask();
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

            if (openingTypes.Contains(type))
            {
                return view;
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
            if (view == null || layerStack == null || closingTypes.Contains(type) || !layerStack.Contains(view))
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

        public async UniTask Preload<T>() where T : View
        {
            await InitializeAsync();
            var view = cacheStack.GetOrCreateView<T>();
            if (view == null || (view.State & ViewState.Loaded) != 0)
            {
                return;
            }

            await view.InitAsync(UIRootManager.Instance.GetRoot(view.Level));
            if (!view.IsEnable)
            {
                await view.Hide(false);
            }
        }

        public async UniTask Preload<T, TData>(TData data) where T : View<TData>
        {
            var view = cacheStack.GetOrCreateView<T>();
            view.SetData(data);
            await Preload<T>();
        }

        public async UniTask CloseAll(bool animation = false)
        {
            if (layerStack == null)
            {
                return;
            }

            var views = cacheStack.GetAllViews();
            for (int i = 0; i < views.Count; i++)
            {
                var view = views[i];
                if (view == null)
                {
                    continue;
                }

                view.Reference = 0;
                if ((view.State & ViewState.Loaded) != 0)
                {
                    await view.Hide(animation);
                }

                await view.Destroy();
            }

            cacheStack.Clear();
            layerStack.Clear();
            HideMask();
            CurrentUIName = null;
            LastCloseName = null;
        }

        public View GetStackTopView()
        {
            return layerStack?.StackTopView;
        }

        public Type GetStackTopViewType()
        {
            return layerStack?.StackTopView?.GetType();
        }

        public void NewStack(string stackName)
        {
            layerStack?.NewStack(stackName);
            OnStackChange?.Invoke(stackName, true);
        }

        public void RemoveStack()
        {
            var name = layerStack?.CurrentStackName;
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
            var type = view.GetType();
            openingTypes.Add(type);
            closingTypes.Remove(type);
            await InitializeAsync();

            try
            {
                var lastView = GetPreviousView(view);
                if (lastView == view)
                {
                    return;
                }

                view.OnBeforeInit();
                var wasContained = layerStack.Contains(view);
                layerStack.CommitShow(view);
                if (!wasContained && view.ViewMode != UIViewMode.Widget)
                {
                    view.Reference++;
                }

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
                if (view.ViewMode != UIViewMode.Widget)
                {
                    CurrentUIName = view.Name;
                }

                if (view.transform != null)
                {
                    view.transform.SetAsLastSibling();
                }
                ApplyMask(view);
                OnOpen?.Invoke(view);
            }
            finally
            {
                openingTypes.Remove(type);
            }
        }

        private async UniTaskVoid HideAsync(View view, bool animation)
        {
            var type = view.GetType();
            closingTypes.Add(type);
            try
            {
                if (view.CameraAnimation != null)
                {
                    await view.CameraAnimation.Hide(view);
                }

                await view.Hide(animation);
                if (layerStack.CommitClose(view) && view.ViewMode != UIViewMode.Widget)
                {
                    view.Reference--;
                }

                var nextView = layerStack.StackTopView;
                if (nextView != null)
                {
                    if (!nextView.IsEnable)
                    {
                        await nextView.Show();
                    }

                    if (nextView.transform != null)
                    {
                        nextView.transform.SetAsLastSibling();
                    }

                    ApplyMask(nextView, true);
                }
                else
                {
                    HideMask();
                }

                OnClose?.Invoke(view);

                if (view.DestroyOnHide && view.Reference <= 0)
                {
                    await view.Destroy();
                    cacheStack.Remove(view.GetType());
                }
            }
            finally
            {
                closingTypes.Remove(type);
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

        private void ApplyMask(View view, bool autoHide = false)
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
                var colors = maskButton.colors;
                colors.disabledColor = Color.white;
                maskButton.colors = colors;
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
