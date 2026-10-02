using Hotfix.JinxCasino.Interaction;
using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    [Serializable]
    public sealed class CasinoS1CardSlot
    {
        public Transform Pose;
        public MeshFilter Filter;
        public Renderer Renderer;
    }
    [Serializable]
    public sealed class CasinoS1CardFace
    {
        public Mesh Mesh;
        public Material[] Materials = Array.Empty<Material>();
    }
    /// 真实公开rank与暗牌背面；已保存牌槽/字形网格交换，不生成UGUI或猜测未揭示牌。
    public sealed class JinxCasinoS1BlackjackPresentation : JinxCasinoS1Presentation
    {
        [SerializeField] private CasinoS1CardSlot[] playerCards = Array.Empty<CasinoS1CardSlot>();
        [SerializeField] private CasinoS1CardSlot[] dealerCards = Array.Empty<CasinoS1CardSlot>();
        [SerializeField] private CasinoS1CardFace[] faces = Array.Empty<CasinoS1CardFace>();
        [SerializeField] private CasinoS1CardFace back;
        [SerializeField] private CasinoS1CardSlot drawCard;
        [SerializeField] private Transform dealOrigin;
        [SerializeField] private Transform dealerArm;
        [SerializeField] private TMP_Text playerTotal;
        [SerializeField] private TMP_Text dealerTotal;
        private Vector3[] playerRest;
        private Vector3[] dealerRest;
        private Quaternion[] playerPoseRest;
        private Quaternion[] dealerPoseRest;
        private Quaternion[] dealerMeshRest;
        private Quaternion armRest;
        private int[] shownPlayer;
        private int[] shownDealer;
        private int[] desiredPlayer = Array.Empty<int>();
        private int[] desiredDealer = Array.Empty<int>();
        private readonly List<Deal> queue = new List<Deal>();
        private readonly List<Flip> flipQueue = new List<Flip>();
        private const float FlipDuration = .48f;
        private const float FlipLift = .14f;
        private bool flipping;
        private float flipTime;
        private Flip currentFlip;
        private bool flying;
        private float flightTime;
        private Deal current;
        private bool previousComplete;
        protected override CasinoGameKind Game => CasinoGameKind.Blackjack;
        public override bool IsAnimating => flying || queue.Count > 0 || flipping || flipQueue.Count > 0;

        /// <summary>绑定已保存的牌槽与牌面库，演出只读取公开结果。</summary>
        /// <param name="id">稳定机台ID。</param>
        /// <param name="players">玩家十二个牌槽。</param>
        /// <param name="dealers">庄家十二个牌槽，牌面网格为同原点子节点。</param>
        /// <param name="rankFaces">按1至13排序的公开点数模板。</param>
        /// <param name="cardBack">未揭示牌背模板。</param>
        /// <param name="movingCard">预置发牌演出物件。</param>
        /// <param name="origin">发牌起点。</param>
        /// <param name="arm">机械庄家手臂挂点。</param>
        /// <param name="playerCounter">玩家可见点数铭牌。</param>
        /// <param name="dealerCounter">庄家可见点数铭牌。</param>
        /// <param name="amount">投入铭牌。</param>
        /// <param name="rules">局部规则铭牌。</param>
        /// <param name="result">结果铭牌。</param>
        public void Configure(string id, CasinoS1CardSlot[] players, CasinoS1CardSlot[] dealers, CasinoS1CardFace[] rankFaces,
            CasinoS1CardFace cardBack, CasinoS1CardSlot movingCard, Transform origin, Transform arm,
            TMP_Text playerCounter, TMP_Text dealerCounter, TMP_Text amount, TMP_Text rules, TMP_Text result)
        {
            if (players == null || players.Length != 12 || dealers == null || dealers.Length != 12 || rankFaces == null || rankFaces.Length != 13 ||
                cardBack == null || movingCard == null || origin == null || arm == null ||
                Array.Exists(players, InvalidSlot) || Array.Exists(dealers, InvalidSlot) || InvalidSlot(movingCard) ||
                Array.Exists(rankFaces, InvalidFace) || InvalidFace(cardBack)) throw new ArgumentException("S1牌桌需要全部保存牌槽、rank模板、背面与发牌引用。");
            playerCards = players; dealerCards = dealers; faces = rankFaces; back = cardBack; drawCard = movingCard;
            dealOrigin = origin; dealerArm = arm; playerTotal = playerCounter; dealerTotal = dealerCounter;
            ConfigureLabels(id, amount, rules, result); ResetVisual();
        }
        protected override void CacheBindings()
        {
            if (playerCards.Length == 0 || dealerCards.Length == 0) return;
            playerRest = Array.ConvertAll(playerCards, card => card.Pose.localPosition);
            dealerRest = Array.ConvertAll(dealerCards, card => card.Pose.localPosition);
            playerPoseRest = Array.ConvertAll(playerCards, card => card.Pose.localRotation);
            dealerPoseRest = Array.ConvertAll(dealerCards, card => card.Pose.localRotation);
            // 正式模型的牌面网格是Pose下独立、同原点的子节点；半程翻转不能改变牌槽或任何碰撞体。
            if (Array.Exists(dealerCards, card => card.Filter.transform.parent != card.Pose ||
                card.Renderer.transform != card.Filter.transform || card.Filter.transform.localPosition.sqrMagnitude > .000001f))
                throw new InvalidOperationException("S1翻牌需要同原点的独立牌面Mesh子节点。请检查保存的模型引用。");
            dealerMeshRest = Array.ConvertAll(dealerCards, card => card.Filter.transform.localRotation);
            shownPlayer = new int[playerCards.Length]; shownDealer = new int[dealerCards.Length];
            if (dealerArm != null) armRest = dealerArm.localRotation;
            Array.Fill(shownPlayer, -1); Array.Fill(shownDealer, -1);
        }
        protected override void ResetVisual()
        {
            if (shownPlayer == null) return;
            ClearAnimations(); previousComplete = false;
            desiredPlayer = desiredDealer = Array.Empty<int>(); Array.Fill(shownPlayer, -1); Array.Fill(shownDealer, -1);
            for (int index = 0; index < playerCards.Length; index++)
            {
                playerCards[index].Pose.localPosition = playerRest[index];
                playerCards[index].Pose.localRotation = playerPoseRest[index];
                playerCards[index].Pose.gameObject.SetActive(false);
            }
            for (int index = 0; index < dealerCards.Length; index++)
            {
                RestoreDealerRotation(index); dealerCards[index].Pose.localPosition = dealerRest[index];
                dealerCards[index].Pose.gameObject.SetActive(false);
            }
            if (drawCard?.Pose != null) drawCard.Pose.gameObject.SetActive(false);
            if (dealerArm != null) dealerArm.localRotation = armRest;
            SetText(playerTotal, string.Empty); SetText(dealerTotal, string.Empty);
        }
        protected override void ApplyView(JinxCasinoTableView view, bool newSettlement, bool snap)
        {
            if (shownPlayer == null) return;
            var round = view.Presentation;
            if (round == null)
            {
                if (!view.HasOtherActiveRound && view.DraftStake > 0) { ResetVisual(); SetResult("等待下注"); }
                return;
            }
            bool nextRound = previousComplete && (!round.IsComplete || newSettlement);
            if (nextRound) ResetVisual();
            desiredPlayer = (int[])round.NumberValues.Clone();
            bool hasHole = !round.IsComplete && round.SecondaryValues.Length == 1;
            desiredDealer = new int[round.SecondaryValues.Length + (hasHole ? 1 : 0)];
            Array.Copy(round.SecondaryValues, desiredDealer, round.SecondaryValues.Length);
            if (desiredPlayer.Length > playerCards.Length || desiredDealer.Length > dealerCards.Length)
                throw new InvalidOperationException("公开牌数超出S1保存牌槽。");
            previousComplete = round.IsComplete;
            if (snap)
            {
                ClearAnimations();
                if (drawCard?.Pose != null) drawCard.Pose.gameObject.SetActive(false);
                if (dealerArm != null) dealerArm.localRotation = armRest;
                SnapRow(playerCards, shownPlayer, desiredPlayer, playerRest, playerPoseRest, false);
                SnapRow(dealerCards, shownDealer, desiredDealer, dealerRest, dealerPoseRest, true);
                UpdateCounters(); UpdateResult(); return;
            }
            // 已落桌暗牌收到公开rank时翻牌；重复DTO只保留已有队列/进度，不能重新计时。
            for (int index = 0; index < Math.Max(desiredPlayer.Length, desiredDealer.Length); index++)
            {
                if (index < desiredPlayer.Length) Schedule(false, index, desiredPlayer[index]);
                if (index < desiredDealer.Length) Schedule(true, index, desiredDealer[index]);
            }
            UpdateCounters(); UpdateAnimationResult();
        }
        private void Schedule(bool dealer, int index, int rank)
        {
            var shown = dealer ? shownDealer : shownPlayer;
            var slots = dealer ? dealerCards : playerCards;
            if (shown[index] >= 0)
            {
                if (dealer && (shown[index] == 0 && rank > 0 || HasScheduledFlip(index)))
                {
                    ScheduleFlip(index, rank); return;
                }
                if (shown[index] != rank) { SetFace(slots[index], rank); shown[index] = rank; }
                return;
            }
            if (flying && current.Dealer == dealer && current.Index == index || queue.Exists(deal => deal.Dealer == dealer && deal.Index == index)) return;
            queue.Add(new Deal { Dealer = dealer, Index = index });
        }
        protected override void Tick(float seconds)
        {
            if (shownPlayer == null) return;
            MoveRow(playerCards, shownPlayer, desiredPlayer, playerRest, seconds, -1);
            MoveRow(dealerCards, shownDealer, desiredDealer, dealerRest, seconds, flipping ? currentFlip.Index : -1);
            // 先完成当前发牌，再揭示暗牌，最后补庄家新抽牌；每张牌仍来自同一公开数组。
            if (!flying && !flipping && flipQueue.Count > 0)
            {
                currentFlip = flipQueue[0]; flipQueue.RemoveAt(0); flipping = true; flipTime = 0;
                RestoreDealerRotation(currentFlip.Index); SetFace(dealerCards[currentFlip.Index], 0);
            }
            if (flipping) { TickFlip(seconds); return; }
            if (!flying && queue.Count > 0)
            { current = queue[0]; queue.RemoveAt(0); flying = true; flightTime = 0; SetFace(drawCard, 0); drawCard.Pose.gameObject.SetActive(true); }
            if (!flying) return;
            flightTime += seconds; float t = Mathf.Clamp01(flightTime / .24f);
            var row = current.Dealer ? dealerCards : playerCards;
            var values = current.Dealer ? desiredDealer : desiredPlayer;
            var rest = current.Dealer ? dealerRest : playerRest;
            var target = RowPosition(current.Index, values.Length, rest);
            Vector3 from = drawCard.Pose.parent.InverseTransformPoint(dealOrigin.position);
            Vector3 to = drawCard.Pose.parent.InverseTransformPoint(row[current.Index].Pose.parent.TransformPoint(target));
            drawCard.Pose.localPosition = Vector3.Lerp(from, to, t) + Vector3.up * (Mathf.Sin(t * Mathf.PI) * .08f);
            if (dealerArm != null) dealerArm.localRotation = armRest * Quaternion.AngleAxis(Mathf.Sin(t * Mathf.PI) * 8, Vector3.forward);
            if (t < 1) return;
            SetFace(row[current.Index], values[current.Index]); row[current.Index].Pose.localPosition = target;
            row[current.Index].Pose.gameObject.SetActive(true);
            (current.Dealer ? shownDealer : shownPlayer)[current.Index] = values[current.Index];
            drawCard.Pose.gameObject.SetActive(false); if (dealerArm != null) dealerArm.localRotation = armRest;
            flying = false; UpdateCounters(); UpdateAnimationResult();
        }
        private bool HasScheduledFlip(int index) => flipping && currentFlip.Index == index || flipQueue.Exists(flip => flip.Index == index);
        private void ScheduleFlip(int index, int rank)
        {
            // rank=0仍是公开暗牌占位，不发起揭示，也不访问规则中的隐藏牌。
            if (rank <= 0) return;
            if (flipping && currentFlip.Index == index) { currentFlip.Rank = rank; return; }
            int queued = flipQueue.FindIndex(flip => flip.Index == index);
            var reveal = new Flip { Index = index, Rank = rank };
            if (queued >= 0) flipQueue[queued] = reveal;
            else flipQueue.Add(reveal);
        }
        private void TickFlip(float seconds)
        {
            flipTime += seconds;
            float t = Mathf.Clamp01(flipTime / FlipDuration);
            var card = dealerCards[currentFlip.Index];
            card.Pose.localPosition = RowPosition(currentFlip.Index, desiredDealer.Length, dealerRest) +
                Vector3.up * (Mathf.Sin(t * Mathf.PI) * FlipLift);
            // 卡牌长边沿Z。0..90度只显示背面；侧面时换真实rank模板，此后继续转至180度。
            card.Pose.localRotation = dealerPoseRest[currentFlip.Index] * Quaternion.AngleAxis(t * 180, Vector3.forward);
            bool revealed = t >= .5f;
            if (revealed)
            {
                if (shownDealer[currentFlip.Index] != currentFlip.Rank)
                {
                    SetFace(card, currentFlip.Rank); shownDealer[currentFlip.Index] = currentFlip.Rank;
                    UpdateCounters();
                }
                // 背面和rank模板都把字形放在+Y；半程把独立Mesh转至背侧，最终两层180度抵消。
                Vector3 meshAxis = Quaternion.Inverse(dealerMeshRest[currentFlip.Index]) * Vector3.forward;
                card.Filter.transform.localRotation = dealerMeshRest[currentFlip.Index] * Quaternion.AngleAxis(180, meshAxis);
            }
            if (t < 1) { SetResult("翻牌中"); return; }
            RestoreDealerRotation(currentFlip.Index);
            card.Pose.localPosition = RowPosition(currentFlip.Index, desiredDealer.Length, dealerRest);
            flipping = false; flipTime = 0; UpdateCounters(); UpdateAnimationResult();
        }
        private void RestoreDealerRotation(int index)
        {
            dealerCards[index].Pose.localRotation = dealerPoseRest[index];
            dealerCards[index].Filter.transform.localRotation = dealerMeshRest[index];
        }
        private void ClearAnimations()
        {
            queue.Clear(); flipQueue.Clear(); flying = false; flipping = false; flightTime = 0; flipTime = 0;
            current = default; currentFlip = default;
        }
        private void UpdateAnimationResult()
        {
            if (!IsAnimating) UpdateResult();
            else SetResult(flipping || flipQueue.Count > 0 ? "翻牌中" : "发牌中");
        }
        private void SetFace(CasinoS1CardSlot slot, int rank)
        {
            if (rank < 0 || rank > 13 || faces.Length != 13 || rank == 0 && back == null)
                throw new InvalidOperationException("S1只接受公开rank1..13或暗牌背面0。");
            var face = rank == 0 ? back : faces[rank - 1];
            slot.Filter.sharedMesh = face.Mesh; slot.Renderer.sharedMaterials = face.Materials;
        }
        private void SnapRow(CasinoS1CardSlot[] slots, int[] shown, int[] values, Vector3[] rest, Quaternion[] rotations, bool dealer)
        {
            for (int index = 0; index < slots.Length; index++)
            {
                bool visible = index < values.Length; slots[index].Pose.gameObject.SetActive(visible); shown[index] = visible ? values[index] : -1;
                slots[index].Pose.localRotation = rotations[index];
                if (dealer) RestoreDealerRotation(index);
                if (visible) { SetFace(slots[index], values[index]); slots[index].Pose.localPosition = RowPosition(index, values.Length, rest); }
            }
        }
        private static void MoveRow(CasinoS1CardSlot[] slots, int[] shown, int[] values, Vector3[] rest, float seconds, int skipIndex)
        {
            for (int index = 0; index < values.Length; index++)
                if (shown[index] >= 0 && index != skipIndex) slots[index].Pose.localPosition = Vector3.Lerp(slots[index].Pose.localPosition, RowPosition(index, values.Length, rest), Mathf.Clamp01(seconds * 15));
        }
        private static Vector3 RowPosition(int index, int count, Vector3[] rest)
        {
            float spacing = count <= 6 ? .26f : 1.84f / (count - 1);
            // +X在此机台正面观察的屏幕左侧；牌序和公开数组一致。
            return new Vector3(((count - 1) * .5f - index) * spacing, rest[0].y + index * .0004f, rest[0].z);
        }
        private void UpdateCounters()
        {
            SetText(playerTotal, CountCards(shownPlayer) == 0 ? string.Empty : Total(shownPlayer).ToString());
            SetText(dealerTotal, CountCards(shownDealer) == 0 ? string.Empty : Total(shownDealer) + (Array.IndexOf(shownDealer, 0) >= 0 ? "+?" : string.Empty));
        }
        private void UpdateResult()
        {
            var round = Latest?.Presentation;
            if (round == null) return;
            SetResult(round.IsComplete ? "返还 " + round.Payout : "要牌 / 停牌");
        }
        private static int CountCards(int[] ranks) { int count = 0; foreach (int rank in ranks) if (rank >= 0) count++; return count; }
        private static int Total(int[] ranks)
        {
            int total = 0, aces = 0;
            foreach (int rank in ranks) if (rank > 0) { total += rank == 1 ? 11 : Math.Min(rank, 10); if (rank == 1) aces++; }
            while (total > 21 && aces-- > 0) total -= 10;
            return total;
        }
        private struct Deal { public bool Dealer; public int Index; }
        private struct Flip { public int Index; public int Rank; }
        private static bool InvalidSlot(CasinoS1CardSlot value) => value == null || value.Pose == null || value.Filter == null || value.Renderer == null;
        private static bool InvalidFace(CasinoS1CardFace value) => value == null || value.Mesh == null || value.Materials == null || value.Materials.Length != value.Mesh.subMeshCount;
    }
}
