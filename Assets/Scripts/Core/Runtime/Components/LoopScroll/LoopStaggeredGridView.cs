using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core.Runtime
{
    [AddComponentMenu("UI/SleepyDemos/Loop Staggered Grid View")]
    public sealed class LoopStaggeredGridView : LoopScrollRect
    {
        private readonly List<LoopStaggeredItemIndexData> itemIndexDataList = new List<LoopStaggeredItemIndexData>();
        private LoopStaggeredLayoutParam layoutParam;
        private Func<int, (float size, float padding)> itemSizeProvider;
        private int groupCount = 1;

        public LoopStaggeredLayoutParam LayoutParam => layoutParam;
        public int CurMaxCreatedItemIndexCount => itemIndexDataList.Count;

        protected override bool IsVertical => true;
        protected override int ContentConstraintCount => groupCount;

        /// <summary>
        /// 初始化瀑布流列表。
        /// </summary>
        public void InitListView(
            int itemTotalCount,
            LoopStaggeredLayoutParam nextLayoutParam,
            Action<LoopStaggeredGridView, int, ItemView> onGetItemByItemIndex = null,
            LoopScrollInitParam initParam = null,
            Func<int, (float size, float padding)> onGetItemSizeByItemIndex = null)
        {
            ResetGridViewLayoutParam(itemTotalCount, nextLayoutParam, onGetItemSizeByItemIndex);
        }

        /// <summary>
        /// 重置瀑布流布局参数并重建索引分组。
        /// </summary>
        public void ResetGridViewLayoutParam(
            int itemTotalCount,
            LoopStaggeredLayoutParam nextLayoutParam,
            Func<int, (float size, float padding)> onGetItemSizeByItemIndex = null)
        {
            layoutParam = nextLayoutParam ?? throw new ArgumentNullException(nameof(nextLayoutParam));
            groupCount = Mathf.Max(1, layoutParam.ColumnOrRowCount);
            itemSizeProvider = onGetItemSizeByItemIndex;
            RebuildItemIndexData(itemTotalCount);
            SetListItemCount(itemTotalCount);
        }

        /// <summary>
        /// 获取指定 item 的瀑布流分组数据。
        /// </summary>
        public LoopStaggeredItemIndexData GetItemIndexData(int itemIndex)
        {
            return itemIndex >= 0 && itemIndex < itemIndexDataList.Count ? itemIndexDataList[itemIndex] : null;
        }

        /// <summary>
        /// 确保瀑布流布局数据至少计算到指定 item。
        /// </summary>
        public void UpdateContentSizeUpToItemIndex(int itemIndex)
        {
            if (itemIndex < itemIndexDataList.Count)
            {
                return;
            }

            RebuildItemIndexData(itemIndex + 1);
        }

        public void MovePanelToItemIndex(int itemIndex, float offset)
        {
            ScrollToCell(itemIndex, -1f, yOffsetGap: offset);
        }

        protected override void PositionRecord(LoopScrollItemRecord record, int index, int visibleSlot)
        {
            UpdateContentSizeUpToItemIndex(index);
            var data = GetItemIndexData(index);
            var rect = record.RectTransform;
            var size = GetItemSize(index);
            rect.sizeDelta = new Vector2(layoutParam?.ItemWidthOrHeight > 0f ? layoutParam.ItemWidthOrHeight : DefaultItemSize.x, size.size);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(data.GroupIndex * rect.sizeDelta.x, -GetGroupOffsetBefore(index, data.GroupIndex));
        }

        private void RebuildItemIndexData(int itemTotalCount)
        {
            itemIndexDataList.Clear();
            var heights = new float[Mathf.Max(1, groupCount)];
            var counts = new int[heights.Length];
            for (int i = 0; i < itemTotalCount; i++)
            {
                var group = GetShortestGroup(heights);
                itemIndexDataList.Add(new LoopStaggeredItemIndexData
                {
                    GroupIndex = group,
                    IndexInGroup = counts[group]
                });

                var size = GetItemSize(i);
                heights[group] += size.size + size.padding;
                counts[group]++;
            }
        }

        private (float size, float padding) GetItemSize(int index)
        {
            if (itemSizeProvider != null)
            {
                return itemSizeProvider(index);
            }

            return (DefaultItemSize.y, 0f);
        }

        private static int GetShortestGroup(IReadOnlyList<float> heights)
        {
            var group = 0;
            var min = heights[0];
            for (int i = 1; i < heights.Count; i++)
            {
                if (heights[i] < min)
                {
                    min = heights[i];
                    group = i;
                }
            }

            return group;
        }

        private float GetGroupOffsetBefore(int itemIndex, int groupIndex)
        {
            var offset = 0f;
            for (int i = 0; i < itemIndex; i++)
            {
                var data = itemIndexDataList[i];
                if (data.GroupIndex != groupIndex)
                {
                    continue;
                }

                var size = GetItemSize(i);
                offset += size.size + size.padding;
            }

            return offset;
        }
    }
}
