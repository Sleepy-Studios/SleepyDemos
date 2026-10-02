using Core.Runtime.Inputs;
using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Adapters.Persistence;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    /// 本地设置与已解锁表情，控件/触控区域均由保存HUD引用，不创建运行时UI。
    public sealed class JinxCasinoLocalSettingsPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject emotePanel;
        [SerializeField] private Slider pcSensitivity;
        [SerializeField] private Slider touchSensitivity;
        [SerializeField] private Slider volume;
        [SerializeField] private Toggle muted;
        [SerializeField] private Toggle leftHanded;
        [SerializeField] private Slider gamepadLookMultiplier;
        [SerializeField] private Slider gamepadDeadzone;
        [SerializeField] private Slider gamepadLookRate;
        [SerializeField] private Slider gamepadMaximum;
        [SerializeField] private Toggle gamepadInvertY;
        [SerializeField] private Toggle rumbleEnabled;
        [SerializeField] private Slider rumbleStrength;
        [SerializeField] private TMP_Text gamepadLookLabel;
        [SerializeField] private TMP_Text gamepadDeadzoneLabel;
        [SerializeField] private TMP_Text gamepadLookRateLabel;
        [SerializeField] private TMP_Text gamepadMaximumLabel;
        [SerializeField] private TMP_Text rumbleStrengthLabel;
        [SerializeField] private GameObject pointerPage;
        [SerializeField] private GameObject gamepadPage;
        [SerializeField] private GameObject audioPage;
        [SerializeField] private Button pointerTabButton;
        [SerializeField] private Button gamepadTabButton;
        [SerializeField] private Button audioTabButton;
        [SerializeField] private Selectable pointerFirstSelection;
        [SerializeField] private Selectable gamepadFirstSelection;
        [SerializeField] private Selectable audioFirstSelection;
        [SerializeField] private TMP_Text pcLabel;
        [SerializeField] private TMP_Text touchLabel;
        [SerializeField] private TMP_Text volumeLabel;
        [SerializeField] private TMP_Text settingsFeedback;
        [SerializeField] private TMP_Text emoteFeedback;
        [SerializeField] private TMP_Dropdown emoteDropdown;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button defaultsButton;
        [SerializeField] private Button settingsCloseButton;
        [SerializeField] private Button emoteCloseButton;
        [SerializeField] private Button playEmoteButton;
        [SerializeField] private TouchInputPad movePad;
        [SerializeField] private TouchInputPad lookPad;
        private readonly List<Action> removeListeners = new List<Action>();
        private readonly List<string> emoteIds = new List<string>();
        private JinxCasinoController owner;
        private CasinoLocalPreferences saved;
        private CasinoLocalPreferences draft;
        private bool previewing;
        private bool positionsCaptured;
        private PadLayout originalMove;
        private PadLayout originalLook;
        private string emoteKey;
        private Action closeSettings;
        private int settingsPage;
        private bool HasPages => pointerPage != null && gamepadPage != null && audioPage != null;

        /// 当前设置页保存的首个控件，交给Core菜单作用域在按键释放后选中。
        public GameObject FirstSelection
        {
            get
            {
                var selectable = HasPages ? settingsPage == 1 ? gamepadFirstSelection : settingsPage == 2 ? audioFirstSelection : pointerFirstSelection : null;
                if (selectable != null && selectable.isActiveAndEnabled && selectable.IsInteractable()) return selectable.gameObject;
                if (pcSensitivity != null && pcSensitivity.isActiveAndEnabled && pcSensitivity.IsInteractable()) return pcSensitivity.gameObject;
                return settingsCloseButton != null ? settingsCloseButton.gameObject : null;
            }
        }

        private struct PadLayout
        {
            internal Vector2 Minimum, Maximum, Pivot, Position, Size;
            internal static PadLayout Capture(RectTransform rect) => new PadLayout { Minimum = rect.anchorMin, Maximum = rect.anchorMax,
                Pivot = rect.pivot, Position = rect.anchoredPosition, Size = rect.sizeDelta };
            internal void Apply(RectTransform rect)
            { rect.anchorMin = Minimum; rect.anchorMax = Maximum; rect.pivot = Pivot; rect.sizeDelta = Size; rect.anchoredPosition = Position; }
        }

        /// <summary>绑定本机场景；测试可注入独立偏好键。</summary>
        /// <param name="controller">音频、输入与本地Avatar的宿主。</param>
        /// <param name="close">关闭后恢复原场地/菜单的模态导航。</param>
        /// <param name="store">为空使用本机正式偏好键。</param>
        public void Bind(JinxCasinoController controller, Action close, CasinoLocalPreferencesStore store = null)
        {
            Unbind(); owner = controller; closeSettings = close;
            if (owner == null) return;
            if (!positionsCaptured && movePad != null && lookPad != null)
            {
                originalMove = PadLayout.Capture((RectTransform)movePad.transform); originalLook = PadLayout.Capture((RectTransform)lookPad.transform);
                positionsCaptured = true;
            }
            owner.LoadLocalPreferences(store); saved = owner.LocalPreferences;
            owner.LocalPreferencesChanged += ApplyPadLayout; owner.Changed += RefreshEmotes; ApplyPadLayout();
            Listen(pcSensitivity, _ => Preview()); Listen(touchSensitivity, _ => Preview()); Listen(volume, _ => Preview());
            Listen(muted, _ => Preview()); Listen(leftHanded, _ => Preview());
            Listen(gamepadLookMultiplier, _ => Preview()); Listen(gamepadDeadzone, _ => Preview());
            Listen(gamepadLookRate, _ => Preview()); Listen(gamepadMaximum, _ => Preview());
            Listen(gamepadInvertY, _ => Preview()); Listen(rumbleEnabled, _ => Preview()); Listen(rumbleStrength, _ => Preview());
            Listen(saveButton, Save); Listen(defaultsButton, Defaults); Listen(playEmoteButton, PlayEmote);
            Listen(settingsCloseButton, CloseSettings); Listen(emoteCloseButton, () => closeSettings?.Invoke());
            Listen(pointerTabButton, () => SetPage(0, true)); Listen(gamepadTabButton, () => SetPage(1, true)); Listen(audioTabButton, () => SetPage(2, true));
        }

        /// 释放订阅并撤销未保存预览，不写用户偏好或成长档案。
        public void Unbind()
        {
            CancelPreview();
            if (owner != null) { owner.LocalPreferencesChanged -= ApplyPadLayout; owner.Changed -= RefreshEmotes; }
            foreach (var remove in removeListeners) remove(); removeListeners.Clear();
            owner = null; draft = saved = null; closeSettings = null; emoteKey = null; emoteIds.Clear();
            if (positionsCaptured && movePad != null && lookPad != null)
            { originalMove.Apply((RectTransform)movePad.transform); originalLook.Apply((RectTransform)lookPad.transform); }
        }

        /// 展示进入面板时的偏好副本；修改只预览，必须明确保存。
        public void ShowSettings()
        {
            if (owner == null) return;
            CancelPreview(); saved = owner.LocalPreferences; draft = saved.Copy(); previewing = false;
            gameObject.SetActive(true); if (settingsPanel != null) settingsPanel.SetActive(true); if (emotePanel != null) emotePanel.SetActive(false);
            SetFeedback(owner.LocalPreferencesWarning ?? "调整会立即预览；保存后保留，返回会撤销未保存的调整。");
            SetPage(0, false);
            RenderDraft();
        }

        /// 撤销未保存的预览并隐藏本面板，再交由所属HUD返回原菜单；不解除游戏暂停。
        public void CloseSettings()
        {
            CancelPreview();
            if (settingsPanel != null) settingsPanel.SetActive(false);
            if (emotePanel != null) emotePanel.SetActive(false);
            gameObject.SetActive(false);
            closeSettings?.Invoke();
        }

        private void SetPage(int page, bool select)
        {
            settingsPage = page;
            if (HasPages)
            {
                pointerPage.SetActive(page == 0); gamepadPage.SetActive(page == 1); audioPage.SetActive(page == 2);
                var first = FirstSelection != null ? FirstSelection.GetComponent<Selectable>() : null;
                Selectable last = page == 1 ? rumbleEnabled : page == 2 ? muted : leftHanded;
                foreach (var button in new[] { saveButton, defaultsButton, settingsCloseButton })
                {
                    if (button == null) continue;
                    var navigation = button.navigation; navigation.selectOnUp = last != null ? last : first; button.navigation = navigation;
                }
                foreach (var button in new[] { pointerTabButton, gamepadTabButton, audioTabButton })
                {
                    if (button == null) continue;
                    var navigation = button.navigation; navigation.selectOnDown = first; button.navigation = navigation;
                }
            }
            if (select && settingsPanel != null && settingsPanel.activeInHierarchy && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(FirstSelection);
        }

        /// 展示Profile已解锁表情，当前模型实际播放成功后才提示成功。
        public void ShowEmotes()
        {
            if (owner == null || emotePanel == null) return;
            CancelPreview(); gameObject.SetActive(true); if (settingsPanel != null) settingsPanel.SetActive(false); emotePanel.SetActive(true); emoteKey = null; RefreshEmotes();
        }

        private void RenderDraft()
        {
            pcSensitivity?.SetValueWithoutNotify(draft.PcLookMultiplier); touchSensitivity?.SetValueWithoutNotify(draft.TouchLookMultiplier);
            volume?.SetValueWithoutNotify(draft.Volume); muted?.SetIsOnWithoutNotify(draft.Muted); leftHanded?.SetIsOnWithoutNotify(draft.LeftHanded);
            gamepadLookMultiplier?.SetValueWithoutNotify(draft.GamepadLookMultiplier); gamepadDeadzone?.SetValueWithoutNotify(draft.GamepadDeadzone);
            gamepadLookRate?.SetValueWithoutNotify(draft.GamepadLookDegreesPerSecond); gamepadMaximum?.SetValueWithoutNotify(draft.GamepadMaximum);
            gamepadInvertY?.SetIsOnWithoutNotify(draft.GamepadInvertY); rumbleEnabled?.SetIsOnWithoutNotify(draft.RumbleEnabled);
            rumbleStrength?.SetValueWithoutNotify(draft.RumbleStrength);
            RenderValues();
        }
        private void RenderValues()
        {
            SetLabel(pcLabel, "鼠标视角灵敏度 · " + Percent(draft.PcLookMultiplier));
            SetLabel(touchLabel, "触控视角灵敏度 · " + Percent(draft.TouchLookMultiplier));
            SetLabel(volumeLabel, "本场景音量 · " + Percent(draft.Volume));
            SetLabel(gamepadLookLabel, "手柄视角倍率 · " + Percent(draft.GamepadLookMultiplier));
            SetLabel(gamepadDeadzoneLabel, "摇杆死区 · " + Percent(draft.GamepadDeadzone));
            SetLabel(gamepadLookRateLabel, "手柄转向速度 · " + draft.GamepadLookDegreesPerSecond.ToString("0") + " 度/秒");
            SetLabel(gamepadMaximumLabel, "摇杆满量程 · " + Percent(draft.GamepadMaximum));
            SetLabel(rumbleStrengthLabel, "震动强度 · " + Percent(draft.RumbleStrength));
        }
        private static string Percent(float value) => (value * 100).ToString("0") + "%";
        private static void SetLabel(TMP_Text label, string text) { if (label != null) label.text = text; }
        private void SetFeedback(string text) => SetLabel(settingsFeedback, text);
        private bool Preview()
        {
            if (owner == null || draft == null || settingsPanel == null || !settingsPanel.activeInHierarchy) return false;
            var candidate = draft.Copy();
            if (pcSensitivity != null) candidate.PcLookMultiplier = pcSensitivity.value;
            if (touchSensitivity != null) candidate.TouchLookMultiplier = touchSensitivity.value;
            if (volume != null) candidate.Volume = volume.value;
            if (muted != null) candidate.Muted = muted.isOn;
            if (leftHanded != null) candidate.LeftHanded = leftHanded.isOn;
            if (gamepadLookMultiplier != null) candidate.GamepadLookMultiplier = gamepadLookMultiplier.value;
            if (gamepadDeadzone != null) candidate.GamepadDeadzone = gamepadDeadzone.value;
            if (gamepadLookRate != null) candidate.GamepadLookDegreesPerSecond = gamepadLookRate.value;
            if (gamepadMaximum != null) candidate.GamepadMaximum = gamepadMaximum.value;
            if (gamepadInvertY != null) candidate.GamepadInvertY = gamepadInvertY.isOn;
            if (rumbleEnabled != null) candidate.RumbleEnabled = rumbleEnabled.isOn;
            if (rumbleStrength != null) candidate.RumbleStrength = rumbleStrength.value;
            if (!candidate.IsValid) { SetFeedback("当前参数超出可用范围，请调整后再保存。"); return false; }
            draft = candidate; previewing = true; owner.ApplyLocalPreferences(draft); RenderValues();
            SetFeedback("正在预览；点击保存后保留，下次进入仍生效。");
            return true;
        }
        private void Save()
        {
            if (owner == null || draft == null) return;
            if (!Preview()) return;
            try { owner.SaveLocalPreferences(draft); saved = owner.LocalPreferences; previewing = false; SetFeedback("本地设置已保存。"); }
            catch (Exception exception) { SetFeedback("设置未保存：" + exception.Message); }
        }
        private void Defaults()
        { draft = new CasinoLocalPreferences(); RenderDraft(); Preview(); }
        private void CancelPreview()
        {
            if (!previewing) return;
            previewing = false; if (owner != null && saved != null) owner.ApplyLocalPreferences(saved);
        }
        private void ApplyPadLayout()
        {
            if (!positionsCaptured || owner == null || movePad == null || lookPad == null) return;
            movePad.ResetInput(); lookPad.ResetInput();
            bool left = owner.LocalPreferences.LeftHanded;
            (left ? originalLook : originalMove).Apply((RectTransform)movePad.transform);
            (left ? originalMove : originalLook).Apply((RectTransform)lookPad.transform);
        }
        private void RefreshEmotes()
        {
            if (owner == null || emotePanel == null || emoteDropdown == null || !emotePanel.activeInHierarchy) return;
            var data = owner.ProfileData;
            string key = data == null ? string.Empty : string.Join("|", data.UnlockedIds);
            if (key == emoteKey) return;
            string previous = emoteDropdown.value >= 0 && emoteDropdown.value < emoteIds.Count ? emoteIds[emoteDropdown.value] : data?.EquippedEmote;
            emoteKey = key; emoteIds.Clear(); var labels = new List<string>();
            foreach (var definition in CasinoProfileCatalog.Definitions)
            {
                if (definition.Kind != CasinoCosmeticKind.Emote || data == null || !data.UnlockedIds.Contains(definition.Id)) continue;
                emoteIds.Add(definition.Id); labels.Add(definition.Name);
            }
            if (labels.Count == 0) labels.Add("当前没有可播放的已解锁表情");
            emoteDropdown.ClearOptions(); emoteDropdown.AddOptions(labels);
            emoteDropdown.SetValueWithoutNotify(Math.Max(0, emoteIds.IndexOf(previous))); if (playEmoteButton != null) playEmoteButton.interactable = emoteIds.Count > 0;
            SetLabel(emoteFeedback, "只对自己的纸片角色播放动作，持续约两秒；不会更改装备或小游戏。");
        }
        private void PlayEmote()
        {
            if (emoteDropdown == null) return;
            int index = emoteDropdown.value; if (owner == null || index < 0 || index >= emoteIds.Count) return;
            bool played = owner.TryPlayLocalEmote(emoteIds[index]);
            SetLabel(emoteFeedback, played ? "正在播放：" + emoteDropdown.options[index].text : "当前本地角色尚未装配，表情未播放。");
        }
        private void Listen(Slider slider, UnityAction<float> callback)
        { if (slider == null) return; slider.onValueChanged.AddListener(callback); removeListeners.Add(() => { if (slider != null) slider.onValueChanged.RemoveListener(callback); }); }
        private void Listen(Toggle toggle, UnityAction<bool> callback)
        { if (toggle == null) return; toggle.onValueChanged.AddListener(callback); removeListeners.Add(() => { if (toggle != null) toggle.onValueChanged.RemoveListener(callback); }); }
        private void Listen(Button button, Action callback)
        {
            if (button == null) return;
            UnityAction listener = () => { if (owner != null) owner.GetComponent<JinxCasinoAudioDirector>()?.PlayUiClick(); callback(); }; button.onClick.AddListener(listener);
            removeListeners.Add(() => { if (button != null) button.onClick.RemoveListener(listener); });
        }
        private void OnDisable() => CancelPreview();
        private void OnDestroy() => Unbind();
    }
}
