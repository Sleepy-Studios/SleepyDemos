using System;

namespace Hotfix.JinxCasino.Rules
{
    public sealed partial class CasinoMiniGameRound
    {
        private void InitializeParty()
        {
            switch (state.Game)
            {
                case CasinoGameKind.CooperativeLevers: state.Selected = new int[Math.Max(2, state.PlayerCount)]; state.RemainingMilliseconds = 15000; break;
                case CasinoGameKind.PushYourLuckDice: state.Limit = HasItem(RerollDice) ? 1 : 0; break;
                case CasinoGameKind.PassingBag: state.Limit = Math.Max(2, state.PlayerCount); state.RemainingMilliseconds = 6000 + Next(21) * 100; break;
                case CasinoGameKind.BlindAuction:
                    state.Values = new[] { (int)(state.Stake * (50 + Next(151)) / 100), (int)(state.Stake * (20 + Next(61)) / 100) };
                    state.RemainingMilliseconds = 10000; break;
                case CasinoGameKind.MechanicalRace:
                    state.Values = new int[4]; state.Secondary = new[] { 700,900,1100,1300 }; state.RemainingMilliseconds = 30000; state.Target = 1200; break;
                case CasinoGameKind.CooperativeVault:
                    state.Values = new[] { Next(9) + 1, Next(9) + 1, Next(9) + 1 }; state.Selected = new int[3];
                    state.Target = state.Values[0] * 100 + state.Values[1] * 10 + state.Values[2]; state.RemainingMilliseconds = 20000; break;
                case CasinoGameKind.ChickenElevator: PrepareElevator(); break;
                case CasinoGameKind.Plinko: state.Cursor = state.Choice; state.RemainingMilliseconds = 800; break;
            }
            DescribeParty();
        }

        private void PrepareElevator() => state.Values = new[] { Next(10) < state.Level + 2 ? 1 : 0, Next(10) < state.Level + 2 ? 1 : 0 };

        private void DescribeParty()
        {
            switch (state.Game)
            {
                case CasinoGameKind.CooperativeLevers:
                    state.Description = "协作杠杆：剩余 " + state.RemainingMilliseconds + " 毫秒，周期位置 " + state.ElapsedMilliseconds % 2000 +
                        "；本周期完成 " + string.Join("/", state.Selected) + (state.CooperationHelpUsed ? "。杠杆i窗口[100+i*250,500+i*250]毫秒，扳手已生效" : "。杠杆i窗口[200+i*250,400+i*250]毫秒") + ",须同周期全完成，成功4倍，三次错时失败。" +
                        (state.PlayerCount == 1 ? "单人：NPC在500毫秒拉1号，你负责0号。" : "多人按编号分工，窗口失败清空本周期。"); break;
                case CasinoGameKind.PushYourLuckDice:
                    state.Description = "骰子止盈：已存 " + state.Score + " 点，上次骰 " + state.Counter + "；" + DiceOdds() +
                        "。1点爆仓，2..6累积，可收手；返还投入*(1+累计点/10)，30点自动4倍。重掷道具提供一次爆仓自动重掷。"; break;
                case CasinoGameKind.PassingBag:
                    state.Description = "烫手福袋：持有人 " + state.Counter + "，你是0号；已过 " + state.ElapsedMilliseconds + " 毫秒，传递 " + state.Score + " 次。保险丝6..8秒随机，爆炸持有人输；袋在别人手中爆炸返还2倍+最多3次传递奖励。" +
                        (state.PlayerCount == 1 ? "单人：1号NPC收到后700毫秒自动传回。" : "离线同伴按700毫秒轮转；0号可传出或扣住500毫秒。"); break;
                case CasinoGameKind.BlindAuction:
                    state.Description = "秘密盲拍：剩余 " + state.RemainingMilliseconds + " 毫秒，你的出价 " + state.Counter + "，其他竞买者出价保密" +
                        "。预算 " + state.Stake + "；暗奖价值为预算50%..200%，NPC底价20%..80%。截止时最高价成交，同价NPC优先，未成交不扣钱。" +
                        (HasItem(Xray) ? "暗奖精确价值 " + state.Values[0] + "。" : state.Phase == 1 ? "线索：暗奖价值在 " + Math.Max(0, state.Values[0] - state.Stake / 4) + ".." + (state.Values[0] + state.Stake / 4) + "。" : "可查看一次价值区间线索。"); break;
                case CasinoGameKind.MechanicalRace:
                    state.Description = "机械赛跑：下注跑者 " + state.Choice + "，位置 " + string.Join("/", state.Values) + " /1200，障碍倒计时 " +
                        string.Join("/", state.Secondary) + "。每100毫秒前进4..8，遇障碍退15；助推+100限3次，闪避护盾500毫秒；四人同速度分布，选中赢家4倍。"; break;
                case CasinoGameKind.CooperativeVault:
                    state.Description = "协作金库：剩余 " + state.RemainingMilliseconds + " 毫秒，错误 " + state.Counter + "/3。互补线索：" +
                        (state.Selected[0] != 0 || HasItem(Xray) ? state.Values[0].ToString() : "?") +
                        (state.Selected[1] != 0 || HasItem(Xray) ? state.Values[1].ToString() : "?") +
                        (state.Selected[2] != 0 || HasItem(Xray) ? state.Values[2].ToString() : "?") +
                        "；每人可查看0/1/2号线索，至少两条后可输入三位密码，成功3倍。" + (state.PlayerCount == 1 ? "NPC每2秒补一条未揭示线索。" : "请按位分工交换线索。"); break;
                case CasinoGameKind.ChickenElevator:
                    state.Description = "胆小鸡电梯：已过 " + state.Level + " 层，收手毛返还 " + new[] { 1,2,3,5,8,12 }[state.Level] +
                        " 倍；选择0或1号梯上下一层，本层每梯独立事故概率 " + (state.Level + 2) * 10 + "%；最高5层12倍，事故全失，可逐层止盈。"; break;
                case CasinoGameKind.Plinko:
                    state.Description = "弹珠：" + (state.Phase == 0 ? "选择落点0..6" : "下落层 " + state.Level + "/8，横槽 " + state.Cursor) +
                        "；每层左右各50%，落到0..14槽，槽位毛返还[20,10,5,3,2,1,0,0,0,1,2,3,5,10,20]倍。"; break;
                case CasinoGameKind.CoinFlip:
                    state.Description = "硬币连胜：已胜 " + state.Score + " 次，正反各50%，猜错全失，每胜毛返还翻倍；可继续猜或收手，最多5胜32倍。"; break;
            }
        }

