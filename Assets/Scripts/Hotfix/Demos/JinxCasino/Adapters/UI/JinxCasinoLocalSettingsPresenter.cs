using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Adapters.Persistence;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
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
        [SerializeField] private JinxCasinoTouchPad movePad;
        [SerializeField] private JinxCasinoTouchPad lookPad;
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
            Unbind(); owner = controller;
            if (!positionsCaptured && movePad != null && lookPad != null)
            {
                originalMove = PadLayout.Capture((RectTransform)movePad.transform); originalLook = PadLayout.Capture((RectTransform)lookPad.transform);
                positionsCaptured = true;
            }
            owner.LoadLocalPreferences(store); saved = owner.LocalPreferences;
            owner.LocalPreferencesChanged += ApplyPadLayout; owner.Changed += RefreshEmotes; ApplyPadLayout();
            Listen(pcSensitivity, _ => Preview()); Listen(touchSensitivity, _ => Preview()); Listen(volume, _ => Preview());
            Listen(muted, _ => Preview()); Listen(leftHanded, _ => Preview());
            Listen(saveButton, Save); Listen(defaultsButton, Defaults); Listen(playEmoteButton, PlayEmote);
            Listen(settingsCloseButton, () => { CancelPreview(); close?.Invoke(); }); Listen(emoteCloseButton, () => close?.Invoke());
        }

        /// 释放订阅并撤销未保存预览，不写用户偏好或成长档案。
        public void Unbind()
        {
            CancelPreview();
            if (owner != null) { owner.LocalPreferencesChanged -= ApplyPadLayout; owner.Changed -= RefreshEmotes; }
            foreach (var remove in removeListeners) remove(); removeListeners.Clear();
            owner = null; draft = saved = null; emoteKey = null; emoteIds.Clear();
            if (positionsCaptured && movePad != null && lookPad != null)
            { originalMove.Apply((RectTransform)movePad.transform); originalLook.Apply((RectTransform)lookPad.transform); }
        }

        /// 展示进入面板时的偏好副本；修改只预览，必须明确保存。
        public void ShowSettings()
        {
            if (owner == null) return;
            CancelPreview(); saved = owner.LocalPreferences; draft = saved.Copy(); previewing = false;
            settingsPanel.SetActive(true); emotePanel.SetActive(false);
            settingsFeedback.text = owner.LocalPreferencesWarning ?? "输入倍率保持原基础灵敏度；关闭会撤销未保存预览。";
            RenderDraft();
        }

        /// 展示Profile已解锁表情，当前模型实际播放成功后才提示成功。
        public void ShowEmotes()
        {
            CancelPreview(); settingsPanel.SetActive(false); emotePanel.SetActive(true); emoteKey = null; RefreshEmotes();
        }

        private void RenderDraft()
        {
            pcSensitivity.SetValueWithoutNotify(draft.PcLookMultiplier); touchSensitivity.SetValueWithoutNotify(draft.TouchLookMultiplier);
            volume.SetValueWithoutNotify(draft.Volume); muted.SetIsOnWithoutNotify(draft.Muted); leftHanded.SetIsOnWithoutNotify(draft.LeftHanded);
            RenderValues();
        }
        private void RenderValues()
        {
            pcLabel.text = "鼠标视角灵敏度 · " + (draft.PcLookMultiplier * 100).ToString("0") + "%";
            touchLabel.text = "触控视角灵敏度 · " + (draft.TouchLookMultiplier * 100).ToString("0") + "%";
            volumeLabel.text = "本场景音量 · " + (draft.Volume * 100).ToString("0") + "%";
        }
        private void Preview()
        {
            if (owner == null || draft == null || !settingsPanel.activeInHierarchy) return;
            draft.PcLookMultiplier = pcSensitivity.value; draft.TouchLookMultiplier = touchSensitivity.value;
            draft.Volume = volume.value; draft.Muted = muted.isOn; draft.LeftHanded = leftHanded.isOn;
            previewing = true; owner.ApplyLocalPreferences(draft); RenderValues();
            settingsFeedback.text = "正在预览；点击保存后保留，下次进入仍生效。";
        }
        private void Save()
        {
            if (owner == null || draft == null) return;
            try { owner.SaveLocalPreferences(draft); saved = owner.LocalPreferences; previewing = false; settingsFeedback.text = "本地设置已保存。"; }
            catch (Exception exception) { settingsFeedback.text = "设置未保存：" + exception.Message; }
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
            if (owner == null || !emotePanel.activeInHierarchy) return;
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
            emoteDropdown.SetValueWithoutNotify(Math.Max(0, emoteIds.IndexOf(previous))); playEmoteButton.interactable = emoteIds.Count > 0;
            emoteFeedback.text = "只对自己的纸片角色播放动作，持续约两秒；不会更改装备或小游戏。";
        }
        private void PlayEmote()
        {
            int index = emoteDropdown.value; if (owner == null || index < 0 || index >= emoteIds.Count) return;
            bool played = owner.TryPlayLocalEmote(emoteIds[index]);
            emoteFeedback.text = played ? "正在播放：" + emoteDropdown.options[index].text : "当前本地角色尚未装配，表情未播放。";
        }
        private void Listen(Slider slider, UnityAction<float> callback)
        { slider.onValueChanged.AddListener(callback); removeListeners.Add(() => { if (slider != null) slider.onValueChanged.RemoveListener(callback); }); }
        private void Listen(Toggle toggle, UnityAction<bool> callback)
        { toggle.onValueChanged.AddListener(callback); removeListeners.Add(() => { if (toggle != null) toggle.onValueChanged.RemoveListener(callback); }); }
        private void Listen(Button button, Action callback)
        {
            UnityAction listener = () => { if (owner != null) owner.GetComponent<JinxCasinoAudioDirector>()?.PlayUiClick(); callback(); }; button.onClick.AddListener(listener);
            removeListeners.Add(() => { if (button != null) button.onClick.RemoveListener(listener); });
        }
        private void OnDisable() => CancelPreview();
        private void OnDestroy() => Unbind();
    }
}
