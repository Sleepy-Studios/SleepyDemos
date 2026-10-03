using Core.Runtime;
using Core.Runtime.Inputs;
using Hotfix.DroneFlight;
using UnityEngine.InputSystem;

namespace Hotfix
{
    /// 按当前输入设备与装备显示操作说明，不提交飞行或装备命令。
    [Module("DroneFlight"), Mvc("DroneFlightHelpView")]
    public partial class DroneFlightHelpView : View<DroneFlightViewData>
    {
        /// <summary>交付会话数据；已显示的指南立即更新装备和设备说明。</summary>
        /// <param name="data">当前机体与输入会话。</param>
        /// <returns>当前指南。</returns>
        public override View<DroneFlightViewData> SetData(DroneFlightViewData data)
        {
            base.SetData(data);
            if (State == ViewState.Visible) Refresh();
            return this;
        }

        protected override void OnGameObjectInitialize()
        {
            Button_Close.onClick.AddListener(Close);
            Button_Return.onClick.AddListener(Close);
            UIMenuScope_DroneFlightHelpView.Canceled += Close;
        }

        protected override void OnShow()
        {
            base.OnShow();
            InputDeviceState.Changed += Refresh;
            Refresh();
            ScrollRect_Guide.verticalNormalizedPosition = 1;
        }

        protected override void OnHide()
        {
            InputDeviceState.Changed -= Refresh;
            base.OnHide();
        }

        protected override void OnDestroy()
        {
            InputDeviceState.Changed -= Refresh;
            base.OnDestroy();
        }

        private void Close() => params1?.Input?.Execute("Help");

        private string Key(string action, string fallback = "操作面板")
        {
            var binding = params1?.Input?.Session?.Asset.FindAction("Flight/" + action);
            string label = InputBindingDisplay.Get(binding, out _);
            return Cap(string.IsNullOrEmpty(label) ? fallback : label);
        }

        private static string Cap(string label) => "<mark=#3A4855C0><color=#F4F1E9> " + label + " </color></mark>";

        private string Part(string actionName, string part)
        {
            var action = params1?.Input?.Session?.Asset.FindAction("Flight/" + actionName);
            if (action != null)
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (!binding.isPartOfComposite || !string.Equals(binding.name, part, System.StringComparison.OrdinalIgnoreCase) ||
                        !binding.effectivePath.StartsWith("<Keyboard>")) continue;
                    string label = binding.effectivePath switch
                    {
                        "<Keyboard>/upArrow" => "↑", "<Keyboard>/downArrow" => "↓",
                        "<Keyboard>/leftArrow" => "←", "<Keyboard>/rightArrow" => "→",
                        _ => action.GetBindingDisplayString(i)
                    };
                    return Cap(label);
                }
            return Cap("未绑定");
        }

        private string Axis(string action, string first, string second, string caption)
            => Part(action, first) + " / " + Part(action, second) + "<pos=270>" + caption;

        private void Refresh()
        {
            bool touch = InputDeviceState.PromptKind == InputDeviceKind.Touch;
            bool pad = InputDeviceState.PromptKind == InputDeviceKind.Gamepad;
            TextMeshProUGUI_Device.text = touch ? "触屏" : pad ? "手柄 · " + InputDeviceState.PromptStyle : "键鼠";
            TextMeshProUGUI_Flight.text = touch
                ? "<color=#F4A23A>左摇杆</color>  上下升降 / 左右偏航\n\n<color=#F4A23A>右摇杆</color>  前后 / 左右平移"
                : pad ? Key("Move") + "  水平移动\n\n" + Key("VerticalYaw") + "  升降 / 偏航"
                : Axis("Move", "Up", "Down", "前进 / 后退") + "\n" + Axis("Move", "Left", "Right", "左右平移") + "\n" +
                  Axis("VerticalYaw", "Up", "Down", "上升 / 下降") + "\n" + Axis("VerticalYaw", "Left", "Right", "左右偏航");
            string look = touch ? "<color=#F4A23A>镜头模式 + 右摇杆</color>" : pad
                ? Key("ViewModifier") + " + " + Key("PadLook") : Part("CameraLook", "Up") + " " + Part("CameraLook", "Down") + " " + Part("CameraLook", "Left") + " " + Part("CameraLook", "Right");
            TextMeshProUGUI_Camera.text = Key("SwitchCamera", "切换镜头") + "  切换视角\n" + look + "  调整镜头\n" +
                Key("Zoom") + "  缩放视野\n" + Key("LandingGear") + "  起落架收放";
            TextMeshProUGUI_Takeoff.text = Key("ArmOrReset", "确认按钮") + "  解锁 / 锁定\n" +
                (pad || touch ? "<color=#F4A23A>操作面板</color>  自动起飞 / 降落\n<color=#F4A23A>操作面板</color>  平稳 / 普通 / 运动" :
                Key("Takeoff") + " / " + Key("Landing") + "  自动起飞 / 降落\n" +
                Key("ProfileCine") + " 平稳  " + Key("ProfileNormal") + " 普通  " + Key("ProfileSport") + " 运动");
            var kind = params1?.TelemetrySource?.Current.Equipment.Kind ?? DroneEquipmentKind.None;
            TextMeshProUGUI_Equipment.text = kind switch
            {
                DroneEquipmentKind.Grapple => "<b>四爪抓斗</b>\n" + Key("Equipment", "装备按钮") + "  四爪开合\n" + Key("Line", "收线 / 放线按钮") + "  上收 / 下放",
                DroneEquipmentKind.Harpoon => "<b>渔叉</b>\n" + Key("Aim", "瞄准按钮") + "  机腹瞄准\n" + Key("Equipment", "装备按钮") + "  发射 / 解除回收\n" + Key("Line", "收线 / 放线按钮") + "  收线 / 放线\n" + (pad || touch ? "镜头模式下用右摇杆调整准星" : "鼠标移动瞄准点"),
                _ => "<b>纯无人机</b>\n\n未安装附加装备\n更换机型可使用抓斗或渔叉"
            };
            TextMeshProUGUI_Reset.text = "长按 " + Key("ArmOrReset", "确认按钮") + " " +
                (params1?.Input?.ResetHoldSeconds ?? 5).ToString("0.#") + " 秒重新运行场景";
            TextMeshProUGUI_ReturnLabel.text = params1?.Input?.IsPanelOpen == true ? "返回操作面板" : "返回飞行";
        }
    }
}
