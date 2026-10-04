using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 各区域的鱼池、装备与价格配置；运行进度存放在独立会话中。
    [CreateAssetMenu(menuName = "SleepyDemos/HowToFish/Catalog")]
    public sealed class HowToFishCatalog : ScriptableObject
    {
        [SerializeField] private HowToFishCreatureDefinition[] creatures = Array.Empty<HowToFishCreatureDefinition>();
        [SerializeField] private HowToFishItemDefinition[] items = Array.Empty<HowToFishItemDefinition>();
        [SerializeField] private HowToFishOutfitVisual[] outfits = Array.Empty<HowToFishOutfitVisual>();
        [SerializeField] private float dripChance = 0.1f;

        /// 生物定义；名称和稳定 ID 与参考清单保持一致。
        public IReadOnlyList<HowToFishCreatureDefinition> Creatures => creatures;
        /// 商店与背包的物品定义。
        public IReadOnlyList<HowToFishItemDefinition> Items => items;
        /// 服装的自制模型和图标；名称与解锁规则由 OutfitCatalog 维护。
        public IReadOnlyList<HowToFishOutfitVisual> Outfits => outfits;
        /// 普通钓获独立进行的珍稀判定，不用于首领。
        public float DripChance => dripChance;

        /// <summary>查找生物定义。</summary>
        /// <param name="id">稳定的生物 ID。</param>
        public HowToFishCreatureDefinition FindCreature(string id) => Array.Find(creatures, entry => entry.Id == id);

        /// <summary>查找物品定义。</summary>
        /// <param name="id">稳定的物品 ID。</param>
        public HowToFishItemDefinition FindItem(string id) => Array.Find(items, entry => entry.Id == id);

        /// <summary>查找已装配的服装模型和图标。</summary>
        /// <param name="id">OutfitCatalog 中的稳定服装 ID。</param>
        public HowToFishOutfitVisual FindOutfit(string id) => Array.Find(outfits, entry => entry.Id == id);

        /// <summary>根据区域、鱼饵和随机抽样选择鱼获；无匹配时返回 null。</summary>
        /// <param name="island">区域索引。</param>
        /// <param name="baitId">已装备的鱼饵 ID。</param>
        /// <param name="sample">范围 [0,1) 的随机抽样。</param>
        /// <param name="rodId">当前实际装备的鱼竿 ID。</param>
        public HowToFishCreatureDefinition RollCatch(int island, string baitId, float sample, string rodId)
        {
            if (island < 0 || island > 4 || string.IsNullOrEmpty(baitId)) throw new ArgumentException("鱼池条件无效。");
            if (FindItem(rodId)?.Kind != HowToFishItemKind.Rod) throw new ArgumentException("鱼池需要有效的鱼竿。");
            if (float.IsNaN(sample) || sample < 0 || sample >= 1) throw new ArgumentOutOfRangeException(nameof(sample));
            float total = 0;
            foreach (var creature in creatures)
                if (creature.CanCatch(island, baitId, rodId)) total += creature.WeightFor(baitId);
            if (total <= 0) return null;
            float threshold = sample * total;
            HowToFishCreatureDefinition last = null;
            foreach (var creature in creatures)
            {
                if (!creature.CanCatch(island, baitId, rodId)) continue;
                last = creature;
                threshold -= creature.WeightFor(baitId);
                if (threshold < 0) return creature;
            }
            return last;
        }

        /// 检查配置 ID、价格与鱼池参数，避免运行期间出现不可推进的配置。
        public void Validate()
        {
            if (creatures == null || items == null || outfits == null || float.IsNaN(dripChance) || dripChance < 0 || dripChance > 1)
                throw new InvalidOperationException("渔力全开配置不完整。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var outfit in outfits)
                if (outfit == null || HowToFishOutfitCatalog.Find(outfit.Id) == null || !ids.Add(outfit.Id) || outfit.Prefab == null || outfit.Icon == null)
                    throw new InvalidOperationException("渔力全开服装资源缺失或 ID 重复。");
            ids.Clear();
            foreach (var item in items)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Id) || !ids.Add(item.Id) || item.Price < 0 ||
                    item.Island < 0 || item.Island > 4 || item.Damage < 0 || item.MagazineSize < 0 || item.ReloadSeconds <= 0 ||
                    !item.HasValidUpgrades || !item.HasValidAttachments ||
                    !item.HasValidBaitSettings ||
                    (item.Kind == HowToFishItemKind.Gun && (item.MagazineSize < 1 || item.MagazineSize > 4096 ||
                    item.Pellets < 1 || item.Pellets > 64 || item.Spread < 0 || item.Spread > 45)))
                    throw new InvalidOperationException("渔力全开物品配置无效或 ID 重复。");
            }
            ids.Clear();
            foreach (var creature in creatures)
            {
                if (creature == null || string.IsNullOrWhiteSpace(creature.Id) || !ids.Add(creature.Id) ||
                    creature.Island < 0 || creature.Island > 4 || creature.Value < 0 || !(creature.Health >= 0) || float.IsInfinity(creature.Health) ||
                    creature.Health == 0 && !creature.IsGroundPickup ||
                    float.IsNaN(creature.Weight) || float.IsInfinity(creature.Weight) || creature.Weight <= 0 || creature.Length <= 0 ||
                    !creature.HasValidNutrition ||
                    creature.Baits == null || !creature.HasValidBaitWeights || creature.IsMiniBoss && !creature.IsBoss)
                    throw new InvalidOperationException("渔力全开生物配置无效或 ID 重复。");
                foreach (var bait in creature.Baits)
                    if (FindItem(bait) == null) throw new InvalidOperationException(creature.Id + " 的鱼饵未登记：" + bait);
                if (!string.IsNullOrEmpty(creature.RequiredRodId) && FindItem(creature.RequiredRodId)?.Kind != HowToFishItemKind.Rod)
                    throw new InvalidOperationException(creature.Id + " 的鱼竿未登记：" + creature.RequiredRodId);
            }
        }
    }

    /// 服装资源引用；不重复声明名称、稀有度或解锁条件。
    [Serializable]
    public sealed class HowToFishOutfitVisual
    {
        [SerializeField] private string id;
        [SerializeField] private GameObject prefab;
        [SerializeField] private Sprite icon;

        public string Id => id;
        public GameObject Prefab => prefab;
        public Sprite Icon => icon;
    }

    /// 生物移动与攻击的表现类型，同类鱼仍由尺寸、速度和数值区分。
    public enum HowToFishCreatureMotion { Shell, Crab, Fish, Eel, Ray, Charge, Spiked, Flying, Leaping, Whale, MagmaWhale }
    /// 装备或消耗品的具体用途。
    public enum HowToFishItemKind { Rod, Melee, Gun, Bait, Food, Radar, Quest, Explosive, Skin }
    /// 顺序同时用于持久化与枪械配件价格表；零表示原装。
    public enum HowToFishAttachment { None, RedDotSight, SniperScope, Compensator, Suppressor, LaserSight, ExtendedMag }

    /// 单一生物的可调定义。
    [Serializable]
    public sealed class HowToFishCreatureDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private int island;
        [SerializeField] private string requiredRodId;
        [SerializeField] private string[] baits = Array.Empty<string>();
        [Tooltip("按鱼饵顺序覆盖权重；留空时所有鱼饵共用 Weight。")]
        [SerializeField] private float[] baitWeights = Array.Empty<float>();
        [SerializeField] private float weight = 1;
        [SerializeField] private int value = 3;
        [SerializeField] private float health = 10;
        [SerializeField] private float baseWeight = 1;
        [SerializeField] private bool skipRandomizedWeight;
        [SerializeField] private float healthRestored = 12;
        [SerializeField] private float fullnessRestored = 20;
        [SerializeField] private bool ignoredBySeller;
        [SerializeField] private bool ignoredBySeagulls;
        [SerializeField] private float length = 0.4f;
        [SerializeField] private float strength = 1;
        [SerializeField] private bool isBoss;
        [SerializeField] private bool isMiniBoss;
        [SerializeField] private bool excludeFromJournal;
        [SerializeField] private bool isGroundPickup;
        [SerializeField] private bool isEndangered;
        [SerializeField] private HowToFishCreatureMotion motion;
        [SerializeField] private GameObject prefab;

        public string Id => id;
        public string DisplayName => displayName;
        public int Island => island;
        public string RequiredRodId => requiredRodId;
        public IReadOnlyList<string> Baits => baits;
        public float Weight => weight;
        public int Value => value;
        public float Health => health;
        public float BaseWeight => baseWeight;
        /// 来源明确跳过个体随机重量的生物使用基础重量。
        public bool SkipRandomizedWeight => skipRandomizedWeight;
        public float HealthRestored => healthRestored;
        public float FullnessRestored => fullnessRestored;
        public bool IgnoredBySeller => ignoredBySeller;
        public bool IgnoredBySeagulls => ignoredBySeagulls;
        public bool IsMainBoss => isBoss && !isMiniBoss;
        public float Length => length;
        public float Strength => strength;
        public bool IsBoss => isBoss;
        /// 可选首领按鱼饵开放到首岛以外，击杀同时登记两种图鉴。
        public bool IsMiniBoss => isMiniBoss;
        /// 是否参与普通及 Drip 图鉴；环境拾取物与隐藏生物不计入收集总数。
        public bool IsJournalEntry => !excludeFromJournal;
        public bool IsGroundPickup => isGroundPickup;
        public bool IsEndangered => isEndangered;
        public HowToFishCreatureMotion Motion => motion;
        public GameObject Prefab => prefab;

        internal bool CanCatch(int location, string bait, string rod) => !isGroundPickup &&
            (!IsMainBoss || location == island) && Array.IndexOf(baits, bait) >= 0 &&
            (bait != "FreeLure" || string.IsNullOrEmpty(requiredRodId) || requiredRodId == rod);

        internal bool HasValidBaitWeights => baitWeights == null || baitWeights.Length == 0 ||
            baitWeights.Length == baits.Length && Array.TrueForAll(baitWeights, value => value > 0 && !float.IsInfinity(value));

        internal bool HasValidNutrition => baseWeight > 0 && baseWeight <= 1000000 &&
            healthRestored >= 0 && healthRestored <= 100000 && fullnessRestored >= 0 && fullnessRestored <= 100000;

        internal float WeightFor(string bait) => baitWeights == null || baitWeights.Length == 0 ? weight : baitWeights[Array.IndexOf(baits, bait)];
    }

    /// 装备、鱼饵和消耗品的购买与使用参数。
    [Serializable]
    public sealed class HowToFishItemDefinition
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private HowToFishItemKind kind;
        [SerializeField] private int island;
        [SerializeField] private int price;
        [SerializeField] private float minimumBiteSeconds = 2;
        [SerializeField] private float maximumBiteSeconds = 5;
        [SerializeField] private float baitLossChance = 1;
        [SerializeField] private bool baitRequiresMovement = true;
        [SerializeField] private float damage = 10;
        [SerializeField] private float[] upgradedDamage = Array.Empty<float>();
        [SerializeField] private int[] upgradeCosts = Array.Empty<int>();
        [SerializeField] private float useInterval = 0.5f;
        [SerializeField] private int magazineSize;
        [SerializeField] private int extendedMagazineSize;
        [Tooltip("依次为红点、瞄准镜、补偿器、消音器、激光、扩容价格；零表示不兼容。")]
        [SerializeField] private int[] attachmentPrices = Array.Empty<int>();
        [SerializeField] private float recoilAngle;
        [SerializeField] private float reloadSeconds = 1.6f;
        [SerializeField] private float range = 3;
        [SerializeField] private float nourishment = 20;
        [SerializeField] private int pellets = 1;
        [SerializeField] private float spread;
        [SerializeField] private bool isConsumable;
        [SerializeField] private bool isCookable;
        [SerializeField] private bool automatic;
        [SerializeField] private GameObject prefab;
        [SerializeField] private GameObject viewPrefab;

        public string Id => id;
        public string DisplayName => displayName;
        public HowToFishItemKind Kind => kind;
        public bool IsEquipment => kind == HowToFishItemKind.Rod || kind == HowToFishItemKind.Melee || kind == HowToFishItemKind.Gun ||
            kind == HowToFishItemKind.Radar || kind == HowToFishItemKind.Explosive || kind == HowToFishItemKind.Food && viewPrefab != null;
        public int Island => island;
        public int Price => price;
        public float Damage => damage;
        public int MaxUpgrade => upgradeCosts?.Length ?? 0;
        internal bool HasValidUpgrades => upgradedDamage != null && upgradeCosts != null &&
            upgradedDamage.Length == upgradeCosts.Length && upgradeCosts.Length <= 100 &&
            Array.TrueForAll(upgradeCosts, value => value > 0) &&
            Array.TrueForAll(upgradedDamage, value => value > 0 && !float.IsInfinity(value));

        /// <summary>取得当前升级等级的单颗弹丸或单次近战伤害。</summary>
        /// <param name="level">已购买的升级等级；旧档越界时使用最高配置。</param>
        public float DamageAtLevel(int level) => level <= 0 || MaxUpgrade == 0 ? damage : upgradedDamage[Math.Min(level, MaxUpgrade) - 1];

        /// <summary>取得下一次升级费用；无可用升级时返回零。</summary>
        /// <param name="level">已购买的升级等级。</param>
        public int NextUpgradeCost(int level) => level >= 0 && level < MaxUpgrade ? upgradeCosts[level] : 0;
        public float UseInterval => useInterval;
        public int MagazineSize => magazineSize;
        public int ExtendedMagazineSize => extendedMagazineSize;
        public float RecoilAngle => recoilAngle;
        internal bool HasValidAttachments => attachmentPrices != null && (attachmentPrices.Length == 0 || attachmentPrices.Length == 6) &&
            Array.TrueForAll(attachmentPrices, value => value >= 0) &&
            (extendedMagazineSize == 0 || extendedMagazineSize >= magazineSize && extendedMagazineSize <= 4096) &&
            recoilAngle >= 0 && recoilAngle <= 20;

        /// <summary>读取该枪配件价格，零表示不支持。</summary>
        /// <param name="attachment">拟安装的配件。</param>
        public int AttachmentPrice(HowToFishAttachment attachment)
        {
            int index = (int)attachment - 1;
            return index >= 0 && index < (attachmentPrices?.Length ?? 0) ? attachmentPrices[index] : 0;
        }
        public float ReloadSeconds => reloadSeconds;
        public float Range => range;
        public float Nourishment => nourishment;
        public float MinimumBiteSeconds => minimumBiteSeconds;
        public float MaximumBiteSeconds => maximumBiteSeconds;
        public float BaitLossChance => isConsumable ? baitLossChance : 0;
        public bool BaitRequiresMovement => baitRequiresMovement;
        internal bool HasValidBaitSettings => minimumBiteSeconds > 0 && maximumBiteSeconds >= minimumBiteSeconds &&
            maximumBiteSeconds <= 300 && baitLossChance >= 0 && baitLossChance <= 1;
        public int Pellets => pellets;
        public float Spread => spread;
        public bool IsConsumable => isConsumable;
        public bool IsCookable => isCookable;
        public bool Automatic => automatic;
        public GameObject Prefab => prefab;
        /// 保存好的第一人称装备结构，不在运行时拆除世界物品的物理组件。
        public GameObject ViewPrefab => viewPrefab;
    }
}
