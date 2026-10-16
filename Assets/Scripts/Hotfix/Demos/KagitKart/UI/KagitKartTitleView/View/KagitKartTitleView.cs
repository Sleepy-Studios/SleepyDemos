namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using Hotfix.SceneManagement;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [Module("KagitKart")]
    [UIBind("KagitKartTitleView")]
    public partial class KagitKartTitleView : View
    {
        private KagitKartWorld world;

        protected override void OnGameObjectInitialize()
        {
            Button_Hub.transform.parent.gameObject.SetActive(GameSceneNavigator.Instance?.StandaloneScene == null);
            Button_Open.onClick.AddListener(Open);
            Button_CoverHit.onClick.AddListener(Open);
            Button_Hub.onClick.AddListener(Back);
            UIMenuScope_Viewport.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>封面显示前绑定本场景，开书操作仍走公共菜单确认。</summary>
        /// <param name="owner">本 Demo 场景宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            KagitKartMenuPaperMotion_Viewport.Play(0, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(Button_Open.gameObject);

        private void Render()
        {
            if (world != null)
            {
                Button_Open.interactable = Button_CoverHit.interactable = Button_Hub.interactable = !world.Busy;
            }
        }

        private void Open() => world?.Dispatch(KagitKartCommand.PlayModes);
        private void Back()
        {
            if (GameSceneNavigator.Instance?.StandaloneScene == null)
            {
                world?.Dispatch(KagitKartCommand.Hub);
            }
        }

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Viewport.Canceled -= Back;
            world = null;
        }
    }
}
