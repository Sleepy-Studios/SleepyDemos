using Core.Runtime;
using Core.Runtime.Inputs;
using Core.Runtime.Settings;
using Hotfix.Dlss;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Hotfix
{
    [Module("Dlss")]
    [UIBind("DlssControlsView")]
    public partial class DlssControlsView : View
    {
        private GameObject touchControls;
        private TouchInputPad movePad, lookPad;
        private InputCommandButton[] buttons;
        private InputBindingPrompt[] prompts;
        private DlssData data;
        internal Vector2 Move => movePad.Move;
        internal Vector2 Look => lookPad.ConsumeLook();
        internal bool Sprint => data?.Sprint == true;
        protected override void OnGameObjectInitialize()
        {
            touchControls = RectTransform_TouchControls.gameObject;
            movePad = TouchInputPad_Move;
            lookPad = TouchInputPad_Look;
            buttons = new Core.Runtime.Inputs.InputCommandButton[] { InputCommandButton_Sprint, InputCommandButton_Reset, InputCommandButton_Settings, InputCommandButton_Exit };
            prompts = gameObject.GetComponentsInChildren<InputBindingPrompt>(true);
            foreach (var prompt in prompts)
            {
                prompt.SetCaptionKey(prompt.Action?.name switch
                {
                    "Sprint" => "Input.Sprint",
                    "Reset" => "Input.ResetView",
                    "Settings" => "Input.Settings",
                    "Exit" => "Input.ReturnHub",
                    _ => throw new System.InvalidOperationException("正式 DLSS 提示缺少已配置的观察动作。")
                });
            }
            GameSettingsService.Changed += OnPublicSettingsChanged;
            OnPublicSettingsChanged(GameSettingsService.Current);
            foreach (var button in buttons) { this.RegisterInputCommandButton(button, OnCommand); this.RegisterInputCommandHold(button, OnHold); }
            BindData<DlssData>(OnData);
        }

        private void OnPublicSettingsChanged(GameSettingsSnapshot value)
        {
            foreach (var prompt in prompts)
            {
                prompt.SetBindingsVisible(value.Accessibility.ShowInputPrompts);
            }
        }
        /// <summary>组件初始化后、显示前绑定当前观察动作副本。</summary>
        /// <param name="actions">当前场景的动作副本。</param>
        public void SetData(InputActionAsset actions) { foreach(var prompt in prompts) prompt.BindAsset(actions); ResetInput(); }
        private void OnData(DlssData value)
        {
            data = value;
            bool touch = value.DeviceKind == InputDeviceKind.Touch;
            if (touchControls.activeSelf != touch) touchControls.SetActive(touch);
            if (!touch) ResetInput();
        }
        private void OnCommand(string command) => GlobalData.Dispatch(new DlssControlAction(command));
        private void OnHold(string command, bool held) { if(command=="Sprint") GlobalData.Dispatch(new DlssSprintAction(held)); }
        private void ResetInput() { movePad.ResetInput(); lookPad.ResetInput(); GlobalData.Dispatch(new DlssSprintAction(false)); }
        protected override void OnHide() { ResetInput(); base.OnHide(); }
        protected override void OnDestroy()
        {
            GameSettingsService.Changed -= OnPublicSettingsChanged;
            ResetInput();
            data = null;
            base.OnDestroy();
        }
    }
}
