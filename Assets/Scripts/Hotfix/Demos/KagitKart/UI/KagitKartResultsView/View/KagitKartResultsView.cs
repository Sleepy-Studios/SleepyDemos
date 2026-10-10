namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using System.Text;

    [Module("KagitKart")]
    [UIBind("KagitKartResultsView")]
    public partial class KagitKartResultsView : View
    {
        private KagitKartWorld world;

        protected override void OnGameObjectInitialize()
        {
            Button_Next.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.NextRound));
            Button_Restart.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Restart));
            Button_Hub.onClick.AddListener(Hub);
            UIMenuScope_Sheet.Canceled += Hub;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>在显示前交付已结束的本场比赛。</summary>
        /// <param name="owner">当前场景宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            Render();
        }

        private void Hub() => world?.Dispatch(KagitKartCommand.Hub);

        private void Render()
        {
            if (world?.Session == null)
            {
                return;
            }
            TextMeshProUGUI_Title.text = world.GrandPrix?.Completed == true ? "大奖赛总成绩" : "比赛结算";
            var text = new StringBuilder();
            if (world.GrandPrix?.Completed == true)
            {
                int place = 1;
                foreach (var row in world.GrandPrix.SortedStandings())
                {
                    text.AppendLine($"{place++}   {KagitKartCatalog.CharacterName(row.CharacterId)}    {row.Points} 分");
                }
            }
            else
            {
                foreach (var row in world.Session.Race.Results)
                {
                    string points = world.GrandPrix == null ? "" : $"   +{row.Points} 分 · 共 {row.TotalPoints} 分";
                    text.AppendLine($"{row.Place}   {KagitKartCatalog.CharacterName(row.CharacterId)}    {KagitKartHudView.TimeText(row.Time)}{points}{(row.Estimated ? "（估算）" : "")}");
                }
            }
            text.AppendLine(world.Feedback);
            TextMeshProUGUI_Detail.text = text.ToString();
            Button_Next.transform.parent.gameObject.SetActive(world.GrandPrix?.RoundComplete == true && !world.GrandPrix.Completed);
            Button_Next.interactable = Button_Restart.interactable = Button_Hub.interactable = !world.Busy;
        }

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Hub;
            world = null;
        }
    }
}
