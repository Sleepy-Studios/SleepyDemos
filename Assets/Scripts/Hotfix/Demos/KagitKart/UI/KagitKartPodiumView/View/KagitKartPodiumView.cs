namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [Module("KagitKart")]
    [UIBind("KagitKartPodiumView")]
    public partial class KagitKartPodiumView : View
    {
        private KagitKartWorld world;

        protected override void OnGameObjectInitialize()
        {
            Button_Menu.onClick.AddListener(Menu);
            ColorUtility.TryParseHtmlString("#d9483b", out var red);
            TextMeshProUGUI_Title.color = red;
            ColorUtility.TryParseHtmlString("#e3a623", out var gold);
            TextMeshProUGUI_PlaceNumber1.color = gold;
            ColorUtility.TryParseHtmlString("#7d97b8", out var blue);
            TextMeshProUGUI_PlaceNumber2.color = blue;
            ColorUtility.TryParseHtmlString("#c8693a", out var copper);
            TextMeshProUGUI_PlaceNumber3.color = copper;
            UIMenuScope_Viewport.Canceled += Menu;
            BindData<KagitKartData>(_ => Button_Menu.interactable = world != null && !world.Busy);
        }

        /// <summary>显示完整杯赛排名；前三纸卡与原作颁奖镜头对应。</summary>
        /// <param name="owner">已完成四场大奖赛的场景宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            var standings = owner.GrandPrix.SortedStandings();
            int place = 0;
            for (int i = 0; i < standings.Count; i++)
            {
                if (standings[i].CharacterId == owner.CharacterId) place = i + 1;
            }
            TextMeshProUGUI_Title.text = place == 1 ? "奖杯属于你！" : place <= 3 ? "登上领奖台！" : "大奖赛已完成";
            TextMeshProUGUI_PlayerResult.text = place > 3 ? $"你获得第 {place} 名 · {standings[place - 1].Points} 分" : "";
            var names = new[] { TextMeshProUGUI_Driver1, TextMeshProUGUI_Driver2, TextMeshProUGUI_Driver3 };
            var points = new[] { TextMeshProUGUI_Points1, TextMeshProUGUI_Points2, TextMeshProUGUI_Points3 };
            for (int i = 0; i < 3; i++)
            {
                names[i].text = KagitKartCatalog.CharacterName(standings[i].CharacterId);
                points[i].text = standings[i].Points + " 分";
            }
            KagitKartMenuPaperMotion_Viewport.Play(0);
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(Button_Menu.gameObject);
        private void Menu() => world?.Dispatch(KagitKartCommand.Title);
        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Viewport.Canceled -= Menu;
            world = null;
        }
    }
}
