using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.Runtime.Inputs
{
    /// 保存于页面/弹窗资源的菜单作用域；复用 Core EventSystem，不创建并行 UI 模块。
    public sealed class UIMenuScope : MonoBehaviour, ICancelHandler
    {
        [SerializeField] private Selectable firstSelection;
        private Selectable previousSelection;
        private MenuInputScope scope;
        private Selectable[] candidates = new Selectable[32];
        private readonly Dictionary<Selectable, Navigation> originalNavigation = new();
        private Selectable[] targets;
        private bool[] availability;
        private bool hierarchyChanged;
        /// 本作用域收到返回，由页面决定关闭、退层或离开。
        public event Action Canceled;
        private void OnEnable()
        {
            if (EventSystem.current == null) return;
            BuildNavigation();
            scope = new MenuInputScope(EventSystem.current, selectionRoot: transform);
            scope.SetContext(GameplayInputContext.Menu, First());
        }
        private void Update()
        {
            if (scope == null) return;
            bool changed = hierarchyChanged;
            for (int i = 0; targets != null && i < targets.Length; i++)
                changed |= availability[i] != Available(targets[i]);
            // 切换面板或解锁按钮时才重建，避免每帧查层级和分配数组。
            if (changed) BuildNavigation();
            scope.Update(First());
        }
        private void OnTransformChildrenChanged() => hierarchyChanged = true;
        internal void CaptureSelection()
        {
            var selected = EventSystem.current?.currentSelectedGameObject?.GetComponent<Selectable>();
            if (selected != null && selected.GetComponentInParent<UIMenuScope>(true) == this)
                previousSelection = selected;
        }
        private void OnDisable()
        {
            CaptureSelection();
            scope?.Dispose(); scope = null;
            foreach (var entry in originalNavigation) if (entry.Key != null) entry.Key.navigation = entry.Value;
            originalNavigation.Clear();
            targets = null; availability = null;
        }
        private void BuildNavigation()
        {
            targets = GetComponentsInChildren<Selectable>(true);
            availability = new bool[targets.Length];
            hierarchyChanged = false;
            for (int i = 0; i < targets.Length; i++) availability[i] = Available(targets[i]);
            foreach (var target in targets)
            {
                if (target.navigation.mode == Navigation.Mode.None || target.GetComponentInParent<UIMenuScope>() != this) continue;
                originalNavigation.TryAdd(target, target.navigation);
                var navigation = target.navigation; navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = Nearest(target, targets, Vector2.left);
                navigation.selectOnRight = Nearest(target, targets, Vector2.right);
                navigation.selectOnUp = Nearest(target, targets, Vector2.up);
                navigation.selectOnDown = Nearest(target, targets, Vector2.down);
                target.navigation = navigation;
            }
        }
        private bool Available(Selectable target) => target != null && target.IsActive() && target.IsInteractable() &&
            target.navigation.mode != Navigation.Mode.None && target.GetComponentInParent<UIMenuScope>() == this;
        private Selectable Nearest(Selectable source, Selectable[] targets, Vector2 direction)
        {
            Selectable best = null; float score = 0;
            foreach (var target in targets)
            {
                if (target == source || !Available(target)) continue;
                Vector2 offset = target.transform.position - source.transform.position;
                float dot = Vector2.Dot(direction, offset);
                if (dot <= 0 || offset.sqrMagnitude < .0001f) continue;
                float candidate = dot / offset.sqrMagnitude;
                if (candidate > score) { score = candidate; best = target; }
            }
            return best;
        }
        private GameObject First()
        {
            if (Available(previousSelection)) return previousSelection.gameObject;
            if (Available(firstSelection)) return firstSelection.gameObject;
            if (candidates.Length < Selectable.allSelectableCount) Array.Resize(ref candidates, Selectable.allSelectableCount + 16);
            int count = Selectable.AllSelectablesNoAlloc(candidates);
            for (int i = 0; i < count; i++)
            {
                var target = candidates[i];
                if (Available(target)) return target.gameObject;
            }
            return null;
        }
        /// <summary>消费本作用域内控件传来的返回，不主动提交按钮。</summary>
        /// <param name="value">公共 UI Module 的 Cancel 事件。</param>
        public void OnCancel(BaseEventData value) { Canceled?.Invoke(); value.Use(); }
    }
}
