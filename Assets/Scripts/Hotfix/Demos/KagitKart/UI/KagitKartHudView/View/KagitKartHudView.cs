namespace Hotfix
{
    using System;
    using Core.Runtime;
    using Core.Runtime.Inputs;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.UI;

    [Module("KagitKart")]
    [UIBind("KagitKartHudView")]
    public partial class KagitKartHudView : View
    {
        private KagitKartWorld world;
        private InputCommandButton[] holds;
        private Rect lastSafe;
        private Vector2 lastScreen;
        private KagitKartVehicle inkPlayer;
        private KagitKartRandom inkRandom;

        protected override void OnGameObjectInitialize()
        {
            inkRandom = new KagitKartRandom(unchecked((uint)Environment.TickCount)).Fork("hudInk");
            holds = new[] { InputCommandButton_Throttle, InputCommandButton_Brake, InputCommandButton_Drift,
                InputCommandButton_Item, InputCommandButton_LookBack, InputCommandButton_ItemCard };
            foreach (var button in holds)
            {
                button.HoldChanged += Hold;
            }
            Button_Pause.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Pause));
            Button_SkipIntro.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.SkipIntro));
            Button_Auto.onClick.AddListener(() =>
            {
                if (world?.Input != null)
                {
                    GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.ChangeSetting,
                        world.Input.AutoAccelerate ? 0 : 1, "AutoAccelerate"));
                }
            });
            BindData<KagitKartData>(_ => Render());
        }

        protected override void OnShow()
        {
            var foreground = UIRootManager.Instance.GetRoot(UILayer.Foreground).GetComponent<Canvas>();
            Canvas_Ink.overrideSorting = true;
            Canvas_Ink.sortingLayerID = foreground.sortingLayerID;
            Canvas_Ink.sortingOrder = foreground.sortingOrder - 1;
        }

        /// <summary>导航显示前交付当前场景，不拥有比赛或输入实例。</summary>
        /// <param name="owner">当前场景宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            UnbindInk();
            world = owner;
            inkPlayer = owner.Session.Player;
            inkPlayer.Signaled += OnInkSignal;
            KagitKartMinimap_Minimap.SetData(owner.Session);
            KagitKartItemSlot_ItemCardGroup.Bind(owner.Session.Player);
            KagitKartHudNotice_HudNotice.Bind(owner);
            Render();
        }

        /// 滴落按原作CSS墙钟更新，渐隐读取暂停感知的规则剩余时间。
        public void AdvanceVisual()
        {
            if (world?.Session == null) return;
            float wallDt = Time.unscaledDeltaTime;
            float dt = world.Session.Pause.IsPaused ? 0 : Mathf.Min(wallDt, 0.1f);
            if (inkPlayer != null) KagitKartHudInk_Ink.Advance(wallDt, inkPlayer.InkTime, world.ReducedMotion);
            KagitKartItemSlot_ItemCardGroup.Advance(dt, world.ReducedMotion);
            KagitKartHudNotice_HudNotice.Advance(dt, wallDt);
        }

        private void OnInkSignal(KagitKartVehicle player, KagitKartVehicleSignal signal)
        {
            if (signal == KagitKartVehicleSignal.Inked)
            {
                KagitKartHudInk_Ink.Splat(player.LastInkDuration, (uint)(inkRandom.Next() * 1e9));
            }
        }

        private void UnbindInk()
        {
            if (inkPlayer != null) inkPlayer.Signaled -= OnInkSignal;
            inkPlayer = null;
            if (KagitKartHudInk_Ink != null) KagitKartHudInk_Ink.Clear();
        }

        /// 每帧交付触屏滑条，按钮保持由公共组件的事件交付。
        public void ReadTouch()
        {
            if (world?.Input == null || !RectTransform_TouchControls.gameObject.activeInHierarchy)
            {
                return;
            }
            world.Input.SetTouchSteer(TouchInputPad_Steering.Move.x);
            RectTransform_SteerKnob.anchoredPosition = new Vector2(TouchInputPad_Steering.Move.x * 176, 0);
        }

        /// 输入作用域变化时释放公共触控组件的指针。
        public void ClearTouch()
        {
            TouchInputPad_Steering.ResetInput();
            world?.Input?.SetTouchSteer(0);
            RectTransform_TouchControls.gameObject.SetActive(false);
        }

        private void Hold(string command, bool held) => world?.Input?.SetTouchHold(command, held);

        private void Render()
        {
            if (world?.Session == null)
            {
                return;
            }
            var session = world.Session;
            var player = session.Player;
            bool results = session.Race.State == KagitKartRaceState.Done;
            if (results && RectTransform_Viewport.gameObject.activeSelf)
            {
                KagitKartHudNotice_HudNotice.Clear();
                KagitKartHudInk_Ink.Clear();
            }
            RectTransform_Viewport.gameObject.SetActive(!results);
            KagitKartHudInk_Ink.gameObject.SetActive(!results);
            bool driving = !world.Busy && !session.Pause.IsPaused && session.Race.State != KagitKartRaceState.Done;
            bool touch = world.ShowTouchControls;
            bool showTouch = driving && !world.IntroPlaying && touch;
            if (!showTouch && RectTransform_TouchControls.gameObject.activeSelf) ClearTouch();
            RectTransform_TouchControls.gameObject.SetActive(showTouch);
            Button_SkipIntro.transform.parent.gameObject.SetActive(world.IntroPlaying && !session.Pause.IsPaused);
            RectTransform_IntroCardGroup.gameObject.SetActive(world.IntroPlaying);
            TextMeshProUGUI_IntroChapter.text = $"第{world.Session.Track.Track.definition.chapter}章";
            TextMeshProUGUI_IntroName.text = KagitKartCatalog.TrackName(world.TrackId);
            TextMeshProUGUI_IntroSubtitle.text = KagitKartCatalog.TrackDescription(world.TrackId);
            RectTransform_ItemCardGroup.gameObject.SetActive(!world.IntroPlaying);
            RectTransform_LapCardGroup.gameObject.SetActive(!world.IntroPlaying);
            RectTransform_MapCardGroup.gameObject.SetActive(!world.IntroPlaying);
            RectTransform_PlaceCardGroup.gameObject.SetActive(!world.IntroPlaying);
            Button_Pause.interactable = driving;
            KagitKartItemSlot_ItemCardGroup.SetLayout(showTouch);
            InputCommandButton_ItemCard.enabled = showTouch;
            InputCommandButton_ItemCard.GetComponent<Image>().raycastTarget = showTouch;
            Button_Auto.GetComponentInChildren<TMPro.TMP_Text>().text = "自动油门：" + (world.Input.AutoAccelerate ? "开" : "关");
            KagitKartMinimap_Minimap.Refresh();
            var map = RectTransform_MapCardGroup;
            map.anchorMin = map.anchorMax = touch ? Vector2.one : Vector2.zero;
            map.anchoredPosition = touch ? new Vector2(-195, -440) : new Vector2(185, 175);
            map.localScale = touch ? Vector3.one * 0.75f : Vector3.one;
            var place = RectTransform_PlaceCardGroup;
            place.anchorMin = place.anchorMax = touch ? new Vector2(0, 1) : new Vector2(1, 0);
            place.anchoredPosition = touch ? new Vector2(354, -135) : new Vector2(-150, 132);
            place.localScale = touch ? Vector3.one * 0.75f : Vector3.one;
            if (lastSafe != Screen.safeArea || lastScreen != new Vector2(Screen.width, Screen.height))
            {
                lastSafe = Screen.safeArea;
                lastScreen = new Vector2(Screen.width, Screen.height);
                RectTransform_Viewport.anchorMin = new Vector2(lastSafe.xMin / Mathf.Max(1, Screen.width), lastSafe.yMin / Mathf.Max(1, Screen.height));
                RectTransform_Viewport.anchorMax = new Vector2(lastSafe.xMax / Mathf.Max(1, Screen.width), lastSafe.yMax / Mathf.Max(1, Screen.height));
                TouchInputPad_Steering.ConfigureHorizontal(Mathf.Clamp(Screen.width * 0.075f, 46, 110) * 720 / Mathf.Max(1, Screen.height));
            }
        }

        internal static string TimeText(double seconds) => KagitKartHudMotion.TimeText(seconds);

        protected override void OnHide()
        {
            UnbindInk();
            KagitKartItemSlot_ItemCardGroup.Bind(null);
            KagitKartHudNotice_HudNotice.Clear();
            InputCommandButton_ItemCard.enabled = false;
            ClearTouch();
            world = null;
            KagitKartMinimap_Minimap.SetData(null);
        }

        protected override void OnDestroy()
        {
            UnbindInk();
            KagitKartHudNotice_HudNotice.Clear();
            foreach (var button in holds)
            {
                button.HoldChanged -= Hold;
            }
            world = null;
        }
    }
}
