using System;
using System.Linq;
using Hotfix.JinxCasino.Rules;

namespace Hotfix.JinxCasino.Interaction
{
    /// S1三款物理机台控制。只保留未提交草稿，全部资金、随机、局与幂等仍属于原冒险聚合。
    public sealed class JinxCasinoTableSession
    {
        private readonly JinxCasinoGame gameFlow;
        private readonly JinxCasinoStation station;
        private readonly string stationId;
        private readonly CasinoGameKind game;
        private long draftStake;
        private bool slotsPrepared;
        private string preparationKey;
        private string preparationReason;
        private bool isOpen = true;
        private int lastFrame = -1;
        private string lastLocalFingerprint;
        private JinxCasinoTableResult lastFrameResult;
        private DomainCommand lastCommand;
        private CasinoAdventureState cachedState;
        private CasinoMiniGamePresentation cachedPresentation;
        private CasinoMiniGameActionDescriptor[] cachedActions = Array.Empty<CasinoMiniGameActionDescriptor>();
        private CasinoGameDefinition[] cachedGames = Array.Empty<CasinoGameDefinition>();

        public string StationId => stationId;
        public CasinoGameKind Game => game;
        public bool IsOpen => isOpen;
        /// 只用于当前本地操作重试，不是存档中另一局的支付入口。
        public string LastRequestId => lastCommand?.RequestId;

        /// <summary>进入具体机台建立本地草稿；重返只读取原机台已投入局，构造不开奖或支付。</summary>
        /// <param name="flow">本场景唯一游戏对象，提供既有缓存状态和规则操作。</param>
        /// <param name="table">已保存的具体机台，不按游戏类型猜测身份。</param>
        public JinxCasinoTableSession(JinxCasinoGame flow, JinxCasinoStation table)
        {
            gameFlow = flow ?? throw new ArgumentNullException(nameof(flow));
            station = table != null ? table : throw new ArgumentNullException(nameof(table));
            if (!ValidId(table.StationId)) throw new ArgumentException("需要具体保存的机台ID。", nameof(table));
            if (table.Game != CasinoGameKind.Slots && table.Game != CasinoGameKind.Blackjack && table.Game != CasinoGameKind.CooperativeLevers)
                throw new ArgumentOutOfRangeException(nameof(table), "S1仅接入三款样板机台。");
            stationId = table.StationId; game = table.Game;
        }

        /// 读取当前本台可见状态；不会Capture新领域快照、绑定其它局、随机或支付。
        public JinxCasinoTableView GetView()
        {
            var state = ReadState(); RefreshPreparation(state);
            bool hasRound = state != null && !string.IsNullOrEmpty(state.ActiveRoundJson);
            bool owns = hasRound && state.ActiveGame == game && state.ActiveStationId == stationId;
            bool canStart = state != null && !hasRound && state.Phase == CasinoAdventurePhase.Playing
                && (state.Mode == CasinoAdventureMode.Practice || state.RemainingMilliseconds > 0) && cachedGames.Any(entry => entry.Kind == game);
            long maximum = state == null ? 0 : Math.Min(state.Coins - state.LockedCoins,
                state.EventStakeCap > 0 ? Math.Min(state.Config.MaximumStake, state.EventStakeCap) : state.Config.MaximumStake);
            string blocked = !StationMatches ? "此机台已经关闭或玩法改变，请重新进入。" : state == null ? "请先开始一场冒险。" :
                hasRound && !owns ? "请返回原来投入的机台完成活动局。" : null;
            var view = new JinxCasinoTableView
            {
                StationId = stationId, Game = game, IsOpen = isOpen, DraftStake = draftStake, MaximumDraft = Math.Max(0, maximum),
                IsSlotsPrepared = slotsPrepared, HasOwnActiveRound = owns,
                HasOtherActiveRound = hasRound && !owns, CanStart = canStart, BlockReason = blocked,
                PreparationReason = preparationReason, IsSinglePlayer = state == null || state.PlayerCount == 1,
                CommitLabel = game == CasinoGameKind.Slots ? "确认筹码，准备拉柄" : "确认投入并开始",
                PrimaryLabel = game == CasinoGameKind.Slots ? "拉下投入 " + draftStake + " 筹码" : game == CasinoGameKind.Blackjack ? "要一张牌" : "拉下自己的杠杆",
                SecondaryLabel = game == CasinoGameKind.Blackjack ? "停牌结算" : string.Empty,
                CanHit = owns && HasAction(CasinoMiniGameAction.Hit), CanStand = owns && HasAction(CasinoMiniGameAction.Stand),
                CanPull = owns && HasAction(CasinoMiniGameAction.PullLever)
            };
            bool showLast = state != null && !hasRound && state.LastStationId == stationId && draftStake == 0;
            if ((owns || showLast) && cachedPresentation?.Game == game) view.Presentation = CopyPresentation(cachedPresentation);
            view.SettlementSequence = showLast && view.Presentation != null ? state.SettledRoundSequence : 0;
            bool helped = owns ? view.Presentation?.CooperationHelpUsed == true : state?.CooperationHelpCharges > 0;
            view.RulesText = LocalRules(state, view.MaximumDraft, helped);
            view.Description = blocked ?? (owns ? ActiveDescription(view.Presentation) :
                slotsPrepared ? "筹码已准备，尚未扣款；拉下将投入 " + draftStake + " 筹码。" :
                showLast ? "上一局已结算 · 投入 " + state.LastRoundCost + " · 返还 " + state.LastRoundPayout + " 筹码" : preparationReason ?? "放入筹码并明确确认。");
            if (view.CanPull && view.Presentation != null)
            {
                long cycle = view.Presentation.ElapsedMilliseconds % 2000;
                int margin = view.Presentation.CooperationHelpUsed ? 100 : 0;
                view.LeverWindowOpen = cycle >= 200 - margin && cycle <= 400 + margin;
            }
            return view;
        }

