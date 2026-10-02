using System;
using System.Collections.Generic;

namespace Hotfix.JinxCasino.Rules
{
    public enum CasinoGameKind
    {
        Slots = 0,
        Roulette = 1,
        CoinFlip = 2,
        Blackjack = 3,
        SicBo = 4,
        DragonTiger = 5,
        HighLow = 6,
        LuckyDraw = 7,
        Bingo = 8,
        Plinko = 9,
        CooperativeLevers = 10,
        PushYourLuckDice = 11,
        PassingBag = 12,
        BlindAuction = 13,
        MechanicalRace = 14,
        CooperativeVault = 15,
        ChickenElevator = 16
    }

    public enum CasinoBetError
    {
        None = 0,
        InvalidIdentity,
        WrongRun,
        InvalidGame,
        InvalidStake,
        InvalidChoice,
        InsufficientCoins,
        ConflictingRequest,
        BalanceOverflow
    }

    // 纯传输对象使用公开字段，以便 JsonUtility 和网络适配器共享同一份请求契约。
    [Serializable]
    public sealed class CasinoBetRequest
    {
        /// 发起请求的局标识；网络层必须绑定到当前房间内的局。
        public string RunId;
        /// 经网络层认证的稳定玩家标识，不能直接信任客户端自报值。
        public string PlayerId;
        /// 玩家在本局内唯一的请求标识；重发必须保留原标识及内容。
        public string RequestId;
        public CasinoGameKind Game;
        /// 从团队钱包扣除的正整数筹码数。
        public long Stake;
        /// 水果机为 0；轮盘为 0..36；硬币为 0 或 1。
        public int Choice;
    }

    [Serializable]
    public sealed class CasinoBetReceipt
    {
        /// 结算时复制的请求，不引用调用者的可变对象。
        public CasinoBetRequest Request;
        public bool Accepted;
        public CasinoBetError Error;
        /// 轮盘/硬币结果；水果机为三符号编码 first * 36 + second * 6 + third。
        public int Outcome;
        /// 水果机三个 0..5 的符号；其他玩法为空数组。
        public int[] Symbols;
        /// 毛返还，包含赢局退回的本金；余额变化始终是 Payout - Stake。
        public long Payout;
        public long BalanceBefore;
        public long BalanceAfter;
        /// 已接受下注的累计数量；拒绝和重发不增加版本。
        public int Revision;
    }

    [Serializable]
    public sealed class CasinoRunState
    {
        public int SchemaVersion = 1;
        public string RunId;
        public uint Seed;
        public long StartingCoins;
        public long Coins;
        public uint RandomState;
        public int Revision;
        /// 同时保存成功和失败回执，使快照恢复后仍能识别重发请求。
        public List<CasinoBetReceipt> Ledger = new List<CasinoBetReceipt>();
    }
}
