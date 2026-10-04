using Core.Runtime;
using System;
using System.Text;
using System.Collections.Generic;
using Core.Runtime.Inputs;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix.HowToFish
{
    /// 独立页面保存的业务显示与绑定，生命周期由对应 View 管理。
    public sealed class HowToFishSettingsPresenter : MonoBehaviour
    {
        [SerializeField] private CanvasGroup settingsContent;
        [SerializeField] private Slider mouseSensitivity;
        [SerializeField] private Slider gamepadSensitivity;
        [SerializeField] private Slider inputDeadZone;
        [SerializeField] private Toggle invertLook;
        [SerializeField] private TextMeshProUGUI[] settingsValues;
        [SerializeField] private Button[] settingsGroups;
        [SerializeField] private Button[] bindingButtons;
        [SerializeField] private TextMeshProUGUI[] bindingLabels;
        [SerializeField] private Button settingsPrevious;
        [SerializeField] private Button settingsNext;
        [SerializeField] private TextMeshProUGUI settingsPage;
        [SerializeField] private TextMeshProUGUI settingsStatus;
        [SerializeField] private Button settingsSave;
        [SerializeField] private Button settingsCancel;
        [SerializeField] private Button settingsDefaults;
        [SerializeField] private Button settingsCaptureCancel;
        [SerializeField] private UITab settingsTabs;
        private HowToFishWorld world;
        private bool settingsOpen;
        private HowToFishLocalPreferences settingsSnapshot;
        private MenuInputScope settingsMenu;
        private readonly List<(string Path, int Binding, string Label)> bindingRows = new();
        private int settingsGroup;
        private int bindingPage;
        private bool waitSettingsCancelRelease;
        private int settingsCancelFrame;
        private void Awake()
        {
            settingsSave.onClick.AddListener(SaveSettings);
            settingsCancel.onClick.AddListener(() => CloseSettings(false));
            settingsDefaults.onClick.AddListener(DefaultSettings);
            settingsCaptureCancel.onClick.AddListener(() => world?.Input.CancelRebind());
            mouseSensitivity.onValueChanged.AddListener(_ => PreviewSettings());
            gamepadSensitivity.onValueChanged.AddListener(_ => PreviewSettings());
            inputDeadZone.onValueChanged.AddListener(_ => PreviewSettings());
            invertLook.onValueChanged.AddListener(_ => PreviewSettings());
            settingsPrevious.onClick.AddListener(() => ChangeBindingPage(-1));
            settingsNext.onClick.AddListener(() => ChangeBindingPage(1));
            for (int i = 0; i < bindingButtons.Length; i++)
            { int row = i; bindingButtons[i].onClick.AddListener(() => RebindSetting(row)); }
            foreach (var button in GetComponentsInChildren<Button>(true)) button.onClick.AddListener(() => world?.PlayUiSound());
        }
        /// <summary>显示前绑定并创建设置预览快照。</summary>
        /// <param name="owner">当前世界。</param>
        public void Bind(HowToFishWorld owner)
        {
            Unbind(); world = owner;
            settingsTabs.Register(SelectSettingsGroup);
            settingsTabs.TrySelect = _ => !world.Input.IsRebinding;
            OpenSettings();
        }
        /// 隐藏或销毁时取消捕获并回滚未保存的预览。
        public void Unbind()
        {
            if (world != null)
            {
                world.Input.CancelRebind();
                try { if (settingsOpen && settingsSnapshot != null) world.ApplyPreferences(settingsSnapshot); }
                finally { world.SetEditingSettings(false); }
            }
            if (settingsTabs != null) { settingsTabs.Unregister(SelectSettingsGroup); settingsTabs.TrySelect = null; }
            settingsMenu?.Dispose(); settingsMenu = null;
            settingsOpen = false; settingsSnapshot = null; world = null;
        }
        private void Update()
        {
            if (world == null || !settingsOpen) return;
            settingsMenu?.Update(); UpdateSettingsCancel();
        }
        private void OnDestroy() => Unbind();
        private void OpenSettings()
        {
            if (world == null || settingsOpen || world.ShowJournal || world.HasSession && !world.IsPaused) return;
            settingsSnapshot = world.GetPreferences();
            settingsOpen = true;
            world.SetEditingSettings(true);
            try
            {
                settingsMenu = new MenuInputScope(EventSystem.current, selectionRoot: transform);
                settingsStatus.text = "选择按键开始修改；按 Esc 或手柄取消键返回。";
                settingsContent.interactable = true; settingsContent.alpha = 1;
                settingsCaptureCancel.gameObject.SetActive(false);
                FillSettingsValues();
                SelectSettingsGroup(world.Input.IsGamepad ? 1 : 0);
                settingsMenu.SetContext(GameplayInputContext.Menu, settingsGroups[settingsGroup].gameObject);
                WaitSettingsCancelRelease();
            }
            catch (Exception exception)
            {
                settingsMenu?.Dispose(); settingsMenu = null;
                settingsOpen = false; settingsSnapshot = null;
                world.SetEditingSettings(false);
                Debug.LogException(exception, this);
            }
        }

        private void FillSettingsValues()
        {
            var value = world.GetPreferences();
            mouseSensitivity.SetValueWithoutNotify(value.MouseSensitivity);
            gamepadSensitivity.SetValueWithoutNotify(value.GamepadSensitivity);
            inputDeadZone.SetValueWithoutNotify(value.DeadZone);
            invertLook.SetIsOnWithoutNotify(value.InvertY);
            settingsValues[0].text = $"鼠标灵敏度  {value.MouseSensitivity:F2}";
            settingsValues[1].text = $"手柄转向速度  {value.GamepadSensitivity:F0}";
            settingsValues[2].text = $"摇杆死区  {value.DeadZone:P0}";
        }

        private void PreviewSettings()
        {
            if (!settingsOpen || world.Input.IsRebinding) return;
            var value = world.GetPreferences();
            value.MouseSensitivity = mouseSensitivity.value;
            value.GamepadSensitivity = gamepadSensitivity.value;
            value.DeadZone = inputDeadZone.value;
            value.InvertY = invertLook.isOn;
            try { world.ApplyPreferences(value); settingsStatus.text = "已预览，保存后下次进入仍会保留。"; }
            catch (Exception exception) { settingsStatus.text = "无法应用设置：" + exception.Message; }
            FillSettingsValues();
        }

        private void DefaultSettings()
        {
            if (!settingsOpen || world.Input.IsRebinding) return;
            try
            {
                world.ApplyPreferences(new HowToFishLocalPreferences());
                FillSettingsValues(); RefreshBindingRows();
                settingsStatus.text = "已预览默认设置；保存生效，取消可恢复。";
            }
            catch (Exception exception) { settingsStatus.text = "无法恢复默认：" + exception.Message; }
        }

        private void SaveSettings()
        {
            if (!settingsOpen || world.Input.IsRebinding) return;
            try
            {
                if (world.SavePreferences()) CloseSettings(true);
                else settingsStatus.text = world.Notice ?? "保存失败，请重试。";
            }
            catch (Exception exception) { settingsStatus.text = "保存失败：" + exception.Message; }
        }

        private void CloseSettings(bool saved)
        {
            if (!settingsOpen) return;
            try
            {
                world.Input.CancelRebind();
                if (!saved) world.ApplyPreferences(settingsSnapshot);
            }
            catch (Exception exception)
            {
                settingsStatus.text = "无法恢复设置：" + exception.Message;
                return;
            }
            settingsOpen = false; settingsSnapshot = null;
            settingsMenu?.Dispose(); settingsMenu = null;
            world.SetEditingSettings(false);
            world.UI.CloseSettings();
        }

        private void SelectSettingsGroup(int group)
        {
            if (world.Input.IsRebinding) return;
            settingsGroup = group; bindingPage = 0;
            bindingRows.Clear();
            string map = group < 2 ? "Gameplay" : "Boat";
            string device = group % 2 == 0 ? "KeyboardMouse" : "Gamepad";
            foreach (string mapName in new[] { map, "UI" })
                foreach (var action in world.Input.Asset.FindActionMap(mapName, true).actions)
                {
                    string label = InputActionLabel(action.name);
                    if (label == null || mapName == "UI" && action.name != "Pause" && action.name != "Journal") continue;
                    for (int i = 0; i < action.bindings.Count; i++)
                    {
                        var binding = action.bindings[i];
                        if (binding.isComposite || !Array.Exists((binding.groups ?? "").Split(';'), value => value == device)) continue;
                        if (action.name == "Move" && !binding.isPartOfComposite) continue;
                        string suffix = binding.isPartOfComposite ? " · " + DirectionLabel(binding.name) : "";
                        string prefix = mapName == "UI" ? "通用 · " : mapName == "Boat" ? "驾驶 · " : "陆地 · ";
                        bindingRows.Add((mapName + "/" + action.name, i, prefix + label + suffix));
                    }
                }
            settingsTabs.SetIndex(group, false);
            RefreshBindingRows();
        }

        private void RefreshBindingRows()
        {
            int count = bindingButtons.Length;
            int pages = Mathf.Max(1, (bindingRows.Count + count - 1) / count);
            bindingPage = Mathf.Clamp(bindingPage, 0, pages - 1);
            for (int i = 0; i < count; i++)
            {
                int index = bindingPage * count + i;
                bindingButtons[i].gameObject.SetActive(index < bindingRows.Count);
                if (index >= bindingRows.Count) continue;
                var row = bindingRows[index];
                string key = world.Input.Asset.FindAction(row.Path, true).GetBindingDisplayString(row.Binding);
                bindingLabels[i].text = row.Label + "     [ " + key + " ]";
            }
            settingsPage.text = $"第 {bindingPage + 1} / {pages} 页";
            settingsPrevious.interactable = bindingPage > 0;
            settingsNext.interactable = bindingPage + 1 < pages;
        }

        private void ChangeBindingPage(int delta)
        {
            if (world.Input.IsRebinding) return;
            bindingPage += delta; RefreshBindingRows();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(bindingButtons[0].gameObject);
        }

        private void RebindSetting(int visibleIndex)
        {
            if (!settingsOpen || world.Input.IsRebinding) return;
            int index = bindingPage * bindingButtons.Length + visibleIndex;
            if (index >= bindingRows.Count) return;
            var row = bindingRows[index];
            try
            {
                world.Input.Rebind(row.Path, row.Binding, result =>
                {
                    if (!settingsOpen || world == null) return;
                    settingsStatus.text = result switch
                    {
                        HowToFishRebindResult.Completed => "按键已修改，保存后下次进入仍会保留。",
                        HowToFishRebindResult.Cancelled => "已取消按键修改。",
                        HowToFishRebindResult.Conflict => "该按键已用于其他操作，请选择其他按键。",
                        _ => "按键修改失败，请重试。"
                    };
                    try
                    {
                        if (result == HowToFishRebindResult.Completed)
                        {
                            var value = world.GetPreferences();
                            value.Bindings = world.Input.SaveBindings();
                            world.ApplyPreferences(value);
                        }
                        RefreshBindingRows();
                    }
                    catch (Exception exception)
                    {
                        settingsStatus.text = "无法应用按键修改，请重试或取消设置。";
                        Debug.LogException(exception, this);
                    }
                    finally { world.Input.SetMode(world.Player.IsDriving, true); }
                }, () =>
                {
                    settingsStatus.text = "请按新的按键（15秒）；Esc 或手柄取消键放弃。";
                    settingsContent.interactable = false; settingsContent.alpha = .45f;
                    // 键鼠捕获中的鼠标点击本身就是候选绑定，不能把取消按钮误绑定为左键。
                    settingsCaptureCancel.gameObject.SetActive(settingsGroup % 2 == 1);
                    settingsMenu.SetContext(GameplayInputContext.Interaction);
                }, () => EndSettingsCapture(visibleIndex));
            }
            catch (Exception exception)
            {
                EndSettingsCapture(visibleIndex);
                settingsStatus.text = "无法修改按键：" + exception.Message;
            }
        }

        private void EndSettingsCapture(int visibleIndex)
        {
            settingsContent.interactable = true; settingsContent.alpha = 1;
            settingsCaptureCancel.gameObject.SetActive(false);
            if (settingsOpen && settingsMenu != null)
                settingsMenu.SetContext(GameplayInputContext.Menu, bindingButtons[visibleIndex].gameObject);
            WaitSettingsCancelRelease();
        }

        private void WaitSettingsCancelRelease()
        {
            waitSettingsCancelRelease = true;
            settingsCancelFrame = Time.frameCount;
        }

        private void UpdateSettingsCancel()
        {
            if (world.Input.IsRebinding) return;
            if (waitSettingsCancelRelease)
            {
                if (Time.frameCount <= settingsCancelFrame) return;
                foreach (string name in new[] { "Pause", "Journal", "Cancel" })
                    foreach (var control in world.Input.Asset.FindAction("UI/" + name, true).controls)
                        if (control is ButtonControl button && button.isPressed) return;
                waitSettingsCancelRelease = false;
                return;
            }
            if (world.Input.Pressed("Cancel") || world.Input.Pressed("Pause") || world.Input.Pressed("Journal")) CloseSettings(false);
        }

        private static string DirectionLabel(string direction) => direction.ToLowerInvariant() switch
        { "up" => "前进", "down" => "后退", "left" => "向左", "right" => "向右", _ => direction };

        private static string InputActionLabel(string action) => action switch
        {
            "Move" => "移动", "Interact" => "交互", "Jump" => "跳跃", "Sprint" => "冲刺",
            "Use" => "使用 / 开火 / 进食", "Alternate" => "次要操作 / 瞄准", "Throw" => "投掷",
            "Reload" => "换弹", "Next" => "下一件装备", "Previous" => "上一件装备", "Bait" => "切换鱼饵",
            "Style" => "转动装备", "Holster" => "收纳装备", "ChangeSkin" => "更换皮肤",
            "Pause" => "暂停", "Journal" => "图鉴",
            "Slot1" => "装备栏 1", "Slot2" => "装备栏 2", "Slot3" => "装备栏 3", "Slot4" => "装备栏 4",
            "Slot5" => "装备栏 5", "Slot6" => "装备栏 6", "Slot7" => "装备栏 7", "Slot8" => "装备栏 8", _ => null
        };

    }
}
