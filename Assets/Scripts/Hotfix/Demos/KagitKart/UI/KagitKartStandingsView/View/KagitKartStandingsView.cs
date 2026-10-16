namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [Module("KagitKart")]
    [UIBind("KagitKartStandingsView")]
    public partial class KagitKartStandingsView : View
    {
        private KagitKartWorld world;
        private KagitKartResultRow[] rows;

        protected override void OnGameObjectInitialize()
        {
            rows = new[] { KagitKartResultRow_Row0, KagitKartResultRow_Row1, KagitKartResultRow_Row2, KagitKartResultRow_Row3,
                KagitKartResultRow_Row4, KagitKartResultRow_Row5, KagitKartResultRow_Row6, KagitKartResultRow_Row7 };
            Button_Next.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.NextRound));
            Button_Hub.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Title));
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>显示本场积分提交后的独立总积分榜，不再次推进或提交积分。</summary>
        /// <param name="owner">拥有真实杯赛及开赛前显示顺序的场景宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            var standings = owner.GrandPrix.SortedStandings();
            int maximum = 1;
            foreach (var row in standings) maximum = Mathf.Max(maximum, row.Points);
            for (int i = 0; i < standings.Count; i++)
            {
                Color body = KagitKartUIColors.Ink;
                foreach (var character in owner.Roster)
                {
                    if (character.id == standings[i].CharacterId) ColorUtility.TryParseHtmlString(character.colors.body, out body);
                }
                rows[i].SetStanding(standings[i], i + 1, owner.PreviousStandingPlace(standings[i].CharacterId), maximum,
                    body, standings[i].CharacterId == owner.CharacterId, owner.ReducedMotion);
            }
            TextMeshProUGUI_Kicker.text = "大奖赛 · 第 " + (owner.GrandPrix.Index + 1) + "/4 章结束后";
            TextMeshProUGUI_Title.text = "总积分榜";
            TextMeshProUGUI_Label.text = owner.GrandPrix.Completed ? "颁奖典礼" : "下一章：" + KagitKartCatalog.TrackName(KagitKartCatalog.Tracks[owner.GrandPrix.Index + 1]);
            Fit();
            KagitKartMenuPaperMotion_BoardFrame.Play(0, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(Button_Next.gameObject);

        private void Render()
        {
            if (world == null) return;
            Button_Next.interactable = Button_Hub.interactable = !world.Busy;
            TextMeshProUGUI_Feedback.text = world.Feedback ?? "";
            KagitKartMenuPaperMotion_BoardFrame.SetReducedMotion(world.ReducedMotion);
            foreach (var row in rows) row.SetReducedMotion(world.ReducedMotion);
        }

        /// 由当前 World.LateUpdate 交付窗口/安全区尺寸；View 不使用 Unity Update 回调。
        public void AdvanceVisual()
        {
            if (gameObject.activeInHierarchy) Fit();
        }

        private void Fit()
        {
            if (world == null) return;
            Rect safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            var viewport = RectTransform_Viewport.rect;
            RectTransform_BoardFrame.localScale = Vector3.one * Mathf.Min(1, (viewport.width - 72) / 1240, (viewport.height - 72) / 790);
        }

        protected override void OnDestroy() => world = null;
    }
}
