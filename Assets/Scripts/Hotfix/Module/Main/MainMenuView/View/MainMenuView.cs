namespace Hotfix
{
    using Core.Runtime;
    using Cysharp.Threading.Tasks;
    using Hotfix.SceneManagement;
    using UnityEngine;

    [Module("Main")]
    [Mvc("MainMenuView")]
    public partial class MainMenuView : View
    {
        protected override void OnGameObjectInitialize()
        {
            EventDispatcher.AddEventListener(EventConst.MainOpenView, OnMainOpenView);
        }

        protected override void OnShow()
        {
            base.OnShow();
            GlobalData.Subscribe<UserData>(OnUserData, true);
            EventDispatcher.TriggerEvent(EventConst.MainOpenView);
        }

        protected override void OnHide()
        {
            base.OnHide();
            GlobalData.UnSubscribe<UserData>(OnUserData);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventDispatcher.RemoveEventListener(EventConst.MainOpenView, OnMainOpenView);
        }

        private void OnMainOpenView()
        {
            Debug.Log("[MainMenuView] 主页面打开。");
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
            //UIManager.Instance.Show<UIFrameworkValidationLauncherView>();
        }
    
        private void OnDroneFlightButtonClick()
        {
            OpenDroneFlightAsync().Forget();
        }

        private async UniTaskVoid OpenDroneFlightAsync()
        {
            var navigator = GameSceneNavigator.Instance;
            if (navigator == null)
            {
                Debug.LogError("[MainMenuView] 全局场景导航尚未初始化。");
                return;
            }

            if (Button_DroneFlightButton != null)
            {
                Button_DroneFlightButton.interactable = false;
            }
            var result = await navigator.SwitchAsync(GameSceneId.DroneFlight);
            if (result.Status is GameSceneSwitchStatus.Failed or GameSceneSwitchStatus.Busy)
            {
                if (Button_DroneFlightButton != null)
                {
                    Button_DroneFlightButton.interactable = true;
                }
                if (result.Status == GameSceneSwitchStatus.Failed)
                {
                    Debug.LogError($"[MainMenuView] 无法进入 DroneFlight：{result.Error}");
                }
            }
        }
    }
}
