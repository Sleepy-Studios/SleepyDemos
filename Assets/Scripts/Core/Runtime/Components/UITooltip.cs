using System;
using UnityEngine;

namespace Core.Runtime
{
    /// 只管理尺寸和跟随；关闭与导航由宿主 View 处理。
    [DisallowMultipleComponent]
    public sealed class UITooltip : MonoBehaviour
    {
        [SerializeField] private UITipsPanel panel;
        [SerializeField] private RectTransform arrow;
        private RectTransform boundary;
        private RectTransform target;
        private Vector2 screenPoint;
        private bool followsTarget;
        private bool configured;
        private TooltipDirection preferred;
        private float gap;
        private Rect previousTarget;
        private Rect previousSafe;
        private bool layoutDirty;
        /// 目标销毁、禁用或移出可见范围时请求关闭。
        public event Action CloseRequested;
        /// 最终方向，便于维护定位行为。
        public TooltipDirection Direction { get; private set; }

        /// <summary>配置目标跟随，定位使用目标矩形。</summary>
        /// <param name="anchor">目标 UI。</param>
        /// <param name="direction">首选主体方向。</param>
        /// <param name="distance">主体到目标边缘的间距。</param>
        public void SetTarget(RectTransform anchor, TooltipDirection direction = TooltipDirection.Up, float distance = 12)
        {
            target = anchor;
            followsTarget = true;
            Configure(direction, distance);
        }

        /// <summary>配置固定屏幕点。</summary>
        /// <param name="position">像素屏幕坐标。</param>
        /// <param name="direction">首选主体方向。</param>
        /// <param name="distance">主体到目标点的间距，Canvas 单位。</param>
        public void SetScreenPoint(Vector2 position, TooltipDirection direction = TooltipDirection.Up, float distance = 12)
        {
            screenPoint = position;
            followsTarget = false;
            target = null;
            Configure(direction, distance);
        }

        /// 停止跟随，清除对业务目标的引用。
        public void ClearTarget() { configured = false; target = null; }

        /// 文本或字体变化时请求重新测量。
        public void RefreshLayout() { layoutDirty = true; Refresh(); }

        private void Configure(TooltipDirection direction, float distance)
        {
            boundary = transform as RectTransform;
            preferred = direction;
            gap = Mathf.Max(0, distance);
            configured = true;
            layoutDirty = true;
            Refresh();
        }

        private void LateUpdate() => Refresh();
        private void OnRectTransformDimensionsChange() => layoutDirty = true;
        private void OnDisable() => ClearTarget();

        private void Refresh()
        {
            if (!configured || boundary == null) return;
            Rect safe = TooltipPlacementUtil.GetSafeRect(boundary);
            Rect targetRect;
            if (followsTarget)
            {
                if (!TooltipPlacementUtil.TryGetTargetRect(target, boundary, out targetRect) || !safe.Overlaps(targetRect))
                {
                    ClearTarget();
                    CloseRequested?.Invoke();
                    return;
                }
            }
            else
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boundary, screenPoint,
                        TooltipPlacementUtil.GetCanvasCamera(boundary), out Vector2 point) || !safe.Contains(point))
                {
                    ClearTarget();
                    CloseRequested?.Invoke();
                    return;
                }
                targetRect = new Rect(point, Vector2.zero);
            }
            if (!layoutDirty && safe == previousSafe && targetRect == previousTarget) return;
            bool resize = layoutDirty || safe != previousSafe;
            layoutDirty = false;
            previousSafe = safe;
            previousTarget = targetRect;
            Vector2 size = resize ? panel.RefreshLayout(safe.size) : panel.Body.rect.size;
            TooltipPlacement result = TooltipPlacementUtil.Calculate(safe, targetRect, size, preferred, gap, arrow.rect.width * .5f + 6);
            Direction = result.Direction;
            RectTransform body = panel.Body;
            Vector2 pivotPosition = result.Body.min + Vector2.Scale(body.pivot, result.Body.size);
            body.position = boundary.TransformPoint(pivotPosition);
            arrow.position = boundary.TransformPoint(result.ArrowPosition);
            arrow.localRotation = Quaternion.Euler(0, 0, Direction switch
            {
                TooltipDirection.Up => 0,
                TooltipDirection.Down => 180,
                TooltipDirection.Left => 90,
                _ => -90
            });
        }
    }
}
