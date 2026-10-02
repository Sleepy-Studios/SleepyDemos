using System;

namespace Hotfix.JinxCasino.Rules
{
    public sealed partial class CasinoMiniGameRound
    {
        // 洗牌与牌序一起存档；恢复绝不再次洗牌或补抽已经展示的牌。
        private int[] Shuffled(int count, int modulus)
        {
            var deck = new int[count];
            for (int i = 0; i < count; i++) deck[i] = i % modulus + 1;
            for (int i = count - 1; i > 0; i--) { int other = Next(i + 1); int value = deck[i]; deck[i] = deck[other]; deck[other] = value; }
            return deck;
        }

        private static int[] Append(int[] values, int value)
        {
            var result = new int[values.Length + 1];
            Array.Copy(values, result, values.Length); result[values.Length] = value; return result;
        }

        private static int CardTotal(int[] cards)
        {
            int total = 0, aces = 0;
            foreach (int card in cards) { total += card == 1 ? 11 : Math.Min(card, 10); if (card == 1) aces++; }
            while (total > 21 && aces-- > 0) total -= 10;
            return total;
        }

        private void InitializeCards()
        {
            if (state.Game == CasinoGameKind.Blackjack)
            {
                state.Deck = Shuffled(52, 13); state.Cursor = 4;
                state.Values = new[] { state.Deck[0], state.Deck[2] }; state.Secondary = new[] { state.Deck[1], state.Deck[3] };
                if (HasItem(RedrawCard)) state.Values[1] = state.Deck[state.Cursor++];
                if (CardTotal(state.Values) == 21 || CardTotal(state.Secondary) == 21) ResolveBlackjack(); else DescribeCards();
            }
            else if (state.Game == CasinoGameKind.HighLow)
            {
                state.Values = new[] { Next(13) + 1, Next(13) + 1 };
                if (HasItem(RedrawCard)) state.Values[1] = Next(13) + 1;
                DescribeCards();
            }
            else if (state.Game == CasinoGameKind.LuckyDraw)
            {
                int risk = state.Choice / 10;
                state.Values = risk == 0 ? new[] { 0,0,1,1,1,2 } : risk == 1 ? new[] { 0,0,0,1,2,3 } : new[] { 0,0,0,0,0,5 };
                for (int i = 5; i > 0; i--) { int other = Next(i + 1); int value = state.Values[i]; state.Values[i] = state.Values[other]; state.Values[other] = value; }
                DescribeCards();
            }
            else if (state.Game == CasinoGameKind.Bingo)
            {
                state.Deck = Shuffled(30, 30); state.RemainingMilliseconds = 1000; DescribeCards();
            }
        }

        private void DescribeCards()
        {
            switch (state.Game)
            {
                case CasinoGameKind.Blackjack:
                    state.Description = "21点：你的牌 " + string.Join("/", state.Values) + "，点数 " + CardTotal(state.Values) +
                        "；庄家 " + state.Secondary[0] + (HasItem(Xray) ? "/" + state.Secondary[1] : "/暗牌") +
                        "。可要牌或停牌；庄家17停，赢2倍、和局1倍、天然21点2.5倍，爆牌零。"; break;
                case CasinoGameKind.HighLow:
                    state.Description = "比大小：当前 " + state.Values[0] + "，下一张" + (HasItem(Xray) ? state.Values[1].ToString() : "未知") +
                        "；已连胜 " + state.Score + " 次。牌面1..13等概率，严格大/小，相等算输；每胜毛返还翻倍，最多5胜。"; break;
                case CasinoGameKind.LuckyDraw:
                    state.Description = "签筒：风险档 " + state.Choice / 10 + "，初选筒 " + state.Choice % 10 +
                        "；输入筒号0..5抽签。档0签值[0,0,1,1,1,2]，档1[0,0,0,1,2,3]，档2[0,0,0,0,0,5]，每筒等概率，数字为毛返还倍数。" +
                        (HasItem(Xray) ? "透视筒值：" + string.Join("/", state.Values) : ""); break;
                case CasinoGameKind.Bingo:
                    state.Description = "宾果：选号 " + string.Join("/", state.Selected) + "；已开 " + string.Join("/", state.Values) +
                        "，命中 " + state.Score + "。从1..30选最多5个不同号码后开奖；共开10球，开始后每秒自动开一球，也可手动开。命中0/1/2/3/4/5毛返还0/0/1/2/5/15倍。"; break;
            }
        }

