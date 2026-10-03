using Core.Runtime.Inputs;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// HUD 保存的触控与操作面板，绑定当前无人机，不拥有飞控或设备状态。
    public sealed class DroneControlsPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject touchControls;
        [SerializeField] private GameObject operationPanel;
        [SerializeField] private TouchInputPad leftPad;
        [SerializeField] private TouchInputPad rightPad;
        [SerializeField] private InputCommandButton[] buttons;
        [SerializeField] private InputBindingPrompt[] prompts;
        private DronePlayerInput input;
        private bool touchLook;

        /// <summary>绑定本次实例；解绑时释放旧按钮订阅和摇杆。</summary>
        /// <param name="value">当前无人机输入。</param>
        public void Bind(DronePlayerInput value)
        {
            Unbind(); input = value;
            if (input == null) return;
            touchLook = false; input.SetTouchLookMode(false);
            foreach (var button in buttons) { button.Clicked += OnCommand; button.HoldChanged += OnHold; }
            foreach (var prompt in prompts)
                if (prompt.Action != null) prompt.Bind(input.Session?.Asset.FindAction(prompt.Action.id));
            input.PanelChanged += OnPanelChanged;
            InputDeviceState.Changed += RefreshDevice;
            operationPanel.GetComponent<UIMenuScope>().Canceled += ClosePanel;
            OnPanelChanged(input.IsPanelOpen); RefreshDevice();
        }
        /// 释放本次绑定，不清空按钮的其它调用方。
        public void Unbind()
        {
            foreach (var button in buttons) { button.Clicked -= OnCommand; button.HoldChanged -= OnHold; }
            if (input != null)
            {
                input.PanelChanged -= OnPanelChanged;
                // 先取消保持跟踪，再解除按钮订阅；HUD 隐藏不能变成短按或继续长按复位。
                input.ResetBufferedInput(); input.SetTouchLookMode(false);
            }
            InputDeviceState.Changed -= RefreshDevice;
            if (operationPanel != null) operationPanel.GetComponent<UIMenuScope>().Canceled -= ClosePanel;
            leftPad?.ResetInput(); rightPad?.ResetInput(); input = null;
        }
        private void OnDisable() => Unbind();
        private void Update()
        {
            if (input != null && input.AcceptsFlightInput)
                input.SetTouchFrame(leftPad.Move, rightPad.Move);
        }
        private void RefreshDevice()
        {
            bool show = InputDeviceState.ActiveKind == InputDeviceKind.Touch;
            touchControls.SetActive(show);
            if (!show) { leftPad.ResetInput(); rightPad.ResetInput(); input?.ResetBufferedInput(); }
        }
        private void OnPanelChanged(bool open)
        {
            // 面板切换释放双指所有权，旧手指保持不能在关闭后继续驱动飞行。
            leftPad.ResetInput(); rightPad.ResetInput();
            operationPanel.SetActive(open);
        }
        private void ClosePanel() => input?.SetPanelOpen(false);
        private void OnCommand(string name)
        {
            if (name == "ViewModifier") { touchLook = !touchLook; input.SetTouchLookMode(touchLook); return; }
            if (name != "ReelIn" && name != "ReelOut") input?.Execute(name);
        }
        private void OnHold(string name, bool held)
        {
            if (name == "ArmOrReset") input?.SetTouchArmHeld(held);
            if (name == "ReelIn") input?.SetTouchLine(held ? -1 : 0);
            if (name == "ReelOut") input?.SetTouchLine(held ? 1 : 0);
        }
    }
}
