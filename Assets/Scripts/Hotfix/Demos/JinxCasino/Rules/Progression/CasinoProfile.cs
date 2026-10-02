using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Hotfix.JinxCasino.Rules
{
    /// 已结束旅程的永久成长聚合；成功操作与效果记录是唯一统计来源，所有更新先在副本完成。
    public sealed class CasinoProfile
    {
        private CasinoProfileData data;
        private CasinoProfile(CasinoProfileData value) { data = value; }
        /// 档案深副本；修改列表或摘要不会改动当前档案。
        public CasinoProfileData Data => Clone(data);
        /// 每100声望升一级，初始等级1；等级不影响下注规则。
        public long Level => 1 + data.Fame / 100;

        /// 创建含默认外观、称号且尚无旅程记录的档案。
        public static CasinoProfile Create()
        {
            var value = new CasinoProfileData(); Unlock(value); return new CasinoProfile(value);
        }

        /// <summary>记录已结束正式/无尽旅程，同RunId只计一次；练习与未结束状态返回false。</summary>
        /// <param name="state">领域导出的完整结束快照；非法结束状态抛异常且不改变档案。</param>
        /// <returns>是否首次提交该旅程；外部持久化须在返回true后保存。</returns>
        public bool RecordFinishedRun(CasinoAdventureState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Mode == CasinoAdventureMode.Practice || state.Phase != CasinoAdventurePhase.Ended) return false;
            var source = CasinoAdventureSession.Restore(JsonUtility.ToJson(state)).CaptureState();
            if (source.Ending == CasinoAdventureEnding.None || source.LockedCoins != 0 || !string.IsNullOrEmpty(source.ActiveRoundJson) ||
                source.Mode == CasinoAdventureMode.Endless && source.Ending != CasinoAdventureEnding.Withdraw ||
                source.Mode == CasinoAdventureMode.Standard && source.CompletedStages > source.Config.StageCount ||
                source.Ending != CasinoAdventureEnding.Withdraw && (source.CompletedStages < source.Config.StageCount ||
                    source.Ending == CasinoAdventureEnding.TakeOver && !source.TakeOverUnlocked))
                throw new ArgumentException("只能记录实际完成结局且不存在未结算机台的旅程。", nameof(state));
            if (data.FinishedRunRecords.Any(record => record.RunId == source.RunId)) return false;
            var candidate = Clone(data);
            var record = new CasinoFinishedRunRecord
            {
                RunId = source.RunId, Mode = source.Mode, Ending = source.Ending,
                CompletedStages = source.CompletedStages, EndingCoins = source.Coins
            };
            var requestIds = new HashSet<string>(StringComparer.Ordinal);
            var effects = new Dictionary<string, CasinoSceneEffect>(StringComparer.Ordinal);
            foreach (var effect in source.Effects) AddEffect(effects, effect);
            foreach (var request in source.ProcessedRequests)
            {
                if (!requestIds.Add(request.RequestId) || request.Result == null) continue;
                if (request.Result.Success)
                {
                    foreach (var effect in request.Result.Effects ?? Array.Empty<CasinoSceneEffect>()) AddEffect(effects, effect);
                    string[] parts = request.Fingerprint.Split(':');
                    if (parts.Length == 4 && parts[0] == "bet" && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int game) &&
                        game >= 0 && game <= (int)CasinoGameKind.ChickenElevator)
                    { record.SubmittedBets++; AddUnique(candidate.DiscoveredGames, (CasinoGameKind)game); }
                    if (parts.Length >= 2 && (parts[0] == "purchase" || parts[0] == "use"))
                    {
                        var item = CasinoContentCatalog.FindItem(parts[1]);
                        if (item == null) continue;
                        AddUnique(candidate.DiscoveredItems, item.Id);
                        if (parts[0] == "use") { record.ItemActions++; if (item.Behavior == CasinoItemBehavior.Rescue) record.Rescues++; }
                    }
                }
            }
            foreach (var inventory in source.Inventory) if (inventory.Count > 0) AddUnique(candidate.DiscoveredItems, inventory.ItemId);
            foreach (var effect in effects.Values)
            {
                if (effect.EffectKind == "TaskCompleted") record.Tasks++;
                if (effect.EffectKind == "EnvironmentProtected" || effect.EffectKind == "ShieldBlocked") record.ProtectionBlocks++;
                // 只统计带保护期的真实整蛊效果，不把未消费的TargetProtected失败请求当成成功整蛊。
                if (effect.ProtectionMilliseconds > 0 && CasinoContentCatalog.Items.Any(item => item.IsPrank && item.EffectKind == effect.EffectKind)) record.Pranks++;
                if (effect.EffectKind == "EventAnnounce")
                {
                    var definition = Array.Find(CasinoContentCatalog.Events, entry => entry.Name + "：" + entry.Description == effect.Description);
                    if (definition != null) AddUnique(candidate.DiscoveredEvents, definition.Id);
                }
            }
            if (CasinoContentCatalog.FindEvent(source.CurrentEventId) != null) AddUnique(candidate.DiscoveredEvents, source.CurrentEventId);
            if (source.ActiveMission != null && CasinoContentCatalog.FindEvent(source.ActiveMission.EventId) != null) AddUnique(candidate.DiscoveredEvents, source.ActiveMission.EventId);
            AddUnique(candidate.DiscoveredEndings, source.Ending);
            record.Fame = CalculateFame(record);
            candidate.FinishedRunRecords.Add(record);
            Recalculate(candidate); Unlock(candidate); Validate(candidate);
            data = candidate;
            return true;
        }

        /// <summary>原子装备已解锁外观/称号；任一ID未知、类型错误或未解锁返回false，原装备保留。</summary>
        /// <param name="colorId">配色ID，null保留当前。</param>
        /// <param name="hatId">帽子ID，null保留当前。</param>
        /// <param name="emoteId">表情ID，null保留当前。</param>
        /// <param name="titleId">称号ID，null保留当前。</param>
        /// <returns>整组装备是否合法且已应用。</returns>
        public bool Equip(string colorId = null, string hatId = null, string emoteId = null, string titleId = null)
        {
            string color = colorId ?? data.EquippedColor, hat = hatId ?? data.EquippedHat;
            string emote = emoteId ?? data.EquippedEmote, title = titleId ?? data.EquippedTitle;
            if (!CanEquip(data, color, CasinoCosmeticKind.Color) || !CanEquip(data, hat, CasinoCosmeticKind.Hat) ||
                !CanEquip(data, emote, CasinoCosmeticKind.Emote) || !CanEquip(data, title, CasinoCosmeticKind.Title)) return false;
            data.EquippedColor = color; data.EquippedHat = hat; data.EquippedEmote = emote; data.EquippedTitle = title; return true;
        }

        /// 导出完整永久档案；不包含运行中机台或三槽进度。
        public string ToJson() => JsonUtility.ToJson(data);

        /// <summary>恢复已验证档案，不重新奖励已记录RunId。</summary>
        /// <param name="json">SchemaVersion=1完整档案；非法数据抛ArgumentException。</param>
        /// <returns>独立恢复对象。</returns>
        public static CasinoProfile Restore(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 16777216) throw new ArgumentException("档案为空或过大。", nameof(json));
            CasinoProfileData value;
            try { value = JsonUtility.FromJson<CasinoProfileData>(json); Validate(value); }
            catch (Exception exception) when (exception is ArgumentException || exception is OverflowException || exception is NullReferenceException)
            { throw new ArgumentException("永久档案结构或累计统计非法。", nameof(json), exception); }
            return new CasinoProfile(value);
        }

        private static void AddEffect(Dictionary<string, CasinoSceneEffect> values, CasinoSceneEffect effect)
        { if (effect != null && !string.IsNullOrEmpty(effect.Id) && !values.ContainsKey(effect.Id)) values.Add(effect.Id, effect); }
        private static void AddUnique<T>(List<T> values, T value) { if (!values.Contains(value)) values.Add(value); }
        private static CasinoProfileData Clone(CasinoProfileData value) => JsonUtility.FromJson<CasinoProfileData>(JsonUtility.ToJson(value));
        private static long CalculateFame(CasinoFinishedRunRecord record)
            => checked(10L + record.CompletedStages * 5L + record.Tasks * 3 + record.Pranks + record.Rescues * 2 +
                (record.Ending == CasinoAdventureEnding.TakeOver ? 40 : record.Ending == CasinoAdventureEnding.LeaveWithDignity ? 20 : 0));

        private static void Recalculate(CasinoProfileData value)
        {
            value.FinishedRuns = value.StandardRuns = value.EndlessRuns = value.CompletedStages = value.SubmittedBets = value.ItemActions =
                value.Pranks = value.Rescues = value.Tasks = value.ProtectionBlocks = value.BestEndingCoins = value.Withdrawals = value.DignifiedExits = value.Takeovers = value.Fame = 0;
            foreach (var record in value.FinishedRunRecords)
            {
                checked
                {
                    value.FinishedRuns++; if (record.Mode == CasinoAdventureMode.Standard) value.StandardRuns++; else value.EndlessRuns++;
                    value.CompletedStages += record.CompletedStages; value.SubmittedBets += record.SubmittedBets; value.ItemActions += record.ItemActions;
                    value.Pranks += record.Pranks; value.Rescues += record.Rescues; value.Tasks += record.Tasks; value.ProtectionBlocks += record.ProtectionBlocks;
                    value.BestEndingCoins = Math.Max(value.BestEndingCoins, record.EndingCoins); value.Fame += record.Fame;
                    if (record.Ending == CasinoAdventureEnding.Withdraw) value.Withdrawals++;
                    if (record.Ending == CasinoAdventureEnding.LeaveWithDignity) value.DignifiedExits++;
                    if (record.Ending == CasinoAdventureEnding.TakeOver) value.Takeovers++;
                }
            }
        }

        private static long Metric(CasinoProfileData value, CasinoProfileMetric metric)
        {
            switch (metric)
            {
                case CasinoProfileMetric.Always: return 1;
                case CasinoProfileMetric.FinishedRuns: return value.FinishedRuns;
                case CasinoProfileMetric.Fame: return value.Fame;
                case CasinoProfileMetric.Bets: return value.SubmittedBets;
                case CasinoProfileMetric.Pranks: return value.Pranks;
                case CasinoProfileMetric.Rescues: return value.Rescues;
                case CasinoProfileMetric.Tasks: return value.Tasks;
                case CasinoProfileMetric.Withdrawals: return value.Withdrawals;
                case CasinoProfileMetric.DignifiedExits: return value.DignifiedExits;
                case CasinoProfileMetric.Takeovers: return value.Takeovers;
                case CasinoProfileMetric.BestEndingCoins: return value.BestEndingCoins;
                case CasinoProfileMetric.AllDiscoveries: return value.DiscoveredGames.Count == 17 && value.DiscoveredItems.Count == 24 && value.DiscoveredEvents.Count == 20 && value.DiscoveredEndings.Count == 3 ? 1 : 0;
                default: return 0;
            }
        }
        private static void Unlock(CasinoProfileData value)
        { foreach (var definition in CasinoProfileCatalog.Definitions) if (Metric(value, definition.Metric) >= definition.Threshold) AddUnique(value.UnlockedIds, definition.Id); }
        private static bool CanEquip(CasinoProfileData value, string id, CasinoCosmeticKind kind)
            => CasinoProfileCatalog.Find(id)?.Kind == kind && value.UnlockedIds.Contains(id);

        // 不信任摘要：从逐Run记录重算统计与奖励，拒绝未知图鉴/装备、重复Run和声望篡改。
        private static void Validate(CasinoProfileData value)
        {
            if (value == null || value.SchemaVersion != 1 || value.FinishedRunRecords == null || value.UnlockedIds == null || value.DiscoveredGames == null ||
                value.DiscoveredItems == null || value.DiscoveredEvents == null || value.DiscoveredEndings == null) throw new ArgumentException("档案列表非法。");
            var runs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var record in value.FinishedRunRecords)
                if (record == null || !Guid.TryParseExact(record.RunId, "N", out _) || !runs.Add(record.RunId) ||
                    record.Mode != CasinoAdventureMode.Standard && record.Mode != CasinoAdventureMode.Endless ||
                    record.Ending < CasinoAdventureEnding.Withdraw || record.Ending > CasinoAdventureEnding.TakeOver ||
                    record.Mode == CasinoAdventureMode.Endless && record.Ending != CasinoAdventureEnding.Withdraw ||
                    record.CompletedStages < 0 || record.EndingCoins < 0 || record.SubmittedBets < 0 || record.ItemActions < 0 || record.Pranks < 0 || record.Rescues < 0 ||
                    record.Rescues > record.ItemActions || record.Pranks > record.ItemActions ||
                    record.Mode == CasinoAdventureMode.Standard && record.CompletedStages > 4 || record.Ending != CasinoAdventureEnding.Withdraw && record.CompletedStages == 0 ||
                    record.Tasks < 0 || record.ProtectionBlocks < 0 || record.Fame != CalculateFame(record)) throw new ArgumentException("旅程摘要非法或重复。");
            CheckUnique(value.DiscoveredGames, game => game >= CasinoGameKind.Slots && game <= CasinoGameKind.ChickenElevator);
            CheckUnique(value.DiscoveredItems, id => CasinoContentCatalog.FindItem(id) != null);
            CheckUnique(value.DiscoveredEvents, id => CasinoContentCatalog.FindEvent(id) != null);
            CheckUnique(value.DiscoveredEndings, ending => ending >= CasinoAdventureEnding.Withdraw && ending <= CasinoAdventureEnding.TakeOver);
            CheckUnique(value.UnlockedIds, id => CasinoProfileCatalog.Find(id) != null);
            var calculated = Clone(value); Recalculate(calculated);
            if (value.FinishedRuns != calculated.FinishedRuns || value.StandardRuns != calculated.StandardRuns || value.EndlessRuns != calculated.EndlessRuns ||
                value.CompletedStages != calculated.CompletedStages || value.SubmittedBets != calculated.SubmittedBets || value.ItemActions != calculated.ItemActions ||
                value.Pranks != calculated.Pranks || value.Rescues != calculated.Rescues || value.Tasks != calculated.Tasks || value.ProtectionBlocks != calculated.ProtectionBlocks ||
                value.BestEndingCoins != calculated.BestEndingCoins || value.Withdrawals != calculated.Withdrawals || value.DignifiedExits != calculated.DignifiedExits || value.Takeovers != calculated.Takeovers || value.Fame != calculated.Fame)
                throw new ArgumentException("档案累计与旅程摘要不一致。");
            calculated.UnlockedIds.Clear(); Unlock(calculated);
            if (!new HashSet<string>(value.UnlockedIds).SetEquals(calculated.UnlockedIds)) throw new ArgumentException("奖励与累计记录不一致。");
            var expectedEndings = new HashSet<CasinoAdventureEnding>(value.FinishedRunRecords.Select(record => record.Ending));
            if (value.DiscoveredGames.Count > value.SubmittedBets || !expectedEndings.SetEquals(value.DiscoveredEndings) || !CanEquip(value, value.EquippedColor, CasinoCosmeticKind.Color) || !CanEquip(value, value.EquippedHat, CasinoCosmeticKind.Hat) ||
                !CanEquip(value, value.EquippedEmote, CasinoCosmeticKind.Emote) || !CanEquip(value, value.EquippedTitle, CasinoCosmeticKind.Title)) throw new ArgumentException("装备或结局图鉴非法。");
        }
        private static void CheckUnique<T>(List<T> values, Func<T, bool> valid)
        { var seen = new HashSet<T>(); foreach (T value in values) if (!valid(value) || !seen.Add(value)) throw new ArgumentException("图鉴或解锁ID非法或重复。"); }
    }
}
