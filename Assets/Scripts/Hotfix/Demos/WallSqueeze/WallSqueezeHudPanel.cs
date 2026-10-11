using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Core.Runtime.Inputs;
using Core.Runtime.Localization;
using Core.Runtime.Settings;

namespace Hotfix.WallSqueeze
{
    /// HUD 保存结构的表现引用。
    public sealed class WallSqueezeHudPanel : MonoBehaviour
    {
        [SerializeField] private TMP_Text stats;
        [SerializeField] private TMP_Text remaining;
        [SerializeField] private TMP_Text residents;
        [SerializeField] private TMP_Text clock;
        [SerializeField] private TMP_Text hint;
        [SerializeField] private Button pause;
        [SerializeField] private Button retry;
        private WallSqueezeWorld world;
        private bool showInputPrompts;

        private void Awake()
        {
            showInputPrompts = GameSettingsService.Current.Accessibility.ShowInputPrompts;
            GameSettingsService.Changed += OnSettingsChanged;
        }

        private void OnDestroy() => GameSettingsService.Changed -= OnSettingsChanged;

        private void OnSettingsChanged(GameSettingsSnapshot value)
        {
            showInputPrompts = value.Accessibility.ShowInputPrompts;
            Render();
        }

        /// <summary>绑定当前会话，按钮只转成领域流程请求。</summary>
        /// <param name="owner">当前场景或 null 解除。</param>
        public void Bind(WallSqueezeWorld owner)
        {
            world = owner;
            pause.onClick.RemoveListener(Pause);
            retry.onClick.RemoveListener(Retry);
            if (owner != null)
            {
                pause.onClick.AddListener(Pause);
                retry.onClick.AddListener(Retry);
                Render();
            }
        }

        /// 回显当前规则与设备提示。
        public void Render()
        {
            if (world?.Simulation == null)
            {
                return;
            }
            stats.text = $"{world.LevelIndex + 1:00}/{world.LevelCount:00}   {world.LevelTitle}";
            remaining.text = $"剩余 <size=150%>{world.Simulation.Remaining:00}</size>";
            residents.text = $"保护 <size=150%>{world.Simulation.ResidentsAlive:00}</size>";
            clock.gameObject.SetActive(world.HasTimeLimit);
            if (world.HasTimeLimit)
            {
                clock.text = $"倒计时 {Mathf.CeilToInt(world.Simulation.RemainingSeconds):00}";
                clock.color = world.Simulation.RemainingSeconds <= 10 ? new Color(208f / 255, 48f / 255, 42f / 255) : Color.black;
            }
            bool playing = !world.Paused && world.Simulation.Result == WallSqueezeResult.Playing;
            pause.interactable = playing;
            retry.interactable = playing;
            hint.gameObject.SetActive(showInputPrompts || world.LastCrushCount > 1);
            hint.text = world.LastCrushCount > 1 ? $"一夹 {world.LastCrushCount} 只！" : world.Device switch
            {
                InputDeviceKind.Touch => "单指拖墙 · 保护蓝色住户",
                _ => BuildBindingHint()
            };
        }

        private string BuildBindingHint()
        {
            var router = world.Input.Router;
            string previous = InputBindingDisplay.Get(router.FindAction("Interaction/PreviousGroup"), out _);
            string next = InputBindingDisplay.Get(router.FindAction("Interaction/NextGroup"), out _);
            string move = InputBindingDisplay.Get(router.FindAction(world.Device == InputDeviceKind.Gamepad
                ? "Interaction/PadNavigate" : "Interaction/Navigate"), out _);
            string pauseText = LanguageService.IsInitialized ? LanguageService.Get("Input.Pause") : "暂停";
            return previous + " / " + next + " 选墙 · " + move + " 移动 · "
                + InputBindingDisplay.Label(router.FindAction(world.Device == InputDeviceKind.Gamepad
                    ? "Interaction/Pause" : "Interaction/Back"), pauseText);
        }

        private void Pause() => world?.Dispatch(WallSqueezeCommand.Pause);
        private void Retry() => world?.Dispatch(WallSqueezeCommand.Retry);
    }
}
