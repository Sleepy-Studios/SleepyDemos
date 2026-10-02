using System;
using System.Collections.Generic;
using Hotfix.JinxCasino.Rules;

namespace Hotfix.JinxCasino.Adapters.UI
{
    /// 机台界面的选项翻译；编号仅作为规则协议值，不进入玩家看到的标签。
    internal static class CasinoMachineUiOptions
    {
        internal sealed class Option
        {
            internal readonly string Label;
            internal readonly int Value;
            internal Option(string label, int value) { Label = label; Value = value; }
        }

        internal sealed class Group
        {
            internal readonly string Label;
            internal readonly Option[] Options;
            internal Group(string label, params Option[] options) { Label = label; Options = options; }
        }

        internal static Group[] Choices(CasinoGameKind game)
        {
            switch (game)
            {
                case CasinoGameKind.CoinFlip:
                    return new[] { new Group("玩法", new Option("猜正面 · 单次", 0), new Option("猜反面 · 单次", 1), new Option("连胜挑战 · 可随时收手", 2)) };
                case CasinoGameKind.Roulette:
                    return new[] { new Group("押单号", Numbers(0, 36, value => "数字 " + value)),
                        new Group("押颜色或单双", new Option("红色", 37), new Option("黑色", 38), new Option("单数", 39), new Option("双数", 40)) };
                case CasinoGameKind.DragonTiger:
                    return new[] { new Group("押哪一方", new Option("龙胜", 0), new Option("虎胜", 1), new Option("打和", 2)) };
                case CasinoGameKind.MechanicalRace:
                    return new[] { new Group("支持跑者", Numbers(0, 3, value => "跑者 " + (value + 1))) };
                case CasinoGameKind.LuckyDraw:
                    return new[] { new Group("稳妥签 · 四签有返还", Numbers(0, 5, value => "第 " + (value + 1) + " 签筒")),
                        new Group("普通签 · 三签有返还", Numbers(10, 15, value => "第 " + (value - 9) + " 签筒")),
                        new Group("冒险签 · 一签返五倍", Numbers(20, 25, value => "第 " + (value - 19) + " 签筒")) };
                case CasinoGameKind.SicBo:
                    var pairs = new List<Option>();
                    for (int first = 1; first <= 6; first++)
                        for (int second = first; second <= 6; second++)
                            pairs.Add(new Option(first == second ? "至少两颗 " + first + " 点" : "同时出现 " + first + " 点和 " + second + " 点", 200 + (first - 1) * 6 + second - 1));
                    return new[] { new Group("大小 / 三同", new Option("小 · 总点数四至十，三同除外", 0), new Option("大 · 总点数十一至十七，三同除外", 1), new Option("任意三颗相同", 2)),
                        new Group("押单点", Numbers(10, 15, value => "出现 " + (value - 9) + " 点")),
                        new Group("押总点数", Numbers(103, 118, value => "总点数 " + (value - 100))), new Group("押两骰组合", pairs.ToArray()),
                        new Group("押指定三同", Numbers(300, 305, value => "三颗都是 " + (value - 299) + " 点")) };
                default: return Array.Empty<Group>();
            }
        }

        internal static Option[] ActionOptions(CasinoMiniGameActionDescriptor action, int playerCount)
        {
            switch (action.Kind)
            {
                case CasinoMiniGameAction.SelectNumber: return Numbers(action.Minimum, action.Maximum, value => "宾果号码 " + value);
                case CasinoMiniGameAction.PickPrize: return Numbers(action.Minimum, action.Maximum, value => "打开第 " + (value + 1) + " 签筒");
                case CasinoMiniGameAction.DropBall: return Numbers(action.Minimum, action.Maximum, value => value == 3 ? "正中央" : value < 3 ? "左侧第 " + (value + 1) + " 落点" : "右侧第 " + (value - 3) + " 落点");
                case CasinoMiniGameAction.PullLever: return playerCount == 1 ? new[] { new Option("你的杠杆 · 等待亮起时拉下", 0) } : Numbers(action.Minimum, action.Maximum, value => "队员 " + (value + 1) + " 的杠杆");
                case CasinoMiniGameAction.InspectClue: return Numbers(action.Minimum, action.Maximum, value => new[] { "百位密码线索", "十位密码线索", "个位密码线索" }[value]);
                case CasinoMiniGameAction.Climb: return new[] { new Option("左侧电梯", 0), new Option("右侧电梯", 1) };
                default: return Array.Empty<Option>();
            }
        }

