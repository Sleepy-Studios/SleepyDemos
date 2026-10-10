namespace Hotfix
{
    using System;
    using System.Collections.Generic;
    using Core.Runtime;
    using Core.Runtime.Inputs;
    using Hotfix.KagitKart;
    using TMPro;
    using UnityEngine;
    using UnityEngine.InputSystem;
    using UnityEngine.UI;

    [Module("KagitKart")]
    [UIBind("KagitKartControlsView")]
    public partial class KagitKartControlsView : View
    {
        private KagitKartWorld world;
        private int device;
        private Button[] tabs;
        private TMP_Text[] values;
        private static readonly string[] Commands = { "Steer", "Throttle", "Brake", "Drift", "Item", "LookBack", "Pause", "Mute", "Restart" };
        private static readonly string[] TouchText =
        {
            "左拇指左右拖动转向滑条", "按住“油门”，或开启自动油门", "按住“刹车”",
            "按住“漂移”并转向", "轻触“道具”；按住刹车再使用可向后发射", "按住“回头”",
            "轻触画面上的“暂停”按钮", "设置 → 声音", "暂停 → 重新开始"
        };

        protected override void OnGameObjectInitialize()
        {
            tabs = new[] { Button_Keyboard, Button_Gamepad, Button_Touch };
            values = new TMP_Text[] { TextMeshProUGUI_Steer, TextMeshProUGUI_Throttle, TextMeshProUGUI_Brake,
                TextMeshProUGUI_Drift, TextMeshProUGUI_Item, TextMeshProUGUI_LookBack, TextMeshProUGUI_Pause,
                TextMeshProUGUI_Mute, TextMeshProUGUI_Restart };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                tabs[i].onClick.AddListener(() => Pick(index));
            }
            Button_Back.onClick.AddListener(Back);
            UIMenuScope_Sheet.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>显示前交付场景输入副本，键鼠/手柄提示读取本场生效绑定。</summary>
        /// <param name="owner">当前 Demo 宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            device = InputDeviceState.ActiveKind == InputDeviceKind.Touch ? 2 : InputDeviceState.PromptKind == InputDeviceKind.Gamepad ? 1 : 0;
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            // 菜单走menus.show折卡，暂停子页走panels.cleanup直接移除。
            KagitKartMenuPaperMotion_Viewport.FoldOnExit = owner.Session == null;
            KagitKartMenuPaperMotion_Viewport.Play(0, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow()
        {
            InputSystem.onActionChange += OnActionChange;
            InputDeviceState.Changed += Render;
        }

        private void Pick(int index)
        {
            device = index;
            Render();
        }

        private void Render()
        {
            if (world?.Input == null)
            {
                return;
            }
            KagitKartMenuPaperMotion_Viewport.SetReducedMotion(world.ReducedMotion);
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].interactable = !world.Busy;
                tabs[i].GetComponent<UIState>().SetState(i == device ? "Selected" : "Normal");
            }
            for (int i = 0; i < Commands.Length; i++)
            {
                string command = Commands[i];
                if (device == 2)
                {
                    values[i].text = TouchText[i];
                    continue;
                }
                string path = i < 6 ? "Drive/" + (device == 1 ? "Pad" : "Key") + command : "Global/" + command;
                string text = Bindings(world.Input.Asset.FindAction(path, true), device == 1);
                if (device == 1 && command == "Steer")
                {
                    text += " / " + Bindings(world.Input.Asset.FindAction("Drive/PadLeft", true), true)
                        + " / " + Bindings(world.Input.Asset.FindAction("Drive/PadRight", true), true);
                }
                values[i].text = command == "Drift" ? text + " · 按住并转向"
                    : command == "Item" ? text + " · 刹车＋道具可向后发射"
                    : command == "Restart" ? text + " · 计时赛快捷重赛" : text;
            }
            InputBindingPrompt_Current.Bind(world.Input.PromptAction("Drift"));
            InputBindingPrompt_Current.gameObject.SetActive(InputDeviceState.PromptKind != InputDeviceKind.Touch);
            Button_Back.interactable = !world.Busy;
        }

        internal static string Bindings(InputAction action, bool gamepad)
        {
            var labels = new List<string>();
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.isComposite || string.IsNullOrEmpty(binding.effectivePath)
                    || (gamepad ? !binding.effectivePath.StartsWith("<Gamepad>", StringComparison.Ordinal)
                        : !(binding.effectivePath.StartsWith("<Keyboard>", StringComparison.Ordinal) || binding.effectivePath.StartsWith("<Mouse>", StringComparison.Ordinal))))
                {
                    continue;
                }
                string label = action.GetBindingDisplayString(i, InputBinding.DisplayStringOptions.DontIncludeInteractions)
                    .Replace("Left Arrow", "←").Replace("Right Arrow", "→").Replace("Up Arrow", "↑").Replace("Down Arrow", "↓")
                    .Replace("Space", "空格").Replace("Left Shift", "左 Shift").Replace("Right Shift", "右 Shift")
                    .Replace("Escape", "Esc").Replace("Left Stick", "左摇杆").Replace("Right Stick", "右摇杆")
                    .Replace("D-Pad", "方向键").Replace("Left Trigger", "左扳机").Replace("Right Trigger", "右扳机")
                    .Replace("Left Shoulder", "左肩键").Replace("Right Shoulder", "右肩键").Replace("Start", "开始键");
                label = label switch { "Left" => "←", "Right" => "→", "Up" => "↑", "Down" => "↓", _ => label };
                if (!labels.Contains(label))
                {
                    labels.Add(label);
                }
            }
            return labels.Count == 0 ? "无此设备绑定" : string.Join(" / ", labels);
        }

        private void OnActionChange(object target, InputActionChange change)
        {
            if (change == InputActionChange.BoundControlsChanged)
            {
                Render();
            }
        }

        private void Back() => world?.Dispatch(KagitKartCommand.CloseOptions);

        protected override void OnHide()
        {
            InputSystem.onActionChange -= OnActionChange;
            InputDeviceState.Changed -= Render;
            InputBindingPrompt_Current.Bind(null);
            world = null;
        }

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Back;
            InputSystem.onActionChange -= OnActionChange;
            InputDeviceState.Changed -= Render;
            world = null;
        }
    }
}
