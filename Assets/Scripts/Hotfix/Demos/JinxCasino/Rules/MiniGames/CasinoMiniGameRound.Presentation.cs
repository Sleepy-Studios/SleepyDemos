using System;

namespace Hotfix.JinxCasino.Rules
{
    /// 表现层公开数据；没有牌堆、随机状态或未揭示密码，修改数组不会改变规则局。
    [Serializable]
    public sealed class CasinoMiniGamePresentation
    {
        public CasinoGameKind Game;
        public bool IsComplete;
        public bool IsObjectiveSuccess;
        public bool CooperationHelpUsed;
        public int Choice;
        public long Cost;
        public long Payout;
        public int[] NumberValues = Array.Empty<int>();
        public int[] SecondaryValues = Array.Empty<int>();
        public int[] SelectedValues = Array.Empty<int>();
        public long ElapsedMilliseconds;
        public int RemainingMilliseconds;
        public int Score;
        public int Level;
        public int Cursor;
        public int Phase;
        public int OperationCount;
    }

    public sealed partial class CasinoMiniGameRound
    {
        /// 返回当前已经公开的牌、骰、位置和线索副本，表现不能从此入口窥视下一张牌或暗奖。
        public CasinoMiniGamePresentation GetPresentation()
        {
            var view = new CasinoMiniGamePresentation
            {
                Game = state.Game, IsComplete = state.Complete, IsObjectiveSuccess = IsObjectiveSuccess, Choice = state.Choice,
                CooperationHelpUsed = state.CooperationHelpUsed,
                Cost = state.Complete ? state.Cost : 0, Payout = state.Complete ? state.Payout : 0,
                ElapsedMilliseconds = state.ElapsedMilliseconds, RemainingMilliseconds = state.RemainingMilliseconds,
                Score = state.Game == CasinoGameKind.BlindAuction ? state.Counter : state.Score,
                Level = state.Level, Cursor = state.Game == CasinoGameKind.Plinko ? state.Cursor : 0, Phase = state.Phase, OperationCount = state.OperationCount,
                SelectedValues = (int[])state.Selected.Clone()
            };
            if (state.Game == CasinoGameKind.HighLow && !state.Complete && !HasItem(Xray))
                view.NumberValues = new[] { state.Values[0] };
            else if (state.Game == CasinoGameKind.LuckyDraw && !state.Complete && !HasItem(Xray))
                view.NumberValues = Array.Empty<int>();
            else if (state.Game == CasinoGameKind.BlindAuction)
                view.NumberValues = state.Complete || HasItem(Xray) ? new[] { state.Values[0] } : Array.Empty<int>();
            else if (state.Game == CasinoGameKind.CooperativeVault && !state.Complete && !HasItem(Xray))
            {
                view.NumberValues = new int[3];
                for (int index = 0; index < 3; index++) if (state.Selected[index] != 0) view.NumberValues[index] = state.Values[index];
            }
            else if (state.Game == CasinoGameKind.ChickenElevator && !state.Complete && !HasItem(Xray))
                view.NumberValues = Array.Empty<int>();
            else if (state.Game == CasinoGameKind.PushYourLuckDice) view.NumberValues = new[] { state.Counter };
            else view.NumberValues = (int[])state.Values.Clone();
            if (state.Game == CasinoGameKind.Blackjack)
                view.SecondaryValues = state.Complete || HasItem(Xray) ? (int[])state.Secondary.Clone() : new[] { state.Secondary[0] };
            else if (state.Game == CasinoGameKind.MechanicalRace) view.SecondaryValues = (int[])state.Secondary.Clone();
            return view;
        }
    }
}
