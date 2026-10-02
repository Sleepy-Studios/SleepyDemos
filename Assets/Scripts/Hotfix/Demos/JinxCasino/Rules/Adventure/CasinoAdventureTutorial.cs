using System;

namespace Hotfix.JinxCasino.Rules
{
    public sealed partial class CasinoAdventureSession
    {
        public const string TutorialSlotsStation = "s1.fruit";
        public const string TutorialBlackjackStation = "s1.cards";
        public const string TutorialLeversStation = "s1.sync";

        /// <summary>在当前新练习局启动教学；不建立另一钱包、不改随机或替玩家操作。</summary>
        /// <param name="requestId">稳定请求编号，重试不重复创建检查点。</param>
        public CasinoAdventureResult StartTutorial(string requestId)
            => Execute(requestId, "tutorial:start", () =>
            {
                if (state.Mode != CasinoAdventureMode.Practice || state.PlayerCount != 1 || state.Phase != CasinoAdventurePhase.Playing || round != null)
                    return Fail("TutorialPracticeRequired", "请先开始没有活动机台的单人练习局。");
                if (state.Teaching.Status != CasinoTutorialStatus.None) return Fail("TutorialAlreadyStarted", "重玩教学需要明确开始新的练习局。");
                if (state.ElapsedMilliseconds != 0 || state.SettledRoundSequence != 0 || !string.IsNullOrEmpty(state.LastRoundJson) ||
                    state.StageIndex != 0 || state.CompletedStages != 0 || state.Coins != state.Config.StartingCoins || state.Inventory.Count != 0 || state.PreparedItems.Count != 0 ||
                    state.CooperationHelpCharges != 0 || state.Effects.Count != 0 || state.ProcessedRequests.Exists(record => record.Result.Success))
                    return Fail("TutorialFreshRunRequired", "请从互动教学入口创建新的练习局后立即开始教学。");
                if (!IsGameAllowed(CasinoGameKind.Slots) || !IsGameAllowed(CasinoGameKind.Blackjack) || !IsGameAllowed(CasinoGameKind.CooperativeLevers) ||
                    state.Config.ShopItemIds.Length > 0 && Array.IndexOf(state.Config.ShopItemIds, "duo_wrench") < 0)
                    return Fail("TutorialUnavailable", "当前配置没有教学需要的三台机台或双人扳手。");
                state.Teaching = new CasinoTutorialState { Status = CasinoTutorialStatus.Active };
                MoveTutorialTo(state.Teaching, CasinoTutorialStep.Look);
                return Ok("互动教学已开始；练习筹码不计正式成长。");
            });

        /// <summary>核验实际事实并推进教学；低频累计观察不进入幂等请求表，也不会发放奖励。</summary>
        /// <param name="fact">实际应用后的视角/位移、已成功物件操作或展示事实。</param>
        /// <param name="value">视角/位移为从本步开始的累计毫度/毫米；结果为结算序号，其余成功事实用1。</param>
        /// <param name="stationId">实际保存的具体机台ID；Walk需当前近台ID，购物和Continue不要求机台。</param>
        public CasinoAdventureResult ObserveTutorial(CasinoTutorialFact fact, int value = 0, string stationId = null)
        {
            if (!Enum.IsDefined(typeof(CasinoTutorialFact), fact) || value < 0) return TutorialObservationResult(Fail("InvalidTutorialFact", "教学事实参数非法。"));
            if (state.Teaching.Status != CasinoTutorialStatus.Active) return TutorialObservationResult(Ok("当前没有进行中的教学。"));
            if (state.Mode != CasinoAdventureMode.Practice || state.Phase != CasinoAdventurePhase.Playing) return TutorialObservationResult(Fail("TutorialPracticeRequired", "教学仅观察当前练习局。"));
            if (state.Revision == int.MaxValue) return TutorialObservationResult(Fail("BalanceOverflow", "检查点版本已达上限。"));
            var candidate = Clone(state.Teaching);
            var result = ObserveTutorialCore(candidate, fact, value, stationId);
            result.Balance = state.Coins;
            if (!result.Success || !result.Changed) return result;
            state.Teaching = candidate; state.Revision++;
            return new CasinoAdventureResult { Success = true, Changed = true, Balance = state.Coins, Description = "教学进度已更新。" };
        }
        private CasinoAdventureResult TutorialObservationResult(CasinoAdventureResult result) { result.Balance = state.Coins; return result; }