        internal static string Describe(CasinoGameKind game, int choice, int diceBias, bool cooperationHelp = false)
        {
            // 复用规则的参数校验，随后只显示玩家语义；概率不会创建局或消耗随机源。
            CasinoMiniGameRound.DescribeRules(game, choice, diceBias, cooperationHelp);
            switch (game)
            {
                case CasinoGameKind.Slots: return "三个独立六符号转轴。恰有两同概率 90/216，返投入的 2 倍；普通三同概率 5/216，返 10 倍；三个最高奖符号概率 1/216，返 30 倍。\n锁轮齿将第一个转轴固定为最高奖符号，中奖分布随之改变。";
                case CasinoGameKind.Roulette: return choice <= 36 ? "押数字 " + choice + "：命中概率 1/37，毛返还 36 倍。" : "押" + new[] { "红色", "黑色", "单数", "双数" }[choice - 37] + "：命中概率 18/37，毛返还 2 倍。数字零不属于这些区域。";
                case CasinoGameKind.CoinFlip: return choice == 2 ? "连胜挑战：每次正反各 50%。每胜毛返还翻倍，五连胜返 32 倍。第一次猜对后可收手，猜错失去本局投入。" : "猜" + (choice == 0 ? "正面" : "反面") + "：命中概率 50%，毛返还 2 倍。";
                case CasinoGameKind.Blackjack: return "一副 52 张牌无放回；A 可算 1 或 11，庄家达到 17 点停牌。胜返 2 倍，和返 1 倍，天然二十一点返 2.5 倍；爆牌全失。";
                case CasinoGameKind.SicBo: return DescribeDice(choice, diceBias);
                case CasinoGameKind.DragonTiger: return "龙、虎各抽一张独立牌，比点数。龙胜和虎胜概率各 6/13，毛返还 2 倍；打和概率 1/13，毛返还 8 倍。";
                case CasinoGameKind.HighLow: return "每次抽一张一至十三点的独立牌，猜严格更大或更小；相等也算失败。每胜毛返还翻倍，可收手，五连胜返 32 倍。胜率随当前牌点数变化。";
                case CasinoGameKind.LuckyDraw:
                    return new[] { "稳妥签：六签毛返还分别为零、零、一、一、一、二倍。", "普通签：六签毛返还分别为零、零、零、一、二、三倍。", "冒险签：六签中五签为零，只有一签返五倍。" }[choice / 10] + "\n六签均匀洗入签筒；确认后可选择实际打开的签筒。";
                case CasinoGameKind.Bingo: return "从一至三十选最多五个不同号码，再开十球，无放回。命中零至五个，毛返还分别为零、零、一、二、五、十五倍。开始开奖后每秒自动开一球，也可手动继续。";
                case CasinoGameKind.Plinko: return "确认投入后选落点。弹珠经过八层，每层向左或右各 50%。十五个终点从左至右毛返还为二十、十、五、三、二、一、零、零、零、一、二、三、五、十、二十倍。";
                case CasinoGameKind.CooperativeLevers: return "十五秒内在同一个两秒周期完成所有杠杆窗口，成功毛返还 4 倍。错时三次或超时失败。\n单人只操作自己的杠杆，助手会拉另一根；" + (cooperationHelp ? "扳手将你的实际窗口放宽至每周期第100至500毫秒。" : "你的实际窗口为每周期第200至400毫秒。") + "仍须自己在窗口拉下。";
                case CasinoGameKind.PushYourLuckDice: return "掷到一点爆仓，其余点数累积。可收手，毛返还为投入 ×（一加累计点数除以十）；三十点自动返 4 倍。重掷骰可救一次爆仓。\n" + DiceFaces(diceBias);
                case CasinoGameKind.PassingBag: return "保险丝随机持续六至八秒，传给助手或扣住半秒。爆炸时在你手中则失败；在别人手中返 2 倍，另加最多三次传递奖励。助手收到后七百毫秒自动传回。";
                case CasinoGameKind.BlindAuction: return "投入是最高竞价预算。暗奖价值为预算的 50% 至 200%，对手底价为 20% 至 80%。十秒截止，最高出价成交，同价对手优先。\n只有成交才扣实际出价，未成交零成本。可以先查看价值区间线索。";
                case CasinoGameKind.MechanicalRace: return "支持跑者 " + (choice + 1) + "，选中冠军返 4 倍。四名跑者使用相同速度分布，每十分之一秒前进四至八格。障碍退十五格；你有三次助推，闪避保护半秒。";
                case CasinoGameKind.CooperativeVault: return "查看百位、十位、个位线索，组合三位密码。每位为一至九；至少揭示两位才可试密。限二十秒，三次输错失败；真实开锁毛返还 3 倍。单人助手每两秒补一条线索。" + (cooperationHelp ? "双人扳手已揭示一条尚未查看线索，仍必须输入真实密码。" : "");
                case CasinoGameKind.ChickenElevator: return "每层选左或右电梯，可逐层收手。第一至五层每梯独立事故概率为 20%、30%、40%、50%、60%；通过后毛返还分别为 2、3、5、8、12 倍。事故全失。";
                default: throw new ArgumentOutOfRangeException(nameof(game));
            }
        }

