using System;
using System.Collections.Generic;
using System.Linq;

namespace Hotfix.HowToFish
{
    /// 人物服装目录；条件采用已记录的社区映射，Bean 造型为本项目自主主题。
    public static class HowToFishOutfitCatalog
    {
        /// 旧档未选择服装时使用的项目默认款。
        public const string DefaultId = "Fisherman";

        /// 四款默认服装与十四款可解锁服装，顺序供选装界面使用。
        public static IReadOnlyList<HowToFishOutfitDefinition> All { get; } = Array.AsReadOnly(new[]
        {
            new HowToFishOutfitDefinition("Badman", "泳者", "默认解锁", true),
            new HowToFishOutfitDefinition("Bikini", "比基尼", "默认解锁", true),
            new HowToFishOutfitDefinition("Fisherman", "渔夫", "默认解锁", true),
            new HowToFishOutfitDefinition("Sailor", "水手", "默认解锁", true),
            new HowToFishOutfitDefinition("LighthouseKeeper", "灯塔看守人", "击败蜘蛛蟹"),
            new HowToFishOutfitDefinition("SwampMan", "沼泽男子", "首次升级船只马达"),
            new HowToFishOutfitDefinition("SwampLady", "沼泽女士", "击败巨型食人鱼"),
            new HowToFishOutfitDefinition("KioskLady", "售货亭女士", "吃下一只烧焦的生物"),
            new HowToFishOutfitDefinition("Tourist", "游客", "击败河豚"),
            new HowToFishOutfitDefinition("GrillMaster", "烧烤师", "领取打火机并解锁烤架"),
            new HowToFishOutfitDefinition("Andrei", "安德烈", "轮盘押绿并获胜"),
            new HowToFishOutfitDefinition("Jacob", "雅各布", "老虎机获得传奇皮肤"),
            new HowToFishOutfitDefinition("GunstoreClerc", "枪店伙计", "同枪升级伤害并装上瞄具、枪口、激光和扩容"),
            new HowToFishOutfitDefinition("ScaredGuyInShorts", "短裤男子", "击败信天翁"),
            new HowToFishOutfitDefinition("StoreGrandma", "商店奶奶", "购买最高档船只马达"),
            new HowToFishOutfitDefinition("Military", "军人", "击败变异弓头鲸"),
            new HowToFishOutfitDefinition("Scientist", "科学家", "完成最后的离岛互动"),
            new HowToFishOutfitDefinition("Bean", "豆豆", "一小时内完成新航程，包含暂停时间")
        });

        /// <summary>查找稳定服装 ID；未知或空 ID 返回 null。</summary>
        /// <param name="id">例如 Fisherman 或 LighthouseKeeper。</param>
        public static HowToFishOutfitDefinition Find(string id) => All.FirstOrDefault(outfit => outfit.Id == id);

        /// <summary>默认款始终可选；奖励款必须已经解锁。</summary>
        /// <param name="id">待选择服装的稳定 ID。</param>
        /// <param name="unlockedOutfits">本地玩家已解锁的奖励服装。</param>
        public static bool IsUnlocked(string id, IEnumerable<string> unlockedOutfits)
        {
            var outfit = Find(id);
            return outfit != null && (outfit.IsDefault || unlockedOutfits?.Contains(id) == true);
        }
    }

    /// 服装显示与解锁提示，不包含运行时角色实例。
    public sealed class HowToFishOutfitDefinition
    {
        /// 源模型名称为 Outfit 加此稳定标识。
        public string Id { get; }
        /// 选装界面的中文名称。
        public string Name { get; }
        /// 未解锁时显示的明确条件。
        public string UnlockHint { get; }
        /// 是否无需奖励记录即可使用。
        public bool IsDefault { get; }

        internal HowToFishOutfitDefinition(string id, string name, string unlockHint, bool isDefault = false)
        { Id = id; Name = name; UnlockHint = unlockHint; IsDefault = isDefault; }
    }
}
