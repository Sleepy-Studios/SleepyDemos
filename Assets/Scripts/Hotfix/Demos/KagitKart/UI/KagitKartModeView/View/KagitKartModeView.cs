namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    [Module("KagitKart")]
    [UIBind("KagitKartModeView")]
    public partial class KagitKartModeView : View
    {
        private KagitKartWorld world;
        private Button[] choices;

        protected override void OnGameObjectInitialize()
        {
            choices = new[] { Button_GrandPrix, Button_SingleRace, Button_TimeTrial };
            for (int i = 0; i < choices.Length; i++)
            {
                int mode = i;
                choices[i].onClick.AddListener(() => GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.ChooseMode, mode)));
            }
            Button_Back.onClick.AddListener(Back);
            Button_Hub.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Hub));
            Button_Settings.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Settings));
            Button_Controls.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Controls));
            UIMenuScope_Viewport.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>页面显示前绑定当前选赛来源。</summary>
        /// <param name="owner">本 Demo 场景宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            KagitKartMenuPaperMotion_Viewport.Play(owner.MenuEntranceDelay, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(choices[(int)world.Mode].gameObject);

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            foreach (var choice in choices)
            {
                choice.interactable = !world.Busy;
            }
            Button_Back.interactable = Button_Hub.interactable = Button_Settings.interactable = Button_Controls.interactable = !world.Busy;
            TextMeshProUGUI_Status.text = world.Feedback ?? "";
        }

        private void Back() => world?.Dispatch(KagitKartCommand.Title);

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Viewport.Canceled -= Back;
            world = null;
        }
    }
}
