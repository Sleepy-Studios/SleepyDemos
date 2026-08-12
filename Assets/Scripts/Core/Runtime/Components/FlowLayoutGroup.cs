using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public enum FlowLayoutAxis
    {
        Horizontal,
        Vertical
    }

    /// 按子节点首选尺寸排列，并在主轴空间不足时自动换行或换列。
    [AddComponentMenu("Layout/Flow Layout Group")]
    public sealed class FlowLayoutGroup : LayoutGroup
    {
        [SerializeField, Tooltip("Horizontal 按行换行，Vertical 按列换列。")]
        private FlowLayoutAxis startAxis = FlowLayoutAxis.Horizontal;
        [SerializeField, Tooltip("水平和垂直方向的元素间距。")]
        private Vector2 spacing;
        [SerializeField, Tooltip("反转参与布局的子节点顺序。")]
        private bool reverseOrder;
        [SerializeField, Tooltip("把每行剩余宽度平均分配给该行子节点。")]
        private bool childForceExpandWidth;
        [SerializeField, Tooltip("把每列或每行剩余高度分配给子节点。")]
        private bool childForceExpandHeight;

        private readonly List<FlowItem> items = new List<FlowItem>();
        private readonly List<FlowBar> bars = new List<FlowBar>();

        /// 主排列轴。
        public FlowLayoutAxis StartAxis
        {
            get => startAxis;
            set => SetProperty(ref startAxis, value);
        }

        /// 水平和垂直间距。
        public Vector2 Spacing
        {
            get => spacing;
            set => SetProperty(ref spacing, new Vector2(Mathf.Max(0f, value.x), Mathf.Max(0f, value.y)));
        }

        /// 是否反转子节点顺序。
        public bool ReverseOrder
        {
            get => reverseOrder;
            set => SetProperty(ref reverseOrder, value);
        }

        /// 是否让子节点填满所在行的剩余宽度。
        public bool ChildForceExpandWidth
        {
            get => childForceExpandWidth;
            set => SetProperty(ref childForceExpandWidth, value);
        }

        /// 是否让子节点填满所在列或行的剩余高度。
        public bool ChildForceExpandHeight
        {
            get => childForceExpandHeight;
            set => SetProperty(ref childForceExpandHeight, value);
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            if (startAxis == FlowLayoutAxis.Horizontal)
            {
                float width = GetGreatestMinimumChildSize(0) + padding.horizontal;
                SetLayoutInputForAxis(width, width, -1f, 0);
                return;
            }

            BuildLayout(GetAvailableMainSize());
            float preferredWidth = GetTotalCrossSize() + padding.horizontal;
            SetLayoutInputForAxis(preferredWidth, preferredWidth, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            if (startAxis == FlowLayoutAxis.Vertical)
            {
                float height = GetGreatestMinimumChildSize(1) + padding.vertical;
                SetLayoutInputForAxis(height, height, -1f, 1);
                return;
            }

            BuildLayout(GetAvailableMainSize());
            float preferredHeight = GetTotalCrossSize() + padding.vertical;
            SetLayoutInputForAxis(preferredHeight, preferredHeight, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
            ApplyLayout(0);
        }

        public override void SetLayoutVertical()
        {
            ApplyLayout(1);
        }

        protected override void OnValidate()
        {
            spacing.x = Mathf.Max(0f, spacing.x);
            spacing.y = Mathf.Max(0f, spacing.y);
            base.OnValidate();
        }

        private void ApplyLayout(int axis)
        {
            float availableMainSize = GetAvailableMainSize();
            BuildLayout(availableMainSize);
            if (bars.Count == 0) return;

            float totalCrossSize = GetTotalCrossSize();
            float crossPosition = GetCrossStart(totalCrossSize);
            float crossSpacing = GetCrossSpacing();

            for (int barIndex = 0; barIndex < bars.Count; barIndex++)
            {
                FlowBar bar = bars[barIndex];
                float distributable = Mathf.Max(0f, availableMainSize - bar.MainSize);
                bool forceExpandMain = startAxis == FlowLayoutAxis.Horizontal
                    ? childForceExpandWidth
                    : childForceExpandHeight;
                float extraMainSize = forceExpandMain && bar.Count > 0 ? distributable / bar.Count : 0f;
                float laidOutMainSize = forceExpandMain ? availableMainSize : bar.MainSize;
                float mainPosition = GetMainStart(laidOutMainSize, availableMainSize);

                for (int itemIndex = bar.StartIndex; itemIndex < bar.StartIndex + bar.Count; itemIndex++)
                {
                    FlowItem item = items[itemIndex];
                    float mainSize = item.MainSize + extraMainSize;
                    float crossSize = GetChildCrossSize(item.CrossSize, bar.CrossSize);
                    float childCrossPosition = crossPosition + GetChildCrossOffset(bar.CrossSize, crossSize);
                    SetItemAlongAxis(item.Rect, axis, mainPosition, childCrossPosition, mainSize, crossSize);
                    mainPosition += mainSize + GetMainSpacing();
                }

                crossPosition += bar.CrossSize + crossSpacing;
            }
        }

        private void BuildLayout(float availableMainSize)
        {
            items.Clear();
            bars.Clear();
            if (rectChildren.Count == 0) return;

            availableMainSize = Mathf.Max(0f, availableMainSize);
            float mainSpacing = GetMainSpacing();
            int barStart = 0;
            int barCount = 0;
            float barMainSize = 0f;
            float barCrossSize = 0f;

            for (int i = 0; i < rectChildren.Count; i++)
            {
                int sourceIndex = reverseOrder ? rectChildren.Count - 1 - i : i;
                RectTransform child = rectChildren[sourceIndex];
                float mainSize = Mathf.Min(GetPreferredSize(child, GetMainAxis()), availableMainSize);
                float crossSize = GetPreferredSize(child, GetCrossAxis());
                float requiredSize = barCount == 0 ? mainSize : barMainSize + mainSpacing + mainSize;

                // 当前行已有内容时才允许换行，保证超窄容器不会生成空行。
                if (barCount > 0 && requiredSize > availableMainSize)
                {
                    bars.Add(new FlowBar(barStart, barCount, barMainSize, barCrossSize));
                    barStart = items.Count;
                    barCount = 0;
                    barMainSize = 0f;
                    barCrossSize = 0f;
                }

                if (barCount > 0) barMainSize += mainSpacing;
                items.Add(new FlowItem(child, mainSize, crossSize));
                barCount++;
                barMainSize += mainSize;
                barCrossSize = Mathf.Max(barCrossSize, crossSize);
            }

            if (barCount > 0) bars.Add(new FlowBar(barStart, barCount, barMainSize, barCrossSize));
        }

        private void SetItemAlongAxis(RectTransform child, int layoutAxis, float mainPosition,
            float crossPosition, float mainSize, float crossSize)
        {
            float horizontalPosition = startAxis == FlowLayoutAxis.Horizontal ? mainPosition : crossPosition;
            float verticalPosition = startAxis == FlowLayoutAxis.Horizontal ? crossPosition : mainPosition;
            float width = startAxis == FlowLayoutAxis.Horizontal ? mainSize : crossSize;
            float height = startAxis == FlowLayoutAxis.Horizontal ? crossSize : mainSize;

            if (layoutAxis == 0) SetChildAlongAxis(child, 0, horizontalPosition, width);
            else SetChildAlongAxis(child, 1, verticalPosition, height);
        }

        private float GetAvailableMainSize()
        {
            return startAxis == FlowLayoutAxis.Horizontal
                ? Mathf.Max(0f, rectTransform.rect.width - padding.horizontal)
                : Mathf.Max(0f, rectTransform.rect.height - padding.vertical);
        }

        private float GetTotalCrossSize()
        {
            float size = 0f;
            for (int i = 0; i < bars.Count; i++) size += bars[i].CrossSize;
            return size + Mathf.Max(0, bars.Count - 1) * GetCrossSpacing();
        }

        private float GetMainStart(float barSize, float availableSize)
        {
            float offset = Mathf.Max(0f, availableSize - barSize);
            if (startAxis == FlowLayoutAxis.Horizontal)
            {
                if (IsHorizontalCenter()) offset *= 0.5f;
                else if (!IsHorizontalRight()) offset = 0f;
                return padding.left + offset;
            }

            if (IsVerticalMiddle()) offset *= 0.5f;
            else if (!IsVerticalBottom()) offset = 0f;
            return padding.top + offset;
        }

        private float GetCrossStart(float totalCrossSize)
        {
            if (startAxis == FlowLayoutAxis.Horizontal)
            {
                float available = Mathf.Max(0f, rectTransform.rect.height - padding.vertical);
                float offset = Mathf.Max(0f, available - totalCrossSize);
                if (IsVerticalMiddle()) offset *= 0.5f;
                else if (!IsVerticalBottom()) offset = 0f;
                return padding.top + offset;
            }

            float crossAvailable = Mathf.Max(0f, rectTransform.rect.width - padding.horizontal);
            float crossOffset = Mathf.Max(0f, crossAvailable - totalCrossSize);
            if (IsHorizontalCenter()) crossOffset *= 0.5f;
            else if (!IsHorizontalRight()) crossOffset = 0f;
            return padding.left + crossOffset;
        }

        private float GetChildCrossSize(float preferredSize, float barSize)
        {
            bool forceExpandCross = startAxis == FlowLayoutAxis.Horizontal
                ? childForceExpandHeight
                : childForceExpandWidth;
            return forceExpandCross ? barSize : preferredSize;
        }

        private float GetChildCrossOffset(float barSize, float childSize)
        {
            float remaining = Mathf.Max(0f, barSize - childSize);
            if (startAxis == FlowLayoutAxis.Horizontal)
            {
                if (IsVerticalMiddle()) return remaining * 0.5f;
                return IsVerticalBottom() ? remaining : 0f;
            }

            if (IsHorizontalCenter()) return remaining * 0.5f;
            return IsHorizontalRight() ? remaining : 0f;
        }

        private float GetMainSpacing() => startAxis == FlowLayoutAxis.Horizontal ? spacing.x : spacing.y;
        private float GetCrossSpacing() => startAxis == FlowLayoutAxis.Horizontal ? spacing.y : spacing.x;
        private int GetMainAxis() => startAxis == FlowLayoutAxis.Horizontal ? 0 : 1;
        private int GetCrossAxis() => startAxis == FlowLayoutAxis.Horizontal ? 1 : 0;

        private float GetGreatestMinimumChildSize(int axis)
        {
            float result = 0f;
            for (int i = 0; i < rectChildren.Count; i++)
                result = Mathf.Max(result, LayoutUtility.GetMinSize(rectChildren[i], axis));
            return result;
        }

        private static float GetPreferredSize(RectTransform child, int axis)
        {
            return Mathf.Max(0f, LayoutUtility.GetPreferredSize(child, axis));
        }

        private bool IsHorizontalCenter() => childAlignment == TextAnchor.UpperCenter ||
                                             childAlignment == TextAnchor.MiddleCenter ||
                                             childAlignment == TextAnchor.LowerCenter;
        private bool IsHorizontalRight() => childAlignment == TextAnchor.UpperRight ||
                                            childAlignment == TextAnchor.MiddleRight ||
                                            childAlignment == TextAnchor.LowerRight;
        private bool IsVerticalMiddle() => childAlignment == TextAnchor.MiddleLeft ||
                                           childAlignment == TextAnchor.MiddleCenter ||
                                           childAlignment == TextAnchor.MiddleRight;
        private bool IsVerticalBottom() => childAlignment == TextAnchor.LowerLeft ||
                                           childAlignment == TextAnchor.LowerCenter ||
                                           childAlignment == TextAnchor.LowerRight;

        private readonly struct FlowItem
        {
            public readonly RectTransform Rect;
            public readonly float MainSize;
            public readonly float CrossSize;

            public FlowItem(RectTransform rect, float mainSize, float crossSize)
            {
                Rect = rect;
                MainSize = mainSize;
                CrossSize = crossSize;
            }
        }

        private readonly struct FlowBar
        {
            public readonly int StartIndex;
            public readonly int Count;
            public readonly float MainSize;
            public readonly float CrossSize;

            public FlowBar(int startIndex, int count, float mainSize, float crossSize)
            {
                StartIndex = startIndex;
                Count = count;
                MainSize = mainSize;
                CrossSize = crossSize;
            }
        }
    }
}
