using System;

namespace Hotfix.JinxCasino.Rules
{
    /// 各机台公开操作；固定数值用于存档与宿主命令，不依赖按钮名称。
    public enum CasinoMiniGameAction
    {
        Play = 0, Hit = 1, Stand = 2, GuessHigher = 3, GuessLower = 4, CashOut = 5,
        SelectNumber = 6, DrawNumber = 7, DropBall = 8, PullLever = 9, RollDice = 10,
        PassBag = 11, HoldBag = 12, RevealClue = 13, Bid = 14, Boost = 15, Dodge = 16,
        InspectClue = 17, EnterCode = 18, Climb = 19, PickPrize = 20, GuessHeads = 21, GuessTails = 22
    }

    /// 可直接呈现的操作，不持有 UI 对象；宿主仍须以 TryAct 的结果为准。
    [Serializable]
    public sealed class CasinoMiniGameActionDescriptor
    {
        /// 操作类型。
        public CasinoMiniGameAction Kind;
        /// 玩家可读的操作名称。
        public string Label;
        /// 是否需要输入整数参数。
        public bool RequiresValue;
        /// 有参数操作的最小值，包含边界。
        public int Minimum;
        /// 有参数操作的最大值，包含边界。
        public int Maximum;
    }
}
