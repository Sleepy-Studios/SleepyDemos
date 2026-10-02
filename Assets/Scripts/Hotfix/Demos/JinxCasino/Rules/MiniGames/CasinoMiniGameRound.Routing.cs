using System;
using System.Collections.Generic;

namespace Hotfix.JinxCasino.Rules
{
    public sealed partial class CasinoMiniGameRound
    {
        partial void InitializeAdditionalGame(ref bool initialized)
        {
            initialized = true;
            if (state.Game == CasinoGameKind.SicBo)
            {
                state.Values = new[] { RollDie(), RollDie(), RollDie() }; int multiplier = SicBoMultiplier(state.Values);
                if (HasItem(RerollDice)) { int[] reroll = { RollDie(), RollDie(), RollDie() }; int other = SicBoMultiplier(reroll); if (other > multiplier) { state.Values = reroll; multiplier = other; } }
                Finish(state.Stake * multiplier, "骰宝 " + string.Join("/", state.Values) + "；" + DiceOdds());
            }
            else if (state.Game == CasinoGameKind.DragonTiger)
            {
                state.Values = new[] { Next(13) + 1, Next(13) + 1 };
                int winner = state.Values[0] == state.Values[1] ? 2 : state.Values[0] > state.Values[1] ? 0 : 1;
                Finish(winner == state.Choice ? state.Stake * (winner == 2 ? 8 : 2) : 0,
                    "龙虎：龙 " + state.Values[0] + "，虎 " + state.Values[1] + "；牌1..13均匀，龙/虎各6/13概率、和1/13，毛返还2/2/8倍");
            }
            else if (state.Game == CasinoGameKind.Blackjack || state.Game == CasinoGameKind.HighLow || state.Game == CasinoGameKind.LuckyDraw || state.Game == CasinoGameKind.Bingo)
                InitializeCards();
            else InitializeParty();
        }

        private int SicBoMultiplier(int[] dice)
        {
            int total = dice[0] + dice[1] + dice[2]; bool triple = dice[0] == dice[1] && dice[1] == dice[2]; int choice = state.Choice;
            if (choice == 0) return !triple && total >= 4 && total <= 10 ? 2 : 0;
            if (choice == 1) return !triple && total >= 11 && total <= 17 ? 2 : 0;
            if (choice == 2) return triple ? 30 : 0;
            if (choice >= 10 && choice <= 15) { int count = Count(dice, choice - 9); return count == 0 ? 0 : count + 1; }
            if (choice >= 103 && choice <= 118) return total == choice - 100 ? new[] { 180,60,30,18,12,8,7,6,6,7,8,12,18,30,60,180 }[total - 3] : 0;
            if (choice >= 300) return triple && dice[0] == choice - 299 ? 180 : 0;
            int first = (choice - 200) / 6 + 1, second = (choice - 200) % 6 + 1;
            return first == second ? Count(dice, first) >= 2 ? 15 : 0 : Count(dice, first) > 0 && Count(dice, second) > 0 ? 5 : 0;
        }

        private static int Count(int[] values, int wanted) { int count = 0; foreach (int value in values) if (value == wanted) count++; return count; }

