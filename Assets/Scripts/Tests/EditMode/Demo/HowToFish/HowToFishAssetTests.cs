using Hotfix.HowToFish;
using System.Linq;
using System.IO;
using System.Globalization;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class HowToFishAssetTests
    {
        [Test]
        public void Skins_HaveShaderBindingsAndFiveIntakes()
        {
            const string root = "Assets/LoadResources/Demos/how_to_fish";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(root + "/Art/Shaders/HowToFishSkin.shader");
            Assert.That(shader, Is.Not.Null); Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(root + "/Data/Catalog.asset");
            foreach (var item in catalog.Items.Where(item => HowToFishSkinCatalog.Supports(item.Id)))
                foreach (var prefab in new[] { item.ViewPrefab, item.Prefab })
                {
                    var view = prefab.GetComponentInChildren<HowToFishSkinView>(true);
                    Assert.That(view, Is.Not.Null, item.Id);
                    Assert.That(new SerializedObject(view).FindProperty("skinShader").objectReferenceValue, Is.SameAs(shader));
                }
            var scene = EditorSceneManager.OpenPreviewScene(root + "/Scenes/Main.unity");
            try
            {
                var machines = scene.GetRootGameObjects().SelectMany(value => value.GetComponentsInChildren<HowToFishSlotMachine>()).ToArray();
                Assert.That(machines.Select(value => value.Island), Is.EquivalentTo(new[] { 0, 1, 2, 3, 4 }));
                foreach (var machine in machines)
                {
                    Assert.That(machine.name, Is.EqualTo("Intake")); Assert.That(machine.GetComponent<BoxCollider>().isTrigger, Is.True);
                    Assert.That(machine.transform.parent.GetComponentsInChildren<Transform>().Count(value => value.name.StartsWith("Reel") && value.name.Length == 5), Is.EqualTo(3));
                }
                Assert.That(scene.GetRootGameObjects().Single(value => value.name == "FishingBoat").transform.Find("Visual").GetComponent<HowToFishSkinView>(), Is.Not.Null);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void SavedShops_RestockVerifiedBaitsRodsAndRadarOnLaterIslands()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/LoadResources/Demos/how_to_fish/Scenes/Main.unity");
            try
            {
                var products = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishStation>())
                    .Where(station => station.Kind == HowToFishStationKind.Product).ToArray();
                foreach (var row in new[] { ("CrabRod", 0), ("Radar", 0), ("HotDog", 0), ("FishingRod", 1),
                    ("BeginnerLure", 1), ("BeginnerBossLure", 1), ("StandardLure", 2), ("StandardBossLure", 2),
                    ("ProfessionalLure", 3), ("ProfessionalBossLure", 3), ("ScientificLure", 4), ("ScientificBossLure", 4), ("Dynamite", 1) })
                    for (int island = 0; island < 5; island++)
                        Assert.That(products.Count(product => product.ItemId == row.Item1 && product.Island == island),
                            Is.EqualTo(island >= row.Item2 ? 1 : 0), row.Item1 + "/" + island);
                Assert.That(products.Where(product => product.ItemId == "Coconut").Select(product => product.Island), Is.EquivalentTo(new[] { 2 }));
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        [Test]
        public void VerifiedCreatureFacts_ApplyAll54BaseStatsAndNutrition()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>("Assets/LoadResources/Demos/how_to_fish/Data/Catalog.asset");
            var lines = File.ReadAllLines("docs/demos/how_to_fish/creature-source-data.tsv");
            var columns = lines[0].Split('\t').Select((name, index) => (name, index)).ToDictionary(row => row.name, row => row.index);
            Assert.That(lines.Length, Is.EqualTo(55));
            foreach (string line in lines.Skip(1))
            {
                var values = line.Split('\t');
                float Number(string key) => float.Parse(values[columns[key]], CultureInfo.InvariantCulture);
                var creature = catalog.FindCreature(values[0]);
                Assert.That(creature, Is.Not.Null, values[0]);
                Assert.That(creature.Health, Is.EqualTo(Number("_maxHp")), values[0]);
                Assert.That(creature.Value, Is.EqualTo(Number("_worth")), values[0]);
                Assert.That(creature.BaseWeight, Is.EqualTo(Number("_weight")), values[0]);
                Assert.That(creature.HealthRestored, Is.EqualTo(Number("_hpToRestore")), values[0]);
                Assert.That(creature.FullnessRestored, Is.EqualTo(Number("_fullnessToRestore")), values[0]);
                Assert.That(creature.IsMainBoss, Is.EqualTo(Number("_bossType") == 2), values[0]);
                Assert.That(creature.IsMiniBoss, Is.EqualTo(Number("_bossType") == 1), values[0]);
                Assert.That(creature.IgnoredBySeller, Is.EqualTo(Number("_ignoredByMoneyNPC") != 0), values[0]);
                Assert.That(creature.IgnoredBySeagulls, Is.EqualTo(Number("_ignoredBySeagulls") != 0), values[0]);
                if (!creature.IsBoss) continue;
                var boss = creature.Prefab.GetComponents<MonoBehaviour>().Single(component => component is IHowToFishBoss);
                Assert.That(new SerializedObject(boss).FindProperty("escapeSeconds").floatValue, Is.EqualTo(Number("_bossTimeInSeconds")), values[0]);
            }
            Assert.That(catalog.DripChance, Is.EqualTo(.1f));
            Assert.That(catalog.FindItem("FishingRod").Price, Is.EqualTo(3));
            Assert.That(catalog.FindItem("CrabMeat").Kind, Is.EqualTo(HowToFishItemKind.Quest));
            Assert.That(catalog.FindItem("HotDog").BaitLossChance, Is.EqualTo(.4f));
            Assert.That(catalog.FindItem("ScientificLure").BaitLossChance, Is.EqualTo(.2f));
            Assert.That(catalog.FindItem("EmptyBeerCan").BaitRequiresMovement, Is.False);
            Assert.That(catalog.FindItem("StandardLure").MinimumBiteSeconds, Is.EqualTo(1));
            Assert.That(catalog.FindItem("StandardLure").MaximumBiteSeconds, Is.EqualTo(2.5f));
            var dynamite = catalog.FindItem("Dynamite");
            Assert.That(dynamite.Kind, Is.EqualTo(HowToFishItemKind.Explosive));
            Assert.That(dynamite.Price, Is.EqualTo(25));
            Assert.That(dynamite.IsEquipment && dynamite.IsConsumable, Is.True);
            Assert.That(dynamite.Prefab.GetComponent<HowToFishDynamite>(), Is.Not.Null);
            Assert.That(dynamite.Prefab.GetComponent<Rigidbody>().mass, Is.EqualTo(1));
        }

        [Test]
        public void GunAttachments_HaveSavedMountsAndPerWeaponPrices()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>("Assets/LoadResources/Demos/how_to_fish/Data/Catalog.asset");
            foreach (var weapon in catalog.Items.Where(item => item.Kind == HowToFishItemKind.Gun))
            {
                var view = new SerializedObject(weapon.ViewPrefab.GetComponent<HowToFishEquipmentView>());
                var attachments = view.FindProperty("attachments"); var sights = view.FindProperty("sightPoints");
                Assert.That(attachments.arraySize, Is.EqualTo(6)); Assert.That(sights.arraySize, Is.EqualTo(3));
                for (int i = 0; i < sights.arraySize; i++) Assert.That(sights.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, weapon.Id);
                for (int i = 0; i < attachments.arraySize; i++)
                {
                    bool compatible = weapon.Id != "Shotgun" || i != 5;
                    Assert.That(weapon.AttachmentPrice((HowToFishAttachment)(i + 1)) > 0, Is.EqualTo(compatible), weapon.Id);
                    Assert.That(attachments.GetArrayElementAtIndex(i).objectReferenceValue != null, Is.EqualTo(compatible), weapon.Id);
                }
                Assert.That(view.FindProperty("laserEmitter").objectReferenceValue, Is.Not.Null, weapon.Id);
                Assert.That(view.FindProperty("laserBeam").objectReferenceValue, Is.Not.Null, weapon.Id);
                var worldView = weapon.Prefab.GetComponent<HowToFishWorldItem>().VisualRoot.GetComponent<HowToFishEquipmentView>();
                Assert.That(worldView, Is.Not.Null, weapon.Id + " 落地模型应有配件状态表现。");
                var worldParts = new SerializedObject(worldView).FindProperty("attachments");
                for (int i = 0; i < worldParts.arraySize; i++)
                    Assert.That(worldParts.GetArrayElementAtIndex(i).objectReferenceValue != null, Is.EqualTo(weapon.Id != "Shotgun" || i != 5), weapon.Id);
            }
            Assert.That(catalog.FindItem("Pistol").ExtendedMagazineSize, Is.EqualTo(17));
            Assert.That(catalog.FindItem("SniperRifle").ExtendedMagazineSize, Is.EqualTo(8));
            Assert.That(catalog.FindItem("AssaultRifle").AttachmentPrice(HowToFishAttachment.SniperScope), Is.EqualTo(4300));
        }

        [Test]
        public void Weapons_HaveCompleteUpgradeCurvesAndShotgunPelletDamage()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>("Assets/LoadResources/Demos/how_to_fish/Data/Catalog.asset");
            foreach (var row in new[] { ("BrassKnuckles", 15, 60), ("Knife", 15, 120), ("Pistol", 12, 160),
                ("Shotgun", 12, 28), ("SMG", 12, 70), ("SniperRifle", 12, 500), ("AssaultRifle", 12, 100) })
            {
                var weapon = catalog.FindItem(row.Item1);
                Assert.That(weapon, Is.Not.Null, row.Item1);
                Assert.That(weapon.MaxUpgrade, Is.EqualTo(row.Item2), row.Item1);
                Assert.That(weapon.DamageAtLevel(row.Item2), Is.EqualTo(row.Item3), row.Item1);
                for (int i = 0; i < row.Item2; i++)
                {
                    Assert.That(weapon.NextUpgradeCost(i), Is.GreaterThan(0), row.Item1);
                    Assert.That(weapon.DamageAtLevel(i + 1), Is.GreaterThan(weapon.DamageAtLevel(i)), row.Item1);
                }
            }
            Assert.That(catalog.FindItem("Knife").Price, Is.EqualTo(45));
            Assert.That(catalog.FindItem("BrassKnuckles").Price, Is.EqualTo(24));
            Assert.That(catalog.FindItem("Shotgun").Pellets, Is.EqualTo(25));
            Assert.That(catalog.FindItem("Shotgun").Damage, Is.EqualTo(3));
        }

        [Test]
        public void Catalog_HasUsableCreatureAndEquipmentPrefabs()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>("Assets/LoadResources/Demos/how_to_fish/Data/Catalog.asset");
            Assert.That(catalog, Is.Not.Null);
            Assert.DoesNotThrow(catalog.Validate);
            foreach (var creature in catalog.Creatures)
            {
                Assert.That(creature.Prefab, Is.Not.Null, creature.Id);
                Assert.That(creature.Prefab.GetComponent<HowToFishWorldItem>(), Is.Not.Null, creature.Id);
                Assert.That(creature.Prefab.GetComponent<Rigidbody>(), Is.Not.Null, creature.Id);
                Assert.That(creature.Prefab.GetComponent<Collider>(), Is.Not.Null, creature.Id);
            }
            foreach (var item in catalog.Items)
            {
                if (!item.IsEquipment) continue;
                Assert.That(item.ViewPrefab, Is.Not.Null, item.Id);
                Assert.That(item.ViewPrefab.GetComponent<HowToFishEquipmentView>(), Is.Not.Null, item.Id);
                Assert.That(item.ViewPrefab.GetComponentInChildren<Rigidbody>(), Is.Null, "第一人称模型不应复用动态物品刚体。");
            }
            var puffer = catalog.FindCreature("Pufferfish").Prefab;
            Assert.That(puffer.GetComponents<Collider>().Length, Is.EqualTo(1));
            Assert.That(puffer.GetComponent<SphereCollider>(), Is.Not.Null);
            var settings = new SerializedObject(puffer.GetComponent<HowToFishPufferfish>());
            Assert.That(settings.FindProperty("ball").objectReferenceValue, Is.Not.Null);
            Assert.That(settings.FindProperty("poisonPrefab").objectReferenceValue, Is.Not.Null);
            var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/LoadResources/Demos/how_to_fish/Art/Materials/BowlGlass.mat");
            Assert.That(glass.GetFloat("_Surface"), Is.EqualTo(1));
            Assert.That(glass.GetColor("_BaseColor").a, Is.LessThan(.5f));
            var bird = new SerializedObject(catalog.FindCreature("Albatross").Prefab.GetComponent<HowToFishAlbatross>());
            foreach (string field in new[] { "leftWing", "rightWing", "droppingPrefab" })
                Assert.That(bird.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(catalog.RollCatch(3, "ProfessionalBossLure", 0, "FishingRod").Id, Is.EqualTo("Tuna"));
            Assert.That(catalog.RollCatch(2, "ProfessionalBossLure", 0, "FishingRod").Id, Is.EqualTo("Tuna"), "可选首领按鱼饵开放到首岛以外。");
            Assert.That(catalog.RollCatch(0, "ProfessionalBossLure", 0, "FishingRod").Id, Is.EqualTo("Tuna"));
            foreach (string id in new[] { "Sunfish", "OldPike", "GoblinShark" })
            {
                var creature = catalog.FindCreature(id);
                Assert.That(creature.IsMiniBoss, Is.True);
                var behavior = new SerializedObject(creature.Prefab.GetComponent<HowToFishJumpingFish>());
                Assert.That(behavior.FindProperty("tail").objectReferenceValue, Is.Not.Null);
            }
            Assert.That(catalog.RollCatch(4, "BeginnerBossLure", 0, "FishingRod").Id, Is.EqualTo("Sunfish"));
            Assert.That(catalog.RollCatch(4, "BeginnerBossLure", .5f, "FishingRod").Id, Is.EqualTo("OldPike"));
            Assert.That(catalog.RollCatch(1, "ScientificBossLure", 0, "FishingRod").Id, Is.EqualTo("GoblinShark"));
            Assert.That(catalog.RollCatch(1, "BeginnerLure", .982f, "FishingRod").Id, Is.Not.EqualTo("Sunfish"));
            Assert.That(catalog.RollCatch(1, "BeginnerLure", .983f, "FishingRod").Id, Is.EqualTo("Sunfish"));
            foreach (string id in new[] { "BowheadWhale", "MutatedBowheadWhale" })
            {
                var whale = new SerializedObject(catalog.FindCreature(id).Prefab.GetComponent<HowToFishWhale>());
                Assert.That(whale.FindProperty("tail").objectReferenceValue, Is.Not.Null);
                Assert.That(whale.FindProperty("blowhole").objectReferenceValue, Is.Not.Null);
                if (id == "MutatedBowheadWhale") Assert.That(whale.FindProperty("lavaPrefab").objectReferenceValue, Is.Not.Null);
            }
            var lava = new SerializedObject(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/how_to_fish/Prefabs/Items/LavaGlob.prefab").GetComponent<HowToFishProjectile>());
            Assert.That(lava.FindProperty("impactPool").objectReferenceValue, Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>("Assets/LoadResources/Demos/how_to_fish/Art/UI/HubPreview.png"), Is.Not.Null);
            Assert.That(catalog.Creatures.Count(value => value.IsJournalEntry), Is.EqualTo(49));
            foreach (string id in new[] { "Clam", "Leech", "FootSnail", "Seagull", "BingBong" }) Assert.That(catalog.FindCreature(id).IsJournalEntry, Is.False);
            Assert.That(catalog.FindCreature("BingBong").IsBoss, Is.False);
            Assert.That(catalog.RollCatch(2, "Coconut", 0, "FishingRod").Id, Is.EqualTo("BingBong"));
            var seagull = new SerializedObject(catalog.FindCreature("Seagull").Prefab.GetComponent<HowToFishSeagull>());
            foreach (string field in new[] { "leftWing", "rightWing", "carryPoint" }) Assert.That(seagull.FindProperty(field).objectReferenceValue, Is.Not.Null);
            var jumpingFish = new SerializedObject(catalog.FindCreature("BingBong").Prefab.GetComponent<HowToFishJumpingFish>());
            Assert.That(jumpingFish.FindProperty("propeller").objectReferenceValue, Is.Not.Null);
            Assert.That(jumpingFish.FindProperty("limbs").arraySize, Is.EqualTo(4));
        }

        [Test]
        public void ForestPools_ExposeConfiguredSpeciesAndKeepStoryBossAtForest()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>("Assets/LoadResources/Demos/how_to_fish/Data/Catalog.asset");
            var free = Enumerable.Range(0, 100).Select(index => catalog.RollCatch(1, "FreeLure", index / 100f, "FishingRod").Id).Distinct();
            Assert.That(free, Is.EquivalentTo(new[] { "Mackerel", "Gar", "Pike" }));
            var beginner = Enumerable.Range(0, 100).Select(index => catalog.RollCatch(3, "BeginnerLure", index / 100f, "FishingRod").Id).Distinct();
            Assert.That(beginner, Is.EquivalentTo(new[] { "Piranha", "Pike", "Cod", "Goldfish", "Perch", "Triggerfish", "Goby", "Salmon", "Sunfish" }));
            Assert.That(catalog.RollCatch(1, "FreeLure", 0, "CrabRod"), Is.Not.Null);
            Assert.That(catalog.RollCatch(1, "ModifiedLeech", 0, "CrabRod").Id, Is.EqualTo("GiantPiranha"));
            Assert.That(catalog.RollCatch(2, "ModifiedLeech", 0, "FishingRod"), Is.Null);
            var professional = Enumerable.Range(0, 100).Select(index => catalog.RollCatch(3, "ProfessionalLure", index / 100f, "FishingRod").Id).Distinct();
            Assert.That(professional, Is.EquivalentTo(new[] { "Bass", "RedSnapper", "Parrotfish", "Tigerfish", "FlyingFish", "Sengarat", "Eel", "Halibut", "Voxelfish", "Dripper", "Tuna", "Seahorse" }));
            Assert.That(catalog.RollCatch(1, "ProfessionalLure", 0, "FishingRod"), Is.Not.Null, "专业普通鱼同样按鱼饵开放，不锁定岩石岛。");
            Assert.That(catalog.RollCatch(3, "ProfessionalLure", 0, "CrabRod"), Is.Not.Null);
            var standard = Enumerable.Range(0, 100).Select(index => catalog.RollCatch(2, "StandardLure", index / 100f, "FishingRod").Id).Distinct();
            Assert.That(standard, Is.EquivalentTo(new[] { "Needlefish", "Seahorse", "Bowlfish", "YellowBoxfish", "Angelfish", "Catfish", "SeaUrchin", "Clownfish", "Bluegill", "BlueShark" }));
            Assert.That(catalog.RollCatch(1, "StandardLure", 0, "FishingRod"), Is.Not.Null);
            Assert.That(catalog.RollCatch(2, "StandardLure", 0, "CrabRod"), Is.Not.Null);
        }

        [Test]
        public void DemoFont_ContainsFishingNamesWithoutChangingPublicFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CNHowToFish.asset");
            Assert.That(font, Is.Not.Null);
            foreach (var character in "渔力全开蛤蜊蜘蛛蟹龙虾饱食鱼饵森林水蛭巨型食人鱼骨架鲭鳝鳕鲈扳机虾虎鲑颌针鲨河豚濒危萝卜鳍烤金枪信天翁俯冲狙击鳗鹦嘴鲷") Assert.That(font.HasCharacter(character), Is.True, character.ToString());
            Assert.That(font, Is.Not.SameAs(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset")));
        }
    }
}
