namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.UI;

    [Module("KagitKart")]
    [UIBind("KagitKartSettingsView")]
    public partial class KagitKartSettingsView : View
    {
        private KagitKartWorld world;
        private Button[][] choices;

        protected override void OnGameObjectInitialize()
        {
            Slider_Music.wholeNumbers = Slider_Sfx.wholeNumbers = true;
            Slider_Music.onValueChanged.AddListener(value => Change("MusicVolume", (int)value));
            Slider_Sfx.onValueChanged.AddListener(value => Change("SfxVolume", (int)value));
            choices = new[]
            {
                new[] { Button_SoundOn, Button_SoundOff },
                new[] { Button_MotionAuto, Button_MotionOn, Button_MotionOff },
                new[] { Button_ShakeOn, Button_ShakeOff },
                new[] { Button_TouchAuto, Button_TouchOn, Button_TouchOff },
                new[] { Button_AutoOn, Button_AutoOff },
                new[] { Button_FpsOn, Button_FpsOff }
            };
            BindChoice(choices[0], "Muted", true);
            BindChoice(choices[1], "ReducedMotion", false);
            BindChoice(choices[2], "CameraShake", true);
            BindChoice(choices[3], "TouchControls", false);
            BindChoice(choices[4], "AutoAccelerate", true);
            BindChoice(choices[5], "ShowFps", true);
            Button_Back.onClick.AddListener(Back);
            UIMenuScope_Sheet.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>导航显示前交付当前宿主，默认值和真实生效状态来自同一份偏好。</summary>
        /// <param name="owner">当前 Demo 世界；页面不创建比赛或音频实例。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            // 菜单走menus.show折卡，暂停子页走panels.cleanup直接移除。
            KagitKartMenuPaperMotion_Viewport.FoldOnExit = owner.Session == null;
            KagitKartMenuPaperMotion_Viewport.Play(0, owner.ReducedMotion);
            Render();
        }

        private void BindChoice(Button[] buttons, string key, bool boolean)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                int value = boolean && key != "Muted" ? 1 - i : i;
                buttons[i].onClick.AddListener(() => Change(key, value));
            }
        }

        private void Change(string key, int value)
        {
            if (world != null)
            {
                GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.ChangeSetting, value, key));
            }
        }

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            KagitKartMenuPaperMotion_Viewport.SetReducedMotion(world.ReducedMotion);
            var settings = world.Settings;
            Slider_Music.SetValueWithoutNotify(Mathf.Round(settings.MusicVolume * 10));
            Slider_Sfx.SetValueWithoutNotify(Mathf.Round(settings.SfxVolume * 10));
            TextMeshProUGUI_MusicValue.text = Mathf.RoundToInt(settings.MusicVolume * 100) + "%";
            TextMeshProUGUI_SfxValue.text = Mathf.RoundToInt(settings.SfxVolume * 100) + "%";
            Select(choices[0], settings.Muted ? 1 : 0);
            Select(choices[1], Choice(settings.ReducedMotion));
            Select(choices[2], settings.CameraShake ? 0 : 1);
            Select(choices[3], Choice(settings.TouchControls));
            Select(choices[4], settings.AutoAccelerate ? 0 : 1);
            Select(choices[5], settings.ShowFps ? 0 : 1);
            Slider_Music.interactable = Slider_Sfx.interactable = Button_Back.interactable = !world.Busy;
            TextMeshProUGUI_Status.text = world.SettingsFeedback ?? "设置立即生效并保存在本机。";
        }

        private void Select(Button[] buttons, int selected)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].interactable = !world.Busy;
                buttons[i].GetComponent<UIState>().SetState(i == selected ? "Selected" : "Normal");
            }
        }

        private static int Choice(string value) => value == "auto" ? 0 : value == "on" ? 1 : 2;

        private void Back() => world?.Dispatch(KagitKartCommand.CloseOptions);

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Back;
            world = null;
        }
    }
}