        partial void PopulateAdditionalActions(List<CasinoMiniGameActionDescriptor> actions)
        {
            switch (state.Game)
            {
                case CasinoGameKind.CoinFlip:
                    actions.Add(Action(CasinoMiniGameAction.GuessHeads, "猜正面")); actions.Add(Action(CasinoMiniGameAction.GuessTails, "猜反面"));
                    if (state.Score > 0) actions.Add(Action(CasinoMiniGameAction.CashOut, "连胜收手")); break;
                case CasinoGameKind.Blackjack: actions.Add(Action(CasinoMiniGameAction.Hit, "要牌")); actions.Add(Action(CasinoMiniGameAction.Stand, "停牌")); break;
                case CasinoGameKind.HighLow:
                    actions.Add(Action(CasinoMiniGameAction.GuessHigher, "猜更大")); actions.Add(Action(CasinoMiniGameAction.GuessLower, "猜更小"));
                    if (state.Score > 0) actions.Add(Action(CasinoMiniGameAction.CashOut, "收手")); break;
                case CasinoGameKind.LuckyDraw: actions.Add(Action(CasinoMiniGameAction.PickPrize, "抽签：选择筒号", true, 0, 5)); break;
                case CasinoGameKind.Bingo:
                    if (state.Phase == 0 && state.Selected.Length < 5) actions.Add(Action(CasinoMiniGameAction.SelectNumber, "选择一个号码", true, 1, 30));
                    if (state.Selected.Length > 0) actions.Add(Action(CasinoMiniGameAction.DrawNumber, "开下一球")); break;
                case CasinoGameKind.Plinko: if (state.Phase == 0) actions.Add(Action(CasinoMiniGameAction.DropBall, "选择落点并放球", true, 0, 6)); break;
                case CasinoGameKind.CooperativeLevers: actions.Add(Action(CasinoMiniGameAction.PullLever, "拉指定杠杆", true, 0, state.Selected.Length - 1)); break;
                case CasinoGameKind.PushYourLuckDice:
                    actions.Add(Action(CasinoMiniGameAction.RollDice, "再掷一骰")); if (state.Score > 0) actions.Add(Action(CasinoMiniGameAction.CashOut, "止盈收手")); break;
                case CasinoGameKind.PassingBag:
                    if (state.Counter == 0 && state.AuxiliaryClock >= 0) { actions.Add(Action(CasinoMiniGameAction.PassBag, "传给下一位")); actions.Add(Action(CasinoMiniGameAction.HoldBag, "扣住500毫秒")); } break;
                case CasinoGameKind.BlindAuction:
                    if (state.Phase == 0) actions.Add(Action(CasinoMiniGameAction.RevealClue, "查看价值区间"));
                    int minimumBid = state.Counter + 1;
                    if (minimumBid <= state.Stake) actions.Add(Action(CasinoMiniGameAction.Bid, "提交更高出价", true, minimumBid, (int)state.Stake));
                    actions.Add(Action(CasinoMiniGameAction.CashOut, "放弃竞拍")); break;
                case CasinoGameKind.MechanicalRace:
                    if (state.Score < 3) actions.Add(Action(CasinoMiniGameAction.Boost, "助推下注跑者"));
                    if (state.AuxiliaryClock == 0) actions.Add(Action(CasinoMiniGameAction.Dodge, "闪避护盾500毫秒")); break;
                case CasinoGameKind.CooperativeVault:
                    if (Count(state.Selected, 1) < 3) actions.Add(Action(CasinoMiniGameAction.InspectClue, "查看互补线索", true, 0, 2));
                    if (Count(state.Selected, 1) >= 2 || HasItem(Xray)) actions.Add(Action(CasinoMiniGameAction.EnterCode, "输入三位密码", true, 0, 999)); break;
                case CasinoGameKind.ChickenElevator:
                    actions.Add(Action(CasinoMiniGameAction.Climb, "选择0或1号梯再上一层", true, 0, 1));
                    if (state.Level > 0) actions.Add(Action(CasinoMiniGameAction.CashOut, "当前楼层收手")); break;
            }
        }

        partial void ActAdditionalGame(CasinoMiniGameAction action, int value, ref bool accepted)
        {
            accepted = ActCards(action, value) || ActParty(action, value);
        }

        partial void AdvanceAdditionalGame(int milliseconds)
        {
            bool timed = state.Game == CasinoGameKind.CooperativeLevers || state.Game == CasinoGameKind.PassingBag ||
                state.Game == CasinoGameKind.BlindAuction || state.Game == CasinoGameKind.MechanicalRace || state.Game == CasinoGameKind.CooperativeVault ||
                state.Game == CasinoGameKind.Bingo && state.Phase == 1 || state.Game == CasinoGameKind.Plinko && state.Phase == 1;
            if (!timed) return;
            while (milliseconds > 0 && !IsComplete)
            {
                int step = Math.Min(100 - state.TickRemainder, milliseconds);
                state.ElapsedMilliseconds += step; state.TickRemainder += step; milliseconds -= step;
                if (state.TickRemainder == 100) { state.TickRemainder = 0; TickParty(); }
            }
        }

        partial void ApplyAdditionalItem(int item)
        {
            if (state.Game == CasinoGameKind.Blackjack || state.Game == CasinoGameKind.HighLow || state.Game == CasinoGameKind.LuckyDraw || state.Game == CasinoGameKind.Bingo) ApplyCardItem(item);
            else { if (item == RerollDice && state.Game == CasinoGameKind.PushYourLuckDice) state.Limit = 1; DescribeParty(); }
        }

