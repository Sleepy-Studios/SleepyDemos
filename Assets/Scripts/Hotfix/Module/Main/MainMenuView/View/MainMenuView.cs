namespace Hotfix
{
    using Core.Runtime;
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
    }
}
