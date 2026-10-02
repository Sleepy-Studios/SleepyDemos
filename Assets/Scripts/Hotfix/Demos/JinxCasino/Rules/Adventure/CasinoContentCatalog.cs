using System;
using UnityEngine;

namespace Hotfix.JinxCasino.Rules
{
    /// 固定内容定义的深拷贝入口；修改返回值不会污染下一局。
    public static class CasinoContentCatalog
    {
        private static readonly CasinoItemDefinition[] items =
        {
            Item("bubble_gun", "泡泡枪", "目标被泡泡包住2秒；同目标5秒内免疫再次整蛊。", 50, CasinoItemBehavior.SceneEffect, 2000, "Bubble", true),
            Item("banana_peel", "香蕉皮", "目标滑行失衡1.5秒；同目标5秒保护。", 50, CasinoItemBehavior.SceneEffect, 1500, "Banana", true),
            Item("spring_glove", "弹簧拳套", "目标被轻推1秒，宿主限制安全位移；同目标5秒保护。", 60, CasinoItemBehavior.SceneEffect, 1000, "SpringPunch", true),
            Item("fake_jackpot", "假大奖广播", "目标附近播放假大奖与纸片庆祝2秒，明显标明整蛊，不改资金。", 40, CasinoItemBehavior.SceneEffect, 2000, "FakeJackpot", true),
            Item("ink_balloon", "墨汁气球", "目标视角周边泼墨2秒，保留中心操作区；同目标5秒保护。", 60, CasinoItemBehavior.SceneEffect, 2000, "Ink", true),
            Item("magnet_hat", "磁力帽", "目标被拉向安全聚会点1秒，宿主限制位移；同目标5秒保护。", 70, CasinoItemBehavior.SceneEffect, 1000, "Magnet", true),
            Item("remote_horn", "遥控喇叭", "目标附近播放1秒卡通喇叭，音量受宿主设置约束；同目标5秒保护。", 40, CasinoItemBehavior.SceneEffect, 1000, "Horn", true),
            Item("disguise_spray", "换装喷雾", "目标临时纸片换装3秒，保留身份标记；同目标5秒保护。", 50, CasinoItemBehavior.SceneEffect, 3000, "Disguise", true),
            Item("relay_battery", "接力电池", "接力送电任务增加一次充能，任务未出现或已结束则拒绝使用。", 80, CasinoItemBehavior.RelayCharge, 1),
            Item("rescue_whistle", "救场哨", "失败阶段重新开放30秒，本区额度保留；活动局不能撤销。", 120, CasinoItemBehavior.Rescue, 30000),
            Item("duo_wrench", "双人扳手", "为下一协作拉杆或金库提供一次NPC提示；不直接判定挑战胜利。", 100, CasinoItemBehavior.CooperationHelp, 1),
            Item("extra_time", "加时券", "当前标准/无尽区增加30秒，练习模式不能消耗。", 100, CasinoItemBehavior.AddTime, 30000),
            Item("teleport_bell", "传送铃", "召集目标至该区安全集合点，宿主验证安全落点；不撤销下注。", 80, CasinoItemBehavior.SceneEffect, 2000, "Teleport"),
            Item("shared_shield", "共享护盾", "抵消下一次负面环境事件；不抵消正常下注损失。", 90, CasinoItemBehavior.ShieldEvent, 1),
            Item("reroll_dice", "重掷骰", "骰类局内重掷一次；不兼容局拒绝且不消耗。", 80, CasinoItemBehavior.Rule),
            Item("redraw_card", "重抽牌", "牌类局内重抽一次；已结束不能使用。", 80, CasinoItemBehavior.Rule),
            Item("lock_reel", "锁轮齿", "下注前预备，水果机锁定一个转轴。", 100, CasinoItemBehavior.Rule),
            Item("xray", "透视镜", "预备或局内揭示当前游戏的线索与隐藏信息。", 70, CasinoItemBehavior.Rule),
            Item("stop_loss", "止损券", "预备下一局，按小游戏规则限制亏损。", 120, CasinoItemBehavior.Rule),
            Item("jackpot_coupon", "彩金券", "预备下一局，提高小游戏中奖毛返还。", 150, CasinoItemBehavior.Rule),
            Item("map_radar", "地图雷达", "显示本区机台与活动任务方向15秒；不改变开奖结果。", 60, CasinoItemBehavior.RevealMap, 15000, "MapRadar"),
            Item("shortcut_key", "捷径钥匙", "打开本区安全捷径至阶段结束，宿主据区域设置路线。", 100, CasinoItemBehavior.Shortcut, 0, "OpenShortcut"),
            Item("event_remote", "事件遥控器", "无进行中任务或待选择事件时，立刻抽取新事件。", 100, CasinoItemBehavior.TriggerEvent),
            Item("mystery_coupon", "神秘兑换券", "按领域固定随机兑换一件非兑换券道具，库存原子变化。", 80, CasinoItemBehavior.MysteryExchange)
        };

