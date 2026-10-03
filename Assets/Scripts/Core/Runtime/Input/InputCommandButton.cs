using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Core.Runtime.Inputs
{
    /// 保存动作名称的 UI 命令按钮；指针保持与普通点击分别交付，不读取硬件或提交第二次 UI。
    [RequireComponent(typeof(Button))]
    public sealed class InputCommandButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private string command;
        [SerializeField] private bool hold;
        private int pointerId = int.MinValue;
        private int releaseFrame = -1;
        /// 保存于资源中的业务动作名称。
        public string Command => command;
        /// 原生 Button 点击或确认，保持操作的指针 Click 不再重复触发。
        public event Action<string> Clicked;
        /// 拥有指针的保持状态，禁用时配对释放。
        public event Action<string, bool> HoldChanged;
        private void OnEnable() => GetComponent<Button>().onClick.AddListener(OnClick);
        private void OnDisable()
        {
            GetComponent<Button>().onClick.RemoveListener(OnClick);
            if (pointerId != int.MinValue) HoldChanged?.Invoke(command, false);
            pointerId = int.MinValue; releaseFrame = -1;
        }
        private void OnClick()
        {
            // 指针释放后的同帧 Click 属于保持操作；拖出取消不能吞掉之后的键盘确认。
            if (pointerId != int.MinValue || releaseFrame == Time.frameCount) return;
            Clicked?.Invoke(command);
        }
        public void OnPointerDown(PointerEventData value)
        {
            if (!hold || value.button != PointerEventData.InputButton.Left || pointerId != int.MinValue || !GetComponent<Button>().IsInteractable()) return;
            pointerId = value.pointerId;
            HoldChanged?.Invoke(command, true);
        }
        public void OnPointerUp(PointerEventData value)
        {
            if (pointerId != value.pointerId) return;
            pointerId = int.MinValue; releaseFrame = Time.frameCount;
            HoldChanged?.Invoke(command, false);
        }
    }
}
