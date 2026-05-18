using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    internal sealed class UIStack
    {
        internal readonly List<View> widgetList = new List<View>();
        internal StackData currentStack = new StackData();
        internal readonly Stack<StackData> jumpList = new Stack<StackData>();

        private readonly Graphic mask;
        private readonly Button maskButton;

        public UIStack()
        {
            mask = UIRootManager.Instance.Mask;
            if (mask != null)
            {
                maskButton = mask.GetComponent<Button>();
                if (maskButton != null)
                {
                    maskButton.onClick.AddListener(() => UIManager.Instance.Back());
                }
            }
        }

        public int TotalCount => currentStack.Count + widgetList.Count;
        public View StackTopView => currentStack.showList.Count == 0 ? null : currentStack.showList[currentStack.showList.Count - 1];
        public IReadOnlyList<View> ShowList => currentStack.showList;

        public int GetLayerCount(UILayer layer)
        {
            return currentStack.layerData.TryGetValue(layer, out var list) ? list.Count : 0;
        }

        public bool Contains(View view)
        {
            return currentStack.showList.Contains(view) || widgetList.Contains(view);
        }

        public bool CurrentStack(string stackName)
        {
            return currentStack.customName == stackName;
        }

        public View Add(View view)
        {
            var list = view.IsWidget ? widgetList : currentStack.showList;
            if (list.Contains(view))
            {
                list.Remove(view);
            }
            else if (!view.IsWidget)
            {
                view.Reference++;
            }

            list.Add(view);
            if (view.IsWidget)
            {
                return null;
            }

            if (!currentStack.layerData.TryGetValue(view.Level, out var layerStack))
            {
                layerStack = new List<View>();
                currentStack.layerData.Add(view.Level, layerStack);
            }

            var lastView = layerStack.Count > 0 ? layerStack[layerStack.Count - 1] : null;
            layerStack.Remove(view);
            layerStack.Add(view);
            return lastView;
        }

        public bool PrepareRemove(View view)
        {
            if (view.IsWidget)
            {
                widgetList.Remove(view);
                return true;
            }

            if (!currentStack.showList.Contains(view))
            {
                return false;
            }

            view.Reference--;
            currentStack.showList.Remove(view);
            return true;
        }

        public async UniTask<View> Remove(View view)
        {
            View nextView = null;
            if (currentStack.layerData.TryGetValue(view.Level, out var layerStack))
            {
                var index = layerStack.IndexOf(view);
                if (index >= 0)
                {
                    layerStack.RemoveAt(index);
                    if (index == layerStack.Count && layerStack.Count > 0)
                    {
                        nextView = layerStack[layerStack.Count - 1];
                    }
                }
            }

            nextView ??= StackTopView;
            if (nextView != null && nextView.gameObject != null)
            {
                nextView.transform.SetAsLastSibling();
                CheckNeedMask(nextView, true);
                await nextView.Show();
            }
            else
            {
                HideMask();
            }

            return nextView;
        }

        public void CheckNeedMask(View view, bool autoHide = false)
        {
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
            if (mask != null)
            {
                mask.transform.localScale = Vector3.zero;
            }
        }

        public void NewStack(string stackName)
        {
            if (currentStack.Count > 0)
            {
                jumpList.Push(currentStack);
            }
            currentStack = new StackData { customName = stackName };
        }

        public void RemoveStack()
        {
            currentStack.Clear();
            if (jumpList.Count > 0)
            {
                currentStack = jumpList.Pop();
            }
        }

        public void Clear()
        {
            widgetList.Clear();
            currentStack.Clear();
            jumpList.Clear();
            HideMask();
        }
    }
}
