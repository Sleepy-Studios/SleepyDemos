using System;
using System.Collections.Generic;

namespace Hotfix.JinxCasino.Rules
{
    public sealed partial class CasinoAdventureSession
    {
        /// 当前待决策团队事件的明确选项；没有选择事件时为空。
        public CasinoEventActionDescriptor[] GetEventActions()
        {
            if (!state.EventChoicePending) return Array.Empty<CasinoEventActionDescriptor>();
            string accept = state.CurrentEventId == "mystery_merchant" ? "花80筹码买一件随机道具" :
                state.CurrentEventId == "group_insurance" ? "花50筹码获得共享护盾" :
                state.CurrentEventId == "risk_contract" ? "本区额度+200，下一局毛返还+50%" :
                state.CurrentEventId == "item_exchange" ? "交换指定库存道具" : "带剩余筹码撤离";
            return new[] { new CasinoEventActionDescriptor { Choice = 0, Label = "继续当前挑战，拒绝交易" }, new CasinoEventActionDescriptor { Choice = 1, Label = accept } };
        }

        /// <summary>提交本地队伍的事件选择；未付款、缺库存或活动局限制均不消费。</summary>
        /// <param name="requestId">唯一请求 ID，重试不重新随机或支付。</param>
        /// <param name="choice">0拒绝，1接受，须匹配 GetEventActions。</param>
        /// <param name="itemId">道具交换时指定已有非预备道具，其他事件不需要。</param>
        public CasinoAdventureResult ResolveEvent(string requestId, int choice, string itemId = null)
            => Execute(requestId, "event-choice:" + choice + ":" + itemId, () =>
            {
                if (!state.EventChoicePending || state.Phase != CasinoAdventurePhase.Playing) return Fail("NoChoiceEvent", "没有待选择的团队事件。");
                if (choice < 0 || choice > 1) return Fail("InvalidChoice", "事件选项为0或1。");
                if (choice == 0) { state.EventChoicePending = false; return Ok("队伍拒绝事件交易，继续挑战。"); }
                switch (state.CurrentEventId)
                {
                    case "mystery_merchant":
                        if (AvailableCoins < 80) return Fail("InsufficientCoins", "交易需要80未锁定筹码。");
                        if (!TryRandomItem("mystery_coupon", out string awarded)) return Fail("NoRewardItem", "当前配置没有兼容奖励，筹码保留。");
                        state.Coins -= 80;
                        AddInventory(awarded, 1);
                        state.EventChoicePending = false;
                        return Ok("神秘商人交付了" + CasinoContentCatalog.FindItem(awarded).Name + "。");
                    case "group_insurance":
                        if (AvailableCoins < 50) return Fail("InsufficientCoins", "保险需要50未锁定筹码。");
                        state.Coins -= 50;
                        state.EventShieldCharges = checked(state.EventShieldCharges + 1);
                        break;
                    case "risk_contract":
                        state.StageTargetAdjustment = checked(state.StageTargetAdjustment + 200);
                        state.NextPayoutBonusPercent = Math.Min(100, state.NextPayoutBonusPercent + 50);
                        break;
                    case "item_exchange":
                        if (string.IsNullOrEmpty(itemId) || CountItem(itemId) < 1 || state.PreparedItems.Contains(itemId)) return Fail("NoExchangeItem", "须选择一个已有且未预备的库存道具。");
                        if (!TryRandomItem(itemId, out string replacement)) return Fail("NoRewardItem", "当前配置没有不同的兼容道具，原道具保留。");
                        RemoveInventory(itemId); AddInventory(replacement, 1);
                        state.EventChoicePending = false;
                        return Ok("交换获得" + CasinoContentCatalog.FindItem(replacement).Name + "。");
                    case "early_exit":
                        if (round != null) return Fail("RoundActive", "请先完成已提交局，撤离不能取消下注。");
                        state.Ending = CasinoAdventureEnding.Withdraw;
                        state.Phase = CasinoAdventurePhase.Ended;
                        break;
                    default: return Fail("UnknownChoiceEvent", "事件不支持团队决策。");
                }
                state.EventChoicePending = false;
                return Ok("队伍选择已执行。");
            });

