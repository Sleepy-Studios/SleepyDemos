namespace Hotfix
{
    using Core.Runtime;
    using Core.Runtime.Inputs;
    using Core.Runtime.Localization;
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
        private static readonly string[] TouchKeys =
        {
            "Input.Kart.TouchSteer", "Input.Kart.TouchThrottle",
            "Input.Kart.TouchDrift", "Input.Kart.TouchItemCard"
        };

        protected override void OnGameObjectInitialize()
        {
            tutorialValues = new TMP_Text[] { TextMeshProUGUI_Steer, TextMeshProUGUI_Throttle, TextMeshProUGUI_Drift, TextMeshProUGUI_Item };
            tutorialGlyphs = new[] { InputBindingPrompt_TutorialSteer, InputBindingPrompt_TutorialThrottle,
                InputBindingPrompt_TutorialDrift, InputBindingPrompt_TutorialItem };
            for (int i = 0; i < tutorialGlyphs.Length; i++)
            {
                tutorialGlyphs[i].SetCaptionKey("Input." + Commands[i]);
            }
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
            bool teach = world.ShowInputPrompts && world.Hints.TutorialVisible;
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
                    tutorialValues[i].gameObject.SetActive(tutorialTouch);
                    if (tutorialTouch)
                    {
                        LanguageService.SetText(tutorialValues[i], TouchKeys[i]);
                        tutorialGlyphs[i].Bind(null);
                        tutorialGlyphs[i].gameObject.SetActive(false);
                        continue;
                    }
                    var action = world.Input.Asset.FindAction("Drive/" + (tutorialGamepad ? "Pad" : "Key") + Commands[i], true);
                    tutorialGlyphs[i].gameObject.SetActive(true);
                    tutorialGlyphs[i].SetDisplayDevice(tutorialGamepad ? InputDeviceKind.Gamepad : InputDeviceKind.KeyboardMouse);
                    tutorialGlyphs[i].Bind(action);
                    tutorialGlyphs[i].SetCaptionKey(i switch
                    {
                        0 => "Input.Steer",
                        1 => "Input.Throttle",
                        2 => "Input.Kart.DriftSteer",
                        _ => "Input.Kart.UseItemBackward"
                    });
                }
            }
            var current = world.Hints.Current;
            RectTransform_HintGroup.gameObject.SetActive(world.ShowInputPrompts && current.HasValue);
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
            bool touch = InputDeviceState.ActiveKind == InputDeviceKind.Touch;
            string hintKey = current switch
            {
                KagitKartHintKind.Corner => "Input.Kart.HintCorner",
                KagitKartHintKind.Tier => "Input.Kart.HintTier",
                KagitKartHintKind.Ramp => "Input.Kart.HintRamp",
                KagitKartHintKind.Item => "Input.Kart.HintItem",
                _ => "Input.Kart.HintPortrait"
            };
            LanguageService.SetText(TextMeshProUGUI_Hint, hintKey);
            TextMeshProUGUI_Hint.gameObject.SetActive(portrait || touch);
            InputBindingPrompt_Context.gameObject.SetActive(world.ShowInputPrompts && !portrait && !touch);
            if (!portrait && !touch)
            {
                InputBindingPrompt_Context.SetDisplayDevice(null);
                InputBindingPrompt_Context.Bind(world.Input.PromptAction(current == KagitKartHintKind.Item ? "Item" : "Drift"));
                InputBindingPrompt_Context.SetCaptionKey(current switch
                {
                    KagitKartHintKind.Corner => "Input.Kart.HintCornerPhysical",
                    KagitKartHintKind.Tier => "Input.Kart.HintTierPhysical",
                    KagitKartHintKind.Ramp => "Input.Kart.HintRampPhysical",
                    _ => "Input.Kart.HintItemPhysical"
                });
            }
        }

        protected override void OnHide() => world = null;

        protected override void OnDestroy() => world = null;
    }
}
