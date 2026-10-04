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
    /// 保存的 HUD Prefab 上的显示与按钮绑定，不创建运行时 UI 层级。
    public sealed class HowToFishHudPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI stats;
        [SerializeField] private TextMeshProUGUI focus;
        [SerializeField] private TextMeshProUGUI notice;
        [SerializeField] private TextMeshProUGUI fishingStatus;
        [SerializeField] private TextMeshProUGUI controls;
        [SerializeField] private TextMeshProUGUI equipmentSlots;
        [SerializeField] private Image tension;
        [SerializeField] private GameObject fishingPanel;
        [SerializeField] private GameObject bossPanel;
        [SerializeField] private TextMeshProUGUI bossStatus;
        [SerializeField] private Image bossHealth;
        [SerializeField] private Image bossEscape;
        [SerializeField] private GameObject radarPanel;
        [SerializeField] private TextMeshProUGUI radarStatus;
        [SerializeField] private TextMeshProUGUI[] radarIslands;
        [SerializeField] private GameObject menu;
        [SerializeField] private TextMeshProUGUI menuTitle;
        [SerializeField] private Button[] slots;
        [SerializeField] private TextMeshProUGUI[] slotLabels;
        [SerializeField] private Button[] newGames;
        [SerializeField] private Button resume;
        [SerializeField] private Button save;
        [SerializeField] private Button back;
        [SerializeField] private TextMeshProUGUI journal;
        [SerializeField] private Button outfitButton;
        [SerializeField] private GameObject outfitPanel;
        [SerializeField] private Button[] outfitCards;
        [SerializeField] private Image[] outfitIcons;
        [SerializeField] private TextMeshProUGUI[] outfitLabels;
        [SerializeField] private TextMeshProUGUI outfitDetails;
        [SerializeField] private Button outfitWear;
        [SerializeField] private Button outfitBack;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject settingsPanel;
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
        private bool settingsOpen;
        private HowToFishLocalPreferences settingsSnapshot;
        private GameObject settingsReturnFocus;
        private MenuInputScope settingsMenu;
        private readonly List<(string Path, int Binding, string Label)> bindingRows = new();
        private int settingsGroup;
        private int bindingPage;
        private bool waitSettingsCancelRelease;
        private int settingsCancelFrame;
        private HowToFishWorld world;
        private float refreshAt;
        private int confirmNewSlot = -1;
        private bool menuWasVisible;
        private bool outfitOpen;
        private int selectedOutfit;
        private GameObject outfitReturnFocus;
        private readonly StringBuilder journalText = new StringBuilder();
        private readonly StringBuilder equipmentText = new StringBuilder();

        private void Awake()
        {
            // 注册一次，晚绑定 world；既支持鼠标也支持手柄 Submit，不轮询界面选择。
            foreach (var button in GetComponentsInChildren<Button>(true)) button.onClick.AddListener(PlayUiSound);
            for (int i = 0; i < slots.Length; i++)
            {
                int index = i;
                slots[i].onClick.AddListener(() => ContinueSlot(index));
                newGames[i].onClick.AddListener(() => NewSlot(index));
            }
            resume.onClick.AddListener(() => { if (world?.ShowEnding == true) world.ContinueAfterEnding(); else world?.SetPaused(false); });
            save.onClick.AddListener(() => world?.Save());
            back.onClick.AddListener(() => world?.ReturnToHub());
            settingsButton.onClick.AddListener(OpenSettings);
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
            for (int i = 0; i < settingsGroups.Length; i++)
            {
                int group = i;
                settingsGroups[i].onClick.AddListener(() => SelectSettingsGroup(group));
            }
            for (int i = 0; i < bindingButtons.Length; i++)
            {
                int row = i;
                bindingButtons[i].onClick.AddListener(() => RebindSetting(row));
            }
            outfitButton.onClick.AddListener(OpenOutfits);
            outfitBack.onClick.AddListener(CloseOutfits);
            outfitWear.onClick.AddListener(WearOutfit);
            for (int i = 0; i < outfitCards.Length; i++)
            {
                int index = i;
                outfitCards[i].onClick.AddListener(() => SelectOutfit(index));
            }
        }

        /// <summary>绑定当前世界，重复显示不重复注册监听。</summary>
        /// <param name="owner">当前 Demo 世界。</param>
        public void Bind(HowToFishWorld owner)
        {
            Unbind();
            world = owner;
            world.Changed += OnChanged;
            menuWasVisible = false;
            confirmNewSlot = -1;
            outfitOpen = false;
            for (int i = 0; i < outfitIcons.Length; i++)
                outfitIcons[i].sprite = world.Catalog.FindOutfit(HowToFishOutfitCatalog.All[i].Id).Icon;
            Refresh();
        }

        /// 界面隐藏或销毁时解除世界引用。
        public void Unbind()
        {
            try
            {
                if (world != null)
                {
                    world.Changed -= OnChanged;
                    try
                    {
                        world.Input.CancelRebind();
                        if (settingsOpen && settingsSnapshot != null) world.ApplyPreferences(settingsSnapshot);
                    }
                    finally { world.SetEditingSettings(false); }
                }
            }
            catch (Exception exception) { Debug.LogException(exception, this); }
            finally
            {
                settingsOpen = false;
                settingsSnapshot = null;
                settingsMenu?.Dispose(); settingsMenu = null;
                if (settingsPanel != null) settingsPanel.SetActive(false);
                world = null;
                outfitOpen = false;
                if (outfitPanel != null) outfitPanel.SetActive(false);
            }
        }

        private void Update()
        {
            if (world == null) return;
            if (settingsOpen)
            {
                settingsMenu?.Update();
                UpdateSettingsCancel();
                return;
            }
            if (Time.unscaledTime < refreshAt) return;
            refreshAt = Time.unscaledTime + 0.05f;
            if (outfitOpen)
            {
                var selected = EventSystem.current?.currentSelectedGameObject;
                for (int i = 0; i < outfitCards.Length; i++)
                    if (i != selectedOutfit && outfitCards[i].gameObject == selected) { SelectOutfit(i); break; }
                return;
            }
            RefreshGameplay();
        }

        private void PlayUiSound() => world?.PlayUiSound();

        private void OnChanged() => Refresh();

        private void Refresh()
        {
            if (world == null) return;
            bool show = world.IsPaused || !world.HasSession;
            if (!world.HasSession || !world.IsPaused || world.ShowJournal) outfitOpen = false;
            menu.SetActive(show && !outfitOpen && !settingsOpen);
            settingsPanel.SetActive(settingsOpen);
            settingsButton.gameObject.SetActive(!world.ShowJournal);
            outfitPanel.SetActive(show && outfitOpen && !settingsOpen);
            outfitButton.gameObject.SetActive(world.HasSession && !world.ShowJournal);
            if (outfitOpen) RefreshOutfits();
            menuTitle.text = world.ShowEnding ? "航程完成\n<size=22>已经返回大陆 · 可继续探索</size>" : world.HasSession ? "已暂停" : "渔力全开\n<size=22>单人航程</size>";
            for (int i = 0; i < slots.Length; i++)
            {
                slots[i].gameObject.SetActive(!world.HasSession);
                newGames[i].gameObject.SetActive(!world.HasSession);
                if (world.HasSession) continue;
                var saved = world.InspectSlot(i);
                slotLabels[i].text = saved.Status switch
                {
                    HowToFishLoadStatus.Empty => $"存档 {i + 1} · 新的航程",
                    HowToFishLoadStatus.Ready => $"存档 {i + 1} · 继续  ${saved.Data.money}",
                    HowToFishLoadStatus.RecoveryAvailable => $"存档 {i + 1} · 恢复备份",
                    HowToFishLoadStatus.UnsupportedVersion => $"存档 {i + 1} · 版本不支持",
                    _ => $"存档 {i + 1} · 数据损坏"
                };
                slots[i].interactable = saved.Status != HowToFishLoadStatus.Corrupt && saved.Status != HowToFishLoadStatus.UnsupportedVersion;
                newGames[i].interactable = saved.Status == HowToFishLoadStatus.Ready || saved.Status == HowToFishLoadStatus.Empty;
            }
            resume.gameObject.SetActive(world.HasSession);
            save.gameObject.SetActive(world.HasSession && !world.ShowEnding);
            journal.gameObject.SetActive(world.ShowJournal);
            if (world.ShowJournal)
            {
                menuTitle.text = "鱼类图鉴";
                journalText.Clear();
                foreach (var creature in world.Catalog.Creatures)
                {
                    if (!creature.IsJournalEntry) continue;
                    bool found = world.Session.State.discoveredCreatures.Contains(creature.Id);
                    bool defeated = world.Session.State.defeatedCreatures.Contains(creature.Id);
                    journalText.Append(found ? creature.DisplayName : "未知生物").Append(defeated ? "  ✓" : "  —");
                    if (world.Session.State.defeatedDripCreatures.Contains(creature.Id)) journalText.Append("  珍品");
                    journalText.AppendLine();
                }
                journal.text = journalText.ToString();
            }
            if (show && !menuWasVisible && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(world.HasSession ? resume.gameObject : slots[0].gameObject);
            menuWasVisible = show;
            RefreshGameplay();
        }

        private void OpenSettings()
        {
            if (world == null || settingsOpen || world.ShowJournal || world.HasSession && !world.IsPaused) return;
            settingsReturnFocus = EventSystem.current?.currentSelectedGameObject;
            settingsSnapshot = world.GetPreferences();
            outfitOpen = false;
            settingsOpen = true;
            world.SetEditingSettings(true);
            try
            {
                settingsMenu = new MenuInputScope(EventSystem.current, selectionRoot: settingsPanel.transform);
                settingsStatus.text = "选择按键开始修改；按 Esc 或手柄取消键返回。";
                settingsContent.interactable = true; settingsContent.alpha = 1;
                settingsCaptureCancel.gameObject.SetActive(false);
                FillSettingsValues();
                SelectSettingsGroup(world.Input.IsGamepad ? 1 : 0);
                Refresh();
                settingsMenu.SetContext(GameplayInputContext.Menu, settingsGroups[settingsGroup].gameObject);
                WaitSettingsCancelRelease();
            }
            catch (Exception exception)
            {
                settingsMenu?.Dispose(); settingsMenu = null;
                settingsOpen = false; settingsSnapshot = null;
                world.SetEditingSettings(false);
                Refresh();
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
            Refresh();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(settingsReturnFocus != null && settingsReturnFocus.activeInHierarchy
                    ? settingsReturnFocus : settingsButton.gameObject);
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
            for (int i = 0; i < settingsGroups.Length; i++)
                settingsGroups[i].image.color = i == group ? new Color(.34f, .52f, .57f) : new Color(.20f, .32f, .36f);
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

        private void OpenOutfits()
        {
            if (world == null || settingsOpen || !world.HasSession || !world.IsPaused || world.ShowJournal) return;
            outfitReturnFocus = EventSystem.current?.currentSelectedGameObject;
            selectedOutfit = 0;
            for (int i = 0; i < HowToFishOutfitCatalog.All.Count; i++)
                if (HowToFishOutfitCatalog.All[i].Id == world.SelectedOutfitId) { selectedOutfit = i; break; }
            outfitOpen = true;
            Refresh();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(outfitCards[selectedOutfit].gameObject);
        }

        private void CloseOutfits()
        {
            outfitOpen = false;
            Refresh();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(outfitReturnFocus != null && outfitReturnFocus.activeInHierarchy ? outfitReturnFocus : outfitButton.gameObject);
        }

        private void SelectOutfit(int index)
        {
            selectedOutfit = index;
            RefreshOutfits();
        }

        private void WearOutfit()
        {
            if (world == null || !outfitOpen) return;
            if (!world.TrySelectOutfit(HowToFishOutfitCatalog.All[selectedOutfit].Id)) return;
            RefreshOutfits();
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(outfitCards[selectedOutfit].gameObject);
        }

        private void RefreshOutfits()
        {
            for (int i = 0; i < outfitCards.Length; i++)
            {
                var outfit = HowToFishOutfitCatalog.All[i];
                bool unlocked = HowToFishOutfitCatalog.IsUnlocked(outfit.Id, world.Session.State.unlockedOutfits);
                bool equipped = outfit.Id == world.SelectedOutfitId;
                outfitLabels[i].text = outfit.Name + (equipped ? " · 已穿戴" : unlocked ? "" : " · 未解锁");
                outfitIcons[i].color = unlocked ? Color.white : new Color(.42f, .42f, .42f, 1);
                // 锁定卡片仍可聚焦，查看条件；只有穿戴操作按解锁状态禁用。
                outfitCards[i].image.color = i == selectedOutfit ? new Color(.34f, .52f, .57f) : new Color(.16f, .24f, .28f);
            }
            var selected = HowToFishOutfitCatalog.All[selectedOutfit];
            bool canWear = HowToFishOutfitCatalog.IsUnlocked(selected.Id, world.Session.State.unlockedOutfits);
            outfitDetails.text = selected.Name + "\n" + selected.UnlockHint +
                (selected.Id == world.SelectedOutfitId ? "\n当前穿戴" : canWear ? "\n已解锁" : "\n尚未解锁");
            outfitWear.interactable = canWear && selected.Id != world.SelectedOutfitId;
        }

        private void RefreshGameplay()
        {
            notice.text = world.Notice ?? "";
            bool radarVisible = world.HasSession && !world.IsPaused && world.Player.Equipment?.Kind == HowToFishItemKind.Radar;
            radarPanel.SetActive(radarVisible);
            if (radarVisible)
            {
                foreach (var dot in radarIslands) dot.gameObject.SetActive(false);
                radarStatus.text = "雷达 · 朝向 " + world.Player.transform.eulerAngles.y.ToString("0") + "°";
                foreach (var island in world.Islands)
                {
                    if (island.Index > world.Session.State.unlockedIsland || island.Index >= radarIslands.Length) continue;
                    var offset = island.Position - world.Player.transform.position;
                    var local = Quaternion.Euler(0, -world.Player.transform.eulerAngles.y, 0) * offset;
                    var dot = radarIslands[island.Index];
                    dot.gameObject.SetActive(true);
                    dot.rectTransform.anchoredPosition = Vector2.ClampMagnitude(new Vector2(local.x, local.z) * .055f, 70);
                    dot.text = "● " + island.DisplayName;
                    if (island.Index == world.Session.State.unlockedIsland)
                        radarStatus.text += $"\n{island.DisplayName} {new Vector2(offset.x, offset.z).magnitude:0}m";
                }
            }
            var boss = world.ActiveBoss;
            bool fighting = boss != null && !world.IsPaused;
            bossPanel.SetActive(fighting);
            if (fighting)
            {
                var creature = boss.Item;
                bossHealth.fillAmount = creature.Health / creature.Creature.Health;
                bossEscape.fillAmount = boss.EscapeFraction;
                bossStatus.text = $"{creature.Creature.DisplayName}  {creature.Health:0} / {creature.Creature.Health:0} · {boss.Hint}";
            }
            if (!world.HasSession)
            { stats.text = ""; focus.text = ""; controls.text = ""; equipmentSlots.text = ""; fishingPanel.SetActive(false); return; }
            var state = world.Session.State;
            stats.text = $"${state.money}\n生命 {state.health:0}   饱食 {state.hunger:0}";
            if (state.poisonSeconds > 0) stats.text += $"  <color=#B1D75B>中毒 {state.poisonSeconds:0.0}s</color>";
            if (state.burningSeconds > 0) stats.text += $"  <color=#FFA45B>燃烧 {state.burningSeconds:0.0}s</color>";
            if (world.Player.Equipment?.Kind == HowToFishItemKind.Rod) stats.text += "\n鱼饵：" + world.Player.Fishing.BaitName;
            var heldFood = world.Player.HeldItem;
            var heldDynamite = heldFood != null ? heldFood.GetComponent<HowToFishDynamite>() : null;
            if (heldDynamite != null && heldDynamite.IsArmed)
                stats.text += $"\n<color=#FF815B>炸药引信 {heldDynamite.RemainingFuse:0.0}s · 立即投掷</color>";
            else if (world.Player.Equipment?.Kind == HowToFishItemKind.Explosive)
                stats.text += $"\n炸药 ×{world.Session.Count(world.Player.Equipment.Id)}";
            if (heldFood != null && world.Player.CanEat)
                stats.text += $"\n{heldFood.Creature?.DisplayName ?? world.Catalog.FindItem(heldFood.DefinitionId)?.DisplayName} · " +
                    (heldFood.IsDrip ? "<color=#FF7777>D</color><color=#FFDD66>r</color><color=#77EE99>i</color><color=#77BBFF>p</color> · " : "") +
                    (heldFood.IsBurnt ? "烧焦" : heldFood.Cooking >= .45f ? "熟成" : heldFood.IsCooked ? "加热中" : "生") +
                    (heldFood.Creature == null ? "" : $" · {heldFood.Weight:0.##} kg  ${heldFood.SaleValue}");
            if (world.Player.EatingProgress > 0) stats.text += $"\n进食 {world.Player.EatingProgress:P0}";
            if (world.Player.Equipment?.Kind == HowToFishItemKind.Gun)
                stats.text += $"\n{world.Player.Equipment.DisplayName}  {world.Player.Ammo}/{world.Player.AmmoCapacity}" +
                    (world.Player.IsReloading ? " · 换弹中" : "");
            focus.text = world.FocusText();
            equipmentText.Clear();
            for (int i = 0; i < world.Session.EquipmentCapacity; i++)
            {
                string id = world.Session.State.equipmentSlots[i];
                bool selected = !string.IsNullOrEmpty(id) && world.Player.Equipment?.Id == id;
                if (selected) equipmentText.Append("<color=#FFD98B>");
                equipmentText.Append('[').Append(i + 1).Append("] ").Append(world.Catalog.FindItem(id)?.DisplayName ?? "空").Append("   ");
                if (selected) equipmentText.Append("</color>");
            }
            var unstored = world.Session.UnstoredEquipment;
            if (unstored != null) equipmentText.Append("手持未收纳：").Append(world.Catalog.FindItem(unstored.id).DisplayName);
            else if (world.Player.Equipment == null) equipmentText.Append("空手");
            equipmentSlots.text = world.IsPaused ? "" : equipmentText.ToString();
            controls.text = world.IsPaused ? "" : world.Player.IsDriving
                ? $"{world.Input.BindingLabel("Move")} 航行   {(world.Input.IsGamepad ? "右摇杆" : "鼠标")} 视角   {world.Input.BindingLabel("Interact")} 离开驾驶位   {world.Input.BindingLabel("Pause")} 暂停"
                : $"{world.Input.BindingLabel("Interact")} 交互   {world.Input.BindingLabel("Throw")} 投掷   " +
                  $"{world.Input.BindingLabel("Next")} 换装备   {world.Input.BindingLabel("Journal")} 图鉴   {world.Input.BindingLabel("Pause")} 暂停";
            if (!world.IsPaused && !world.Player.IsDriving && !world.Player.CanEat && world.Player.Equipment?.Kind == HowToFishItemKind.Gun)
                controls.text = $"{world.Input.BindingLabel("Use")} 开火   {world.Input.BindingLabel("Alternate")} 瞄准   {world.Input.BindingLabel("Reload")} 换弹   " + controls.text;
            if (!world.IsPaused && !world.Player.IsDriving) controls.text += "   " + world.Input.BindingLabel("Holster") + " 收纳/空手";
            if (!world.IsPaused && (world.Player.IsDriving || HowToFishSkinCatalog.Supports(world.Player.Equipment?.Id)))
                controls.text += "   " + world.Input.BindingLabel("ChangeSkin") + " 更换皮肤";
            if (!world.IsPaused && !world.Player.IsDriving && world.Player.CanEat)
                controls.text = "按住 " + world.Input.BindingLabel("Use") + " 进食   " + controls.text;
            if (!world.IsPaused && !world.Player.IsDriving && world.Player.HeldItem == null && world.Player.Equipment?.Kind == HowToFishItemKind.Explosive)
                controls.text = world.Input.BindingLabel("Use") + " 点燃投出（3秒）   " + controls.text;
            var fishing = world.Player.Fishing.State;
            fishingPanel.SetActive(!world.IsPaused && fishing.IsActive);
            bool pullBack = world.Player.Equipment?.Id == "FishingRod";
            tension.fillAmount = fishing.Phase == HowToFishFishingPhase.Charging ? fishing.Charge : pullBack ? fishing.Progress : fishing.Tension;
            tension.color = fishing.Tension > 0.75f ? new Color(0.95f, 0.24f, 0.12f) : new Color(0.94f, 0.76f, 0.24f);
            fishingStatus.text = fishing.Phase switch
            {
                HowToFishFishingPhase.Charging => "松手抛竿",
                HowToFishFishingPhase.Flying => "抛竿",
                HowToFishFishingPhase.Waiting => world.Player.Equipment?.Id == "FishingRod"
                    ? "按住 " + world.Input.BindingLabel("Use") + " 慢收，吸引鱼咬钩" : "等待鱼讯…",
                HowToFishFishingPhase.Bite => world.Player.Equipment?.Id == "FishingRod" ? "咬钩！松开后再按下收线" : "咬钩！按下收线",
                HowToFishFishingPhase.Reeling => pullBack ? $"连按收线 {fishing.Progress:P0}" : $"收线 {fishing.Progress:P0} · 松手降低张力",
                _ => ""
            };
        }

        private void ContinueSlot(int index)
        {
            var saved = world.InspectSlot(index);
            world.StartSlot(index, saved.Status == HowToFishLoadStatus.Empty, saved.Status == HowToFishLoadStatus.RecoveryAvailable);
        }

        private void NewSlot(int index)
        {
            if (world.InspectSlot(index).Status == HowToFishLoadStatus.Ready && confirmNewSlot != index)
            { confirmNewSlot = index; world.Notify("再次点击此槽的“重开”，确认覆盖当前航程；上一份进度会保留为备份。"); return; }
            world.StartSlot(index, true);
        }

        private void OnDestroy() => Unbind();
    }
}
