namespace Hotfix
{
    using Core.Runtime;
    using Core.Runtime.Inputs;
    using Hotfix.KagitKart;
    using TMPro;
    using UnityEngine;

    [Module("KagitKart")]
    [UIBind("KagitKartHintsView")]
    public partial class KagitKartHintsView : View
    {
        private KagitKartWorld world;
        private int shownFrameRate = -2;
        private TMP_Text[] tutorialValues;
        private InputBindingPrompt[] tutorialGlyphs;
        private bool tutorialTouch;
        private bool tutorialGamepad;
        private KagitKartHintKind? shownHint;
        private bool shownReducedMotion;
        private static readonly string[] Commands = { "Steer", "Throttle", "Drift", "Item" };
        private static readonly string[] TouchValues =
        {
            "左拇指左右拖动转向滑条", "按住“油门”，或开启自动油门",
            "按住“漂移”并转向", "轻触“道具”或道具卡"
        };

        protected override void OnGameObjectInitialize()
        {
            tutorialValues = new TMP_Text[] { TextMeshProUGUI_Steer, TextMeshProUGUI_Throttle, TextMeshProUGUI_Drift, TextMeshProUGUI_Item };
            tutorialGlyphs = new[] { InputBindingPrompt_TutorialSteer, InputBindingPrompt_TutorialThrottle,
                InputBindingPrompt_TutorialDrift, InputBindingPrompt_TutorialItem };
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>显示前交付当前场景的非交互提示状态，不建立输入或菜单作用域。</summary>
        /// <param name="owner">本 Demo 场景；不拥有输入副本或比赛。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            Render();
        }

        protected override void OnShow()
        {
            var tip = UIRootManager.Instance.GetRoot(UILayer.Tip).GetComponent<Canvas>();
            Canvas_FpsGroup.overrideSorting = true;
            Canvas_FpsGroup.sortingLayerID = tip.sortingLayerID;
            Canvas_FpsGroup.sortingOrder = tip.sortingOrder + 1;
        }

        /// <summary>复用世界半秒采样，在菜单、比赛和暂停中更新原作帧率文本。</summary>
        /// <param name="framesPerSecond">-1为刚开启而尚未完成采样。</param>
        public void RefreshFrameRate(int framesPerSecond)
        {
            bool show = world?.Settings.ShowFps == true;
            RectTransform_FpsGroup.gameObject.SetActive(show);
            if (shownFrameRate == framesPerSecond) return;
            shownFrameRate = framesPerSecond;
            if (framesPerSecond < 0) TextMeshProUGUI_Fps.text = "";
            else TextMeshProUGUI_Fps.SetText("帧率 {0}", framesPerSecond);
        }

