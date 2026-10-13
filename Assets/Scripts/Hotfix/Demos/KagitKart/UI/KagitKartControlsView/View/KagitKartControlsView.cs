namespace Hotfix
{
    using Core.Runtime;
    using Core.Runtime.Inputs;
    using Core.Runtime.Localization;
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
        private static readonly string[] TouchKeys =
        {
            "Input.Kart.TouchSteer", "Input.Kart.TouchThrottle", "Input.Kart.TouchBrake",
            "Input.Kart.TouchDrift", "Input.Kart.TouchItem", "Input.Kart.TouchLookBack",
            "Input.Kart.TouchPause", "Input.Kart.TouchMute", "Input.Kart.TouchRestart"
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
            InputBindingPrompt_Current.SetCaptionKey("Input.Back");
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
                values[i].gameObject.SetActive(device == 2);
                if (device == 2)
                {
                    LanguageService.SetText(values[i], TouchKeys[i]);
                    continue;
                }
                string path = i < 6 ? "Drive/" + (device == 1 ? "Pad" : "Key") + command : "Global/" + command;
                var prompt = InputPromptGroup_KagitKartControlsView.Get(command);
                prompt.SetDisplayDevice(device == 1 ? InputDeviceKind.Gamepad : InputDeviceKind.KeyboardMouse);
                prompt.Bind(world.Input.Asset.FindAction(path, true));
                prompt.SetCaptionKey(command switch
                {
                    "Drift" => "Input.Kart.DriftSteer",
                    "Item" => "Input.Kart.ItemBackward",
                    "Restart" => "Input.Kart.TimeTrialRestart",
                    _ => "Input." + command
                });
                prompt.SetBindingsVisible(world.ShowInputPrompts);
            }
            InputPromptGroup_KagitKartControlsView.SetVisible(device != 2);
            InputPromptGroup_KagitKartControlsView.SetRowVisible("Mute", device == 0);
            InputPromptGroup_KagitKartControlsView.SetRowVisible("Restart", device == 0);
            TextMeshProUGUI_MuteTitle.gameObject.SetActive(device != 1);
            TextMeshProUGUI_RestartTitle.gameObject.SetActive(device != 1);
            foreach (string direction in new[] { "Left", "Right" })
            {
                var prompt = InputPromptGroup_KagitKartControlsView.Get("Steer" + direction);
                prompt.gameObject.SetActive(device == 1);
                prompt.SetDisplayDevice(InputDeviceKind.Gamepad);
                prompt.Bind(world.Input.Asset.FindAction("Drive/Pad" + direction, true));
                prompt.SetCaptionKey("Input.Steer" + direction);
                prompt.SetBindingsVisible(world.ShowInputPrompts);
            }
            InputBindingPrompt_Current.BindAsset(world.Input.Asset);
            InputBindingPrompt_Current.gameObject.SetActive(world.ShowInputPrompts && InputDeviceState.PromptKind != InputDeviceKind.Touch);
            Button_Back.interactable = !world.Busy;
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
