namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using Hotfix.SceneManagement;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    [Module("KagitKart")]
    [UIBind("KagitKartClassView")]
    public partial class KagitKartClassView : View
    {
        private static readonly int[] Classes = { 80, 120, 200 };
        private KagitKartWorld world;
        private Button[] choices;

        protected override void OnGameObjectInitialize()
        {
            Button_Hub.transform.parent.gameObject.SetActive(GameSceneNavigator.Instance?.StandaloneScene == null);
            choices = new[] { Button_Class80, Button_Class120, Button_Class200 };
            for (int i = 0; i < choices.Length; i++)
            {
                int speed = Classes[i];
                choices[i].onClick.AddListener(() => GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.ChooseSpeedClass, speed)));
            }
            Button_Back.onClick.AddListener(Back);
            Button_Hub.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Hub));
            UIMenuScope_Viewport.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>显示前交付当前模式与速度组别。</summary>
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

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(choices[System.Array.IndexOf(Classes, world.SpeedClass)].gameObject);

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            KagitKartMenuPaperMotion_Viewport.SetReducedMotion(world.ReducedMotion);
            foreach (var choice in choices)
            {
                choice.interactable = !world.Busy;
            }
            Button_Back.interactable = Button_Hub.interactable = !world.Busy;
            TextMeshProUGUI_Status.text = world.Feedback ?? "选择纸张厚度，决定本场比赛的速度。";
        }

        private void Back() => world?.Dispatch(KagitKartCommand.Modes);

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Viewport.Canceled -= Back;
            world = null;
        }
    }
}
