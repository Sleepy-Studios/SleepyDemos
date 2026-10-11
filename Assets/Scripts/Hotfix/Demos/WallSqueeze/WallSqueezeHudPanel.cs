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
        private InputPromptGroup prompts;

        /// <summary>View 交付正式索引里的提示组，保留 HUD 原纸面布局。</summary>
        /// <param name="group">已经制作并纳入 UIBind 的四行提示。</param>
        public void ConfigurePrompts(InputPromptGroup group)
        {
            prompts = group != null ? group : throw new System.ArgumentNullException(nameof(group));
            prompts.Get("Previous").SetCaptionKey("Input.PreviousWall");
            prompts.Get("Next").SetCaptionKey("Input.NextWall");
            prompts.Get("Move").SetCaptionKey("Input.Move");
            prompts.Get("Pause").SetCaptionKey("Input.Pause");
        }

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
            bool crush = world.LastCrushCount > 1;
            bool touch = InputDeviceState.PromptKind == InputDeviceKind.Touch;
            hint.gameObject.SetActive(crush || showInputPrompts && touch);
            if (crush)
            {
                hint.text = $"一夹 {world.LastCrushCount} 只！";
            }
            else
            {
                LanguageService.SetText(hint, "Input.Wall.TouchHint");
            }
            prompts.SetVisible(showInputPrompts && !touch && !crush);
            BindPrompt("Previous", "Interaction/PreviousGroup");
            BindPrompt("Next", "Interaction/NextGroup");
            BindPrompt("Move", InputDeviceState.PromptKind == InputDeviceKind.Gamepad ? "Interaction/PadNavigate" : "Interaction/Navigate");
            BindPrompt("Pause", "Interaction/Pause");
        }

        private void BindPrompt(string key, string path)
        {
            var prompt = prompts.Get(key);
            var action = world.Input.Router.FindAction(path);
            if (prompt.Action != action)
            {
                prompt.Bind(action);
            }
        }

        private void Pause() => world?.Dispatch(WallSqueezeCommand.Pause);
        private void Retry() => world?.Dispatch(WallSqueezeCommand.Retry);
    }
}
