using System;
using System.Collections.Generic;

namespace Hotfix.JinxCasino.Rules
{
    public enum CasinoAdventureMode { Standard, Practice, Endless }
    public enum CasinoAdventurePhase { Playing, Shopping, Closing, Failed, Finale, Ended }
    public enum CasinoAdventureEnding { None, Withdraw, LeaveWithDignity, TakeOver }
    public enum CasinoItemBehavior
    {
        Rule, AddTime, ShieldEvent, RelayCharge, Rescue, CooperationHelp, SceneEffect,
        RevealMap, Shortcut, TriggerEvent, MysteryExchange
    }
    public enum CasinoEventBehavior { StakeCap, PayoutBonus, DiceBias, RotateGames, Multiplier, RepairMission, SceneEffect, TeamChoice, Mission }
    public enum CasinoTaskAction { Collect, Touch, Carry, Deliver, Charge, Press, Repair }

    [Serializable]
    public sealed class CasinoAdventureConfig
    {
        public long StartingCoins = 1000;
        public long MaximumStake = 1000000;
        public int StageCount = 4;
        public long[] Targets = { 1200, 2000, 3500, 5000 };
        public int StageDurationMilliseconds = 240000;
        public int EventIntervalMilliseconds = 45000;
        public int AdditionalPlayerTargetPercent = 25;
        public int EndlessCycleTargetPercent = 50;
        /// 为空时全部商店商品开放；非空时仅列出的稳定 ID 可买。
        public string[] ShopItemIds = Array.Empty<string>();
        /// 为空时全机台开放；非空仅允许本场景实际装配的机台，练习和结局挑战同样遵守。
        public CasinoGameKind[] AllowedGames = Array.Empty<CasinoGameKind>();
        /// 场景明确配置的起始可用玩法，仍受AllowedGames限制；旧档缺失时视为空。
        public CasinoGameKind[] InitiallyAvailableGames = Array.Empty<CasinoGameKind>();
        /// 为空使用目录默认权重，设为 0 可禁用某事件。
        public CasinoEventWeight[] EventWeights = Array.Empty<CasinoEventWeight>();
    }

    [Serializable]
    public sealed class CasinoEventWeight { public string EventId; public int Weight; }
    [Serializable]
    public sealed class CasinoInventoryEntry { public string ItemId; public int Count; }
    [Serializable]
    public sealed class CasinoTargetProtection { public string TargetId; public long UntilMilliseconds; }

    [Serializable]
    public sealed class CasinoSceneEffect
    {
        public string Id;
        public string EffectKind;
        public string TargetId;
        public int DurationMilliseconds;
        public int ProtectionMilliseconds;
        public long CreatedAtMilliseconds;
        public long Value;
        public string Description;
    }

    [Serializable]
    public sealed class CasinoAdventureMission
    {
        public string Id;
        public string EventId;
        public string Description;
        public CasinoTaskAction Action;
        public int Progress;
        public int TargetCount;
        public long DeadlineMilliseconds;
        public long RewardCoins;
        public bool Carrying;
        public bool Completed;
        public bool Failed;
        public List<int> Visited = new List<int>();
        public List<string> Actors = new List<string>();
    }

    [Serializable]
    public sealed class CasinoEventActionDescriptor
    {
        public int Choice;
        public string Label;
    }

    [Serializable]
    public sealed class CasinoAdventureResult
    {
        public bool Success;
        public bool Changed;
        public string Error;
        public string Description;
        public long Balance;
        public CasinoSceneEffect[] Effects = Array.Empty<CasinoSceneEffect>();
    }

    [Serializable]
    public sealed class CasinoAdventureRequestRecord
    {
        public string RequestId;
        public string Fingerprint;
        public CasinoAdventureResult Result;
    }

    [Serializable]
    public sealed class CasinoAdventureState
    {
        public int SchemaVersion = 2;
        public string RunId;
        public uint Seed;
        public uint RandomState;
        public CasinoAdventureConfig Config;
        public CasinoAdventureMode Mode;
        public CasinoAdventurePhase Phase;
        public CasinoAdventureEnding Ending;
        public int PlayerCount;
        public int StageIndex;
        public int CompletedStages;
        public long Coins;
        /// 最大投入仍在 Coins 中，但不能购物或被事件扣走，结算仅扣实际 Cost。
        public long LockedCoins;
        public long StageTarget;
        public long StageTargetAdjustment;
        public int RemainingMilliseconds;
        public long ElapsedMilliseconds;
        public int EventCountdownMilliseconds;
        public int Revision;
        public int EffectSequence;
        public int EventShieldCharges;
        public int NextPayoutBonusPercent;
        public int ActiveRoundPayoutBonusPercent;
        public int NextDiceBias;
        public int EventPayoutBonusPercent;
        public long EventStakeCap;
        public long RuleExpiresMilliseconds;
        public int GameRotationOffset;
        public bool EventChoicePending;
        public int CooperationHelpCharges;
        public CasinoAdventureMission ActiveMission;
        public bool TakeOverUnlocked;
        public bool ActiveRoundIsChallenge;
        public CasinoGameKind ActiveGame;
        /// 已投入局绑定的具体机台；旧版未定位的局为空，须显式认领后才能使用新桌面。
        public string ActiveStationId;
        /// 最近结算发生的机台，防止同类机台播放其它桌面的结果。
        public string LastStationId;
        public long LastRoundCost;
        public long LastRoundPayout;
        public string LastRoundDescription;
        /// 只在原子结算后递增，表现及音效不能因请求重发再次播放。
        public int SettledRoundSequence;
        /// 最近完成局的完整快照，供安全表现投影；不作为再次支付的入口。
        public string LastRoundJson;
        public string ActiveRoundJson;
        public string CurrentEventId;
        public List<CasinoInventoryEntry> Inventory = new List<CasinoInventoryEntry>();
        public List<string> PreparedItems = new List<string>();
        public List<CasinoSceneEffect> Effects = new List<CasinoSceneEffect>();
        public List<CasinoTargetProtection> TargetProtection = new List<CasinoTargetProtection>();
        public List<CasinoAdventureRequestRecord> ProcessedRequests = new List<CasinoAdventureRequestRecord>();
    }

    [Serializable]
    public sealed class CasinoItemDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public long Price;
        public CasinoItemBehavior Behavior;
        public long Value;
        public string EffectKind;
        public bool IsPrank;
    }

    [Serializable]
    public sealed class CasinoEventDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public int Weight;
        public CasinoEventBehavior Behavior;
        public long Value;
        public string EffectKind;
    }

    [Serializable]
    public sealed class CasinoGameDefinition
    {
        public string Id;
        public CasinoGameKind Kind;
        public string Name;
        public string Description;
        public int AreaIndex;
    }

    [Serializable]
    public sealed class CasinoAreaDefinition
    {
        public string Id;
        public string Name;
        public string Description;
        public long DefaultTarget;
    }
}
