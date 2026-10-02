namespace Hotfix
{
    using Core.Runtime;
    using Core.Runtime.Inputs;
    using Cysharp.Threading.Tasks;
    using Hotfix.SceneManagement;
    using System;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    [Module("Main")]
    [Mvc("MainMenuView")]
    public partial class MainMenuView : View
    {
        private bool isEnteringDemo;
        private string defaultTitle;
        private MenuInputScope menuInput;

        protected override void OnGameObjectInitialize()
        {
            defaultTitle = TextMeshProUGUI_Title.text;
            Button_UIFrameworkValidationButton.interactable = false;
            Button_UIFrameworkValidationButton.GetComponentInChildren<Text>().text = "UI 验证（未开放）";
            EventDispatcher.AddEventListener(EventConst.MainOpenView, OnMainOpenView);
        }

        protected override void OnShow()
        {
            base.OnShow();
            RefreshEntryControls();
            ReleaseMenuInput();
            menuInput = new MenuInputScope(EventSystem.current);
            menuInput.SetContext(GameplayInputContext.Menu, Button_DroneFlightButton.gameObject);
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

        private void OnMainOpenView()
        {
            Debug.Log("[MainMenuView] 主页面打开。");
        }

        private async UniTask UpdateMenuInputAsync(MenuInputScope scope)
        {
            // 页面只负责驱动公共作用域，不读取键位或模拟指针；旧页面的循环不能驱动新作用域。
            while (IsEnable && ReferenceEquals(menuInput, scope))
            {
                scope.Update();
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
            if (data == null)
            {
                Debug.LogWarning("[MainMenuView] UserData is null.");
                return;
            }

            Debug.Log($"[MainMenuView] {data.GetHardwareSummary()}");
        }

        private void OnUIFrameworkValidationButtonClick()
        {
            // 入口尚未接入场景导航，保持不可用，也不响应误触回调。
        }

        private void OnDroneFlightButtonClick()
        {
            EnterDemoAsync(GameSceneId.DroneFlight).Forget();
        }

        private void OnDlssButtonClick()
        {
            EnterDemoAsync(GameSceneId.Dlss).Forget();
        }

        private void OnBlockPortersButtonClick()
        {
            EnterDemoAsync(GameSceneId.BlockPorters).Forget();
        }

        private void OnJinxCasinoButtonClick()
        {
            EnterDemoAsync(GameSceneId.JinxCasino).Forget();
        }

        private async UniTask EnterDemoAsync(GameSceneId target)
        {
            if (isEnteringDemo || !IsEnable) return;
            var navigator = GameSceneNavigator.Instance;
            if (navigator == null)
            {
                RefreshEntryControls("入口未就绪\n请稍后重试");
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
                    message = "进入失败\n请重试";
                    Debug.LogError($"[MainMenuView] 无法进入 {target}：{result.Error}");
                }
            }
            catch (Exception exception)
            {
                message = "进入失败\n请重试";
                Debug.LogError($"[MainMenuView] 无法进入 {target}：{exception}");
            }
            finally
            {
                isEnteringDemo = false;
                // Loading 会销毁原 Hub；回滚后应更新新实例，不能再操作本实例的已释放控件。
                if (ReferenceEquals(GameSceneNavigator.Instance, navigator) &&
                    navigator.CurrentScene == GameSceneId.Hub && !navigator.IsTransitioning)
                {
                    var currentMenu = UIManager.Instance.Get<MainMenuView>();
                    if (currentMenu?.IsEnable == true) currentMenu.RefreshEntryControls(message);
                }
            }
        }

        private void RefreshEntryControls(string message = null)
        {
            bool canEnter = !isEnteringDemo && GameSceneNavigator.Instance?.IsTransitioning != true;
            Button_DroneFlightButton.interactable = canEnter;
            Button_DlssButton.interactable = canEnter;
            Button_BlockPortersButton.interactable = canEnter;
            Button_JinxCasinoButton.interactable = canEnter;
            TextMeshProUGUI_Title.text = message ?? defaultTitle;
        }
    }
}