        private void ResolveBlackjack()
        {
            int player = CardTotal(state.Values);
            while (player <= 21 && CardTotal(state.Secondary) < 17 && state.Cursor < 52)
                state.Secondary = Append(state.Secondary, state.Deck[state.Cursor++]);
            int dealer = CardTotal(state.Secondary);
            bool natural = state.Values.Length == 2 && player == 21;
            bool dealerNatural = state.Secondary.Length == 2 && dealer == 21;
            long payout = player > 21 ? 0 : natural && !dealerNatural ? state.Stake * 5 / 2 : dealerNatural && !natural ? 0 :
                player == dealer ? state.Stake : dealer > 21 || player > dealer ? state.Stake * 2 : 0;
            Finish(payout, "21点结算：你 " + player + "，庄家 " + dealer + "（" + string.Join("/", state.Secondary) + "）");
        }

        private bool ActCards(CasinoMiniGameAction action, int value)
        {
            switch (state.Game)
            {
                case CasinoGameKind.Blackjack:
                    if (action == CasinoMiniGameAction.Hit)
                    {
                        if (state.Cursor >= state.Deck.Length) return false;
                        state.Values = Append(state.Values, state.Deck[state.Cursor++]);
                        if (CardTotal(state.Values) >= 21) ResolveBlackjack(); else DescribeCards(); return true;
                    }
                    if (action == CasinoMiniGameAction.Stand) { ResolveBlackjack(); return true; } break;
                case CasinoGameKind.HighLow:
                    if (action == CasinoMiniGameAction.CashOut) { Finish(state.Stake * (1L << state.Score), "比大小收手，连胜 " + state.Score); return true; }
                    if (action == CasinoMiniGameAction.GuessHigher || action == CasinoMiniGameAction.GuessLower)
                    {
                        int next = state.Values[1];
                        bool won = action == CasinoMiniGameAction.GuessHigher ? next > state.Values[0] : next < state.Values[0];
                        if (!won) Finish(0, "比大小猜错或相等，翻到 " + next);
                        else { state.Score++; state.Values[0] = next; if (state.Score == 5) Finish(state.Stake * 32, "比大小五连胜"); else { state.Values[1] = Next(13) + 1; DescribeCards(); } }
                        return true;
                    } break;
                case CasinoGameKind.LuckyDraw:
                    if (action == CasinoMiniGameAction.PickPrize) { Finish(state.Stake * state.Values[value], "签筒 " + value + " 抽到 " + state.Values[value] + " 倍签"); return true; } break;
                case CasinoGameKind.Bingo:
                    if (action == CasinoMiniGameAction.SelectNumber)
                    {
                        if (Array.IndexOf(state.Selected, value) >= 0 || state.Selected.Length >= 5 || state.Phase != 0) return false;
                        state.Selected = Append(state.Selected, value); DescribeCards(); return true;
                    }
                    if (action == CasinoMiniGameAction.DrawNumber) { state.Phase = 1; DrawBingo(); return true; } break;
            }
            return false;
        }

        private void DrawBingo()
        {
            int number = state.Deck[state.Cursor++]; state.Values = Append(state.Values, number);
            if (Array.IndexOf(state.Selected, number) >= 0) state.Score++;
            if (state.Cursor == 10 || state.Score == 5)
                Finish(state.Stake * new[] { 0,0,1,2,5,15 }[state.Score], "宾果命中 " + state.Score + " 个，开奖号 " + string.Join("/", state.Values));
            else DescribeCards();
        }

        private void ApplyCardItem(int item)
        {
            if (item == RedrawCard)
            {
                if (state.Game == CasinoGameKind.Blackjack)
                {
                    state.Values[state.Values.Length - 1] = state.Deck[state.Cursor++];
                    if (CardTotal(state.Values) >= 21) ResolveBlackjack();
                }
                else if (state.Game == CasinoGameKind.HighLow) state.Values[1] = Next(13) + 1;
            }
            if (!IsComplete) DescribeCards();
        }
    }
}
