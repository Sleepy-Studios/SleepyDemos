using Core.Runtime;
using Hotfix.JinxCasino;
using System;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Interaction
{
    /// 观察实际玩家交互并报告教学事实，不创建第二套玩法或替玩家操作。
    public sealed class JinxCasinoTutorialGuide
    {
        private readonly JinxCasinoGame game;

        private readonly JinxCasinoPlayerInteraction player;

        private JinxCasinoGameSettings configuration;

        private string tutorialObservedRun;

        private CasinoTutorialStep tutorialObservedStep;

        private double tutorialLookDegrees;

        private double tutorialWalkMeters;

        private int tutorialReportedLook;

        private int tutorialReportedWalk;

        private string tutorialReportedWalkStation;

        private float tutorialNextObservationTime;

        private int tutorialAttemptedSettlement;

        private string tutorialPendingExitStation;

        /// <summary>绑定具体游戏与玩家交互，不订阅全局输入或自动开始旅程。</summary>
        /// <param name="game">场景拥有的唯一游戏实例。</param>
        /// <param name="player">真实位移、聚焦与物件交互来源。</param>
        /// <param name="configuration">当前场景教学配置；为空时拒绝开始教学。</param>
        public JinxCasinoTutorialGuide(JinxCasinoGame game, JinxCasinoPlayerInteraction player, JinxCasinoGameSettings configuration)
        {
            this.game = game ?? throw new ArgumentNullException(nameof(game));
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            this.configuration = configuration;
        }

        /// 当前练习的教学状态；未开始的正式或普通练习返回None。
        public CasinoTutorialStatus Status => game.State?.Teaching?.Status ?? CasinoTutorialStatus.None;

        /// 当前持久检查点，必须结合Status解释。
        public CasinoTutorialStep Step => game.State?.Teaching?.Step ?? CasinoTutorialStep.Look;

        /// 最近教学反馈，不覆盖桌面或柜台的交易回执。
        public string Feedback { get; private set; }

        /// 非模态操作提示；界面只显示，不自行提交事实。
        public string Hint
        {
            get
            {
                if (Status == CasinoTutorialStatus.Completed)
                    return "教学完成。继续本局练习，或明确开始新的正式冒险。";
                if (Status == CasinoTutorialStatus.Skipped)
                    return "已跳过教学，当前练习与已投入机台仍保留。";
                if (Status != CasinoTutorialStatus.Active)
                    return string.Empty;
                switch (Step)
                {
                    case CasinoTutorialStep.Look:
                        return "转动探索视角，看看大厅。";
                    case CasinoTutorialStep.Walk:
                        return "走近大厅左侧的水果机。";
                    case CasinoTutorialStep.EnterSlots:
                        return "进入水果机，等镜头聚焦。";
                    case CasinoTutorialStep.AddChips:
                        return "将10筹码放入托盘，确认前不付款。";
                    case CasinoTutorialStep.Confirm:
                        return "确认10筹码，然后亲手拉柄投入。";
                    case CasinoTutorialStep.SlotsResult:
                        return "拉动手柄，等转轮停稳后查看返还。";
                    case CasinoTutorialStep.LeaveSlots:
                        return "离开桌面，等探索相机回到原位。";
                    case CasinoTutorialStep.Blackjack:
                        return "二十一点：投入10筹码，再选择要牌或停牌。";
                    case CasinoTutorialStep.BuyWrench:
                        return "柜台选择双人扳手，再确认购买；价格100筹码。";
                    case CasinoTutorialStep.UseWrench:
                        return "使用刚买的扳手，让助手更容易配合你。";
                    case CasinoTutorialStep.Levers:
                        return "投入10筹码；绿灯亮起时拉动你的拉杆，助手会配合。错过了可以再试。";
                    case CasinoTutorialStep.Ready:
                        return "三台体验已完成。先离开桌面，再选择完成教学。";
                    default:
                        return string.Empty;
                }
            }
        }

        /// 方位文字来自当前样板，不创建导航对象或读取隐藏结果。
        public string Direction
        {
            get
            {
                if (Status != CasinoTutorialStatus.Active)
                    return string.Empty;
                switch (Step)
                {
                    case CasinoTutorialStep.Look:
                        return "探索视角 · 键鼠 / 右摇杆 / 右侧触控";
                    case CasinoTutorialStep.Walk:
                    case CasinoTutorialStep.EnterSlots:
                    case CasinoTutorialStep.AddChips:
                    case CasinoTutorialStep.Confirm:
                    case CasinoTutorialStep.SlotsResult:
                    case CasinoTutorialStep.LeaveSlots:
                        return "大厅左侧 · 水果机";
                    case CasinoTutorialStep.Blackjack:
                        return "大厅前方 · 二十一点";
                    case CasinoTutorialStep.BuyWrench:
                    case CasinoTutorialStep.UseWrench:
                        return "入口左侧 · 补给柜台";
                    case CasinoTutorialStep.Levers:
                        return "大厅右侧 · 合拍拉杆";
                    default:
                        return string.Empty;
                }
            }
        }

        /// <summary>同步创建并开始新的教学练习；失败时保留当前旅程。</summary>
        /// <param name="replaceCurrentRun">已有局时默认拒绝；明确保存或放弃旧局后传true。</param>
        /// <param name="seed">可选固定种子，不设置必中结果。</param>
        /// <returns>真实创建与教学启动回执。</returns>
        public CasinoAdventureResult StartAdventure(bool replaceCurrentRun = false, uint? seed = null)
        {
            if (!player.AcceptsCommands)
                return Failure("TutorialUnavailable", "请先结束当前操作，再开始教学。");
            if (configuration == null)
                return Failure("TutorialUnavailable", "当前场景缺少教学配置，无法开始教学。");
            if (game.HasAdventure && !replaceCurrentRun)
                return Failure("TutorialReplacementRequired", "先选择保存或放弃当前局，再明确重玩教学。");
            try
            {
                var request = new JinxCasinoStartTutorialAdventureAction(game, configuration.CreateConfig(), replaceCurrentRun, seed);
                GlobalData.Dispatch(request);
                return PublishFeedback(request.Result);
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            {
                return Failure("TutorialStartFailed", exception.Message);
            }
        }

        /// 明确跳过提示，保留练习钱包、库存与已经投入的局。
        public CasinoAdventureResult Skip()
        {
            if (!game.HasAdventure || !player.AcceptsCommands)
                return Failure("TutorialInactive", "当前没有可操作教学。");
            var request = new JinxCasinoSkipTutorialAction(game);
            GlobalData.Dispatch(request);
            var result = request.Result;
            if (result.Changed)
                Reset();
            return PublishFeedback(result);
        }

        /// 在Ready检查点明确完成教学，仍保留练习，不自动开始正式旅程。
        public CasinoAdventureResult Complete()
        {
            if (!game.HasAdventure || !player.AcceptsCommands || Status != CasinoTutorialStatus.Active || Step != CasinoTutorialStep.Ready)
                return Failure("TutorialNotReady", "请先完成实际教学步骤。");
            var request = new JinxCasinoCompleteTutorialAction(game);
            GlobalData.Dispatch(request);
            return PublishFeedback(request.Result);
        }

        // 唯一调用点在探索旋转和CharacterController.Move之后，不观察聚焦、恢复或传送产生的变换。

        internal void ObserveExploration(float appliedYaw, float appliedPitch, float distance, bool hadMoveInput)
        {
            if (player.IsPaused || player.IsMenuOpen || game.IsRestoring || !SynchronizeObservation())
                return;
            if (tutorialObservedStep == CasinoTutorialStep.Look)
            {
                tutorialLookDegrees = Math.Min(15, tutorialLookDegrees + Math.Abs(appliedYaw) + Math.Abs(appliedPitch));
                FlushMovement(false);
            }
            else if (tutorialObservedStep == CasinoTutorialStep.Walk)
            {
                if (hadMoveInput && !float.IsNaN(distance) && !float.IsInfinity(distance))
                    tutorialWalkMeters = Math.Min(.6d, tutorialWalkMeters + Math.Max(0, distance));
                // 达到距离后仍须进入实际机台交互范围，不能仅靠输入累计。
                FlushMovement(false);
            }
        }

        // 保存前force=true、notify=false同步尚未报告的真实观察，不递归触发场景刷新。

        internal void FlushMovement(bool force, bool notify = true)
        {
            if (!SynchronizeObservation())
                return;
            if (!force && Time.unscaledTime < tutorialNextObservationTime)
                return;
            tutorialNextObservationTime = Time.unscaledTime + .15f;
            if (tutorialObservedStep == CasinoTutorialStep.Look)
            {
                int value = Math.Min(15000, (int)(tutorialLookDegrees * 1000));
                if (value <= tutorialReportedLook)
                    return;
                tutorialReportedLook = value;
                ObserveFact(CasinoTutorialFact.LookApplied, value, publish: notify);
            }
            else if (tutorialObservedStep == CasinoTutorialStep.Walk)
            {
                int value = Math.Min(600, (int)(tutorialWalkMeters * 1000));
                string station = player.FindNearbyStation()?.StationId;
                if (value <= tutorialReportedWalk && station == tutorialReportedWalkStation)
                    return;
                tutorialReportedWalk = value;
                tutorialReportedWalkStation = station;
                ObserveFact(CasinoTutorialFact.WalkApplied, value, station, notify);
            }
        }

        internal void ObserveTableCommand(JinxCasinoTableAction action, JinxCasinoTableResult result)
        {
            if (!SynchronizeObservation() || player.FocusedStation == null || result == null || !result.Success || !result.Changed)
                return;
            var view = player.GetFocusedTableView(player.FocusedStation);
            if (view == null || view.StationId != CasinoAdventureSession.TutorialSlotsStation)
                return;
            if (action == JinxCasinoTableAction.ChipAdd && Step == CasinoTutorialStep.AddChips && view.DraftStake > 0)
                ObserveFact(CasinoTutorialFact.ChipsAdded, (int)Math.Min(int.MaxValue, view.DraftStake), view.StationId);
            else if (action == JinxCasinoTableAction.Commit && Step == CasinoTutorialStep.Confirm && view.IsSlotsPrepared)
                ObserveFact(CasinoTutorialFact.ChipsConfirmed, 1, view.StationId);
        }

        internal void ObserveShopCommand(JinxCasinoTableAction action, string itemId, CasinoAdventureResult result)
        {
            if (!SynchronizeObservation() || itemId != "duo_wrench" || result == null)
                return;
            if (!result.Success)
            {
                Feedback = result.Description ?? result.Error;
                return;
            }

            if (action == JinxCasinoTableAction.PurchaseProduct && Step == CasinoTutorialStep.BuyWrench)
                ObserveFact(CasinoTutorialFact.ItemPurchased, 1);
            else if ((action == JinxCasinoTableAction.UseProduct || action == JinxCasinoTableAction.Secondary) && Step == CasinoTutorialStep.UseWrench)
                ObserveFact(CasinoTutorialFact.ItemUsed, 1);
        }

        // 在共享聚焦时钟Tick后观察，系统清理不能算作玩家主动离桌。

        internal void UpdateSceneFacts()
        {
            if (player.IsPaused || player.IsMenuOpen || game.IsRestoring || !SynchronizeObservation())
                return;
            if (tutorialPendingExitStation != null && !player.HasFocus)
            {
                string left = tutorialPendingExitStation;
                tutorialPendingExitStation = null;
                ObserveFact(CasinoTutorialFact.TableLeft, 1, left);
            }

            var station = player.FocusedStation;
            if (station == null || !player.IsFocusReady || player.GetFocusedTableView(station) == null)
                return;
            string stationId = station.StationId;
            if (Step == CasinoTutorialStep.EnterSlots)
            {
                if (stationId == CasinoAdventureSession.TutorialSlotsStation)
                    ObserveFact(CasinoTutorialFact.TableEntered, 1, stationId);
                return;
            }

            if (Step != CasinoTutorialStep.SlotsResult && Step != CasinoTutorialStep.Blackjack && Step != CasinoTutorialStep.Levers)
                return;
            var presentation = player.FocusedPresentation;
            if (stationId != game.State.Teaching.StationId || presentation == null)
                return;
            int sequence = game.State.SettledRoundSequence;
            if (sequence <= game.State.Teaching.SettlementBaseline || sequence == tutorialAttemptedSettlement || game.State.LastStationId != stationId || presentation.PresentedSettlementSequence != sequence)
                return;
            // 同序号只核验一次；协作失败给真实重试提示，不能每帧刷新回执。
            tutorialAttemptedSettlement = sequence;
            ObserveFact(CasinoTutorialFact.RoundPresented, sequence, stationId);
        }

        internal void RegisterPlayerExit()
        {
            if (Status == CasinoTutorialStatus.Active && Step == CasinoTutorialStep.LeaveSlots && player.FocusedStation != null && player.FocusedStation.StationId == CasinoAdventureSession.TutorialSlotsStation && player.IsFocusReady)
                tutorialPendingExitStation = player.FocusedStation.StationId;
        }

        internal void Reset()
        {
            tutorialObservedRun = null;
            tutorialLookDegrees = 0;
            tutorialWalkMeters = 0;
            tutorialReportedLook = 0;
            tutorialReportedWalk = 0;
            tutorialReportedWalkStation = null;
            tutorialNextObservationTime = 0;
            tutorialAttemptedSettlement = 0;
            tutorialPendingExitStation = null;
            Feedback = null;
        }

        internal void SetConfiguration(JinxCasinoGameSettings settings) => configuration = settings;

        private bool SynchronizeObservation()
        {
            var teaching = game.State?.Teaching;
            if (!game.HasAdventure || teaching?.Status != CasinoTutorialStatus.Active)
                return false;
            if (tutorialObservedRun != game.State.RunId || tutorialObservedStep != teaching.Step)
            {
                tutorialObservedRun = game.State.RunId;
                tutorialObservedStep = teaching.Step;
                tutorialLookDegrees = teaching.LookMillidegrees / 1000d;
                tutorialWalkMeters = teaching.WalkMillimeters / 1000d;
                tutorialReportedLook = teaching.LookMillidegrees;
                tutorialReportedWalk = teaching.WalkMillimeters;
                tutorialReportedWalkStation = null;
                tutorialNextObservationTime = 0;
                tutorialAttemptedSettlement = 0;
            }

            return true;
        }

        private CasinoAdventureResult ObserveFact(CasinoTutorialFact fact, int value, string stationId = null, bool publish = true)
        {
            // 教学事实绕过经济提交帧，不伪造下注请求或发放奖励。
            var request = new JinxCasinoObserveTutorialAction(game, fact, value, stationId, publish);
            GlobalData.Dispatch(request);
            var result = request.Result;
            if (result.Changed)
                Feedback = null;
            else if (!result.Success && publish)
            {
                Feedback = result.Error == "TutorialCooperationRequired" ? "还没合拍成功。等绿灯亮起，再试一次。" : result.Error == "TutorialChoiceRequired" ? "这次再试试要牌或停牌。" : result.Error == "TutorialResultRequired" ? "在这张桌上玩一局，等演出结束后查看结果。" : result.Description ?? result.Error;
                player.NotifyChanged();
            }

            return result;
        }

        private CasinoAdventureResult PublishFeedback(CasinoAdventureResult result)
        {
            Feedback = result.Description ?? result.Error;
            player.NotifyChanged();
            return result;
        }

        private CasinoAdventureResult Failure(string error, string description) => PublishFeedback(new CasinoAdventureResult { Error = error, Description = description, Balance = game.State?.Coins ?? 0 });
    }
}
