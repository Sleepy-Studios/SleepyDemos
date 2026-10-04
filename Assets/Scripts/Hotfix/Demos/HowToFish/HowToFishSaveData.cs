using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// 单人存档快照；仅保存可恢复的业务状态，不序列化运行时物理对象。
    [Serializable]
    public sealed class HowToFishSaveData
    {
        // DTO 字段供 JsonUtility 序列化，不用于 Inspector 配置。
        public int version = 1;
        public int money;
        public int unlockedIsland;
        public int safeIsland;
        public float health = 100f;
        public float hunger = 100f;
        public float poisonSeconds;
        public float poisonDamagePerSecond;
        public float burningSeconds;
        public float burningDamagePerSecond;
        public float playedSeconds;
        public bool hasBoatKey;
        public int boatMotorTier;
        public bool hasBoatRadar;
        public string boatSkinId;
        public List<string> unlockedSkins = new List<string>();
        public bool hasGrill;
        public bool hasFinished;
        public bool hasMilitaryBoatKey;
        public int volcanoFish;
        public int forestLeeches;
        public Vector3 safePosition;
        public float safeYaw;
        public Vector3 boatPosition;
        public float boatYaw;
        public bool isOnBoat;
        public bool isDriving;
        public Vector3 boatLocalPosition;
        public float boatLocalYaw;
        public string equippedItemId;
        // 缺失或空列表表示尚未采用装备栏的旧档；会话按原有装备完整迁移。
        public List<string> equipmentSlots;
        public int selectedEquipmentSlot;
        public List<HowToFishOwnedItem> inventory = new List<HowToFishOwnedItem>();
        public List<string> completedQuests = new List<string>();
        public List<string> discoveredCreatures = new List<string>();
        public List<string> defeatedCreatures = new List<string>();
        public List<string> defeatedDripCreatures = new List<string>();
        public List<HowToFishWorldItemData> worldItems = new List<HowToFishWorldItemData>();

        /// 验证存档结构；拒绝不支持的版本、非法数值和重复的持久实体。
        public void Validate()
        {
            if (version != 1) throw new NotSupportedException("不支持的渔力全开存档版本：" + version);
            if (money < 0 || unlockedIsland < 0 || unlockedIsland > 4 || safeIsland < 0 || safeIsland > unlockedIsland)
                throw new FormatException("存档金钱或区域状态无效。");
            if (!IsFinite(health) || health < 0f || health > 100f || !IsFinite(hunger) || hunger < 0f || hunger > 100f ||
                !IsFinite(playedSeconds) || playedSeconds < 0f || !IsFinite(safePosition) || !IsFinite(boatPosition) ||
                !IsFinite(safeYaw) || !IsFinite(boatYaw) || !IsFinite(boatLocalPosition) || !IsFinite(boatLocalYaw))
                throw new FormatException("存档包含无效的生命、时间或坐标。");
            if (isDriving && (!isOnBoat || !hasBoatKey)) throw new FormatException("存档驾驶状态与船只权限不一致。");
            if (!(poisonSeconds >= 0 && poisonSeconds <= 60) || !(burningSeconds >= 0 && burningSeconds <= 60) ||
                !(poisonDamagePerSecond >= 0 && poisonDamagePerSecond <= 1000) || !(burningDamagePerSecond >= 0 && burningDamagePerSecond <= 1000) ||
                poisonSeconds > 0 && poisonDamagePerSecond == 0 || burningSeconds > 0 && burningDamagePerSecond == 0)
                throw new FormatException("存档中毒或燃烧状态无效。");
            if (boatMotorTier < 0 || boatMotorTier > 2) throw new FormatException("船只马达级别无效。");
            if (!HowToFishSkinCatalog.IsValidSelection("Boat", boatSkinId)) throw new FormatException("船只皮肤无效。");
            // 旧档没有皮肤解锁字段，视为尚未解锁；实例选择与玩家解锁记录分别验证。
            unlockedSkins ??= new List<string>();
            ValidateUnlockedSkins(unlockedSkins);
            if (volcanoFish < 0 || volcanoFish > 5 || (hasFinished && !hasMilitaryBoatKey) || (hasMilitaryBoatKey && unlockedIsland < 4))
                throw new FormatException("火山任务或返航状态无效。");
            if (forestLeeches < 0 || forestLeeches > 2) throw new FormatException("森林水蛭交付数量无效。");
            if (!string.IsNullOrEmpty(equippedItemId) && !ValidId(equippedItemId)) throw new FormatException("存档装备标识无效。");
            if (inventory == null || completedQuests == null || discoveredCreatures == null || defeatedCreatures == null ||
                defeatedDripCreatures == null || worldItems == null)
                throw new FormatException("存档缺少状态列表。");
            if (inventory.Count > 512 || worldItems.Count > 2048) throw new FormatException("存档物品数量异常。");
            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in inventory)
            {
                if (item == null || !item.IsValid || !itemIds.Add(item.id))
                    throw new FormatException("存档背包项无效或重复。");
            }
            if (equipmentSlots != null && equipmentSlots.Count > 0)
            {
                if (equipmentSlots.Count < 3 || equipmentSlots.Count > 512 || selectedEquipmentSlot < 0 || selectedEquipmentSlot >= equipmentSlots.Count)
                    throw new FormatException("存档装备栏容量或选择无效。");
                var slotted = new HashSet<string>(StringComparer.Ordinal);
                foreach (string id in equipmentSlots)
                    if (!string.IsNullOrEmpty(id) && (!itemIds.Contains(id) || !slotted.Add(id))) throw new FormatException("装备栏引用无效或重复。");
            }
            ValidateIds(completedQuests);
            ValidateIds(discoveredCreatures);
            ValidateIds(defeatedCreatures);
            ValidateIds(defeatedDripCreatures);
            itemIds.Clear();
            foreach (var item in worldItems)
            {
                if (item == null || !ValidId(item.instanceId) || !ValidId(item.definitionId) || !itemIds.Add(item.instanceId) ||
                    !IsFinite(item.position) || !IsFinite(item.eulerAngles) || !IsFinite(item.health) || item.health < 0f ||
                    !IsFinite(item.cooking) || item.cooking < 0 || item.cooking > 1 ||
                    !IsFinite(item.styleMultiplier) || item.styleMultiplier < 1 ||
                    !IsFinite(item.bettingMultiplier) || item.bettingMultiplier < 1 ||
                    !IsFinite(item.weightMultiplier) || item.weightMultiplier < .8f || item.weightMultiplier > 1.2f ||
                    !(item.dynamiteFuseSeconds >= 0 && item.dynamiteFuseSeconds <= 3) ||
                    item.dynamiteFuseSeconds > 0 && item.definitionId != "Dynamite" ||
                    item.HasEquipment && (!item.equipment.IsValid || item.equipment.id != item.definitionId || item.equipment.count != 1))
                    throw new FormatException("存档世界物品无效或重复。");
            }
        }

        private static void ValidateIds(List<string> ids)
        {
            if (ids.Count > 1024) throw new FormatException("存档记录数量异常。");
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in ids)
                if (!ValidId(id) || !unique.Add(id)) throw new FormatException("存档记录标识无效或重复。");
        }

        internal static void ValidateUnlockedSkins(List<string> skins)
        {
            if (skins == null || skins.Count > HowToFishSkinCatalog.All.Count) throw new FormatException("皮肤解锁列表无效。");
            var skinIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (string skinId in skins)
            {
                var skin = HowToFishSkinCatalog.Find(skinId);
                if (skin == null || skin.Rarity == HowToFishSkinRarity.Default || !skinIds.Add(skinId))
                    throw new FormatException("皮肤解锁记录无效或重复。");
            }
        }

        internal static bool ValidId(string id) => !string.IsNullOrWhiteSpace(id) && id.Length <= 128;
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }

    /// 背包中的物品数量与升级级别。
    [Serializable]
    public sealed class HowToFishOwnedItem
    {
        public string id;
        public int count;
        public int upgrade;
        // -1 表示尚未初始化弹匣；旧存档仍可在首次装备时建立弹药状态。
        public int ammo = -1;
        public HowToFishAttachment sight;
        public HowToFishAttachment barrel;
        public bool hasLaser;
        public bool hasExtendedMag;
        public float cooking;
        public string skinId;
        internal bool IsValid => HowToFishSaveData.ValidId(id) && count >= 1 && count <= 100000 && upgrade >= 0 && upgrade <= 100 &&
            ammo >= -1 && ammo <= 4096 && cooking >= 0 && cooking <= 1 && HasValidAttachments && HowToFishSkinCatalog.IsValidSelection(id, skinId);

        /// 创建独立装备状态，防止落地实体与背包共享可变进度。
        public HowToFishOwnedItem Copy() => (HowToFishOwnedItem)MemberwiseClone();

        internal bool HasValidAttachments =>
            (sight == HowToFishAttachment.None || sight == HowToFishAttachment.RedDotSight || sight == HowToFishAttachment.SniperScope) &&
            (barrel == HowToFishAttachment.None || barrel == HowToFishAttachment.Compensator || barrel == HowToFishAttachment.Suppressor);

        /// <summary>判断单件武器已经安装的配件。</summary>
        /// <param name="attachment">查询的配件。</param>
        public bool HasAttachment(HowToFishAttachment attachment) => attachment switch
        {
            HowToFishAttachment.RedDotSight or HowToFishAttachment.SniperScope => sight == attachment,
            HowToFishAttachment.Compensator or HowToFishAttachment.Suppressor => barrel == attachment,
            HowToFishAttachment.LaserSight => hasLaser,
            HowToFishAttachment.ExtendedMag => hasExtendedMag,
            _ => false
        };
    }

    /// 安全检查点中的散落物品；活动首领不进入此列表。
    [Serializable]
    public sealed class HowToFishWorldItemData
    {
        public string instanceId;
        public string definitionId;
        public Vector3 position;
        public Vector3 eulerAngles;
        public float health;
        public bool isCooked;
        public float cooking;
        public bool hasBeenHeld;
        public float styleMultiplier = 1;
        /// 累计轮盘倍率；旧档缺失时按1读取。
        public float bettingMultiplier = 1;
        /// 个体重量倍率；旧档缺失字段按1读取。
        public float weightMultiplier = 1;
        public bool hasBeenHitByPlayer;
        public bool isDrip;
        public HowToFishOwnedItem equipment;
        /// 剩余引信游戏秒数；旧档缺失或零表示未点燃。
        public float dynamiteFuseSeconds;
        // JsonUtility 会把空的内联对象恢复成默认实例，以物品 ID 区分实际装备快照。
        public bool HasEquipment => equipment != null && !string.IsNullOrEmpty(equipment.id);
    }
}
