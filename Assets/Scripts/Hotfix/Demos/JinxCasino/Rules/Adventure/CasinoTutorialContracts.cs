using System;

namespace Hotfix.JinxCasino.Rules
{
    public enum CasinoTutorialStatus { None = 0, Active = 1, Completed = 2, Skipped = 3 }
    public enum CasinoTutorialStep
    {
        Look = 0, Walk = 1, EnterSlots = 2, AddChips = 3, Confirm = 4, SlotsResult = 5,
        LeaveSlots = 6, Blackjack = 7, BuyWrench = 8, UseWrench = 9, Levers = 10,
        Ready = 11, Completed = 12, Skipped = 13
    }
    /// 宿主只报告已经发生的动作；RoundPresented的value为真实结算序号。
    public enum CasinoTutorialFact
    {
        LookApplied = 0, WalkApplied = 1, TableEntered = 2, ChipsAdded = 3, ChipsConfirmed = 4,
        RoundPresented = 5, TableLeft = 6, ItemPurchased = 7, ItemUsed = 8, Continue = 9
    }

    [Serializable]
    public sealed class CasinoTutorialState
    {
        public int Version = 1;
        public CasinoTutorialStatus Status;
        public CasinoTutorialStep Step;
        public string StationId;
        public int SettlementBaseline;
        /// 记录版本基线不依赖ProcessedRequests的数组位置，裁剪旧记录不会误认之前的购买。
        public int ReceiptBaselineRevision;
        public int CompletedGameMask;
        /// 实际应用视角的累计毫度，15000封顶。
        public int LookMillidegrees;
        /// 已发生平面位移累计毫米，600封顶；达标且靠近水果机才能继续。
        public int WalkMillimeters;
        public int WrenchInventoryBaseline;
        public int WrenchChargesBaseline;
    }
}
