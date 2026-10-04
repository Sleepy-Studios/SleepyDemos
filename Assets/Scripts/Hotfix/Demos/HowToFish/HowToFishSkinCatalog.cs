using System;
using System.Collections.Generic;
using System.Linq;

namespace Hotfix.HowToFish
{
    public enum HowToFishSkinRarity { Default, Common, Rare, Legendary }
    public enum HowToFishSkinEffect { Standard, Rainbow }

    /// 已核实的外观标识与效果；材质由本项目独立制作。
    public sealed class HowToFishSkinDefinition
    {
        public string Id { get; }
        public string ItemId { get; }
        public string Name { get; }
        public HowToFishSkinRarity Rarity { get; }
        public HowToFishSkinEffect Effect { get; }

        internal HowToFishSkinDefinition(string itemId, string name, HowToFishSkinRarity rarity, HowToFishSkinEffect effect)
        { ItemId = itemId; Name = name; Id = itemId + "/" + name; Rarity = rarity; Effect = effect; }
    }

    public static class HowToFishSkinCatalog
    {
        private static readonly string[][] islandTypes =
        {
            new[] { "BrassKnuckles", "Knife" },
            new[] { "BrassKnuckles", "Knife", "Pistol", "FishingRod", "Shotgun", "Boat" },
            new[] { "Pistol", "SMG", "FishingRod", "Shotgun", "Boat" },
            new[] { "FishingRod", "SMG", "SniperRifle", "Boat" },
            new[] { "FishingRod", "AssaultRifle", "SniperRifle", "Boat" }
        };

        /// 包含 Default 类记录的完整九类外观表；Default 类不属于老虎机奖励。
        public static IReadOnlyList<HowToFishSkinDefinition> All { get; } = Array.AsReadOnly(Create());

        private static HowToFishSkinDefinition[] Create()
        {
            var result = new List<HowToFishSkinDefinition>();
            void Add(string itemId, string common, string rare, string legendary, string defaults = "Default", string rainbow = "")
            {
                var animated = rainbow.Split('|');
                var groups = new[] { defaults, common, rare, legendary };
                for (int rarity = 0; rarity < groups.Length; rarity++)
                    foreach (string name in groups[rarity].Split('|'))
                        result.Add(new HowToFishSkinDefinition(itemId, name, (HowToFishSkinRarity)rarity,
                            animated.Contains(name) ? HowToFishSkinEffect.Rainbow : HowToFishSkinEffect.Standard));
            }
            Add("Pistol", "Wood|Pink|White|Blaze|Bluze", "Emerald|Black and White|Inverted Diamond|Galaxy|Ruby", "Diamond|Gold", "Default|Missing");
            Add("Boat", "Winter|Red|Orange|Blue", "Camouflage|Emerald|Ruby|Polka|Blue Fade", "Rainbow|Gold|Diamond", "Default|Fire", "Rainbow");
            Add("BrassKnuckles", "Polka|Chess", "Camouflage|Wood|Emerald|Hazard|Ruby", "Gold|Rainbow|Diamond", rainbow: "Rainbow");
            Add("FishingRod", "Orange|Winter|Cow|Hazard|Wood", "Asiimov|Camouflage|Emerald|Ruby|Missing", "Galaxy|Gold|Diamond", rainbow: "Asiimov|Galaxy");
            Add("Knife", "Camouflage|Chess|Tiger|Hazard|Missing", "Blaze|Emerald|Wood|Polka|Rainbow|Galaxy|Ruby", "Diamond|Gold", rainbow: "Rainbow|Galaxy");
            Add("Shotgun", "Orange|Redwood|Greyscale", "Hazard|Wood|Bronze|Emerald|Ruby|GoldStriped", "Rainbow|Diamond|Gold", rainbow: "Rainbow");
            Add("SMG", "White|Blue|Pink|Red|Brown|Hazard", "Wood|Emerald|Ruby|Purple|Tiger|Cow", "Rainbow|Diamond|Gold", rainbow: "Rainbow");
            Add("SniperRifle", "Wood|Brown|Light Gray|Winter|Camouflage|Cow|Blaze|Sponge", "Asiimov|Emerald|Ruby|Tiger|Galaxy", "Gold|Rainbow", rainbow: "Asiimov|Galaxy|Rainbow");
            Add("AssaultRifle", "White", "Tiger|Hazard|Wood|Emerald|Ruby|Pink|Cow", "Rainbow|Diamond|Gold", "Default|Orange|Blue", "Rainbow");
            return result.ToArray();
        }

        /// <summary>按稳定的“类型/原名”查找外观，不存在时返回 null。</summary>
        /// <param name="id">例如 Pistol/Black and White。</param>
        public static HowToFishSkinDefinition Find(string id) => All.FirstOrDefault(skin => skin.Id == id);

        /// <summary>检查该装备或船是否有已核实的皮肤表。</summary>
        /// <param name="itemId">装备 ID，船使用 Boat。</param>
        public static bool Supports(string itemId) => All.Any(skin => skin.ItemId == itemId);

        /// <summary>验证实例所用外观；未设置表示默认，不要求该玩家已解锁此实例的外观。</summary>
        /// <param name="itemId">实例定义 ID。</param>
        /// <param name="skinId">外观 ID；null 或空串均为默认。</param>
        public static bool IsValidSelection(string itemId, string skinId) => string.IsNullOrEmpty(skinId) || Find(skinId)?.ItemId == itemId;

        /// <summary>返回该岛该稀有度的完整奖池；每个预设等权，包含已解锁的预设。</summary>
        /// <param name="island">0至4的岛屿序号。</param>
        /// <param name="rarity">只能是 Common、Rare、Legendary。</param>
        public static HowToFishSkinDefinition[] Rewards(int island, HowToFishSkinRarity rarity)
        {
            if (island < 0 || island >= islandTypes.Length || rarity < HowToFishSkinRarity.Common || rarity > HowToFishSkinRarity.Legendary)
                throw new ArgumentOutOfRangeException(nameof(island), "老虎机岛屿或奖励稀有度无效。");
            return All.Where(skin => skin.Rarity == rarity && islandTypes[island].Contains(skin.ItemId)).ToArray();
        }
    }
}
