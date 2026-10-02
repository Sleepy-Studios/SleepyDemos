using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.JinxCasino.Rules
{
    /// 离线冒险聚合；钱包、库存、阶段与独立小游戏在一次请求内原子提交。
    public sealed partial class CasinoAdventureSession
    {
        private CasinoAdventureState state;
        private CasinoMiniGameRound round;
        private CasinoMiniGameRound lastCompletedRound;

        private CasinoAdventureSession(CasinoAdventureState state)
        {
            this.state = state;
            if (!string.IsNullOrEmpty(state.ActiveRoundJson)) round = CasinoMiniGameRound.Restore(state.ActiveRoundJson);
            if (!string.IsNullOrEmpty(state.LastRoundJson)) lastCompletedRound = CasinoMiniGameRound.Restore(state.LastRoundJson);
        }

        public CasinoAdventureState State => CaptureState();
        public bool HasActiveRound => round != null;
        public string ActiveRoundDescription => round?.Description ?? state.LastRoundDescription ?? string.Empty;
        public long CurrentTarget => EffectiveTarget;
        public long AvailableBalance => AvailableCoins;
        /// 当前活动局或最近已完成局的安全表现数据；没有任何机台时返回null。
        public CasinoMiniGamePresentation GetPresentation()
        {
            if (round != null) return round.GetPresentation();
            var view = lastCompletedRound?.GetPresentation();
            if (view != null) { view.Cost = state.LastRoundCost; view.Payout = state.LastRoundPayout; }
            return view;
        }
        /// 下次投入前须显示的规则变化；已提交局使用投入时锁定的规则。
        public string NextBetDescription => "最高投入 " + (state.EventStakeCap > 0 ? Math.Min(state.Config.MaximumStake, state.EventStakeCap) : state.Config.MaximumStake)
            + "；事件毛返还加成 " + state.EventPayoutBonusPercent + "%；道具加成 " + state.NextPayoutBonusPercent + "%"
            + (state.NextDiceBias > 0 ? "；骰子点数" + state.NextDiceBias + "概率37.5%，其余各12.5%" : "；骰子每面概率1/6")
            + "；预备协作帮助 " + state.CooperationHelpCharges + " 次（协作局每局一次）"
            + "；预备道具 " + string.Join("、", GetPreparedItems());

        /// <summary>创建共享钱包冒险；配置被复制，调用方不能再修改本局规则。</summary>
        /// <param name="seed">固定领域随机种子；零映射为固定非零状态。</param>
        /// <param name="mode">标准四区、无限练习或循环无尽。</param>
        /// <param name="playerCount">1..6，决定目标缩放与协作局提示。</param>
        /// <param name="config">为空使用1000筹码、四区、每区240秒的生产默认。</param>
        public static CasinoAdventureSession Start(uint seed, CasinoAdventureMode mode, int playerCount, CasinoAdventureConfig config = null)
        {
            config = Clone(config ?? new CasinoAdventureConfig());
            ValidateConfig(config);
            if (!Enum.IsDefined(typeof(CasinoAdventureMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (playerCount < 1 || playerCount > 6) throw new ArgumentOutOfRangeException(nameof(playerCount));
            var session = new CasinoAdventureSession(new CasinoAdventureState
            {
                RunId = Guid.NewGuid().ToString("N"), Seed = seed, RandomState = new CasinoRandom(seed).State, Config = config, Mode = mode,
                PlayerCount = playerCount, Coins = config.StartingCoins, Phase = CasinoAdventurePhase.Playing
            });
            session.PrepareStage();
            return session;
        }

        /// 返回独立状态副本，禁止通过引用绕过钱包与幂等规则。
        public CasinoAdventureState CaptureState() { SyncRound(); return Clone(state); }
        /// 返回已预备下一局的稳定道具 ID 副本。
        public string[] GetPreparedItems() => state.PreparedItems.ToArray();
        /// 当前活动小游戏公开的合法动作；无活动局时为空数组。
        public CasinoMiniGameActionDescriptor[] GetAvailableActions() => round?.GetAvailableActions() ?? Array.Empty<CasinoMiniGameActionDescriptor>();
        /// 当前区域可用机台副本；练习全部开放，轮换展位可额外开放一台。
        public CasinoGameDefinition[] GetAvailableGames() => Array.FindAll(CasinoContentCatalog.Games, IsGameAvailable);

        /// <summary>宿主确认后提交一局，锁定最高投入；完成局按实际成本与毛返还结算。</summary>
        /// <param name="requestId">本冒险唯一请求 ID；重试保留 ID 和内容。</param>
        /// <param name="game">已开放的机台；最终接管挑战仅允许协作金库。</param>
        /// <param name="stake">1..MaximumStake 的最高投入，须不超过可用钱包。</param>
        /// <param name="choice">小游戏 Create 的机台参数。</param>
        /// <param name="stationId">具体机台的稳定ID；null保留旧原型入口兼容，新桌面必须提供。</param>
        public CasinoAdventureResult BeginGame(string requestId, CasinoGameKind game, long stake, int choice, string stationId = null)
            => Execute(requestId, "bet:" + (int)game + ":" + stake + ":" + choice + StationFingerprint(stationId), () =>
            {
                if (!IsValidStationId(stationId)) return Fail("InvalidStation", "机台标识非法。");
                bool challenge = state.Phase == CasinoAdventurePhase.Finale;
                if (round != null) return Fail("RoundActive", "已提交的局须完成后才能再次下注。");
                if (state.Phase != CasinoAdventurePhase.Playing && !challenge) return Fail("WrongPhase", "请先完成购物并确认进入下一阶段。");
                if (state.Mode != CasinoAdventureMode.Practice && state.RemainingMilliseconds <= 0 && !challenge) return Fail("TimeExpired", "本区时间已结束。");
                long maximum = state.EventStakeCap > 0 ? Math.Min(state.Config.MaximumStake, state.EventStakeCap) : state.Config.MaximumStake;
                if (stake < 1 || stake > maximum || stake > AvailableCoins) return Fail("InvalidStake", "最高投入超出可用筹码或当前事件上限。");
                var definition = Array.Find(CasinoContentCatalog.Games, entry => entry.Kind == game);
                if (definition == null) return Fail("InvalidGame", "机台不存在。");
                if (!IsGameAllowed(game)) return Fail("GameLocked", "本场景未装配该机台。");
                if (challenge && game != CasinoGameKind.CooperativeVault) return Fail("ChallengeOnly", "接管挑战需要协作金库。");
                if (!challenge && !IsGameAvailable(definition)) return Fail("GameLocked", "该机台在后续区域开放。");
                var random = new CasinoRandom(state.RandomState);
                uint roundSeed = random.NextUInt();
                // Create拒绝不兼容预备物品时，整个请求回滚，既不消耗道具也不推进随机。
                round = CasinoMiniGameRound.Create(game, roundSeed, stake, choice, state.PlayerCount, state.PreparedItems.ToArray(), state.NextDiceBias);
                state.RandomState = random.State;
                foreach (string prepared in state.PreparedItems) RemoveInventory(prepared);
                state.PreparedItems.Clear();
                state.ActiveGame = game;
                state.ActiveStationId = stationId;
                state.ActiveRoundIsChallenge = challenge;
                state.LockedCoins = stake;
                state.ActiveRoundPayoutBonusPercent = checked(state.NextPayoutBonusPercent + state.EventPayoutBonusPercent);
                state.NextPayoutBonusPercent = 0;
                if (state.CooperationHelpCharges > 0 && round.TryApplyCooperationHelp())
                {
                    AddEffect("CooperationHint", "team", 3000, 0, "双人扳手已提供真实帮助，请继续实际操作完成协作。", 0);
                    state.CooperationHelpCharges--;
                }
                if (round.IsComplete) SettleRound();
                return Ok(round == null ? state.LastRoundDescription : round.Description);
            });

        /// <summary>操作已提交局；关闭界面不能撤回局，重发不会重复操作或结算。</summary>
        /// <param name="requestId">唯一请求 ID；相同 ID 不可用于另一操作。</param>
        /// <param name="action">当前 GetAvailableActions 返回的合法动作。</param>
        /// <param name="value">对应小游戏的整数参数，默认零。</param>
        /// <param name="stationId">绑定局的机台ID；旧原型未绑定的局使用null。</param>
        public CasinoAdventureResult Act(string requestId, CasinoMiniGameAction action, int value = 0, string stationId = null)
            => Execute(requestId, "act:" + (int)action + ":" + value + StationFingerprint(stationId), () =>
            {
                if (round == null) return Fail("NoRound", "没有进行中的小游戏。");
                if (!IsValidStationId(stationId) || !string.Equals(state.ActiveStationId ?? string.Empty, stationId ?? string.Empty, StringComparison.Ordinal))
                    return Fail("WrongStation", "请返回已投入的机台继续本局。");
                if (!round.TryAct(action, value)) return Fail("InvalidAction", "当前局不接受该操作或参数。");
                string description = round.Description;
                if (round.IsComplete) { SettleRound(); description = state.LastRoundDescription; }
                return Ok(description);
            });

        /// <summary>推进游戏领域时钟与事件；购物、结束、失败阶段冻结时间。</summary>
        /// <param name="milliseconds">0..3600000 的显式增量；一次大增量仍按事件边界逐段处理。</param>
        public CasinoAdventureResult Advance(int milliseconds)
        {
            if (milliseconds < 0 || milliseconds > 3600000) return Fail("InvalidTime", "时间增量须为0至3600000毫秒。");
            if (milliseconds == 0 || !CanAdvance) return new CasinoAdventureResult { Success = true, Balance = state.Coins, Description = string.Empty };
            string before = ToSnapshotJson();
            int effectStart = state.Effects.Count;
            var previousPhase = state.Phase;
            bool hadRound = round != null;
            try
            {
                int pending = milliseconds;
                while (pending > 0 && CanAdvance)
                {
                    bool timed = state.Mode != CasinoAdventureMode.Practice && state.Phase == CasinoAdventurePhase.Playing;
                    int step = pending;
                    if (timed) step = Math.Min(step, Math.Max(1, state.RemainingMilliseconds));
                    bool eventsEnabled = timed && state.Config.EventIntervalMilliseconds > 0;
                    if (eventsEnabled) step = Math.Min(step, Math.Max(1, state.EventCountdownMilliseconds));
                    if (state.RuleExpiresMilliseconds > state.ElapsedMilliseconds) step = (int)Math.Min(step, state.RuleExpiresMilliseconds - state.ElapsedMilliseconds);
                    if (state.ActiveMission != null && !state.ActiveMission.Completed && !state.ActiveMission.Failed && state.ActiveMission.DeadlineMilliseconds > state.ElapsedMilliseconds)
                        step = (int)Math.Min(step, state.ActiveMission.DeadlineMilliseconds - state.ElapsedMilliseconds);
                    state.ElapsedMilliseconds = checked(state.ElapsedMilliseconds + step);
                    ExpireEventState();
                    if (timed) state.RemainingMilliseconds = Math.Max(0, state.RemainingMilliseconds - step);
                    if (eventsEnabled) state.EventCountdownMilliseconds -= step;
                    round?.Advance(step);
                    if (round != null && round.IsComplete) SettleRound();
                    if (eventsEnabled && state.EventCountdownMilliseconds <= 0)
                    {
                        TriggerRandomEvent();
                        state.EventCountdownMilliseconds = state.Config.EventIntervalMilliseconds;
                    }
                    pending -= step;
                    if (timed && state.RemainingMilliseconds == 0)
                    {
                        if (round == null) CloseStage();
                        else state.Phase = CasinoAdventurePhase.Closing;
                    }
                }
                SyncRound();
                state.Revision = checked(state.Revision + 1);
                string description = state.Effects.Count > effectStart ? state.Effects[state.Effects.Count - 1].Description :
                    hadRound && round == null ? state.LastRoundDescription : previousPhase != state.Phase ?
                    state.Phase == CasinoAdventurePhase.Failed ? "时间结束，未达到本区额度；可使用救场哨或撤离。" :
                    state.Phase == CasinoAdventurePhase.Closing ? "本区时间已到，已提交局仍须完成结算。" : "本区额度达成，筹码保留，请购物或选择结局。" : string.Empty;
                return ResultSince(effectStart, description);
            }
            catch (Exception exception) when (exception is OverflowException || exception is ArgumentException)
            {
                LoadState(before);
                return Fail("InvalidTimeState", "时钟推进回滚：" + exception.Message);
            }
        }

        /// <summary>主动提前结算本区；达标仅作阈值检查，不从钱包扣除额度。</summary>
        /// <param name="requestId">唯一请求 ID；重发不重复推进阶段。</param>
        public CasinoAdventureResult CompleteStage(string requestId)
            => Execute(requestId, "complete-stage", () =>
            {
                if (state.Mode == CasinoAdventureMode.Practice || state.Phase != CasinoAdventurePhase.Playing) return Fail("WrongPhase", "当前模式或阶段不能结算区域。");
                if (round != null) return Fail("RoundActive", "请先完成已提交局。");
                if (state.Coins < EffectiveTarget) return Fail("TargetNotReached", "筹码未达到本区额度。");
                CloseStage();
                return Ok("本区已达标，筹码保留；下一步购物或选择结局。");
            });

        /// <summary>全队准备完成后的显式推进入口；不会通过一次下注隐式确认购物。</summary>
        /// <param name="requestId">唯一请求 ID；重发不重复开始区域。</param>
        public CasinoAdventureResult BeginNextStage(string requestId)
            => Execute(requestId, "next-stage", () =>
            {
                if (state.Phase != CasinoAdventurePhase.Shopping) return Fail("WrongPhase", "尚未处于阶段间购物。");
                state.StageIndex = checked(state.StageIndex + 1);
                state.Phase = CasinoAdventurePhase.Playing;
                PrepareStage();
                return Ok("已确认进入下一阶段。");
            });

        /// <summary>显式重置练习的钱包和活动局；练习不产生正式通关战绩。</summary>
        /// <param name="requestId">唯一请求 ID；活动局未结算时拒绝。</param>
        public CasinoAdventureResult ResetPractice(string requestId)
            => Execute(requestId, "reset-practice", () =>
            {
                if (state.Mode != CasinoAdventureMode.Practice || state.Phase == CasinoAdventurePhase.Ended || round != null) return Fail("WrongPhase", "只可在练习空闲时补筹码。");
                state.Coins = state.Config.StartingCoins;
                return Ok("练习筹码已显式重置，不计入正式战绩。");
            });

        /// <summary>选择原创结局；接管必须先真实赢得额外协作金库挑战。</summary>
        /// <param name="requestId">唯一请求 ID；相同请求重发不重复结束。</param>
        /// <param name="ending">撤离、体面离场或挑战后接管。</param>
        public CasinoAdventureResult ChooseEnding(string requestId, CasinoAdventureEnding ending)
            => Execute(requestId, "ending:" + (int)ending, () =>
            {
                if (round != null || state.Phase == CasinoAdventurePhase.Ended) return Fail("WrongPhase", "请完成活动局，已结束冒险不能改结局。");
                if (ending == CasinoAdventureEnding.Withdraw)
                {
                    CloseOpenEvents();
                    state.Ending = ending; state.Phase = CasinoAdventurePhase.Ended;
                    return Ok("带着剩余筹码撤离，倒霉故事暂告一段落。");
                }
                if (state.Mode != CasinoAdventureMode.Standard || state.Phase != CasinoAdventurePhase.Finale) return Fail("FinaleRequired", "标准局全部区域达标后才能选择该结局。");
                if (ending == CasinoAdventureEnding.TakeOver && !state.TakeOverUnlocked) return Fail("ChallengeRequired", "须先完成协作金库额外挑战并赢得筹码。");
                if (ending != CasinoAdventureEnding.TakeOver && ending != CasinoAdventureEnding.LeaveWithDignity) return Fail("InvalidEnding", "结局不存在。");
                CloseOpenEvents();
                state.Ending = ending; state.Phase = CasinoAdventurePhase.Ended;
                return Ok(ending == CasinoAdventureEnding.TakeOver ? "你们解开金库，接管倒霉蛋俱乐部。" : "你们见好就收，体面离场。");
            });

        /// 导出包含局、库存、事件、配置与全部幂等回执的恢复快照。
        public string ToSnapshotJson() { SyncRound(); return JsonUtility.ToJson(state); }

        /// <summary>恢复阶段或进行中的局，绝不重新下注、消费库存或支付结算。</summary>
        /// <param name="json">本类导出的版本4快照；不迁移旧版本，缺失版本或非法结构抛出异常。</param>
        public static CasinoAdventureSession Restore(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 16777216) throw new ArgumentException("冒险快照为空或过大。", nameof(json));
            // 不允许字段初始化值把缺失的版本伪装成当前格式。
            var restored = new CasinoAdventureState { SchemaVersion = 0 };
            JsonUtility.FromJsonOverwrite(json, restored);
            ValidateState(restored);
            var session = new CasinoAdventureSession(restored);
            if (session.round != null && (session.round.GetPresentation().Game != restored.ActiveGame || session.round.IsComplete || restored.LockedCoins == 0 || session.round.Cost > restored.LockedCoins ||
                (restored.Phase != CasinoAdventurePhase.Playing && restored.Phase != CasinoAdventurePhase.Closing && restored.Phase != CasinoAdventurePhase.Finale))) throw new ArgumentException("活动局与锁定筹码或阶段不匹配。", nameof(json));
            session.RestoreTutorialCheckpoint();
            return session;
        }

        private long AvailableCoins => state.Coins - state.LockedCoins;
        private long EffectiveTarget => Math.Max(0, checked(state.StageTarget + state.StageTargetAdjustment));
        private bool CanAdvance => state.Phase != CasinoAdventurePhase.Ended;
        private bool IsGameAllowed(CasinoGameKind game) => state.Config.AllowedGames.Length == 0 || Array.IndexOf(state.Config.AllowedGames, game) >= 0;
        private bool IsGameAvailable(CasinoGameDefinition game) => IsGameAllowed(game.Kind) && (state.Mode == CasinoAdventureMode.Practice || Array.IndexOf(state.Config.InitiallyAvailableGames, game.Kind) >= 0 || game.AreaIndex <= state.StageIndex % 4
            || state.GameRotationOffset > 0 && (int)game.Kind == (state.StageIndex * 5 + state.GameRotationOffset) % 17);

        private void PrepareStage()
        {
            int cycle = state.StageIndex / state.Config.StageCount;
            long target = state.Config.Targets[state.StageIndex % state.Config.StageCount];
            long percent = checked(100L + (state.PlayerCount - 1L) * state.Config.AdditionalPlayerTargetPercent + cycle * (long)state.Config.EndlessCycleTargetPercent);
            state.StageTarget = checked(target * percent / 100);
            state.StageTargetAdjustment = 0;
            state.RemainingMilliseconds = state.Config.StageDurationMilliseconds;
            state.EventCountdownMilliseconds = state.Config.EventIntervalMilliseconds;
            state.CurrentEventId = null;
            state.ActiveMission = null;
            state.EventChoicePending = false;
            state.GameRotationOffset = 0;
            ClearEventRules();
        }

        private void CloseStage()
        {
            CloseOpenEvents();
            if (state.Coins < EffectiveTarget) { state.Phase = CasinoAdventurePhase.Failed; return; }
            state.CompletedStages = checked(state.CompletedStages + 1);
            state.Phase = state.Mode == CasinoAdventureMode.Standard && state.CompletedStages >= state.Config.StageCount
                ? CasinoAdventurePhase.Finale : CasinoAdventurePhase.Shopping;
        }

        private void CloseOpenEvents()
        {
            state.EventChoicePending = false;
            if (state.ActiveMission != null && !state.ActiveMission.Completed) state.ActiveMission.Failed = true;
        }

        private void SettleRound()
        {
            if (round == null || !round.IsComplete) return;
            long cost = round.Cost;
            if (cost < 0 || cost > state.LockedCoins) throw new ArgumentException("小游戏成本超出锁定金额。");
            long payout = round.Payout;
            payout = checked(payout + payout * state.ActiveRoundPayoutBonusPercent / 100);
            long balance = checked(state.Coins - cost + payout);
            if (balance < 0) throw new ArgumentException("结算后钱包非法。");
            state.Coins = balance;
            state.LastRoundCost = cost;
            state.LastRoundPayout = payout;
            state.LastRoundDescription = round.Description + "；实际投入 " + cost + "，毛返还 " + payout + "，团队筹码 " + balance;
            state.LastRoundJson = round.ToSnapshotJson();
            state.LastStationId = state.ActiveStationId;
            lastCompletedRound = round;
            state.SettledRoundSequence = checked(state.SettledRoundSequence + 1);
            // 保险、彩金券与事件加成可能让失败局获得赔偿，不能代替正确输入金库密码。
            if (state.ActiveRoundIsChallenge && round.IsObjectiveSuccess && round.Payout > cost) state.TakeOverUnlocked = true;
            state.ActiveRoundIsChallenge = false;
            state.ActiveRoundPayoutBonusPercent = 0;
            state.LockedCoins = 0;
            state.ActiveRoundJson = null;
            state.ActiveStationId = null;
            round = null;
            if (state.Phase == CasinoAdventurePhase.Closing) CloseStage();
        }

        private CasinoAdventureResult Execute(string requestId, string fingerprint, Func<CasinoAdventureResult> mutation)
        {
            if (string.IsNullOrWhiteSpace(requestId) || requestId.Length > 128) return Fail("InvalidRequest", "请求编号不能为空或超过128字符。");
            var existing = state.ProcessedRequests.Find(entry => entry.RequestId == requestId);
            if (existing != null)
            {
                if (existing.Fingerprint != fingerprint) return Fail("ConflictingRequest", "同一请求编号不能修改请求内容。");
                var retry = Clone(existing.Result);
                retry.Changed = false;
                // 回执保留原效果供日志显示；宿主按唯一Effect.Id去重，不能再次应用。
                return retry;
            }
            string before = ToSnapshotJson();
            int effectStart = state.Effects.Count;
            CasinoAdventureResult result;
            try
            {
                result = mutation();
                if (result.Success)
                {
                    SyncRound(); state.Revision = checked(state.Revision + 1);
                    result = ResultSince(effectStart, result.Description);
                }
                else LoadState(before);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException || exception is NotSupportedException)
            {
                LoadState(before);
                result = Fail(exception is OverflowException ? "BalanceOverflow" : "Rejected", exception.Message);
            }
            result.Balance = state.Coins;
            state.ProcessedRequests.Add(new CasinoAdventureRequestRecord { RequestId = requestId, Fingerprint = fingerprint, Revision = state.Revision, Result = Clone(result) });
            return Clone(result);
        }

        private CasinoAdventureResult ResultSince(int effectStart, string description)
            => new CasinoAdventureResult { Success = true, Changed = true, Description = description, Balance = state.Coins,
                Effects = state.Effects.GetRange(effectStart, state.Effects.Count - effectStart).ToArray() };
        private CasinoAdventureResult Ok(string description) => new CasinoAdventureResult { Success = true, Description = description, Balance = state.Coins };
        private CasinoAdventureResult Fail(string error, string description) => new CasinoAdventureResult { Error = error, Description = description, Balance = state.Coins };
        private void SyncRound() { state.ActiveRoundJson = round?.ToSnapshotJson(); }
        private void LoadState(string json)
        {
            state = JsonUtility.FromJson<CasinoAdventureState>(json);
            NormalizeAbsentMission(state);
            round = string.IsNullOrEmpty(state.ActiveRoundJson) ? null : CasinoMiniGameRound.Restore(state.ActiveRoundJson);
            lastCompletedRound = string.IsNullOrEmpty(state.LastRoundJson) ? null : CasinoMiniGameRound.Restore(state.LastRoundJson);
        }
        private static T Clone<T>(T value)
        {
            var copy = JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
            if (copy is CasinoAdventureState adventureState) NormalizeAbsentMission(adventureState);
            return copy;
        }

        // JsonUtility对内联引用不保留null：仅接受完整零值的“无任务”占位，绝不吞掉部分损坏的任务。
        private static void NormalizeAbsentMission(CasinoAdventureState value)
        {
            var mission = value?.ActiveMission;
            if (mission != null && string.IsNullOrEmpty(mission.Id) && string.IsNullOrEmpty(mission.EventId) && string.IsNullOrEmpty(mission.Description) &&
                mission.Action == CasinoTaskAction.Collect && mission.Progress == 0 && mission.TargetCount == 0 && mission.DeadlineMilliseconds == 0 && mission.RewardCoins == 0 &&
                !mission.Carrying && !mission.Completed && !mission.Failed && mission.Visited != null && mission.Visited.Count == 0 && mission.Actors != null && mission.Actors.Count == 0)
                value.ActiveMission = null;
        }

        private static void ValidateConfig(CasinoAdventureConfig config)
        {
            if (config == null || config.StartingCoins < 1 || config.MaximumStake < 1 || config.MaximumStake > CasinoMiniGameRound.MaximumStake ||
                config.StageCount < 1 || config.StageCount > 4 || config.Targets == null || config.Targets.Length < config.StageCount ||
                config.StageDurationMilliseconds < 1 || config.StageDurationMilliseconds > 3600000 || config.EventIntervalMilliseconds < 0 ||
                config.AdditionalPlayerTargetPercent < 0 || config.AdditionalPlayerTargetPercent > 1000 || config.EndlessCycleTargetPercent < 0 || config.EndlessCycleTargetPercent > 1000 ||
                config.ShopItemIds == null || config.AllowedGames == null || config.EventWeights == null) throw new ArgumentException("冒险配置非法。");
            foreach (long target in config.Targets) if (target < 0 || target > long.MaxValue / 100000) throw new ArgumentException("阶段额度非法。");
            foreach (string id in config.ShopItemIds) if (CasinoContentCatalog.FindItem(id) == null) throw new ArgumentException("商店配置含未知道具。");
            var gameKinds = new HashSet<CasinoGameKind>();
            foreach (var game in config.AllowedGames) if (!Enum.IsDefined(typeof(CasinoGameKind), game) || !gameKinds.Add(game)) throw new ArgumentException("机台配置含无效或重复类型。");
            config.InitiallyAvailableGames ??= Array.Empty<CasinoGameKind>();
            var initiallyAvailable = new HashSet<CasinoGameKind>();
            foreach (var game in config.InitiallyAvailableGames)
                if (!Enum.IsDefined(typeof(CasinoGameKind), game) || !initiallyAvailable.Add(game)) throw new ArgumentException("起始机台配置含无效或重复类型。");
            var eventIds = new HashSet<string>();
            foreach (var weight in config.EventWeights) if (weight == null || weight.Weight < 0 || weight.Weight > 1000000 || CasinoContentCatalog.FindEvent(weight.EventId) == null || !eventIds.Add(weight.EventId)) throw new ArgumentException("事件权重配置非法。");
        }

        private static void ValidateState(CasinoAdventureState value)
        {
            if (value == null) throw new ArgumentException("冒险快照无状态。");
            NormalizeAbsentMission(value);
            ValidateConfig(value.Config);
            if (value.SchemaVersion != CasinoAdventureState.CurrentSchemaVersion || !IsValidStationId(value.ActiveStationId) || !IsValidStationId(value.LastStationId) ||
                (string.IsNullOrEmpty(value.ActiveRoundJson) && !string.IsNullOrEmpty(value.ActiveStationId)) ||
                (string.IsNullOrEmpty(value.LastRoundJson) && !string.IsNullOrEmpty(value.LastStationId)) ||
                !Guid.TryParseExact(value.RunId, "N", out _) || value.RandomState == 0 || value.Coins < 0 || value.LockedCoins < 0 || value.LockedCoins > value.Coins ||
                value.PlayerCount < 1 || value.PlayerCount > 6 || value.StageIndex < 0 || value.CompletedStages < 0 || value.StageTarget < 0 || value.RemainingMilliseconds < 0 ||
                value.ElapsedMilliseconds < 0 || value.Revision < 0 || value.SettledRoundSequence < 0 || value.EffectSequence < 0 || value.EventShieldCharges < 0 || value.NextPayoutBonusPercent < 0 || value.NextPayoutBonusPercent > 100 ||
                value.ActiveRoundPayoutBonusPercent < 0 || value.ActiveRoundPayoutBonusPercent > 200 || value.NextDiceBias < 0 || value.NextDiceBias > 6 || value.EventPayoutBonusPercent < 0 || value.EventPayoutBonusPercent > 100 ||
                value.Inventory == null || value.PreparedItems == null || value.Effects == null || value.TargetProtection == null || value.ProcessedRequests == null ||
                !Enum.IsDefined(typeof(CasinoAdventureMode), value.Mode) || !Enum.IsDefined(typeof(CasinoAdventurePhase), value.Phase) || !Enum.IsDefined(typeof(CasinoAdventureEnding), value.Ending) ||
                (string.IsNullOrEmpty(value.ActiveRoundJson) && (value.LockedCoins != 0 || value.ActiveRoundIsChallenge))) throw new ArgumentException("冒险快照结构非法。");
            var inventoryIds = new HashSet<string>();
            if (!string.IsNullOrEmpty(value.LastRoundJson))
            {
                var last = CasinoMiniGameRound.Restore(value.LastRoundJson);
                if (!last.IsComplete || last.Cost != value.LastRoundCost || last.Payout > value.LastRoundPayout || value.SettledRoundSequence < 1)
                    throw new ArgumentException("已结算表现快照非法。");
            }
            foreach (var item in value.Inventory) if (item == null || item.Count < 1 || CasinoContentCatalog.FindItem(item.ItemId) == null || !inventoryIds.Add(item.ItemId)) throw new ArgumentException("库存快照非法。");
            var preparedIds = new HashSet<string>();
            foreach (string id in value.PreparedItems) if (CasinoContentCatalog.FindItem(id)?.Behavior != CasinoItemBehavior.Rule || !preparedIds.Add(id) || value.Inventory.Find(item => item.ItemId == id) == null) throw new ArgumentException("预备道具快照非法。");
            var requestIds = new HashSet<string>();
            foreach (var entry in value.ProcessedRequests) if (entry == null || string.IsNullOrWhiteSpace(entry.RequestId) || entry.Result == null || entry.Fingerprint == null || entry.Revision < 0 || entry.Revision > value.Revision || !requestIds.Add(entry.RequestId)) throw new ArgumentException("请求回执快照非法。");
            var effectIds = new HashSet<string>();
            foreach (var effect in value.Effects) if (effect == null || string.IsNullOrWhiteSpace(effect.Id) || string.IsNullOrWhiteSpace(effect.EffectKind) || effect.TargetId == null || effect.DurationMilliseconds < 0 || effect.ProtectionMilliseconds < 0 || !effectIds.Add(effect.Id)) throw new ArgumentException("场景效果快照非法。");
            foreach (var protection in value.TargetProtection) if (protection == null || string.IsNullOrWhiteSpace(protection.TargetId) || protection.UntilMilliseconds < 0) throw new ArgumentException("目标保护快照非法。");
            var mission = value.ActiveMission;
            if (mission != null && (CasinoContentCatalog.FindEvent(mission.EventId) == null || mission.Id == null || mission.Visited == null || mission.Actors == null || mission.TargetCount < 1 || mission.Progress < 0 || mission.Progress > mission.TargetCount ||
                mission.DeadlineMilliseconds < 0 || mission.RewardCoins < 0 || mission.Completed && mission.Progress != mission.TargetCount)) throw new ArgumentException("任务快照非法。");
            ValidateTutorial(value);
        }
    }
}
