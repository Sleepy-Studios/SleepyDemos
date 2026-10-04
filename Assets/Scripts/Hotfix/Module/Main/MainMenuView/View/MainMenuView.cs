namespace Hotfix
{
    using Core.Runtime;
    using Core.Runtime.Inputs;
    using Cysharp.Threading.Tasks;
    using Hotfix.SceneManagement;
    using SleepyStudios.LoopScroll;
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.InputSystem;

    [Module("Main")]
    [Mvc("MainMenuView")]
    public partial class MainMenuView : View
    {
        private readonly List<MainMenuDemoEntry> entries = new List<MainMenuDemoEntry>();
        private bool isEnteringDemo;
        private bool lastCanEnter;
        private string entryFeedback;
        private MenuInputScope menuInput;
        private LoopScrollMenuNavigation navigation;
        private int selectedIndex = -1;
        private IDisposable graphicsEntryLease;
        private bool settingsBorrowed;

        protected override void OnGameObjectInitialize()
        {
            const string art = "LoadResources/UI/Hall/Art/Gallery/";
            entries.Add(new MainMenuDemoEntry("drone_flight", "无人机飞行", "在训练场探索飞行手感。\n选择机型，开始你的飞行体验。", art + "DroneFlight", GameSceneId.DroneFlight, "起飞、穿越与精准操控"));
            entries.Add(new MainMenuDemoEntry("block_porters", "小小搬豆工", "操纵搬运机械，规划运送路线。\n在立体场地中完成搬运挑战。", art + "BlockPorters", GameSceneId.BlockPorters, "机械协作，巧妙搬运"));
            entries.Add(new MainMenuDemoEntry("jinx_casino", "倒霉蛋俱乐部", "走进复古俱乐部，直接操作机台。\n选择你的玩法，挑战自己的运气。", art + "JinxCasino", GameSceneId.JinxCasino, "下一次，会有好运吗？"));
            entries.Add(new MainMenuDemoEntry("dlss", "DLSS 实验室", "切换画质模式，比较画面与性能。\n体验实时渲染技术带来的差异。", art + "Dlss", GameSceneId.Dlss, "探索画质与性能的平衡"));
            entries.Add(new MainMenuDemoEntry("ui_validation", "UI 交互展台", "界面与导航的交互体验。\n展台正在准备中，敬请期待。", art + "UiValidation", null, "新的体验，即将开放"));
            entries.Add(new MainMenuDemoEntry("how_to_fish", "渔力全开", "驾船探索群岛，钓起奇异生物。\n单人冒险开发中。", "LoadResources/Demos/how_to_fish/Art/UI/HubPreview", GameSceneId.HowToFish, "出海垂钓，探索未知"));
            this.RegisterLoopScrollRect<MainMenuDemoItemView>(LoopScrollView_DemoList, OnDemoRectData);
            this.RegisterLoopScrollClick<MainMenuDemoItemView>(LoopScrollView_DemoList, OnDemoClick);
            this.RegisterLoopScrollItemHide<MainMenuDemoItemView>(LoopScrollView_DemoList, OnDemoItemHide);
            navigation = LoopScrollView_DemoList.GetComponent<LoopScrollMenuNavigation>();
            navigation.Initialize(LoopScrollView_DemoList);
            LoopScrollView_DemoList.SetTotalCount(entries, getItemKey: item => ((MainMenuDemoEntry)item).Key);
            SelectEntry(0);
            EventDispatcher.AddEventListener(EventConst.MainOpenView, OnMainOpenView);
        }

        protected override void OnShow()
        {
            base.OnShow();
            RefreshEntryControls();
            ReleaseMenuInput();
            menuInput = new MenuInputScope(EventSystem.current, selectionRoot: transform);
            menuInput.SetContext(GameplayInputContext.Menu, navigation.FirstSelection);
            graphicsEntryLease = GraphicsSettingsUI.SuppressEntry();
            InputDeviceState.Changed += RefreshInputHints;
            RefreshInputHints();
            UpdateMenuInputAsync(menuInput).Forget();
            GlobalData.Subscribe<UserData>(OnUserData, true);
            EventDispatcher.TriggerEvent(EventConst.MainOpenView);
        }

        protected override void OnHide()
        {
            base.OnHide();
            ReleaseMenuInput();
            ReleasePresentation();
            GlobalData.UnSubscribe<UserData>(OnUserData);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseMenuInput();
            ReleasePresentation();
            EventDispatcher.RemoveEventListener(EventConst.MainOpenView, OnMainOpenView);
        }

        private void OnMainOpenView() => Debug.Log("[MainMenuView] 主页面打开。");

        private async UniTask UpdateMenuInputAsync(MenuInputScope scope)
        {
            while (IsEnable && ReferenceEquals(menuInput, scope))
            {
                // OnShow 可能早于返回 Hub 的事务收尾，导航完成后自动恢复入口。
                if (lastCanEnter != CanEnterDemo) RefreshAvailability();
                if (settingsBorrowed && UIManager.Instance.Get<DlssSettingsView>()?.IsSettingsPanelOpen != true)
                {
                    settingsBorrowed = false;
                    graphicsEntryLease = GraphicsSettingsUI.SuppressEntry();
                }
                if (InputDeviceState.ActiveKind == InputDeviceKind.Touch) scope.Update();
                else scope.Update(!string.IsNullOrEmpty(entryFeedback) && Button_Start.interactable
                    ? Button_Start.gameObject : navigation.FirstSelection);
                // 焦点由公共列表导航维护，本页只同步当前玩法的展示数据。
                var selected = EventSystem.current?.currentSelectedGameObject;
                if (selected != null && selected.transform.IsChildOf(LoopScrollView_DemoList.transform))
                {
                    var cell = selected.GetComponentInParent<LoopCell>();
                    if (cell != null && cell.Context.IsCurrent) SelectEntry(cell.Context.Index);
                }
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);
            }
        }

        private void ReleaseMenuInput()
        {
            menuInput?.Dispose();
            menuInput = null;
        }

        private void OnUserData(UserData data)
        {
            if (data == null) { Debug.LogWarning("[MainMenuView] UserData is null."); return; }
            Debug.Log($"[MainMenuView] {data.GetHardwareSummary()}");
        }

        private void OnDemoRectData(MainMenuDemoItemView item, int index) => item.SetData(entries[index]);
        private void OnDemoItemHide(MainMenuDemoItemView item) => item.Clear();

        private void OnDemoClick(MainMenuDemoItemView item, int index)
        {
            SelectEntry(index);
            // 指针点击只选择；公共输入记录的键盘/手柄确认直接进入。
            if (InputDeviceState.ActiveDevice is Keyboard || InputDeviceState.ActiveDevice is Gamepad) OnStartClick();
        }

        private void SelectEntry(int index)
        {
            if (index < 0 || index >= entries.Count || selectedIndex == index) return;
            if (selectedIndex >= 0 && selectedIndex < entries.Count) entries[selectedIndex].IsSelected = false;
            selectedIndex = index;
            var entry = entries[index];
            entry.IsSelected = true;
            TextMeshProUGUI_Title.text = entry.Title;
            TextMeshProUGUI_Subtitle.text = entry.Subtitle;
            TextMeshProUGUI_Description.text = entry.Description;
            UIImageLoader_Hero.SetImage(entry.PreviewAddress, setNativeSize: false, isAsync: true);
            RefreshVisibleCards();
            RefreshPrimaryAction();
        }

        private bool CanEnterDemo => !isEnteringDemo && GameSceneNavigator.Instance?.IsTransitioning != true;

        private async UniTask EnterDemoAsync(GameSceneId target)
        {
            if (isEnteringDemo || !IsEnable) return;
            var navigator = GameSceneNavigator.Instance;
            if (navigator == null)
            {
                RefreshEntryControls("入口未就绪，请稍后重试");
                Debug.LogError("[MainMenuView] 全局场景导航尚未初始化。");
                return;
            }
            if (navigator.IsTransitioning) return;
            isEnteringDemo = true;
            RefreshEntryControls();
            string message = null;
            try
            {
                var result = await navigator.SwitchAsync(target);
                if (result.Status == GameSceneSwitchStatus.Failed)
                {
                    message = "进入失败，请重试";
                    Debug.LogError($"[MainMenuView] 无法进入 {target}：{result.Error}");
                }
            }
            catch (Exception exception)
            {
                message = "进入失败，请重试";
                Debug.LogError($"[MainMenuView] 无法进入 {target}：{exception}");
            }
            finally
            {
                isEnteringDemo = false;
                // Loading 会销毁原页面，失败回滚应刷新新实例。
                if (ReferenceEquals(GameSceneNavigator.Instance, navigator) &&
                    navigator.CurrentScene == GameSceneId.Hub && !navigator.IsTransitioning)
                {
                    var current = UIManager.Instance.Get<MainMenuView>();
                    if (current?.IsEnable == true)
                    {
                        current.SelectEntry(current.entries.FindIndex(entry => entry.SceneId == target));
                        current.RefreshEntryControls(message);
                    }
                }
            }
        }

        private void RefreshEntryControls(string message = null)
        {
            entryFeedback = message;
            RefreshAvailability();
            if (!string.IsNullOrEmpty(message)) EventSystem.current?.SetSelectedGameObject(null);
        }

        private void RefreshAvailability()
        {
            lastCanEnter = CanEnterDemo;
            foreach (var entry in entries)
            {
                entry.CanEnter = lastCanEnter && entry.SceneId.HasValue;
                entry.CanBrowse = lastCanEnter;
            }
            RefreshVisibleCards();
            RefreshPrimaryAction();
            TextMeshProUGUI_Status.text = entryFeedback ?? (lastCanEnter ? "选择一个玩法，开始体验" : "正在准备体验…");
            TextMeshProUGUI_Status.color = string.IsNullOrEmpty(entryFeedback)
                ? new Color(.39f, .43f, .42f) : new Color(.72f, .22f, .16f);
        }

        private void OnStartClick()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count) return;
            var entry = entries[selectedIndex];
            if (entry.CanEnter && entry.SceneId.HasValue && CanEnterDemo) EnterDemoAsync(entry.SceneId.Value).Forget();
        }

        private void OnSettingsClick()
        {
            var settings = UIManager.Instance.Get<DlssSettingsView>();
            if (settings == null || settings.State != ViewState.Visible) return;
            graphicsEntryLease?.Dispose();
            graphicsEntryLease = null;
            settingsBorrowed = true;
            settings.SetSettingsPanelOpen(true);
        }

        private void RefreshPrimaryAction()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count) return;
            var entry = entries[selectedIndex];
            Button_Start.interactable = entry.CanEnter && CanEnterDemo;
            Button_Settings.interactable = CanEnterDemo;
            TextMeshProUGUI_StartLabel.text = !entry.SceneId.HasValue ? "未开放" :
                !CanEnterDemo ? "加载中…" : string.IsNullOrEmpty(entryFeedback) ? "开始体验" : "重试";
        }

        private void RefreshVisibleCards()
        {
            // 业务选中态不改变列表身份；直接刷新已绑定 Item，避免回收整排卡片和重复加载主体图。
            var views = LoopScrollView_DemoList.ItemViews();
            for (int index = 0; index < entries.Count; index++)
            {
                var cell = LoopScrollView_DemoList.GetVisibleCell(index);
                if (cell != null && cell.Context.IsCurrent && views.TryGetItemView(cell, out var item) &&
                    item is MainMenuDemoItemView card) card.SetData(entries[index]);
            }
        }

        private void ReleasePresentation()
        {
            InputDeviceState.Changed -= RefreshInputHints;
            graphicsEntryLease?.Dispose();
            graphicsEntryLease = null;
            settingsBorrowed = false;
        }

        private void RefreshInputHints()
        {
            if (Application.isMobilePlatform || InputDeviceState.ActiveKind == InputDeviceKind.Touch)
                TextMeshProUGUI_Hints.text = "滑动选择玩法 · 点击开始体验";
            else if (InputDeviceState.PromptKind == InputDeviceKind.Gamepad)
            {
                string confirm = InputDeviceState.PromptStyle == GamepadStyle.PlayStation ? "×" :
                    InputDeviceState.PromptStyle == GamepadStyle.Switch ? "B" : "A";
                TextMeshProUGUI_Hints.text = $"方向键 / 摇杆 选择    {confirm} 确认";
            }
            else TextMeshProUGUI_Hints.text = "← → 选择    Enter 确认";
        }
    }
}
