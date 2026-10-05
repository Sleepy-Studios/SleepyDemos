using System;
using Core.Runtime;
using Hotfix.HowToFish;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class HowToFishSessionTests
    {
        private HowToFishCatalog catalog;
        [Test]
        public void FluxCommandsKeepSynchronousResultsAndRejectClearedSession()
        {
            var data = GlobalData.Add(new HowToFishData(null));
            var first = new HowToFishSession(catalog, new HowToFishSaveData());
            data.Session = first;
            first.GrantItem("FreeLure", 2);
            try
            {
                var consume = new HowToFishTryConsumeAction(first, "FreeLure");
                bool resultDuringRefresh = false;
                GlobalData.Subscribe<HowToFishData>(_ => resultDuringRefresh = consume.Result, false);
                GlobalData.Dispatch(consume);
                Assert.That(consume.Result, Is.True);
                Assert.That(resultDuringRefresh, Is.True);
                Assert.That(first.Count("FreeLure"), Is.EqualTo(1));
                int version = data.Version;
                data.ClearData();
                Assert.That(data.Session, Is.Null);
                Assert.That(data.Version, Is.GreaterThan(version));
                Assert.That(data.IsPaused, Is.True);
                var next = new HowToFishSession(catalog, new HowToFishSaveData());
                next.GrantItem("FreeLure", 2);
                data.Session = next;
                var stale = new HowToFishTryConsumeAction(first, "FreeLure");
                GlobalData.Dispatch(stale);
                Assert.That(stale.Result, Is.False);
                Assert.That(first.Count("FreeLure"), Is.EqualTo(1));
                Assert.That(next.Count("FreeLure"), Is.EqualTo(2));
                var current = new HowToFishTryConsumeAction(next, "FreeLure");
                GlobalData.Dispatch(current);
                Assert.That(current.Result, Is.True);
                Assert.That(next.Count("FreeLure"), Is.EqualTo(1));
            }
            finally
            {
                data.Handler.Dispose();
                GlobalData.Remove<HowToFishData>();
            }
        }

        [Test]
        public void Outfits_DefaultsAreSelectableAndRewardsAreIdempotent()
        {
            var session = new HowToFishSession(catalog, new HowToFishSaveData());
            Assert.That(HowToFishOutfitCatalog.All.Count, Is.EqualTo(18));
            foreach (string id in new[]
            {
                "Badman",
                "Bikini",
                "Fisherman",
                "Sailor"
            }

            )
            {
                Assert.That(HowToFishOutfitCatalog.IsUnlocked(id, session.State.unlockedOutfits), Is.True);
                Assert.That(session.UnlockOutfit(id), Is.False);
            }

            Assert.That(session.State.unlockedOutfits, Is.Empty);
            Assert.That(HowToFishOutfitCatalog.IsUnlocked("Andrei", session.State.unlockedOutfits), Is.False);
            Assert.That(session.UnlockOutfit("Andrei"), Is.True);
            Assert.That(session.UnlockOutfit("Andrei"), Is.False);
            Assert.That(session.State.unlockedOutfits, Is.EqualTo(new[] { "Andrei" }));
            Assert.That(HowToFishOutfitCatalog.IsUnlocked("Unknown", new[] { "Unknown" }), Is.False);
            Assert.Throws<ArgumentException>(() => session.UnlockOutfit("Unknown"));
        }

        [Test]
        public void Outfits_ReconcilePersistentProgressWithoutInventingInstantAchievements()
        {
            var state = new HowToFishSaveData
            {
                unlockedIsland = 4,
                hasBoatKey = true,
                boatMotorTier = 2,
                hasGrill = true,
                hasMilitaryBoatKey = true,
                hasFinished = true,
                playedSeconds = 20
            };
            state.defeatedCreatures.AddRange(new[] { "SpiderCrab", "GiantPiranha", "Pufferfish", "Albatross", "MutatedBowheadWhale" });
            state.unlockedSkins.Add("Pistol/Gold");
            state.worldItems.Add(new HowToFishWorldItemData { instanceId = "OldCatch", definitionId = "Shrimp", cooking = 1, bettingMultiplier = 35 });
            var session = new HowToFishSession(catalog, state);
            var expected = new[]
            {
                "LighthouseKeeper",
                "SwampLady",
                "Tourist",
                "ScaredGuyInShorts",
                "Military",
                "SwampMan",
                "StoreGrandma",
                "GrillMaster",
                "Scientist"
            };
            Assert.That(state.unlockedOutfits, Is.EquivalentTo(expected));
            state.tracksPausedPlaytime = true;
            session.ReconcileOutfits();
            Assert.That(state.unlockedOutfits, Is.EquivalentTo(expected), "历史皮肤、鱼获或短时结局都不能凭空补瞬时成就。");
        }

        [Test]
        public void Outfits_MotorAndCompleteGunOperationsGrantOnlyMatchingMilestones()
        {
            JsonUtility.FromJsonOverwrite("{\"upgradeCosts\":[10],\"upgradedDamage\":[30],\"extendedMagazineSize\":17,\"attachmentPrices\":[310,940,400,2500,100,90]}", catalog.FindItem("Pistol"));
            JsonUtility.FromJsonOverwrite("{\"attachmentPrices\":[280,650,220,1800,140,0]}", catalog.FindItem("Shotgun"));
            var session = new HowToFishSession(catalog, new HowToFishSaveData { money = 10000, unlockedIsland = 3, hasBoatKey = true });
            Assert.That(session.TryBuyMotor(2, 3, out _), Is.True);
            Assert.That(session.State.unlockedOutfits, Is.EquivalentTo(new[] { "SwampMan", "StoreGrandma" }));
            session.GrantItem("Pistol");
            session.GrantItem("Shotgun");
            var pistol = session.State.inventory.Find(item => item.id == "Pistol");
            pistol.sight = HowToFishAttachment.RedDotSight;
            pistol.barrel = HowToFishAttachment.Compensator;
            pistol.hasExtendedMag = true;
            session.State.inventory.Find(item => item.id == "Shotgun").hasLaser = true;
            session.ReconcileOutfits();
            Assert.That(session.State.unlockedOutfits, Does.Not.Contain("GunstoreClerc"), "配件不能跨枪拼接。");
            Assert.That(session.TryBuyAttachment("Pistol", HowToFishAttachment.LaserSight, 1, out _), Is.True);
            Assert.That(session.State.unlockedOutfits, Does.Not.Contain("GunstoreClerc"), "仍缺少该枪的伤害升级。");
            Assert.That(session.TryUpgrade("Pistol", 1, out _), Is.True);
            Assert.That(session.State.unlockedOutfits, Does.Contain("GunstoreClerc"));
            var complete = pistol.Copy();
            Assert.That(session.TryConsume("Pistol"), Is.True);
            session.ReconcileOutfits();
            Assert.That(session.State.unlockedOutfits, Does.Contain("GunstoreClerc"), "放下装备不能撤销已解锁服装。");
            var pickup = new HowToFishSession(catalog, new HowToFishSaveData());
            pickup.GrantEquipment(complete);
            Assert.That(pickup.State.unlockedOutfits, Does.Contain("GunstoreClerc"));
        }

        [Test]
        public void Outfits_BossDiscoveryDoesNotGrantDeathReward()
        {
            JsonUtility.FromJsonOverwrite("{\"id\":\"SpiderCrab\"}", catalog.FindCreature("Shrimp"));
            var session = new HowToFishSession(catalog, new HowToFishSaveData());
            session.RegisterCreature("SpiderCrab", false, false);
            Assert.That(session.State.unlockedOutfits, Is.Empty);
            session.RegisterCreature("SpiderCrab", true, false);
            Assert.That(session.State.unlockedOutfits, Is.EqualTo(new[] { "LighthouseKeeper" }));
        }

        [Test]
        public void IndividualWeight_ValueCombinesFactorsAndRejectsInvalidWeight()
        {
            var state = new HowToFishSaveData();
            var session = new HowToFishSession(catalog, state);
            Assert.That(session.CatchValue("Shrimp", .5f, true, 2, 35, .8f), Is.EqualTo(1260));
            Assert.That(session.CatchValue("Shrimp", .5f, true, 2, 35), Is.EqualTo(1575), "旧调用默认重量倍率为1。");
            Assert.That(session.SellCatch("Shrimp", 0, false, 1, 1, 1.2f), Is.EqualTo(6));
            foreach (float invalid in new[]
            {
                0,
                -1,
                float.NaN,
                float.PositiveInfinity
            }

            )
                Assert.Throws<ArgumentOutOfRangeException>(() => session.SellCatch("Shrimp", 0, false, 1, 1, invalid));
            Assert.Throws<InvalidOperationException>(() => session.SellCatch("Shrimp", 0, false, 1, 1, float.MaxValue));
            Assert.That(state.money, Is.EqualTo(6));
        }

        [Test]
        public void RouletteValue_MultipliesExistingFactorsAndRejectsOverflowWithoutPaying()
        {
            var state = new HowToFishSaveData
            {
                money = 10
            };
            var session = new HowToFishSession(catalog, state);
            Assert.That(session.CatchValue("Shrimp", 0, false, 1, 2), Is.EqualTo(10));
            Assert.That(session.CatchValue("Shrimp", 0, false, 1, 35), Is.EqualTo(175));
            Assert.That(session.CatchValue("Shrimp", .5f, true, 2, 35 * 2), Is.EqualTo(3150));
            Assert.That(state.money, Is.EqualTo(10), "轮盘倍率估价不能直接发现金。");
            Assert.That(session.SellCatch("Shrimp", .5f, true, 2, 35 * 2), Is.EqualTo(3150));
            Assert.That(state.money, Is.EqualTo(3160));
            foreach (float invalid in new[]
            {
                0,
                -1,
                float.NaN,
                float.PositiveInfinity
            }

            )
                Assert.Throws<ArgumentOutOfRangeException>(() => session.SellCatch("Shrimp", 0, false, 1, invalid));
            Assert.Throws<InvalidOperationException>(() => session.SellCatch("Shrimp", 0, false, 1, float.MaxValue));
            Assert.That(state.money, Is.EqualTo(3160), "非法值或溢出失败不能改变余额。");
            int red = 0, black = 0, green = 0;
            for (int pocket = 0; pocket < 37; pocket++)
                switch (HowToFishRoulette.ColorForPocket(pocket))
                {
                    case HowToFishRouletteColor.Red:
                        red++;
                        break;
                    case HowToFishRouletteColor.Black:
                        black++;
                        break;
                    case HowToFishRouletteColor.Green:
                        green++;
                        break;
                }

            Assert.That(new[] { red, black, green }, Is.EqualTo(new[] { 18, 18, 1 }));
            Assert.That(HowToFishRoulette.ColorForPocket(0), Is.EqualTo(HowToFishRouletteColor.Green));
            Assert.Throws<ArgumentOutOfRangeException>(() => HowToFishRoulette.ColorForPocket(37));
        }

        [SetUp]
        public void SetUp()
        {
            catalog = ScriptableObject.CreateInstance<HowToFishCatalog>();
            JsonUtility.FromJsonOverwrite("{\"items\":[" + "{\"id\":\"FreeLure\",\"kind\":3,\"price\":0,\"isConsumable\":false,\"reloadSeconds\":1}," + "{\"id\":\"CrabRod\",\"kind\":0,\"price\":3,\"reloadSeconds\":1}," + "{\"id\":\"FishingRod\",\"kind\":0,\"price\":25,\"island\":1,\"reloadSeconds\":1}," + "{\"id\":\"Knife\",\"kind\":1,\"price\":8,\"reloadSeconds\":1}," + "{\"id\":\"HotDog\",\"kind\":3,\"price\":1,\"isConsumable\":true,\"reloadSeconds\":1}," + "{\"id\":\"Shotgun\",\"kind\":2,\"price\":150,\"island\":1,\"reloadSeconds\":1,\"magazineSize\":2,\"pellets\":3}," + "{\"id\":\"Pistol\",\"kind\":2,\"price\":50,\"damage\":25,\"island\":1,\"reloadSeconds\":1,\"magazineSize\":10,\"pellets\":1}" + "],\"creatures\":[" + "{\"id\":\"Shrimp\",\"island\":0,\"requiredRodId\":\"CrabRod\",\"baits\":[\"FreeLure\"],\"weight\":1,\"value\":5,\"health\":10,\"length\":0.2}," + "{\"id\":\"BrownCrab\",\"island\":0,\"requiredRodId\":\"CrabRod\",\"baits\":[\"FreeLure\"],\"weight\":1,\"value\":3,\"health\":10,\"length\":0.3}," + "{\"id\":\"Lobster\",\"island\":0,\"requiredRodId\":\"CrabRod\",\"baits\":[\"HotDog\"],\"weight\":1,\"value\":9,\"health\":20,\"length\":0.4}," + "{\"id\":\"Mackerel\",\"island\":1,\"requiredRodId\":\"FishingRod\",\"baits\":[\"FreeLure\"],\"weight\":1,\"value\":6,\"health\":12,\"length\":0.8}" + "]}", catalog);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(catalog);
        [Test]
        public void Skins_CycleOnlyMatchingUnlocksAndPreserveDuplicateRewards()
        {
            var session = new HowToFishSession(catalog, new HowToFishSaveData());
            session.GrantItem("Knife");
            Assert.That(session.UnlockSkin("Knife/Chess"), Is.True);
            Assert.That(session.UnlockSkin("Knife/Chess"), Is.False);
            Assert.That(session.UnlockSkin("Boat/Gold"), Is.True);
            Assert.That(session.NextUnlockedSkin("Knife", null), Is.EqualTo("Knife/Chess"));
            Assert.That(session.NextUnlockedSkin("Knife", "Knife/Chess"), Is.Null.Or.Empty);
            Assert.That(session.ChangeEquipmentSkin("Knife"), Is.True);
            Assert.That(session.State.inventory.Find(item => item.id == "Knife").skinId, Is.EqualTo("Knife/Chess"));
            Assert.That(session.NextUnlockedSkin("Pistol", "Pistol/Gold"), Is.Null.Or.Empty, "实例带来的未解锁皮肤不能加入可选序列。");
            Assert.Throws<ArgumentException>(() => session.UnlockSkin("Boat/Fire"));
            Assert.That(session.State.money, Is.Zero);
            foreach (var rarity in new[]
            {
                HowToFishSkinRarity.Common,
                HowToFishSkinRarity.Rare,
                HowToFishSkinRarity.Legendary
            }

            )
                for (int island = 0; island < 5; island++)
                {
                    var pool = HowToFishSkinCatalog.Rewards(island, rarity);
                    Assert.That(pool, Is.Not.Empty);
                    foreach (var skin in pool)
                    {
                        Assert.That(skin.Rarity, Is.EqualTo(rarity));
                        if (island == 0)
                            Assert.That(skin.ItemId, Is.EqualTo("Knife").Or.EqualTo("BrassKnuckles"));
                        if (island == 4)
                            Assert.That(skin.ItemId, Is.EqualTo("FishingRod").Or.EqualTo("AssaultRifle").Or.EqualTo("SniperRifle").Or.EqualTo("Boat"));
                    }
                }

            Assert.That(HowToFishSkinCatalog.Find("Pistol/Galaxy").Effect, Is.EqualTo(HowToFishSkinEffect.Standard));
            Assert.That(HowToFishSkinCatalog.Find("FishingRod/Galaxy").Effect, Is.EqualTo(HowToFishSkinEffect.Rainbow));
        }

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
            foreach (float invalid in new[]
            {
                .99f,
                float.NaN,
                float.PositiveInfinity
            }

            )
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => session.CatchValue("Shrimp", 0, false, invalid));
                var state = new HowToFishSaveData();
                state.worldItems.Add(new HowToFishWorldItemData { instanceId = "InvalidScore", definitionId = "Shrimp", styleMultiplier = invalid });
                Assert.Throws<FormatException>(() => state.Validate());
            }

            var legacy = JsonUtility.FromJson<HowToFishWorldItemData>("{\"styleMultiplier\":2}");
            Assert.That(legacy.styleMultiplier, Is.EqualTo(2), "旧档整数倍率必须仍可读取。");
            Assert.That(session.State.money, Is.Zero, "查看售价不应改变金钱。");
            foreach (float invalid in new[]
            {
                -.1f,
                1.1f,
                float.NaN,
                float.PositiveInfinity
            }

            )
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
            var state = new HowToFishSaveData
            {
                money = 1200,
                unlockedIsland = 4
            };
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
            var state = new HowToFishSaveData
            {
                money = 10
            };
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
            foreach (string id in new[]
            {
                "CrabRod",
                "Knife",
                "Shotgun"
            }

            )
                session.GrantItem(id);
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
            snapshot.ammo = 7;
            snapshot.upgrade = 3;
            snapshot.sight = HowToFishAttachment.SniperScope;
            snapshot.barrel = HowToFishAttachment.Suppressor;
            snapshot.hasLaser = snapshot.hasExtendedMag = true;
            Assert.That(session.TryConsume("Pistol"), Is.True);
            Assert.That(session.State.equipmentSlots[3], Is.Empty);
            session.GrantEquipment(snapshot);
            snapshot.ammo = 0;
            var restored = session.State.inventory.Find(item => item.id == "Pistol");
            Assert.That(restored.ammo, Is.EqualTo(7));
            Assert.That(restored.upgrade, Is.EqualTo(3));
            Assert.That(restored.barrel, Is.EqualTo(HowToFishAttachment.Suppressor));
            Assert.That(restored.hasLaser && restored.hasExtendedMag, Is.True);
            foreach (int cost in new[]
            {
                10,
                25,
                50,
                100
            }

            )
            {
                money = session.State.money;
                Assert.That(session.NextSlotCost, Is.EqualTo(cost));
                Assert.That(session.TryExpandInventory(1, out _), Is.True);
                Assert.That(session.State.money, Is.EqualTo(money - cost));
            }

            Assert.That(session.EquipmentCapacity, Is.EqualTo(8));
            Assert.That(session.TryExpandInventory(4, out _), Is.False);
            session.TryConsume("Shotgun");
            Assert.Throws<ArgumentException>(() => session.GrantEquipment(new HowToFishOwnedItem { id = "Shotgun", count = 1, hasExtendedMag = true }));
            Assert.That(session.Count("Shotgun"), Is.Zero);
            var definitions = new string[9];
            var legacy = new HowToFishSaveData();
            for (int i = 0; i < 9; i++)
            {
                definitions[i] = "{\"id\":\"Gear" + i + "\",\"kind\":0,\"reloadSeconds\":1}";
                legacy.inventory.Add(new HowToFishOwnedItem { id = "Gear" + i, count = 1 });
            }

            JsonUtility.FromJsonOverwrite("{\"items\":[" + string.Join(",", definitions) + "],\"creatures\":[]}", catalog);
            var migrated = new HowToFishSession(catalog, legacy);
            Assert.That(migrated.EquipmentCapacity, Is.EqualTo(9), "旧原型无容量上限，迁移不得丢弃第九件装备。");
            Assert.That(migrated.UnstoredEquipment, Is.Null);
            Assert.That(migrated.NextSlotCost, Is.Zero);
            Assert.DoesNotThrow(legacy.Validate);
        }

        [Test]
        public void Attachments_ArePricedPerWeaponAndRejectDowngradeDuplicatesAndUnsupportedParts()
        {
            var pistol = catalog.FindItem("Pistol");
            JsonUtility.FromJsonOverwrite("{\"extendedMagazineSize\":17,\"attachmentPrices\":[310,940,400,2500,100,90]}", pistol);
            JsonUtility.FromJsonOverwrite("{\"attachmentPrices\":[280,650,220,1800,140,0]}", catalog.FindItem("Shotgun"));
            var session = new HowToFishSession(catalog, new HowToFishSaveData { money = 99, unlockedIsland = 3 });
            session.GrantItem("Pistol");
            session.GrantItem("Shotgun");
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
            var state = new HowToFishSaveData
            {
                money = 1000,
                unlockedIsland = 2
            };
            var session = new HowToFishSession(catalog, state);
            session.GrantItem("Knife");
            session.GrantItem("Shotgun");
            Assert.That(session.TryUpgrade("Shotgun", 0, out _), Is.False);
            Assert.That(session.TryUpgrade("Knife", 3, out _), Is.False);
            Assert.That(session.TryUpgrade("Knife", -1, out _), Is.False);
            Assert.That(state.money, Is.EqualTo(1000));
            for (int i = 0; i < 3; i++)
                Assert.That(session.TryUpgrade("Knife", 0, out _), Is.True);
            Assert.That(session.TryUpgrade("Knife", 0, out _), Is.False, "已解锁后岛不能提升首岛商店上限。");
            Assert.That(state.money, Is.EqualTo(922));
            Assert.That(knife.DamageAtLevel(session.UpgradeLevel("Knife")), Is.EqualTo(22));
            Assert.That(session.TryUpgrade("Knife", 1, out _), Is.True);
            Assert.That(session.TryUpgrade("Knife", 4, out _), Is.False);
            Assert.That(knife.NextUpgradeCost(4), Is.Zero);
            Assert.That(knife.DamageAtLevel(100), Is.EqualTo(24));
            for (int i = 0; i < 3; i++)
                Assert.That(session.TryUpgrade("Shotgun", 1, out _), Is.True);
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
            session.RegisterCreature("Mackerel", true, false);
            session.RegisterCreature("Mackerel", true, false);
            Assert.That(session.State.defeatedCreatures, Is.EquivalentTo(new[] { "Mackerel" }));
            Assert.That(session.State.defeatedDripCreatures, Is.EquivalentTo(new[] { "Mackerel" }));
            JsonUtility.FromJsonOverwrite("{\"isMiniBoss\":false}", miniBoss);
            Assert.That(catalog.RollCatch(4, "FreeLure", .95f, "FishingRod").Id, Is.EqualTo("Shrimp"), "主线首领仍受区域限制。");
            var previous = new HowToFishSaveData();
            previous.defeatedCreatures.Add("Mackerel");
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