        private bool ActParty(CasinoMiniGameAction action, int value)
        {
            switch (state.Game)
            {
                case CasinoGameKind.CoinFlip:
                    if (action == CasinoMiniGameAction.CashOut) { Finish(state.Stake * (1L << state.Score), "硬币连胜收手 " + state.Score); return true; }
                    int flip = Next(2); state.Values = new[] { flip };
                    if (flip != (action == CasinoMiniGameAction.GuessHeads ? 0 : 1)) Finish(0, "连胜硬币猜错：" + flip);
                    else if (++state.Score == 5) Finish(state.Stake * 32, "硬币五连胜"); else DescribeParty(); return true;
                case CasinoGameKind.Plinko: state.Cursor = value; state.Phase = 1; DescribeParty(); return true;
                case CasinoGameKind.CooperativeLevers:
                    if (state.Selected[value] != 0) return false;
                    long cycle = state.ElapsedMilliseconds % 2000; int start = 200 + value * 250;
                    int margin = state.CooperationHelpUsed ? 100 : 0;
                    if (cycle >= start - margin && cycle <= start + 200 + margin) { state.Selected[value] = 1; CheckLevers(); }
                    else { Array.Clear(state.Selected, 0, state.Selected.Length); if (++state.Score >= 3) Finish(0, "杠杆三次错时"); }
                    if (!IsComplete) DescribeParty(); return true;
                case CasinoGameKind.PushYourLuckDice:
                    if (action == CasinoMiniGameAction.CashOut) { Finish(state.Stake * (10 + state.Score) / 10, "骰子止盈 " + state.Score + " 点"); return true; }
                    int die = RollDie(); if (die == 1 && state.Limit > 0) { state.Limit--; die = RollDie(); }
                    state.Counter = die;
                    if (die == 1) Finish(0, "骰子爆仓，掷到1"); else { state.Score += die; if (state.Score >= 30) Finish(state.Stake * 4, "骰子累计30点达标"); else DescribeParty(); } return true;
                case CasinoGameKind.PassingBag:
                    if (action == CasinoMiniGameAction.PassBag) { state.Counter = 1; state.Score++; state.AuxiliaryClock = 0; }
                    else state.AuxiliaryClock = -500;
                    DescribeParty(); return true;
                case CasinoGameKind.BlindAuction:
                    if (action == CasinoMiniGameAction.CashOut) { state.Cost = 0; Finish(0, "放弃盲拍，未成交"); return true; }
                    if (action == CasinoMiniGameAction.RevealClue) { state.Phase = 1; DescribeParty(); return true; }
                    if (value <= state.Counter) return false;
                    state.Counter = value; DescribeParty(); return true;
                case CasinoGameKind.MechanicalRace:
                    if (action == CasinoMiniGameAction.Boost) { state.Score++; state.Values[state.Choice] += 100; }
                    else state.AuxiliaryClock = 500;
                    CheckRace(false); if (!IsComplete) DescribeParty(); return true;
                case CasinoGameKind.CooperativeVault:
                    if (action == CasinoMiniGameAction.InspectClue) { if (state.Selected[value] != 0) return false; state.Selected[value] = 1; }
                    else if (value == state.Target) { state.ObjectiveSuccess = true; Finish(state.Stake * 3, "金库开启，密码 " + state.Target); return true; }
                    else if (++state.Counter >= 3) { Finish(0, "金库三次错误锁死"); return true; }
                    DescribeParty(); return true;
                case CasinoGameKind.ChickenElevator:
                    if (action == CasinoMiniGameAction.CashOut) { Finish(state.Stake * new[] { 1,2,3,5,8,12 }[state.Level], "电梯在 " + state.Level + " 层收手"); return true; }
                    if (state.Values[value] == 1) Finish(0, "电梯在第 " + (state.Level + 1) + " 层事故");
                    else if (++state.Level == 5) Finish(state.Stake * 12, "电梯五层登顶"); else { PrepareElevator(); DescribeParty(); } return true;
            }
            return false;
        }

