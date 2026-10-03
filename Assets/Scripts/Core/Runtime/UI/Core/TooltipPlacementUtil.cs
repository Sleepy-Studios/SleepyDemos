using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public enum TooltipDirection { Up, Down, Left, Right }

    public readonly struct TooltipPlacement
    {
        public TooltipPlacement(Rect body, TooltipDirection direction, Vector2 arrowPosition)
        {
            Body = body;
            Direction = direction;
            ArrowPosition = arrowPosition;
        }
        /// 边界本地坐标中的最终主体矩形。
        public Rect Body { get; }
        /// 主体相对目标的方向。
        public TooltipDirection Direction { get; }
        /// 箭头与主体连接端的本地坐标。
        public Vector2 ArrowPosition { get; }
    }

    /// 所有候选与收拢都在边界本地坐标计算；先完成尺寸适配，再定位。
    public static class TooltipPlacementUtil
    {
        private static readonly Vector3[] Corners = new Vector3[4];

        /// <summary>取得 Canvas 与屏幕安全区相交的本地边界。</summary>
        /// <param name="boundary">Tips 所在的平面 RectTransform。</param>
        /// <param name="margin">边界本地单位内边距，默认 20。</param>
        public static Rect GetSafeRect(RectTransform boundary, float margin = 20)
        {
            Camera camera = GetCanvasCamera(boundary);
            Rect screen = Screen.safeArea;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boundary, screen.min, camera, out Vector2 min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(boundary, screen.max, camera, out Vector2 max);
            Rect area = boundary.rect;
            float left = Mathf.Max(area.xMin, Mathf.Min(min.x, max.x));
            float right = Mathf.Min(area.xMax, Mathf.Max(min.x, max.x));
            float bottom = Mathf.Max(area.yMin, Mathf.Min(min.y, max.y));
            float top = Mathf.Min(area.yMax, Mathf.Max(min.y, max.y));
            float mx = Mathf.Min(Mathf.Max(0, margin), Mathf.Max(0, right - left) * .5f);
            float my = Mathf.Min(Mathf.Max(0, margin), Mathf.Max(0, top - bottom) * .5f);
            return Rect.MinMaxRect(left + mx, bottom + my, right - mx, top - my);
        }

        /// <summary>投影目标矩形，不使用目标 Pivot 作为位置。</summary>
        /// <param name="target">可来自另一 Canvas 的 UI 目标。</param>
        /// <param name="boundary">Tips 的坐标边界。</param>
        /// <param name="rect">输出目标在边界中的矩形。</param>
        /// <returns>目标无效、禁用或在相机背后时返回 false。</returns>
        public static bool TryGetTargetRect(RectTransform target, RectTransform boundary, out Rect rect)
        {
            rect = default;
            if (target == null || boundary == null || !target.gameObject.activeInHierarchy) return false;
            if (!TryProjectRect(target, boundary, out rect)) return false;
            // 滚动列表内的目标可能仍在屏幕内，却已经被父级 Viewport 裁掉。
            for (Transform parent = target.parent; parent != null; parent = parent.parent)
            {
                var rectMask = parent.GetComponent<RectMask2D>();
                var mask = parent.GetComponent<Mask>();
                if ((rectMask == null || !rectMask.IsActive()) && (mask == null || !mask.IsActive())) continue;
                if (!TryProjectRect(parent as RectTransform, boundary, out Rect clip)) return false;
                if (!rect.Overlaps(clip)) return false;
                rect = Rect.MinMaxRect(Mathf.Max(rect.xMin, clip.xMin), Mathf.Max(rect.yMin, clip.yMin),
                    Mathf.Min(rect.xMax, clip.xMax), Mathf.Min(rect.yMax, clip.yMax));
            }
            return true;
        }

        private static bool TryProjectRect(RectTransform target, RectTransform boundary, out Rect rect)
        {
            rect = default;
            if (target == null) return false;
            Camera targetCamera = GetCanvasCamera(target);
            Camera boundaryCamera = GetCanvasCamera(boundary);
            target.GetWorldCorners(Corners);
            Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < 4; i++)
            {
                if (targetCamera != null && targetCamera.WorldToScreenPoint(Corners[i]).z <= 0) return false;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(targetCamera, Corners[i]);
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(boundary, screen, boundaryCamera, out Vector2 point)) return false;
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
            rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            return true;
        }

        /// <summary>取得 UI 投影使用的 Camera。</summary>
        /// <param name="rect">所属 Canvas 中的节点；Overlay 返回 null。</param>
        public static Camera GetCanvasCamera(RectTransform rect)
        {
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        /// <summary>依次尝试首选、对侧和另一轴，最后收拢主体并调整箭头。</summary>
        /// <param name="boundary">已经扣除安全边距的本地边界。</param>
        /// <param name="target">本地目标矩形；点目标可传零尺寸矩形。</param>
        /// <param name="size">最终主体尺寸。</param>
        /// <param name="preferred">主体相对目标的首选方向。</param>
        /// <param name="gap">主体到目标边缘距离，默认 12。</param>
        /// <param name="arrowHalfWidth">箭头连接端半宽，用于避免超过圆角边缘。</param>
        public static TooltipPlacement Calculate(Rect boundary, Rect target, Vector2 size, TooltipDirection preferred = TooltipDirection.Up,
            float gap = 12, float arrowHalfWidth = 10)
        {
            size = Vector2.Max(Vector2.zero, size);
            gap = Mathf.Max(0, gap);
            TooltipDirection perpendicular = preferred == TooltipDirection.Up || preferred == TooltipDirection.Down
                ? (target.center.x - boundary.xMin >= boundary.xMax - target.center.x ? TooltipDirection.Left : TooltipDirection.Right)
                : (target.center.y - boundary.yMin >= boundary.yMax - target.center.y ? TooltipDirection.Down : TooltipDirection.Up);
            TooltipDirection bestDirection = preferred;
            Rect best = Candidate(target, size, preferred, gap);
            float overflow = Overflow(best, boundary);
            for (int i = 1; i < 4 && overflow > .01f; i++)
            {
                TooltipDirection direction = i == 1 ? Opposite(preferred) : i == 2 ? perpendicular : Opposite(perpendicular);
                Rect candidate = Candidate(target, size, direction, gap);
                float next = Overflow(candidate, boundary);
                if (next < overflow) { best = candidate; bestDirection = direction; overflow = next; }
            }
            best.x = size.x > boundary.width ? boundary.center.x - size.x * .5f : Mathf.Clamp(best.x, boundary.xMin, boundary.xMax - size.x);
            best.y = size.y > boundary.height ? boundary.center.y - size.y * .5f : Mathf.Clamp(best.y, boundary.yMin, boundary.yMax - size.y);
            float insetX = Mathf.Min(Mathf.Max(0, arrowHalfWidth), best.width * .5f);
            float insetY = Mathf.Min(Mathf.Max(0, arrowHalfWidth), best.height * .5f);
            float x = Mathf.Clamp(target.center.x, best.xMin + insetX, best.xMax - insetX);
            float y = Mathf.Clamp(target.center.y, best.yMin + insetY, best.yMax - insetY);
            Vector2 arrow = bestDirection switch
            {
                TooltipDirection.Up => new Vector2(x, best.yMin),
                TooltipDirection.Down => new Vector2(x, best.yMax),
                TooltipDirection.Left => new Vector2(best.xMax, y),
                _ => new Vector2(best.xMin, y)
            };
            return new TooltipPlacement(best, bestDirection, arrow);
        }

        private static Rect Candidate(Rect target, Vector2 size, TooltipDirection direction, float gap)
        {
            Vector2 min = target.center - size * .5f;
            switch (direction)
            {
                case TooltipDirection.Up: min.y = target.yMax + gap; break;
                case TooltipDirection.Down: min.y = target.yMin - gap - size.y; break;
                case TooltipDirection.Left: min.x = target.xMin - gap - size.x; break;
                case TooltipDirection.Right: min.x = target.xMax + gap; break;
            }
            return new Rect(min, size);
        }
        private static float Overflow(Rect body, Rect boundary)
            => Mathf.Max(0, boundary.xMin - body.xMin) + Mathf.Max(0, body.xMax - boundary.xMax)
               + Mathf.Max(0, boundary.yMin - body.yMin) + Mathf.Max(0, body.yMax - boundary.yMax);
        private static TooltipDirection Opposite(TooltipDirection direction) => direction switch
        {
            TooltipDirection.Up => TooltipDirection.Down,
            TooltipDirection.Down => TooltipDirection.Up,
            TooltipDirection.Left => TooltipDirection.Right,
            _ => TooltipDirection.Left
        };
    }
}
