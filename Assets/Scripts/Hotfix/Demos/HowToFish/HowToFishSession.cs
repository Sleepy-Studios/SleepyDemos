using System;
using System.Collections.Generic;

namespace Hotfix.HowToFish
{
    /// 单人会话的交易、背包与收集规则；输入和物理层不能自行修改金钱。
    public sealed class HowToFishSession
    {
        private readonly HowToFishCatalog catalog;
        private readonly HowToFishSaveData state;
        private static readonly int[] slotCosts = { 5, 10, 25, 50, 100 };

        /// 状态变化后由 HUD 统一刷新。
        public event Action Changed;
        /// 当前可持久化状态，世界协调器负责在安全点更新物理快照。
        public HowToFishSaveData State => state;
        public int EquipmentCapacity => state.equipmentSlots.Count;
        public int NextSlotCost => EquipmentCapacity < 8 ? slotCosts[EquipmentCapacity - 3] : 0;
        public HowToFishOwnedItem UnstoredEquipment => state.inventory.Find(item => catalog.FindItem(item.id)?.IsEquipment == true && !state.equipmentSlots.Contains(item.id));

        /// <summary>从已验证配置和存档建立单人会话。</summary>
        /// <param name="catalog">游戏内容定义。</param>
        /// <param name="state">新档或读档状态。</param>
        public HowToFishSession(HowToFishCatalog catalog, HowToFishSaveData state)
        {
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            catalog.Validate();
            state.Validate();
            if (state.equipmentSlots == null || state.equipmentSlots.Count == 0)
            {
                var equipment = state.inventory.FindAll(item => catalog.FindItem(item.id)?.IsEquipment == true);
                state.equipmentSlots = new List<string>();
                // 旧原型没有容量限制，迁移时保留全部已有装备；新档从三个栏位开始。
                for (int i = 0; i < Math.Max(3, equipment.Count); i++) state.equipmentSlots.Add(i < equipment.Count ? equipment[i].id : "");
                state.selectedEquipmentSlot = Math.Max(0, state.equipmentSlots.IndexOf(state.equippedItemId));
            }
            foreach (string id in state.equipmentSlots)
                if (!string.IsNullOrEmpty(id) && catalog.FindItem(id)?.IsEquipment != true) throw new FormatException("装备栏包含不能装备的物品。");
            if (!string.IsNullOrEmpty(state.equippedItemId) && (Count(state.equippedItemId) == 0 || catalog.FindItem(state.equippedItemId)?.IsEquipment != true))
                throw new FormatException("当前装备不在背包中或不能装备。");
            if (state.inventory.FindAll(item => catalog.FindItem(item.id)?.IsEquipment == true && !state.equipmentSlots.Contains(item.id)).Count > 1)
                throw new FormatException("存档包含多件未收纳装备。");
            foreach (var item in state.inventory)
                if (catalog.FindItem(item.id)?.IsEquipment == true && !HasCompatibleEquipmentState(item)) throw new FormatException("存档装备配件不兼容：" + item.id);
            // 旧进度中已击败的首领同样满足普通与 Drip 两种登记。
            foreach (string id in state.defeatedCreatures)
                if (catalog.FindCreature(id)?.IsBoss == true && !state.defeatedDripCreatures.Contains(id)) state.defeatedDripCreatures.Add(id);
        }

        /// <summary>读取某物品的持有数量。</summary>
        /// <param name="id">物品 ID。</param>
        public int Count(string id) => state.inventory.Find(item => item.id == id)?.count ?? 0;

        /// <summary>读取装备升级级别。</summary>
        /// <param name="id">装备 ID。</param>
        public int UpgradeLevel(string id) => state.inventory.Find(item => item.id == id)?.upgrade ?? 0;

        /// <summary>登记本地解锁，不改变任何现有装备外观；重复中奖返回 false。</summary>
        /// <param name="skinId">Common、Rare 或 Legendary 外观的稳定 ID。</param>
        public bool UnlockSkin(string skinId)
        {
            var skin = HowToFishSkinCatalog.Find(skinId);
            if (skin == null || skin.Rarity == HowToFishSkinRarity.Default) throw new ArgumentException("不是可解锁的奖励皮肤。", nameof(skinId));
            if (state.unlockedSkins.Contains(skinId)) return false;
            state.unlockedSkins.Add(skinId);
            Changed?.Invoke();
            return true;
        }

