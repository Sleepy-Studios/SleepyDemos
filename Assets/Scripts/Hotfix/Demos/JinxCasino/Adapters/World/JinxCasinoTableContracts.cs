using Hotfix.JinxCasino.Rules;

namespace Hotfix.JinxCasino.Adapters
{
    /// 桌面控制的薄宿主边界；快照必须复用宿主已有副本，不能在getter重新Capture领域。
    public interface IJinxCasinoTableOperations
    {
        CasinoAdventureState AdventureState { get; }
        string StationId { get; }
        CasinoGameKind StationGame { get; }
        bool IsStationAvailable { get; }
        string AdventureBetRules { get; }
        string ActiveRoundDescription { get; }
        /// 当前活动或已完成局的公开投影，不能读取隐藏牌堆。
        CasinoMiniGamePresentation GetAdventurePresentation();
        /// 当前局的真实合法动作。
        CasinoMiniGameActionDescriptor[] GetAdventureActions();
        /// 按当前场景配置和区域开放的机台。
        CasinoGameDefinition[] GetAvailableAdventureGames();
        /// <summary>通过现有宿主提交明确投入，不预先扣款或构造第二套规则。</summary>
        /// <param name="requestId">同一操作重试保留的唯一编号。</param>
        /// <param name="game">实际机台玩法。</param>
        /// <param name="stake">已确认的最高投入。</param>
        /// <param name="choice">S1三款均为0。</param>
        /// <param name="stationId">具体已保存机台ID。</param>
        CasinoAdventureResult BeginAdventureGame(string requestId, CasinoGameKind game, long stake, int choice, string stationId);
        /// <summary>通过现有宿主操作已投入且属于本机台的局。</summary>
        /// <param name="requestId">同一动作重试保留的编号。</param>
        /// <param name="action">由真实小游戏允许的动作。</param>
        /// <param name="value">当前动作的参数，单人拉杆仅0。</param>
        /// <param name="stationId">具体已保存机台ID。</param>
        CasinoAdventureResult ActInAdventure(string requestId, CasinoMiniGameAction action, int value, string stationId);
    }

    public readonly struct JinxCasinoTableAvailability
    {
        public bool IsAvailable { get; }
        public string Reason { get; }
        internal JinxCasinoTableAvailability(bool available, string reason = null) { IsAvailable = available; Reason = available ? null : reason; }
    }

    /// 本地输入回执只有提示和请求身份，效果与钱包刷新仍由既有Controller处理。
    public sealed class JinxCasinoTableResult
    {
        public bool Success { get; }
        public bool Changed { get; }
        public string Error { get; }
        public string Description { get; }
        public string RequestId { get; }
        internal JinxCasinoTableResult(bool success, bool changed, string error, string description, string requestId = null)
        { Success = success; Changed = changed; Error = error; Description = description; RequestId = requestId; }
        internal JinxCasinoTableResult WithoutChange() => new JinxCasinoTableResult(Success, false, Error, Description, RequestId);
    }

    /// 单次展示数据。宿主先取一次View，再按每个保存目标的Action/Value查询，不重复读取规则。
    public sealed class JinxCasinoTableView
    {
        public string StationId { get; internal set; }
        public CasinoGameKind Game { get; internal set; }
        public long DraftStake { get; internal set; }
        public long MaximumDraft { get; internal set; }
        public bool IsSlotsPrepared { get; internal set; }
        public bool HasOwnActiveRound { get; internal set; }
        public bool HasOtherActiveRound { get; internal set; }
        public bool LeverWindowOpen { get; internal set; }
        public int SettlementSequence { get; internal set; }
        public string Description { get; internal set; }
        public string RulesText { get; internal set; }
        public string CommitLabel { get; internal set; }
        public string PrimaryLabel { get; internal set; }
        public string SecondaryLabel { get; internal set; }
        public CasinoMiniGamePresentation Presentation { get; internal set; }
        internal bool IsOpen;
        internal bool CanStart;
        internal bool CanHit;
        internal bool CanStand;
        internal bool CanPull;
        internal bool IsSinglePlayer;
        internal string BlockReason;
        internal string PreparationReason;

        /// <summary>查询具体保存物件的可用性；数值为面额或杠杆编号，不映射为未声明的动作。</summary>
        /// <param name="action">已有TableTarget保存的设备无关动作。</param>
        /// <param name="value">ChipAdd仅10/50/100，其它动作除Pull外均0。</param>
        public JinxCasinoTableAvailability GetAvailability(JinxCasinoTableAction action, int value = 0)
        {
            if (!IsOpen) return No("已经离开此机台。");
            if (action == JinxCasinoTableAction.Help) return value == 0 ? Yes() : No("帮助参数无效。");
            if (action == JinxCasinoTableAction.ChipClear) return value != 0 ? No("清空参数无效。") : DraftStake > 0 || IsSlotsPrepared ? Yes() : No("没有待清空的筹码草稿。");
            if (BlockReason != null) return No(BlockReason);
            if (action == JinxCasinoTableAction.ChipAdd)
            {
                if (value != 10 && value != 50 && value != 100) return No("请选择10、50或100面额。");
                if (!CanStart) return No("请先完成已投入局或当前阶段。");
                return DraftStake <= MaximumDraft - value ? Yes() : No("超过当前可用筹码或最高投入。");
            }
            if (action == JinxCasinoTableAction.Commit)
            {
                if (value != 0) return No("确认参数无效。");
                if (!CanStart) return No("请先完成已投入局或当前阶段。");
                if (DraftStake <= 0 || DraftStake > MaximumDraft) return No("先放入可承担的筹码。");
                return IsSlotsPrepared ? No("已经准备，请拉柄明确投入。") : Yes();
            }
            if (action == JinxCasinoTableAction.Primary)
            {
                if (Game == CasinoGameKind.Slots)
                    return value == 0 && IsSlotsPrepared && CanStart ? Yes() : No(PreparationReason ?? "先确认筹码，再拉柄投入。");
                if (Game == CasinoGameKind.Blackjack) return value == 0 && CanHit ? Yes() : No("先确认投入发牌，再决定要牌。");
                if (IsSinglePlayer && value != 0) return No("这根杠杆由助手负责。");
                if (!CanPull || Presentation == null || value < 0 || value >= Presentation.SelectedValues.Length) return No("先确认投入启动合拍拉杆。");
                return Presentation.SelectedValues[value] == 0 ? Yes() : No("本周期这根杠杆已经拉下。");
            }
            if (action == JinxCasinoTableAction.Secondary)
                return value == 0 && Game == CasinoGameKind.Blackjack && CanStand ? Yes() : No("当前没有可停牌的局。");
            return No("此物件动作尚不适用于本机台。");
        }

        private static JinxCasinoTableAvailability Yes() => new JinxCasinoTableAvailability(true);
        private static JinxCasinoTableAvailability No(string reason) => new JinxCasinoTableAvailability(false, reason);
    }
}
