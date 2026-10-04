using System;
using System.IO;
using System.Globalization;
using System.Linq;
using Hotfix.HowToFish;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Hotfix.Editor.HowToFish
{
    /// 渔力全开资源装配入口；只修改该 Demo 自有资源。
    public static class HowToFishAssetBuilder
    {
        internal const string Root = "Assets/LoadResources/Demos/how_to_fish";
        internal const string FontPath = "Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CNHowToFish.asset";

        internal static void ConfigureVerifiedCreatures()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var settings = new SerializedObject(catalog);
            settings.FindProperty("dripChance").floatValue = .1f;
            var entries = settings.FindProperty("creatures");
            var lines = File.ReadAllLines(Path.Combine(Application.dataPath, "../docs/demos/how_to_fish/creature-source-data.tsv"));
            var columns = lines[0].Split('\t').Select((name, index) => (name, index)).ToDictionary(row => row.name, row => row.index);
            var verified = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (string line in lines.Skip(1).Where(value => !string.IsNullOrWhiteSpace(value)))
            {
                var values = line.Split('\t');
                if (values.Length != columns.Count || !verified.Add(values[0])) throw new InvalidOperationException("生物来源表列数无效或ID重复。");
                float Number(string field) => float.Parse(values[columns[field]], CultureInfo.InvariantCulture);
                var entry = FindEntry(entries, values[0]);
                entry.FindPropertyRelative("health").floatValue = Number("_maxHp");
                entry.FindPropertyRelative("value").intValue = (int)Number("_worth");
                entry.FindPropertyRelative("baseWeight").floatValue = Number("_weight");
                entry.FindPropertyRelative("healthRestored").floatValue = Number("_hpToRestore");
                entry.FindPropertyRelative("fullnessRestored").floatValue = Number("_fullnessToRestore");
                entry.FindPropertyRelative("isBoss").boolValue = Number("_bossType") > 0;
                entry.FindPropertyRelative("isMiniBoss").boolValue = Number("_bossType") == 1;
                entry.FindPropertyRelative("isEndangered").boolValue = Number("_isEndangered") != 0;
                entry.FindPropertyRelative("ignoredBySeller").boolValue = Number("_ignoredByMoneyNPC") != 0;
                entry.FindPropertyRelative("ignoredBySeagulls").boolValue = Number("_ignoredBySeagulls") != 0;
                bool ordinaryMotion = Number("_bossType") == 0 && Number("_maxHp") > 0 &&
                    values[0] != "Seagull" && values[0] != "BingBong";
                if (Number("_bossType") == 0 && !ordinaryMotion) continue;
                string path = AssetDatabase.GetAssetPath(entry.FindPropertyRelative("prefab").objectReferenceValue);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if ((values[0] == "GiantPiranha" || values[0] == "Pufferfish" || values[0] == "MutatedBowheadWhale") &&
                        root.GetComponent<HowToFishBossTransition>() == null) root.AddComponent<HowToFishBossTransition>();
                    if (ordinaryMotion)
                    {
                        var motion = root.GetComponent<HowToFishFishMotion>() ?? root.AddComponent<HowToFishFishMotion>();
                        var configured = new SerializedObject(motion);
                        foreach (var pair in new[]
                        {
                            ("jumpForce", "_jumpForce"), ("towardsWaterForce", "_towardsWaterForce"),
                            ("towardsPlayerForce", "_towardsPlayerForce"), ("airTowardsPlayerForce", "_airTowardsPlayerForce"),
                            ("minimumInterval", "_minTime"), ("maximumInterval", "_maxTime"),
                            ("damage", "_onHitDamage"), ("damageInterval", "_timeBetweenDamage")
                        })
                            if (!string.IsNullOrEmpty(values[columns[pair.Item2]]))
                                configured.FindProperty(pair.Item1).floatValue = Number(pair.Item2);
                        configured.FindProperty("attackInAir").boolValue = values[columns["_attackInAir"]] == "1";
                        configured.FindProperty("jumpOnWater").boolValue = values[columns["_jumpOnWater"]] == "1";
                        if (values[0] == "BrownCrab" || values[0] == "RockCrab" || values[0] == "Lobster")
                        {
                            configured.FindProperty("walkSpeed").floatValue = values[0] == "BrownCrab" ? 2.25f : values[0] == "RockCrab" ? 2.5f : 3;
                            configured.FindProperty("walkSideways").boolValue = values[0] != "Lobster";
                        }
                        configured.ApplyModifiedPropertiesWithoutUndo();
                    }
                    foreach (var behavior in root.GetComponents<MonoBehaviour>().Where(component => component is IHowToFishBoss))
                    {
                        var configured = new SerializedObject(behavior);
                        if (behavior is HowToFishGiantPiranha) configured.FindProperty("summonSeconds").floatValue = 15;
                        if (behavior is HowToFishSpiderCrab) configured.FindProperty("stunSeconds").floatValue = 2.5f;
                        configured.FindProperty("escapeSeconds").floatValue = Number("_bossTimeInSeconds");
                        string damageKey = !string.IsNullOrEmpty(values[columns["_onHitDamage"]]) ? "_onHitDamage" : "_meleeDamage";
                        if (!string.IsNullOrEmpty(values[columns[damageKey]]))
                        {
                            var damage = configured.FindProperty("damage") ?? configured.FindProperty("diveDamage") ?? configured.FindProperty("chargeDamage");
                            if (damage != null) damage.floatValue = Number(damageKey);
                        }
                        var interval = configured.FindProperty("damageInterval");
                        if (interval != null && !string.IsNullOrEmpty(values[columns["_timeBetweenDamage"]])) interval.floatValue = Number("_timeBetweenDamage");
                        configured.ApplyModifiedPropertiesWithoutUndo();
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (var creature in catalog.Creatures.Where(creature => creature.IsJournalEntry))
                if (!verified.Contains(creature.Id)) throw new InvalidOperationException("图鉴生物缺少来源记录：" + creature.Id);
            var trophy = FindEntry(settings.FindProperty("items"), "CrabMeat");
            trophy.FindPropertyRelative("displayName").stringValue = "蜘蛛蟹壳";
            trophy.FindPropertyRelative("kind").enumValueIndex = (int)HowToFishItemKind.Quest;
            trophy.FindPropertyRelative("isCookable").boolValue = false;
            settings.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
            foreach (var hazard in new[] { ("PoisonPool", 4f, .75f, false), ("LavaPool", 20f, 1.75f, true) })
            {
                string path = Root + "/Prefabs/Items/" + hazard.Item1 + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var pool = new SerializedObject(root.GetComponent<HowToFishDamagePool>());
                    pool.FindProperty("duration").floatValue = hazard.Item2;
                    pool.FindProperty("radius").floatValue = hazard.Item3;
                    pool.FindProperty("burning").boolValue = hazard.Item4;
                    pool.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
        }

        private static SerializedProperty FindEntry(SerializedProperty array, string id) => Enumerable.Range(0, array.arraySize)
            .Select(array.GetArrayElementAtIndex).Single(entry => entry.FindPropertyRelative("id").stringValue == id);

        internal static void ConfigureVerifiedBaitPools()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var settings = new SerializedObject(catalog);
            var creatures = settings.FindProperty("creatures");
            for (int i = 0; i < creatures.arraySize; i++)
            {
                creatures.GetArrayElementAtIndex(i).FindPropertyRelative("baits").arraySize = 0;
                creatures.GetArrayElementAtIndex(i).FindPropertyRelative("baitWeights").arraySize = 0;
            }
            void Pool(string bait, params (string id, float weight)[] rows)
            {
                foreach (var row in rows)
                {
                    var entry = FindEntry(creatures, row.id);
                    var baits = entry.FindPropertyRelative("baits"); var weights = entry.FindPropertyRelative("baitWeights");
                    int index = baits.arraySize++; weights.arraySize = baits.arraySize;
                    baits.GetArrayElementAtIndex(index).stringValue = bait;
                    weights.GetArrayElementAtIndex(index).floatValue = row.weight;
                }
            }
            Pool("FreeLure", ("BrownCrab", 5), ("Shrimp", 4), ("Mackerel", 2), ("Gar", 2), ("Pike", 1));
            Pool("HotDog", ("RockCrab", 10), ("Lobster", 3));
            Pool("BeginnerLure", ("Cod", 10), ("Pike", 10), ("Piranha", 10), ("Triggerfish", 5), ("Goby", 5), ("Salmon", 7), ("Goldfish", 4), ("Perch", 5), ("Sunfish", 1));
            Pool("StandardLure", ("Catfish", 12), ("Bluegill", 10), ("YellowBoxfish", 8), ("Angelfish", 8), ("Needlefish", 8), ("SeaUrchin", 10), ("Clownfish", 6), ("Seahorse", 3), ("Bowlfish", 3), ("BlueShark", 1));
            Pool("ProfessionalLure", ("Sengarat", 10), ("RedSnapper", 10), ("Tigerfish", 10), ("Parrotfish", 10), ("Bass", 10), ("Halibut", 10), ("FlyingFish", 5), ("Eel", 5), ("Voxelfish", 3), ("Dripper", 3), ("Tuna", 1), ("Seahorse", 1));
            Pool("ScientificLure", ("Stonefish", 10), ("Blobfish", 10), ("Anglerfish", 10), ("Oarfish", 6), ("SuperdwarfFish", 4));
            Pool("EmptyBeerCan", ("SpiderCrab", 1)); Pool("ModifiedLeech", ("GiantPiranha", 1)); Pool("Carrot", ("Pufferfish", 1));
            Pool("BeginnerBossLure", ("Sunfish", 1), ("OldPike", 1)); Pool("StandardBossLure", ("BlueShark", 1));
            Pool("ProfessionalBossLure", ("Tuna", 1)); Pool("ScientificBossLure", ("GoblinShark", 1));
            Pool("Coconut", ("BingBong", 1)); Pool("FishBucket", ("BowheadWhale", 1));
            var items = settings.FindProperty("items");
            FindEntry(items, "FishingRod").FindPropertyRelative("price").intValue = 3;
            foreach (var row in new[]
            {
                ("FreeLure", 1f, 4f, 0f, false), ("HotDog", 1f, 3f, .4f, false),
                ("BeginnerLure", 1f, 3f, .35f, true), ("StandardLure", 1f, 2.5f, .3f, true),
                ("ProfessionalLure", 1f, 2f, .25f, true), ("ScientificLure", .75f, 1.5f, .2f, true),
                ("EmptyBeerCan", 2f, 4f, 1f, false), ("BeginnerBossLure", 1f, 3f, 1f, true),
                ("ModifiedLeech", 1f, 3f, 1f, true), ("Carrot", 1f, 2.5f, 1f, true),
                ("Coconut", 1f, 2.5f, 1f, true), ("StandardBossLure", 1f, 2.5f, 1f, true),
                ("ProfessionalBossLure", 1f, 2f, 1f, true), ("FishBucket", 2f, 3f, 1f, true),
                ("ScientificBossLure", .75f, 1.5f, 1f, true)
            })
            {
                var entry = FindEntry(items, row.Item1);
                entry.FindPropertyRelative("minimumBiteSeconds").floatValue = row.Item2;
                entry.FindPropertyRelative("maximumBiteSeconds").floatValue = row.Item3;
                entry.FindPropertyRelative("baitLossChance").floatValue = row.Item4;
                entry.FindPropertyRelative("baitRequiresMovement").boolValue = row.Item5;
            }
            settings.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
        }

        internal static void ConfigureWeaponUpgrades()
        {
            BuildWorldItem("BrassKnuckles");
            BuildEquipment("BrassKnuckles", "Strike");
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var data = new SerializedObject(catalog);
            var items = data.FindProperty("items");
            if (catalog.FindItem("BrassKnuckles") == null)
            {
                var entry = items.GetArrayElementAtIndex(items.arraySize++);
                entry.FindPropertyRelative("id").stringValue = "BrassKnuckles";
                entry.FindPropertyRelative("displayName").stringValue = "指虎";
                entry.FindPropertyRelative("kind").enumValueIndex = (int)HowToFishItemKind.Melee;
                entry.FindPropertyRelative("island").intValue = 0;
                entry.FindPropertyRelative("price").intValue = 24;
                entry.FindPropertyRelative("useInterval").floatValue = .25f;
                entry.FindPropertyRelative("range").floatValue = 2.4f;
                entry.FindPropertyRelative("reloadSeconds").floatValue = 1;
                entry.FindPropertyRelative("magazineSize").intValue = 0;
                entry.FindPropertyRelative("pellets").intValue = 1;
                entry.FindPropertyRelative("spread").floatValue = 0;
                entry.FindPropertyRelative("automatic").boolValue = false;
                entry.FindPropertyRelative("isConsumable").boolValue = false;
                entry.FindPropertyRelative("isCookable").boolValue = false;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/BrassKnuckles.prefab");
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Equipment/BrassKnuckles.prefab");
            }
            var rows = new[]
            {
                ("BrassKnuckles", new[] {5,6,7,8,9,10,12,14,16,20,25,30,35,42,50,60}, new[] {8,16,34,60,90,160,220,480,710,1600,2800,6200,13200,16700,22000}),
                ("Knife", new[] {16,18,20,22,24,26,28,32,36,40,46,54,62,80,100,120}, new[] {14,28,36,84,190,290,480,810,1100,1800,3100,6200,14600,19250,28750}),
                ("Pistol", new[] {25,28,30,33,35,40,45,50,55,60,100,130,160}, new[] {15,25,35,150,250,300,1500,1920,2560,4700,6300,9100}),
                ("Shotgun", new[] {3,4,5,6,7,8,9,11,13,15,20,24,28}, new[] {20,30,40,200,250,300,1500,2500,3000,4240,6120,8340}),
                ("SMG", new[] {24,25,26,27,28,29,30,32,35,36,45,60,70}, new[] {32,64,128,180,240,375,1500,1800,2200,3400,4850,6700}),
                ("SniperRifle", new[] {200,210,220,230,240,250,260,270,280,300,350,400,500}, new[] {140,220,349,690,900,1400,2000,2650,3300,5600,8500,10000}),
                ("AssaultRifle", new[] {40,45,50,55,60,65,70,75,80,85,90,95,100}, new[] {300,480,700,920,1360,1890,2480,3100,3875,4520,5125,5800})
            };
            foreach (var row in rows)
            {
                int index = Enumerable.Range(0, items.arraySize).Single(i => items.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == row.Item1);
                var item = items.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("damage").floatValue = row.Item2[0];
                var damage = item.FindPropertyRelative("upgradedDamage"); damage.arraySize = row.Item3.Length;
                var costs = item.FindPropertyRelative("upgradeCosts"); costs.arraySize = row.Item3.Length;
                for (int i = 0; i < costs.arraySize; i++)
                {
                    damage.GetArrayElementAtIndex(i).floatValue = row.Item2[i + 1];
                    costs.GetArrayElementAtIndex(i).intValue = row.Item3[i];
                }
                if (row.Item1 == "Knife") item.FindPropertyRelative("price").intValue = 45;
                if (row.Item1 == "Shotgun")
                {
                    item.FindPropertyRelative("pellets").intValue = 25;
                    item.FindPropertyRelative("spread").floatValue = 6;
                }
                if (row.Item1 == "SMG") item.FindPropertyRelative("useInterval").floatValue = .05f;
                if (row.Item1 == "AssaultRifle") item.FindPropertyRelative("useInterval").floatValue = .07f;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            catalog.Validate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        internal static void ConfigureGunAttachments()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var data = new SerializedObject(catalog);
            var items = data.FindProperty("items");
            (string id, int[] prices, int magazine, float height, float depth, float ironHeight, float recoil, float bottom)[] rows =
            {
                ("Pistol", new[] {310,940,400,2500,100,90},17,.093f,.035f,.105f,.8f,-.025f),
                ("Shotgun", new[] {280,650,220,1800,140,0},0,.083f,.07f,.09f,2f,0),
                ("SMG", new[] {460,940,600,2500,280,320},40,.135f,.13f,.15f,.3f,-.22f),
                ("SniperRifle", new[] {640,1200,500,2500,1600,1200},8,.111f,.13f,.144f,2f,-.12f),
                ("AssaultRifle", new[] {5500,4300,3000,8000,4800,5400},40,.125f,.14f,.18f,.6f,-.22f)
            };
            var laserMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Materials/LaserBeam.mat");
            if (laserMaterial == null)
            {
                laserMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                laserMaterial.SetColor("_BaseColor", new Color(1, .035f, .015f));
                AssetDatabase.CreateAsset(laserMaterial, Root + "/Art/Materials/LaserBeam.mat");
            }
            foreach (var row in rows)
            {
                var item = items.GetArrayElementAtIndex(Enumerable.Range(0, items.arraySize).Single(i => items.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue == row.id));
                var prices = item.FindPropertyRelative("attachmentPrices"); prices.arraySize = 6;
                for (int i = 0; i < 6; i++) prices.GetArrayElementAtIndex(i).intValue = row.prices[i];
                item.FindPropertyRelative("extendedMagazineSize").intValue = row.magazine;
                item.FindPropertyRelative("recoilAngle").floatValue = row.recoil;
                foreach (string folder in new[] { "Equipment", "Items" })
                {
                    string path = Root + "/Prefabs/" + folder + "/" + row.id + ".prefab";
                    var contents = PrefabUtility.LoadPrefabContents(path);
                    var root = folder == "Items" ? contents.GetComponent<HowToFishWorldItem>().VisualRoot.gameObject : contents;
                    try
                    {
                        var component = root.GetComponent<HowToFishEquipmentView>();
                        if (component == null) component = root.AddComponent<HowToFishEquipmentView>();
                        var view = new SerializedObject(component);
                        if (folder == "Items") view.FindProperty("tip").objectReferenceValue = root.GetComponentsInChildren<Transform>(true).Single(node => node.name == "Muzzle");
                        var tip = (Transform)view.FindProperty("tip").objectReferenceValue;
                        var container = root.transform.Find("Attachments");
                        if (container == null) { container = new GameObject("Attachments").transform; container.SetParent(root.transform, false); }
                        var attachments = view.FindProperty("attachments"); attachments.arraySize = 6;
                        var sights = view.FindProperty("sightPoints"); sights.arraySize = 3;
                        var iron = root.transform.Find("IronAim");
                        if (iron == null) { iron = new GameObject("IronAim").transform; iron.SetParent(root.transform, false); }
                        iron.localPosition = new Vector3(0, row.ironHeight, .04f); sights.GetArrayElementAtIndex(0).objectReferenceValue = iron;
                        var originals = root.GetComponentsInChildren<Transform>(true).Where(node => node.name.StartsWith("RearSight", StringComparison.Ordinal) || node.name == "FrontSight").ToArray();
                        var ironVisuals = view.FindProperty("ironSightVisuals"); ironVisuals.arraySize = originals.Length;
                        for (int i = 0; i < originals.Length; i++) ironVisuals.GetArrayElementAtIndex(i).objectReferenceValue = originals[i].gameObject;
                        for (int i = 0; i < 6; i++)
                        {
                            var kind = (HowToFishAttachment)(i + 1);
                            if (row.prices[i] == 0) { attachments.GetArrayElementAtIndex(i).objectReferenceValue = null; continue; }
                            string name = kind.ToString();
                            var holder = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(node => node.name == "Attachment_" + name);
                            if (holder == null)
                            {
                                holder = new GameObject("Attachment_" + name).transform; holder.SetParent(container, false);
                                int copies = row.id == "Shotgun" && (kind == HowToFishAttachment.Compensator || kind == HowToFishAttachment.Suppressor) ? 2 : 1;
                                for (int copy = 0; copy < copies; copy++)
                                {
                                    var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" + name + ".fbx"));
                                    model.transform.SetParent(holder, false);
                                    if (copies == 2) model.transform.localPosition = Vector3.right * (copy == 0 ? -.025f : .025f);
                                    foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialFor).ToArray();
                                }
                            }
                            holder.SetParent(container, false); holder.localRotation = Quaternion.identity;
                            if (kind == HowToFishAttachment.RedDotSight || kind == HowToFishAttachment.SniperScope)
                            {
                                holder.localPosition = new Vector3(0, row.height, row.depth);
                                sights.GetArrayElementAtIndex((int)kind).objectReferenceValue = holder.GetComponentsInChildren<Transform>(true).Single(node => node.name == "Aim");
                            }
                            else if (kind == HowToFishAttachment.Compensator || kind == HowToFishAttachment.Suppressor)
                            {
                                if (row.id == "Shotgun") holder.SetParent(root.GetComponentsInChildren<Transform>(true).Single(node => node.name == "Breech"), false);
                                holder.position = tip.position;
                            }
                            else if (kind == HowToFishAttachment.LaserSight)
                            {
                                var muzzle = root.transform.InverseTransformPoint(tip.position);
                                holder.localPosition = new Vector3(.046f, muzzle.y - .025f, muzzle.z - .09f);
                                view.FindProperty("laserEmitter").objectReferenceValue = holder.GetComponentsInChildren<Transform>(true).Single(node => node.name == "Emitter");
                            }
                            else
                            {
                                holder.SetParent(root.GetComponentsInChildren<Transform>(true).Single(node => node.name == "Magazine"), false);
                                holder.localPosition = Vector3.up * row.bottom;
                            }
                            holder.gameObject.SetActive(false); attachments.GetArrayElementAtIndex(i).objectReferenceValue = holder.gameObject;
                        }
                        if (folder == "Equipment")
                        {
                            var beam = root.transform.Find("LaserBeam");
                            if (beam == null) { beam = new GameObject("LaserBeam", typeof(LineRenderer)).transform; beam.SetParent(root.transform, false); }
                            var line = beam.GetComponent<LineRenderer>(); line.sharedMaterial = laserMaterial; line.positionCount = 2; line.useWorldSpace = true;
                            line.startWidth = .002f; line.endWidth = .009f; line.enabled = false;
                            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; line.receiveShadows = false;
                            view.FindProperty("laserBeam").objectReferenceValue = line;
                        }
                        view.ApplyModifiedPropertiesWithoutUndo();
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(contents); }
                }
            }
            data.ApplyModifiedPropertiesWithoutUndo(); catalog.Validate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        internal static void EnsureFont()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/LoadResources/Fonts/Source/CN/HarmonyOS_CN.ttf");
            if (source == null) throw new InvalidOperationException("缺少项目中文源字体。");
            bool isNew = font == null;
            if (isNew) font = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            font.name = "HarmonyOS_CNHowToFish";
            var sources = Directory.GetFiles("Assets/Scripts/Hotfix/Demos/HowToFish", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/Scripts/Hotfix/Editor/HowToFish", "*.cs", SearchOption.AllDirectories));
            var text = string.Concat(sources.Select(File.ReadAllText));
            var characters = new string(text.Where(character => character >= 32 && !char.IsSurrogate(character)).Distinct().ToArray());
            font.TryAddCharacters(characters, out var missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning("[HowToFish] 字体仍缺少字符：" + missing);
            if (isNew) AssetDatabase.CreateAsset(font, FontPath);
            if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures)
            {
                if (!AssetDatabase.Contains(atlas)) AssetDatabase.AddObjectToAsset(atlas, font);
                EditorUtility.SetDirty(atlas);
            }
            EditorUtility.SetDirty(font);
        }

        /// 创建首岛的持久化装备、生物与输入资产；已有配置保留设计者的调整。
        [MenuItem("Tools/SleepyDemos/HowToFish/生成首岛资源")]
        public static void BuildFirstIslandAssets()
        {
            ConfigureModels();
            EnsureFolder(Root + "/Prefabs/Items");
            EnsureFolder(Root + "/Prefabs/Equipment");
            EnsureFolder(Root + "/Art/Materials");
            EnsureFolder(Root + "/Data");
            foreach (var name in new[] { "BrownCrab", "RockCrab", "Shrimp", "Lobster", "Clam", "SpiderCrab", "CrabRod", "Knife", "CrabMeat", "Beer", "HotDog", "Radar" })
                BuildWorldItem(name);
            BuildEquipment("CrabRod", "RodTip");
            BuildEquipment("Knife", "Strike");
            BuildEquipment("Beer", "Forward");
            BuildEquipment("Radar", "Forward");
            const string inputPath = Root + "/Data/PlayerInput.asset";
            if (AssetDatabase.LoadMainAssetAtPath(inputPath) == null)
                AssetDatabase.CreateAsset(HowToFishInput.CreateDefaultAsset(), inputPath);
            var input = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(inputPath);
            HowToFishInput.AddEquipmentBindings(input); EditorUtility.SetDirty(input);
            const string catalogPath = Root + "/Data/Catalog.asset";
            if (AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(catalogPath) == null)
            {
                var catalog = ScriptableObject.CreateInstance<HowToFishCatalog>();
                var data = new SerializedObject(catalog);
                var creatures = data.FindProperty("creatures");
                var rows = new[]
                {
                    ("BrownCrab", "棕蟹", "FreeLure", 3, 14f, 0.38f, 1f, HowToFishCreatureMotion.Crab),
                    ("RockCrab", "岩蟹", "HotDog", 7, 24f, 0.48f, 1.3f, HowToFishCreatureMotion.Crab),
                    ("Shrimp", "虾", "FreeLure", 5, 8f, 0.24f, 0.8f, HowToFishCreatureMotion.Fish),
                    ("Lobster", "龙虾", "HotDog", 9, 28f, 0.36f, 1.5f, HowToFishCreatureMotion.Crab),
                    ("Clam", "蛤蜊", "FreeLure", 1, 1f, 0.15f, 0.5f, HowToFishCreatureMotion.Shell),
                    ("SpiderCrab", "蜘蛛蟹", "EmptyBeerCan", 10000, 250f, 3f, 2.8f, HowToFishCreatureMotion.Charge)
                };
                creatures.arraySize = rows.Length;
                for (int i = 0; i < rows.Length; i++)
                {
                    var row = rows[i]; var entry = creatures.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("id").stringValue = row.Item1;
                    entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                    entry.FindPropertyRelative("island").intValue = 0;
                    var baits = entry.FindPropertyRelative("baits"); baits.arraySize = 1;
                    baits.GetArrayElementAtIndex(0).stringValue = row.Item3;
                    entry.FindPropertyRelative("weight").floatValue = 1;
                    entry.FindPropertyRelative("value").intValue = row.Item4;
                    entry.FindPropertyRelative("health").floatValue = row.Item5;
                    entry.FindPropertyRelative("length").floatValue = row.Item6;
                    entry.FindPropertyRelative("strength").floatValue = row.Item7;
                    entry.FindPropertyRelative("motion").enumValueIndex = (int)row.Item8;
                    entry.FindPropertyRelative("isBoss").boolValue = row.Item1 == "SpiderCrab";
                    entry.FindPropertyRelative("isGroundPickup").boolValue = row.Item1 == "Clam";
                    entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
                }
                var items = data.FindProperty("items");
                var equipment = new[]
                {
                    ("CrabRod", "钓竿", HowToFishItemKind.Rod, 3, false),
                    ("Knife", "小刀", HowToFishItemKind.Melee, 8, false),
                    ("FreeLure", "基础鱼饵", HowToFishItemKind.Bait, 0, false),
                    ("HotDog", "热狗", HowToFishItemKind.Bait, 1, true),
                    ("Beer", "啤酒", HowToFishItemKind.Food, 12, true),
                    ("EmptyBeerCan", "空啤酒罐", HowToFishItemKind.Quest, 0, true),
                    ("Radar", "雷达", HowToFishItemKind.Radar, 10, false)
                };
                items.arraySize = equipment.Length;
                for (int i = 0; i < equipment.Length; i++)
                {
                    var row = equipment[i]; var entry = items.GetArrayElementAtIndex(i);
                    entry.FindPropertyRelative("id").stringValue = row.Item1;
                    entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                    entry.FindPropertyRelative("kind").enumValueIndex = (int)row.Item3;
                    entry.FindPropertyRelative("island").intValue = 0;
                    entry.FindPropertyRelative("price").intValue = row.Item4;
                    entry.FindPropertyRelative("isConsumable").boolValue = row.Item5;
                    entry.FindPropertyRelative("damage").floatValue = row.Item1 == "Knife" ? 10 : 2;
                    entry.FindPropertyRelative("useInterval").floatValue = 0.42f;
                    entry.FindPropertyRelative("reloadSeconds").floatValue = 1.6f;
                    entry.FindPropertyRelative("range").floatValue = 3;
                    entry.FindPropertyRelative("nourishment").floatValue = 20;
                    entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
                    entry.FindPropertyRelative("viewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Equipment/" + row.Item1 + ".prefab");
                }
                data.ApplyModifiedPropertiesWithoutUndo();
                catalog.Validate();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            var existing = new SerializedObject(AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(catalogPath));
            var savedCreatures = existing.FindProperty("creatures");
            for (int i = 0; i < savedCreatures.arraySize; i++)
            {
                var entry = savedCreatures.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("id").stringValue == "SpiderCrab" && entry.FindPropertyRelative("value").intValue == 100)
                    entry.FindPropertyRelative("value").intValue = 10000; // 替换本次原型的临时售价，采用参考清单 S3 候选值。
            }
            var savedItems = existing.FindProperty("items");
            if (((HowToFishCatalog)existing.targetObject).FindItem("CrabMeat") == null)
            {
                int index = savedItems.arraySize++;
                var meat = savedItems.GetArrayElementAtIndex(index);
                meat.FindPropertyRelative("id").stringValue = "CrabMeat";
                meat.FindPropertyRelative("displayName").stringValue = "蟹肉";
                meat.FindPropertyRelative("kind").enumValueIndex = (int)HowToFishItemKind.Quest;
                meat.FindPropertyRelative("island").intValue = 0;
                meat.FindPropertyRelative("price").intValue = 0;
                meat.FindPropertyRelative("damage").floatValue = 0;
                meat.FindPropertyRelative("isConsumable").boolValue = true;
                meat.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/CrabMeat.prefab");
                meat.FindPropertyRelative("viewPrefab").objectReferenceValue = null;
            }
            for (int i = 0; i < savedItems.arraySize; i++)
            {
                var entry = savedItems.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("id").stringValue;
                if (id == "Beer" && entry.FindPropertyRelative("price").intValue == 2)
                    entry.FindPropertyRelative("price").intValue = 12; // 原作 1.0.2 的商店截图明确显示 $12。
                if (entry.FindPropertyRelative("prefab").objectReferenceValue == null)
                    entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + id + ".prefab");
                var view = entry.FindPropertyRelative("viewPrefab");
                if (view.objectReferenceValue == null)
                    view.objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Equipment/" + entry.FindPropertyRelative("id").stringValue + ".prefab");
            }
            existing.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[HowToFish] 首岛资源已创建；数值仍需按参考继续核对。");
        }

        /// 在既有目录追加森林任务和鱼获，不覆盖已调整的定义。
        internal static void BuildForestAssets()
        {
            foreach (var name in new[] { "Leech", "Piranha", "GiantPiranha", "PiranhaSkeleton", "Pistol", "Shotgun",
                "Mackerel", "Gar", "Pike", "Cod", "Goldfish", "Perch", "Triggerfish", "Goby", "Salmon", "FishingRod", "BeginnerLure" }) BuildWorldItem(name);
            BuildEquipment("Pistol", "Muzzle");
            BuildEquipment("Shotgun", "Muzzle");
            BuildEquipment("FishingRod", "RodTip");
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var data = new SerializedObject(catalog);
            var creatures = data.FindProperty("creatures");
            foreach (var row in new[] { ("Leech", "水蛭", "FreeLure", 1, 1f, .5f),
                ("Piranha", "食人鱼", "HotDog,BeginnerLure", 4, 24f, .5f), ("GiantPiranha", "巨型食人鱼", "ModifiedLeech", 10000, 400f, 2f),
                ("Mackerel", "鲭鱼", "FreeLure", 6, 12f, .8f), ("Gar", "雀鳝", "FreeLure", 5, 10f, 1f),
                ("Pike", "狗鱼", "FreeLure,BeginnerLure", 12, 22f, .9f), ("Cod", "鳕鱼", "BeginnerLure", 10, 24f, .75f),
                ("Goldfish", "金鱼", "FreeLure,BeginnerLure", 24, 40f, .55f), ("Perch", "河鲈", "BeginnerLure", 18, 24f, .65f),
                ("Triggerfish", "扳机鱼", "BeginnerLure", 18, 28f, .6f), ("Goby", "虾虎鱼", "BeginnerLure", 4, 10f, .55f),
                ("Salmon", "鲑鱼", "BeginnerLure", 14, 26f, .9f) })
            {
                if (catalog.FindCreature(row.Item1) != null) continue;
                var entry = creatures.GetArrayElementAtIndex(creatures.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("island").intValue = 1;
                var baitIds = row.Item3.Split(',');
                var baits = entry.FindPropertyRelative("baits"); baits.arraySize = baitIds.Length;
                for (int i = 0; i < baitIds.Length; i++) baits.GetArrayElementAtIndex(i).stringValue = baitIds[i];
                entry.FindPropertyRelative("weight").floatValue = 1;
                entry.FindPropertyRelative("value").intValue = row.Item4;
                entry.FindPropertyRelative("health").floatValue = row.Item5;
                entry.FindPropertyRelative("length").floatValue = row.Item6;
                entry.FindPropertyRelative("strength").floatValue = row.Item1 == "GiantPiranha" ? 3 : 1;
                entry.FindPropertyRelative("isBoss").boolValue = row.Item1 == "GiantPiranha";
                entry.FindPropertyRelative("isGroundPickup").boolValue = row.Item1 == "Leech";
                entry.FindPropertyRelative("requiredRodId").stringValue = row.Item1 == "Leech" || row.Item1 == "Piranha" ? "" : "FishingRod";
                entry.FindPropertyRelative("motion").enumValueIndex = (int)HowToFishCreatureMotion.Fish;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
            }
            for (int i = 0; i < creatures.arraySize; i++)
            {
                var entry = creatures.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("id").stringValue;
                var rod = entry.FindPropertyRelative("requiredRodId");
                if (string.IsNullOrEmpty(rod.stringValue) && !entry.FindPropertyRelative("isGroundPickup").boolValue)
                {
                    if (entry.FindPropertyRelative("island").intValue == 0) rod.stringValue = "CrabRod";
                    else if (id == "GiantPiranha") rod.stringValue = "FishingRod";
                }
                if (id != "Piranha") continue;
                var baits = entry.FindPropertyRelative("baits");
                if (!Enumerable.Range(0, baits.arraySize).Any(index => baits.GetArrayElementAtIndex(index).stringValue == "BeginnerLure"))
                    baits.GetArrayElementAtIndex(baits.arraySize++).stringValue = "BeginnerLure";
                if (entry.FindPropertyRelative("value").intValue == 18) entry.FindPropertyRelative("value").intValue = 4;
            }
            var items = data.FindProperty("items");
            foreach (var row in new[] { ("ModifiedLeech", "改造水蛭", HowToFishItemKind.Bait), ("PiranhaSkeleton", "巨型食人鱼骨架", HowToFishItemKind.Quest) })
            {
                if (catalog.FindItem(row.Item1) != null) continue;
                var entry = items.GetArrayElementAtIndex(items.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)row.Item3;
                entry.FindPropertyRelative("island").intValue = 1;
                entry.FindPropertyRelative("price").intValue = 0;
                entry.FindPropertyRelative("damage").floatValue = 0;
                entry.FindPropertyRelative("isConsumable").boolValue = true;
                entry.FindPropertyRelative("reloadSeconds").floatValue = 1;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = null;
            }
            foreach (var row in new[] { ("FishingRod", "普通鱼竿", HowToFishItemKind.Rod, 25), ("BeginnerLure", "初级鱼饵", HowToFishItemKind.Bait, 3) })
            {
                if (catalog.FindItem(row.Item1) != null) continue;
                var entry = items.GetArrayElementAtIndex(items.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)row.Item3;
                entry.FindPropertyRelative("island").intValue = 1;
                entry.FindPropertyRelative("price").intValue = row.Item4;
                entry.FindPropertyRelative("damage").floatValue = 2;
                entry.FindPropertyRelative("magazineSize").intValue = 0;
                entry.FindPropertyRelative("pellets").intValue = 1;
                entry.FindPropertyRelative("spread").floatValue = 0;
                entry.FindPropertyRelative("useInterval").floatValue = .5f;
                entry.FindPropertyRelative("range").floatValue = 3;
                entry.FindPropertyRelative("reloadSeconds").floatValue = 1;
                entry.FindPropertyRelative("isConsumable").boolValue = row.Item3 == HowToFishItemKind.Bait;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Equipment/" + row.Item1 + ".prefab");
            }
            foreach (var row in new[] { ("Pistol", "手枪", 50, 10, 1, .15f, 60f), ("Shotgun", "霰弹枪", 150, 2, 3, .22f, 35f) })
            {
                if (catalog.FindItem(row.Item1) != null) continue;
                var entry = items.GetArrayElementAtIndex(items.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)HowToFishItemKind.Gun;
                entry.FindPropertyRelative("island").intValue = 1;
                entry.FindPropertyRelative("price").intValue = row.Item3;
                entry.FindPropertyRelative("damage").floatValue = 25;
                entry.FindPropertyRelative("magazineSize").intValue = row.Item4;
                entry.FindPropertyRelative("pellets").intValue = row.Item5;
                entry.FindPropertyRelative("spread").floatValue = row.Item5 == 1 ? 0 : 4;
                entry.FindPropertyRelative("useInterval").floatValue = row.Item6;
                entry.FindPropertyRelative("range").floatValue = row.Item7;
                entry.FindPropertyRelative("reloadSeconds").floatValue = row.Item5 == 1 ? 1.4f : 1.8f;
                entry.FindPropertyRelative("isConsumable").boolValue = false;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Equipment/" + row.Item1 + ".prefab");
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            catalog.Validate();
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        /// 追加沙漠鱼池、任务道具及冲锋枪；已有条目保留人工数值。
        internal static void BuildDesertAssets()
        {
            var poisonPath = Root + "/Prefabs/Items/PoisonPool.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(poisonPath) == null)
            {
                var pool = new GameObject("PoisonPool", typeof(HowToFishDamagePool));
                try
                {
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/PoisonPool.fbx"));
                    visual.transform.SetParent(pool.transform, false);
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialFor).ToArray();
                    PrefabUtility.SaveAsPrefabAsset(pool, poisonPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(pool); }
            }
            foreach (var id in new[] { "Needlefish", "Seahorse", "Bowlfish", "YellowBoxfish", "Angelfish", "Catfish", "SeaUrchin", "Clownfish", "Bluegill", "BlueShark", "Pufferfish", "Carrot", "PufferfishFin", "FishMeat", "Lighter", "SMG", "StandardLure" })
                BuildWorldItem(id);
            BuildEquipment("SMG", "Muzzle");
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var data = new SerializedObject(catalog);
            var creatures = data.FindProperty("creatures");
            foreach (var row in new[] { ("Needlefish", "颌针鱼", "StandardLure", 60, 60f, 1.1f),
                ("Seahorse", "海马", "StandardLure", 100, 90f, .55f), ("Bowlfish", "鱼缸鱼", "StandardLure", 150, 160f, .75f),
                ("YellowBoxfish", "黄箱鲀", "StandardLure", 50, 60f, .45f),
                ("Angelfish", "神仙鱼", "StandardLure", 75, 70f, .9f), ("Catfish", "鲶鱼", "StandardLure", 90, 110f, 1.3f),
                ("SeaUrchin", "海胆", "StandardLure", 45, 50f, .55f), ("Clownfish", "小丑鱼", "StandardLure", 65, 65f, .7f),
                ("Bluegill", "蓝鳃太阳鱼", "StandardLure", 70, 85f, .8f),
                ("BlueShark", "蓝鲨", "StandardBossLure", 250, 600f, 3f), ("Pufferfish", "河豚", "Carrot", 10000, 1000f, 2f) })
            {
                if (catalog.FindCreature(row.Item1) != null) continue;
                var entry = creatures.GetArrayElementAtIndex(creatures.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("island").intValue = 2;
                entry.FindPropertyRelative("requiredRodId").stringValue = "FishingRod";
                var baits = entry.FindPropertyRelative("baits"); baits.arraySize = 1; baits.GetArrayElementAtIndex(0).stringValue = row.Item3;
                entry.FindPropertyRelative("weight").floatValue = 1;
                entry.FindPropertyRelative("value").intValue = row.Item4;
                entry.FindPropertyRelative("health").floatValue = row.Item5;
                entry.FindPropertyRelative("length").floatValue = row.Item6;
                bool boss = row.Item1 == "BlueShark" || row.Item1 == "Pufferfish";
                entry.FindPropertyRelative("strength").floatValue = boss ? 3.5f : 1.4f;
                entry.FindPropertyRelative("isBoss").boolValue = boss;
                entry.FindPropertyRelative("isGroundPickup").boolValue = false;
                entry.FindPropertyRelative("isEndangered").boolValue = row.Item1 == "Needlefish" || row.Item1 == "Seahorse" || row.Item1 == "Bowlfish";
                entry.FindPropertyRelative("motion").enumValueIndex = (int)HowToFishCreatureMotion.Fish;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
            }
            var items = data.FindProperty("items");
            foreach (var row in new[] { ("StandardLure", "标准鱼饵", HowToFishItemKind.Bait, 15),
                ("StandardBossLure", "标准首领鱼饵", HowToFishItemKind.Bait, 280),
                ("Carrot", "胡萝卜", HowToFishItemKind.Bait, 0), ("PufferfishFin", "河豚鱼鳍", HowToFishItemKind.Quest, 0),
                ("FishMeat", "鱼肉块", HowToFishItemKind.Food, 0), ("Lighter", "打火机", HowToFishItemKind.Quest, 0),
                ("SMG", "冲锋枪", HowToFishItemKind.Gun, 650) })
            {
                if (catalog.FindItem(row.Item1) != null) continue;
                var entry = items.GetArrayElementAtIndex(items.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)row.Item3;
                entry.FindPropertyRelative("island").intValue = 2;
                entry.FindPropertyRelative("price").intValue = row.Item4;
                bool gun = row.Item3 == HowToFishItemKind.Gun;
                entry.FindPropertyRelative("damage").floatValue = gun ? 20 : 0;
                entry.FindPropertyRelative("magazineSize").intValue = gun ? 30 : 0;
                entry.FindPropertyRelative("pellets").intValue = 1;
                entry.FindPropertyRelative("spread").floatValue = gun ? 1.2f : 0;
                entry.FindPropertyRelative("useInterval").floatValue = gun ? .09f : .5f;
                entry.FindPropertyRelative("range").floatValue = gun ? 55 : 3;
                entry.FindPropertyRelative("reloadSeconds").floatValue = 1.8f;
                entry.FindPropertyRelative("nourishment").floatValue = 30;
                entry.FindPropertyRelative("automatic").boolValue = gun;
                entry.FindPropertyRelative("isCookable").boolValue = row.Item1 == "FishMeat";
                entry.FindPropertyRelative("isConsumable").boolValue = row.Item3 == HowToFishItemKind.Bait || row.Item3 == HowToFishItemKind.Food;
                string model = row.Item1 == "StandardBossLure" ? "StandardLure" : row.Item1;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + model + ".prefab");
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = gun ? AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Equipment/SMG.prefab") : null;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            catalog.Validate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        /// 追加岩石岛主线、专业鱼池及狙击枪。
        internal static void BuildRocksAssets()
        {
            var droppingPath = Root + "/Prefabs/Items/BirdDropping.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(droppingPath) == null)
            {
                var drop = new GameObject("BirdDropping", typeof(HowToFishProjectile));
                try
                {
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/BirdDropping.fbx"));
                    visual.transform.SetParent(drop.transform, false);
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialFor).ToArray();
                    PrefabUtility.SaveAsPrefabAsset(drop, droppingPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(drop); }
            }
            foreach (var id in new[] { "Tuna", "Albatross", "AlbatrossHead", "SniperRifle", "ProfessionalLure" }) BuildWorldItem(id);
            var regularFish = new[] { ("Bass", "鲈鱼", 250, 120f, 1.14f), ("RedSnapper", "红鲷", 280, 140f, 1.08f),
                ("Parrotfish", "鹦嘴鱼", 350, 180f, 1.05f), ("Tigerfish", "虎鱼", 310, 180f, 1.3f),
                ("FlyingFish", "飞鱼", 320, 80f, 1.12f), ("Sengarat", "Sengarat", 280, 150f, 1.48f),
                ("Eel", "鳗鱼", 280, 120f, 1.8f), ("Halibut", "大比目鱼", 290, 220f, 1.32f),
                ("Voxelfish", "方块鱼", 340, 200f, .89f), ("Dripper", "球鞋鱼", 350, 250f, .64f) };
            foreach (var row in regularFish) BuildWorldItem(row.Item1);
            BuildEquipment("SniperRifle", "Muzzle");
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var data = new SerializedObject(catalog);
            var creatures = data.FindProperty("creatures");
            foreach (var row in new[] { ("Tuna", "金枪鱼", 600f, 2.6f, 800), ("Albatross", "信天翁", 1800f, 4.4f, 5000) })
            {
                if (catalog.FindCreature(row.Item1) != null) continue;
                var entry = creatures.GetArrayElementAtIndex(creatures.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("island").intValue = 3;
                entry.FindPropertyRelative("requiredRodId").stringValue = "FishingRod";
                var baits = entry.FindPropertyRelative("baits"); baits.arraySize = row.Item1 == "Tuna" ? 1 : 0;
                if (baits.arraySize > 0) baits.GetArrayElementAtIndex(0).stringValue = "ProfessionalBossLure";
                entry.FindPropertyRelative("weight").floatValue = 1;
                entry.FindPropertyRelative("value").intValue = row.Item5;
                entry.FindPropertyRelative("health").floatValue = row.Item3;
                entry.FindPropertyRelative("length").floatValue = row.Item4;
                entry.FindPropertyRelative("strength").floatValue = 3.5f;
                entry.FindPropertyRelative("isBoss").boolValue = true;
                entry.FindPropertyRelative("isGroundPickup").boolValue = row.Item1 == "Albatross";
                entry.FindPropertyRelative("isEndangered").boolValue = false;
                entry.FindPropertyRelative("motion").enumValueIndex = (int)(row.Item1 == "Tuna" ? HowToFishCreatureMotion.Leaping : HowToFishCreatureMotion.Flying);
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
            }
            foreach (var row in regularFish)
            {
                if (catalog.FindCreature(row.Item1) != null) continue;
                var entry = creatures.GetArrayElementAtIndex(creatures.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("island").intValue = 3;
                entry.FindPropertyRelative("requiredRodId").stringValue = "FishingRod";
                var baits = entry.FindPropertyRelative("baits"); baits.arraySize = 1; baits.GetArrayElementAtIndex(0).stringValue = "ProfessionalLure";
                entry.FindPropertyRelative("weight").floatValue = 1;
                entry.FindPropertyRelative("value").intValue = row.Item3;
                entry.FindPropertyRelative("health").floatValue = row.Item4;
                entry.FindPropertyRelative("length").floatValue = row.Item5;
                entry.FindPropertyRelative("strength").floatValue = 2;
                entry.FindPropertyRelative("isBoss").boolValue = false;
                entry.FindPropertyRelative("isGroundPickup").boolValue = false;
                entry.FindPropertyRelative("isEndangered").boolValue = row.Item1 == "Voxelfish" || row.Item1 == "Dripper";
                entry.FindPropertyRelative("motion").enumValueIndex = (int)(row.Item1 == "Eel" ? HowToFishCreatureMotion.Eel : HowToFishCreatureMotion.Fish);
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
            }
            var items = data.FindProperty("items");
            foreach (var row in new[] { ("ProfessionalLure", "专业鱼饵", HowToFishItemKind.Bait, 50),
                ("ProfessionalBossLure", "专业首领鱼饵", HowToFishItemKind.Bait, 1200),
                ("AlbatrossHead", "信天翁头", HowToFishItemKind.Quest, 0), ("SniperRifle", "狙击步枪", HowToFishItemKind.Gun, 3800) })
            {
                if (catalog.FindItem(row.Item1) != null) continue;
                var entry = items.GetArrayElementAtIndex(items.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)row.Item3;
                entry.FindPropertyRelative("island").intValue = 3;
                entry.FindPropertyRelative("price").intValue = row.Item4;
                bool gun = row.Item3 == HowToFishItemKind.Gun;
                entry.FindPropertyRelative("damage").floatValue = gun ? 200 : 0;
                entry.FindPropertyRelative("magazineSize").intValue = gun ? 5 : 0;
                entry.FindPropertyRelative("pellets").intValue = 1;
                entry.FindPropertyRelative("spread").floatValue = 0;
                entry.FindPropertyRelative("useInterval").floatValue = gun ? 1 : .5f;
                entry.FindPropertyRelative("range").floatValue = gun ? 180 : 3;
                entry.FindPropertyRelative("reloadSeconds").floatValue = 2.2f;
                entry.FindPropertyRelative("nourishment").floatValue = 20;
                entry.FindPropertyRelative("automatic").boolValue = false;
                entry.FindPropertyRelative("isCookable").boolValue = false;
                entry.FindPropertyRelative("isConsumable").boolValue = row.Item3 == HowToFishItemKind.Bait;
                string model = row.Item1 == "ProfessionalBossLure" ? "ProfessionalLure" : row.Item1;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + model + ".prefab");
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = gun ? AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Equipment/SniperRifle.prefab") : null;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            catalog.Validate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        /// 追加火山普通鱼、两阶段鲸鱼与科学家任务资产；已有配置保留人工调整。
        internal static void BuildVolcanoAssets()
        {
            foreach (var id in new[] { "LavaPool", "LavaGlob" })
            {
                string path = Root + "/Prefabs/Items/" + id + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
                var root = new GameObject(id);
                try
                {
                    var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" + id + ".fbx"));
                    visual.transform.SetParent(root.transform, false);
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialFor).ToArray();
                    var data = new SerializedObject(id == "LavaPool" ? (Component)root.AddComponent<HowToFishDamagePool>() : root.AddComponent<HowToFishProjectile>());
                    if (id == "LavaPool")
                    {
                        data.FindProperty("damagePerSecond").floatValue = 12;
                        data.FindProperty("duration").floatValue = 7;
                    }
                    else
                    {
                        data.FindProperty("damage").floatValue = 16;
                        data.FindProperty("radius").floatValue = .22f;
                        data.FindProperty("impactPool").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/LavaPool.prefab").GetComponent<HowToFishDamagePool>();
                    }
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            var fish = new[] { ("Blobfish", "水滴鱼", 750, 160f, .8f), ("Anglerfish", "鮟鱇鱼", 850, 190f, 1.1f),
                ("Oarfish", "皇带鱼", 1200, 220f, 3.2f), ("Stonefish", "石头鱼", 900, 260f, .8f),
                ("SuperdwarfFish", "超级侏儒鱼", 1800, 80f, .22f), ("FootSnail", "脚蜗牛", 15, 30f, .4f),
                ("BowheadWhale", "弓头鲸", 9000, 3000f, 6f), ("MutatedBowheadWhale", "变异弓头鲸", 18000, 6000f, 8.1f) };
            foreach (var row in fish) BuildWorldItem(row.Item1);
            foreach (var id in new[] { "FishBucket", "WhaleFin", "MilitaryBoatKey", "ScientificLure", "AssaultRifle" }) BuildWorldItem(id);
            BuildEquipment("AssaultRifle", "Muzzle");
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var settings = new SerializedObject(catalog);
            var items = settings.FindProperty("items");
            foreach (var row in new[] { ("ScientificLure", "科学鱼饵", HowToFishItemKind.Bait, 500),
                ("FishBucket", "鱼桶", HowToFishItemKind.Bait, 0), ("WhaleFin", "鲸鱼鳍", HowToFishItemKind.Quest, 0),
                ("MilitaryBoatKey", "军用船钥匙", HowToFishItemKind.Quest, 0), ("AssaultRifle", "突击步枪", HowToFishItemKind.Gun, 20000) })
            {
                if (catalog.FindItem(row.Item1) != null) continue;
                var entry = items.GetArrayElementAtIndex(items.arraySize++);
                bool gun = row.Item3 == HowToFishItemKind.Gun;
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)row.Item3;
                entry.FindPropertyRelative("island").intValue = 4;
                entry.FindPropertyRelative("price").intValue = row.Item4;
                entry.FindPropertyRelative("damage").floatValue = gun ? 40 : 0;
                entry.FindPropertyRelative("magazineSize").intValue = gun ? 30 : 0;
                entry.FindPropertyRelative("pellets").intValue = 1;
                entry.FindPropertyRelative("spread").floatValue = gun ? .7f : 0;
                entry.FindPropertyRelative("useInterval").floatValue = gun ? .1f : .5f;
                entry.FindPropertyRelative("range").floatValue = gun ? 90 : 3;
                entry.FindPropertyRelative("reloadSeconds").floatValue = 1.8f;
                entry.FindPropertyRelative("automatic").boolValue = gun;
                entry.FindPropertyRelative("isCookable").boolValue = false;
                entry.FindPropertyRelative("isConsumable").boolValue = row.Item3 == HowToFishItemKind.Bait;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = gun ? AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Equipment/AssaultRifle.prefab") : null;
            }
            var creatures = settings.FindProperty("creatures");
            foreach (var row in fish)
            {
                if (catalog.FindCreature(row.Item1) != null) continue;
                var entry = creatures.GetArrayElementAtIndex(creatures.arraySize++);
                bool boss = row.Item1.Contains("Whale"), ground = row.Item1 == "FootSnail" || row.Item1 == "MutatedBowheadWhale";
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("island").intValue = 4;
                entry.FindPropertyRelative("requiredRodId").stringValue = ground ? "" : "FishingRod";
                var baits = entry.FindPropertyRelative("baits"); baits.arraySize = ground ? 0 : 1;
                if (!ground) baits.GetArrayElementAtIndex(0).stringValue = boss ? "FishBucket" : "ScientificLure";
                entry.FindPropertyRelative("weight").floatValue = 1;
                entry.FindPropertyRelative("value").intValue = row.Item3;
                entry.FindPropertyRelative("health").floatValue = row.Item4;
                entry.FindPropertyRelative("length").floatValue = row.Item5;
                entry.FindPropertyRelative("strength").floatValue = boss ? 4 : 2;
                entry.FindPropertyRelative("isBoss").boolValue = boss;
                entry.FindPropertyRelative("isGroundPickup").boolValue = ground;
                entry.FindPropertyRelative("isEndangered").boolValue = row.Item1 == "Oarfish" || row.Item1 == "SuperdwarfFish";
                entry.FindPropertyRelative("motion").enumValueIndex = (int)(!boss ? HowToFishCreatureMotion.Fish :
                    row.Item1 == "BowheadWhale" ? HowToFishCreatureMotion.Whale : HowToFishCreatureMotion.MagmaWhale);
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            catalog.Validate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        private static void BuildEquipment(string name, string tipName)
        {
            var path = Root + "/Prefabs/Equipment/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                var existing = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    EnsureHand(existing, name);
                    PrefabUtility.SaveAsPrefabAsset(existing, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(existing); }
                return;
            }
            var root = new GameObject(name + "View");
            try
            {
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" + name + ".fbx"));
                model.transform.SetParent(root.transform, false);
                foreach (var renderer in model.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialFor).ToArray();
                var view = root.AddComponent<HowToFishEquipmentView>();
                var settings = new SerializedObject(view);
                settings.FindProperty("tip").objectReferenceValue = model.GetComponentsInChildren<Transform>().Single(t => t.name == tipName);
                if (name == "Pistol" || name == "Shotgun" || name == "SMG" || name == "SniperRifle")
                {
                    settings.FindProperty("reloadPart").objectReferenceValue = model.GetComponentsInChildren<Transform>().Single(t => t.name == (name == "Shotgun" ? "Breech" : "Magazine"));
                    settings.FindProperty("hingedReload").boolValue = name == "Shotgun";
                    var flash = new GameObject("MuzzleFlash", typeof(Light)).GetComponent<Light>();
                    flash.transform.SetParent((Transform)settings.FindProperty("tip").objectReferenceValue, false);
                    flash.color = new Color(1, .72f, .22f); flash.intensity = 2; flash.range = 1.5f; flash.enabled = false;
                    settings.FindProperty("muzzleFlash").objectReferenceValue = flash;
                }
                settings.ApplyModifiedPropertiesWithoutUndo();
                root.transform.localPosition = new Vector3(0.29f, -0.25f, 0.42f);
                root.transform.localRotation = Quaternion.Euler(-8, -12, -12);
                EnsureHand(root, name);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        /// 补齐可选鱼类、专用鱼饵和初级饵的稀有翻车鱼权重。
        internal static void BuildOptionalAssets()
        {
            foreach (string id in new[] { "Sunfish", "OldPike", "GoblinShark", "BeginnerBossLure", "ScientificBossLure" }) BuildWorldItem(id);
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var data = new SerializedObject(catalog);
            var items = data.FindProperty("items");
            foreach (var row in new[] { ("BeginnerBossLure", "初级首领鱼饵", 1, 40), ("ScientificBossLure", "科学首领鱼饵", 4, 5800) })
            {
                if (catalog.FindItem(row.Item1) != null) continue;
                var entry = items.GetArrayElementAtIndex(items.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)HowToFishItemKind.Bait;
                entry.FindPropertyRelative("island").intValue = row.Item3;
                entry.FindPropertyRelative("price").intValue = row.Item4;
                entry.FindPropertyRelative("damage").floatValue = 0;
                entry.FindPropertyRelative("magazineSize").intValue = 0;
                entry.FindPropertyRelative("pellets").intValue = 1;
                entry.FindPropertyRelative("spread").floatValue = 0;
                entry.FindPropertyRelative("useInterval").floatValue = .5f;
                entry.FindPropertyRelative("range").floatValue = 3;
                entry.FindPropertyRelative("reloadSeconds").floatValue = 1;
                entry.FindPropertyRelative("isConsumable").boolValue = true;
                entry.FindPropertyRelative("isCookable").boolValue = false;
                entry.FindPropertyRelative("automatic").boolValue = false;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = null;
            }
            var creatures = data.FindProperty("creatures");
            foreach (var row in new[] { ("Sunfish", "翻车鱼", 1, 50, 1500f, 3.4f),
                ("OldPike", "老狗鱼", 1, 80, 1000f, 3.7f), ("GoblinShark", "哥布林鲨", 4, 6200, 7500f, 4.2f) })
            {
                if (catalog.FindCreature(row.Item1) != null) continue;
                var entry = creatures.GetArrayElementAtIndex(creatures.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("island").intValue = row.Item3;
                entry.FindPropertyRelative("requiredRodId").stringValue = "FishingRod";
                bool sunfish = row.Item1 == "Sunfish";
                var baits = entry.FindPropertyRelative("baits"); baits.arraySize = sunfish ? 2 : 1;
                baits.GetArrayElementAtIndex(0).stringValue = row.Item3 == 1 ? "BeginnerBossLure" : "ScientificBossLure";
                if (sunfish) baits.GetArrayElementAtIndex(1).stringValue = "BeginnerLure";
                var weights = entry.FindPropertyRelative("baitWeights"); weights.arraySize = sunfish ? 2 : 0;
                if (sunfish) { weights.GetArrayElementAtIndex(0).floatValue = 10; weights.GetArrayElementAtIndex(1).floatValue = 1; }
                entry.FindPropertyRelative("weight").floatValue = 10;
                entry.FindPropertyRelative("value").intValue = row.Item4;
                entry.FindPropertyRelative("health").floatValue = row.Item5;
                entry.FindPropertyRelative("length").floatValue = row.Item6;
                entry.FindPropertyRelative("strength").floatValue = row.Item3 == 1 ? 2.5f : 4;
                entry.FindPropertyRelative("isBoss").boolValue = true;
                entry.FindPropertyRelative("isMiniBoss").boolValue = true;
                entry.FindPropertyRelative("isGroundPickup").boolValue = false;
                entry.FindPropertyRelative("isEndangered").boolValue = false;
                entry.FindPropertyRelative("motion").enumValueIndex = (int)HowToFishCreatureMotion.Leaping;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
            }
            for (int i = 0; i < creatures.arraySize; i++)
            {
                var entry = creatures.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("id").stringValue;
                if (id == "BlueShark" || id == "Tuna") entry.FindPropertyRelative("isMiniBoss").boolValue = true;
                int weight = id == "Cod" || id == "Pike" || id == "Piranha" ? 10 : id == "Salmon" ? 7 :
                    id == "Goldfish" ? 4 : id == "Triggerfish" || id == "Goby" || id == "Perch" ? 5 : 0;
                if (weight == 0) continue;
                var baits = entry.FindPropertyRelative("baits");
                var weights = entry.FindPropertyRelative("baitWeights");
                if (weights.arraySize > 0) continue;
                weights.arraySize = baits.arraySize;
                for (int b = 0; b < baits.arraySize; b++)
                    weights.GetArrayElementAtIndex(b).floatValue = baits.GetArrayElementAtIndex(b).stringValue == "BeginnerLure" ? weight : entry.FindPropertyRelative("weight").floatValue;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            catalog.Validate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        /// 环境海鸥、椰子特殊鱼和死亡遗体；不把它们计入 49 项鱼类图鉴。
        internal static void BuildWildlifeAssets()
        {
            foreach (string id in new[] { "BingBong", "Seagull", "Coconut", "PlayerRemains" }) BuildWorldItem(id);
            var catalog = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset");
            var data = new SerializedObject(catalog);
            var items = data.FindProperty("items");
            foreach (var row in new[] { ("Coconut", "椰子", HowToFishItemKind.Bait, 2, 350), ("PlayerRemains", "你的遗体", HowToFishItemKind.Quest, 0, 0) })
            {
                if (catalog.FindItem(row.Item1) != null) continue;
                var entry = items.GetArrayElementAtIndex(items.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("kind").enumValueIndex = (int)row.Item3;
                entry.FindPropertyRelative("island").intValue = row.Item4;
                entry.FindPropertyRelative("price").intValue = row.Item5;
                entry.FindPropertyRelative("damage").floatValue = 0;
                entry.FindPropertyRelative("magazineSize").intValue = 0;
                entry.FindPropertyRelative("pellets").intValue = 1;
                entry.FindPropertyRelative("spread").floatValue = 0;
                entry.FindPropertyRelative("useInterval").floatValue = .5f;
                entry.FindPropertyRelative("range").floatValue = 3;
                entry.FindPropertyRelative("reloadSeconds").floatValue = 1;
                entry.FindPropertyRelative("isConsumable").boolValue = row.Item3 == HowToFishItemKind.Bait;
                entry.FindPropertyRelative("isCookable").boolValue = false;
                entry.FindPropertyRelative("automatic").boolValue = false;
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
                entry.FindPropertyRelative("viewPrefab").objectReferenceValue = null;
            }
            var creatures = data.FindProperty("creatures");
            foreach (var row in new[] { ("BingBong", "Bing Bong", 2, 300, 2000f, 1f), ("Seagull", "海鸥", 0, 5, 30f, 1.6f) })
            {
                if (catalog.FindCreature(row.Item1) != null) continue;
                bool bird = row.Item1 == "Seagull";
                var entry = creatures.GetArrayElementAtIndex(creatures.arraySize++);
                entry.FindPropertyRelative("id").stringValue = row.Item1;
                entry.FindPropertyRelative("displayName").stringValue = row.Item2;
                entry.FindPropertyRelative("island").intValue = row.Item3;
                entry.FindPropertyRelative("requiredRodId").stringValue = bird ? "" : "FishingRod";
                var baits = entry.FindPropertyRelative("baits"); baits.arraySize = bird ? 0 : 1;
                if (!bird) baits.GetArrayElementAtIndex(0).stringValue = "Coconut";
                entry.FindPropertyRelative("baitWeights").arraySize = 0;
                entry.FindPropertyRelative("weight").floatValue = 1;
                entry.FindPropertyRelative("value").intValue = row.Item4;
                entry.FindPropertyRelative("health").floatValue = row.Item5;
                entry.FindPropertyRelative("length").floatValue = row.Item6;
                entry.FindPropertyRelative("strength").floatValue = 2;
                entry.FindPropertyRelative("isBoss").boolValue = false;
                entry.FindPropertyRelative("isMiniBoss").boolValue = false;
                entry.FindPropertyRelative("excludeFromJournal").boolValue = true;
                entry.FindPropertyRelative("isGroundPickup").boolValue = bird;
                entry.FindPropertyRelative("isEndangered").boolValue = !bird;
                entry.FindPropertyRelative("motion").enumValueIndex = (int)(bird ? HowToFishCreatureMotion.Flying : HowToFishCreatureMotion.Leaping);
                entry.FindPropertyRelative("prefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/" + row.Item1 + ".prefab");
            }
            for (int i = 0; i < creatures.arraySize; i++)
            {
                var entry = creatures.GetArrayElementAtIndex(i);
                string id = entry.FindPropertyRelative("id").stringValue;
                if (id == "Clam" || id == "Leech" || id == "FootSnail") entry.FindPropertyRelative("excludeFromJournal").boolValue = true;
            }
            data.ApplyModifiedPropertiesWithoutUndo();
            catalog.Validate(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
        }

        private static void EnsureHand(GameObject root, string equipment)
        {
            if (root.transform.Find("HandVisual") != null) return;
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/RightHand.fbx");
            if (model == null) throw new InvalidOperationException("缺少第一人称手部模型。");
            var hand = (GameObject)PrefabUtility.InstantiatePrefab(model);
            hand.name = "HandVisual";
            hand.transform.SetParent(root.transform, false);
            hand.transform.localPosition = equipment == "Knife" ? Vector3.back * .07f : Vector3.zero;
            foreach (var renderer in hand.GetComponentsInChildren<Renderer>()) renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialFor).ToArray();
        }

        private static void BuildWorldItem(string name)
        {
            var prefabPath = Root + "/Prefabs/Items/" + name + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null)
            {
                if (name == "SpiderCrab" || name == "Albatross")
                {
                    var existing = PrefabUtility.LoadPrefabContents(prefabPath);
                    try
                    {
                        if (name == "SpiderCrab" && existing.GetComponent<HowToFishSpiderCrab>() == null)
                        {
                            existing.AddComponent<HowToFishSpiderCrab>();
                            PrefabUtility.SaveAsPrefabAsset(existing, prefabPath);
                        }
                        if (name == "Albatross") { EnsureBirdHitboxes(existing); PrefabUtility.SaveAsPrefabAsset(existing, prefabPath); }
                    }
                    finally { PrefabUtility.UnloadPrefabContents(existing); }
                }
                return;
            }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" + name + ".fbx");
            if (model == null) throw new InvalidOperationException("缺少已验证模型：" + name);
            var root = new GameObject(name);
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                visual.transform.SetParent(root.transform, false);
                visual.name = "Visual";
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(MaterialFor).ToArray();
                var rigidbody = root.AddComponent<Rigidbody>();
                rigidbody.mass = name == "SpiderCrab" ? 80 : 0.7f;
                rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                var bodyMeshes = visual.GetComponentsInChildren<MeshFilter>().Where(mesh =>
                    mesh.name == "Carapace" || mesh.name == "Body" || mesh.name == "Head" ||
                    mesh.name.StartsWith("Segment", StringComparison.Ordinal) || mesh.name.StartsWith("Shell", StringComparison.Ordinal) ||
                    (name == "Dripper" && mesh.name.StartsWith("Sole", StringComparison.Ordinal)) ||
                    (name == "GoblinShark" && mesh.name == "LongSnout") || (name == "OldPike" && mesh.name == "UpperBill")).ToArray();
                if (name == "BingBong" || name == "PlayerRemains") bodyMeshes = Array.Empty<MeshFilter>();
                var collider = root.AddComponent<BoxCollider>();
                if (bodyMeshes.Length == 0)
                {
                    var bounds = new Bounds(visual.transform.position, Vector3.zero);
                    foreach (var renderer in visual.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                    collider.center = bounds.center; collider.size = Vector3.Max(bounds.size, Vector3.one * 0.025f);
                }
                else
                {
                    var bounds = bodyMeshes[0].GetComponent<Renderer>().bounds;
                    foreach (var mesh in bodyMeshes.Skip(1)) bounds.Encapsulate(mesh.GetComponent<Renderer>().bounds);
                    collider.center = bounds.center;
                    collider.size = Vector3.Max(bounds.size, Vector3.one * 0.03f);
                }
                var component = root.AddComponent<HowToFishWorldItem>();
                if (name == "SpiderCrab") root.AddComponent<HowToFishSpiderCrab>();
                if (name == "Sunfish" || name == "OldPike" || name == "GoblinShark" || name == "BingBong")
                {
                    rigidbody.mass = name == "BingBong" ? 5 : name == "Sunfish" ? 84 : 70;
                    var behavior = new SerializedObject(root.AddComponent<HowToFishJumpingFish>());
                    behavior.FindProperty("tail").objectReferenceValue = visual.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Tail");
                    behavior.FindProperty("damage").floatValue = name == "BingBong" ? 75 : name == "Sunfish" ? 0 : 25;
                    behavior.FindProperty("damageInterval").floatValue = name == "GoblinShark" ? .5f : 1;
                    behavior.FindProperty("speed").floatValue = name == "BingBong" ? 7 : name == "Sunfish" ? 3 : name == "GoblinShark" ? 9 : 6;
                    if (name == "BingBong")
                    {
                        behavior.FindProperty("propeller").objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Propeller");
                        var limbs = behavior.FindProperty("limbs"); limbs.arraySize = 4;
                        string[] names = { "ArmLeft", "ArmRight", "LegLeft", "LegRight" };
                        for (int i = 0; i < names.Length; i++) limbs.GetArrayElementAtIndex(i).objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == names[i]);
                        behavior.FindProperty("jumpForce").floatValue = 10;
                        behavior.FindProperty("jumpInterval").floatValue = .1f;
                        behavior.FindProperty("steerInAir").boolValue = false;
                    }
                    behavior.ApplyModifiedPropertiesWithoutUndo();
                    foreach (var fin in visual.GetComponentsInChildren<MeshFilter>().Where(value => value.name.StartsWith("LongFin", StringComparison.Ordinal)))
                    {
                        var shape = fin.gameObject.AddComponent<BoxCollider>(); shape.center = fin.sharedMesh.bounds.center;
                        shape.size = Vector3.Max(fin.sharedMesh.bounds.size, Vector3.one * .1f);
                    }
                }
                if (name == "PlayerRemains") rigidbody.mass = 75;
                if (name == "Seagull")
                {
                    rigidbody.mass = 1.5f;
                    EnsureBirdHitboxes(root);
                    var bird = new SerializedObject(root.AddComponent<HowToFishSeagull>());
                    foreach (var binding in new[] { ("leftWing", "WingLeft"), ("rightWing", "WingRight"), ("carryPoint", "CarryPoint") })
                        bird.FindProperty(binding.Item1).objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == binding.Item2);
                    bird.ApplyModifiedPropertiesWithoutUndo();
                }
                if (name == "GiantPiranha") { rigidbody.mass = 45; root.AddComponent<HowToFishGiantPiranha>(); }
                if (name == "Tuna")
                {
                    rigidbody.mass = 20;
                    var boss = new SerializedObject(root.AddComponent<HowToFishTuna>());
                    boss.FindProperty("tail").objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Tail");
                    boss.ApplyModifiedPropertiesWithoutUndo();
                }
                if (name == "Albatross")
                {
                    rigidbody.mass = 8;
                    EnsureBirdHitboxes(root);
                    var boss = new SerializedObject(root.AddComponent<HowToFishAlbatross>());
                    boss.FindProperty("leftWing").objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "WingLeft");
                    boss.FindProperty("rightWing").objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "WingRight");
                    boss.FindProperty("droppingPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/BirdDropping.prefab").GetComponent<HowToFishProjectile>();
                    boss.ApplyModifiedPropertiesWithoutUndo();
                }
                if (name == "BowheadWhale" || name == "MutatedBowheadWhale")
                {
                    rigidbody.mass = name == "BowheadWhale" ? 400 : 650;
                    var boss = new SerializedObject(root.AddComponent<HowToFishWhale>());
                    boss.FindProperty("tail").objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Tail");
                    boss.FindProperty("blowhole").objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Blowhole");
                    if (name == "MutatedBowheadWhale")
                    {
                        boss.FindProperty("lavaPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/LavaGlob.prefab").GetComponent<HowToFishProjectile>();
                        boss.FindProperty("escapeSeconds").floatValue = 210;
                        boss.FindProperty("chargeSpeed").floatValue = 14;
                        boss.FindProperty("chargeDamage").floatValue = 34;
                        boss.FindProperty("slamDamage").floatValue = 44;
                    }
                    boss.ApplyModifiedPropertiesWithoutUndo();
                }
                if (name == "BlueShark")
                {
                    rigidbody.mass = 30;
                    var boss = new SerializedObject(root.AddComponent<HowToFishBlueShark>());
                    boss.FindProperty("rollRoot").objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "RollPivot");
                    boss.ApplyModifiedPropertiesWithoutUndo();
                }
                if (name == "Pufferfish")
                {
                    rigidbody.mass = 60;
                    UnityEngine.Object.DestroyImmediate(collider);
                    var sphere = root.AddComponent<SphereCollider>(); sphere.center = Vector3.up * .9f; sphere.radius = .95f;
                    var boss = new SerializedObject(root.AddComponent<HowToFishPufferfish>());
                    boss.FindProperty("ball").objectReferenceValue = visual.GetComponentsInChildren<Transform>().Single(t => t.name == "Ball");
                    boss.FindProperty("poisonPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/Items/PoisonPool.prefab").GetComponent<HowToFishDamagePool>();
                    boss.ApplyModifiedPropertiesWithoutUndo();
                }
                var serialized = new SerializedObject(component);
                serialized.FindProperty("definitionId").stringValue = name;
                serialized.FindProperty("visualRoot").objectReferenceValue = visual.transform;
                serialized.FindProperty("hookPoint").objectReferenceValue = visual.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Hook");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void EnsureBirdHitboxes(GameObject root)
        {
            foreach (var mesh in root.GetComponentsInChildren<MeshFilter>().Where(value =>
                value.name.StartsWith("WingSurface", StringComparison.Ordinal) || value.name == "Skull"))
            {
                if (mesh.GetComponent<Collider>() != null) continue;
                var shape = mesh.gameObject.AddComponent<BoxCollider>();
                shape.center = mesh.sharedMesh.bounds.center;
                shape.size = Vector3.Max(mesh.sharedMesh.bounds.size, Vector3.one * .12f);
            }
        }

        internal static Material MaterialFor(Material original)
        {
            if (original == null) throw new InvalidOperationException("模型存在未指定材质槽。");
            var path = Root + "/Art/Materials/" + original.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = original.name, enableInstancing = true };
            material.SetColor("_BaseColor", original.color);
            material.SetFloat("_Smoothness", 0.18f);
            if (original.name.StartsWith("VolcanoLava", StringComparison.Ordinal))
            { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", original.color * 3); }
            if (original.color.a < .99f)
            {
                material.SetFloat("_Surface", 1);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        internal static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// 对当前 Demo 模型应用统一导入设置，并保留已有 GUID。
        [MenuItem("Tools/SleepyDemos/HowToFish/配置模型导入")]
        public static void ConfigureModels()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请在 Edit Mode 配置模型。");
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root + "/Art/Models" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                importer.globalScale = 1;
                importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importAnimation = true;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.isReadable = false;
                importer.SaveAndReimport();
            }
            Debug.Log("[HowToFish] 模型导入配置完成，接下来运行模型契约测试。");
        }
    }
}
