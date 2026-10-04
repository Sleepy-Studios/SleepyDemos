using System;
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
            Assert.That(loaded.Status, Is.EqualTo(HowToFishLoadStatus.Ready), loaded.Error);
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
        public void CorruptPrimary_OffersBackupAndRequiresExplicitRecovery()
        {
            store.Save(0, new HowToFishSaveData { money = 20 });
            store.Save(0, new HowToFishSaveData { money = 40 });
            var path = Path.Combine(directory, "Slot1.json");
            File.WriteAllText(path, "interrupted");
            Assert.That(store.Load(0).Status, Is.EqualTo(HowToFishLoadStatus.RecoveryAvailable));
            Assert.Throws<IOException>(() => store.Save(0, new HowToFishSaveData()));
            Assert.That(File.ReadAllText(path), Is.EqualTo("interrupted"));
            store.RestoreBackup(0);
            Assert.That(store.Load(0).Data.money, Is.EqualTo(20));
            Assert.That(Directory.GetFiles(directory, "*.corrupt-*").Length, Is.EqualTo(1));
        }

        [Test]
        public void FutureVersion_DoesNotSilentlyRestoreOlderBackup()
        {
            store.Save(0, new HowToFishSaveData());
            store.Save(0, new HowToFishSaveData { money = 15 });
            File.WriteAllText(Path.Combine(directory, "Slot1.json"), "{\"format\":2}");
            Assert.That(store.Load(0).Status, Is.EqualTo(HowToFishLoadStatus.UnsupportedVersion));
            Assert.Throws<IOException>(() => store.Save(0, new HowToFishSaveData()));
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

        [TestCase("interrupted", HowToFishLoadStatus.Corrupt)]
        [TestCase("{\"format\":2}", HowToFishLoadStatus.UnsupportedVersion)]
        public void MissingPrimary_WithUnreadableBackup_IsNotAnEmptySlot(string backup, HowToFishLoadStatus expected)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "Slot1.json");
            File.WriteAllText(path + ".bak", backup);
            Assert.That(store.Load(0).Status, Is.EqualTo(expected));
            Assert.Throws<IOException>(() => store.Save(0, new HowToFishSaveData()));
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.ReadAllText(path + ".bak"), Is.EqualTo(backup));
        }
    }
}