        /// <summary>明确跳过提示；保留当前钱包、随机与所有已经投入的活动局。</summary>
        /// <param name="requestId">稳定请求编号；相同请求不会再次更改检查点。</param>
        public CasinoAdventureResult SkipTutorial(string requestId)
            => Execute(requestId, "tutorial:skip", () =>
            {
                if (state.Teaching.Status != CasinoTutorialStatus.Active) return Fail("TutorialInactive", "当前没有可跳过的教学。");
                state.Teaching.Status = CasinoTutorialStatus.Skipped; state.Teaching.Step = CasinoTutorialStep.Skipped; state.Teaching.StationId = null;
                return Ok("教学提示已跳过，当前练习与已投入机台保留。");
            });

        private CasinoAdventureResult ObserveTutorialCore(CasinoTutorialState teaching, CasinoTutorialFact fact, int value, string stationId)
        {
            bool sameStation = stationId == teaching.StationId;
            int oldValue;
            switch (teaching.Step)
            {
                case CasinoTutorialStep.Look:
                    if (fact != CasinoTutorialFact.LookApplied) return Ok("先实际转动探索视角。");
                    oldValue = teaching.LookMillidegrees; teaching.LookMillidegrees = Math.Max(oldValue, Math.Min(15000, value));
                    if (teaching.LookMillidegrees == 15000) MoveTutorialTo(teaching, CasinoTutorialStep.Walk);
                    else if (oldValue == teaching.LookMillidegrees) return Ok("视角累计未变化。");
                    break;
                case CasinoTutorialStep.Walk:
                    if (fact != CasinoTutorialFact.WalkApplied) return Ok("实际走近水果机。");
                    oldValue = teaching.WalkMillimeters; teaching.WalkMillimeters = Math.Max(oldValue, Math.Min(600, value));
                    if (teaching.WalkMillimeters == 600 && sameStation) MoveTutorialTo(teaching, CasinoTutorialStep.EnterSlots);
                    else if (oldValue == teaching.WalkMillimeters) return Ok("尚未进入水果机交互范围。");
                    break;
                case CasinoTutorialStep.EnterSlots:
                    if (fact != CasinoTutorialFact.TableEntered || !sameStation || value != 1) return Ok("进入具体水果机并等聚焦完成。");
                    MoveTutorialTo(teaching, CasinoTutorialStep.AddChips); break;
                case CasinoTutorialStep.AddChips:
                    if (fact != CasinoTutorialFact.ChipsAdded || !sameStation || value < 1 || round != null) return Ok("实际放入筹码。");
                    MoveTutorialTo(teaching, CasinoTutorialStep.Confirm); break;
                case CasinoTutorialStep.Confirm:
                    if (fact != CasinoTutorialFact.ChipsConfirmed || !sameStation || value != 1 || round != null) return Ok("确认筹码；此时仍未付款。");
                    MoveTutorialTo(teaching, CasinoTutorialStep.SlotsResult); break;
                case CasinoTutorialStep.SlotsResult:
                    if (fact != CasinoTutorialFact.RoundPresented || !sameStation || !MatchesTutorialResult(teaching, CasinoGameKind.Slots, value)) return Fail("TutorialResultRequired", "请实际拉柄并查看这台的新结算。");
                    teaching.CompletedGameMask |= 1; MoveTutorialTo(teaching, CasinoTutorialStep.LeaveSlots); break;
                case CasinoTutorialStep.LeaveSlots:
                    if (fact != CasinoTutorialFact.TableLeft || !sameStation || value != 1) return Ok("实际离桌并等探索相机恢复。");
                    MoveTutorialTo(teaching, CasinoTutorialStep.Blackjack); break;
                case CasinoTutorialStep.Blackjack:
                    if (fact != CasinoTutorialFact.RoundPresented || !sameStation || !MatchesTutorialResult(teaching, CasinoGameKind.Blackjack, value)) return Fail("TutorialResultRequired", "请完成这台二十一点并查看结果。");
                    var cards = lastCompletedRound.GetPresentation();
                    // BJ合法TryAct只有Hit/Stand；道具不增加OperationCount，天然自动结算另核下注后的规则使用。
                    if (cards.OperationCount == 0 && !IsTutorialNaturalBlackjack(teaching))
                        return Fail("TutorialChoiceRequired", "请实际选择要牌或停牌。");
                    teaching.CompletedGameMask |= 2; MoveTutorialTo(teaching, CasinoTutorialStep.BuyWrench); break;
                case CasinoTutorialStep.BuyWrench:
                    if (fact != CasinoTutorialFact.ItemPurchased || !HasTutorialReceipt(teaching, "purchase:duo_wrench", true) || CountItem("duo_wrench") <= teaching.WrenchInventoryBaseline)
                        return Fail("TutorialPurchaseRequired", "请在柜台成功购买双人扳手。");
                    MoveTutorialTo(teaching, CasinoTutorialStep.UseWrench); break;
                case CasinoTutorialStep.UseWrench:
                    if (fact != CasinoTutorialFact.ItemUsed || !HasTutorialReceipt(teaching, "use:duo_wrench:") ||
                        !(state.CooperationHelpCharges > teaching.WrenchChargesBaseline || round != null && state.ActiveGame == CasinoGameKind.CooperativeLevers && state.ActiveStationId == TutorialLeversStation && round.GetPresentation().CooperationHelpUsed))
                        return Fail("TutorialUseRequired", "请实际使用库存中的双人扳手。");
                    MoveTutorialTo(teaching, CasinoTutorialStep.Levers); break;
                case CasinoTutorialStep.Levers:
                    if (fact != CasinoTutorialFact.RoundPresented || !sameStation || !MatchesTutorialResult(teaching, CasinoGameKind.CooperativeLevers, value)) return Fail("TutorialResultRequired", "请完成自己的0号杆与NPC1的真实协作。");
                    var levers = lastCompletedRound.GetPresentation();
                    if (!levers.IsObjectiveSuccess || levers.SelectedValues.Length != 2 || levers.SelectedValues[0] == 0 || levers.SelectedValues[1] == 0)
                        return Fail("TutorialCooperationRequired", "等待正确窗口后重试，不以超时或赔偿计为完成。");
                    teaching.CompletedGameMask |= 4; MoveTutorialTo(teaching, CasinoTutorialStep.Ready); break;
                case CasinoTutorialStep.Ready:
                    if (fact != CasinoTutorialFact.Continue || value != 1) return Ok("明确完成教学后选择继续练习或新正式冒险。");
                    teaching.Status = CasinoTutorialStatus.Completed; MoveTutorialTo(teaching, CasinoTutorialStep.Completed); break;
                default: return Ok("教学已结束。");
            }
            return new CasinoAdventureResult { Success = true, Changed = true, Balance = state.Coins };
        }

