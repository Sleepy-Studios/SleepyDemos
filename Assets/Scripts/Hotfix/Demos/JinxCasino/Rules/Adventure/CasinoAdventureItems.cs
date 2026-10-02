using System;

namespace Hotfix.JinxCasino.Rules
{
    public sealed partial class CasinoAdventureSession
    {
        /// <summary>使用可用钱包购买一个道具；已锁定投入不能用于购物。</summary>
        /// <param name="requestId">唯一请求 ID，重发不重复付款或增加库存。</param>
        /// <param name="itemId">目录与本局商店配置中的稳定 ID。</param>
        public CasinoAdventureResult Purchase(string requestId, string itemId)
            => Execute(requestId, "purchase:" + itemId, () =>
            {
                if (state.Phase == CasinoAdventurePhase.Ended || state.Phase == CasinoAdventurePhase.Failed || state.Phase == CasinoAdventurePhase.Closing) return Fail("WrongPhase", "当前阶段不能购物。");
                var item = CasinoContentCatalog.FindItem(itemId);
                if (item == null || state.Config.ShopItemIds.Length > 0 && Array.IndexOf(state.Config.ShopItemIds, itemId) < 0) return Fail("ItemUnavailable", "该商品不在本局商店。");
                if (AvailableCoins < item.Price) return Fail("InsufficientCoins", "未锁定筹码不足。");
                AddInventory(itemId, 1);
                state.Coins -= item.Price;
                return Ok("购买" + item.Name + "，支付 " + item.Price + " 筹码。");
            });

        /// <summary>原子使用库存；规则或阶段拒绝时保留库存，整蛊同目标五秒保护。</summary>
        /// <param name="requestId">唯一请求 ID，重发不得再次消费。</param>
        /// <param name="itemId">已有库存的目录 ID。</param>
        /// <param name="targetId">宿主验证的目标稳定 ID，默认 team；最多64字符。</param>
        public CasinoAdventureResult UseItem(string requestId, string itemId, string targetId = "team")
            => Execute(requestId, "use:" + itemId + ":" + targetId, () =>
            {
                if (state.Phase == CasinoAdventurePhase.Ended) return Fail("WrongPhase", "冒险已结束。");
                if (string.IsNullOrWhiteSpace(targetId) || targetId.Length > 64) return Fail("InvalidTarget", "目标标识非法。");
                var item = CasinoContentCatalog.FindItem(itemId);
                if (item == null || CountItem(itemId) < 1) return Fail("NoItem", "库存中没有该道具。");
                bool preparing = item.Behavior == CasinoItemBehavior.Rule && round == null;
                var applied = ApplyItem(item, targetId);
                if (!applied.Success) return applied;
                if (!preparing) RemoveInventory(itemId);
                if (round != null && round.IsComplete) SettleRound();
                return applied;
            });

        /// <summary>取消尚未提交下注的预备标记；库存尚未消费，活动局不能取消已应用道具。</summary>
        /// <param name="requestId">唯一请求 ID，重发不重复归还。</param>
        /// <param name="itemId">GetPreparedItems 中的稳定 ID。</param>
        public CasinoAdventureResult CancelPreparedItem(string requestId, string itemId)
            => Execute(requestId, "unprepare:" + itemId, () =>
            {
                if (round != null || state.Phase == CasinoAdventurePhase.Ended) return Fail("WrongPhase", "已提交的局不能撤销道具。");
                if (!state.PreparedItems.Remove(itemId)) return Fail("NotPrepared", "该道具尚未预备。");
                return Ok("预备已取消，库存未消费。");
            });