        /// <summary>保存物件或统一输入动作的唯一入口，同帧相同动作不重复，不同动作拒绝混入。</summary>
        /// <param name="action">目标保存的设备无关动作。</param>
        /// <param name="value">面额或杠杆编号，必须通过本台可用性校验。</param>
        /// <param name="frameId">宿主Time.frameCount，同一次帧处理稳定；不能使用随机或递增按钮计数。</param>
        public JinxCasinoTableResult Apply(JinxCasinoTableAction action, int value, int frameId)
        {
            string fingerprint = "target:" + (int)action + ":" + value;
            if (!TryFrame(frameId, fingerprint, out var replay)) return replay;
            var view = GetView(); var available = view.GetAvailability(action, value);
            if (!available.IsAvailable) return Finish(Fail("Unavailable", available.Reason));
            if (action == JinxCasinoTableAction.Help) return Finish(new JinxCasinoTableResult(true, false, null, view.RulesText));
            if (action == JinxCasinoTableAction.ChipAdd)
            {
                draftStake += value; InvalidatePreparation(null);
                return Finish(Local("已放入 " + draftStake + " 筹码草稿，尚未投入。"));
            }
            if (action == JinxCasinoTableAction.ChipClear)
            {
                ClearDraft(); return Finish(Local("筹码草稿已清空，已投入局保持原状态。"));
            }
            var state = gameFlow.State;
            if (action == JinxCasinoTableAction.Commit)
            {
                if (game == CasinoGameKind.Slots)
                {
                    slotsPrepared = true; preparationKey = PreparationKey(state); preparationReason = null;
                    return Finish(Local("已确认草稿；拉下将投入 " + draftStake + " 筹码。"));
                }
                return Finish(Send(NewCommand(CommandKind.Begin, state), frameId));
            }
            if (game == CasinoGameKind.Slots) return Finish(Send(NewCommand(CommandKind.Begin, state), frameId));
            var command = NewCommand(CommandKind.Act, state);
            command.Action = game == CasinoGameKind.Blackjack ? action == JinxCasinoTableAction.Primary ? CasinoMiniGameAction.Hit : CasinoMiniGameAction.Stand : CasinoMiniGameAction.PullLever;
            command.Value = value;
            return Finish(Send(command, frameId));
        }

