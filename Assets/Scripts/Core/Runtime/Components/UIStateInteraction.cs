using Core.Runtime.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Core.Runtime
{
    /// 交互表现只消费 EventSystem 事件；业务 Selected 由 UITab/Toggle 等独立维护。
    [DisallowMultipleComponent, RequireComponent(typeof(Selectable))]
    public sealed class UIStateInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        [SerializeField] private UIState interactionState;
        private Selectable selectable;
        private bool hovered;
        private bool pressed;
        private float submitUntil;
        private string applied;
        /// 当前交互表现；业务选中态保持独立。
        public string InteractionState => applied;

        /// <summary>动态示例接入已有状态定义，不创建模板兜底。</summary>
        /// <param name="value">明确创建的交互状态。</param>
        public void Bind(UIState value) { interactionState = value; applied = null; Refresh(); }

        private void Awake() => selectable = GetComponent<Selectable>();
        private void OnEnable()
        {
            InputDeviceState.Initialize();
            InputDeviceState.Changed += Refresh;
            Refresh();
        }
        private void OnDisable()
        {
            InputDeviceState.Changed -= Refresh;
            hovered = pressed = false; submitUntil = 0; applied = null;
            interactionState?.SetState("Normal");
        }
        private void LateUpdate() => Refresh(); // interactable/CanvasGroup 没有统一变更事件，仅状态变化时写属性。

        private void Refresh()
        {
            if (selectable == null) selectable = GetComponent<Selectable>();
            bool focus = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            bool touch = InputDeviceState.ActiveKind == InputDeviceKind.Touch;
            bool keyboardFocus = InputDeviceState.ActiveKind == InputDeviceKind.Gamepad || InputDeviceState.ActiveDevice is Keyboard;
            string next = touch ? "Normal" : !selectable.IsInteractable() ? "Disabled"
                : pressed || Time.unscaledTime < submitUntil ? "Pressed"
                : focus && keyboardFocus ? "Focused" : hovered && InputDeviceState.ActiveKind == InputDeviceKind.KeyboardMouse ? "Hover" : "Normal";
            if (next == applied) return;
            applied = next;
            interactionState?.SetState(next);
        }

        private static void RecordPointer(PointerEventData value)
        {
            if (value is ExtendedPointerEventData extended) InputDeviceState.Notify(extended.device);
            else if (value.pointerId >= 0) InputDeviceState.NotifyTouch();
            else InputDeviceState.Notify(Mouse.current);
        }

        // 界面出现时也会产生 Enter；实际设备由硬件活动或真实按下识别，避免静止鼠标抢走触控。
        public void OnPointerEnter(PointerEventData value) { hovered = true; Refresh(); }
        public void OnPointerExit(PointerEventData value) { hovered = false; Refresh(); }
        public void OnPointerDown(PointerEventData value)
        {
            if (value.button != PointerEventData.InputButton.Left) return;
            RecordPointer(value); pressed = selectable.IsInteractable(); Refresh();
        }
        public void OnPointerUp(PointerEventData value) { pressed = false; Refresh(); }
        public void OnSelect(BaseEventData value) => Refresh();
        public void OnDeselect(BaseEventData value) => Refresh();
        public void OnSubmit(BaseEventData value)
        {
            if (selectable.IsInteractable()) submitUntil = Time.unscaledTime + Mathf.Max(0.08f, selectable.colors.fadeDuration);
            Refresh();
        }
    }
}
