using UnityEngine;

namespace Core.Runtime
{
    [AddComponentMenu("UI/SleepyDemos/Loop Vertical Scroll Rect")]
    public sealed class LoopVerticalScrollRect : LoopScrollRect
    {
        [SerializeField] private LoopListArrangeType arrangeType = LoopListArrangeType.TopToBottom;

        public LoopListArrangeType ArrangeType { get => arrangeType; set => arrangeType = value; }

        protected override bool IsVertical => true;

        protected override void PositionRecord(LoopScrollItemRecord record, int index, int visibleSlot)
        {
            base.PositionRecord(record, index, visibleSlot);
            if (arrangeType != LoopListArrangeType.BottomToTop)
            {
                return;
            }

            var pos = record.RectTransform.anchoredPosition;
            record.RectTransform.anchoredPosition = new Vector2(pos.x, -pos.y);
        }
    }
}
