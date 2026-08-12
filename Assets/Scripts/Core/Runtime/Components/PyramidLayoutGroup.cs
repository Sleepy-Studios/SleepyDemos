using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Core.Runtime
{
    public enum PyramidRemainderPosition
    {
        Top,
        Bottom
    }

    public enum PyramidLayoutMode
    {
        Stagger,
        Triangle
    }

    public enum PyramidHorizontalAlignment
    {
        Left,
        Center,
        Right
    }

    /// 将固定尺寸子节点排成品字或逐行递增的三角形。
    [AddComponentMenu("Layout/Pyramid Layout Group")]
    public sealed class PyramidLayoutGroup : LayoutGroup
    {
        [SerializeField, Min(1), Tooltip("每行最多容纳的子节点数。")]
        private int columns = 3;
        [SerializeField, Tooltip("每个子节点的固定尺寸。")]
        private Vector2 cellSize = new Vector2(100f, 100f);
        [SerializeField, Tooltip("水平和垂直方向的元素间距。")]
        private Vector2 spacing;
        [SerializeField] private PyramidLayoutMode layoutMode = PyramidLayoutMode.Stagger;
        [SerializeField] private PyramidRemainderPosition remainderPosition = PyramidRemainderPosition.Bottom;
        [SerializeField] private PyramidHorizontalAlignment horizontalAlignment = PyramidHorizontalAlignment.Center;
        [SerializeField] private bool reverseArrangement;

        private readonly List<int> rowCounts = new List<int>();

        /// 每行最大列数。
        public int Columns
        {
            get => columns;
            set { columns = Mathf.Max(1, value); SetDirty(); }
        }

        /// 子节点固定尺寸。
        public Vector2 CellSize
        {
            get => cellSize;
            set { cellSize = value; SetDirty(); }
        }

        /// 子节点间距。
        public Vector2 Spacing
        {
            get => spacing;
            set { spacing = value; SetDirty(); }
        }

        /// 排列模式。
        public PyramidLayoutMode LayoutMode
        {
            get => layoutMode;
            set { layoutMode = value; SetDirty(); }
        }

        /// 大行位于顶部或底部。
        public PyramidRemainderPosition RemainderPosition
        {
            get => remainderPosition;
            set { remainderPosition = value; SetDirty(); }
        }

        /// 小行的水平对齐方式。
        public PyramidHorizontalAlignment HorizontalAlignment
        {
            get => horizontalAlignment;
            set { horizontalAlignment = value; SetDirty(); }
        }

        /// 是否反向分配子节点顺序。
        public bool ReverseArrangement
        {
            get => reverseArrangement;
            set { reverseArrangement = value; SetDirty(); }
        }

        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            int count = Mathf.Min(Mathf.Max(1, columns), rectChildren.Count);
            float preferred = padding.horizontal + count * cellSize.x + Mathf.Max(0, count - 1) * spacing.x;
            SetLayoutInputForAxis(preferred, preferred, -1f, 0);
        }

        public override void CalculateLayoutInputVertical()
        {
            int rows = BuildRowCounts().Count;
            float preferred = padding.vertical + rows * cellSize.y + Mathf.Max(0, rows - 1) * spacing.y;
            SetLayoutInputForAxis(preferred, preferred, -1f, 1);
        }

        public override void SetLayoutHorizontal()
        {
        }

        public override void SetLayoutVertical()
        {
            List<int> rows = BuildRowCounts();
            if (rows.Count == 0) return;

            float containerWidth = rectTransform.rect.width - padding.horizontal;
            int childIndex = 0;
            int startRow = reverseArrangement ? rows.Count - 1 : 0;
            int endRow = reverseArrangement ? -1 : rows.Count;
            int step = reverseArrangement ? -1 : 1;

            for (int row = startRow; row != endRow && childIndex < rectChildren.Count; row += step)
            {
                int rowCount = rows[row];
                float rowWidth = rowCount * cellSize.x + Mathf.Max(0, rowCount - 1) * spacing.x;
                float startX = CalculateRowStartX(containerWidth, rowWidth);
                float startY = padding.top + row * (cellSize.y + spacing.y);
                for (int column = 0; column < rowCount && childIndex < rectChildren.Count; column++, childIndex++)
                {
                    RectTransform child = rectChildren[childIndex];
                    SetChildAlongAxis(child, 0, startX + column * (cellSize.x + spacing.x), cellSize.x);
                    SetChildAlongAxis(child, 1, startY, cellSize.y);
                }
            }
        }

        private List<int> BuildRowCounts()
        {
            rowCounts.Clear();
            int childCount = rectChildren.Count;
            if (childCount == 0) return rowCounts;
            int maxColumns = Mathf.Max(1, columns);

            if (layoutMode == PyramidLayoutMode.Triangle)
            {
                int remaining = childCount;
                int rowSize = 1;
                while (remaining > 0)
                {
                    int count = Mathf.Min(rowSize, remaining);
                    rowCounts.Add(count);
                    remaining -= count;
                    rowSize = Mathf.Min(rowSize + 1, maxColumns);
                }

                if (remainderPosition == PyramidRemainderPosition.Top) rowCounts.Reverse();
                return rowCounts;
            }

            int remainder = childCount % maxColumns;
            int fullRows = childCount / maxColumns;
            if (remainderPosition == PyramidRemainderPosition.Bottom && remainder > 0) rowCounts.Add(remainder);
            for (int i = 0; i < fullRows; i++) rowCounts.Add(maxColumns);
            if (remainderPosition == PyramidRemainderPosition.Top && remainder > 0) rowCounts.Add(remainder);
            return rowCounts;
        }

        private float CalculateRowStartX(float containerWidth, float rowWidth)
        {
            switch (horizontalAlignment)
            {
                case PyramidHorizontalAlignment.Left:
                    return padding.left;
                case PyramidHorizontalAlignment.Right:
                    return padding.left + containerWidth - rowWidth;
                default:
                    return padding.left + (containerWidth - rowWidth) * 0.5f;
            }
        }
    }
}
