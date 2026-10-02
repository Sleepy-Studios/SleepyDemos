using Hotfix.JinxCasino.Interaction;
using System;
using Hotfix.JinxCasino.Rules;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    public sealed partial class JinxCasinoController
    {
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

        /// 包括台内/柜台聚焦与相机退出过渡；完成选择菜单不能在此期间抢走操作焦点。
        public bool HasImmersionFocus => tableFocus?.IsActive ?? false;
        /// 当前练习的教学状态；未开始的正式/普通练习返回None。
        public CasinoTutorialStatus TutorialStatus => Game.State?.Teaching?.Status ?? CasinoTutorialStatus.None;
        /// 当前持久检查点，必须结合TutorialStatus解释。
        public CasinoTutorialStep TutorialStep => Game.State?.Teaching?.Step ?? CasinoTutorialStep.Look;
        /// 最近教学反馈，不覆盖原机台/柜台的真实交易回执。
        public string TutorialFeedback { get; private set; }
        /// 非模态操作提示；UI只显示，不自行提交教学事实。
        public string TutorialHint
        {
            get
            {
                if (TutorialStatus == CasinoTutorialStatus.Completed) return "教学完成。继续本局练习，或明确开始新的正式冒险。";
                if (TutorialStatus == CasinoTutorialStatus.Skipped) return "已跳过教学，当前练习与已投入机台仍保留。";
                if (TutorialStatus != CasinoTutorialStatus.Active) return string.Empty;
                switch (TutorialStep)
                {
                    case CasinoTutorialStep.Look: return "转动探索视角，看看大厅。";
                    case CasinoTutorialStep.Walk: return "走近大厅左侧的水果机。";
                    case CasinoTutorialStep.EnterSlots: return "进入水果机，等镜头聚焦。";
                    case CasinoTutorialStep.AddChips: return "将10筹码放入托盘，确认前不付款。";
                    case CasinoTutorialStep.Confirm: return "确认10筹码，然后亲手拉柄投入。";
                    case CasinoTutorialStep.SlotsResult: return "拉动手柄，等转轮停稳后查看返还。";
                    case CasinoTutorialStep.LeaveSlots: return "离开桌面，等探索相机回到原位。";
                    case CasinoTutorialStep.Blackjack: return "二十一点：投入10筹码，再选择要牌或停牌。";
                    case CasinoTutorialStep.BuyWrench: return "柜台选择双人扳手，再确认购买；价格100筹码。";
                    case CasinoTutorialStep.UseWrench: return "使用刚买的扳手，让助手更容易配合你。";
                    case CasinoTutorialStep.Levers: return "投入10筹码；绿灯亮起时拉动你的拉杆，助手会配合。错过了可以再试。";
                    case CasinoTutorialStep.Ready: return "三台体验已完成。先离开桌面，再选择完成教学。";
                    default: return string.Empty;
                }
            }
        }
        /// 方位文字来自当前样板，不创建额外导航对象或读取隐藏结果。
        public string TutorialDirection
        {
            get
            {
                if (TutorialStatus != CasinoTutorialStatus.Active) return string.Empty;
                switch (TutorialStep)
                {
                    case CasinoTutorialStep.Look: return "探索视角 · 键鼠 / 右摇杆 / 右侧触控";
                    case CasinoTutorialStep.Walk:
                    case CasinoTutorialStep.EnterSlots:
                    case CasinoTutorialStep.AddChips:
                    case CasinoTutorialStep.Confirm:
                    case CasinoTutorialStep.SlotsResult:
                    case CasinoTutorialStep.LeaveSlots: return "大厅左侧 · 水果机";
                    case CasinoTutorialStep.Blackjack: return "大厅前方 · 二十一点";
                    case CasinoTutorialStep.BuyWrench:
                    case CasinoTutorialStep.UseWrench: return "入口左侧 · 补给柜台";
                    case CasinoTutorialStep.Levers: return "大厅右侧 · 合拍拉杆";
                    default: return string.Empty;
                }
            }
        }

        /// <summary>创建新的单人教学Practice，并在同一调用中立即启动教学；没有yield或补发奖励。</summary>
        /// <param name="replaceCurrentRun">已有局时默认拒绝；仅在用户明确保存/放弃当前局后传true。</param>
        /// <param name="seed">可选固定教学种子用于回归；不设置必中结果。</param>
        /// <returns>真实创建/启动回执，失败时保留原局。</returns>
        public CasinoAdventureResult StartTutorialAdventure(bool replaceCurrentRun = false, uint? seed = null)
        {
            if (!UsesImmersion || IsBusy || IsLegacySession) return TutorialFailure("TutorialUnavailable", "请先结束当前操作，并从沉浸样板进入教学。");
            if (Game.HasAdventure && !replaceCurrentRun) return TutorialFailure("TutorialReplacementRequired", "先选择保存或放弃当前局，再明确重玩教学。");
            try
            {
                var config = gameSettings != null ? gameSettings.CreateConfig() : new CasinoAdventureConfig();
                return PublishTutorialFeedback(Game.StartTutorialAdventure(config, replaceCurrentRun, seed));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException || exception is OverflowException)
            { return TutorialFailure("TutorialStartFailed", exception.Message); }
        }

        /// 明确跳过提示，保留本Practice的钱包、库存与已经投入的活动局。
        public CasinoAdventureResult SkipTutorial()
        {
            if (!Game.HasAdventure || IsBusy) return TutorialFailure("TutorialInactive", "当前没有可操作教学。");
            var result = Game.SkipTutorial();
            if (result.Changed) ResetTutorialObservations();
            return PublishTutorialFeedback(result);
        }

        /// Ready后由玩家明确完成；仍保留Practice，不自动开始正式局或搬运练习钱包。
        public CasinoAdventureResult CompleteTutorial()
        {
            if (!Game.HasAdventure || IsBusy || TutorialStatus != CasinoTutorialStatus.Active || TutorialStep != CasinoTutorialStep.Ready)
                return TutorialFailure("TutorialNotReady", "请先完成实际教学步骤。");
            var result = Game.CompleteTutorial();
            return PublishTutorialFeedback(result);
        }

        private bool SynchronizeTutorialObservation()
        {
            var teaching = Game.State?.Teaching;
            if (!Game.HasAdventure || teaching?.Status != CasinoTutorialStatus.Active) return false;
            if (tutorialObservedRun != Game.State.RunId || tutorialObservedStep != teaching.Step)
            {
                tutorialObservedRun = Game.State.RunId; tutorialObservedStep = teaching.Step;
                tutorialLookDegrees = teaching.LookMillidegrees / 1000d; tutorialWalkMeters = teaching.WalkMillimeters / 1000d;
                tutorialReportedLook = teaching.LookMillidegrees; tutorialReportedWalk = teaching.WalkMillimeters;
                tutorialReportedWalkStation = null; tutorialNextObservationTime = 0; tutorialAttemptedSettlement = 0;
            }
            return true;
        }

        // 唯一调用点在探索旋转和CharacterController.Move之后；不观察Focus/Restore/传送产生的变换。
        private void ObserveTutorialAppliedExploration(float appliedYaw, float appliedPitch, float distance, bool hadMoveInput)
        {
            if (IsImmersionPaused || IsAdventureInputBlocked || Game.IsRestoring || !SynchronizeTutorialObservation()) return;
            if (tutorialObservedStep == CasinoTutorialStep.Look)
            {
                tutorialLookDegrees = Math.Min(15, tutorialLookDegrees + Math.Abs(appliedYaw) + Math.Abs(appliedPitch));
                FlushTutorialMovement(false);
            }
            else if (tutorialObservedStep == CasinoTutorialStep.Walk)
            {
                if (hadMoveInput && !float.IsNaN(distance) && !float.IsInfinity(distance))
                    tutorialWalkMeters = Math.Min(.6d, tutorialWalkMeters + Math.Max(0, distance));
                // 达到距离后仍要在实际进入水果机交互范围时报告，不能仅靠输入累计。
                FlushTutorialMovement(false);
            }
        }

        private void FlushTutorialMovement(bool force, bool notify = true)
        {
            if (!SynchronizeTutorialObservation()) return;
            if (!force && Time.unscaledTime < tutorialNextObservationTime) return;
            tutorialNextObservationTime = Time.unscaledTime + .15f;
            if (tutorialObservedStep == CasinoTutorialStep.Look)
            {
                int value = Math.Min(15000, (int)(tutorialLookDegrees * 1000));
                if (value <= tutorialReportedLook) return;
                tutorialReportedLook = value;
                ObserveTutorialFact(CasinoTutorialFact.LookApplied, value, publish: notify);
            }
            else if (tutorialObservedStep == CasinoTutorialStep.Walk)
            {
                int value = Math.Min(600, (int)(tutorialWalkMeters * 1000));
                string station = FindNearbyStation()?.StationId;
                if (value <= tutorialReportedWalk && station == tutorialReportedWalkStation) return;
                tutorialReportedWalk = value; tutorialReportedWalkStation = station;
                ObserveTutorialFact(CasinoTutorialFact.WalkApplied, value, station, notify);
            }
        }

        private void ObserveTutorialTableCommand(JinxCasinoTableAction action, JinxCasinoTableResult result)
        {
            if (!SynchronizeTutorialObservation() || focusedStation == null || result == null || !result.Success || !result.Changed) return;
            var view = tableSession?.GetView();
            if (view == null || view.StationId != CasinoAdventureSession.TutorialSlotsStation) return;
            if (action == JinxCasinoTableAction.ChipAdd && TutorialStep == CasinoTutorialStep.AddChips && view.DraftStake > 0)
                ObserveTutorialFact(CasinoTutorialFact.ChipsAdded, (int)Math.Min(int.MaxValue, view.DraftStake), view.StationId);
            else if (action == JinxCasinoTableAction.Commit && TutorialStep == CasinoTutorialStep.Confirm && view.IsSlotsPrepared)
                ObserveTutorialFact(CasinoTutorialFact.ChipsConfirmed, 1, view.StationId);
        }

        private void ObserveTutorialShopCommand(JinxCasinoTableAction action, string itemId, CasinoAdventureResult result)
        {
            if (!SynchronizeTutorialObservation() || itemId != "duo_wrench" || result == null) return;
            if (!result.Success) { TutorialFeedback = result.Description ?? result.Error; return; }
            if (action == JinxCasinoTableAction.PurchaseProduct && TutorialStep == CasinoTutorialStep.BuyWrench)
                ObserveTutorialFact(CasinoTutorialFact.ItemPurchased, 1);
            else if ((action == JinxCasinoTableAction.UseProduct || action == JinxCasinoTableAction.Secondary) && TutorialStep == CasinoTutorialStep.UseWrench)
                ObserveTutorialFact(CasinoTutorialFact.ItemUsed, 1);
        }

        // 在实际共享聚焦时钟Tick后观察；不能把加载/传送的系统清理算作玩家离桌。
        private void UpdateTutorialSceneFacts()
        {
            if (IsImmersionPaused || IsAdventureInputBlocked || Game.IsRestoring || !SynchronizeTutorialObservation()) return;
            if (tutorialPendingExitStation != null && tableFocus != null && !tableFocus.IsActive)
            {
                string left = tutorialPendingExitStation; tutorialPendingExitStation = null;
                ObserveTutorialFact(CasinoTutorialFact.TableLeft, 1, left);
            }
            if (focusedStation == null || tableSession == null || !tableFocus.IsReady) return;
            string stationId = focusedStation.StationId;
            if (TutorialStep == CasinoTutorialStep.EnterSlots)
            {
                if (stationId == CasinoAdventureSession.TutorialSlotsStation) ObserveTutorialFact(CasinoTutorialFact.TableEntered, 1, stationId);
                return;
            }
            if (TutorialStep != CasinoTutorialStep.SlotsResult && TutorialStep != CasinoTutorialStep.Blackjack && TutorialStep != CasinoTutorialStep.Levers) return;
            if (stationId != Game.State.Teaching.StationId || focusedPresentation == null) return;
            int sequence = Game.State.SettledRoundSequence;
            if (sequence <= Game.State.Teaching.SettlementBaseline || sequence == tutorialAttemptedSettlement ||
                Game.State.LastStationId != stationId || focusedPresentation.PresentedSettlementSequence != sequence) return;
            // 只尝试一次同序号结果。协作失败要给真实重试提示，不能每次Refresh反复核验/刷回执。
            tutorialAttemptedSettlement = sequence;
            ObserveTutorialFact(CasinoTutorialFact.RoundPresented, sequence, stationId);
        }

        private void RegisterTutorialPlayerExit()
        {
            if (TutorialStatus == CasinoTutorialStatus.Active && TutorialStep == CasinoTutorialStep.LeaveSlots &&
                focusedStation != null && focusedStation.StationId == CasinoAdventureSession.TutorialSlotsStation && tableFocus?.IsReady == true)
                tutorialPendingExitStation = focusedStation.StationId;
        }
        private void ResetTutorialObservations()
        {
            tutorialObservedRun = null; tutorialLookDegrees = 0; tutorialWalkMeters = 0;
            tutorialReportedLook = 0; tutorialReportedWalk = 0; tutorialReportedWalkStation = null;
            tutorialNextObservationTime = 0; tutorialAttemptedSettlement = 0; tutorialPendingExitStation = null; TutorialFeedback = null;
        }
        private CasinoAdventureResult ObserveTutorialFact(CasinoTutorialFact fact, int value, string stationId = null, bool publish = true)
        {
            // 教学观察有自己的只读事实入口，不能占用lastUserCommandFrame或伪造经济请求编号。
            var result = Game.ObserveTutorial(fact, value, stationId, publish);
            if (result.Changed)
            {
                TutorialFeedback = null;
            }
            else if (!result.Success && publish)
            {
                TutorialFeedback = result.Error == "TutorialCooperationRequired" ? "还没合拍成功。等绿灯亮起，再试一次。" :
                    result.Error == "TutorialChoiceRequired" ? "这次再试试要牌或停牌。" :
                    result.Error == "TutorialResultRequired" ? "在这张桌上玩一局，等演出结束后查看结果。" : result.Description ?? result.Error;
                ImmersionInputChanged?.Invoke();
            }
            return result;
        }
        private CasinoAdventureResult PublishTutorialFeedback(CasinoAdventureResult result)
        {
            TutorialFeedback = result.Description ?? result.Error;
            ImmersionInputChanged?.Invoke(); return result;
        }
        private CasinoAdventureResult TutorialFailure(string error, string description) => PublishTutorialFeedback(
            new CasinoAdventureResult { Error = error, Description = description, Balance = Game.State?.Coins ?? 0 });
    }
}
