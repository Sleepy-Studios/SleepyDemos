using System;
using Core.Runtime;
using System.Collections.Generic;
using System.IO;
using Hotfix.HowToFish;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class HowToFishSaveTests
    {
        private string directory;
        private HowToFishSaveStore store;

        [Test]
        public void InputPreferences_RoundTripRejectsInvalidWritesAndPreservesCorruptRecord()
        {
            string key = "HowToFish.Tests.Preferences." + Guid.NewGuid().ToString("N");
            string warning = null;
            HowToFishLocalPreferences Load() => LocalDataManager.LoadData(key, new HowToFishLocalPreferences(), out warning, HowToFishLocalPreferences.Validate);
            void Save(HowToFishLocalPreferences value)
            {
                LocalDataManager.SaveData(key, value, HowToFishLocalPreferences.Validate);
                warning = null;
            }
            try
            {
                var defaults = Load();
                Assert.That(defaults.Bindings, Is.Empty, "默认绑定无需任何覆盖 JSON。");
                Assert.That(warning, Is.Null);
                Assert.That(PlayerPrefs.HasKey(key), Is.False, "读取缺失记录不能隐式写入默认值。");
                var saved = new HowToFishLocalPreferences
                {
                    MouseSensitivity = .25f, GamepadSensitivity = 180, DeadZone = .3f,
                    InvertY = true, Bindings = ""
                };
                Save(saved);
                string validRecord = PlayerPrefs.GetString(key);
                var restored = Load();
                Assert.That(restored.MouseSensitivity, Is.EqualTo(saved.MouseSensitivity));
                Assert.That(restored.GamepadSensitivity, Is.EqualTo(saved.GamepadSensitivity));
                Assert.That(restored.DeadZone, Is.EqualTo(saved.DeadZone));
                Assert.That(restored.InvertY, Is.True);
                Assert.That(restored.Bindings, Is.Empty);

                // 验证写入事务边界：几种真实坏输入都不能覆盖上一份合法记录。
                foreach (var invalid in new[]
                {
                    new HowToFishLocalPreferences { MouseSensitivity = float.NaN },
                    new HowToFishLocalPreferences { GamepadSensitivity = float.PositiveInfinity },
                    new HowToFishLocalPreferences { DeadZone = 2 },
                    new HowToFishLocalPreferences { Version = HowToFishLocalPreferences.CurrentVersion + 1 },
                    new HowToFishLocalPreferences { Bindings = "{broken" }
                })
                {
                    Assert.Throws<ArgumentException>(() => Save(invalid));
                    Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(validRecord), "拒绝无效设置后原记录被修改。");
                }

                var future = saved.Copy(); future.Version++;
                var brokenBindings = saved.Copy(); brokenBindings.Bindings = "{broken";
                foreach (string corrupt in new[] { "{broken", "{}", JsonUtility.ToJson(future), JsonUtility.ToJson(brokenBindings) })
                {
                    PlayerPrefs.SetString(key, corrupt);
                    restored = Load();
                    Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(defaults)), "损坏记录应退回完整默认设置。");
                    Assert.That(warning, Is.Not.Null.And.Not.Empty, "回退默认值必须向用户提供提示。");
                    Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(corrupt), "加载不能偷偷覆盖损坏或未来版本的原始记录。");
                }
                Save(saved);
                Assert.That(warning, Is.Null, "明确保存成功后应清除旧警告。");
                Assert.That(PlayerPrefs.GetString(key), Is.EqualTo(validRecord));
            }
            finally
            {
                PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "HowToFishTests", Guid.NewGuid().ToString("N"));
            store = new HowToFishSaveStore(directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void Outfits_GlobalSelectionChangesWithoutNewUnlocksAndSurvivesSlotMerges()
        {
            var first = new HowToFishSaveData { tracksPausedPlaytime = true };
            first.unlockedOutfits.Add("Andrei");
            first.worldItems.Add(new HowToFishWorldItemData { instanceId = "Remains", definitionId = "PlayerRemains", outfitId = "Andrei" });
            store.Save(0, first);
            var profile = store.LoadSkinProfile();
            Assert.That(profile.selectedOutfitId, Is.EqualTo("Fisherman"));
            profile.selectedOutfitId = "Andrei";
            store.SaveSkinProfile(profile);
            Assert.That(store.LoadSkinProfile().selectedOutfitId, Is.EqualTo("Andrei"), "解锁数量没变也必须保存换装。");
            var fresh = new HowToFishSaveData();
            Assert.That(store.LoadSharedSkins(fresh).selectedOutfitId, Is.EqualTo("Andrei"));
            fresh.unlockedOutfits.Add("Jacob");
            store.Save(1, fresh);
            var old = store.Load(0).Data;
            profile = store.LoadSharedSkins(old);
            Assert.That(old.unlockedOutfits, Is.EquivalentTo(new[] { "Andrei", "Jacob" }));
            Assert.That(old.tracksPausedPlaytime, Is.True);
            Assert.That(old.worldItems[0].outfitId, Is.EqualTo("Andrei"));
            profile.selectedOutfitId = "Sailor";
            store.SaveSkinProfile(profile);
            store.Save(0, old);
            Assert.That(store.LoadSkinProfile().selectedOutfitId, Is.EqualTo("Sailor"), "旧槽不能覆盖全局选择。");
            Assert.That(store.Load(0).Data.worldItems[0].outfitId, Is.EqualTo("Andrei"), "已有遗体保留死亡时服装。");
        }

        [Test]
        public void Outfits_InvalidSelectionsAreRejectedBeforeWriting()
        {
            var state = new HowToFishSaveData();
            store.Save(0, state);
            foreach (var invalid in new[] { new[] { "Unknown" }, new[] { "Fisherman" }, new[] { "Andrei", "Andrei" } })
                Assert.Throws<FormatException>(() => store.Save(0, new HowToFishSaveData { unlockedOutfits = new List<string>(invalid) }));
            foreach (string invalid in new[] { "Unknown", "Jacob" })
                Assert.Throws<FormatException>(() => store.SaveSkinProfile(new HowToFishSkinProfile { selectedOutfitId = invalid }));
            var remains = new HowToFishWorldItemData { instanceId = "Body", definitionId = "PlayerRemains", outfitId = "Bean" };
            state.worldItems.Add(remains);
            Assert.DoesNotThrow(state.Validate);
            remains.definitionId = "Shrimp";
            Assert.Throws<FormatException>(state.Validate);
            remains.definitionId = "PlayerRemains";
            remains.outfitId = "Unknown";
            Assert.Throws<FormatException>(state.Validate);
            Assert.That(store.Load(0).Data.worldItems, Is.Empty);
        }

        [Test]
        public void Outfits_ProfileWriteFailureKeepsSelectionAndSlotMirrorRecoversRewards()
        {
            var profile = new HowToFishSkinProfile { selectedOutfitId = "Sailor" };
            store.SaveSkinProfile(profile);
            string profilePath = Path.Combine(directory, "PlayerSkins.json");
            string before = File.ReadAllText(profilePath);
            profile.selectedOutfitId = "Bikini";
            var world = new HowToFishSaveData();
            world.unlockedOutfits.Add("Bean");
            using (var locked = new FileStream(profilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() => store.SaveSkinProfile(profile));
                Assert.That(File.ReadAllText(profilePath), Is.EqualTo(before));
                Assert.DoesNotThrow(() => store.Save(1, world));
                Assert.That(store.SkinProfileNotice, Does.Contain("待下次进入"));
                Assert.That(store.Load(1).Data.unlockedOutfits, Does.Contain("Bean"));
            }
            var fresh = new HowToFishSaveData();
            profile = store.LoadSharedSkins(fresh);
            Assert.That(profile.selectedOutfitId, Is.EqualTo("Sailor"));
            Assert.That(fresh.unlockedOutfits, Does.Contain("Bean"));
            File.WriteAllText(profilePath, "interrupted");
            Assert.That(store.LoadSkinProfile().selectedOutfitId, Is.EqualTo("Fisherman"));
            Assert.That(store.LoadSharedSkins(new HowToFishSaveData()).unlockedOutfits, Does.Contain("Bean"));
            var deleted = store.LoadSkinProfile();
            deleted.unlockedOutfits.Clear();
            Assert.Throws<InvalidOperationException>(() => store.SaveSkinProfile(deleted));
        }

        [Test]
        public void IndividualWeight_RoundTripsAndRejectsInvalidValues()
        {
            var state = new HowToFishSaveData();
            var item = new HowToFishWorldItemData { instanceId = "WeightedFish", definitionId = "Shrimp", weightMultiplier = 1.125f };
            state.worldItems.Add(item);
            store.Save(0, state);
            Assert.That(store.Load(0).Data.worldItems[0].weightMultiplier, Is.EqualTo(1.125f));
            foreach (float invalid in new[] { 0, -1, .79f, 1.21f, float.NaN, float.PositiveInfinity })
            {
                item.weightMultiplier = invalid;
                Assert.Throws<FormatException>(() => store.Save(0, state));
            }
            Assert.That(store.Load(0).Data.worldItems[0].weightMultiplier, Is.EqualTo(1.125f));
        }

        [Test]
        public void RouletteMultiplier_RoundTripsAndRejectsInvalidValues()
        {
            var state = new HowToFishSaveData();
            var item = new HowToFishWorldItemData
            {
                instanceId = "WonShrimp", definitionId = "Shrimp", health = 0,
                bettingMultiplier = 35 * 2, styleMultiplier = 2, hasBeenHeld = true
            };
            state.worldItems.Add(item);
            store.Save(0, state);
            Assert.That(store.Load(0).Data.worldItems[0].bettingMultiplier, Is.EqualTo(70));
            foreach (float invalid in new[] { 0, -1, float.NaN, float.PositiveInfinity })
            {
                item.bettingMultiplier = invalid;
                Assert.Throws<FormatException>(() => store.Save(0, state));
            }
            Assert.That(store.Load(0).Data.worldItems[0].bettingMultiplier, Is.EqualTo(70));
        }

        [Test]
        public void SharedSkins_MergeValidSlotsAndRejectInvalidProfile()
        {
            var first = new HowToFishSaveData { boatSkinId = "Boat/Gold" };
            first.unlockedSkins.Add("Knife/Chess");
            store.Save(0, first);
            var other = new HowToFishSaveData();
            Assert.That(store.LoadSharedSkins(other).unlockedSkins, Does.Contain("Knife/Chess"));
            string path = Path.Combine(directory, "PlayerSkins.json");
            File.WriteAllText(path, "interrupted");
            Assert.That(store.LoadSkinProfile().unlockedSkins, Is.Empty);
            Assert.That(store.LoadSharedSkins(new HowToFishSaveData()).unlockedSkins, Does.Contain("Knife/Chess"));
            string before = File.ReadAllText(path);
            Assert.Throws<FormatException>(() => store.SaveSkinProfile(new HowToFishSkinProfile { unlockedSkins = new List<string> { "Unknown" } }));
            Assert.That(File.ReadAllText(path), Is.EqualTo(before));
        }

        [Test]
        public void Skins_PreserveOwnershipAndInstanceSelectionsAndRejectInvalidIds()
        {
            var data = new HowToFishSaveData { boatSkinId = "Boat/Gold" };
            data.unlockedSkins.Add("Knife/Chess");
            data.inventory.Add(new HowToFishOwnedItem { id = "Knife", count = 1, skinId = "Knife/Chess", cooking = .6f });
            // 拾回实例的外观不等同于解锁，因此允许保存未解锁的合法外观。
            data.worldItems.Add(new HowToFishWorldItemData { instanceId = "DroppedGun", definitionId = "Pistol",
                equipment = new HowToFishOwnedItem { id = "Pistol", count = 1, skinId = "Pistol/Black and White", ammo = 3 } });
            store.Save(0, data);
            var loaded = store.Load(0);
            Assert.That(loaded.Status, Is.EqualTo(HowToFishLoadStatus.Ready));
            Assert.That(loaded.Data.boatSkinId, Is.EqualTo("Boat/Gold"));
            Assert.That(loaded.Data.unlockedSkins, Is.EqualTo(new[] { "Knife/Chess" }));
            Assert.That(loaded.Data.inventory[0].skinId, Is.EqualTo("Knife/Chess"));
            Assert.That(loaded.Data.inventory[0].cooking, Is.EqualTo(.6f));
            Assert.That(loaded.Data.worldItems[0].equipment.skinId, Is.EqualTo("Pistol/Black and White"));
            var incomplete = new HowToFishSaveData { unlockedSkins = null };
            Assert.Throws<FormatException>(incomplete.Validate);
            foreach (string invalid in new[] { "Missing/Gold", "Boat/Default", "" })
            {
                var corrupt = new HowToFishSaveData(); corrupt.unlockedSkins.Add(invalid);
                Assert.Throws<FormatException>(() => corrupt.Validate());
            }
            data.unlockedSkins.Add("Knife/Chess"); Assert.Throws<FormatException>(() => data.Validate()); data.unlockedSkins.RemoveAt(1);
            data.boatSkinId = "Knife/Chess"; Assert.Throws<FormatException>(() => data.Validate()); data.boatSkinId = null;
            data.inventory[0].skinId = "Boat/Gold"; Assert.Throws<FormatException>(() => data.Validate());
        }

        [Test]
        public void SaveAndLoad_PreservesProgressAndSeparatesSlots()
        {
            var data = new HowToFishSaveData
            {
                money = 120, unlockedIsland = 2, safeIsland = 1, hasBoatKey = true, isDriving = true, isOnBoat = true,
                boatPosition = new Vector3(15, .2f, -60), boatLocalPosition = new Vector3(0, .2f, -.8f),
                boatLocalYaw = 15, equippedItemId = "Knife", forestLeeches = 2,
                poisonSeconds = 2, poisonDamagePerSecond = 8, burningSeconds = 1.5f, burningDamagePerSecond = 12
            };
            data.inventory.Add(new HowToFishOwnedItem { id = "Knife", count = 1, upgrade = 2 });
            data.inventory.Add(new HowToFishOwnedItem { id = "Pistol", count = 1, ammo = 7, cooking = .7f,
                sight = HowToFishAttachment.SniperScope, barrel = HowToFishAttachment.Suppressor, hasLaser = true, hasExtendedMag = true });
            data.equipmentSlots = new List<string> { "Knife", "Pistol", "" };
            data.worldItems.Add(new HowToFishWorldItemData { instanceId = "SavedGun", definitionId = "Pistol", equipment = data.inventory[1].Copy() });
            data.worldItems.Add(new HowToFishWorldItemData { instanceId = "SavedFish", definitionId = "Shrimp", cooking = 1, hasBeenHeld = true, styleMultiplier = 12.5f, hasBeenHitByPlayer = true });
            data.completedQuests.Add("KeeperBeer");
            data.defeatedDripCreatures.Add("Shrimp");
            store.Save(0, data);
            var loaded = store.Load(0);
            Assert.That(loaded.Status, Is.EqualTo(HowToFishLoadStatus.Ready));
            Assert.That(loaded.Data.money, Is.EqualTo(120));
            Assert.That(loaded.Data.poisonSeconds, Is.EqualTo(2));
            Assert.That(loaded.Data.poisonDamagePerSecond, Is.EqualTo(8));
            Assert.That(loaded.Data.burningSeconds, Is.EqualTo(1.5f));
            Assert.That(loaded.Data.burningDamagePerSecond, Is.EqualTo(12));
            Assert.Throws<FormatException>(() => new HowToFishSaveData { poisonSeconds = float.NaN }.Validate());
            Assert.Throws<FormatException>(() => new HowToFishSaveData { burningDamagePerSecond = float.PositiveInfinity }.Validate());
            Assert.Throws<FormatException>(() => new HowToFishSaveData { poisonSeconds = 2 }.Validate());
            Assert.That(loaded.Data.inventory[0].upgrade, Is.EqualTo(2));
            Assert.That(loaded.Data.inventory[1].ammo, Is.EqualTo(7));
            Assert.That(loaded.Data.inventory[1].sight, Is.EqualTo(HowToFishAttachment.SniperScope));
            Assert.That(loaded.Data.inventory[1].barrel, Is.EqualTo(HowToFishAttachment.Suppressor));
            Assert.That(loaded.Data.inventory[1].hasLaser && loaded.Data.inventory[1].hasExtendedMag, Is.True);
            Assert.That(loaded.Data.equipmentSlots, Is.EqualTo(data.equipmentSlots));
            Assert.That(loaded.Data.worldItems[0].equipment.ammo, Is.EqualTo(7));
            Assert.That(loaded.Data.worldItems[0].equipment.cooking, Is.EqualTo(.7f));
            Assert.That(loaded.Data.inventory[1].cooking, Is.EqualTo(.7f));
            Assert.That(loaded.Data.worldItems[1].cooking, Is.EqualTo(1));
            Assert.That(loaded.Data.worldItems[1].hasBeenHeld, Is.True);
            Assert.That(loaded.Data.worldItems[1].styleMultiplier, Is.EqualTo(12.5f));
            Assert.That(loaded.Data.worldItems[1].hasBeenHitByPlayer, Is.True);
            Assert.That(loaded.Data.worldItems[0].equipment.sight, Is.EqualTo(HowToFishAttachment.SniperScope));
            TestContext.WriteLine("普通鱼快照往返：" + JsonUtility.ToJson(loaded.Data.worldItems[1]));
            Assert.That(loaded.Data.worldItems[1].HasEquipment, Is.False);
            Assert.That(loaded.Data.completedQuests, Does.Contain("KeeperBeer"));
            Assert.That(loaded.Data.defeatedDripCreatures, Does.Contain("Shrimp"));
            Assert.That(loaded.Data.isDriving && loaded.Data.isOnBoat, Is.True);
            Assert.That(loaded.Data.boatPosition, Is.EqualTo(data.boatPosition));
            Assert.That(loaded.Data.boatLocalPosition, Is.EqualTo(data.boatLocalPosition));
            Assert.That(loaded.Data.boatLocalYaw, Is.EqualTo(15));
            Assert.That(loaded.Data.equippedItemId, Is.EqualTo("Knife"));
            Assert.That(loaded.Data.forestLeeches, Is.EqualTo(2));
            Assert.Throws<FormatException>(() => new HowToFishSaveData { forestLeeches = 3 }.Validate());
            Assert.That(store.Load(1).Status, Is.EqualTo(HowToFishLoadStatus.Empty));
        }

        [Test]
        public void DynamiteFuse_RoundTripsAndRejectsInvalidOrUnrelatedItems()
        {
            var data = new HowToFishSaveData();
            var item = new HowToFishWorldItemData { instanceId = "LitDynamite", definitionId = "Dynamite", dynamiteFuseSeconds = 1.25f };
            data.worldItems.Add(item);
            store.Save(0, data);
            Assert.That(store.Load(0).Data.worldItems[0].dynamiteFuseSeconds, Is.EqualTo(1.25f));
            foreach (float invalid in new[] { -1, 3.01f, float.NaN, float.PositiveInfinity })
            {
                item.dynamiteFuseSeconds = invalid;
                Assert.Throws<FormatException>(() => store.Save(0, data));
            }
            item.dynamiteFuseSeconds = 1; item.definitionId = "Shrimp";
            Assert.Throws<FormatException>(() => data.Validate());
            Assert.That(store.Load(0).Data.worldItems[0].dynamiteFuseSeconds, Is.EqualTo(1.25f));
        }

        [Test]
        public void VolcanoProgressAndEnding_SurviveSaveAndRejectImpossibleStates()
        {
            var data = new HowToFishSaveData { unlockedIsland = 4, volcanoFish = 3 };
            store.Save(2, data);
            Assert.That(store.Load(2).Data.volcanoFish, Is.EqualTo(3));
            data.volcanoFish = 5; data.hasMilitaryBoatKey = true; data.hasFinished = true;
            store.Save(2, data);
            var loaded = store.Load(2).Data;
            Assert.That(loaded.volcanoFish, Is.EqualTo(5));
            Assert.That(loaded.hasMilitaryBoatKey && loaded.hasFinished, Is.True);
            Assert.Throws<FormatException>(() => new HowToFishSaveData { volcanoFish = 6 }.Validate());
            Assert.Throws<FormatException>(() => new HowToFishSaveData { hasFinished = true }.Validate());
            Assert.Throws<FormatException>(() => new HowToFishSaveData { hasMilitaryBoatKey = true }.Validate());
        }

        [Test]
        public void CorruptPrimary_IsAnEmptySlotUntilExplicitSave()
        {
            store.Save(0, new HowToFishSaveData { money = 12 });
            string path = Path.Combine(directory, "Slot1.json");
            File.WriteAllText(path, "interrupted");
            Assert.That(store.Load(0).Status, Is.EqualTo(HowToFishLoadStatus.Empty));
            Assert.That(File.ReadAllText(path), Is.EqualTo("interrupted"));
            store.Save(0, new HowToFishSaveData { money = 3 });
            Assert.That(store.Load(0).Data.money, Is.EqualTo(3));
        }

        [Test]
        public void UnsupportedStructure_IsAnEmptySlot()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "Slot1.json"), "{\"version\":99}");
            Assert.That(store.Load(0).Status, Is.EqualTo(HowToFishLoadStatus.Empty));
        }

        [Test]
        public void InvalidState_IsRejectedBeforeExistingSaveChanges()
        {
            store.Save(0, new HowToFishSaveData { money = 10 });
            Assert.Throws<FormatException>(() => store.Save(0, new HowToFishSaveData { money = -1 }));
            Assert.Throws<FormatException>(() => store.Save(0, new HowToFishSaveData { health = float.NaN }));
            Assert.Throws<FormatException>(() => store.Save(0, new HowToFishSaveData { isDriving = true, isOnBoat = true }));
            Assert.Throws<ArgumentOutOfRangeException>(() => store.Load(3));
            var invalidAttachment = new HowToFishSaveData();
            invalidAttachment.inventory.Add(new HowToFishOwnedItem { id = "Pistol", count = 1, sight = HowToFishAttachment.Suppressor });
            Assert.Throws<FormatException>(() => store.Save(0, invalidAttachment));
            var invalidSlots = new HowToFishSaveData { equipmentSlots = new List<string> { "Pistol", "", "" } };
            Assert.Throws<FormatException>(() => store.Save(0, invalidSlots));
            invalidSlots.inventory.Add(new HowToFishOwnedItem { id = "Pistol", count = 1 });
            invalidSlots.equipmentSlots[1] = "Pistol";
            Assert.Throws<FormatException>(() => store.Save(0, invalidSlots));
            Assert.That(store.Load(0).Data.money, Is.EqualTo(10));
        }

        [Test]
        public void MissingPrimary_DoesNotReadOldBackup()
        {
            Directory.CreateDirectory(directory);
            string backup = Path.Combine(directory, "Slot1.json.bak");
            File.WriteAllText(backup, JsonUtility.ToJson(new HowToFishSaveData { money = 99 }));
            Assert.That(store.Load(0).Status, Is.EqualTo(HowToFishLoadStatus.Empty));
            store.Save(0, new HowToFishSaveData { money = 1 });
            Assert.That(store.Load(0).Data.money, Is.EqualTo(1));
            Assert.That(File.ReadAllText(backup), Does.Contain("99"));
        }
    }
}
