using UnityEngine;

namespace Core.Runtime
{
    [AddComponentMenu("UI/SleepyDemos/Loop Horizontal Scroll Rect")]
    public sealed class LoopHorizontalScrollRect : LoopScrollRect
    {
        [SerializeField] private LoopListArrangeType arrangeType = LoopListArrangeType.LeftToRight;

        public LoopListArrangeType ArrangeType { get => arrangeType; set => arrangeType = value; }

        protected override bool IsVertical => false;

        protected override void PositionRecord(LoopScrollItemRecord record, int index, int visibleSlot)
        {
            base.PositionRecord(record, index, visibleSlot);
            if (arrangeType != LoopListArrangeType.RightToLeft)
            {
                return;
            }

            var pos = record.RectTransform.anchoredPosition;
            record.RectTransform.anchoredPosition = new Vector2(-pos.x, pos.y);
        }
    }
}
