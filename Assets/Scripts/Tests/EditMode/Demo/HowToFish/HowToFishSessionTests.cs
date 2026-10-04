using System;
using Hotfix.HowToFish;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class HowToFishSessionTests
    {
        private HowToFishCatalog catalog;

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<HowToFishCatalog>();
            JsonUtility.FromJsonOverwrite("{\"items\":[" +
                "{\"id\":\"FreeLure\",\"kind\":3,\"price\":0,\"isConsumable\":false,\"reloadSeconds\":1}," +
                "{\"id\":\"CrabRod\",\"kind\":0,\"price\":3,\"reloadSeconds\":1}," +
                "{\"id\":\"FishingRod\",\"kind\":0,\"price\":25,\"island\":1,\"reloadSeconds\":1}," +
                "{\"id\":\"Knife\",\"kind\":1,\"price\":8,\"reloadSeconds\":1}," +
                "{\"id\":\"HotDog\",\"kind\":3,\"price\":1,\"isConsumable\":true,\"reloadSeconds\":1}," +
                "{\"id\":\"Shotgun\",\"kind\":2,\"price\":150,\"island\":1,\"reloadSeconds\":1,\"magazineSize\":2,\"pellets\":3}," +
                "{\"id\":\"Pistol\",\"kind\":2,\"price\":50,\"damage\":25,\"island\":1,\"reloadSeconds\":1,\"magazineSize\":10,\"pellets\":1}" +
                "],\"creatures\":[" +
                "{\"id\":\"Shrimp\",\"island\":0,\"requiredRodId\":\"CrabRod\",\"baits\":[\"FreeLure\"],\"weight\":1,\"value\":5,\"health\":10,\"length\":0.2}," +
                "{\"id\":\"BrownCrab\",\"island\":0,\"requiredRodId\":\"CrabRod\",\"baits\":[\"FreeLure\"],\"weight\":1,\"value\":3,\"health\":10,\"length\":0.3}," +
                "{\"id\":\"Lobster\",\"island\":0,\"requiredRodId\":\"CrabRod\",\"baits\":[\"HotDog\"],\"weight\":1,\"value\":9,\"health\":20,\"length\":0.4}," +
                "{\"id\":\"Mackerel\",\"island\":1,\"requiredRodId\":\"FishingRod\",\"baits\":[\"FreeLure\"],\"weight\":1,\"value\":6,\"health\":12,\"length\":0.8}" +
                "]}", catalog);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(catalog);

        [Test]
        public void CookingValue_PeaksBeforeBurningAndRejectsInvalidState()
        {
            var session = new HowToFishSession(catalog, new HowToFishSaveData());
            Assert.That(HowToFishSession.CookingMultiplier(0), Is.EqualTo(1));
            Assert.That(HowToFishSession.CookingMultiplier(.5f), Is.EqualTo(1.5f));
            Assert.That(HowToFishSession.CookingMultiplier(1), Is.EqualTo(.1f).Within(.00001f));
            Assert.That(session.CatchValue("Shrimp", .5f, false, 2), Is.EqualTo(15));
            Assert.That(session.CatchValue("Shrimp", 1, false, 2), Is.EqualTo(1));
            Assert.That(session.CatchValue("Shrimp", 0, false, 12.5f), Is.EqualTo(62));
            foreach (float invalid in new[] { .99f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => session.CatchValue("Shrimp", 0, false, invalid));
                var state = new HowToFishSaveData();
                state.worldItems.Add(new HowToFishWorldItemData { instanceId = "InvalidScore", definitionId = "Shrimp", styleMultiplier = invalid });
                Assert.Throws<FormatException>(() => state.Validate());
            }
            var legacy = JsonUtility.FromJson<HowToFishWorldItemData>("{\"styleMultiplier\":2}");
            Assert.That(legacy.styleMultiplier, Is.EqualTo(2), "旧档整数倍率必须仍可读取。");
            Assert.That(session.State.money, Is.Zero, "查看售价不应改变金钱。");
            foreach (float invalid in new[] { -.1f, 1.1f, float.NaN, float.PositiveInfinity })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => session.SellCatch("Shrimp", invalid, false));
                var state = new HowToFishSaveData();
                state.inventory.Add(new HowToFishOwnedItem { id = "Knife", count = 1, cooking = invalid });
                Assert.Throws<FormatException>(() => state.Validate());
            }
            Assert.That(session.State.money, Is.Zero);
        }

        [Test]
        public void BoatUpgrades_CheckRegionFundsAndTierWithoutConsumingEquipmentSlots()
        {
            var state = new HowToFishSaveData { money = 1200, unlockedIsland = 4 };
            var session = new HowToFishSession(catalog, state);
            Assert.That(session.TryBuyMotor(1, 1, out _), Is.False);
            state.hasBoatKey = true;
            Assert.That(session.TryBuyMotor(1, 0, out _), Is.False);
            Assert.That(session.TryBuyMotor(2, 2, out _), Is.False);
            Assert.That(state.money, Is.EqualTo(1200));
            Assert.That(session.TryBuyMotor(2, 3, out _), Is.True);
            Assert.That(state.boatMotorTier, Is.EqualTo(2));
            Assert.That(state.money, Is.EqualTo(340));
            Assert.That(session.TryBuyMotor(1, 1, out _), Is.False);
            Assert.That(session.TryBuyMotor(2, 4, out _), Is.False);
            Assert.That(session.TryBuyBoatRadar(1, out _), Is.False);
            Assert.That(session.TryBuyBoatRadar(2, out _), Is.True);
            Assert.That(session.TryBuyBoatRadar(3, out _), Is.False);
            Assert.That(state.money, Is.EqualTo(140));
            Assert.That(session.EquipmentCapacity, Is.EqualTo(3));
            Assert.That(state.inventory, Is.Empty);
            var restored = JsonUtility.FromJson<HowToFishSaveData>(JsonUtility.ToJson(state));
            restored.Validate();
            Assert.That(restored.boatMotorTier, Is.EqualTo(2));
            Assert.That(restored.hasBoatRadar, Is.True);
            restored.boatMotorTier = 3;
            Assert.Throws<FormatException>(() => restored.Validate());
            state.boatMotorTier = 0;
            Assert.That(session.TryBuyMotor(1, 1, out _), Is.False);
            state.money = 230;
            state.unlockedIsland = 0;
            Assert.That(session.TryBuyMotor(1, 1, out _), Is.False);
            state.unlockedIsland = 1;
            Assert.That(session.TryBuyMotor(1, 1, out _), Is.True);
            Assert.That(state.money, Is.Zero);
        }

        [Test]
        public void Purchase_IsAtomicForInsufficientMoneyDuplicatesAndLockedIsland()
        {
            var state = new HowToFishSaveData { money = 10 };
            var session = new HowToFishSession(catalog, state);
            Assert.That(session.TryBuy("Knife", 0, out _), Is.True);
            Assert.That(session.TryBuy("Knife", 0, out _), Is.False);
            Assert.That(session.TryBuy("Shotgun", 0, out _), Is.False);
            Assert.That(session.TryBuy("Knife", 1, out _), Is.False);
            Assert.That(state.money, Is.EqualTo(2));
            Assert.That(session.Count("Knife"), Is.EqualTo(1));
            Assert.That(session.TryBuy("HotDog", 0, out _), Is.True);
            Assert.That(session.TryBuy("HotDog", 0, out _), Is.True);
            Assert.That(session.TryBuy("HotDog", 0, out _), Is.False);
            Assert.That(session.Count("HotDog"), Is.EqualTo(2));
        }

        [Test]
        public void EquipmentSlots_ExpandPreserveDroppedStateAndMigrateLegacyGearWithoutLoss()
        {
            JsonUtility.FromJsonOverwrite("{\"extendedMagazineSize\":17,\"attachmentPrices\":[310,940,400,2500,100,90]}", catalog.FindItem("Pistol"));
            var session = new HowToFishSession(catalog, new HowToFishSaveData { money = 1000, unlockedIsland = 4 });
            Assert.That(session.EquipmentCapacity, Is.EqualTo(3));
            foreach (string id in new[] { "CrabRod", "Knife", "Shotgun" }) session.GrantItem(id);
            Assert.That(session.TryBuy("Pistol", 1, out _), Is.True);
            Assert.That(session.UnstoredEquipment.id, Is.EqualTo("Pistol"));
            Assert.That(session.State.equippedItemId, Is.EqualTo("Pistol"));
            int money = session.State.money;
            Assert.That(session.TryBuy("FishingRod", 1, out _), Is.False);
            Assert.That(session.TryExpandInventory(0, out _), Is.False);
            Assert.That(session.State.money, Is.EqualTo(money));
            Assert.That(session.TryExpandInventory(1, out _), Is.True);
            Assert.That(session.State.money, Is.EqualTo(money - 5));
            Assert.That(session.TryStoreEquipment("Pistol", 3), Is.True);
            Assert.That(session.UnstoredEquipment, Is.Null);
            Assert.That(session.TryStoreEquipment("Shotgun", 3), Is.False);
            var snapshot = session.State.inventory.Find(item => item.id == "Pistol").Copy();
            snapshot.ammo = 7; snapshot.upgrade = 3; snapshot.sight = HowToFishAttachment.SniperScope;
            snapshot.barrel = HowToFishAttachment.Suppressor; snapshot.hasLaser = snapshot.hasExtendedMag = true;
            Assert.That(session.TryConsume("Pistol"), Is.True);
            Assert.That(session.State.equipmentSlots[3], Is.Empty);
            session.GrantEquipment(snapshot);
            snapshot.ammo = 0;
            var restored = session.State.inventory.Find(item => item.id == "Pistol");
            Assert.That(restored.ammo, Is.EqualTo(7)); Assert.That(restored.upgrade, Is.EqualTo(3));
            Assert.That(restored.barrel, Is.EqualTo(HowToFishAttachment.Suppressor));
            Assert.That(restored.hasLaser && restored.hasExtendedMag, Is.True);
            foreach (int cost in new[] {10,25,50,100})
            {
                money = session.State.money; Assert.That(session.NextSlotCost, Is.EqualTo(cost));
                Assert.That(session.TryExpandInventory(1, out _), Is.True); Assert.That(session.State.money, Is.EqualTo(money - cost));
            }
            Assert.That(session.EquipmentCapacity, Is.EqualTo(8)); Assert.That(session.TryExpandInventory(4, out _), Is.False);
            session.TryConsume("Shotgun");
            Assert.Throws<ArgumentException>(() => session.GrantEquipment(new HowToFishOwnedItem { id = "Shotgun", count = 1, hasExtendedMag = true }));
            Assert.That(session.Count("Shotgun"), Is.Zero);
            var definitions = new string[9]; var legacy = new HowToFishSaveData();
            for (int i = 0; i < 9; i++)
            {
                definitions[i] = "{\"id\":\"Gear" + i + "\",\"kind\":0,\"reloadSeconds\":1}";
                legacy.inventory.Add(new HowToFishOwnedItem { id = "Gear" + i, count = 1 });
            }
            JsonUtility.FromJsonOverwrite("{\"items\":[" + string.Join(",", definitions) + "],\"creatures\":[]}", catalog);
            var migrated = new HowToFishSession(catalog, legacy);
            Assert.That(migrated.EquipmentCapacity, Is.EqualTo(9), "旧原型无容量上限，迁移不得丢弃第九件装备。");
            Assert.That(migrated.UnstoredEquipment, Is.Null); Assert.That(migrated.NextSlotCost, Is.Zero);
            Assert.DoesNotThrow(legacy.Validate);
        }

        [Test]
        public void Attachments_ArePricedPerWeaponAndRejectDowngradeDuplicatesAndUnsupportedParts()
        {
            var pistol = catalog.FindItem("Pistol");
            JsonUtility.FromJsonOverwrite("{\"extendedMagazineSize\":17,\"attachmentPrices\":[310,940,400,2500,100,90]}", pistol);
            JsonUtility.FromJsonOverwrite("{\"attachmentPrices\":[280,650,220,1800,140,0]}", catalog.FindItem("Shotgun"));
            var session = new HowToFishSession(catalog, new HowToFishSaveData { money = 99, unlockedIsland = 3 });
            session.GrantItem("Pistol"); session.GrantItem("Shotgun");
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.LaserSight, 1, out _), Is.False);
            Assert.That(session.State.money, Is.EqualTo(99));
            session.State.money = 10000;
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.LaserSight, 0, out _), Is.False);
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.LaserSight, 4, out _), Is.False);
            Assert.That(session.TryBuyAttachment("Pistol", (HowToFishAttachment)99, 3, out _), Is.False);
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.LaserSight, 1, out _), Is.True);
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.RedDotSight, 2, out _), Is.True);
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.SniperScope, 3, out _), Is.True);
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.Compensator, 2, out _), Is.True);
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.Suppressor, 3, out _), Is.True);
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.ExtendedMag, 2, out _), Is.True);
            int remaining = session.State.money;
            Assert.That(remaining, Is.EqualTo(5660));
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.Compensator, 3, out _), Is.False);
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.SniperScope, 3, out _), Is.False);
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.LaserSight, 3, out _), Is.False);
            Assert.That(session.TryBuyAttachment("Shotgun", HowToFishAttachment.ExtendedMag, 3, out _), Is.False);
            Assert.That(session.State.money, Is.EqualTo(remaining));
            Assert.That(session.State.inventory.Find(item => item.id == "Shotgun").barrel, Is.EqualTo(HowToFishAttachment.None));
            var owned = session.State.inventory.Find(item => item.id == "Pistol");
            Assert.That(owned.HasAttachment(HowToFishAttachment.SniperScope), Is.True);
            Assert.That(owned.HasAttachment(HowToFishAttachment.RedDotSight), Is.False);
            Assert.That(session.TryBuyAttachment("Shotgun", HowToFishAttachment.Suppressor, 3, out _), Is.True);
            Assert.That(session.State.money, Is.EqualTo(remaining - 1800));
            Assert.That(pistol.Damage, Is.EqualTo(25), "配件不改变武器伤害。");
            JsonUtility.FromJsonOverwrite("{\"extendedMagazineSize\":5}", pistol);
            Assert.Throws<InvalidOperationException>(catalog.Validate);
        }

        [Test]
        public void WeaponUpgrade_UsesNextPriceAndDamageAndActualShopLimit()
        {
            var knife = catalog.FindItem("Knife");
            var shotgun = catalog.FindItem("Shotgun");
            JsonUtility.FromJsonOverwrite("{\"damage\":16,\"upgradedDamage\":[18,20,22,24],\"upgradeCosts\":[14,28,36,84]}", knife);
            JsonUtility.FromJsonOverwrite("{\"damage\":3,\"upgradedDamage\":[4,5,6,7],\"upgradeCosts\":[20,30,40,200]}", shotgun);
            var state = new HowToFishSaveData { money = 1000, unlockedIsland = 2 };
            var session = new HowToFishSession(catalog, state);
            session.GrantItem("Knife"); session.GrantItem("Shotgun");
            Assert.That(session.TryUpgrade("Shotgun", 0, out _), Is.False);
            Assert.That(session.TryUpgrade("Knife", 3, out _), Is.False);
            Assert.That(session.TryUpgrade("Knife", -1, out _), Is.False);
            Assert.That(state.money, Is.EqualTo(1000));
            for (int i = 0; i < 3; i++) Assert.That(session.TryUpgrade("Knife", 0, out _), Is.True);
            Assert.That(session.TryUpgrade("Knife", 0, out _), Is.False, "已解锁后岛不能提升首岛商店上限。");
            Assert.That(state.money, Is.EqualTo(922));
            Assert.That(knife.DamageAtLevel(session.UpgradeLevel("Knife")), Is.EqualTo(22));
            Assert.That(session.TryUpgrade("Knife", 1, out _), Is.True);
            Assert.That(session.TryUpgrade("Knife", 4, out _), Is.False);
            Assert.That(knife.NextUpgradeCost(4), Is.Zero);
            Assert.That(knife.DamageAtLevel(100), Is.EqualTo(24));
            for (int i = 0; i < 3; i++) Assert.That(session.TryUpgrade("Shotgun", 1, out _), Is.True);
            Assert.That(session.TryUpgrade("Shotgun", 1, out _), Is.False);
            state.money = 199;
            Assert.That(session.TryUpgrade("Shotgun", 2, out _), Is.False);
            Assert.That(state.money, Is.EqualTo(199));
            Assert.That(session.UpgradeLevel("Shotgun"), Is.EqualTo(3));
            state.money = 200;
            Assert.That(session.TryUpgrade("Shotgun", 2, out _), Is.True);
            Assert.That(state.money, Is.Zero);
            Assert.That(shotgun.DamageAtLevel(4), Is.EqualTo(7));
            JsonUtility.FromJsonOverwrite("{\"upgradeCosts\":[20]}", shotgun);
            Assert.Throws<InvalidOperationException>(() => catalog.Validate());
        }

        [Test]
        public void FishPool_UsesRodDefaultsAndSharedPaidPools()
        {
            catalog.Validate();
            Assert.That(catalog.RollCatch(0, "FreeLure", 0, "CrabRod").Id, Is.EqualTo("Shrimp"));
            Assert.That(catalog.RollCatch(0, "FreeLure", 0.999f, "CrabRod").Id, Is.EqualTo("BrownCrab"));
            Assert.That(catalog.RollCatch(0, "HotDog", 0, "CrabRod").Id, Is.EqualTo("Lobster"));
            Assert.That(catalog.RollCatch(1, "FreeLure", 0, "CrabRod").Id, Is.EqualTo("Shrimp"));
            Assert.That(catalog.RollCatch(0, "FreeLure", 0, "FishingRod").Id, Is.EqualTo("Mackerel"));
            Assert.That(catalog.RollCatch(4, "HotDog", 0, "FishingRod").Id, Is.EqualTo("Lobster"));
            Assert.That(catalog.RollCatch(1, "FreeLure", 0, "FishingRod").Id, Is.EqualTo("Mackerel"));
            Assert.That(catalog.RollCatch(4, "FreeLure", 0, "FishingRod").Id, Is.EqualTo("Mackerel"));
            Assert.Throws<ArgumentOutOfRangeException>(() => catalog.RollCatch(0, "FreeLure", 1, "CrabRod"));
            Assert.Throws<ArgumentException>(() => catalog.RollCatch(1, "FreeLure", 0, "Knife"));
        }

        [Test]
        public void MiniBoss_UsesPerBaitWeightsAndRegistersBothJournalEntries()
        {
            var miniBoss = catalog.FindCreature("Mackerel");
            JsonUtility.FromJsonOverwrite("{\"isBoss\":true,\"isMiniBoss\":true,\"baits\":[\"FreeLure\",\"HotDog\"],\"baitWeights\":[1,9]}", miniBoss);
            JsonUtility.FromJsonOverwrite("{\"island\":1,\"requiredRodId\":\"FishingRod\",\"baits\":[\"FreeLure\",\"HotDog\"],\"weight\":9}", catalog.FindCreature("Shrimp"));
            catalog.Validate();
            Assert.That(catalog.RollCatch(4, "FreeLure", .85f, "FishingRod").Id, Is.EqualTo("Shrimp"));
            Assert.That(catalog.RollCatch(4, "FreeLure", .95f, "FishingRod").Id, Is.EqualTo("Mackerel"));
            Assert.That(catalog.RollCatch(4, "HotDog", .7f, "FishingRod").Id, Is.EqualTo("Mackerel"));
            Assert.That(catalog.RollCatch(0, "HotDog", .7f, "FishingRod").Id, Is.EqualTo("Mackerel"));
            var session = new HowToFishSession(catalog, new HowToFishSaveData());
            session.RegisterCreature("Mackerel", false, false);
            Assert.That(session.State.defeatedDripCreatures, Is.Empty);
            session.RegisterCreature("Mackerel", true, false); session.RegisterCreature("Mackerel", true, false);
            Assert.That(session.State.defeatedCreatures, Is.EquivalentTo(new[] { "Mackerel" }));
            Assert.That(session.State.defeatedDripCreatures, Is.EquivalentTo(new[] { "Mackerel" }));
            JsonUtility.FromJsonOverwrite("{\"isMiniBoss\":false}", miniBoss);
            Assert.That(catalog.RollCatch(4, "FreeLure", .95f, "FishingRod").Id, Is.EqualTo("Shrimp"), "主线首领仍受区域限制。");
            var previous = new HowToFishSaveData(); previous.defeatedCreatures.Add("Mackerel");
            Assert.That(new HowToFishSession(catalog, previous).State.defeatedDripCreatures, Does.Contain("Mackerel"), "旧存档的主线首领记录也应补齐。");
            JsonUtility.FromJsonOverwrite("{\"excludeFromJournal\":true}", catalog.FindCreature("Shrimp"));
            session.RegisterCreature("Shrimp", true, true);
            Assert.That(session.State.discoveredCreatures, Does.Not.Contain("Shrimp"));
            Assert.That(session.SellCatch("Shrimp", 0, false), Is.EqualTo(5), "不进入图鉴不等于禁止交易。");
            JsonUtility.FromJsonOverwrite("{\"baitWeights\":[0,9]}", miniBoss);
            Assert.Throws<InvalidOperationException>(() => catalog.Validate());
        }

        [Test]
        public void SaleAndDiscovery_KeepAmountsAndCollectionsConsistent()
        {
            var state = new HowToFishSaveData();
            var session = new HowToFishSession(catalog, state);
            Assert.That(session.SellCatch("Shrimp", .5f, false, 2), Is.EqualTo(15));
            session.RegisterCreature("Shrimp", true, true);
            session.RegisterCreature("Shrimp", true, true);
            Assert.That(state.money, Is.EqualTo(15));
            Assert.That(state.defeatedCreatures.Count, Is.EqualTo(1));
            Assert.That(state.defeatedDripCreatures.Count, Is.EqualTo(1));
        }
    }
}