        private void Render()
        {
            RefreshFrameRate(world?.FramesPerSecond ?? -1);
            if (world?.Hints == null || world.Input == null)
            {
                return;
            }
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            bool teach = world.Hints.TutorialVisible;
            if (teach && !RectTransform_TutorialGroup.gameObject.activeSelf)
            {
                tutorialTouch = InputDeviceState.ActiveKind == InputDeviceKind.Touch;
                tutorialGamepad = InputDeviceState.ActiveKind == InputDeviceKind.Gamepad;
            }
            bool openingTutorial = teach && !RectTransform_TutorialGroup.gameObject.activeSelf;
            RectTransform_TutorialGroup.gameObject.SetActive(teach);
            if (teach && (openingTutorial || shownReducedMotion != world.ReducedMotion))
            {
                KagitKartMenuPaperMotion_TutorialMotion.Play(0, world.ReducedMotion);
            }
            if (teach)
            {
                bool touchLayout = world.ShowTouchControls;
                RectTransform_TutorialGroup.anchorMin = RectTransform_TutorialGroup.anchorMax = touchLayout ? new Vector2(0, 1) : new Vector2(1, 0.5f);
                RectTransform_TutorialGroup.anchoredPosition = touchLayout ? new Vector2(335, -315) : new Vector2(-345, 0);
                for (int i = 0; i < Commands.Length; i++)
                {
                    if (tutorialTouch)
                    {
                        tutorialValues[i].text = TouchValues[i];
                        tutorialGlyphs[i].Bind(null);
                        tutorialGlyphs[i].gameObject.SetActive(false);
                        continue;
                    }
                    var action = world.Input.Asset.FindAction("Drive/" + (tutorialGamepad ? "Pad" : "Key") + Commands[i], true);
                    string keys = KagitKartControlsView.Bindings(action, tutorialGamepad);
                    if (tutorialGamepad && i == 0)
                    {
                        keys += " / " + KagitKartControlsView.Bindings(world.Input.Asset.FindAction("Drive/PadLeft", true), true)
                            + " / " + KagitKartControlsView.Bindings(world.Input.Asset.FindAction("Drive/PadRight", true), true);
                    }
                    if (i == 2)
                    {
                        keys += " · 按住并转向";
                    }
                    else if (i == 3)
                    {
                        string backwardBrake = KagitKartControlsView.Bindings(world.Input.Asset.FindAction("Drive/" + (tutorialGamepad ? "Pad" : "Key") + "Brake", true), tutorialGamepad);
                        keys += " · " + backwardBrake + "＋道具向后发射";
                    }
                    tutorialValues[i].text = keys;
                    tutorialGlyphs[i].gameObject.SetActive(true);
                    tutorialGlyphs[i].Bind(action);
                }
            }
            var current = world.Hints.Current;
            RectTransform_HintGroup.gameObject.SetActive(current.HasValue);
            if (current.HasValue && (current != shownHint || shownReducedMotion != world.ReducedMotion))
            {
                KagitKartMenuPaperMotion_HintMotion.Play(0, world.ReducedMotion);
            }
            shownHint = current;
            shownReducedMotion = world.ReducedMotion;
            if (!current.HasValue)
            {
                return;
            }
            bool portrait = current == KagitKartHintKind.Portrait;
            RectTransform_HintGroup.anchorMin = RectTransform_HintGroup.anchorMax = new Vector2(0.5f, portrait ? 1 : 0);
            RectTransform_HintGroup.anchoredPosition = new Vector2(0, portrait ? -80 : 170);
            string drift = Key("Drift", "漂移");
            string item = Key("Item", "道具");
            string brake = Key("Brake", "刹车");
            TextMeshProUGUI_Hint.text = current switch
            {
                KagitKartHintKind.Corner => "漂移：按住 " + drift + " 并转向",
                KagitKartHintKind.Tier => "出现火花：松开 " + drift + " → 加速",
                KagitKartHintKind.Ramp => "在跳台边缘按下 " + drift,
                KagitKartHintKind.Item => "用 " + item + " 使用；" + brake + "＋" + item + " 向后发射",
                _ => "把手机横过来，体验更好"
            };
            bool touch = InputDeviceState.ActiveKind == InputDeviceKind.Touch;
            InputBindingPrompt_Context.gameObject.SetActive(!portrait && !touch);
            if (!portrait && !touch)
            {
                InputBindingPrompt_Context.Bind(world.Input.PromptAction(current == KagitKartHintKind.Item ? "Item" : "Drift"));
            }
        }

        private string Key(string command, string touchCaption)
        {
            if (InputDeviceState.ActiveKind == InputDeviceKind.Touch)
            {
                return "“" + touchCaption + "”";
            }
            bool gamepad = InputDeviceState.ActiveKind == InputDeviceKind.Gamepad;
            return KagitKartControlsView.Bindings(world.Input.Asset.FindAction("Drive/" + (gamepad ? "Pad" : "Key") + command, true), gamepad);
        }

        protected override void OnHide() => world = null;

        protected override void OnDestroy() => world = null;
    }
}
