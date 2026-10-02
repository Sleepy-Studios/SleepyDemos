using Hotfix.JinxCasino.Interaction;
using System;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    /// 六种实体符号随保存轮轴转动；只按公开结果停稳，出币池不承担钱包逻辑。
    public sealed class JinxCasinoS1SlotsPresentation : JinxCasinoS1Presentation
    {
        [SerializeField] private Transform[] reels = Array.Empty<Transform>();
        [SerializeField] private Transform lever;
        [SerializeField] private Transform[] payoutChips = Array.Empty<Transform>();
        private Quaternion[] restReels;
        private Quaternion restLever;
        private Vector3[] restChips;
        private int[] symbols = new int[3];
        private float elapsed;
        private bool playing;
        private int visibleChips;
        private long payout;
        protected override CasinoGameKind Game => CasinoGameKind.Slots;
        public override bool IsAnimating => playing;

        /// <summary>绑定水果机保存的轮轴、拉柄及出币池。</summary>
        /// <param name="id">稳定机台ID。</param>
        /// <param name="wheelAxes">从左至右的三个轮轴。</param>
        /// <param name="handleAxis">启动拉柄轴。</param>
        /// <param name="coins">八枚演出筹码，不承担经济结算。</param>
        /// <param name="amount">投入铭牌。</param>
        /// <param name="rules">局部规则铭牌。</param>
        /// <param name="result">结果铭牌。</param>
        public void Configure(string id, Transform[] wheelAxes, Transform handleAxis, Transform[] coins,
            TMP_Text amount, TMP_Text rules, TMP_Text result)
        {
            if (wheelAxes == null || wheelAxes.Length != 3 || Array.Exists(wheelAxes, value => value == null) || handleAxis == null ||
                coins == null || coins.Length != 8 || Array.Exists(coins, value => value == null)) throw new ArgumentException("S1水果机需要保存的3轮轴/柄/8奖筹码。");
            reels = wheelAxes; lever = handleAxis; payoutChips = coins; ConfigureLabels(id, amount, rules, result); ResetVisual();
        }
        protected override void CacheBindings()
        {
            if (reels == null || reels.Length != 3 || Array.Exists(reels, value => value == null)) return;
            restReels = Array.ConvertAll(reels, value => value.localRotation);
            if (lever != null) restLever = lever.localRotation;
            restChips = Array.ConvertAll(payoutChips ?? Array.Empty<Transform>(), value => value.localPosition);
        }
        protected override void ResetVisual()
        {
            playing = false; elapsed = 0;
            if (restReels != null) for (int index = 0; index < 3; index++) reels[index].localRotation = restReels[index];
            if (lever != null) lever.localRotation = restLever;
            HideChips();
        }
        protected override void ApplyView(JinxCasinoTableView view, bool newSettlement, bool snap)
        {
            var round = view.Presentation;
            if (round == null)
            {
                if (!view.HasOtherActiveRound && view.DraftStake > 0 && !playing) { HideChips(); SetResult("等待拉柄"); }
                return;
            }
            if (!round.IsComplete || round.NumberValues.Length != 3) return;
            symbols = (int[])round.NumberValues.Clone(); payout = round.Payout;
            visibleChips = payout <= 0 ? 0 : (int)Math.Min(8, payout / 50 + 1);
            if (snap)
            {
                playing = false; SetFinalPose(); SetResult("返还 " + payout); return;
            }
            if (!newSettlement) return;
            elapsed = 0; playing = true; HideChips(); SetResult("转动中");
        }
        protected override void Tick(float seconds)
        {
            if (!playing || restReels == null) return;
            elapsed += seconds;
            for (int index = 0; index < 3; index++)
            {
                float duration = .80f + index * .18f;
                float t = Mathf.Clamp01(elapsed / duration); float ease = 1 - Mathf.Pow(1 - t, 3);
                reels[index].localRotation = restReels[index] * Quaternion.AngleAxis((720 + symbols[index] * 60) * ease, Vector3.right);
            }
            // 此源手柄在本地+Y/+Z伸出；X正角使其向下拉，而不是沿旧模型负角举起。
            if (lever != null)
                lever.localRotation = restLever * Quaternion.AngleAxis(elapsed < .28f ? Mathf.Sin(Mathf.Clamp01(elapsed / .28f) * Mathf.PI) * 35 : 0, Vector3.right);
            const float lastStop = 1.16f;
            if (elapsed < lastStop) return;
            SetResult("返还 " + payout);
            for (int index = 0; index < payoutChips.Length; index++)
            {
                float coinTime = elapsed - lastStop - index * .045f;
                bool show = index < visibleChips && coinTime >= 0;
                payoutChips[index].gameObject.SetActive(show);
                if (show)
                {
                    float t = Mathf.Clamp01(coinTime / .24f);
                    payoutChips[index].localPosition = restChips[index] + Vector3.up * ((1 - t) * .22f + Mathf.Sin(t * Mathf.PI) * .05f)
                        + Vector3.back * ((1 - t) * .18f);
                }
            }
            if (elapsed >= lastStop + .24f + Math.Max(0, visibleChips - 1) * .045f) { playing = false; SetFinalPose(); }
        }
        private void SetFinalPose()
        {
            if (restReels == null) return;
            for (int index = 0; index < 3; index++) reels[index].localRotation = restReels[index] * Quaternion.AngleAxis(symbols[index] * 60, Vector3.right);
            if (lever != null) lever.localRotation = restLever;
            for (int index = 0; index < payoutChips.Length; index++)
            { payoutChips[index].localPosition = restChips[index]; payoutChips[index].gameObject.SetActive(index < visibleChips); }
        }
        private void HideChips() { foreach (var chip in payoutChips) if (chip != null) chip.gameObject.SetActive(false); }
    }
}