        /// <summary>宿主确认场景任务交互后推进目标，唯一请求与拾取序号防止重复奖励。</summary>
        /// <param name="requestId">唯一请求 ID，重发不重复计数或发奖。</param>
        /// <param name="taskId">State.ActiveMission.Id，不可用过期任务 ID。</param>
        /// <param name="action">须匹配任务当前动作；搬箱须先 Carry 再 Deliver。</param>
        /// <param name="value">拾取或检查点序号0..TargetCount-1；搬箱与按钮使用默认零。</param>
        /// <param name="actorId">宿主验证的队员稳定 ID；按钮必须每位不同队员各一次，单人默认为 local。</param>
        public CasinoAdventureResult AdvanceTask(string requestId, string taskId, CasinoTaskAction action, int value = 0, string actorId = "local")
            => Execute(requestId, "task:" + taskId + ":" + (int)action + ":" + value + ":" + actorId, () =>
            {
                var mission = state.ActiveMission;
                if (mission == null || mission.Id != taskId || mission.Completed || mission.Failed || state.Phase != CasinoAdventurePhase.Playing || state.ElapsedMilliseconds >= mission.DeadlineMilliseconds)
                    return Fail("TaskUnavailable", "任务未出现、已结束或超时。");
                if (string.IsNullOrWhiteSpace(actorId) || actorId.Length > 64) return Fail("InvalidActor", "任务队员标识非法。");
                if (mission.EventId == "gold_delivery")
                {
                    if (!mission.Carrying && action == CasinoTaskAction.Carry)
                    {
                        mission.Carrying = true; mission.Action = CasinoTaskAction.Deliver;
                        AddEffect("CarryGold", actorId, 0, 0, "金箱已拿起，送至交付点后提交Deliver。", 0);
                        return Ok("金箱已拿起，请送到交付点。");
                    }
                    if (!mission.Carrying || action != CasinoTaskAction.Deliver) return Fail("InvalidTaskAction", "须先搬起金箱再送达。");
                    mission.Progress = 1; mission.Carrying = false;
                }
                else if (mission.EventId == "sync_buttons")
                {
                    if (action != CasinoTaskAction.Press || mission.Actors.Contains(actorId)) return Fail("InvalidTaskAction", "每位队员只计一次按钮。");
                    mission.Actors.Add(actorId); mission.Progress++;
                }
                else
                {
                    if (action != mission.Action || value < 0 || value >= mission.TargetCount || mission.Visited.Contains(value)) return Fail("InvalidTaskAction", "须交互未完成的唯一任务点。");
                    if (mission.EventId == "mascot_chase" && value != mission.Progress) return Fail("WrongCheckpoint", "吉祥物检查点须按顺序追逐。");
                    mission.Visited.Add(value); mission.Progress++;
                }
                FinishMissionIfReady();
                return Ok(mission.Completed ? "任务完成，已发放 " + mission.RewardCoins + " 筹码。" : "任务进度 " + mission.Progress + "/" + mission.TargetCount + "。");
            });

        private bool HasUnresolvedEvent => state.EventChoicePending || state.ActiveMission != null && !state.ActiveMission.Completed && !state.ActiveMission.Failed;
        private bool HasOpenMission(string eventId) => state.ActiveMission != null && state.ActiveMission.EventId == eventId &&
            !state.ActiveMission.Completed && !state.ActiveMission.Failed && state.ElapsedMilliseconds < state.ActiveMission.DeadlineMilliseconds && state.Phase == CasinoAdventurePhase.Playing;

        private bool TriggerRandomEvent()
        {
            if (HasUnresolvedEvent) return false;
            var enabled = new List<CasinoEventDefinition>();
            var weights = new List<int>();
            int total = 0;
            foreach (var entry in CasinoContentCatalog.Events)
            {
                var configured = Array.Find(state.Config.EventWeights, weight => weight.EventId == entry.Id);
                int weight = configured?.Weight ?? entry.Weight;
                if (weight <= 0) continue;
                enabled.Add(entry); weights.Add(weight); total = checked(total + weight);
            }
            if (total == 0) return false;
            var random = new CasinoRandom(state.RandomState);
            int ticket = random.NextInt(total);
            state.RandomState = random.State;
            int index = 0;
            while (ticket >= weights[index]) { ticket -= weights[index]; index++; }
            ApplyEvent(enabled[index]);
            return true;
        }