        /// <summary>在默认和本地已解锁外观中循环；实例自带但未解锁的皮肤切换后回到默认。</summary>
        /// <param name="itemId">皮肤类型，例如 Boat 或 Pistol。</param>
        /// <param name="currentSkinId">当前实例选择；空值为默认。</param>
        public string NextUnlockedSkin(string itemId, string currentSkinId)
        {
            if (!HowToFishSkinCatalog.Supports(itemId) || !HowToFishSkinCatalog.IsValidSelection(itemId, currentSkinId))
                throw new ArgumentException("当前物品或皮肤类型无效。");
            var options = new List<string> { null };
            foreach (var skin in HowToFishSkinCatalog.All)
                if (skin.ItemId == itemId && state.unlockedSkins.Contains(skin.Id)) options.Add(skin.Id);
            int index = options.IndexOf(string.IsNullOrEmpty(currentSkinId) || currentSkinId == itemId + "/Default" ? null : currentSkinId);
            return options[(index + 1) % options.Count];
        }

        /// <summary>切换背包中该装备的已解锁皮肤，保留升级和配件。</summary>
        /// <param name="id">已拥有的装备 ID。</param>
        public bool ChangeEquipmentSkin(string id)
        {
            var owned = state.inventory.Find(item => item.id == id);
            if (owned == null || !HowToFishSkinCatalog.Supports(id)) return false;
            owned.skinId = NextUnlockedSkin(id, owned.skinId);
            Changed?.Invoke();
            return true;
        }

        /// 循环船只的默认和本地已解锁外观。
        public void ChangeBoatSkin()
        {
            state.boatSkinId = NextUnlockedSkin("Boat", state.boatSkinId);
            Changed?.Invoke();
        }

