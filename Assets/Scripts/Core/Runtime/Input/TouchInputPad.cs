using UnityEngine;
using UnityEngine.EventSystems;

namespace Core.Runtime.Inputs
{
    /// 单指触控区域，移动与视角使用独立指针，避免两根手指互相抢输入。
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, "Hotfix.JinxCasino.Adapters.UI", "Hotfix", "JinxCasinoTouchPad")]
    public sealed class TouchInputPad : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        private const float ReferenceHeight = 720f;
        private const float MoveRadius = 80f;
        [SerializeField] private bool isLookPad;
        private int pointerId = int.MinValue;
        private Vector2 origin;
        private Vector2 move;
        private Vector2 look;
        /// 当前摇杆输入。
        public Vector2 Move => move;

        /// 读取并清空本帧视角增量，使用720p参考像素；未发生拖动时为零。
        public Vector2 ConsumeLook()
        {
            Vector2 value = look;
            look = Vector2.zero;
            return value;
        }

        /// <summary>装配移动或视角区域。</summary>
        /// <param name="lookPad">true 为视角增量；false 为移动摇杆。</param>
        public void Configure(bool lookPad) => isLookPad = lookPad;

        /// <summary>占用当前区域；已有手指操作时不允许额外指针接管。</summary>
        /// <param name="eventData">由EventSystem提供的独立指针编号和起始屏幕位置。</param>
        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointerId != int.MinValue) return;
            pointerId = eventData.pointerId;
            origin = eventData.position;
        }

        /// <summary>只更新占用区域的指针，按屏幕高度换算统一的触控响应。</summary>
        /// <param name="eventData">移动使用相对起点的位置；视角使用本次拖动增量。</param>
        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != pointerId) return;
            // 保持相同屏幕比例的滑动产生相同输入；低分辨率也必须参与缩放，键鼠仍使用原始增量。
            float referenceScale = ReferenceHeight / Mathf.Max(1, Screen.height);
            if (isLookPad) look += eventData.delta * referenceScale;
            else move = Vector2.ClampMagnitude((eventData.position - origin) * (referenceScale / MoveRadius), 1);
        }

        /// <summary>仅释放拥有当前区域的指针，并丢弃未消费输入。</summary>
        /// <param name="eventData">松开的指针；其它指针的松开事件不影响当前操作。</param>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == pointerId) ResetInput();
        }

        /// 清空触控输入，失焦、离开界面时调用。
        public void ResetInput()
        {
            pointerId = int.MinValue;
            move = look = Vector2.zero;
        }

        private void OnDisable() => ResetInput();
    }
}