        private CasinoAdventureResult ApplyItem(CasinoItemDefinition item, string targetId)
        {
            switch (item.Behavior)
            {
                case CasinoItemBehavior.Rule:
                    if (round != null)
                    {
                        if (!round.ApplyRuleItem(item.Id)) return Fail("IncompatibleItem", "该局不接受道具，库存保留。");
                        return Ok(item.Name + "已在本局应用：" + round.Description);
                    }
                    if (state.Phase != CasinoAdventurePhase.Playing && state.Phase != CasinoAdventurePhase.Shopping && state.Phase != CasinoAdventurePhase.Finale) return Fail("WrongPhase", "不能在当前阶段预备规则道具。");
                    if (state.PreparedItems.Contains(item.Id)) return Fail("AlreadyPrepared", "同种规则道具每局只能预备一次。");
                    state.PreparedItems.Add(item.Id);
                    return Ok(item.Name + "已预备；下一下注前确认，机台不兼容会拒绝投入，可取消预备退回库存。");
                case CasinoItemBehavior.AddTime:
                    if (state.Mode == CasinoAdventureMode.Practice || state.Phase != CasinoAdventurePhase.Playing) return Fail("WrongPhase", "加时券仅用于进行中的计时区域。");
                    state.RemainingMilliseconds = checked(state.RemainingMilliseconds + (int)item.Value);
                    return Ok("本区增加30秒。");
                case CasinoItemBehavior.ShieldEvent:
                    state.EventShieldCharges = checked(state.EventShieldCharges + 1);
                    return Ok("共享护盾已准备，抵消下一负面环境事件。");
                case CasinoItemBehavior.RelayCharge:
                    if (!HasOpenMission("power_relay")) return Fail("NoRelayTask", "没有进行中的接力送电任务，库存保留。");
                    state.ActiveMission.Progress = Math.Min(state.ActiveMission.TargetCount, state.ActiveMission.Progress + 1);
                    FinishMissionIfReady();
                    return Ok("接力电池完成一次充能。");
                case CasinoItemBehavior.Rescue:
                    if (state.Phase != CasinoAdventurePhase.Failed || round != null || state.Mode == CasinoAdventureMode.Practice) return Fail("WrongPhase", "救场哨仅用于已失败计时区域。");
                    state.Phase = CasinoAdventurePhase.Playing;
                    state.RemainingMilliseconds = (int)item.Value;
                    return Ok("救场哨重新开放30秒；额度与钱包保持。");
                case CasinoItemBehavior.CooperationHelp:
                    if (round != null)
                    {
                        if (!round.TryApplyCooperationHelp()) return Fail("IncompatibleItem", "本局已接受帮助、线索全部已查看或不支持扳手，库存保留。");
                        AddEffect("CooperationHint", targetId, 3000, 0, "扳手真实帮助已生效，仍需实际完成协作动作。", 0);
                    }
                    else state.CooperationHelpCharges = checked(state.CooperationHelpCharges + 1);
                    return Ok("双人扳手提供一次协作提示，不直接解锁接管。");
                case CasinoItemBehavior.RevealMap:
                    AddEffect(item.EffectKind, targetId, (int)item.Value, 0, "显示本区机台及任务位置15秒。", state.StageIndex);
                    return Ok("地图雷达已开启。");
                case CasinoItemBehavior.Shortcut:
                    if (state.Phase != CasinoAdventurePhase.Playing) return Fail("WrongPhase", "捷径仅能在活动区域打开。");
                    // 以完整阶段身份持久化；加时不关闭捷径，无尽再次来到同一区也不能复用旧钥匙。
                    AddEffect(item.EffectKind, targetId, 0, 0, "开放本区安全捷径至阶段结束，禁止跨过阶段额度。", state.StageIndex);
                    return Ok("本区捷径已开放。");
                case CasinoItemBehavior.TriggerEvent:
                    if (state.Phase != CasinoAdventurePhase.Playing || HasUnresolvedEvent) return Fail("EventBusy", "请先结束当前事件任务或团队选择。");
                    if (!TriggerRandomEvent()) return Fail("NoEnabledEvent", "本局未启用随机事件。");
                    return Ok("事件遥控器触发了" + CasinoContentCatalog.FindEvent(state.CurrentEventId).Name + "。");
                case CasinoItemBehavior.MysteryExchange:
                    if (!TryRandomItem(item.Id, out string awarded)) return Fail("NoRewardItem", "当前配置没有可兑换的兼容道具，兑换券保留。");
                    AddInventory(awarded, 1);
                    return Ok("兑换获得" + CasinoContentCatalog.FindItem(awarded).Name + "。");
                case CasinoItemBehavior.SceneEffect:
                    if (item.IsPrank)
                    {
                        var protectedTarget = state.TargetProtection.Find(entry => entry.TargetId == targetId);
                        if (protectedTarget != null && protectedTarget.UntilMilliseconds > state.ElapsedMilliseconds) return Fail("TargetProtected", "同目标五秒保护中，库存保留。");
                        if (protectedTarget == null) { protectedTarget = new CasinoTargetProtection { TargetId = targetId }; state.TargetProtection.Add(protectedTarget); }
                        protectedTarget.UntilMilliseconds = checked(state.ElapsedMilliseconds + 5000);
                    }
                    AddEffect(item.EffectKind, targetId, (int)Math.Min(3000, item.Value), item.IsPrank ? 5000 : 0, item.Description, 0);
                    return Ok(item.Name + "已生成场景效果请求。");
                default: return Fail("UnsupportedItem", "道具规则未配置。");
            }
        }

