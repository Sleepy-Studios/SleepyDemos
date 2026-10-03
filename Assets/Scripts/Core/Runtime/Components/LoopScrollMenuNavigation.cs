using System;
using System.Collections.Generic;
using SleepyStudios.LoopScroll;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.Runtime
{
    /// 为规则列表和 Grid 菜单维护数据焦点；仅控制选中和滚动，不接管 Submit 或菜单输入作用域。
    [DisallowMultipleComponent, RequireComponent(typeof(LoopScrollView))]
    public sealed class LoopScrollMenuNavigation : MonoBehaviour
    {
        private readonly Dictionary<LoopCell, LoopScrollMenuButton> buttons = new Dictionary<LoopCell, LoopScrollMenuButton>();
        private LoopScrollView list;
        private string selectedKey;
        private string pendingKey;
        private int pendingStep;
        private int pendingBand = -1;
        private bool restoreRecycledSelection;

        /// 当前可用的列表菜单焦点，交给页面唯一的 MenuInputScope 使用。
        public GameObject FirstSelection
        {
            get
            {
                LoopScrollMenuButton first = null;
                foreach (var pair in buttons)
                {
                    var button = pair.Value;
                    if (!CanSelect(button) || !IsInViewport(pair.Key)) continue;
                    if (button.Context.Key == pendingKey) return button.gameObject;
                    if (button.Context.Key == selectedKey) return button.gameObject;
                    if (first == null || button.Context.Index < first.Context.Index) first = button;
                }
                return first != null ? first.gameObject : null;
            }
        }

        private void OnEnable() { Initialize(GetComponent<LoopScrollView>()); }

        /// <summary>接入同一对象上的列表；重复初始化不会重复订阅。</summary>
        /// <param name="target">菜单列表，必须属于当前对象。</param>
        public void Initialize(LoopScrollView target)
        {
            if (target == null || target.gameObject != gameObject) throw new ArgumentException("导航组件应与列表位于同一对象。", nameof(target));
            if (list == target) return;
            list = target;
            list.CellBound += OnBound;
            list.CellUnbound += OnUnbound;
            list.DataChanged += OnDataChanged;
            // 允许导航组件晚于列表数据初始化，但不访问或修改包内部池与布局算法。
            foreach (var cell in target.GetComponentsInChildren<LoopCell>(true))
                if (cell.Context.IsCurrent) OnBound(cell, cell.Context);
        }

        private void OnDisable()
        {
            if (list != null)
            {
                list.CellBound -= OnBound;
                list.CellUnbound -= OnUnbound;
                list.DataChanged -= OnDataChanged;
            }
            foreach (var button in buttons.Values) ClearButton(button);
            buttons.Clear(); list = null; pendingKey = null; restoreRecycledSelection = false;
        }

        private void OnBound(LoopCell cell, CellBindContext context)
        {
            var button = cell.GetComponentInChildren<LoopScrollMenuButton>(true);
            if (button == null) return;
            ClearButton(button);
            button.Owner = this; button.Context = context;
            buttons[cell] = button;
        }

        private void OnUnbound(LoopCell cell, CellBindContext context)
        {
            if (!buttons.TryGetValue(cell, out var button)) return;
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == button.gameObject)
                restoreRecycledSelection = true;
            ClearButton(button); buttons.Remove(cell);
        }

        private static void ClearButton(LoopScrollMenuButton button)
        {
            if (button == null) return;
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == button.gameObject) events.SetSelectedGameObject(null);
            button.Context = default; button.Owner = null;
        }

        private void OnDataChanged()
        {
            if (!list.TryGetIndex(selectedKey, out _)) selectedKey = null;
            if (!list.TryGetIndex(pendingKey, out _)) pendingKey = null;
        }

        private void LateUpdate()
        {
            var events = EventSystem.current;
            if (restoreRecycledSelection && events != null && events.sendNavigationEvents)
            {
                var current = events.currentSelectedGameObject;
                var available = FirstSelection;
                var target = available != null ? available.GetComponent<LoopScrollMenuButton>() : null;
                if (current != null) restoreRecycledSelection = false;
                else if (target != null && target.Context.Key == selectedKey)
                {
                    restoreRecycledSelection = false;
                    events.SetSelectedGameObject(available);
                }
            }
            if (pendingKey == null || list == null || !list.TryGetIndex(pendingKey, out var index)) return;
            var cell = list.GetVisibleCell(index);
            if (cell != null && buttons.TryGetValue(cell, out var button) && button.Context.Key == pendingKey)
            {
                if (CanSelect(button))
                {
                    pendingKey = null;
                    EventSystem.current?.SetSelectedGameObject(button.gameObject);
                }
                else
                {
                    var next = index + pendingStep;
                    if (next < 0 || next >= list.Count || pendingBand >= 0 && next / GetLaneCount() != pendingBand) pendingKey = null;
                    else { pendingKey = list.GetItemKey(next); list.ScrollToCell(next, ScrollAlignment.Center); }
                }
            }
        }

        internal void RememberSelection(LoopScrollMenuButton button)
        {
            if (button.Context.IsCurrent) { selectedKey = button.Context.Key; pendingKey = null; restoreRecycledSelection = false; }
        }

        internal bool IsCurrentSelection(LoopScrollMenuButton button)
        {
            return button.Owner == this && button.Context.IsCurrent && button.Context.Key == selectedKey;
        }

        internal bool MoveSelection(LoopScrollMenuButton origin, MoveDirection direction)
        {
            if (list == null || !CanSelect(origin) || direction == MoveDirection.None) return false;
            var lanes = GetLaneCount();
            var index = origin.Context.Index;
            var alongAxis = list.IsVertical ? direction == MoveDirection.Up || direction == MoveDirection.Down
                : direction == MoveDirection.Left || direction == MoveDirection.Right;
            var increasing = direction == MoveDirection.Right || direction == MoveDirection.Down;
            var increment = alongAxis ? lanes : 1;
            // Grid 横轴不能从一行的末端跳到下一行的开头。
            if (!alongAxis && (!list.IsGrid || increasing && index % lanes == lanes - 1 || !increasing && index % lanes == 0)) return false;
            var target = index + (increasing ? increment : -increment);
            while (target >= 0 && target < list.Count)
            {
                var key = list.GetItemKey(target);
                var cell = list.GetVisibleCell(target);
                if (cell == null || !buttons.TryGetValue(cell, out var button) || CanSelect(button))
                {
                    pendingKey = key;
                    pendingStep = increasing ? increment : -increment;
                    pendingBand = alongAxis ? -1 : index / lanes;
                    list.ScrollToCell(target, ScrollAlignment.Center);
                    // 同步布局完成时立即选中；否则等待包的正常布局帧。
                    LateUpdate();
                    return true;
                }
                target += increasing ? increment : -increment;
                if (!alongAxis && target / lanes != index / lanes) break;
            }
            return false;
        }

        private int GetLaneCount()
        {
            // 读取包的真实布局，不能从残缺末行或缓冲池中的按钮数量推导列数。
            return list.LayoutLaneCount;
        }

        private bool IsInViewport(LoopCell cell)
        {
            if (list == null || list.ScrollRect.viewport == null) return false;
            var viewport = list.ScrollRect.viewport;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(viewport, cell.RectTransform);
            var rect = viewport.rect;
            return bounds.max.x > rect.xMin && bounds.min.x < rect.xMax && bounds.max.y > rect.yMin && bounds.min.y < rect.yMax;
        }

        private static bool CanSelect(LoopScrollMenuButton button)
        {
            return button != null && button.Context.IsCurrent && button.IsActive() && button.IsInteractable();
        }
    }
}