        // 只恢复合法形状；数组、牌序、计时范围和阶段互相校验，防止后续操作越界。
        partial void ValidateAdditionalSnapshot(ref bool valid)
        {
            bool scalars = state.Counter >= 0 && state.Score >= 0 && state.Level >= 0 && state.Cursor >= 0 && state.RemainingMilliseconds >= 0 &&
                state.Phase <= 1 && state.ElapsedMilliseconds <= 30000 && state.Payout <= state.Stake * 200 + state.Stake;
            if (!scalars) { valid = false; return; }
            switch (state.Game)
            {
                case CasinoGameKind.Slots: valid = state.Complete && Range(state.Values, 3, 0, 5); break;
                case CasinoGameKind.Roulette: valid = state.Complete && Range(state.Values, 1, 0, 36); break;
                case CasinoGameKind.CoinFlip: valid = state.Choice == 2 ? state.Score <= 5 && (state.Complete || state.Score < 5) && (state.Values.Length == 0 || Range(state.Values, 1, 0, 1)) : state.Complete && Range(state.Values, 1, 0, 1); break;
                case CasinoGameKind.Blackjack:
                    valid = state.Deck.Length == 52 && Range(state.Deck, 52, 1, 13) && state.Cursor >= 4 && state.Cursor <= 52 &&
                        state.Values.Length >= 2 && state.Secondary.Length >= 2 && state.Values.Length + state.Secondary.Length <= state.Cursor &&
                        Range(state.Values, state.Values.Length, 1, 13) && Range(state.Secondary, state.Secondary.Length, 1, 13);
                    if (valid) for (int rank = 1; rank <= 13; rank++) if (Count(state.Deck, rank) != 4) valid = false; break;
                case CasinoGameKind.SicBo: valid = state.Complete && Range(state.Values, 3, 1, 6); break;
                case CasinoGameKind.DragonTiger: valid = state.Complete && Range(state.Values, 2, 1, 13); break;
                case CasinoGameKind.HighLow: valid = Range(state.Values, 2, 1, 13) && state.Score <= 5 && (state.Complete || state.Score < 5); break;
                case CasinoGameKind.LuckyDraw: valid = Range(state.Values, 6, 0, 5); break;
                case CasinoGameKind.Bingo:
                    valid = Range(state.Deck, 30, 1, 30) && Unique(state.Deck) && state.Selected.Length <= 5 && Range(state.Selected, state.Selected.Length, 1, 30) &&
                        Unique(state.Selected) && state.Cursor <= 10 && state.Values.Length == state.Cursor && Range(state.Values, state.Cursor, 1, 30) && Unique(state.Values) && state.Score <= state.Selected.Length && state.RemainingMilliseconds <= 1000 &&
                        (state.Phase == 0 ? state.Cursor == 0 : state.Selected.Length > 0) && (state.Complete || state.Cursor < 10 && state.Score < 5);
                    if (valid) for (int i = 0; i < state.Cursor; i++) if (state.Values[i] != state.Deck[i]) valid = false;
                    if (valid) { int hits = 0; foreach (int number in state.Values) if (Array.IndexOf(state.Selected, number) >= 0) hits++; valid = hits == state.Score; } break;
                case CasinoGameKind.Plinko: valid = state.Level <= 8 && state.Cursor <= state.Level + 6 && state.RemainingMilliseconds <= 800 && (state.Complete ? state.Level == 8 : state.Level < 8); break;
                case CasinoGameKind.CooperativeLevers: valid = Range(state.Selected, Math.Max(2, state.PlayerCount), 0, 1) && state.Score <= 3 && state.RemainingMilliseconds <= 15000; break;
                case CasinoGameKind.PushYourLuckDice: valid = state.Counter <= 6 && state.Score <= 35 && state.Limit >= 0 && state.Limit <= 1; break;
                case CasinoGameKind.PassingBag: valid = state.Limit == Math.Max(2, state.PlayerCount) && state.Counter < state.Limit && state.RemainingMilliseconds <= 8000 && state.AuxiliaryClock >= -500 && state.AuxiliaryClock <= 700; break;
                case CasinoGameKind.BlindAuction: valid = Range(state.Values, 2, 0, (int)state.Stake * 2) && state.Counter <= state.Stake && state.Score <= state.Stake && state.RemainingMilliseconds <= 10000 &&
                    (state.Complete ? state.Cost == 0 || state.Cost == state.Counter : state.Cost == 0); break;
                case CasinoGameKind.MechanicalRace: valid = Range(state.Values, 4, 0, 1400) && Range(state.Secondary, 4, 0, 1500) && state.Score <= 3 && state.Target == 1200 && state.RemainingMilliseconds <= 30000 && state.AuxiliaryClock >= 0 && state.AuxiliaryClock <= 500; break;
                case CasinoGameKind.CooperativeVault: valid = Range(state.Values, 3, 1, 9) && Range(state.Selected, 3, 0, 1) && state.Target == state.Values[0] * 100 + state.Values[1] * 10 + state.Values[2] && state.Counter <= 3 && state.RemainingMilliseconds <= 20000; break;
                case CasinoGameKind.ChickenElevator: valid = Range(state.Values, 2, 0, 1) && state.Level <= 5 && (state.Complete || state.Level < 5); break;
            }
            if (state.Game != CasinoGameKind.BlindAuction && state.Cost != state.Stake) valid = false;
        }

        private static bool Range(int[] values, int length, int minimum, int maximum)
        {
            if (values.Length != length) return false; foreach (int value in values) if (value < minimum || value > maximum) return false; return true;
        }
        private static bool Unique(int[] values) { var seen = new HashSet<int>(); foreach (int value in values) if (!seen.Add(value)) return false; return true; }
    }
}
