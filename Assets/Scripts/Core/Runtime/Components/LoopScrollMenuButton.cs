using SleepyStudios.LoopScroll;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 虚拟列表中的普通菜单按钮；绑定身份和导航由 Core 管理，业务仍使用 Button.onClick。
    public sealed class LoopScrollMenuButton : Button
    {
        internal LoopScrollMenuNavigation Owner;
        internal CellBindContext Context;
        private CellBindContext pressedContext;
        private int pressedPointerId;
        private bool pointerPressed;

        /// <summary>保存按下时的数据身份，防止抬手前回收换绑后误点另一项。</summary>
        /// <param name="eventData">真实鼠标或触摸按下事件。</param>
        public override void OnPointerDown(PointerEventData eventData)
        {
            pointerPressed = eventData.button == PointerEventData.InputButton.Left && Context.IsCurrent && IsActive() && IsInteractable();
            if (!pointerPressed) return;
            pressedContext = Context;
            pressedPointerId = eventData.pointerId;
            base.OnPointerDown(eventData);
        }

        /// <summary>在当前数据身份有效时保持普通指针点击语义。</summary>
        /// <param name="eventData">真实指针事件。</param>
        public override void OnPointerClick(PointerEventData eventData)
        {
            var valid = pointerPressed && pressedPointerId == eventData.pointerId && pressedContext.IsCurrent && Context.IsCurrent;
            pointerPressed = false;
            if (valid) base.OnPointerClick(eventData);
        }

        protected override void OnDisable()
        {
            pointerPressed = false;
            base.OnDisable();
        }

        /// <summary>提交仍由现有 UI Module 派发，不向已回收或变更身份的按钮提交。</summary>
        /// <param name="eventData">现有 EventSystem 的提交事件。</param>
        public override void OnSubmit(BaseEventData eventData)
        {
            if (Context.IsCurrent && Owner != null && Owner.IsCurrentSelection(this)) base.OnSubmit(eventData);
        }

        /// <summary>记录当前选中数据身份；不触发点击。</summary>
        /// <param name="eventData">现有 EventSystem 的选中事件。</param>
        public override void OnSelect(BaseEventData eventData)
        {
            if (!Context.IsCurrent) return;
            Owner?.RememberSelection(this);
            base.OnSelect(eventData);
        }

        /// <summary>优先使用列表几何布局移动焦点，列表边缘保留普通控件导航。</summary>
        /// <param name="eventData">现有 EventSystem 的方向事件。</param>
        public override void OnMove(AxisEventData eventData)
        {
            if (Owner != null && Owner.MoveSelection(this, eventData.moveDir)) eventData.Use();
            else base.OnMove(eventData);
        }
    }
}