        private bool MatchesTutorialResult(CasinoTutorialState teaching, CasinoGameKind game, int sequence)
            => sequence == state.SettledRoundSequence && sequence > teaching.SettlementBaseline && state.LastStationId == teaching.StationId &&
                round == null && lastCompletedRound != null && lastCompletedRound.IsComplete && lastCompletedRound.GetPresentation().Game == game && state.LastRoundCost > 0;
        private bool HasTutorialReceipt(CasinoTutorialState teaching, string fingerprint, bool exact = false)
            => state.ProcessedRequests.Exists(record => record.Revision > teaching.ReceiptBaselineRevision && record.Result.Success &&
                (exact ? record.Fingerprint == fingerprint : record.Fingerprint.StartsWith(fingerprint, StringComparison.Ordinal)));
        private bool IsTutorialNaturalBlackjack(CasinoTutorialState teaching)
        {
            int begun = 0;
            foreach (var record in state.ProcessedRequests)
                if (record.Result.Success && record.Revision > teaching.ReceiptBaselineRevision && record.Fingerprint.StartsWith("bet:" + (int)CasinoGameKind.Blackjack + ":", StringComparison.Ordinal) &&
                    record.Fingerprint.EndsWith(":station:" + TutorialBlackjackStation, StringComparison.Ordinal)) begun = Math.Max(begun, record.Revision);
            return begun > 0 && !state.ProcessedRequests.Exists(record => record.Result.Success && record.Revision > begun && record.Fingerprint.StartsWith("use:redraw_card:", StringComparison.Ordinal));
        }

