using System;
using UnityEngine;

namespace Hotfix.JinxCasino.Rules
{
    /// 5配色、6帽子、8表情与真实行为称号；奖励只改变永久档案，不修改任何局的钱包或概率。
    public static class CasinoProfileCatalog
    {
        private static readonly CasinoCosmeticDefinition[] definitions =
        {
            Define("color_blue", "霓虹蓝", CasinoCosmeticKind.Color, CasinoProfileMetric.Always, 0, "默认配色", "36A8FF"),
            Define("color_pink", "派对粉", CasinoCosmeticKind.Color, CasinoProfileMetric.FinishedRuns, 1, "结束1次正式或无尽旅程", "FF6BA9"),
            Define("color_mint", "助手绿", CasinoCosmeticKind.Color, CasinoProfileMetric.Tasks, 3, "完成3个场地任务", "56DFB0"),
            Define("color_gold", "离场金", CasinoCosmeticKind.Color, CasinoProfileMetric.DignifiedExits, 1, "体面离场1次", "F5CA57"),
            Define("color_violet", "城主紫", CasinoCosmeticKind.Color, CasinoProfileMetric.Takeovers, 1, "完成额外金库挑战并接管1次", "A57AFF"),
            Define("hat_none", "轻装上阵", CasinoCosmeticKind.Hat, CasinoProfileMetric.Always, 0, "默认不戴帽子"),
            Define("hat_party", "派对纸帽", CasinoCosmeticKind.Hat, CasinoProfileMetric.FinishedRuns, 1, "结束1次正式或无尽旅程"),
            Define("hat_banana", "香蕉帽", CasinoCosmeticKind.Hat, CasinoProfileMetric.Pranks, 3, "成功整蛊3次"),
            Define("hat_mechanic", "维修帽", CasinoCosmeticKind.Hat, CasinoProfileMetric.Tasks, 5, "完成5个场地任务"),
            Define("hat_crown", "狂欢王冠", CasinoCosmeticKind.Hat, CasinoProfileMetric.Takeovers, 1, "接管俱乐部1次"),
            Define("hat_lucky", "幸运礼帽", CasinoCosmeticKind.Hat, CasinoProfileMetric.Bets, 25, "确认投入25局，结果不影响解锁"),
            Define("emote_wave", "挥手", CasinoCosmeticKind.Emote, CasinoProfileMetric.Always, 0, "默认表情"),
            Define("emote_clap", "鼓掌", CasinoCosmeticKind.Emote, CasinoProfileMetric.FinishedRuns, 1, "结束1次正式或无尽旅程"),
            Define("emote_shrug", "摊手", CasinoCosmeticKind.Emote, CasinoProfileMetric.Withdrawals, 1, "狼狈撤离1次"),
            Define("emote_dance", "小步舞", CasinoCosmeticKind.Emote, CasinoProfileMetric.Pranks, 5, "成功整蛊5次"),
            Define("emote_salute", "致敬助手", CasinoCosmeticKind.Emote, CasinoProfileMetric.Tasks, 5, "完成5个场地任务"),
            Define("emote_bow", "谢幕", CasinoCosmeticKind.Emote, CasinoProfileMetric.DignifiedExits, 1, "体面离场1次"),
            Define("emote_crown", "举冠", CasinoCosmeticKind.Emote, CasinoProfileMetric.Takeovers, 1, "接管1次"),
            Define("emote_fireworks", "纸片烟花", CasinoCosmeticKind.Emote, CasinoProfileMetric.AllDiscoveries, 1, "发现17机台、24道具、20事件和3结局"),
            Define("title_newcomer", "初来乍到", CasinoCosmeticKind.Title, CasinoProfileMetric.Always, 0, "默认称号"),
            Define("title_story", "有故事的人", CasinoCosmeticKind.Title, CasinoProfileMetric.FinishedRuns, 1, "结束1次正式或无尽旅程"),
            Define("title_prankster", "整蛊搭子", CasinoCosmeticKind.Title, CasinoProfileMetric.Pranks, 3, "成功整蛊3次"),
            Define("title_rescuer", "救场高手", CasinoCosmeticKind.Title, CasinoProfileMetric.Rescues, 1, "实际成功使用救场哨1次"),
            Define("title_helper", "可靠助手", CasinoCosmeticKind.Title, CasinoProfileMetric.Tasks, 5, "完成5个场地任务"),
            Define("title_survivor", "下次再来", CasinoCosmeticKind.Title, CasinoProfileMetric.Withdrawals, 1, "狼狈撤离1次"),
            Define("title_dignified", "见好就收", CasinoCosmeticKind.Title, CasinoProfileMetric.DignifiedExits, 1, "体面离场1次"),
            Define("title_owner", "新任城主", CasinoCosmeticKind.Title, CasinoProfileMetric.Takeovers, 1, "完成金库后实际接管1次"),
            Define("title_collector", "荒诞收藏家", CasinoCosmeticKind.Title, CasinoProfileMetric.AllDiscoveries, 1, "发现17机台、24道具、20事件和3结局"),
            Define("title_saver", "口袋鼓鼓", CasinoCosmeticKind.Title, CasinoProfileMetric.BestEndingCoins, 5000, "一次结束旅程时保留至少5000筹码")
        };

        /// 返回独立奖励配置副本；调用方修改不会改变永久解锁规则。
        public static CasinoCosmeticDefinition[] Definitions => Array.ConvertAll(definitions, Copy);
        /// 声望说明，可直接展示；不包含任何局内可花费筹码。
        public static string FameRule => "每个去重的正式/无尽结束旅程10声望，完成区域每区5，完成任务每个3，成功整蛊每次1，成功救场每次2；体面离场另加20，接管另加40。练习与未结束旅程不计，声望不进入团队筹码。";

        /// <summary>查找外观或称号的稳定定义。</summary>
        /// <param name="id">完整稳定ID；未知ID返回null。</param>
        /// <returns>定义深副本。</returns>
        public static CasinoCosmeticDefinition Find(string id) => Copy(Array.Find(definitions, entry => entry.Id == id));
        private static CasinoCosmeticDefinition Copy(CasinoCosmeticDefinition value) => value == null ? null : JsonUtility.FromJson<CasinoCosmeticDefinition>(JsonUtility.ToJson(value));
        private static CasinoCosmeticDefinition Define(string id, string name, CasinoCosmeticKind kind, CasinoProfileMetric metric, long threshold, string requirement, string color = null)
            => new CasinoCosmeticDefinition { Id = id, Name = name, Kind = kind, Metric = metric, Threshold = threshold, Requirement = requirement, ColorHex = color };
    }
}
