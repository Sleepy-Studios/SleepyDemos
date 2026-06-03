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
            EventDispatcher.AddEventListener(EventConst.MainOpenView,Test);
        }

        protected override void OnShow()
        {
            base.OnShow();
            EventDispatcher.TriggerEvent(EventConst.MainOpenView);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            EventDispatcher.RemoveEventListener(EventConst.MainOpenView,Test);
        }

        void Test()
        {
            Debug.LogError("主页面打开");
        }
    }
}