        private void MoveTutorialTo(CasinoTutorialState teaching, CasinoTutorialStep step)
        {
            teaching.Step = step; teaching.StationId = TutorialStationFor(step);
            teaching.SettlementBaseline = state.SettledRoundSequence; teaching.ReceiptBaselineRevision = state.Revision;
            teaching.WrenchInventoryBaseline = CountItem("duo_wrench"); teaching.WrenchChargesBaseline = state.CooperationHelpCharges;
        }
        private static string TutorialStationFor(CasinoTutorialStep step)
            => step <= CasinoTutorialStep.LeaveSlots ? TutorialSlotsStation : step == CasinoTutorialStep.Blackjack ? TutorialBlackjackStation : step == CasinoTutorialStep.Levers ? TutorialLeversStation : null;

        private void RestoreTutorialCheckpoint()
        {
            if (state.Teaching.Status != CasinoTutorialStatus.Active) return;
            var teaching = state.Teaching;
            if (teaching.Step == CasinoTutorialStep.Confirm || teaching.Step == CasinoTutorialStep.SlotsResult && !MatchesTutorialResult(teaching, CasinoGameKind.Slots, state.SettledRoundSequence))
                MoveTutorialTo(teaching, CasinoTutorialStep.AddChips);
        }

        private static void ValidateTutorial(CasinoAdventureState value)
        {
            var teaching = value.Teaching;
            if (teaching == null || teaching.Version != 1 || !Enum.IsDefined(typeof(CasinoTutorialStatus), teaching.Status) || !Enum.IsDefined(typeof(CasinoTutorialStep), teaching.Step) ||
                teaching.LookMillidegrees < 0 || teaching.LookMillidegrees > 15000 || teaching.WalkMillimeters < 0 || teaching.WalkMillimeters > 600 ||
                teaching.SettlementBaseline < 0 || teaching.SettlementBaseline > value.SettledRoundSequence || teaching.ReceiptBaselineRevision < 0 || teaching.ReceiptBaselineRevision > value.Revision ||
                teaching.WrenchInventoryBaseline < 0 || teaching.WrenchChargesBaseline < 0 || (teaching.CompletedGameMask != 0 && teaching.CompletedGameMask != 1 && teaching.CompletedGameMask != 3 && teaching.CompletedGameMask != 7))
                throw new ArgumentException("教学快照非法。");
            if (teaching.Status == CasinoTutorialStatus.None)
            {
                if (teaching.Step != CasinoTutorialStep.Look || !string.IsNullOrEmpty(teaching.StationId) || teaching.SettlementBaseline != 0 || teaching.ReceiptBaselineRevision != 0 || teaching.CompletedGameMask != 0 ||
                    teaching.LookMillidegrees != 0 || teaching.WalkMillimeters != 0 || teaching.WrenchInventoryBaseline != 0 || teaching.WrenchChargesBaseline != 0) throw new ArgumentException("未开始的教学含进度。");
                return;
            }
            if (value.Mode != CasinoAdventureMode.Practice || value.PlayerCount != 1 ||
                teaching.Status == CasinoTutorialStatus.Active && (teaching.Step >= CasinoTutorialStep.Completed || (teaching.StationId ?? string.Empty) != (TutorialStationFor(teaching.Step) ?? string.Empty)) ||
                teaching.Status == CasinoTutorialStatus.Completed && (teaching.Step != CasinoTutorialStep.Completed || teaching.CompletedGameMask != 7 || !string.IsNullOrEmpty(teaching.StationId)) ||
                teaching.Status == CasinoTutorialStatus.Skipped && (teaching.Step != CasinoTutorialStep.Skipped || !string.IsNullOrEmpty(teaching.StationId))) throw new ArgumentException("教学状态与练习步骤不匹配。");
            if (teaching.Status != CasinoTutorialStatus.Skipped)
            {
                int expectedMask = teaching.Step <= CasinoTutorialStep.SlotsResult ? 0 : teaching.Step <= CasinoTutorialStep.Blackjack ? 1 : teaching.Step <= CasinoTutorialStep.Levers ? 3 : 7;
                if (teaching.CompletedGameMask != expectedMask || teaching.Step > CasinoTutorialStep.Look && teaching.LookMillidegrees != 15000 || teaching.Step > CasinoTutorialStep.Walk && teaching.WalkMillimeters != 600)
                    throw new ArgumentException("教学检查点跳过前置事实。");
            }
            else if ((teaching.CompletedGameMask > 0 || teaching.WalkMillimeters > 0) && teaching.LookMillidegrees != 15000 || teaching.CompletedGameMask > 0 && teaching.WalkMillimeters != 600)
                throw new ArgumentException("已跳过教学的历史事实不一致。");
        }
    }
}
