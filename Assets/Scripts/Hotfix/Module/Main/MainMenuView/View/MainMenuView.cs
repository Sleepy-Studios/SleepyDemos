namespace Hotfix
{
    using Core.Runtime;
    using Core.Runtime.Inputs;
    using Cysharp.Threading.Tasks;
    using Hotfix.SceneManagement;
    using System;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.EventSystems;

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

        protected override void OnGameObjectInitialize()
        {
            entries.Add(new MainMenuDemoEntry("drone_flight", "无人机飞行", "起飞、穿越与精准操控，在训练场探索飞行手感。", "LoadResources/UI/Hall/Art/DroneFlightPreview", GameSceneId.DroneFlight));
            entries.Add(new MainMenuDemoEntry("block_porters", "小小搬豆工", "操纵搬运机械，在立体场地中完成运送挑战。", "LoadResources/UI/Hall/Art/BlockPortersPreview", GameSceneId.BlockPorters));
            entries.Add(new MainMenuDemoEntry("jinx_casino", "倒霉蛋俱乐部", "走进复古俱乐部，直接操作机台，挑战自己的运气。", "LoadResources/UI/Hall/Art/JinxCasinoPreview", GameSceneId.JinxCasino));
            entries.Add(new MainMenuDemoEntry("dlss", "DLSS 实验室", "比较画质与性能，体验实时渲染技术的差异。", "LoadResources/UI/Hall/Art/DlssPreview", GameSceneId.Dlss));
            entries.Add(new MainMenuDemoEntry("ui_validation", "UI 交互展台", "界面与导航的交互体验，即将开放。", "LoadResources/UI/Hall/Art/UiValidationPreview", null));
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
            menuInput = new MenuInputScope(EventSystem.current);
            menuInput.SetContext(GameplayInputContext.Menu, navigation.FirstSelection);
            UpdateMenuInputAsync(menuInput).Forget();
            GlobalData.Subscribe<UserData>(OnUserData, true);
            EventDispatcher.TriggerEvent(EventConst.MainOpenView);
        }

        protected override void OnHide()
        {
            base.OnHide();
            ReleaseMenuInput();
            GlobalData.UnSubscribe<UserData>(OnUserData);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            ReleaseMenuInput();
            EventDispatcher.RemoveEventListener(EventConst.MainOpenView, OnMainOpenView);
        }

        private void OnMainOpenView() => Debug.Log("[MainMenuView] 主页面打开。");

        private async UniTask UpdateMenuInputAsync(MenuInputScope scope)
        {
            while (IsEnable && ReferenceEquals(menuInput, scope))
            {
                // OnShow 可能早于返回 Hub 的事务收尾，导航完成后自动恢复入口。
                if (lastCanEnter != CanEnterDemo) RefreshAvailability();
                scope.Update(navigation.FirstSelection);
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
            var entry = entries[index];
            if (entry.CanEnter && entry.SceneId.HasValue && CanEnterDemo) EnterDemoAsync(entry.SceneId.Value).Forget();
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
                    if (current?.IsEnable == true) current.RefreshEntryControls(message);
                }
            }
        }

        private void RefreshEntryControls(string message = null)
        {
            entryFeedback = message;
            RefreshAvailability();
        }

        private void RefreshAvailability()
        {
            lastCanEnter = CanEnterDemo;
            foreach (var entry in entries) entry.CanEnter = lastCanEnter && entry.SceneId.HasValue;
            LoopScrollView_DemoList.RefreshCells();
            TextMeshProUGUI_Status.text = entryFeedback ?? (lastCanEnter ? "选择一张卡片，开始体验" : "正在准备体验…");
        }
    }
}
