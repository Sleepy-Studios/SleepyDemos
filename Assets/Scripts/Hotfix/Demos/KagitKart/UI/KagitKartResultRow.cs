using Core.Runtime;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.KagitKart
{
    /// 正式表行子 Prefab；只接收一次结算显示数据，不计算比赛或积分。
    public sealed class KagitKartResultRow : MonoBehaviour
    {
        [SerializeField] private TMP_Text placeText;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text bestLapText;
        [SerializeField] private TMP_Text pointsText;
        [SerializeField] private TMP_Text bonusText;
        [SerializeField] private TMP_Text bestTag;
        [SerializeField] private TMP_Text movementText;
        [SerializeField] private Image portrait;
        [SerializeField] private Image topRule;
        [SerializeField] private Image bottomRule;
        [SerializeField] private KagitKartResultShape playerHighlight;
        [SerializeField] private KagitKartResultShape pointsBar;
        [SerializeField] private Sprite[] portraits;
        [SerializeField] private string[] portraitIds;
        private static readonly Color Ink = UIColors.KagitKart.Ink;
        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 position;
        private float elapsed, delay;
        private bool reducedMotion;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
            position = rect.anchoredPosition;
        }

        /// <summary>显示普通/大奖赛一名车手的真实结果。</summary>
        /// <param name="result">本场已经排序的结果。</param>
        /// <param name="isPlayer">是否玩家高亮行。</param>
        /// <param name="grandPrix">是否显示累计积分及本场增量。</param>
        /// <param name="index">源行入场序号。</param>
        /// <param name="reduced">当前低动态策略。</param>
        public void SetRace(KagitKartRaceResult result, bool isPlayer, bool grandPrix, int index, bool reduced)
        {
            Reset(index, reduced);
            placeText.text = result.Place + ".";
            placeText.color = PlaceInk(result.Place);
            nameText.text = KagitKartCatalog.CharacterName(result.CharacterId);
            ShowPortrait(result.CharacterId);
            timeText.text = (result.Estimated ? "≈ " : "") + KagitKartHudMotion.TimeText(result.Time);
            timeText.color = ColorUtil.WithAlpha(Ink, result.Estimated ? 0.72f : 1);
            bestLapText.text = result.BestLap.HasValue ? KagitKartHudMotion.TimeText(result.BestLap.Value) : "—";
            pointsText.text = grandPrix ? result.TotalPoints.ToString() : "";
            bonusText.text = grandPrix && result.Points != 0 ? "+" + result.Points : "";
            playerHighlight.gameObject.SetActive(isPlayer);
        }

        /// <summary>显示计时赛单圈或玩家全程，最佳圈使用当场最短圈。</summary>
        /// <param name="lap">从1起的圈次；0表示总时间。</param>
        /// <param name="time">真实圈速或全程时间。</param>
        /// <param name="best">是否当场最佳圈。</param>
        /// <param name="characterId">总时间行的玩家头像；圈速行不显示头像。</param>
        /// <param name="index">源行入场序号。</param>
        /// <param name="reduced">当前低动态策略。</param>
        public void SetLap(int lap, double time, bool best, string characterId, int index, bool reduced)
        {
            Reset(index, reduced);
            placeText.text = lap > 0 ? lap + "." : "";
            placeText.color = Ink;
            nameText.text = lap > 0 ? "圈速" : "总时间";
            timeText.text = KagitKartHudMotion.TimeText(time);
            bestTag.transform.parent.gameObject.SetActive(best);
            topRule.gameObject.SetActive(lap == 0);
            bottomRule.gameObject.SetActive(lap != 0);
            playerHighlight.gameObject.SetActive(lap == 0);
            if (lap == 0) ShowPortrait(characterId);
        }

        /// <summary>显示总积分榜一行，升降比较源开赛前积分顺序。</summary>
        /// <param name="standing">真实总积分。</param>
        /// <param name="place">当前总名次。</param>
        /// <param name="previousPlace">前场积分榜的名次；0表示首场。</param>
        /// <param name="maximum">当前最高积分，至少1。</param>
        /// <param name="bodyColor">原角色body色。</param>
        /// <param name="isPlayer">是否玩家。</param>
        /// <param name="reduced">当前低动态策略。</param>
        public void SetStanding(KagitKartStanding standing, int place, int previousPlace, int maximum,
            Color bodyColor, bool isPlayer, bool reduced)
        {
            Reset(place - 1, reduced);
            placeText.text = place + ".";
            placeText.color = PlaceInk(place);
            nameText.text = KagitKartCatalog.CharacterName(standing.CharacterId);
            ShowPortrait(standing.CharacterId);
            pointsText.text = standing.Points.ToString();
            pointsBar.gameObject.SetActive(true);
            pointsBar.SetFill((float)standing.Points / maximum, bodyColor);
            int change = previousPlace > 0 ? previousPlace - place : 0;
            movementText.text = change > 0 ? "▲" + change : change < 0 ? "▼" + -change : "";
            movementText.color = change > 0 ? UIColors.KagitKart.Rising : UIColors.KagitKart.Accent;
            movementText.gameObject.SetActive(change != 0);
            playerHighlight.gameObject.SetActive(isPlayer);
        }

        /// <summary>只更新动态策略，不重播行入场。</summary>
        /// <param name="value">当前实际低动态策略。</param>
        public void SetReducedMotion(bool value) => reducedMotion = value;

        private void Reset(int index, bool reduced)
        {
            reducedMotion = reduced;
            delay = 0.25f + index * 0.09f;
            elapsed = 0;
            timeText.color = Ink;
            timeText.text = bestLapText.text = pointsText.text = bonusText.text = "";
            bestTag.transform.parent.gameObject.SetActive(false);
            movementText.gameObject.SetActive(false);
            portrait.gameObject.SetActive(false);
            topRule.gameObject.SetActive(false);
            bottomRule.gameObject.SetActive(true);
            pointsBar.gameObject.SetActive(false);
            gameObject.SetActive(true);
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            float t = reducedMotion ? 1 : Mathf.Clamp01((elapsed - delay) / 0.32f);
            float eased = t <= 0 ? 0 : t >= 1 ? 1 : KagitKartHudMotion.Ease(t, new Vector4(0.2f, 1.4f, 0.4f, 1));
            rect.anchoredPosition = position + Vector2.left * (28.8f * (1 - eased));
            group.alpha = Mathf.Clamp01(eased);
        }

        private void ShowPortrait(string id)
        {
            portrait.sprite = portraits[Array.IndexOf(portraitIds, id)];
            portrait.gameObject.SetActive(true);
        }

        private static Color PlaceInk(int value) => value == 1 ? UIColors.KagitKart.FirstPlace : value == 2 ? UIColors.KagitKart.SecondPlace : value == 3 ? UIColors.KagitKart.ThirdPlace : Ink;
}
}
