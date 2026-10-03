using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix.BlockPorters.Adapters
{
    /// 按钮只记录指针状态，缩放反馈由 HUD 统一推进，不影响业务点击和调度。
    public sealed class BlockPortersButtonFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private Button button;
        private bool pressed;
        private float pulse;
        private void Awake() => button = GetComponent<Button>();
        public void OnPointerDown(PointerEventData eventData) => pressed = button != null && button.interactable;
        public void OnPointerUp(PointerEventData eventData) => pressed = false;
        public void OnPointerExit(PointerEventData eventData) => pressed = false;
        private void OnDisable() { pressed = false; pulse = 0; transform.localScale = Vector3.one; }
        /// 队列递补时触发一次短促回弹。
        public void Pulse() => pulse = .18f;
        /// <summary>由 HUD 每帧调用，避免为各按钮维护独立 Update。</summary>
        /// <param name="delta">不受玩法暂停影响的界面时间。</param>
        public void Tick(float delta)
        {
            if (!gameObject.activeInHierarchy) return;
            pulse = Mathf.Max(0, pulse - delta);
            float target = pressed && Core.Runtime.Inputs.InputDeviceState.ActiveKind != Core.Runtime.Inputs.InputDeviceKind.Touch ? .96f : 1 + Mathf.Sin(pulse / .18f * Mathf.PI) * .035f;
            transform.localScale = Vector3.one * Mathf.MoveTowards(transform.localScale.x, target, delta * 2);
        }
    }
}