        /// <summary>重试最近一条领域命令，保留请求与原参数；不复用到新局或更早的恢复检查点。</summary>
        /// <param name="frameId">当前宿主帧，同帧第二次输入仍拒绝。</param>
        public JinxCasinoTableResult RetryLastCommand(int frameId)
        {
            if (!TryFrame(frameId, "retry:" + LastRequestId, out var replay)) return replay;
            var state = ReadState(); RefreshPreparation(state);
            if (!isOpen || !StationMatches || lastCommand == null || state == null || state.RunId != lastCommand.RunId)
                return Finish(Fail("StaleCommand", "请重新进入机台并确认本次操作。"));
            var receipt = state.ProcessedRequests.Find(entry => entry.RequestId == lastCommand.RequestId);
            if (lastCommand.AppliedLocally && receipt == null) return Finish(Fail("RestoredEarlierState", "已恢复较早进度，请重新确认操作。"));
            if (receipt == null && (state.StageIndex != lastCommand.StageIndex ||
                lastCommand.Kind == CommandKind.Begin && game == CasinoGameKind.Slots && (!slotsPrepared || preparationKey != lastCommand.PreparationKey)))
                return Finish(Fail("PreparationChanged", "阶段或投入规则已改变，请重新确认。"));
            return Finish(Send(lastCommand, frameId));
        }

        /// 离开只取消本地草稿，已投入局仍在原机台；重返创建新Session读取原快照。
        public void Close() { isOpen = false; ClearDraft(); }

        private CasinoAdventureState ReadState()
        {
            var state = gameFlow.State;
            if (!ReferenceEquals(state, cachedState))
            {
                cachedState = state; cachedPresentation = gameFlow.GetPresentation();
                cachedActions = gameFlow.GetActions() ?? Array.Empty<CasinoMiniGameActionDescriptor>();
                cachedGames = gameFlow.GetAvailableGames() ?? Array.Empty<CasinoGameDefinition>();
            }
            return state;
        }

        private bool StationMatches => station != null && station.isActiveAndEnabled && station.HasTableInteraction && station.StationId == stationId && station.Game == game;
        private bool HasAction(CasinoMiniGameAction action) => cachedActions.Any(entry => entry.Kind == action);

        private string LocalRules(CasinoAdventureState state, long maximum, bool helped)
        {
            string rules = game == CasinoGameKind.Slots ? "两同：返2倍\n普通三同：返10倍\n三根香蕉：返30倍" :
                game == CasinoGameKind.Blackjack ? "A算1或11，庄家17停牌。\n胜返2倍，和局退投入；\n天然21返2.5倍。" :
                "绿灯亮时拉自己的杆，助手会配合。15秒内完成返4倍；错时3次失败。" + (helped ? "扳手已扩大绿灯时机。" : string.Empty);
            if (state == null) return rules;
            int bonus = state.EventPayoutBonusPercent + state.NextPayoutBonusPercent;
            var names = state.PreparedItems.Select(id => CasinoContentCatalog.FindItem(id)?.Name ?? id).ToArray();
            return rules + "\n最高投入 " + maximum + "\n返还加成 " + bonus + "%" + (names.Length > 0 ? "\n预备 " + string.Join("、", names) : string.Empty);
        }

        private string ActiveDescription(CasinoMiniGamePresentation view)
        {
            if (view == null) return "等待机台状态。";
            if (game == CasinoGameKind.Blackjack)
            {
                int total = 0, aces = 0;
                foreach (int card in view.NumberValues) { total += card == 1 ? 11 : Math.Min(10, card); if (card == 1) aces++; }
                while (total > 21 && aces-- > 0) total -= 10;
                return "你的点数 " + total + " · " + (view.SecondaryValues.Length > 0 ? "庄家明牌 " + view.SecondaryValues[0] + " · " : string.Empty) + "要牌或停牌";
            }
            return "剩余 " + (view.RemainingMilliseconds / 1000f).ToString("0.0") + "秒 · 错时 " + view.Score + "/3 · 看亮灯拉下自己的杠杆";
        }

        private void RefreshPreparation(CasinoAdventureState state)
        {
            if (slotsPrepared && (!StationMatches || state == null || preparationKey != PreparationKey(state)))
                InvalidatePreparation("金额、规则或阶段已经改变，请重新确认筹码。");
        }

