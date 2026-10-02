using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.JinxCasino.Rules
{
    /// 单局小游戏规则与可恢复状态；宿主只按 Cost / Payout 结算，不在这里修改团队钱包。
    public sealed partial class CasinoMiniGameRound
    {
        private const int StopLoss = 1;
        private const int JackpotCoupon = 2;
        private const int Xray = 4;
        private const int LockedReel = 8;
        private const int RerollDice = 16;
        private const int RedrawCard = 32;
        private readonly RoundState state;

        private CasinoMiniGameRound(RoundState state) { this.state = state; }

        /// 是否已经产生不可再操作的最终结果。
        public bool IsComplete => state.Complete;
        /// 原始机台目标是否达成；保险、优惠券或宿主追加返还不会把失败金库变成成功。
        public bool IsObjectiveSuccess => state.Complete && state.ObjectiveSuccess;
        /// 已完成局的毛返还；未完成时为零。
        public long Payout => state.Payout;
        /// 最终投入；盲拍未成交为零，成交只支付出价，其余玩法为初始投入。
        public long Cost => state.Cost;
        /// 当前可读状态、操作提示或最终结果。
        public string Description => state.Description;

        /// <summary>返回下注前的编码、概率和毛返还说明；不会创建局或推进随机源。</summary>
        /// <param name="game">待选择的机台。</param>
        /// <param name="choice">该机台的合法初始选择编码。</param>
        /// <param name="diceBias">0公平；1..6指定面概率37.5%，其余各12.5%。</param>
        /// <param name="cooperationHelp">协作局扳手帮助是否有效，实际杠杆窗口前后各扩100毫秒。</param>
        /// <returns>可直接显示的规则说明。</returns>
        public static string DescribeRules(CasinoGameKind game, int choice = 0, int diceBias = 0, bool cooperationHelp = false)
        {
            if (game < CasinoGameKind.Slots || game > CasinoGameKind.ChickenElevator || !IsChoiceValid(game, choice)) throw new ArgumentOutOfRangeException(nameof(choice));
            if (diceBias < 0 || diceBias > 6) throw new ArgumentOutOfRangeException(nameof(diceBias));
            if (cooperationHelp && game != CasinoGameKind.CooperativeLevers && game != CasinoGameKind.CooperativeVault) throw new ArgumentOutOfRangeException(nameof(cooperationHelp));
            string[] descriptions =
            {
                "老虎机：三个独立六面转轴，两同2倍，三同10倍，三个5号30倍；锁轴把首轴固定5号。",
                "轮盘：0..36单号，命中概率1/37、毛返还36倍；37红、38黑、39单、40双，命中18/37、2倍，零号区域全输。",
                "硬币：0正/1反，各50%，命中2倍；2为连胜模式，猜正反、继续或收手，最多五胜32倍。",
                "21点：52张无放回；A可1或11，庄家17停；赢2倍、和1倍、天然21点2.5倍，爆牌零。",
                "骰宝：0小4..10/1大11..17排除三同，2倍（公平骰105/216）；2任意三同30倍（6/216）；10..15单点1..6，出现1/2/3次概率75/15/1除216，返2/3/4倍；103..118和值3..18概率为[1,3,6,10,15,21,25,27,27,25,21,15,10,6,3,1]/216，返[180,60,30,18,12,8,7,6,6,7,8,12,18,30,60,180]倍；200+(首点-1)*6+(次点-1)，不同点都出现概率30/216返5倍、同点至少两骰16/216返15倍；300..305指定三同1/216返180倍。以上为公平骰概率；重掷取两轮较高返还。",
                "龙虎：两张1..13独立牌，0龙/1虎/2和；概率6/13、6/13、1/13，毛返还2/2/8倍。",
                "比大小：1..13独立牌，猜严格大/小，相等输；每胜翻倍，可收手，五胜32倍。",
                "签筒：choice=风险档*10+初选筒号，档0..2、筒号0..5；档0签值[0,0,1,1,1,2]，档1[0,0,0,1,2,3]，档2[0,0,0,0,0,5]，均匀洗入六筒；选择实际筒号抽取，数值为毛返还倍数。",
                "宾果：1..30选最多五个不同号码，开十球不放回；命中0/1/2/3/4/5返0/0/1/2/5/15倍；开奖后每秒自动开球，可手动继续。",
                "弹珠：选0..6落点，八层每层左右各50%；0..14槽返[20,10,5,3,2,1,0,0,0,1,2,3,5,10,20]倍。",
                "协作杠杆：15秒内同2秒周期完成全部窗口，成功4倍；三次错时或超时零；单人NPC负责1号。",
                "骰子止盈：1爆仓，2..6累计点；可收手，毛返还投入*(1+点数/10)，30点自动4倍；重掷道具救一次爆仓。",
                "烫手福袋：6..8秒随机保险丝，传出或扣住500毫秒；爆炸时在自己手中零，否则2倍+最多3次传递奖励；单人NPC700毫秒传回。",
                "盲拍：预算50%..200%暗奖，NPC底价20%..80%；查看区间线索并竞价，10秒最高价成交，同价NPC赢；仅扣成交出价，未成交零成本。",
                "机械赛跑：下注0..3跑者，100毫秒固定步长，速度4..8；助推三次、闪避500毫秒；障碍退15，第一名4倍。",
                "协作金库：互补三位1..9密码线索，揭示至少两位后可试密；20秒限三错，成功3倍；单人NPC每2秒补线索。",
                "胆小鸡电梯：每层选择0/1梯，独立事故概率20/30/40/50/60%；可逐层收手，层1..5返2/3/5/8/12倍，事故零。"
            };
            return descriptions[(int)game] + (game == CasinoGameKind.SicBo || game == CasinoGameKind.PushYourLuckDice ?
                diceBias == 0 ? " 骰子每面1/6。" : " 偏科" + diceBias + "：该面37.5%，其余各12.5%。" : "") +
                (game == CasinoGameKind.CooperativeLevers ? cooperationHelp ? " 扳手已生效：编号i实际窗口[100+i*250,500+i*250]毫秒。" : " 编号i实际窗口[200+i*250,400+i*250]毫秒。" :
                game == CasinoGameKind.CooperativeVault && cooperationHelp ? " 扳手揭示首条未查看线索，仍必须自己输入真实密码。" : "");
        }

        /// <summary>创建独立局；预备道具在瞬时机台开奖前生效，失败不会产生可提交的局。</summary>
        /// <param name="game">十七款游戏之一，原三款枚举值保持兼容。</param>
        /// <param name="seed">独立固定随机种子；不使用 Unity 随机源。</param>
        /// <param name="stake">锁定的最大投入，1..1000000；盲拍实际成交后再确定 Cost。</param>
        /// <param name="choice">轮盘 0..36 单号、37红/38黑/39单/40双；硬币 0/1 单局、2连胜；签筒 risk*10+筒号，risk0..2、筒号0..5。骰宝 0小/1大/2任意三同，10..15单点，103..118和值，200+两点组合编码，300..305指定三同。</param>
        /// <param name="playerCount">1..6；单人协作机台提供明确 NPC 辅助。</param>
        /// <param name="preparedItems">每种道具至多一次，不兼容或重复道具抛出异常；调用方仅在创建成功后消费。</param>
        /// <param name="diceBias">0 为公平骰，1..6 每骰有 25% 强制该面，其余 75% 均匀；参与存档。</param>
        /// <returns>包含完整随机和操作状态的新局，瞬时机台可能已经完成。</returns>
        public static CasinoMiniGameRound Create(CasinoGameKind game, uint seed, long stake, int choice,
            int playerCount = 1, string[] preparedItems = null, int diceBias = 0)
        {
            if (game < CasinoGameKind.Slots || game > CasinoGameKind.ChickenElevator) throw new ArgumentOutOfRangeException(nameof(game));
            if (stake < 1 || stake > CasinoSession.MaximumStake) throw new ArgumentOutOfRangeException(nameof(stake));
            if (playerCount < 1 || playerCount > 6) throw new ArgumentOutOfRangeException(nameof(playerCount));
            if (!IsChoiceValid(game, choice)) throw new ArgumentOutOfRangeException(nameof(choice));
            if (diceBias < 0 || diceBias > 6) throw new ArgumentOutOfRangeException(nameof(diceBias));
            var round = new CasinoMiniGameRound(new RoundState
            {
                Game = game, Stake = stake, Cost = game == CasinoGameKind.BlindAuction ? 0 : stake,
                Choice = choice, PlayerCount = playerCount, RandomState = new CasinoRandom(seed).State, Phase = -1, DiceBias = diceBias
            });
            if (preparedItems != null)
                foreach (string item in preparedItems)
                    if (!round.ApplyRuleItem(item)) throw new ArgumentException("预备道具不兼容或重复：" + item, nameof(preparedItems));
            round.state.Phase = 0;
            round.Initialize();
            return round;
        }

        /// 返回当前合法操作的独立描述符；完成的局为空数组。
        public CasinoMiniGameActionDescriptor[] GetAvailableActions()
        {
            var actions = new List<CasinoMiniGameActionDescriptor>();
            if (!IsComplete) PopulateAdditionalActions(actions);
            return actions.ToArray();
        }

        /// <summary>执行当前公开操作；非法动作或参数返回 false，且不推进随机、计时或其他状态。</summary>
        /// <param name="action">必须属于当前 GetAvailableActions 的操作。</param>
        /// <param name="value">需要参数时使用描述符闭区间；无参数操作默认零。</param>
        /// <returns>是否被规则接纳，成功不等于一定赢得筹码。</returns>
        public bool TryAct(CasinoMiniGameAction action, int value = 0)
        {
            foreach (var available in GetAvailableActions())
            {
                if (available.Kind != action) continue;
                if (available.RequiresValue && (value < available.Minimum || value > available.Maximum)) return false;
                bool accepted = false;
                ActAdditionalGame(action, value, ref accepted);
                if (accepted) state.OperationCount++;
                return accepted;
            }
            return false;
        }

        /// <summary>推进规则整数时钟；完成局不再变化，赛跑按固定 100 毫秒 tick 处理。</summary>
        /// <param name="milliseconds">非负时间增量；负数抛出异常，零无副作用。</param>
        public void Advance(int milliseconds)
        {
            if (milliseconds < 0) throw new ArgumentOutOfRangeException(nameof(milliseconds));
            if (milliseconds == 0 || IsComplete) return;
            AdvanceAdditionalGame(milliseconds);
        }

        /// 导出含随机、操作、计时和道具效果的完整局快照。
        public string ToSnapshotJson() => JsonUtility.ToJson(state);

        /// <summary>恢复同一局的完整状态；不抽牌、不重新开奖、不执行 Create。</summary>
        /// <param name="json">SchemaVersion 为一的完整局快照；非法结构抛出 ArgumentException。</param>
        /// <returns>与原对象独立且可以继续操作的局。</returns>
        public static CasinoMiniGameRound Restore(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 262144) throw new ArgumentException("小游戏快照为空或过大。", nameof(json));
            RoundState restored;
            try { restored = JsonUtility.FromJson<RoundState>(json); }
            catch (ArgumentException exception) { throw new ArgumentException("小游戏快照不是合法 JSON。", nameof(json), exception); }
            if (restored == null || restored.SchemaVersion != 1 || restored.Game < CasinoGameKind.Slots || restored.Game > CasinoGameKind.ChickenElevator ||
                restored.Stake < 1 || restored.Stake > CasinoSession.MaximumStake || restored.Cost < 0 || restored.Cost > restored.Stake ||
                restored.Payout < 0 || (!restored.Complete && (restored.Payout != 0 || restored.ObjectiveSuccess)) || restored.RandomState == 0 ||
                restored.PlayerCount < 1 || restored.PlayerCount > 6 || restored.Phase < 0 || restored.OperationCount < 0 ||
                !IsChoiceValid(restored.Game, restored.Choice) || restored.DiceBias < 0 || restored.DiceBias > 6 ||
                restored.CooperationHelpUsed && restored.Game != CasinoGameKind.CooperativeVault && restored.Game != CasinoGameKind.CooperativeLevers ||
                restored.ElapsedMilliseconds < 0 || restored.TickRemainder < 0 || restored.TickRemainder >= 100 ||
                restored.Values == null || restored.Secondary == null || restored.Selected == null || restored.Deck == null ||
                restored.Values.Length > 64 || restored.Secondary.Length > 64 || restored.Selected.Length > 64 || restored.Deck.Length > 64 ||
                (restored.Items & ~63) != 0 || restored.Description == null)
                throw new ArgumentException("小游戏快照结构非法。", nameof(json));
            var round = new CasinoMiniGameRound(restored);
            bool valid = false;
            round.ValidateAdditionalSnapshot(ref valid);
            if (!valid) throw new ArgumentException("小游戏快照机台状态非法。", nameof(json));
            return round;
        }

        /// <summary>应用兼容道具；拒绝时不消耗或改变状态，完成局与重复道具始终拒绝。</summary>
        /// <param name="itemId">reroll_dice、redraw_card、lock_reel、xray、stop_loss 或 jackpot_coupon。</param>
        /// <returns>成功时宿主才应消费该道具库存。</returns>
        public bool ApplyRuleItem(string itemId)
        {
            if (IsComplete) return false;
            int item = itemId == "stop_loss" ? StopLoss : itemId == "jackpot_coupon" ? JackpotCoupon :
                itemId == "xray" ? Xray : itemId == "lock_reel" ? LockedReel :
                itemId == "reroll_dice" ? RerollDice : itemId == "redraw_card" ? RedrawCard : 0;
            if (item == 0 || (state.Items & item) != 0) return false;
            if (item == RedrawCard && state.Game == CasinoGameKind.Blackjack && state.Phase >= 0 && state.Cursor >= state.Deck.Length) return false;
            bool compatible = item == StopLoss || item == JackpotCoupon ||
                item == LockedReel && state.Game == CasinoGameKind.Slots && state.Phase == -1 ||
                item == RerollDice && (state.Game == CasinoGameKind.SicBo || state.Game == CasinoGameKind.PushYourLuckDice) ||
                item == RedrawCard && (state.Game == CasinoGameKind.Blackjack || state.Game == CasinoGameKind.HighLow) ||
                item == Xray && (state.Game == CasinoGameKind.Blackjack || state.Game == CasinoGameKind.HighLow ||
                    state.Game == CasinoGameKind.LuckyDraw || state.Game == CasinoGameKind.BlindAuction || state.Game == CasinoGameKind.CooperativeVault);
            if (!compatible) return false;
            state.Items |= item;
            if (state.Phase >= 0) ApplyAdditionalItem(item);
            state.OperationCount++;
            return true;
        }

        private void Initialize()
        {
            if (state.Game == CasinoGameKind.CoinFlip && state.Choice != 2)
            {
                int result = Next(2);
                state.Values = new[] { result };
                Finish(result == state.Choice ? state.Stake * 2 : 0, "硬币" + (result == 0 ? "正面" : "反面"));
            }
            else if (state.Game == CasinoGameKind.Roulette)
            {
                int result = Next(37);
                state.Values = new[] { result };
                bool red = Array.IndexOf(new[] { 1,3,5,7,9,12,14,16,18,19,21,23,25,27,30,32,34,36 }, result) >= 0;
                bool won = state.Choice <= 36 ? result == state.Choice : result != 0 &&
                    (state.Choice == 37 ? red : state.Choice == 38 ? !red : state.Choice == 39 ? result % 2 == 1 : result % 2 == 0);
                Finish(won ? state.Stake * (state.Choice <= 36 ? 36 : 2) : 0, "轮盘停在 " + result + "（单号毛返还36倍；红黑单双2倍，零号区域全输）");
            }
            else if (state.Game == CasinoGameKind.Slots)
            {
                state.Values = new[] { Next(6), Next(6), Next(6) };
                if (HasItem(LockedReel)) state.Values[0] = 5;
                int[] reels = state.Values;
                int multiplier = reels[0] == reels[1] && reels[1] == reels[2] ? reels[0] == 5 ? 30 : 10 :
                    reels[0] == reels[1] || reels[1] == reels[2] || reels[0] == reels[2] ? 2 : 0;
                Finish(state.Stake * multiplier, "转轴 " + string.Join(" / ", reels));
            }
            else
            {
                bool initialized = false;
                InitializeAdditionalGame(ref initialized);
                if (!initialized) throw new NotSupportedException("该机台规则尚未接入：" + state.Game);
            }
        }

        private int Next(int exclusiveMaximum)
        {
            var random = new CasinoRandom(state.RandomState);
            int value = random.NextInt(exclusiveMaximum);
            state.RandomState = random.State;
            return value;
        }

        private bool HasItem(int item) => (state.Items & item) != 0;

        private static bool IsChoiceValid(CasinoGameKind game, int choice)
        {
            if (game == CasinoGameKind.SicBo) return choice >= 0 && choice <= 2 || choice >= 10 && choice <= 15 ||
                choice >= 103 && choice <= 118 || choice >= 200 && choice <= 235 || choice >= 300 && choice <= 305;
            if (game == CasinoGameKind.LuckyDraw) return choice >= 0 && choice / 10 <= 2 && choice % 10 <= 5;
            int maximum = game == CasinoGameKind.Roulette ? 40 : game == CasinoGameKind.CoinFlip || game == CasinoGameKind.DragonTiger ? 2 :
                game == CasinoGameKind.Plinko ? 6 : game == CasinoGameKind.MechanicalRace ? 3 : 0;
            return choice >= 0 && choice <= maximum;
        }

        private int RollDie() => state.DiceBias != 0 && Next(4) == 0 ? state.DiceBias : Next(6) + 1;
        private string DiceOdds() => state.DiceBias == 0 ? "每面概率1/6" : "偏科" + state.DiceBias + "：该面37.5%，其余各12.5%";

        private void Finish(long payout, string result)
        {
            if (IsComplete) return;
            if (state.Game != CasinoGameKind.CooperativeVault) state.ObjectiveSuccess = payout > state.Cost;
            if (HasItem(StopLoss)) payout = Math.Max(payout, state.Cost / 2);
            if (HasItem(JackpotCoupon) && payout > 0) payout += state.Stake;
            state.Payout = payout;
            state.Complete = true;
            state.Description = result + "；投入 " + state.Cost + "，返还 " + payout + " 筹码。";
        }

        private static CasinoMiniGameActionDescriptor Action(CasinoMiniGameAction kind, string label,
            bool requiresValue = false, int minimum = 0, int maximum = 0) => new CasinoMiniGameActionDescriptor
            { Kind = kind, Label = label, RequiresValue = requiresValue, Minimum = minimum, Maximum = maximum };

        partial void InitializeAdditionalGame(ref bool initialized);
        partial void PopulateAdditionalActions(List<CasinoMiniGameActionDescriptor> actions);
        partial void ActAdditionalGame(CasinoMiniGameAction action, int value, ref bool accepted);
        partial void AdvanceAdditionalGame(int milliseconds);
        partial void ApplyAdditionalItem(int item);
        partial void ValidateAdditionalSnapshot(ref bool valid);

        [Serializable]
        private sealed class RoundState
        {
            public int SchemaVersion = 1;
            public CasinoGameKind Game;
            public long Stake;
            public long Cost;
            public long Payout;
            public int Choice;
            public int PlayerCount;
            public uint RandomState;
            public int Items;
            public int DiceBias;
            public bool Complete;
            public bool ObjectiveSuccess;
            public bool CooperationHelpUsed;
            public string Description = string.Empty;
            public int OperationCount;
            // Phase 为0准备/1运行或已看线索；计数器由对应机台持有，完整进入同一个快照。
            public int Phase;
            public int Counter;
            public int Score;
            public int Level;
            public int Limit;
            public int Cursor;
            public int Target;
            public int RemainingMilliseconds;
            public long ElapsedMilliseconds;
            public int TickRemainder;
            public int AuxiliaryClock;
            // Values为牌/骰/槽/位置，Secondary为庄家牌或障碍，Selected为号码/杠杆/线索，Deck为固定洗牌序列。
            public int[] Values = Array.Empty<int>();
            public int[] Secondary = Array.Empty<int>();
            public int[] Selected = Array.Empty<int>();
            public int[] Deck = Array.Empty<int>();
        }
    }
}