        private void CheckLevers()
        {
            foreach (int pulled in state.Selected) if (pulled == 0) return;
            Finish(state.Stake * 4, "全部杠杆在同周期窗口完成");
        }

        private void CheckRace(bool timeout)
        {
            int winner = 0; for (int i = 1; i < 4; i++) if (state.Values[i] > state.Values[winner]) winner = i;
            if (timeout || state.Values[winner] >= state.Target) Finish(winner == state.Choice ? state.Stake * 4 : 0, "机械赛跑赢家 " + winner + "，位置 " + string.Join("/", state.Values));
        }

        // 所有自动行为只发生在固定规则 tick，保存未满 tick 的余量保证不同帧分割一致。
        private void TickParty()
        {
            state.RemainingMilliseconds = Math.Max(0, state.RemainingMilliseconds - 100);
            switch (state.Game)
            {
                case CasinoGameKind.Bingo:
                    if (state.RemainingMilliseconds == 0) { state.RemainingMilliseconds = 1000; DrawBingo(); } return;
                case CasinoGameKind.Plinko:
                    state.Cursor += Next(2); state.Level++;
                    if (state.Level == 8) Finish(state.Stake * new[] { 20,10,5,3,2,1,0,0,0,1,2,3,5,10,20 }[state.Cursor], "弹珠落到 " + state.Cursor + " 槽"); break;
                case CasinoGameKind.CooperativeLevers:
                    long cycle = state.ElapsedMilliseconds % 2000;
                    if (cycle == 0) Array.Clear(state.Selected, 0, state.Selected.Length);
                    if (state.PlayerCount == 1 && cycle == 500) { state.Selected[1] = 1; CheckLevers(); }
                    if (state.RemainingMilliseconds == 0) Finish(0, "杠杆超时"); break;
                case CasinoGameKind.PassingBag:
                    if (state.Counter != 0 || state.AuxiliaryClock < 0) state.AuxiliaryClock += 100;
                    if (state.RemainingMilliseconds == 0) { Finish(state.Counter == 0 ? 0 : state.Stake * (2 + Math.Min(3, state.Score)), "福袋在 " + state.Counter + " 号手中爆炸"); break; }
                    if (state.Counter != 0 && state.AuxiliaryClock >= 700) { state.Counter = (state.Counter + 1) % state.Limit; state.Score++; state.AuxiliaryClock = 0; } break;
                case CasinoGameKind.BlindAuction:
                    if (state.ElapsedMilliseconds % 1000 == 0) state.Score = Math.Min(state.Values[1], state.Score + Math.Max(1, (int)state.Stake / 10));
                    if (state.RemainingMilliseconds == 0)
                    {
                        bool won = state.Counter > state.Score; state.Cost = won ? state.Counter : 0;
                        Finish(won ? state.Values[0] : 0, won ? "盲拍成交，成交价 " + state.Counter + "，奖值 " + state.Values[0] : "NPC赢得盲拍，未扣款");
                    } break;
                case CasinoGameKind.MechanicalRace:
                    for (int i = 0; i < 4; i++)
                    {
                        state.Values[i] += 4 + Next(5); state.Secondary[i] -= 100;
                        if (state.Secondary[i] <= 0) { if (i != state.Choice || state.AuxiliaryClock <= 0) state.Values[i] = Math.Max(0, state.Values[i] - 15); state.Secondary[i] = 700 + Next(8) * 100; }
                    }
                    state.AuxiliaryClock = Math.Max(0, state.AuxiliaryClock - 100); CheckRace(state.RemainingMilliseconds == 0); break;
                case CasinoGameKind.CooperativeVault:
                    if (state.PlayerCount == 1 && state.ElapsedMilliseconds % 2000 == 0)
                        for (int i = 0; i < 3; i++) if (state.Selected[i] == 0) { state.Selected[i] = 1; break; }
                    if (state.RemainingMilliseconds == 0) Finish(0, "金库超时锁死"); break;
            }
            if (!IsComplete) DescribeParty();
        }
    }
}
