namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [Module("KagitKart")]
    [UIBind("KagitKartOnlineView")]
    public partial class KagitKartOnlineView : View
    {
        private KagitKartWorld world;

        protected override void OnGameObjectInitialize()
        {
            Button_CreateRoom.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.CreateRoom));
            Button_JoinRoom.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.JoinRoom));
            Button_Back.onClick.AddListener(Back);
            UIMenuScope_Sheet.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>显示页面前交付当前纸艺卡丁车世界。</summary>
        /// <param name="owner">当前 Demo 宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            float fit = Mathf.Min(1, (RectTransform_Viewport.rect.width - 48) / 1120, (RectTransform_Viewport.rect.height - 48) / 920);
            RectTransform_SheetGroup.localScale = Vector3.one * Mathf.Max(0.01f, fit);
            KagitKartMenuPaperMotion_Viewport.Play(owner.MenuEntranceDelay, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(Button_CreateRoom.gameObject);

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            KagitKartMenuPaperMotion_Viewport.SetReducedMotion(world.ReducedMotion);
            Button_CreateRoom.interactable = Button_JoinRoom.interactable = Button_Back.interactable = !world.Busy;
            TextMeshProUGUI_Status.text = world.Feedback ?? "";
        }

        private void Back() => world?.Dispatch(KagitKartCommand.PlayModes);

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Back;
            world = null;
        }
    }
}
