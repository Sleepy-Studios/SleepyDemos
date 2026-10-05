using Hotfix.JinxCasino;
using Hotfix.JinxCasino.UI;
using Hotfix.JinxCasino.Presentation;
using Core.Runtime;
using Core.Runtime.Inputs;
using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Persistence;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix
{
    /// 本机输入与音量设置，控件和触控区域均由保存HUD引用。
    [Module("JinxCasino")]
    [UIBind("JinxCasinoSettingsView")]
    public sealed partial class JinxCasinoSettingsView : View
    {
        private GameObject settingsPanel;
        private Slider pcSensitivity;
        private Slider touchSensitivity;
        private Slider volume;
        private Toggle muted;
        private Toggle leftHanded;
        private Slider gamepadLookMultiplier;
        private Slider gamepadDeadzone;
        private Slider gamepadLookRate;
        private Slider gamepadMaximum;
        private Toggle gamepadInvertY;
        private Toggle rumbleEnabled;
        private Slider rumbleStrength;
        private TMP_Text gamepadLookLabel;
        private TMP_Text gamepadDeadzoneLabel;
        private TMP_Text gamepadLookRateLabel;
        private TMP_Text gamepadMaximumLabel;
        private TMP_Text rumbleStrengthLabel;
        private GameObject pointerPage;
        private GameObject gamepadPage;
        private GameObject audioPage;
        private Button pointerTabButton;
        private Button gamepadTabButton;
        private Button audioTabButton;
        private Selectable pointerFirstSelection;
        private Selectable gamepadFirstSelection;
        private Selectable audioFirstSelection;
        private TMP_Text pcLabel;
        private TMP_Text touchLabel;
        private TMP_Text volumeLabel;
        private TMP_Text settingsFeedback;
        private Button saveButton;
        private Button defaultsButton;
        private Button settingsCloseButton;
        private readonly List<Action> removeListeners = new List<Action>();
        private JinxCasinoController owner;
        protected override void OnGameObjectInitialize()
        {
            settingsPanel = RectTransform_SettingsPanel.gameObject;
            pcSensitivity = Slider_PcSensitivity;
            touchSensitivity = Slider_TouchSensitivity;
            volume = Slider_Volume;
            muted = Toggle_Muted;
            leftHanded = Toggle_LeftHanded;
            gamepadLookMultiplier = Slider_LookMultiplier;
            gamepadDeadzone = Slider_Deadzone;
            gamepadLookRate = Slider_LookRate;
            gamepadMaximum = Slider_Maximum;
            gamepadInvertY = Toggle_InvertY;
            rumbleEnabled = Toggle_RumbleEnabled;
            rumbleStrength = Slider_RumbleStrength;
            gamepadLookLabel = TextMeshProUGUI_LookMultiplierLabel;
            gamepadDeadzoneLabel = TextMeshProUGUI_DeadzoneLabel;
            gamepadLookRateLabel = TextMeshProUGUI_LookRateLabel;
            gamepadMaximumLabel = TextMeshProUGUI_MaximumLabel;
            rumbleStrengthLabel = TextMeshProUGUI_RumbleStrengthLabel;
            pointerPage = RectTransform_PointerPage.gameObject;
            gamepadPage = RectTransform_GamepadPage.gameObject;
            audioPage = RectTransform_AudioPage.gameObject;
            pointerTabButton = Button_PointerTab;
            gamepadTabButton = Button_GamepadTab;
            audioTabButton = Button_AudioTab;
            pointerFirstSelection = Slider_PcSensitivity;
            gamepadFirstSelection = Slider_LookMultiplier;
            audioFirstSelection = Slider_Volume;
            pcLabel = TextMeshProUGUI_PcSensitivityLabel;
            touchLabel = TextMeshProUGUI_TouchSensitivityLabel;
            volumeLabel = TextMeshProUGUI_VolumeLabel;
            settingsFeedback = TextMeshProUGUI_Feedback;
            saveButton = Button_Save;
            defaultsButton = Button_Defaults;
            settingsCloseButton = Button_Close;
            settingsTabs = UITab_SettingsPanel;
            settingsTabs.Register(OnTabSelected);
            Listen(pcSensitivity, _ => Preview()); Listen(touchSensitivity, _ => Preview()); Listen(volume, _ => Preview());
            Listen(muted, _ => Preview()); Listen(leftHanded, _ => Preview());
            Listen(gamepadLookMultiplier, _ => Preview()); Listen(gamepadDeadzone, _ => Preview());
            Listen(gamepadLookRate, _ => Preview()); Listen(gamepadMaximum, _ => Preview());
            Listen(gamepadInvertY, _ => Preview()); Listen(rumbleEnabled, _ => Preview()); Listen(rumbleStrength, _ => Preview());
            Listen(saveButton, Save); Listen(defaultsButton, Defaults);
            Listen(settingsCloseButton, CloseSettings);
            BindData<JinxCasinoData>(OnData);
            var relay = UICancelRelay_JinxCasinoSettingsView;
            Action cancel = () => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CancelWindow));
            relay.Canceled += cancel; AddBinding(() => relay.Canceled -= cancel);
        }
        public void SetData(JinxCasinoController controller) { owner = controller; ShowSettings(); }
        private GameObject first;
        private void OnData(JinxCasinoData value) { owner = value.Scene; if (first != FirstSelection) { first = FirstSelection; owner.UI.SetFirstSelection(first); } }
        public void CancelPreview() => GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CloseSettings));
        private JinxCasinoSettingsAction SendSettings(JinxCasinoSettingsOperation operation, CasinoLocalPreferences value = null)
        { var request = new JinxCasinoSettingsAction(owner, operation, value); GlobalData.Dispatch(request); return request; }
        protected override void OnHide() { SendSettings(JinxCasinoSettingsOperation.Cancel); owner = null; base.OnHide(); }
        protected override void OnDestroy()
        {
            if (owner != null) SendSettings(JinxCasinoSettingsOperation.Cancel);
            if (settingsTabs != null) settingsTabs.Unregister(OnTabSelected);
            foreach (var release in removeListeners) release(); removeListeners.Clear(); owner = null; base.OnDestroy();
        }
        private CasinoLocalPreferences saved => owner?.Data.SettingsSaved;
        private CasinoLocalPreferences draft => owner?.Data.SettingsDraft;


        private UITab settingsTabs;
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

        /// <summary>绑定本机场景；测试可注入独立偏好键。</summary>
        /// <param name="controller">音频和输入的宿主。</param>
        /// <param name="close">关闭后恢复原场地/菜单的模态导航。</param>
        /// <param name="store">为空使用本机正式偏好键。</param>


        /// 释放订阅并撤销未保存预览，不写用户偏好或成长档案。


        /// 展示进入面板时的偏好副本；修改只预览，必须明确保存。
        public void ShowSettings()
        {
            if (owner == null) return;
            SendSettings(JinxCasinoSettingsOperation.Begin);
            if (settingsPanel != null) settingsPanel.SetActive(true);
            SetFeedback(owner.Settings.Warning ?? "调整会立即预览；保存后保留，返回会撤销未保存的调整。");
            SetPage(0, false);
            RenderDraft();
        }

        /// 撤销未保存的预览并隐藏本面板，再交由所属HUD返回原菜单；不解除游戏暂停。

        public void CloseSettings() => CancelPreview();

        private void OnTabSelected(int page) => SetPage(page, true);
        private void SetPage(int page, bool select)
        {
            settingsTabs.SetIndex(page, false);
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
            var result = SendSettings(JinxCasinoSettingsOperation.Preview, candidate);
            if (!result.Success) { SetFeedback(result.Error); return false; }
            RenderValues();
            SetFeedback("正在预览；点击保存后保留，下次进入仍生效。");
            return true;
        }
        private void Save()
        {
            if (owner == null || !Preview()) return;
            var result = SendSettings(JinxCasinoSettingsOperation.Save);
            SetFeedback(result.Success ? "本地设置已保存。" : "设置未保存：" + result.Error);
        }
        private void Defaults() { SendSettings(JinxCasinoSettingsOperation.Preview, new CasinoLocalPreferences()); RenderDraft(); }

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


    }
}
