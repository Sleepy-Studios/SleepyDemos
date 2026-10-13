namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [Module("KagitKart")]
    [UIBind("KagitKartResultsView")]
    public partial class KagitKartResultsView : View
    {
        private KagitKartWorld world;
        private KagitKartResultRow[] rows;
        private float stampElapsed;
        private bool timeTrial;

        protected override void OnGameObjectInitialize()
        {
            rows = new[] { KagitKartResultRow_Row0, KagitKartResultRow_Row1, KagitKartResultRow_Row2, KagitKartResultRow_Row3,
                KagitKartResultRow_Row4, KagitKartResultRow_Row5, KagitKartResultRow_Row6, KagitKartResultRow_Row7 };
            Button_Next.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.ShowStandings));
            Button_Restart.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Restart));
            Button_Hub.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Title));
            // 原 Results 的 FocusList 没有 onBack；保留返回菜单按钮，不把 Esc 当退出比赛。
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>交付已结束的当前场，表格只读真实圈速/结果及已保存的最好全程。</summary>
        /// <param name="owner">当前 Demo 宿主，不复制比赛或存档状态。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            timeTrial = owner.Mode == KagitKartRaceMode.TimeTrial;
            float height = timeTrial ? 560 : 790;
            RectTransform_BoardFrame.sizeDelta = RectTransform_SheetGroup.sizeDelta = new Vector2(1240, height);
            RectTransform_SheetGroup.anchoredPosition = new Vector2(0, -height / 2);
            TextMeshProUGUI_Kicker.text = "第 " + owner.Session.Track.Track.definition.chapter + " 章 · " + (timeTrial ? "计时赛" : "比赛结算");
            TextMeshProUGUI_Title.text = KagitKartCatalog.TrackName(owner.TrackId);
            TextMeshProUGUI_HeadingPlace.text = timeTrial ? "圈次" : "名次";
            TextMeshProUGUI_HeadingDriver.gameObject.SetActive(!timeTrial);
            TextMeshProUGUI_HeadingBest.gameObject.SetActive(!timeTrial);
            TextMeshProUGUI_HeadingPoints.gameObject.SetActive(owner.Mode == KagitKartRaceMode.GrandPrix);
            foreach (var row in rows) row.gameObject.SetActive(false);
            bool newRecord = false;
            if (timeTrial)
            {
                var laps = owner.PlayerEntry.Progress.LapTimes;
                double best = double.PositiveInfinity;
                foreach (double lap in laps) best = System.Math.Min(best, lap);
                for (int i = 0; i < laps.Count; i++)
                {
                    rows[i].SetLap(i + 1, laps[i], laps[i] == best, owner.CharacterId, i, owner.ReducedMotion);
                }
                rows[laps.Count].SetLap(0, owner.PlayerEntry.FinishTime, false, owner.CharacterId, laps.Count, owner.ReducedMotion);
                var record = owner.LoadRecord(owner.TrackId);
                newRecord = record.bestRace.HasValue && System.Math.Abs(record.bestRace.Value - owner.PlayerEntry.FinishTime) < 0.001;
            }
            else
            {
                var results = owner.Session.Race.Results;
                for (int i = 0; i < results.Count; i++)
                {
                    rows[i].SetRace(results[i], results[i].CharacterId == owner.CharacterId,
                        owner.Mode == KagitKartRaceMode.GrandPrix, i, owner.ReducedMotion);
                }
            }
            RectTransform_NewRecord.gameObject.SetActive(newRecord);
            stampElapsed = 0;
            Button_Next.transform.parent.gameObject.SetActive(owner.Mode == KagitKartRaceMode.GrandPrix);
            Button_Restart.transform.parent.gameObject.SetActive(owner.Mode != KagitKartRaceMode.GrandPrix);
            TextMeshProUGUI_Label.text = "总积分榜";
            TextMeshProUGUI_Label1.text = timeTrial ? "再试一次" : "再赛一场";
            Fit();
            KagitKartMenuPaperMotion_BoardFrame.Play(0, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(timeTrial || world.Mode != KagitKartRaceMode.GrandPrix ? Button_Restart.gameObject : Button_Next.gameObject);

        private void Render()
        {
            if (world == null) return;
            Button_Next.interactable = Button_Restart.interactable = Button_Hub.interactable = !world.Busy;
            TextMeshProUGUI_Feedback.text = world.Feedback ?? "";
            KagitKartMenuPaperMotion_BoardFrame.SetReducedMotion(world.ReducedMotion);
            foreach (var row in rows) row.SetReducedMotion(world.ReducedMotion);
        }

        /// 由当前 World.LateUpdate 交付墙钟表现帧；View 不是 MonoBehaviour。
        public void AdvanceVisual()
        {
            if (world == null || !gameObject.activeInHierarchy) return;
            Fit();
            if (!RectTransform_NewRecord.gameObject.activeSelf) return;
            stampElapsed += Time.unscaledDeltaTime;
            float t = world.ReducedMotion ? 1 : Mathf.Clamp01((stampElapsed - 0.6f) / 0.35f);
            float eased = t <= 0 ? 0 : t >= 1 ? 1 : KagitKartHudMotion.Ease(t, new Vector4(0.2f, 1.6f, 0.45f, 1));
            RectTransform_NewRecord.localScale = Vector3.one * Mathf.LerpUnclamped(1.25f, 1, eased);
            // rotate:-6deg 与源 stamp transform 的 -9→-3.5deg 相加；UGUI Z 方向取反。
            RectTransform_NewRecord.localRotation = Quaternion.Euler(0, 0, 6 + Mathf.LerpUnclamped(9, 3.5f, eased));
        }

        private void Fit()
        {
            Rect safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            var viewport = RectTransform_Viewport.rect;
            float height = timeTrial ? 560 : 790;
            RectTransform_BoardFrame.localScale = Vector3.one * Mathf.Min(1, (viewport.width - 72) / 1240, (viewport.height - 72) / height);
        }

        protected override void OnDestroy() => world = null;
    }
}