        private int CountItem(string id) => state.Inventory.Find(entry => entry.ItemId == id)?.Count ?? 0;
        private void AddInventory(string id, int count)
        {
            var entry = state.Inventory.Find(item => item.ItemId == id);
            if (entry == null) state.Inventory.Add(new CasinoInventoryEntry { ItemId = id, Count = count });
            else entry.Count = checked(entry.Count + count);
        }
        private void RemoveInventory(string id)
        {
            var entry = state.Inventory.Find(item => item.ItemId == id);
            if (entry == null || entry.Count < 1) throw new ArgumentException("库存不足。");
            if (--entry.Count == 0) state.Inventory.Remove(entry);
        }
        private bool TryRandomItem(string excludedId, out string awarded)
        {
            var choices = Array.FindAll(CasinoContentCatalog.Items, item => item.Id != excludedId &&
                (state.Config.ShopItemIds.Length == 0 || Array.IndexOf(state.Config.ShopItemIds, item.Id) >= 0) && HasConfiguredGameForItem(item));
            awarded = null;
            if (choices.Length == 0) return false;
            var random = new CasinoRandom(state.RandomState);
            awarded = choices[random.NextInt(choices.Length)].Id;
            state.RandomState = random.State;
            return true;
        }
        private bool HasConfiguredGameForItem(CasinoItemDefinition item)
        {
            if (item.Behavior != CasinoItemBehavior.Rule || state.Config.AllowedGames.Length == 0) return true;
            switch (item.Id)
            {
                case "lock_reel": return IsGameAllowed(CasinoGameKind.Slots);
                case "reroll_dice": return IsGameAllowed(CasinoGameKind.SicBo) || IsGameAllowed(CasinoGameKind.PushYourLuckDice);
                case "redraw_card": return IsGameAllowed(CasinoGameKind.Blackjack) || IsGameAllowed(CasinoGameKind.HighLow);
                case "xray": return IsGameAllowed(CasinoGameKind.Blackjack) || IsGameAllowed(CasinoGameKind.HighLow) ||
                    IsGameAllowed(CasinoGameKind.LuckyDraw) || IsGameAllowed(CasinoGameKind.BlindAuction) || IsGameAllowed(CasinoGameKind.CooperativeVault);
                case "stop_loss":
                case "jackpot_coupon": return true;
                default: return false;
            }
        }
        private void AddEffect(string kind, string target, int duration, int protection, string description, long value)
        {
            int sequence = checked(++state.EffectSequence);
            state.Effects.Add(new CasinoSceneEffect { Id = state.RunId + ":" + sequence,
                EffectKind = kind, TargetId = target, DurationMilliseconds = duration, ProtectionMilliseconds = protection,
                CreatedAtMilliseconds = state.ElapsedMilliseconds, Description = description, Value = value });
        }
    }
}
