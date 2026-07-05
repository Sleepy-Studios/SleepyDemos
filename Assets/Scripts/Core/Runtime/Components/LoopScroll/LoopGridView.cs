using UnityEngine;

namespace Core.Runtime
{
    [AddComponentMenu("UI/SleepyDemos/Loop Grid View")]
    public sealed class LoopGridView : LoopScrollRect
    {
        [SerializeField] private LoopGridArrangeType arrangeType = LoopGridArrangeType.TopLeftToBottomRight;
        [SerializeField] private GridFixedType fixedType = GridFixedType.ColumnCountFixed;
        [SerializeField] private int fixedRowOrColumnCount = 1;

        public LoopGridArrangeType ArrangeType { get => arrangeType; set => arrangeType = value; }
        public GridFixedType FixedType => fixedType;
        public int FixedRowOrColumnCount => fixedRowOrColumnCount;

        protected override bool IsVertical => fixedType == GridFixedType.ColumnCountFixed;
        protected override int ContentConstraintCount => Mathf.Max(1, fixedRowOrColumnCount);

        /// <summary>
        /// 设置 Grid 固定行或固定列数量。
        /// </summary>
        public void SetGridFixedGroupCount(GridFixedType nextFixedType, int count)
        {
            fixedType = nextFixedType;
            fixedRowOrColumnCount = Mathf.Max(1, count);
        }

        /// <summary>
        /// 根据 Grid 设置参数更新布局。
        /// </summary>
        public void UpdateGridSetting(LoopGridSettingParam settingParam)
        {
            if (settingParam == null)
            {
                return;
            }

            SetGridFixedGroupCount(settingParam.FixedType, settingParam.FixedRowOrColumnCount);
            DefaultItemSize = settingParam.ItemSize;
        }

        /// <summary>
        /// 将 itemIndex 转换为行列坐标。
        /// </summary>
        public RowColumnPair GetRowColumnByItemIndex(int itemIndex)
        {
            var count = Mathf.Max(1, fixedRowOrColumnCount);
            if (fixedType == GridFixedType.ColumnCountFixed)
            {
                return new RowColumnPair(itemIndex / count, itemIndex % count);
            }

            return new RowColumnPair(itemIndex % count, itemIndex / count);
        }

        /// <summary>
        /// 将行列坐标转换为 itemIndex。
        /// </summary>
        public int GetItemIndexByRowColumn(int row, int column)
        {
            var count = Mathf.Max(1, fixedRowOrColumnCount);
            return fixedType == GridFixedType.ColumnCountFixed ? row * count + column : column * count + row;
        }

        public void MovePanelToItemByIndex(int itemIndex, float offsetX = 0f, float offsetY = 0f)
        {
            ScrollToCell(itemIndex, -1f, xOffsetGap: offsetX, yOffsetGap: offsetY);
        }

        public void MovePanelToItemByRowColumn(int row, int column, float offsetX = 0f, float offsetY = 0f)
        {
            MovePanelToItemByIndex(GetItemIndexByRowColumn(row, column), offsetX, offsetY);
        }

        protected override void PositionRecord(LoopScrollItemRecord record, int index, int visibleSlot)
        {
            var pair = GetRowColumnByItemIndex(index);
            var rect = record.RectTransform;
            rect.sizeDelta = DefaultItemSize;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);

            var x = pair.Column * DefaultItemSize.x;
            var y = -pair.Row * DefaultItemSize.y;
            if (arrangeType == LoopGridArrangeType.TopRightToBottomLeft ||
                arrangeType == LoopGridArrangeType.BottomRightToTopLeft)
            {
                x = -x;
            }

            if (arrangeType == LoopGridArrangeType.BottomLeftToTopRight ||
                arrangeType == LoopGridArrangeType.BottomRightToTopLeft)
            {
                y = -y;
            }

            rect.anchoredPosition = new Vector2(x, y);
        }
    }
}
