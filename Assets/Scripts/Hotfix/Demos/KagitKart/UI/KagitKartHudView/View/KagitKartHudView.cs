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

        protected override void OnGameObjectInitialize()
        {
            holds = new[] { InputCommandButton_Throttle, InputCommandButton_Brake, InputCommandButton_Drift,
                InputCommandButton_Item, InputCommandButton_LookBack };
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

        /// <summary>导航显示前交付当前场景，不拥有比赛或输入实例。</summary>
        /// <param name="owner">当前场景宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            KagitKartMinimap_Minimap.SetData(owner.Session);
            Render();
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
            bool driving = !world.Busy && !session.Pause.IsPaused && session.Race.State != KagitKartRaceState.Done;
            bool touch = world.ShowTouchControls;
            TextMeshProUGUI_Fps.gameObject.SetActive(world.Settings.ShowFps);
            TextMeshProUGUI_Fps.text = "帧率 " + world.FramesPerSecond;
            bool showTouch = driving && !world.IntroPlaying && touch;
            if (!showTouch && RectTransform_TouchControls.gameObject.activeSelf) ClearTouch();
            RectTransform_TouchControls.gameObject.SetActive(showTouch);
            Button_SkipIntro.gameObject.SetActive(world.IntroPlaying && !session.Pause.IsPaused);
            RectTransform_IntroCardGroup.gameObject.SetActive(world.IntroPlaying);
            TextMeshProUGUI_IntroChapter.text = $"第{world.Session.Track.Track.definition.chapter}章";
            TextMeshProUGUI_IntroName.text = KagitKartCatalog.TrackName(world.TrackId);
            TextMeshProUGUI_IntroSubtitle.text = KagitKartCatalog.TrackDescription(world.TrackId);
            RectTransform_ItemCardGroup.gameObject.SetActive(!world.IntroPlaying);
            RectTransform_LapCardGroup.gameObject.SetActive(!world.IntroPlaying);
            RectTransform_MapCardGroup.gameObject.SetActive(!world.IntroPlaying);
            RectTransform_PlaceCardGroup.gameObject.SetActive(!world.IntroPlaying);
            Button_Pause.interactable = driving;
            TextMeshProUGUI_Item.text = player.Roulette > 0 ? "抽取中…"
                : player.Item == null ? "暂无道具" : KagitKartCatalog.ItemName(player.Item);
            TextMeshProUGUI_ItemCount.text = player.ItemCount > 1 ? "×" + player.ItemCount : "";
            TextMeshProUGUI_Lap.text = $"圈数 {Mathf.Clamp(world.PlayerEntry.Progress.Lap, 1, 3)} / 3";
            TextMeshProUGUI_Clock.text = TimeText(session.RaceTime);
            TextMeshProUGUI_Place.text = player.Place.ToString();
            TextMeshProUGUI_Countdown.text = !world.IntroPlaying && session.Race.State == KagitKartRaceState.Countdown ? session.Race.Countdown.ToString() : "";
            TextMeshProUGUI_Banner.text = world.Feedback ?? (world.PlayerEntry.Progress.WrongWay ? "逆行！请调头"
                : player.Rescuing ? "纸鹤救援中…" : player.Finished && session.Race.State == KagitKartRaceState.Finishing ? "冲线！等待其他车手"
                : world.PlayerEntry.Progress.Lap == 3 && !player.Finished ? "最后一圈" : "");
            TextMeshProUGUI_Drift.text = player.Drifting ? $"漂移蓄力  {new string('●', player.DriftTier)}" : "";
            TextMeshProUGUI_Drift.color = player.DriftTier == 3 ? new Color(0.7f, 0.33f, 0.76f)
                : player.DriftTier == 2 ? new Color(0.9f, 0.43f, 0.12f) : new Color(0.17f, 0.4f, 0.65f);
            InputBindingPrompt_ThrottlePrompt.Bind(world.Input.PromptAction("Throttle"));
            InputBindingPrompt_DriftPrompt.Bind(world.Input.PromptAction("Drift"));
            InputBindingPrompt_ItemPrompt.Bind(world.Input.PromptAction("Item"));
            InputBindingPrompt_ThrottlePrompt.gameObject.SetActive(!world.IntroPlaying && !touch);
            InputBindingPrompt_DriftPrompt.gameObject.SetActive(!world.IntroPlaying && !touch);
            InputBindingPrompt_ItemPrompt.gameObject.SetActive(!world.IntroPlaying && !touch);
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

        internal static string TimeText(double seconds)
        {
            int millis = (int)Math.Max(0, Math.Floor(seconds * 1000));
            return $"{millis / 60000:00}:{millis / 1000 % 60:00}.{millis % 1000:000}";
        }

        protected override void OnHide()
        {
            ClearTouch();
            world = null;
            KagitKartMinimap_Minimap.SetData(null);
        }

        protected override void OnDestroy()
        {
            foreach (var button in holds)
            {
                button.HoldChanged -= Hold;
            }
            world = null;
        }
    }
}