        private static readonly CasinoEventDefinition[] events =
        {
            Event("low_stakes", "低注狂欢", "未来45秒最高投入100，下一局额外毛返还20%；投入前显示。", CasinoEventBehavior.StakeCap, 100),
            Event("jackpot_hour", "彩金时段", "未来45秒下一下注毛返还增加25%，开奖概率不变。", CasinoEventBehavior.PayoutBonus, 25),
            Event("biased_dice", "骰子偏科", "未来45秒点数6概率37.5%，其他面各12.5%；每骰25%强制6，其余均匀，下注前显示。", CasinoEventBehavior.DiceBias, 6),
            Event("machine_rotation", "机台轮换", "当前区域的轮换展位改变至本区结束，已提交局保持。", CasinoEventBehavior.RotateGames, 1),
            Event("crazy_multiplier", "疯狂倍率", "未来45秒毛返还额外增加50%，概率不变。", CasinoEventBehavior.Multiplier, 50),
            Event("power_repair", "停电抢修", "45秒内完成3次维修交互，恢复灯光并获得80筹码。", CasinoEventBehavior.RepairMission, 80, "PowerOutage"),
            Event("bubble_leak", "泡泡泄漏", "区域泡泡2秒，不撤销任何已提交操作。", CasinoEventBehavior.SceneEffect, 2000, "BubbleStorm"),
            Event("portal_fault", "传送门故障", "全员安全落点传送表现1秒，保留本局。", CasinoEventBehavior.SceneEffect, 1000, "PortalFault"),
            Event("moving_tables", "移动赌桌", "空闲赌桌沿安全路径移动3秒，操作中的桌不移动。", CasinoEventBehavior.SceneEffect, 3000, "MovingTables"),
            Event("slippery_floor", "地板滑行", "地板打滑2秒，宿主限速与防坠落。", CasinoEventBehavior.SceneEffect, 2000, "SlipperyFloor"),
            Event("mystery_merchant", "神秘商人", "选择购买随机道具或拒绝；购买先显示固定价格80。", CasinoEventBehavior.TeamChoice, 80),
            Event("group_insurance", "集体保险", "选择花50筹码获得共享护盾，或保留筹码。", CasinoEventBehavior.TeamChoice, 50),
            Event("risk_contract", "风险合同", "选择将本区额度提高200换取下一局额外50%返还，或拒绝。", CasinoEventBehavior.TeamChoice, 200),
            Event("item_exchange", "道具交换", "选择一件已有道具换取随机不同道具，或拒绝。", CasinoEventBehavior.TeamChoice, 0),
            Event("early_exit", "提前撤离", "选择带剩余筹码撤离，或继续挑战；不能撤销活动局。", CasinoEventBehavior.TeamChoice, 0),
            Event("chip_rain", "筹码雨", "45秒内收集5枚唯一任务筹码，全部收集后获得150。", CasinoEventBehavior.Mission, 150, "ChipRain"),
            Event("mascot_chase", "追逐吉祥物", "45秒内在3个检查点追上吉祥物，完成获得180。", CasinoEventBehavior.Mission, 180, "Mascot"),
            Event("gold_delivery", "搬运金箱", "45秒内先拿起金箱再送至交付点，完成获得200。", CasinoEventBehavior.Mission, 200, "GoldCrate"),
            Event("power_relay", "接力送电", "45秒内完成3次接力充能，完成获得160。", CasinoEventBehavior.Mission, 160, "RelayStation"),
            Event("sync_buttons", "全员同步按钮", "45秒内每位队员各按一次；单人只需自己，完成获得人数缩放奖励。", CasinoEventBehavior.Mission, 120, "SyncButtons")
        };