        private static string DiceFaces(int bias) => bias == 0 ? "每个点数的概率均为 1/6。" : "当前偏科：" + bias + " 点概率 37.5%，其他各点概率 12.5%。赔率维持机台标价。";

        private static string DescribeDice(int choice, int bias)
        {
            int[] sums = { 180, 60, 30, 18, 12, 8, 7, 6, 6, 7, 8, 12, 18, 30, 60, 180 };
            string reward = choice <= 1 ? "毛返还 2 倍" : choice == 2 ? "毛返还 30 倍" : choice >= 300 ? "毛返还 180 倍" : choice >= 200 ?
                ((choice - 200) / 6 == (choice - 200) % 6 ? "毛返还 15 倍" : "毛返还 5 倍") : choice >= 103 ? "毛返还 " + sums[choice - 103] + " 倍" : "命中一、两、三颗时，毛返还分别为 2、3、4 倍";
            double probability = 0;
            for (int first = 1; first <= 6; first++)
                for (int second = 1; second <= 6; second++)
                    for (int third = 1; third <= 6; third++)
                    {
                        int sum = first + second + third;
                        bool triple = first == second && second == third;
                        bool win = choice == 0 ? !triple && sum >= 4 && sum <= 10 : choice == 1 ? !triple && sum >= 11 && sum <= 17 : choice == 2 ? triple :
                            choice >= 300 ? triple && first == choice - 299 : choice >= 200 ? PairMatches(choice, first, second, third) :
                            choice >= 103 ? sum == choice - 100 : first == choice - 9 || second == choice - 9 || third == choice - 9;
                        if (win) probability += FaceOdds(first, bias) * FaceOdds(second, bias) * FaceOdds(third, bias);
                    }
            return "当前下注命中概率 " + (probability * 100).ToString("0.##") + "%；" + reward + "。\n" + DiceFaces(bias) + " 重掷骰取两轮较高返还。";
        }

        private static double FaceOdds(int face, int bias) => bias == 0 ? 1.0 / 6 : face == bias ? 0.375 : 0.125;

        private static bool PairMatches(int choice, int first, int second, int third)
        {
            int left = (choice - 200) / 6 + 1, right = (choice - 200) % 6 + 1;
            int leftCount = (first == left ? 1 : 0) + (second == left ? 1 : 0) + (third == left ? 1 : 0);
            return left == right ? leftCount >= 2 : leftCount > 0 && (first == right || second == right || third == right);
        }

        private static Option[] Numbers(int minimum, int maximum, Func<int, string> label)
        {
            var result = new Option[maximum - minimum + 1];
            for (int index = 0; index < result.Length; index++) result[index] = new Option(label(minimum + index), minimum + index);
            return result;
        }
    }
}
