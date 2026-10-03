using Core.Runtime.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hotfix.DroneFlight
{
    /// HUD 保存的触控与操作面板；所有动作都交付当前会话的 Core 输入桥。
    public sealed class DroneControlsPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject touchControls;
        [SerializeField] private GameObject operationPanel;
        [SerializeField] private UIMenuScope menuScope;
        [SerializeField] private TouchInputPad leftPad;
        [SerializeField] private TouchInputPad rightPad;
        [SerializeField] private RectTransform leftThumb;
        [SerializeField] private RectTransform rightThumb;
        private bool lastActive;
        [SerializeField] private InputCommandButton[] buttons;
        [SerializeField] private InputBindingPrompt[] prompts;
        private DronePlayerInput input;
        private bool touchLook;
        private DroneEquipmentKind equipmentKind;
        private GameObject menuSelection;

        /// <summary>绑定当前实例并释放旧会话的输入订阅。</summary>
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
            input.HelpChanged += OnOverlayChanged;
            input.DebugChanged += OnOverlayChanged;
            InputDeviceState.Changed += RefreshDevice;
            if (menuScope != null) menuScope.Canceled += ClosePanel;
            RefreshDevice();
        }

        /// 释放本次绑定；不能将被取消的长按当作解锁短按。
        public void Unbind()
        {
            if (buttons != null)
                foreach (var button in buttons) { button.Clicked -= OnCommand; button.HoldChanged -= OnHold; }
            if (input != null)
            {
                input.PanelChanged -= OnPanelChanged;
                input.HelpChanged -= OnOverlayChanged;
                input.DebugChanged -= OnOverlayChanged;
                input.ResetBufferedInput(); input.SetTouchLookMode(false);
            }
            InputDeviceState.Changed -= RefreshDevice;
            if (menuScope != null) menuScope.Canceled -= ClosePanel;
            leftPad?.ResetInput(); rightPad?.ResetInput(); input = null; menuSelection = null;
        }

        /// <summary>只显示已安装装备支持的按钮。</summary>
        /// <param name="kind">当前装备类型。</param>
        public void SetEquipment(DroneEquipmentKind kind)
        {
            equipmentKind = kind;
            if (buttons == null) return;
            foreach (var button in buttons)
            {
                bool show = button.Command switch
                {
                    "Equipment" or "ReelIn" or "ReelOut" => kind != DroneEquipmentKind.None,
                    "Aim" => kind == DroneEquipmentKind.Harpoon,
                    "Activate" => input != null && !input.isActiveAndEnabled,
                    "ArmOrReset" or "ViewModifier" => input != null && input.isActiveAndEnabled,
                    _ => true
                };
                button.gameObject.SetActive(show);
            }
        }

        private void OnDisable() => Unbind();
        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseInput(); }
        private void OnApplicationPause(bool paused) { if (paused) ReleaseInput(); }
        private void Update()
        {
            bool active = input != null && input.isActiveAndEnabled;
            if (active != lastActive) { lastActive = active; ReleaseInput(); SetEquipment(equipmentKind); }
            if (leftThumb != null) leftThumb.anchoredPosition = leftPad.Move * 48f;
            if (rightThumb != null) rightThumb.anchoredPosition = rightPad.Move * 48f;
            if (input != null && input.AcceptsFlightInput)
                input.SetTouchFrame(leftPad.Move, rightPad.Move);
        }
        private void ReleaseInput()
        {
            leftPad?.ResetInput(); rightPad?.ResetInput(); input?.ResetBufferedInput();
        }
        private void RefreshDevice()
        {
            ReleaseInput();
            bool overlay = input != null && (input.IsHelpOpen || input.IsDebugOpen);
            operationPanel.SetActive(input != null && input.IsPanelOpen && !overlay);
            touchControls.SetActive(InputDeviceState.ActiveKind == InputDeviceKind.Touch && !overlay && input != null && !input.IsPanelOpen);
            SetEquipment(equipmentKind);
        }
        private void OnPanelChanged(bool open) => RefreshDevice();
        private void OnOverlayChanged(bool open)
        {
            var events = EventSystem.current;
            if (open && operationPanel.activeInHierarchy && events?.currentSelectedGameObject != null
                && events.currentSelectedGameObject.transform.IsChildOf(operationPanel.transform))
                menuSelection = events.currentSelectedGameObject;
            RefreshDevice();
            if (!open && operationPanel.activeInHierarchy && menuSelection != null && menuSelection.activeInHierarchy)
            {
                events?.SetSelectedGameObject(menuSelection);
                menuSelection = null;
            }
        }
        private void ClosePanel() => input?.SetPanelOpen(false);
        private void OnCommand(string name)
        {
            if (input == null) return;
            if (name == "ViewModifier") { touchLook = !touchLook; input.SetTouchLookMode(touchLook); return; }
            if (name != "ReelIn" && name != "ReelOut") input.Execute(name);
        }
        private void OnHold(string name, bool held)
        {
            if (name == "ArmOrReset") input?.SetTouchArmHeld(held);
            if (name == "ReelIn") input?.SetTouchLine(held ? -1 : 0);
            if (name == "ReelOut") input?.SetTouchLine(held ? 1 : 0);
        }
    }
}