        /// <summary>购买更高级马达，允许从初始马达直接升级到双机。</summary>
        /// <param name="tier">中型为1，大型双机为2。</param>
        /// <param name="shopIsland">实际商店所在区域。</param>
        /// <param name="reason">购买失败的原因。</param>
        public bool TryBuyMotor(int tier, int shopIsland, out string reason)
        {
            if (tier < 1 || tier > 2 || shopIsland < (tier == 1 ? 1 : 3) || shopIsland > state.unlockedIsland || shopIsland > 4)
                return Fail("此区域尚未出售该马达。", out reason);
            if (!state.hasBoatKey) return Fail("请先取得船钥匙。", out reason);
            if (tier <= state.boatMotorTier) return Fail("已经安装相同或更高级的马达。", out reason);
            int cost = tier == 1 ? 230 : 860;
            if (state.money < cost) return Fail("升级马达的费用不足。", out reason);
            state.money -= cost;
            state.boatMotorTier = tier;
            reason = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>购买船载雷达，不占用装备栏，也不解锁任务坐标。</summary>
        /// <param name="shopIsland">实际商店所在区域，从沙漠开始出售。</param>
        /// <param name="reason">购买失败的原因。</param>
        public bool TryBuyBoatRadar(int shopIsland, out string reason)
        {
            if (shopIsland < 2 || shopIsland > state.unlockedIsland || shopIsland > 4)
                return Fail("此区域尚未出售船载雷达。", out reason);
            if (!state.hasBoatKey) return Fail("请先取得船钥匙。", out reason);
            if (state.hasBoatRadar) return Fail("已经安装船载雷达。", out reason);
            if (state.money < 200) return Fail("购买船载雷达的费用不足。", out reason);
            state.money -= 200;
            state.hasBoatRadar = true;
            reason = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>购买一个商店物品；失败不会扣钱或改变背包。</summary>
        /// <param name="id">物品 ID。</param>
        /// <param name="shopIsland">玩家实际交互的商店区域。</param>
        /// <param name="reason">失败时可显示的原因。</param>
        public bool TryBuy(string id, int shopIsland, out string reason)
        {
            var definition = catalog.FindItem(id);
            if (definition == null || definition.Kind == HowToFishItemKind.Quest) return Fail("此物品不可购买。", out reason);
            if (shopIsland < 0 || shopIsland > state.unlockedIsland || definition.Island > shopIsland)
                return Fail("此区域尚未出售该物品。", out reason);
            if (!definition.IsConsumable && Count(id) > 0) return Fail("已经拥有此装备。", out reason);
            if (definition.IsEquipment && Count(id) == 0 && UnstoredEquipment != null) return Fail("请先收纳或放下手中未收纳的装备。", out reason);
            if (Count(id) >= 100000) return Fail("该物品已达到携带上限。", out reason);
            if (state.money < definition.Price) return Fail("钱不够，再卖一些鱼吧。", out reason);
            state.money -= definition.Price;
            AddItem(id, 1);
            reason = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>按武器逐级价格升级；每个商店区域开放三级。</summary>
        /// <param name="id">已持有的近战或枪械 ID。</param>
        /// <param name="shopIsland">实际交互的商店区域；枪械升级从森林开始。</param>
        /// <param name="reason">失败原因。</param>
        public bool TryUpgrade(string id, int shopIsland, out string reason)
        {
            var definition = catalog.FindItem(id);
            var owned = state.inventory.Find(item => item.id == id);
            if (definition == null || owned == null || (definition.Kind != HowToFishItemKind.Melee && definition.Kind != HowToFishItemKind.Gun))
                return Fail("需要先拥有可升级的武器。", out reason);
            if (shopIsland < 0 || shopIsland > state.unlockedIsland || shopIsland > 4)
                return Fail("此区域尚未开放。", out reason);
            if (owned.upgrade >= definition.MaxUpgrade) return Fail("此武器已升满。", out reason);
            int regionLimit = (shopIsland + (definition.Kind == HowToFishItemKind.Melee ? 1 : 0)) * 3;
            if (owned.upgrade >= regionLimit) return Fail("本岛升级已达上限，请前往后续区域。", out reason);
            int cost = definition.NextUpgradeCost(owned.upgrade);
            if (state.money < cost) return Fail("升级费用不足。", out reason);
            state.money -= cost;
            owned.upgrade++;
            reason = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>检查当前武器、商店区域和余额是否允许安装配件。</summary>
        /// <param name="id">已持有的枪械 ID。</param>
        /// <param name="attachment">拟安装的配件。</param>
        /// <param name="shopIsland">实际交互的商店区域。</param>
        /// <param name="reason">不可购买的原因。</param>
        public bool CanBuyAttachment(string id, HowToFishAttachment attachment, int shopIsland, out string reason)
        {
            var definition = catalog.FindItem(id);
            var owned = state.inventory.Find(item => item.id == id);
            if (definition?.Kind != HowToFishItemKind.Gun || owned == null) return Fail("请先手持枪械。", out reason);
            int firstIsland = attachment switch
            {
                HowToFishAttachment.LaserSight => 1,
                HowToFishAttachment.RedDotSight or HowToFishAttachment.Compensator or HowToFishAttachment.ExtendedMag => 2,
                HowToFishAttachment.SniperScope or HowToFishAttachment.Suppressor => 3,
                _ => 5
            };
            if (shopIsland < firstIsland || shopIsland > state.unlockedIsland || shopIsland > 4)
                return Fail("此区域尚未出售该配件。", out reason);
            int cost = definition.AttachmentPrice(attachment);
            if (cost <= 0 || attachment == HowToFishAttachment.ExtendedMag && definition.ExtendedMagazineSize <= definition.MagazineSize)
                return Fail("当前枪械不支持此配件。", out reason);
            if (owned.HasAttachment(attachment)) return Fail("当前枪械已安装此配件。", out reason);
            if (attachment == HowToFishAttachment.Compensator && owned.barrel == HowToFishAttachment.Suppressor)
                return Fail("已安装消音器，不能降为补偿器。", out reason);
            if (state.money < cost) return Fail("购买配件的费用不足。", out reason);
            reason = null;
            return true;
        }

        /// <summary>为单件枪械购买配件，同类别替换，其它武器保持原状。</summary>
        /// <param name="id">已持有的枪械 ID。</param>
        /// <param name="attachment">拟安装的配件。</param>
        /// <param name="shopIsland">实际交互的商店区域。</param>
        /// <param name="reason">购买失败的原因。</param>
        public bool TryBuyAttachment(string id, HowToFishAttachment attachment, int shopIsland, out string reason)
        {
            if (!CanBuyAttachment(id, attachment, shopIsland, out reason)) return false;
            var owned = state.inventory.Find(item => item.id == id);
            state.money -= catalog.FindItem(id).AttachmentPrice(attachment);
            switch (attachment)
            {
                case HowToFishAttachment.RedDotSight: case HowToFishAttachment.SniperScope: owned.sight = attachment; break;
                case HowToFishAttachment.Compensator: case HowToFishAttachment.Suppressor: owned.barrel = attachment; break;
                case HowToFishAttachment.LaserSight: owned.hasLaser = true; break;
                case HowToFishAttachment.ExtendedMag: owned.hasExtendedMag = true; break;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>消耗一件背包物品。</summary>
        /// <param name="id">物品 ID。</param>
        public bool TryConsume(string id)
        {
            var item = state.inventory.Find(entry => entry.id == id);
            if (item == null) return false;
            if (--item.count == 0)
            {
                state.inventory.Remove(item);
                int slot = state.equipmentSlots.IndexOf(id);
                if (slot >= 0) state.equipmentSlots[slot] = "";
                if (state.equippedItemId == id) state.equippedItemId = null;
            }
            Changed?.Invoke();
            return true;
        }

        /// <summary>领取有效的任务或拾取物品。</summary>
        /// <param name="id">目录中的物品 ID。</param>
        /// <param name="count">正数数量。</param>
        public void GrantItem(string id, int count = 1)
        {
            if (catalog.FindItem(id) == null || count <= 0 || count > 100000 - Count(id))
                throw new ArgumentException("奖励物品或数量无效。");
            if (catalog.FindItem(id).IsEquipment && Count(id) == 0 && UnstoredEquipment != null)
                throw new InvalidOperationException("请先收纳或放下手中未收纳的装备。");
            AddItem(id, count);
            Changed?.Invoke();
        }

        /// <summary>拾回一件完整装备，保留升级、配件和余弹。</summary>
        /// <param name="equipment">待转入背包的独立快照。</param>
        public void GrantEquipment(HowToFishOwnedItem equipment)
        {
            if (equipment == null || equipment.count != 1 || !HasCompatibleEquipmentState(equipment) || Count(equipment.id) != 0)
                throw new ArgumentException("待拾取装备状态无效或已经拥有。");
            if (UnstoredEquipment != null) throw new InvalidOperationException("请先收纳或放下手中未收纳的装备。");
            AddEquipment(equipment.Copy());
            Changed?.Invoke();
        }

        internal bool HasCompatibleEquipmentState(HowToFishOwnedItem equipment)
        {
            if (equipment == null || !equipment.IsValid) return false;
            var definition = catalog.FindItem(equipment.id);
            if (definition?.IsEquipment != true) return false;
            for (int i = 1; i <= 6; i++)
                if (equipment.HasAttachment((HowToFishAttachment)i) &&
                    (definition.Kind != HowToFishItemKind.Gun || definition.AttachmentPrice((HowToFishAttachment)i) <= 0)) return false;
            return !equipment.hasExtendedMag || definition.ExtendedMagazineSize > definition.MagazineSize;
        }

        /// <summary>把手中未收纳的装备放入指定空栏位。</summary>
        /// <param name="id">已持有装备 ID。</param>
        /// <param name="slot">目标栏位索引。</param>
        public bool TryStoreEquipment(string id, int slot)
        {
            if (slot < 0 || slot >= EquipmentCapacity || !string.IsNullOrEmpty(state.equipmentSlots[slot]) ||
                Count(id) == 0 || catalog.FindItem(id)?.IsEquipment != true || state.equipmentSlots.Contains(id)) return false;
            state.equipmentSlots[slot] = id; Changed?.Invoke(); return true;
        }

        /// <summary>按顺序购买一个新装备栏位。</summary>
        /// <param name="shopIsland">实际商店区域，从森林开始出售。</param>
        /// <param name="reason">失败原因。</param>
        public bool TryExpandInventory(int shopIsland, out string reason)
        {
            if (shopIsland < 1 || shopIsland > state.unlockedIsland || shopIsland > 4) return Fail("此区域尚未提供背包扩容。", out reason);
            int cost = NextSlotCost;
            if (cost == 0) return Fail("装备栏已扩至上限。", out reason);
            if (state.money < cost) return Fail("背包扩容费用不足。", out reason);
            state.money -= cost; state.equipmentSlots.Add(""); reason = null; Changed?.Invoke(); return true;
        }

        /// <summary>将已处理鱼获转换为金钱；物理物品的单次消费由调用方保证。</summary>
        /// <param name="creatureId">生物 ID。</param>
        /// <param name="cooking">受热程度，0为生、0.5为熟成峰值、1为完全烧焦。</param>
        /// <param name="drip">是否珍稀变体。</param>
        /// <param name="styleMultiplier">本次击杀的奖励乘积，至少为1。</param>
        public int SellCatch(string creatureId, float cooking, bool drip, float styleMultiplier = 1)
        {
            int amount = CatchValue(creatureId, cooking, drip, styleMultiplier);
            if (amount > int.MaxValue - state.money) throw new InvalidOperationException("金钱已达到上限。");
            state.money += (int)amount;
            Changed?.Invoke();
            return (int)amount;
        }

        /// <summary>计算展示与出售共用的鱼获价值，不改动余额。</summary>
        /// <param name="creatureId">生物定义。</param>
        /// <param name="cooking">0至1的受热程度。</param>
        /// <param name="drip">是否为珍稀变体。</param>
        /// <param name="styleMultiplier">击杀奖励倍率。</param>
        public int CatchValue(string creatureId, float cooking, bool drip, float styleMultiplier = 1)
        {
            var creature = catalog.FindCreature(creatureId) ?? throw new ArgumentException("生物不存在。", nameof(creatureId));
            if (!(styleMultiplier >= 1) || float.IsInfinity(styleMultiplier)) throw new ArgumentOutOfRangeException(nameof(styleMultiplier));
            double amount = Math.Round((double)creature.Value * styleMultiplier * (drip ? 3 : 1) * CookingMultiplier(cooking));
            if (amount > int.MaxValue) throw new InvalidOperationException("鱼获价值超出上限。");
            return (int)amount;
        }

        /// <summary>烹饪价值曲线：中间使用平滑插值，端点遵循已核实的熟成和焦化倍率。</summary>
        /// <param name="cooking">0至1的受热程度。</param>
        public static float CookingMultiplier(float cooking)
        {
            if (!(cooking >= 0 && cooking <= 1)) throw new ArgumentOutOfRangeException(nameof(cooking));
            float t = cooking <= .5f ? cooking * 2 : (cooking - .5f) * 2;
            t = t * t * (3 - 2 * t);
            return cooking <= .5f ? 1 + .5f * t : 1.5f + (.1f - 1.5f) * t;
        }

        /// <summary>登记发现与击杀记录，重复登记不重复追加。</summary>
        /// <param name="creatureId">生物 ID。</param>
        /// <param name="defeated">是否已击杀。</param>
        /// <param name="drip">是否为珍稀变体。</param>
        public void RegisterCreature(string creatureId, bool defeated, bool drip)
        {
            var creature = catalog.FindCreature(creatureId) ?? throw new ArgumentException("生物不存在。", nameof(creatureId));
            if (!creature.IsJournalEntry) return;
            if (!state.discoveredCreatures.Contains(creatureId)) state.discoveredCreatures.Add(creatureId);
            if (defeated && !state.defeatedCreatures.Contains(creatureId)) state.defeatedCreatures.Add(creatureId);
            if (defeated && (drip || creature.IsBoss) && !state.defeatedDripCreatures.Contains(creatureId)) state.defeatedDripCreatures.Add(creatureId);
            Changed?.Invoke();
        }

        private void AddItem(string id, int count)
        {
            var owned = state.inventory.Find(item => item.id == id);
            if (owned == null)
            {
                var added = new HowToFishOwnedItem { id = id, count = count };
                if (catalog.FindItem(id).IsEquipment) AddEquipment(added);
                else state.inventory.Add(added);
            }
            else owned.count += count;
        }

        private void AddEquipment(HowToFishOwnedItem equipment)
        {
            state.inventory.Add(equipment);
            int empty = state.equipmentSlots.FindIndex(string.IsNullOrEmpty);
            if (empty >= 0) state.equipmentSlots[empty] = equipment.id;
            if (empty < 0 || state.equippedItemId == null)
            {
                state.equippedItemId = equipment.id;
                if (empty >= 0) state.selectedEquipmentSlot = empty;
            }
        }

        private static bool Fail(string message, out string reason)
        {
            reason = message;
            return false;
        }
    }
}