        private string PreparationKey(CasinoAdventureState state) => string.Join("|", state.RunId, state.StageIndex, state.Phase, state.Coins,
            state.LockedCoins, state.RandomState, state.SettledRoundSequence, draftStake, state.PlayerCount,
            state.Config.MaximumStake, state.EventStakeCap, state.EventPayoutBonusPercent, state.NextPayoutBonusPercent,
            state.NextDiceBias, state.RuleExpiresMilliseconds, state.CurrentEventId, string.Join(",", state.PreparedItems));

        private DomainCommand NewCommand(CommandKind kind, CasinoAdventureState state) => new DomainCommand
        {
            RequestId = Guid.NewGuid().ToString("N"), RunId = state.RunId, StageIndex = state.StageIndex,
            Kind = kind, Stake = draftStake, PreparationKey = preparationKey
        };

        private JinxCasinoTableResult Send(DomainCommand command, int frameId)
        {
            lastCommand = command;
            CasinoAdventureResult result = command.Kind == CommandKind.Begin ? gameFlow.BeginGame(command.RequestId, game, command.Stake, 0, stationId, frameId) :
                gameFlow.Act(command.RequestId, command.Action, command.Value, stationId, frameId);
            if (result.Success && !command.AppliedLocally)
            {
                if (command.Kind == CommandKind.Begin) ClearDraft();
                command.AppliedLocally = true;
            }
            return new JinxCasinoTableResult(result.Success, result.Changed, result.Error, result.Description, command.RequestId);
        }

        private bool TryFrame(int frameId, string fingerprint, out JinxCasinoTableResult replay)
        {
            replay = null;
            if (frameId < 0) { replay = Fail("InvalidFrame", "帧编号无效。"); return false; }
            if (frameId < lastFrame) { replay = Fail("StaleFrame", "过期帧输入已忽略。"); return false; }
            if (frameId == lastFrame)
            {
                replay = fingerprint == lastLocalFingerprint && lastFrameResult != null ? lastFrameResult.WithoutChange() : Fail("FrameBusy", "请等待本次机台操作完成。");
                return false;
            }
            lastFrame = frameId; lastLocalFingerprint = fingerprint; lastFrameResult = null;
            return true;
        }

        private JinxCasinoTableResult Finish(JinxCasinoTableResult value) { lastFrameResult = value; return value; }
        private void ClearDraft() { draftStake = 0; InvalidatePreparation(null); }
        private void InvalidatePreparation(string reason) { slotsPrepared = false; preparationKey = null; preparationReason = reason; }
        private static JinxCasinoTableResult Local(string description) => new JinxCasinoTableResult(true, true, null, description);
        private static JinxCasinoTableResult Fail(string code, string description) => new JinxCasinoTableResult(false, false, code, description);
        private static bool ValidId(string value) => !string.IsNullOrEmpty(value) && value.Length <= 96
            && value.All(letter => letter >= 'a' && letter <= 'z' || letter >= 'A' && letter <= 'Z' || letter >= '0' && letter <= '9' || letter == '-' || letter == '_' || letter == '.');
        private static CasinoMiniGamePresentation CopyPresentation(CasinoMiniGamePresentation value) => new CasinoMiniGamePresentation
        {
            Game = value.Game, IsComplete = value.IsComplete, IsObjectiveSuccess = value.IsObjectiveSuccess,
            CooperationHelpUsed = value.CooperationHelpUsed, Choice = value.Choice, Cost = value.Cost, Payout = value.Payout,
            NumberValues = (int[])value.NumberValues.Clone(), SecondaryValues = (int[])value.SecondaryValues.Clone(), SelectedValues = (int[])value.SelectedValues.Clone(),
            ElapsedMilliseconds = value.ElapsedMilliseconds, RemainingMilliseconds = value.RemainingMilliseconds, Score = value.Score,
            Level = value.Level, Cursor = value.Cursor, Phase = value.Phase, OperationCount = value.OperationCount
        };
        private enum CommandKind { Begin, Act }
        private sealed class DomainCommand
        {
            public string RequestId;
            public string RunId;
            public int StageIndex;
            public CommandKind Kind;
            public long Stake;
            public CasinoMiniGameAction Action;
            public int Value;
            public string PreparationKey;
            public bool AppliedLocally;
        }
    }
}
