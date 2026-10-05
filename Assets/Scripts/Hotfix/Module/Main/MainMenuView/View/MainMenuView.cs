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
    [UIBind("MainMenuView")]
    public partial class MainMenuView : View
    {
        private MainMenuData data;
        private List<MainMenuDemoEntry> entries;
        private bool lastCanEnter;
        private string entryFeedback => data.Feedback;
        private MenuInputScope menuInput;
        private LoopScrollMenuNavigation navigation;
        private int selectedIndex => data.SelectedIndex;

        protected override void OnGameObjectInitialize()
        {
            data = GlobalData.Get<MainMenuData>() ?? GlobalData.Add<MainMenuData>();
            entries = data.Entries;
            BindData<MainMenuData>(RenderMainData);
            BindData<UserData>(OnUserData);
            this.RegisterLoopScrollRect<MainMenuDemoItemView>(LoopScrollView_DemoList, OnDemoRectData);
            this.RegisterLoopScrollClick<MainMenuDemoItemView>(LoopScrollView_DemoList, OnDemoClick);
            this.RegisterLoopScrollItemHide<MainMenuDemoItemView>(LoopScrollView_DemoList, OnDemoItemHide);
            navigation = LoopScrollView_DemoList.GetComponent<LoopScrollMenuNavigation>();
            navigation.Initialize(LoopScrollView_DemoList);
            LoopScrollView_DemoList.SetTotalCount(entries, getItemKey: item => ((MainMenuDemoEntry)item).Key);
            EventDispatcher.AddEventListener(EventConst.MainOpenView, OnMainOpenView);
        }

        protected override void OnShow()
        {
            base.OnShow();
            RefreshEntryControls();
            ReleaseMenuInput();
            menuInput = new MenuInputScope(EventSystem.current, selectionRoot: transform);
            menuInput.SetContext(GameplayInputContext.Menu, navigation.FirstSelection);
            InputDeviceState.Changed += RefreshInputHints;
            RefreshInputHints();
            UpdateMenuInputAsync(menuInput).Forget();
            EventDispatcher.TriggerEvent(EventConst.MainOpenView);
        }

        protected override void OnHide()
        {
            base.OnHide();
            ReleaseMenuInput();
            ReleasePresentation();
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
                if (lastCanEnter != (!data.IsEntering && GameSceneNavigator.Instance?.IsTransitioning != true)) RefreshAvailability();
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

        private void SelectEntry(int index) => GlobalData.Dispatch(new MainMenuSelectAction(index));

        private void RenderMainData(MainMenuData value)
        {
            data = value;
            if ((uint)selectedIndex >= entries.Count) return;
            var entry = entries[selectedIndex];
            TextMeshProUGUI_Title.text = entry.Title;
            TextMeshProUGUI_Subtitle.text = entry.Subtitle;
            TextMeshProUGUI_Description.text = entry.Description;
            UIImageLoader_Hero.SetImage(entry.PreviewAddress, setNativeSize: false, isAsync: true);
            RenderAvailability();
        }

        private bool CanEnterDemo => data.CanEnter;



        private void RefreshEntryControls(string message = null)
        {
            GlobalData.Dispatch(new MainMenuFeedbackAction(message));
            if (!string.IsNullOrEmpty(message)) EventSystem.current?.SetSelectedGameObject(null);
        }

        private void RefreshAvailability() => GlobalData.Dispatch(new MainMenuAvailabilityAction(GameSceneNavigator.Instance?.IsTransitioning == true));

        private void RenderAvailability()
        {
            lastCanEnter = CanEnterDemo;
            RefreshVisibleCards();
            RefreshPrimaryAction();
            TextMeshProUGUI_Status.text = entryFeedback ?? (lastCanEnter ? "选择一个玩法，开始体验" : "正在准备体验…");
            TextMeshProUGUI_Status.color = string.IsNullOrEmpty(entryFeedback) ? new Color(.39f, .43f, .42f) : new Color(.72f, .22f, .16f);
        }

        private void OnStartClick()
        {
            if (selectedIndex < 0 || selectedIndex >= entries.Count) return;
            var entry = entries[selectedIndex];
            if (entry.CanEnter && entry.SceneId.HasValue && CanEnterDemo) GlobalData.Dispatch(new MainMenuEnterAction(entry.SceneId.Value));
        }

        private void OnSettingsClick() => UIManager.Instance.ShowAsync<DlssSettingsView>().Forget();

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
