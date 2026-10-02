using System;
using System.Collections.Generic;

namespace Hotfix.JinxCasino.Rules
{
    public enum CasinoCosmeticKind { Color, Hat, Emote, Title }
    public enum CasinoProfileMetric { Always, FinishedRuns, Fame, Bets, Pranks, Rescues, Tasks, Withdrawals, DignifiedExits, Takeovers, AllDiscoveries, BestEndingCoins }

    /// 永久奖励定义；Threshold比较累计真实统计，Requirement直接用于界面展示。
    [Serializable]
    public sealed class CasinoCosmeticDefinition
    {
        public string Id;
        public string Name;
        public CasinoCosmeticKind Kind;
        public CasinoProfileMetric Metric;
        public long Threshold;
        public string Requirement;
        public string ColorHex;
    }

    /// 单个已结束正式旅程的真实摘要；无每局完整投入账本，因此不声称统计输赢次数。
    [Serializable]
    public sealed class CasinoFinishedRunRecord
    {
        public string RunId;
        public CasinoAdventureMode Mode;
        public CasinoAdventureEnding Ending;
        public int CompletedStages;
        public long EndingCoins;
        public long SubmittedBets;
        /// 成功use操作数，包含预备；不是无法从现有记录证明的实际消费数。
        public long ItemActions;
        public long Pranks;
        public long Rescues;
        public long Tasks;
        public long ProtectionBlocks;
        public long Fame;
    }

    /// 可序列化职业档案；外部只能取得独立Data副本，不得直接修改档案内部列表。
    [Serializable]
    public sealed class CasinoProfileData
    {
        public int SchemaVersion = 1;
        public long FinishedRuns;
        public long StandardRuns;
        public long EndlessRuns;
        public long CompletedStages;
        public long SubmittedBets;
        public long ItemActions;
        public long Pranks;
        public long Rescues;
        public long Tasks;
        public long ProtectionBlocks;
        public long BestEndingCoins;
        public long Withdrawals;
        public long DignifiedExits;
        public long Takeovers;
        public long Fame;
        public string EquippedColor = "color_blue";
        public string EquippedHat = "hat_none";
        public string EquippedEmote = "emote_wave";
        public string EquippedTitle = "title_newcomer";
        public List<string> UnlockedIds = new List<string>();
        public List<CasinoGameKind> DiscoveredGames = new List<CasinoGameKind>();
        public List<string> DiscoveredItems = new List<string>();
        public List<string> DiscoveredEvents = new List<string>();
        public List<CasinoAdventureEnding> DiscoveredEndings = new List<CasinoAdventureEnding>();
        public List<CasinoFinishedRunRecord> FinishedRunRecords = new List<CasinoFinishedRunRecord>();
    }
}
