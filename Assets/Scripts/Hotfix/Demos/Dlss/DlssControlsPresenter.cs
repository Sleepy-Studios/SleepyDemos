using Core.Runtime.Inputs;
using UnityEngine;

namespace Hotfix.Dlss
{
    /// 保存的观察控制界面；不同输入设备消费相同观察命令。
    public sealed class DlssControlsPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject touchControls;
        [SerializeField] private TouchInputPad movePad;
        [SerializeField] private TouchInputPad lookPad;
        [SerializeField] private InputCommandButton[] buttons;
        [SerializeField] private InputBindingPrompt[] prompts;
        private DlssDemoController owner;
        internal Vector2 Move => movePad.Move;
        internal Vector2 Look => lookPad.ConsumeLook();
        internal bool Sprint { get; private set; }
        internal void Bind(DlssDemoController controller)
        {
            owner = controller;
            foreach (var button in buttons) { button.Clicked += OnCommand; button.HoldChanged += OnHold; }
            foreach (var prompt in prompts)
                if (prompt.Action != null) prompt.Bind(owner.Actions.Asset.FindAction(prompt.Action.id));
            InputDeviceState.Changed += Refresh; Refresh();
        }
        private void OnDestroy()
        {
            foreach (var button in buttons) { button.Clicked -= OnCommand; button.HoldChanged -= OnHold; }
            InputDeviceState.Changed -= Refresh;
        }
        private void OnDisable() { movePad.ResetInput(); lookPad.ResetInput(); Sprint = false; }
        private void OnCommand(string name)
        {
            if (owner == null || !owner.AcceptsControls) return;
            switch (name)
            {
                case "Reset": owner.ResetCamera(); break;
                case "Exit": owner.RequestExit(); break;
                case "Settings": owner.OpenSettings(); break;
            }
        }
        private void OnHold(string name, bool held) { if (name == "Sprint") Sprint = held; }
        private void Refresh()
        {
            touchControls.SetActive(InputDeviceState.ActiveKind == InputDeviceKind.Touch);
            if (!touchControls.activeSelf) { movePad.ResetInput(); lookPad.ResetInput(); Sprint = false; }
        }
    }
}