        private void ApplyEvent(CasinoEventDefinition entry)
        {
            state.CurrentEventId = entry.Id;
            if ((entry.Behavior == CasinoEventBehavior.SceneEffect || entry.Behavior == CasinoEventBehavior.RepairMission) && state.EventShieldCharges > 0)
            {
                state.EventShieldCharges--;
                AddEffect("ShieldBlocked", "team", 1200, 0, "共享护盾抵消了" + entry.Name + "。", 0);
                return;
            }
            if (entry.Behavior == CasinoEventBehavior.StakeCap || entry.Behavior == CasinoEventBehavior.PayoutBonus || entry.Behavior == CasinoEventBehavior.DiceBias || entry.Behavior == CasinoEventBehavior.Multiplier)
            {
                ClearEventRules();
                state.RuleExpiresMilliseconds = checked(state.ElapsedMilliseconds + 45000);
                if (entry.Behavior == CasinoEventBehavior.StakeCap) { state.EventStakeCap = entry.Value; state.EventPayoutBonusPercent = 20; }
                else if (entry.Behavior == CasinoEventBehavior.DiceBias) state.NextDiceBias = (int)entry.Value;
                else state.EventPayoutBonusPercent = (int)entry.Value;
            }
            else if (entry.Behavior == CasinoEventBehavior.RotateGames) state.GameRotationOffset = checked(state.GameRotationOffset + 1);
            else if (entry.Behavior == CasinoEventBehavior.TeamChoice) state.EventChoicePending = true;
            else if (entry.Behavior == CasinoEventBehavior.SceneEffect)
                AddProtectedEnvironmentEffect(entry);
            else if (entry.Behavior == CasinoEventBehavior.Mission || entry.Behavior == CasinoEventBehavior.RepairMission)
            {
                int count = entry.Id == "chip_rain" ? 5 : entry.Id == "gold_delivery" ? 1 : entry.Id == "sync_buttons" ? state.PlayerCount : 3;
                CasinoTaskAction action = entry.Id == "chip_rain" ? CasinoTaskAction.Collect : entry.Id == "mascot_chase" ? CasinoTaskAction.Touch :
                    entry.Id == "gold_delivery" ? CasinoTaskAction.Carry : entry.Id == "power_relay" ? CasinoTaskAction.Charge : entry.Id == "sync_buttons" ? CasinoTaskAction.Press : CasinoTaskAction.Repair;
                state.ActiveMission = new CasinoAdventureMission { Id = "task:" + state.RunId + ":" + checked(++state.EffectSequence),
                    EventId = entry.Id, Description = entry.Description, Action = action, TargetCount = count,
                    DeadlineMilliseconds = checked(state.ElapsedMilliseconds + 45000), RewardCoins = checked(entry.Value * (100L + (state.PlayerCount - 1L) * 25) / 100) };
                AddEffect(entry.EffectKind, "team", 45000, 0, entry.Description, count);
            }
            AddEffect("EventAnnounce", "team", 2500, 0, entry.Name + "：" + entry.Description, 0);
        }

        private void AddProtectedEnvironmentEffect(CasinoEventDefinition entry)
        {
            const string target = "team";
            var protection = state.TargetProtection.Find(record => record.TargetId == target);
            if (protection != null && protection.UntilMilliseconds > state.ElapsedMilliseconds)
            {
                AddEffect("EnvironmentProtected", target, 1000, 0, "五秒保护抵消了" + entry.Name + "。", 0);
                return;
            }
            if (protection == null) { protection = new CasinoTargetProtection { TargetId = target }; state.TargetProtection.Add(protection); }
            protection.UntilMilliseconds = checked(state.ElapsedMilliseconds + 5000);
            AddEffect(entry.EffectKind, target, (int)Math.Min(3000, entry.Value), 5000, entry.Description, 0);
        }

        private void FinishMissionIfReady()
        {
            var mission = state.ActiveMission;
            if (mission == null || mission.Completed || mission.Failed || mission.Progress < mission.TargetCount) return;
            state.Coins = checked(state.Coins + mission.RewardCoins);
            mission.Completed = true;
            if (mission.EventId == "power_repair") AddEffect("PowerRestored", "team", 1000, 0, "抢修完成，灯光恢复。", 0);
            AddEffect("TaskCompleted", "team", 1500, 0, "完成" + CasinoContentCatalog.FindEvent(mission.EventId).Name + "，团队获得 " + mission.RewardCoins + "。", mission.RewardCoins);
        }

        private void ExpireEventState()
        {
            if (state.RuleExpiresMilliseconds > 0 && state.ElapsedMilliseconds >= state.RuleExpiresMilliseconds) ClearEventRules();
            if (state.ActiveMission != null && !state.ActiveMission.Completed && !state.ActiveMission.Failed && state.ElapsedMilliseconds >= state.ActiveMission.DeadlineMilliseconds)
            {
                state.ActiveMission.Failed = true;
                AddEffect("TaskExpired", "team", 1000, 0, "任务超时，无奖励；已提交局不受影响。", 0);
            }
        }
        private void ClearEventRules()
        {
            state.NextDiceBias = 0; state.EventPayoutBonusPercent = 0; state.EventStakeCap = 0; state.RuleExpiresMilliseconds = 0;
        }
    }
}
