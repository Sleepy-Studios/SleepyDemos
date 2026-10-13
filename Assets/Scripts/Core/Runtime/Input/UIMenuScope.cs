using System;
using System.Collections.Generic;
using SleepyStudios.LoopScroll;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
        private bool navigationEnabled = true;
        private GameObject revealedSelection;
        private readonly Vector3[] scrollCorners = new Vector3[4];
        /// 本作用域收到返回，由页面决定关闭、退层或离开。
        public event Action Canceled;
        private void OnEnable()
        {
            if (EventSystem.current == null) return;
            BuildNavigation();
            scope = new MenuInputScope(EventSystem.current, selectionRoot: transform);
            scope.SetContext(navigationEnabled ? GameplayInputContext.Menu : GameplayInputContext.Gameplay, First());
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
            RevealSelectedControl();
        }
        private void OnTransformChildrenChanged() => hierarchyChanged = true;

        /// <summary>临时抑制原生菜单导航，保留指针交互；恢复时等待确认和方向输入回到中立。</summary>
        /// <param name="enabled">改键监听等输入采集期间传 false，结束或取消后传 true。</param>
        public void SetNavigationEnabled(bool enabled)
        {
            if (navigationEnabled == enabled)
            {
                return;
            }
            navigationEnabled = enabled;
            scope?.SetContext(enabled ? GameplayInputContext.Menu : GameplayInputContext.Gameplay, First());
        }

        internal void CaptureSelection()
        {
            var eventSystem = EventSystem.current;
            var selectedObject = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            var selected = selectedObject != null ? selectedObject.GetComponent<Selectable>() : null;
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
            revealedSelection = null;
        }

        private void RevealSelectedControl()
        {
            // 指针滚动不被旧焦点拉回；键盘/手柄的新焦点才需要自动显露。
            if (InputDeviceState.ActiveDevice is not Keyboard && InputDeviceState.ActiveKind != InputDeviceKind.Gamepad)
            {
                revealedSelection = null;
                return;
            }
            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected == null || selected == revealedSelection || selected.GetComponentInParent<UIMenuScope>() != this
                || !selected.TryGetComponent<RectTransform>(out var target))
            {
                return;
            }
            revealedSelection = selected;
            Canvas.ForceUpdateCanvases();
            foreach (var scroll in selected.GetComponentsInParent<ScrollRect>())
            {
                if (!scroll.isActiveAndEnabled || scroll.content == null || scroll.viewport == null
                    || !scroll.transform.IsChildOf(transform) || !target.IsChildOf(scroll.content)
                    || scroll.TryGetComponent<LoopScrollView>(out _))
                {
                    continue;
                }
                var visible = scroll.viewport.rect;
                var item = BoundsInViewport(target, scroll.viewport);
                var content = BoundsInViewport(scroll.content, scroll.viewport);
                var next = scroll.normalizedPosition;
                float width = content.size.x - visible.width;
                float height = content.size.y - visible.height;
                if (scroll.horizontal && width > 0.01f)
                {
                    float offset = item.size.x > visible.width || item.min.x < visible.xMin ? visible.xMin - item.min.x
                        : item.max.x > visible.xMax ? visible.xMax - item.max.x : 0;
                    next.x = Mathf.Clamp01(next.x - offset / width);
                }
                if (scroll.vertical && height > 0.01f)
                {
                    float offset = item.size.y > visible.height || item.max.y > visible.yMax ? visible.yMax - item.max.y
                        : item.min.y < visible.yMin ? visible.yMin - item.min.y : 0;
                    next.y = Mathf.Clamp01(next.y - offset / height);
                }
                if ((next - scroll.normalizedPosition).sqrMagnitude > 0.000001f)
                {
                    scroll.StopMovement();
                    scroll.normalizedPosition = next;
                }
            }
        }

        private Bounds BoundsInViewport(RectTransform target, RectTransform viewport)
        {
            // 只计算控件本身，关闭的 Dropdown 选项或装饰子节点不扩大它的可见边界。
            target.GetWorldCorners(scrollCorners);
            var bounds = new Bounds(viewport.InverseTransformPoint(scrollCorners[0]), Vector3.zero);
            for (int index = 1; index < scrollCorners.Length; index++)
            {
                bounds.Encapsulate(viewport.InverseTransformPoint(scrollCorners[index]));
            }
            return bounds;
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
                // UI Module 不向父级冒泡 Cancel；已有取消处理的下拉框等控件保留自己的行为。
                if (target.GetComponent<ICancelHandler>() == null)
                {
                    target.gameObject.AddComponent<UIMenuCancelRelay>();
                }
                originalNavigation.TryAdd(target, target.navigation);
                if (originalNavigation[target].mode == Navigation.Mode.Explicit) continue;
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