        private static readonly CasinoGameDefinition[] games =
        {
            Game(CasinoGameKind.Slots, "霓虹水果机", "一次确认，三个符号决定毛返还。", 0),
            Game(CasinoGameKind.Roulette, "迷你轮盘", "押0至36的单号。", 0),
            Game(CasinoGameKind.CoinFlip, "倒霉硬币", "选择正反面。", 0),
            Game(CasinoGameKind.Blackjack, "二十一点", "要牌或停牌，爆牌失败。", 0),
            Game(CasinoGameKind.SicBo, "三骰桌", "根据三骰结果结算。", 1),
            Game(CasinoGameKind.DragonTiger, "龙虎牌", "两张牌比较大小。", 1),
            Game(CasinoGameKind.HighLow, "高低猜猜", "连续选择更高或更低，也可止盈。", 0),
            Game(CasinoGameKind.LuckyDraw, "抽签柜", "选择奖签，领取固定概率奖项。", 0),
            Game(CasinoGameKind.Bingo, "宾果钟", "标号与开奖组合决定返还。", 1),
            Game(CasinoGameKind.Plinko, "弹珠塔", "选择落点后等待弹珠到底。", 1),
            Game(CasinoGameKind.CooperativeLevers, "合拍拉杆", "按照节奏完成多人或单人NPC协作。", 1),
            Game(CasinoGameKind.PushYourLuckDice, "再摇一次", "累积奖励或及时止盈。", 2),
            Game(CasinoGameKind.PassingBag, "烫手福袋", "选择传递或持有的风险。", 2),
            Game(CasinoGameKind.BlindAuction, "盲拍纸箱", "最高投入锁定，只有实际成交价计成本。", 2),
            Game(CasinoGameKind.MechanicalRace, "发条竞速", "加速与闪避影响比赛。", 2),
            Game(CasinoGameKind.CooperativeVault, "合伙金库", "分工检查三条线索，输入密码；单人可检查全部。", 3),
            Game(CasinoGameKind.ChickenElevator, "胆小鬼电梯", "逐层攀升或取奖离开。", 3)
        };

        private static readonly CasinoAreaDefinition[] areas =
        {
            new CasinoAreaDefinition { Id = "lobby", Name = "街角幸运厅", Description = "短局与基础风险判断。", DefaultTarget = 1200 },
            new CasinoAreaDefinition { Id = "arcade", Name = "霓虹夜市", Description = "弹珠、骰桌与合拍小游戏。", DefaultTarget = 2000 },
            new CasinoAreaDefinition { Id = "backroom", Name = "机械奇术馆", Description = "止盈、盲拍与机械赛跑。", DefaultTarget = 3500 },
            new CasinoAreaDefinition { Id = "penthouse", Name = "空中金库", Description = "协作金库与最终抉择。", DefaultTarget = 5000 }
        };

        public static CasinoItemDefinition[] Items => Copy(items);
        public static CasinoEventDefinition[] Events => Copy(events);
        public static CasinoGameDefinition[] Games => Copy(games);
        public static CasinoAreaDefinition[] Areas => Copy(areas);

        /// <summary>按稳定道具 ID 获取独立定义副本。</summary>
        /// <param name="id">目录中的固定 ID；不存在时返回 null。</param>
        public static CasinoItemDefinition FindItem(string id) => Copy(Array.Find(items, item => item.Id == id));

        /// <summary>按稳定事件 ID 获取独立定义副本。</summary>
        /// <param name="id">目录中的固定 ID；不存在时返回 null。</param>
        public static CasinoEventDefinition FindEvent(string id) => Copy(Array.Find(events, entry => entry.Id == id));

        private static CasinoItemDefinition Item(string id, string name, string description, long price, CasinoItemBehavior behavior, long value = 0, string effect = null, bool prank = false)
            => new CasinoItemDefinition { Id = id, Name = name, Description = description, Price = price, Behavior = behavior, Value = value, EffectKind = effect, IsPrank = prank };
        private static CasinoEventDefinition Event(string id, string name, string description, CasinoEventBehavior behavior, long value, string effect = null)
            => new CasinoEventDefinition { Id = id, Name = name, Description = description, Weight = 1, Behavior = behavior, Value = value, EffectKind = effect };
        private static CasinoGameDefinition Game(CasinoGameKind kind, string name, string description, int area)
            => new CasinoGameDefinition { Id = kind.ToString(), Kind = kind, Name = name, Description = description, AreaIndex = area };
        private static T Copy<T>(T value) where T : class => value == null ? null : JsonUtility.FromJson<T>(JsonUtility.ToJson(value));
        private static T[] Copy<T>(T[] values) where T : class => Array.ConvertAll(values, value => Copy(value));
    }
}
