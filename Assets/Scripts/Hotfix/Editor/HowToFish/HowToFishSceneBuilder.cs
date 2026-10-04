using System;
using System.Collections.Generic;
using System.Linq;
using Core.Editor.MvcBind;
using Core.Runtime;
using Hotfix.HowToFish;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.HowToFish
{
    /// 首岛可玩结构的编辑期装配；地形与建筑处于玩法阶段，后续换入正式环境模型。
    public static class HowToFishSceneBuilder
    {
        private const string Root = HowToFishAssetBuilder.Root;
        private const string HudPath = Root + "/Prefabs/UI/HowToFishHudView.prefab";
        private static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(HowToFishAssetBuilder.FontPath);

        [MenuItem("Tools/SleepyDemos/HowToFish/装配群岛场景和HUD")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出 Play Mode 后装配。");
            HowToFishAssetBuilder.BuildFirstIslandAssets();
            HowToFishAssetBuilder.BuildForestAssets();
            HowToFishAssetBuilder.BuildDesertAssets();
            HowToFishAssetBuilder.BuildRocksAssets();
            HowToFishAssetBuilder.BuildVolcanoAssets();
            HowToFishAssetBuilder.BuildOptionalAssets();
            HowToFishAssetBuilder.BuildWildlifeAssets();
            HowToFishAssetBuilder.ConfigureVerifiedCreatures();
            HowToFishAssetBuilder.ConfigureVerifiedBaitPools();
            HowToFishAssetBuilder.ConfigureWeaponUpgrades();
            HowToFishAssetBuilder.ConfigureGunAttachments();
            HowToFishAssetBuilder.EnsureFont();
            HowToFishAssetBuilder.EnsureFolder(Root + "/Prefabs/UI");
            HowToFishAssetBuilder.EnsureFolder(Root + "/Scenes");
            BuildHud();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(Root + "/Scenes/Main.unity") == null) BuildScene();
            UpdateEnvironmentModels();
            AssetDatabase.SaveAssets();
            Debug.Log("[HowToFish] 群岛场景及 HUD 已保存；区域内容进度见 Demo 实施计划。");
        }

        private static void UpdateEnvironmentModels()
        {
            string path = Root + "/Scenes/Main.unity";
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            var previous = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            try
            {
                var boat = scene.GetRootGameObjects().Single(root => root.name == "FishingBoat");
                if (boat.transform.Find("Visual") == null)
                {
                    foreach (var renderer in boat.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
                    var visual = Model("FishingBoat", scene);
                    visual.name = "Visual";
                    visual.transform.SetParent(boat.transform, false);
                }
                foreach (var renderer in boat.transform.Find("Visual").GetComponentsInChildren<Renderer>())
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                    if (source != null) renderer.sharedMaterials = source.sharedMaterials.Select(HowToFishAssetBuilder.MaterialFor).ToArray();
                }
                boat.transform.Find("WheelBlockout").localPosition = new Vector3(-.4f, 1, -.14f);
                UpdateBoatUpgrades(boat, scene);
                if (!scene.GetRootGameObjects().Any(root => root.name == "Lighthouse"))
                {
                    foreach (var root in scene.GetRootGameObjects().Where(root => root.name == "LighthouseBlockout" || root.name == "LighthouseRoofBlockout"))
                        root.GetComponent<Renderer>().enabled = false;
                    var tower = Model("Lighthouse", scene);
                    tower.transform.position = new Vector3(-5, 2.4f, 2);
                    tower.transform.rotation = Quaternion.Euler(0, 180, 0);
                }
                if (!scene.GetRootGameObjects().Any(root => root.name == "LighthouseKeeper"))
                {
                    var proxy = scene.GetRootGameObjects().Single(root => root.name == "KeeperBlockout");
                    proxy.GetComponent<Renderer>().enabled = false;
                    var keeper = Model("Keeper", scene);
                    keeper.name = "LighthouseKeeper";
                    keeper.transform.position = new Vector3(-4, 2.4f, -3);
                    keeper.transform.rotation = Quaternion.Euler(0, 180, 0);
                }
                var island = scene.GetRootGameObjects().Single(root => root.name == "LighthouseIsland");
                ConfigureIsland(island, 0, "灯塔岛", 36);
                island.GetComponent<Renderer>().sharedMaterials = new[]
                {
                    Material("LighthouseRock", new Color(.38f, .36f, .37f)),
                    Material("LighthouseShore", new Color(.44f, .42f, .41f)),
                    Material("LighthouseWetRock", new Color(.27f, .27f, .29f))
                };
                UpdateFirstIslandShop(scene);
                if (!scene.GetRootGameObjects().Any(root => root.name == "ForestIsland"))
                {
                    var forest = BuildIsland(Material("ForestGrass", new Color(.18f, .27f, .12f)),
                        Material("ForestSand", new Color(.54f, .49f, .35f)), Material("ForestRock", new Color(.28f, .29f, .25f)),
                        "ForestIsland", new Vector3(-180, 0, 180), true);
                    ConfigureIsland(forest, 1, "森林岛", 60);
                    Physics.SyncTransforms();
                    var terrain = forest.GetComponent<MeshCollider>();
                    for (int i = 0; i < 48; i++)
                    {
                        float angle = i * 2.399963f;
                        var local = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * (27 + i % 17 * 1.2f);
                        if (local.z < -25 && Mathf.Abs(local.x) < 9) continue;
                        var origin = forest.transform.position + local + Vector3.up * 30;
                        if (!terrain.Raycast(new Ray(origin, Vector3.down), out var hit, 60)) continue;
                        var tree = Model("Pine", scene);
                        tree.name = "Pine" + i;
                        tree.transform.SetParent(forest.transform, true);
                        tree.transform.position = hit.point;
                        tree.transform.rotation = Quaternion.Euler(0, i * 47, 0);
                        tree.transform.localScale = Vector3.one * (1 + i % 4 * .15f);
                        var trunk = tree.AddComponent<CapsuleCollider>(); trunk.radius = .15f; trunk.height = 3; trunk.center = Vector3.up * 1.5f;
                    }
                }
                UpdateForestQuest(scene);
                UpdateDesert(scene);
                UpdateRocks(scene);
                UpdateVolcano(scene);
                UpdateWeaponShops(scene);
                UpdateBaitSupplyShops(scene);
                UpdateCookingStations(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("群岛内容保存失败。");
            }
            finally
            {
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void UpdateForestQuest(Scene scene)
        {
            var forest = scene.GetRootGameObjects().Single(root => root.name == "ForestIsland");
            var ground = forest.GetComponent<MeshCollider>();
            Physics.SyncTransforms();
            Vector3 Surface(float x, float z)
            {
                var ray = new Ray(forest.transform.position + new Vector3(x, 30, z), Vector3.down);
                if (!ground.Raycast(ray, out var hit, 60)) throw new InvalidOperationException("森林任务落点不在地形上。");
                return hit.point;
            }
            if (forest.transform.Find("ForestLady") == null)
            {
                var lady = Model("ForestLady", scene);
                lady.transform.SetParent(forest.transform, true);
                lady.transform.position = Surface(0, -25);
                lady.transform.rotation = Quaternion.Euler(0, 180, 0);
                var collider = lady.AddComponent<CapsuleCollider>(); collider.radius = .3f; collider.height = 1.9f; collider.center = Vector3.up * .95f;
                Station(lady, HowToFishStationKind.ForestLady, "湖畔女士");
            }
            if (forest.transform.Find("ForestShop") == null)
            {
                var shop = Model("ForestShop", scene);
                shop.transform.SetParent(forest.transform, true);
                shop.transform.position = Surface(5, -36);
                shop.transform.rotation = Quaternion.Euler(0, 180, 0);
                foreach (var mesh in shop.GetComponentsInChildren<MeshFilter>())
                {
                    if (mesh.name == "Roof") continue;
                    var collider = mesh.gameObject.AddComponent<BoxCollider>();
                    collider.center = mesh.sharedMesh.bounds.center; collider.size = mesh.sharedMesh.bounds.size;
                    if (mesh.name == "Counter") Station(mesh.gameObject, HowToFishStationKind.Sell, "出售鱼获");
                }
                var pistol = Product("Pistol", shop.transform.TransformPoint(new Vector3(1.65f, 1.45f, .72f)), shop.transform.rotation * Quaternion.Euler(-90, 0, 0), 1);
                pistol.transform.SetParent(shop.transform, true);
                var shotgun = Product("Shotgun", shop.transform.TransformPoint(new Vector3(.8f, 1.5f, .72f)), shop.transform.rotation * Quaternion.Euler(-65, 0, 0), 1);
                shotgun.transform.SetParent(shop.transform, true);
                var bait = Product("HotDog", shop.transform.TransformPoint(new Vector3(-1.4f, 1.05f, .75f)), null, 1);
                bait.transform.SetParent(shop.transform, true);
            }
            var savedShop = forest.transform.Find("ForestShop");
            foreach (string id in new[] { "FishingRod", "BeginnerLure", "BeginnerBossLure" })
            {
                if (savedShop.GetComponentsInChildren<HowToFishStation>().Any(station => station.ItemId == id)) continue;
                var position = id == "FishingRod" ? new Vector3(-1.95f, .35f, 1.1f) : new Vector3(id == "BeginnerLure" ? -.6f : .1f, 1.05f, .8f);
                var product = Product(id, savedShop.TransformPoint(position),
                    id == "FishingRod" ? savedShop.rotation * Quaternion.Euler(-90, 0, 0) : savedShop.rotation, 1);
                product.transform.SetParent(savedShop, true);
            }
            var points = forest.transform.Find("LeechSpawns");
            if (points == null)
            {
                points = new GameObject("LeechSpawns").transform;
                points.SetParent(forest.transform, false);
                foreach (var offset in new[] { new Vector2(-6, -27), new Vector2(7, -26), new Vector2(-12, -20) })
                {
                    var point = new GameObject("LeechSpawn").transform;
                    point.SetParent(points, true);
                    point.position = Surface(offset.x, offset.y) + Vector3.up * .15f;
                }
            }
            var world = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishWorld>()).Single();
            var settings = new SerializedObject(world);
            settings.FindProperty("m_EditorClassIdentifier").stringValue = "Hotfix::" + typeof(HowToFishWorld).FullName;
            var spawns = settings.FindProperty("leechPoints"); spawns.arraySize = points.childCount;
            for (int i = 0; i < points.childCount; i++) spawns.GetArrayElementAtIndex(i).objectReferenceValue = points.GetChild(i);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateDesert(Scene scene)
        {
            var desert = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "DesertIsland");
            if (desert == null)
                desert = BuildIsland(Material("DesertSand", new Color(.76f, .67f, .43f)),
                    Material("DesertShore", new Color(.82f, .74f, .52f)), Material("DesertRock", new Color(.40f, .35f, .28f)),
                    "DesertIsland", new Vector3(-440, 0, 430));
            ConfigureIsland(desert, 2, "沙漠岛", 72);
            Physics.SyncTransforms();
            var ground = desert.GetComponent<MeshCollider>();
            Vector3 Surface(float x, float z)
            {
                if (!ground.Raycast(new Ray(desert.transform.position + new Vector3(x, 30, z), Vector3.down), out var hit, 60))
                    throw new InvalidOperationException("沙漠落点不在地形上。");
                return hit.point;
            }
            if (desert.transform.Find("Palms") == null)
            {
                var palms = new GameObject("Palms").transform; palms.SetParent(desert.transform, false);
                for (int i = 0; i < 35; i++)
                {
                    float angle = i * 2.399963f;
                    float radius = 16 + i % 13 * 2.7f;
                    float x = Mathf.Sin(angle) * radius, z = Mathf.Cos(angle) * radius;
                    if (z < -27 && Mathf.Abs(x) < 16) continue;
                    var palm = Model("Palm", scene); palm.transform.SetParent(palms, true);
                    palm.transform.position = Surface(x, z); palm.transform.rotation = Quaternion.Euler(0, i * 67, 0);
                    var shape = palm.AddComponent<CapsuleCollider>(); shape.radius = .25f; shape.height = 4.5f; shape.center = Vector3.up * 2.25f;
                }
            }
            if (desert.transform.Find("Tourist") == null)
            {
                var palm = Model("Palm", scene); palm.name = "TouristPalm";
                palm.transform.SetParent(desert.transform, true); palm.transform.position = Surface(-8, -40);
                var trunk = palm.AddComponent<CapsuleCollider>(); trunk.radius = .25f; trunk.height = 4.5f; trunk.center = Vector3.up * 2.25f;
                var tourist = Model("Tourist", scene); tourist.transform.SetParent(desert.transform, true);
                tourist.transform.position = Surface(-8, -40.4f); tourist.transform.rotation = Quaternion.Euler(0, 180, 0);
                var shape = tourist.AddComponent<BoxCollider>(); shape.center = new Vector3(0, .6f, .35f); shape.size = new Vector3(.65f, 1.2f, 1);
                Station(tourist, HowToFishStationKind.Tourist, "棕榈树下的游客");
            }
            if (desert.transform.Find("GrillMaster") == null)
            {
                var chef = Model("GrillMaster", scene); chef.transform.SetParent(desert.transform, true);
                chef.transform.position = Surface(10, -37); chef.transform.rotation = Quaternion.Euler(0, 180, 0);
                var shape = chef.AddComponent<CapsuleCollider>(); shape.radius = .3f; shape.height = 1.9f; shape.center = Vector3.up * .95f;
                Station(chef, HowToFishStationKind.GrillMaster, "烧烤师");
                var grill = Model("Grill", scene); grill.transform.SetParent(desert.transform, true);
                grill.transform.position = Surface(11.4f, -38); grill.transform.rotation = Quaternion.Euler(0, 180, 0);
                var grillShape = grill.AddComponent<BoxCollider>(); grillShape.center = Vector3.up * .48f; grillShape.size = new Vector3(1, .96f, .6f);
                Station(grill, HowToFishStationKind.Grill, "烧烤架");
            }
            if (desert.transform.Find("DesertShop") == null)
            {
                var shop = Model("ForestShop", scene); shop.name = "DesertShop";
                shop.transform.SetParent(desert.transform, true); shop.transform.position = Surface(0, -32);
                shop.transform.rotation = Quaternion.Euler(0, 180, 0);
                foreach (var mesh in shop.GetComponentsInChildren<MeshFilter>())
                {
                    if (mesh.name == "Roof") continue;
                    var shape = mesh.gameObject.AddComponent<BoxCollider>(); shape.center = mesh.sharedMesh.bounds.center; shape.size = mesh.sharedMesh.bounds.size;
                    if (mesh.name == "Counter") Station(mesh.gameObject, HowToFishStationKind.Sell, "出售鱼获");
                }
                foreach (var row in new[] { ("SMG", new Vector3(1.25f, 1.5f, .72f)),
                    ("StandardLure", new Vector3(-1.25f, 1.05f, .8f)), ("StandardBossLure", new Vector3(-.55f, 1.05f, .8f)),
                    ("FishingRod", new Vector3(-1.95f, .35f, 1.1f)) })
                {
                    var product = Product(row.Item1, shop.transform.TransformPoint(row.Item2),
                        shop.transform.rotation * (row.Item1 == "SMG" || row.Item1 == "FishingRod" ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity), 2);
                    product.transform.SetParent(shop.transform, true);
                }
            }
            var savedShop = desert.transform.Find("DesertShop");
            if (!savedShop.GetComponentsInChildren<HowToFishStation>().Any(value => value.ItemId == "Coconut"))
            {
                var coconut = Product("Coconut", savedShop.TransformPoint(new Vector3(.15f, 1.05f, .8f)), savedShop.rotation, 2);
                coconut.transform.SetParent(savedShop, true);
            }
        }

        private static void UpdateRocks(Scene scene)
        {
            var rocks = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "RocksIsland");
            if (rocks == null)
                rocks = BuildIsland(Material("RocksPlateau", new Color(.38f, .38f, .36f)),
                    Material("RocksShore", new Color(.46f, .45f, .42f)), Material("RocksWet", new Color(.27f, .28f, .28f)),
                    "RocksIsland", new Vector3(-720, 0, 650));
            ConfigureIsland(rocks, 3, "岩石岛", 72);
            var savedShop = rocks.transform.Find("RocksShop");
            if (savedShop != null) { UpdateRocksProducts(savedShop); return; }
            Physics.SyncTransforms();
            var ground = rocks.GetComponent<MeshCollider>();
            if (!ground.Raycast(new Ray(rocks.transform.position + new Vector3(0, 30, -25), Vector3.down), out var floor, 60))
                throw new InvalidOperationException("岩石岛商店落点不在地形上。");
            var shop = Model("RocksShop", scene);
            shop.transform.SetParent(rocks.transform, true); shop.transform.position = floor.point;
            shop.transform.rotation = Quaternion.Euler(0, 180, 0);
            foreach (var mesh in shop.GetComponentsInChildren<MeshFilter>())
            {
                if (mesh.name.StartsWith("RoofTile", StringComparison.Ordinal))
                    mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                else
                {
                    var shape = mesh.gameObject.AddComponent<BoxCollider>(); shape.center = mesh.sharedMesh.bounds.center; shape.size = mesh.sharedMesh.bounds.size;
                }
                if (mesh.name == "Counter") Station(mesh.gameObject, HowToFishStationKind.Sell, "出售鱼获");
            }
            var islander = Model("Islander", scene); islander.transform.SetParent(shop.transform, false);
            islander.transform.localPosition = new Vector3(.7f, .15f, -1.45f);
            var body = islander.AddComponent<CapsuleCollider>(); body.radius = .25f; body.height = 1.94f; body.center = Vector3.up * .97f;
            Station(islander, HowToFishStationKind.Islander, "岩石岛岛民");
            UpdateRocksProducts(shop.transform);
        }

        private static void UpdateRocksProducts(Transform shop)
        {
            foreach (var row in new[] { ("SniperRifle", new Vector3(-1.95f, 1.48f, -.6f)),
                ("ProfessionalBossLure", new Vector3(.2f, 1.04f, -.7f)), ("ProfessionalLure", new Vector3(1.3f, 1.04f, -.7f)) })
            {
                if (shop.GetComponentsInChildren<HowToFishStation>().Any(station => station.ItemId == row.Item1)) continue;
                var product = Product(row.Item1, shop.TransformPoint(row.Item2),
                    shop.rotation * (row.Item1 == "SniperRifle" ? Quaternion.Euler(0, 90, 0) : Quaternion.identity), 3);
                product.transform.SetParent(shop, true);
            }
        }

        private static void UpdateVolcano(Scene scene)
        {
            var island = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "VolcanoIsland");
            if (island == null)
            {
                island = Model("VolcanoTerrain", scene); island.name = "VolcanoIsland";
                island.transform.position = new Vector3(-720, 0, 1040);
                var terrain = island.GetComponentsInChildren<MeshFilter>().Single(mesh => mesh.name == "Terrain");
                terrain.gameObject.AddComponent<MeshCollider>().sharedMesh = terrain.sharedMesh;
            }
            ConfigureIsland(island, 4, "火山岛", 98);
            var ground = island.GetComponentInChildren<MeshCollider>();
            Physics.SyncTransforms();
            Vector3 Surface(float x, float z)
            {
                if (!ground.Raycast(new Ray(island.transform.position + new Vector3(x, 90, z), Vector3.down), out var hit, 120))
                    throw new InvalidOperationException("火山装配落点不在地形上。");
                return hit.point;
            }
            string[] products = { "AssaultRifle", "ScientificLure", "SniperRifle", "SMG", "Shotgun", "Pistol" };
            if (island.transform.Find("VolcanoCamp") == null)
            {
                var camp = new GameObject("VolcanoCamp").transform; camp.SetParent(island.transform, false);
                var tent = Model("MilitaryTent", scene); tent.transform.SetParent(camp, true);
                tent.transform.position = Surface(0, -63); tent.transform.rotation = Quaternion.Euler(0, 180, 0);
                AddStaticModelColliders(tent);
                var scientist = Model("Scientist", scene); scientist.transform.SetParent(camp, true);
                scientist.transform.position = Surface(4, -71); scientist.transform.rotation = Quaternion.Euler(0, 180, 0);
                var body = scientist.AddComponent<CapsuleCollider>(); body.radius = .26f; body.height = 1.95f; body.center = Vector3.up * .975f;
                Station(scientist, HowToFishStationKind.Scientist, "火山科学家");
                for (int i = 0; i < products.Length; i++)
                {
                    var crate = Model("SupplyCrate", scene); crate.name = "WeaponCrate" + i;
                    crate.transform.SetParent(camp, true); crate.transform.position = Surface(-5 + i * 1.5f, -67.5f);
                    AddStaticModelColliders(crate);
                    var product = Product(products[i], crate.transform.position + Vector3.up * .84f,
                        products[i] == "ScientificLure" ? Quaternion.identity : Quaternion.Euler(0, 90, 0), 4);
                    product.transform.SetParent(camp, true);
                }
                var seller = Model("SupplyCrate", scene); seller.name = "FishCounter"; seller.transform.SetParent(camp, true);
                seller.transform.position = Surface(7, -68); AddStaticModelColliders(seller);
                Station(seller, HowToFishStationKind.Sell, "出售鱼获");
            }
            var savedCamp = island.transform.Find("VolcanoCamp");
            if (!savedCamp.GetComponentsInChildren<HowToFishStation>().Any(value => value.ItemId == "ScientificBossLure"))
            {
                var crate = Model("SupplyCrate", scene); crate.name = "BossLureCrate";
                crate.transform.SetParent(savedCamp, true); crate.transform.position = Surface(4, -67.5f);
                AddStaticModelColliders(crate);
                var product = Product("ScientificBossLure", crate.transform.position + Vector3.up * .9f, Quaternion.identity, 4);
                product.transform.SetParent(savedCamp, true);
            }
            for (int i = 0; i < products.Length; i++)
            {
                var product = savedCamp.GetComponentsInChildren<HowToFishStation>().Single(value => value.ItemId == products[i]);
                var crate = savedCamp.Find("WeaponCrate" + i);
                product.transform.rotation = products[i] == "ScientificLure" ? Quaternion.identity : Quaternion.Euler(0, 90, 90);
                float top = crate.GetComponentsInChildren<Renderer>().Max(value => value.bounds.max.y);
                float bottom = product.GetComponentsInChildren<Renderer>().Min(value => value.bounds.min.y);
                product.transform.position += Vector3.up * (top + .03f - bottom);
            }
            var path = island.transform.Find("WoodenAscent");
            if (path == null) { path = new GameObject("WoodenAscent").transform; path.SetParent(island.transform, false); }
            Vector3 PathPoint(float t)
            {
                float angle = Mathf.PI + .22f + t * Mathf.PI * 1.4f, radius = Mathf.Lerp(70, 20, t);
                return Surface(Mathf.Sin(angle) * radius, Mathf.Cos(angle) * radius) + Vector3.up * .15f;
            }
            for (int i = 0; i <= 180; i++)
            {
                float t = i / 180f;
                var saved = path.Find("AscentPlank" + i);
                var plank = saved != null ? saved.gameObject : Model("VolcanoPlank", scene);
                if (saved == null)
                {
                    plank.name = "AscentPlank" + i; plank.transform.SetParent(path, true);
                    plank.AddComponent<BoxCollider>().size = new Vector3(2.9f, .1f, 1.05f);
                }
                plank.transform.position = PathPoint(t);
                var direction = PathPoint(Mathf.Min(1, t + .002f)) - PathPoint(Mathf.Max(0, t - .002f));
                plank.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
            if (island.transform.Find("CraterTrigger") == null)
            {
                var trigger = new GameObject("CraterTrigger"); trigger.transform.SetParent(island.transform, false);
                trigger.transform.localPosition = new Vector3(0, 22, 0);
                var shape = trigger.AddComponent<BoxCollider>(); shape.isTrigger = true; shape.size = new Vector3(23, 2, 23);
                trigger.AddComponent<HowToFishVolcanoCrater>();
                var spawn = new GameObject("WhaleEmergence").transform; spawn.SetParent(island.transform, true);
                spawn.position = Surface(24, 0) + Vector3.up * 1.2f;
                var settings = new SerializedObject(trigger.GetComponent<HowToFishVolcanoCrater>());
                Bind(settings, "bossSpawn", spawn); settings.ApplyModifiedPropertiesWithoutUndo();
            }
            if (island.transform.Find("MilitaryBoat") == null)
            {
                var military = Model("MilitaryBoat", scene); military.transform.SetParent(island.transform, false);
                military.transform.localPosition = new Vector3(9, .1f, -94);
                AddStaticModelColliders(military);
                var wheel = military.GetComponentsInChildren<Transform>().Single(node => node.name == "SteeringWheel");
                var shape = wheel.gameObject.AddComponent<BoxCollider>(); shape.size = new Vector3(.6f, .55f, .3f);
                Station(wheel.gameObject, HowToFishStationKind.MilitaryDeparture, "驾驶军用船返回大陆");
                for (int i = 0; i < 8; i++)
                {
                    var plank = Model("VolcanoPlank", scene); plank.name = "MilitaryDock" + i;
                    plank.transform.SetParent(island.transform, false); plank.transform.localPosition = new Vector3(9, .3f, -85 - i);
                    plank.AddComponent<BoxCollider>().size = new Vector3(2.9f, .1f, 1.05f);
                }
            }
            if (island.transform.Find("FootSnailPoints") == null)
            {
                var points = new GameObject("FootSnailPoints").transform; points.SetParent(island.transform, false);
                for (int i = 0; i < 4; i++)
                {
                    var point = new GameObject("FootSnail" + i).transform; point.SetParent(points, true);
                    point.position = Surface(-10 - i * 2, -68 + i) + Vector3.up * .1f;
                }
            }
            var world = scene.GetRootGameObjects().Single(root => root.GetComponent<HowToFishWorld>() != null).GetComponent<HowToFishWorld>();
            var data = new SerializedObject(world);
            Bind(data, "crater", island.GetComponentInChildren<HowToFishVolcanoCrater>());
            var snails = data.FindProperty("snailPoints");
            var snailRoot = island.transform.Find("FootSnailPoints"); snails.arraySize = snailRoot.childCount;
            for (int i = 0; i < snailRoot.childCount; i++) snails.GetArrayElementAtIndex(i).objectReferenceValue = snailRoot.GetChild(i);
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateBaitSupplyShops(Scene scene)
        {
            var names = new[] { "LighthouseIsland", "ForestIsland", "DesertIsland", "RocksIsland", "VolcanoIsland" };
            var positions = new[] { new Vector2(-7, -2), new Vector2(9, -36), new Vector2(4, -32), new Vector2(5, -25), new Vector2(-8, -67) };
            var stock = new[] { ("CrabRod", 0), ("Radar", 0), ("HotDog", 0), ("FishingRod", 1),
                ("BeginnerLure", 1), ("BeginnerBossLure", 1), ("StandardLure", 2), ("StandardBossLure", 2),
                ("ProfessionalLure", 3), ("ProfessionalBossLure", 3), ("ScientificLure", 4), ("ScientificBossLure", 4) };
            for (int i = 0; i < names.Length; i++)
            {
                var island = scene.GetRootGameObjects().Single(root => root.name == names[i]);
                var ground = island.GetComponent<MeshCollider>();
                if (ground == null) ground = island.GetComponentsInChildren<MeshCollider>().Single(value => value.name == "Terrain");
                int standIndex = 0;
                foreach (var row in stock.Where(row => row.Item2 <= i))
                {
                    var point = positions[i] + new Vector2(3 + standIndex % 3 * 1.65f, -4 - standIndex / 3 * 1.65f);
                    standIndex++;
                    if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishStation>())
                        .Any(station => station.Kind == HowToFishStationKind.Product && station.ItemId == row.Item1 && station.Island == i)) continue;
                    Physics.SyncTransforms();
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var floor, 120))
                        throw new InvalidOperationException("补给台落点不在地形上：" + names[i] + "/" + row.Item1);
                    var stand = Model("SupplyCrate", scene); stand.name = row.Item1 + "Supply";
                    stand.transform.SetParent(island.transform, true); stand.transform.position = floor.point; AddStaticModelColliders(stand);
                    float top = stand.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y);
                    var product = Product(row.Item1, new Vector3(floor.point.x, top, floor.point.z),
                        row.Item1.EndsWith("Rod", StringComparison.Ordinal) ? Quaternion.Euler(-90, 0, 0) : Quaternion.identity, i);
                    product.transform.SetParent(stand.transform, true);
                    float bottom = product.GetComponentsInChildren<Renderer>().Min(renderer => renderer.bounds.min.y);
                    product.transform.position += Vector3.up * (top - bottom + .025f);
                }
            }
        }

        private static void UpdateCookingStations(Scene scene)
        {
            var rocks = scene.GetRootGameObjects().Single(root => root.name == "RocksIsland");
            if (rocks.transform.Find("GrillMaster") == null)
            {
                var ground = rocks.GetComponent<MeshCollider>();
                foreach (var row in new[] { ("GrillMaster", new Vector2(12, -31)), ("Grill", new Vector2(10.4f, -31)) })
                {
                    if (!ground.Raycast(new Ray(rocks.transform.position + new Vector3(row.Item2.x, 60, row.Item2.y), Vector3.down), out var floor, 90))
                        throw new InvalidOperationException("岩石岛烤架落点不在地形上。");
                    var model = Model(row.Item1, scene); model.transform.SetParent(rocks.transform, true);
                    model.transform.position = floor.point; model.transform.rotation = Quaternion.Euler(0, 180, 0);
                    if (row.Item1 == "GrillMaster")
                    {
                        var shape = model.AddComponent<CapsuleCollider>(); shape.center = Vector3.up * .9f; shape.height = 1.8f; shape.radius = .3f;
                        Station(model, HowToFishStationKind.GrillMaster, "烧烤师");
                    }
                    else
                    {
                        var shape = model.AddComponent<BoxCollider>(); shape.center = Vector3.up * .48f; shape.size = new Vector3(1, .96f, .6f);
                        Station(model, HowToFishStationKind.Grill, "烧烤架");
                    }
                }
            }
            foreach (var station in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishStation>())
                .Where(value => value.Kind == HowToFishStationKind.Grill || value.Kind == HowToFishStationKind.GrillMaster))
            {
                var stationSettings = new SerializedObject(station);
                stationSettings.FindProperty("island").intValue = station.GetComponentInParent<HowToFishIsland>().Index;
                stationSettings.ApplyModifiedPropertiesWithoutUndo();
                if (station.Kind != HowToFishStationKind.Grill) continue;
                if (station.transform.Find("HeatArea") != null) continue;
                var area = new GameObject("HeatArea", typeof(BoxCollider), typeof(HowToFishGrill));
                area.transform.SetParent(station.transform, false);
                var shape = area.GetComponent<BoxCollider>(); shape.isTrigger = true;
                shape.center = Vector3.up * 1.12f; shape.size = new Vector3(1.08f, .4f, .68f);
                var lightObject = new GameObject("Embers", typeof(Light)); lightObject.transform.SetParent(area.transform, false);
                lightObject.transform.localPosition = Vector3.up * .88f;
                var glow = lightObject.GetComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(1, .32f, .06f);
                glow.intensity = 1.2f; glow.range = 1.8f; glow.enabled = false;
                var settings = new SerializedObject(area.GetComponent<HowToFishGrill>()); Bind(settings, "embers", glow); settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void UpdateBoatUpgrades(GameObject boat, Scene scene)
        {
            var settings = new SerializedObject(boat.GetComponent<HowToFishBoat>());
            var motors = settings.FindProperty("motors"); motors.arraySize = 3;
            var propellers = new List<Transform>();
            var names = new[] { "SmallMotor", "MediumMotor", "BigMotor" };
            for (int i = 0; i < names.Length; i++)
            {
                var mount = boat.transform.Find(names[i]);
                if (mount == null)
                {
                    mount = Model(names[i], scene).transform;
                    mount.SetParent(boat.transform, false);
                    mount.localPosition = new Vector3(0, .4f, -2.35f);
                }
                mount.gameObject.SetActive(i == 0);
                motors.GetArrayElementAtIndex(i).objectReferenceValue = mount.gameObject;
                propellers.AddRange(mount.GetComponentsInChildren<Transform>(true).Where(child => child.name.StartsWith("Propeller", StringComparison.Ordinal)));
            }
            var propellerRefs = settings.FindProperty("propellers"); propellerRefs.arraySize = propellers.Count;
            for (int i = 0; i < propellers.Count; i++) propellerRefs.GetArrayElementAtIndex(i).objectReferenceValue = propellers[i];
            var radar = boat.transform.Find("BoatRadar");
            if (radar == null)
            {
                radar = Model("BoatRadar", scene).transform;
                radar.SetParent(boat.transform, false);
                radar.localPosition = new Vector3(.38f, .83f, -.05f);
                radar.localRotation = Quaternion.Euler(0, 180, 0);
                var screen = new GameObject("Screen", typeof(RectTransform), typeof(Canvas));
                screen.transform.SetParent(radar, false);
                screen.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                var rect = screen.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(200, 160); rect.localScale = Vector3.one * .0018f;
                rect.localPosition = new Vector3(0, .21f, .084f); rect.localRotation = Quaternion.Euler(0, 180, 0);
                Text("You", rect, "▲", new Vector2(20, 20), Vector2.zero, 16).color = new Color(.5f, 1, .65f);
                for (int i = 0; i < 5; i++)
                    Text("Island" + i, rect, "", new Vector2(24, 24), Vector2.zero, 18).color = new Color(.5f, 1, .65f);
            }
            Bind(settings, "radar", radar.gameObject);
            var dots = settings.FindProperty("radarIslands"); dots.arraySize = 5;
            for (int i = 0; i < 5; i++) dots.GetArrayElementAtIndex(i).objectReferenceValue = radar.Find("Screen/Island" + i).GetComponent<TextMeshProUGUI>();
            radar.gameObject.SetActive(false);
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateWeaponShops(Scene scene)
        {
            var names = new[] { "LighthouseIsland", "ForestIsland", "DesertIsland", "RocksIsland", "VolcanoIsland" };
            var positions = new[] { new Vector2(-7, -2), new Vector2(9, -36), new Vector2(4, -32), new Vector2(5, -25), new Vector2(-8, -67) };
            var oldAnvil = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Anvil");
            if (oldAnvil != null) Object.DestroyImmediate(oldAnvil);
            Physics.SyncTransforms();
            for (int i = 0; i < names.Length; i++)
            {
                var island = scene.GetRootGameObjects().Single(root => root.name == names[i]);
                var ground = island.GetComponent<MeshCollider>();
                if (ground == null) ground = island.GetComponentsInChildren<MeshCollider>().Single(collider => collider.name == "Terrain");
                foreach (var upgrade in new[] { ("MediumMotor", 1, "中型马达", 1), ("BigMotor", 3, "大型双机马达", 2), ("BoatRadar", 2, "船载雷达", 0) })
                {
                    if (i < upgrade.Item2) continue;
                    var point = positions[i] + new Vector2(-3 - upgrade.Item4 * 1.4f, -6.5f);
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var floor, 120))
                        throw new InvalidOperationException("船只升级台落点不在地形上。");
                    var existing = island.transform.Find(upgrade.Item1 + "Shop");
                    if (existing != null) { existing.position = floor.point; continue; }
                    var stand = Model("SupplyCrate", scene); stand.name = upgrade.Item1 + "Shop";
                    stand.transform.SetParent(island.transform, true); stand.transform.position = floor.point; AddStaticModelColliders(stand);
                    float baseHeight = stand.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y) - stand.transform.position.y;
                    var display = Model(upgrade.Item1, scene); display.transform.SetParent(stand.transform, false);
                    float bottom = display.GetComponentsInChildren<Renderer>().Min(renderer => renderer.bounds.min.y) - display.transform.position.y;
                    display.transform.localPosition = Vector3.up * (baseHeight - bottom); AddStaticModelColliders(display);
                    Station(stand, upgrade.Item4 == 0 ? HowToFishStationKind.BoatRadar : HowToFishStationKind.MotorUpgrade, upgrade.Item3);
                    var settings = new SerializedObject(stand.GetComponent<HowToFishStation>());
                    settings.FindProperty("island").intValue = i;
                    settings.FindProperty("motorTier").intValue = upgrade.Item4; settings.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (string modelId in i == 0 ? new[] { "UpgradeAnvil" } : new[] { "UpgradeAnvil", "AmmoUpgrade" })
                {
                    if (island.transform.Find(modelId) != null) continue;
                    var point = positions[i] + (modelId == "AmmoUpgrade" ? Vector2.right * 1.4f : Vector2.zero);
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var hit, 120))
                        throw new InvalidOperationException("升级台落点不在地形上：" + names[i]);
                    var bench = Model(modelId, scene); bench.transform.SetParent(island.transform, true);
                    bench.transform.position = hit.point;
                    bench.transform.rotation = Quaternion.Euler(0, 180, 0);
                    AddStaticModelColliders(bench);
                    Station(bench, modelId == "UpgradeAnvil" ? HowToFishStationKind.Anvil : HowToFishStationKind.AmmoUpgrade,
                        modelId == "UpgradeAnvil" ? "磨锐当前近战武器" : "升级当前枪械弹药");
                    var settings = new SerializedObject(bench.GetComponent<HowToFishStation>());
                    settings.FindProperty("island").intValue = i; settings.ApplyModifiedPropertiesWithoutUndo();
                }
                var anvil = island.transform.Find("UpgradeAnvil");
                foreach (var row in new[] { ("BrassKnuckles", -.12f), ("Knife", .10f) })
                {
                    if (scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishStation>())
                        .Any(station => station.Kind == HowToFishStationKind.Product && station.ItemId == row.Item1 && station.Island == i)) continue;
                    var product = Product(row.Item1, anvil.TransformPoint(new Vector3(row.Item2, .89f, 0)), Quaternion.Euler(0, 90, 90), i);
                    product.transform.SetParent(anvil, true);
                }
                if (i > 0 && island.transform.Find("InventoryUpgrade") == null)
                {
                    var point = positions[i] + Vector2.left * 1.5f;
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var floor, 120))
                        throw new InvalidOperationException("背包扩容台落点不在地形上。");
                    var station = Model("SupplyCrate", scene); station.name = "InventoryUpgrade";
                    station.transform.SetParent(island.transform, true); station.transform.position = floor.point; AddStaticModelColliders(station);
                    float height = station.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y) - station.transform.position.y;
                    var bag = Model("Backpack", scene); bag.transform.SetParent(station.transform, false);
                    bag.transform.localPosition = Vector3.up * height; bag.transform.localRotation = Quaternion.Euler(0, 180, 0); AddStaticModelColliders(bag);
                    Station(station, HowToFishStationKind.InventoryUpgrade, "扩充装备栏");
                    var settings = new SerializedObject(station.GetComponent<HowToFishStation>());
                    settings.FindProperty("island").intValue = i; settings.ApplyModifiedPropertiesWithoutUndo();
                }
                int displayIndex = 0;
                foreach (var row in new[] { (HowToFishAttachment.RedDotSight, 2, "红点瞄具"), (HowToFishAttachment.SniperScope, 3, "瞄准镜"),
                    (HowToFishAttachment.Compensator, 2, "补偿器"), (HowToFishAttachment.Suppressor, 3, "消音器"),
                    (HowToFishAttachment.LaserSight, 1, "激光瞄具"), (HowToFishAttachment.ExtendedMag, 2, "扩容弹匣") })
                {
                    if (i < row.Item2) continue;
                    var point = positions[i] + new Vector2(displayIndex % 3 * 1.4f, -2.2f - displayIndex / 3 * 1.6f);
                    displayIndex++;
                    string name = "AttachmentShop_" + row.Item1;
                    if (island.transform.Find(name) != null) continue;
                    if (!ground.Raycast(new Ray(island.transform.position + new Vector3(point.x, 90, point.y), Vector3.down), out var hit, 120))
                        throw new InvalidOperationException("配件箱落点不在地形上：" + names[i]);
                    var shop = new GameObject(name); SceneManager.MoveGameObjectToScene(shop, scene);
                    shop.transform.SetParent(island.transform, true); shop.transform.position = hit.point; shop.transform.rotation = Quaternion.Euler(0, 180, 0);
                    var support = Model("SupplyCrate", scene); support.transform.SetParent(shop.transform, false); AddStaticModelColliders(support);
                    float height = support.GetComponentsInChildren<Renderer>().Max(renderer => renderer.bounds.max.y) - shop.transform.position.y;
                    var crate = Model("AttachmentCrate", scene); crate.transform.SetParent(shop.transform, false); crate.transform.localPosition = Vector3.up * height;
                    AddStaticModelColliders(crate);
                    var display = crate.GetComponentsInChildren<Transform>().Single(node => node.name == "Display");
                    var part = Model(row.Item1.ToString(), scene); part.transform.SetParent(display, false);
                    part.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    float bottom = part.GetComponentsInChildren<Renderer>().Min(renderer => renderer.bounds.min.y);
                    part.transform.position += Vector3.up * (display.position.y - bottom + .005f);
                    Station(shop, HowToFishStationKind.Attachment, row.Item3);
                    var settings = new SerializedObject(shop.GetComponent<HowToFishStation>());
                    settings.FindProperty("island").intValue = i; settings.FindProperty("attachment").enumValueIndex = (int)row.Item1;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void AddStaticModelColliders(GameObject model)
        {
            foreach (var mesh in model.GetComponentsInChildren<MeshFilter>())
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        }

        private static void ConfigureIsland(GameObject root, int index, string name, float radius)
        {
            var marker = root.GetComponent<HowToFishIsland>() ?? root.AddComponent<HowToFishIsland>();
            var settings = new SerializedObject(marker);
            settings.FindProperty("index").intValue = index;
            settings.FindProperty("displayName").stringValue = name;
            settings.FindProperty("radius").floatValue = radius;
            settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void UpdateFirstIslandShop(Scene scene)
        {
            string[] products = { "CrabRod", "Knife", "HotDog", "Beer", "Radar" };
            foreach (var station in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<HowToFishStation>()).ToArray())
                if (station.Kind == HowToFishStationKind.Product && station.Island == 0 && products.Contains(station.ItemId))
                    Object.DestroyImmediate(station.gameObject);
            foreach (var root in scene.GetRootGameObjects().Where(root => root.name == "StoreBoard" || root.name == "SellingCounter" || root.name == "FreeRodSpawn"))
                Object.DestroyImmediate(root);
            Product("CrabRod", new Vector3(-5.40f, 2.65f, .12f), Quaternion.Euler(-90, 0, 0));
            Product("Knife", new Vector3(-5.12f, 3.25f, .12f), Quaternion.Euler(-90, 0, 0));
            Product("HotDog", new Vector3(-4.66f, 3.95f, .12f), Quaternion.Euler(-90, 0, 0));
            Product("Beer", new Vector3(-4.66f, 3.02f, .12f));
            Product("Radar", new Vector3(-3.45f, 3.05f, -3.15f));
        }

        private static GameObject Model(string name, Scene scene)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" + name + ".fbx");
            if (source == null) throw new InvalidOperationException("缺少模型：" + name);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source, scene);
            model.name = name;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
                renderer.sharedMaterials = renderer.sharedMaterials.Select(HowToFishAssetBuilder.MaterialFor).ToArray();
            return model;
        }

        private static void BuildHud()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(HudPath) != null)
            {
                var existing = PrefabUtility.LoadPrefabContents(HudPath);
                try
                {
                    var previousFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/LoadResources/Fonts/TMP_FontAssets/CN/HarmonyOS_CN.asset");
                    foreach (var text in existing.GetComponentsInChildren<TextMeshProUGUI>(true))
                        if (text.font == previousFont) text.font = Font;
                    EnsureBossHud(existing);
                    EnsureRadarHud(existing);
                    EnsureInventoryHud(existing);
                    GenerateHudBinding(existing);
                }
                finally { PrefabUtility.UnloadPrefabContents(existing); }
                return;
            }
            if (Font == null) throw new InvalidOperationException("缺少公共中文 TMP 字体。");
            var root = Rect("HowToFishHudView", null, new Vector2(1920, 1080), Vector2.zero).gameObject;
            root.GetComponent<RectTransform>().anchorMin = Vector2.zero;
            root.GetComponent<RectTransform>().anchorMax = Vector2.one;
            root.GetComponent<RectTransform>().sizeDelta = Vector2.zero;
            try
            {
                var presenter = root.AddComponent<HowToFishHudPresenter>();
                var serialized = new SerializedObject(presenter);
                Bind(serialized, "stats", Text("Stats", root.transform, "", new Vector2(360, 90), new Vector2(24, -24), 26, new Vector2(0, 1)));
                Bind(serialized, "focus", Text("Focus", root.transform, "", new Vector2(800, 65), new Vector2(0, -80), 26));
                Text("Crosshair", root.transform, "+", new Vector2(32, 32), Vector2.zero, 24);
                Bind(serialized, "controls", Text("Controls", root.transform, "", new Vector2(1350, 50), new Vector2(0, 30), 21, new Vector2(.5f, 0)));
                var fishingPanel = Rect("FishingPanel", root.transform, new Vector2(440, 80), new Vector2(0, -170));
                Bind(serialized, "fishingPanel", fishingPanel.gameObject);
                Bind(serialized, "fishingStatus", Text("FishingStatus", fishingPanel, "", new Vector2(440, 40), new Vector2(0, 18), 23));
                Panel("TensionTrack", fishingPanel, new Vector2(310, 12), new Vector2(0, -15), new Color(.08f, .08f, .09f, .8f));
                var tension = Panel("Tension", fishingPanel, new Vector2(310, 12), new Vector2(0, -15), Color.yellow);
                tension.type = Image.Type.Filled;
                tension.fillMethod = Image.FillMethod.Horizontal;
                tension.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                Bind(serialized, "tension", tension);
                var menu = Panel("Menu", root.transform, new Vector2(660, 720), Vector2.zero, new Color(.065f, .09f, .11f, .97f)).transform;
                Bind(serialized, "menu", menu.gameObject);
                Bind(serialized, "menuTitle", Text("MenuTitle", menu, "渔力全开", new Vector2(580, 95), new Vector2(0, 275), 40));
                var slots = serialized.FindProperty("slots"); var labels = serialized.FindProperty("slotLabels"); var newGames = serialized.FindProperty("newGames");
                slots.arraySize = labels.arraySize = newGames.arraySize = 3;
                for (int i = 0; i < 3; i++)
                {
                    var button = Button("Slot" + i, menu, "存档 " + (i + 1), new Vector2(370, 55), new Vector2(-60, 150 - i * 95), out var label);
                    slots.GetArrayElementAtIndex(i).objectReferenceValue = button;
                    labels.GetArrayElementAtIndex(i).objectReferenceValue = label;
                    newGames.GetArrayElementAtIndex(i).objectReferenceValue = Button("New" + i, menu, "重开", new Vector2(110, 55), new Vector2(205, 150 - i * 95), out _);
                }
                Bind(serialized, "resume", Button("Resume", menu, "继续航程", new Vector2(300, 52), new Vector2(0, -205), out _));
                Bind(serialized, "save", Button("Save", menu, "保存进度", new Vector2(300, 52), new Vector2(0, -135), out _));
                Bind(serialized, "back", Button("ReturnHub", menu, "返回主入口", new Vector2(300, 52), new Vector2(0, -285), out _));
                Bind(serialized, "journal", Text("Journal", menu, "", new Vector2(530, 340), new Vector2(0, 30), 25));
                Bind(serialized, "notice", Text("Notice", root.transform, "", new Vector2(1000, 80), new Vector2(0, -410), 25));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EnsureBossHud(root);
                EnsureRadarHud(root);
                EnsureInventoryHud(root);
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
                GenerateHudBinding(root);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void EnsureInventoryHud(GameObject root)
        {
            var controls = root.transform.Find("Controls").GetComponent<TextMeshProUGUI>();
            controls.rectTransform.sizeDelta = new Vector2(1700, 50);
            controls.enableAutoSizing = true; controls.fontSizeMin = 16; controls.fontSizeMax = 21;
            if (root.transform.Find("EquipmentSlots") != null) return;
            var slots = Text("EquipmentSlots", root.transform, "", new Vector2(1600, 50), new Vector2(0, 90), 18, new Vector2(.5f, 0));
            slots.enableAutoSizing = true; slots.fontSizeMin = 15; slots.fontSizeMax = 18;
            var settings = new SerializedObject(root.GetComponent<HowToFishHudPresenter>());
            Bind(settings, "equipmentSlots", slots); settings.ApplyModifiedPropertiesWithoutUndo();
            root.transform.Find("Notice").GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -350);
        }

        private static void EnsureRadarHud(GameObject root)
        {
            if (root.transform.Find("RadarPanel") != null) return;
            var panel = Panel("RadarPanel", root.transform, new Vector2(245, 280), new Vector2(-150, 200), new Color(.015f, .07f, .035f, .94f));
            panel.rectTransform.anchorMin = panel.rectTransform.anchorMax = new Vector2(1, 0);
            var settings = new SerializedObject(root.GetComponent<HowToFishHudPresenter>());
            Bind(settings, "radarPanel", panel.gameObject);
            Bind(settings, "radarStatus", Text("RadarStatus", panel.transform, "雷达", new Vector2(235, 54), new Vector2(0, 104), 19));
            var map = Rect("RadarMap", panel.transform, new Vector2(220, 200), new Vector2(0, -25));
            Panel("RadarAxisX", map, new Vector2(205, 1), Vector2.zero, new Color(.2f, .4f, .25f));
            Panel("RadarAxisY", map, new Vector2(1, 195), Vector2.zero, new Color(.2f, .4f, .25f));
            Text("RadarPlayer", map, "▲", new Vector2(20, 24), Vector2.zero, 19);
            var dots = settings.FindProperty("radarIslands"); dots.arraySize = 5;
            for (int i = 0; i < dots.arraySize; i++)
            {
                var dot = Text("RadarIsland" + i, map, "", new Vector2(105, 24), Vector2.zero, 16);
                dot.color = new Color(.5f, 1, .55f);
                dots.GetArrayElementAtIndex(i).objectReferenceValue = dot;
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
        }

        private static void EnsureBossHud(GameObject root)
        {
            if (root.transform.Find("BossPanel") != null) return;
            var panel = Rect("BossPanel", root.transform, new Vector2(640, 90), new Vector2(0, -85), new Vector2(.5f, 1));
            var settings = new SerializedObject(root.GetComponent<HowToFishHudPresenter>());
            Bind(settings, "bossPanel", panel.gameObject);
            Bind(settings, "bossStatus", Text("BossStatus", panel, "", new Vector2(640, 35), new Vector2(0, 25), 25));
            foreach (var bar in new[] { ("bossHealth", "BossHealth", 0f, new Color(.8f, .12f, .08f)), ("bossEscape", "BossEscape", -17f, Color.white) })
            {
                Panel(bar.Item2 + "Track", panel, new Vector2(580, 10), new Vector2(0, bar.Item3), new Color(.08f, .08f, .09f, .8f));
                var fill = Panel(bar.Item2, panel, new Vector2(580, 10), new Vector2(0, bar.Item3), bar.Item4);
                fill.type = Image.Type.Filled;
                fill.fillMethod = Image.FillMethod.Horizontal;
                fill.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
                Bind(settings, bar.Item1, fill);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
        }

        private static void GenerateHudBinding(GameObject root)
        {
            var presenter = new SerializedObject(root.GetComponent<HowToFishHudPresenter>());
            presenter.FindProperty("m_EditorClassIdentifier").stringValue = "Hotfix::" + typeof(HowToFishHudPresenter).FullName;
            presenter.ApplyModifiedPropertiesWithoutUndo();
            var nodes = MvcPrefabScanner.Scan(root);
            foreach (var node in nodes)
            {
                var component = node.gameObject.GetComponent<HowToFishHudPresenter>() as Component
                    ?? node.gameObject.GetComponent<Button>() as Component
                    ?? node.gameObject.GetComponent<TextMeshProUGUI>() as Component
                    ?? node.gameObject.GetComponent<Image>();
                if (component != null) Select(node, component.GetType());
            }
            var settings = new MvcBindSettings
            {
                prefabPath = HudPath, moduleName = "HowToFish", viewName = "HowToFishHudView", namespaceName = "Hotfix",
                address = MvcBindPathUtility.ToRuntimeAddress(HudPath), viewType = ViewType.View,
                layer = UILayer.Decorate, viewMode = UIViewMode.Widget, mask = MaskType.None,
                isHotfix = true, enableOnInit = true, destroyOnHide = true,
                useCustomModuleOutputDirectory = true,
                customModuleOutputDirectory = "Assets/Scripts/Hotfix/Demos/HowToFish/UI",
                uiTransitionType = typeof(EmptyUITransition).FullName
            };
            settings.outputFolder = MvcBindPathUtility.ToOutputFolder(settings);
            if (!MvcBindComponentWindowBridge.GenerateAndBind(root, settings, nodes, false, out _, out var message)) throw new InvalidOperationException(message);
            PrefabUtility.SaveAsPrefabAsset(root, HudPath);
        }

        private static void BuildScene()
        {
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var sand = Material("Sand", new Color(.78f, .69f, .48f));
                var grass = Material("Grass", new Color(.26f, .38f, .14f));
                var rock = Material("CoastalRock", new Color(.34f, .31f, .26f));
                BuildIsland(grass, sand, rock);
                Primitive("Ocean", PrimitiveType.Plane, Vector3.down * .03f, new Vector3(300, 1, 300), Material("Ocean", new Color(.1f, .39f, .58f)), false);
                var light = new GameObject("Sun").AddComponent<Light>();
                light.type = LightType.Directional; light.intensity = 1.6f; light.shadows = LightShadows.Soft;
                light.transform.rotation = Quaternion.Euler(45, -35, 0);
                RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.52f, .60f, .67f);
                RenderSettings.fog = true; RenderSettings.fogColor = new Color(.44f, .66f, .79f); RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogStartDistance = 180; RenderSettings.fogEndDistance = 650;
                var wood = Material("WeatheredWood", new Color(.34f, .21f, .13f));
                Primitive("LighthouseBlockout", PrimitiveType.Cylinder, new Vector3(-5, 7, 2), new Vector3(3.4f, 4.5f, 3.4f), Material("LighthouseStone", new Color(.77f, .76f, .68f)));
                Primitive("LighthouseRoofBlockout", PrimitiveType.Cylinder, new Vector3(-5, 12, 2), new Vector3(4, .6f, 4), Material("LighthouseRoof", new Color(.44f, .12f, .08f)));
                var keeper = Primitive("KeeperBlockout", PrimitiveType.Capsule, new Vector3(-4, 3.4f, -3), new Vector3(.65f, .95f, .65f), Material("KeeperClothes", new Color(.22f, .24f, .25f)));
                Station(keeper, HowToFishStationKind.Keeper, "与灯塔看守人交谈");
                var anvil = Primitive("Anvil", PrimitiveType.Cube, new Vector3(-7, 2.9f, -2), new Vector3(.8f, .6f, .5f), Material("AnvilIron", new Color(.15f, .16f, .18f)));
                Station(anvil, HowToFishStationKind.Anvil, "升级当前武器");
                var player = BuildPlayer();
                var boat = BuildBoat(wood);
                var world = new GameObject("HowToFishWorld").AddComponent<HowToFishWorld>();
                var state = new SerializedObject(world);
                Bind(state, "catalog", AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset"));
                Bind(state, "inputTemplate", AssetDatabase.LoadAssetAtPath<InputActionAsset>(Root + "/Data/PlayerInput.asset"));
                Bind(state, "player", player); Bind(state, "boat", boat);
                var start = new GameObject("StartPoint").transform; start.position = new Vector3(0, 3, -15);
                Bind(state, "startPoint", start);
                player.transform.position = start.position;
                var points = state.FindProperty("clamPoints"); points.arraySize = 12;
                for (int i = 0; i < 12; i++)
                {
                    var point = new GameObject("ClamSpawn" + i).transform;
                    float angle = (-.7f + i * .13f);
                    point.position = new Vector3(Mathf.Sin(angle) * 23, 1.6f, -Mathf.Cos(angle) * 23);
                    points.GetArrayElementAtIndex(i).objectReferenceValue = point;
                }
                state.ApplyModifiedPropertiesWithoutUndo();
                if (!EditorSceneManager.SaveScene(scene, Root + "/Scenes/Main.unity")) throw new InvalidOperationException("场景保存失败。");
            }
            finally { SceneManager.SetActiveScene(previous); EditorSceneManager.CloseScene(scene, true); }
        }

        private static HowToFishPlayer BuildPlayer()
        {
            var root = new GameObject("Fisher");
            var motor = root.AddComponent<CharacterController>(); motor.height = 1.8f; motor.radius = .28f; motor.center = Vector3.up * .9f; motor.stepOffset = .35f;
            var eye = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            eye.transform.SetParent(root.transform, false); eye.transform.localPosition = Vector3.up * 1.65f;
            eye.tag = "MainCamera"; eye.nearClipPlane = .03f; eye.farClipPlane = 1200; eye.fieldOfView = 75;
            eye.clearFlags = CameraClearFlags.SolidColor; eye.backgroundColor = new Color(.44f, .66f, .79f);
            eye.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var equipment = new GameObject("Equipment").transform; equipment.SetParent(eye.transform, false);
            var line = new GameObject("FishingLine").AddComponent<LineRenderer>(); line.transform.SetParent(root.transform, false);
            line.sharedMaterial = Material("FishingLine", new Color(.9f, .92f, .87f)); line.startWidth = .007f; line.endWidth = .003f;
            line.positionCount = 18; line.enabled = false;
            const string bobberPath = Root + "/Prefabs/Items/Bobber.prefab";
            var bobber = AssetDatabase.LoadAssetAtPath<Rigidbody>(bobberPath);
            if (bobber == null)
            {
                var floating = Primitive("Bobber", PrimitiveType.Sphere, Vector3.zero, Vector3.one * .09f, Material("BobberRed", new Color(.92f, .18f, .08f)));
                var body = floating.AddComponent<Rigidbody>(); body.mass = .04f; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                bobber = PrefabUtility.SaveAsPrefabAsset(floating, bobberPath).GetComponent<Rigidbody>(); Object.DestroyImmediate(floating);
            }
            var fishing = root.AddComponent<HowToFishFishingRig>(); var rig = new SerializedObject(fishing); Bind(rig, "bobberPrefab", bobber); Bind(rig, "line", line); rig.ApplyModifiedPropertiesWithoutUndo();
            var player = root.AddComponent<HowToFishPlayer>(); var settings = new SerializedObject(player);
            Bind(settings, "eye", eye); Bind(settings, "equipmentRoot", equipment); Bind(settings, "fishing", fishing); settings.ApplyModifiedPropertiesWithoutUndo();
            return player;
        }

        private static HowToFishBoat BuildBoat(Material wood)
        {
            var root = new GameObject("FishingBoat"); root.transform.position = new Vector3(8, .2f, -30);
            var deck = Primitive("DeckBlockout", PrimitiveType.Cube, Vector3.zero, new Vector3(2.1f, .18f, 4.5f), wood); deck.transform.SetParent(root.transform, false);
            for (int side = -1; side <= 1; side += 2)
            {
                var wall = Primitive("Gunwale" + side, PrimitiveType.Cube, Vector3.zero, new Vector3(.15f, .6f, 4.5f), wood);
                wall.transform.SetParent(root.transform, false); wall.transform.localPosition = new Vector3(side * 1.1f, .3f, 0);
            }
            var body = root.AddComponent<Rigidbody>(); body.mass = 180; body.linearDamping = .6f; body.angularDamping = 3;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            var seat = new GameObject("DriverSeat").transform; seat.SetParent(root.transform, false); seat.localPosition = new Vector3(0, .2f, -.8f);
            var exit = new GameObject("ExitPoint").transform; exit.SetParent(root.transform, false); exit.localPosition = new Vector3(0, .3f, -1.5f);
            var wheel = Primitive("WheelBlockout", PrimitiveType.Cylinder, Vector3.zero, new Vector3(.5f, .08f, .5f), wood);
            wheel.transform.SetParent(root.transform, false); wheel.transform.localPosition = new Vector3(0, 1, 0);
            wheel.transform.localRotation = Quaternion.Euler(65, 0, 0); Station(wheel, HowToFishStationKind.BoatWheel, "驾驶船只");
            var boat = root.AddComponent<HowToFishBoat>(); var settings = new SerializedObject(boat); Bind(settings, "seat", seat); Bind(settings, "exitPoint", exit); settings.ApplyModifiedPropertiesWithoutUndo();
            return boat;
        }

        private static GameObject BuildIsland(Material grass, Material sand, Material rock,
            string name = "LighthouseIsland", Vector3 position = default, bool lake = false)
        {
            bool desert = name == "DesertIsland";
            bool rocks = name == "RocksIsland";
            var meshPath = Root + (rocks ? "/Data/RocksTerrain.asset" : desert ? "/Data/DesertTerrain.asset" : lake ? "/Data/ForestTerrain.asset" : "/Data/IslandTerrain.asset");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh == null)
            {
                var vertices = new List<Vector3>(); var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
                float[] radius = desert || rocks ? new[] { 0f, 14, 35, 53, 64, 72 } : lake ? new[] { 0f, 15, 21, 35, 48, 60 } : new[] { 0f, 7, 20, 25, 30, 36 };
                float[] height = rocks ? new[] { 4.8f, 4.8f, 4.8f, 1.2f, .2f, -3f } : desert ? new[] { 3.5f, 3.1f, 2.6f, 1f, .2f, -3f } : lake ? new[] { -4f, -3, .3f, 2.4f, .6f, -3 } : new[] { 2.4f, 2.4f, 2.1f, .6f, -.4f, -3f };
                Vector3 Point(int ring, int index)
                {
                    float angle = index * Mathf.PI * 2 / 48;
                    float r = radius[ring] * (1 + (ring == 0 ? 0 : Mathf.Sin(angle * 5) * .035f));
                    return new Vector3(Mathf.Sin(angle) * r, height[ring], Mathf.Cos(angle) * r);
                }
                void Triangle(Vector3 a, Vector3 b, Vector3 c, int submesh)
                { int first = vertices.Count; vertices.Add(a); vertices.Add(c); vertices.Add(b); triangles[submesh].AddRange(new[] { first, first + 1, first + 2 }); }
                for (int ring = 0; ring < 5; ring++) for (int i = 0; i < 48; i++)
                {
                    int sub = lake ? (ring < 2 ? 2 : ring < 4 ? 0 : 1) : (ring < 2 ? 0 : ring < 4 ? 1 : 2);
                    Triangle(Point(ring, i), Point(ring + 1, i + 1), Point(ring + 1, i), sub);
                    if (ring > 0) Triangle(Point(ring, i), Point(ring, i + 1), Point(ring + 1, i + 1), sub);
                }
                mesh = new Mesh { name = "IslandTerrain", subMeshCount = 3 }; mesh.SetVertices(vertices);
                for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
                mesh.RecalculateNormals(); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, meshPath);
            }
            var island = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            island.transform.position = position;
            island.GetComponent<MeshFilter>().sharedMesh = mesh; island.GetComponent<MeshCollider>().sharedMesh = mesh;
            island.GetComponent<MeshRenderer>().sharedMaterials = new[] { grass, sand, rock };
            return island;
        }

        private static GameObject Product(string id, Vector3 position, Quaternion? rotation = null, int island = 0)
        {
            var definition = AssetDatabase.LoadAssetAtPath<HowToFishCatalog>(Root + "/Data/Catalog.asset").FindItem(id);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" +
                (id == "StandardBossLure" ? "StandardLure" : id == "ProfessionalBossLure" ? "ProfessionalLure" : id) + ".fbx");
            GameObject item;
            if (source != null)
            {
                item = (GameObject)PrefabUtility.InstantiatePrefab(source); item.transform.position = position;
                foreach (var renderer in item.GetComponentsInChildren<Renderer>())
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(m => AssetDatabase.LoadAssetAtPath<Material>(Root + "/Art/Materials/" + m.name + ".mat")).ToArray();
                var bounds = new Bounds(item.transform.position, Vector3.zero);
                foreach (var renderer in item.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                var collider = item.AddComponent<BoxCollider>();
                collider.center = bounds.center - item.transform.position;
                collider.size = Vector3.Max(bounds.size, Vector3.one * .14f);
            }
            else item = Primitive(id + "DisplayBlockout", PrimitiveType.Cylinder, position, new Vector3(.16f, .22f, .16f), Material("ShopDisplay", new Color(.67f, .35f, .11f)));
            item.transform.rotation = rotation ?? Quaternion.identity;
            Station(item, HowToFishStationKind.Product, "购买 " + definition.DisplayName, id);
            var settings = new SerializedObject(item.GetComponent<HowToFishStation>());
            settings.FindProperty("island").intValue = island; settings.ApplyModifiedPropertiesWithoutUndo();
            return item;
        }

        private static void Station(GameObject target, HowToFishStationKind kind, string label, string id = "")
        {
            var settings = new SerializedObject(target.AddComponent<HowToFishStation>());
            settings.FindProperty("kind").enumValueIndex = (int)kind; settings.FindProperty("label").stringValue = label; settings.FindProperty("itemId").stringValue = id; settings.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Primitive(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            var result = GameObject.CreatePrimitive(type); result.name = name; result.transform.position = position; result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Object.DestroyImmediate(result.GetComponent<Collider>());
            return result;
        }

        private static Material Material(string name, Color color)
        {
            var path = Root + "/Art/Materials/" + name + ".mat"; var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name }; material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .05f);
            AssetDatabase.CreateAsset(material, path); return material;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position, Vector2? anchor = null)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.gameObject.layer = LayerMask.NameToLayer("UI");
            if (parent != null) rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor ?? new Vector2(.5f, .5f); rect.pivot = anchor ?? new Vector2(.5f, .5f);
            rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }

        private static TextMeshProUGUI Text(string name, Transform parent, string value, Vector2 size, Vector2 position, int fontSize, Vector2? anchor = null)
        {
            var text = Rect(name, parent, size, position, anchor).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Font; text.fontSize = fontSize; text.text = value; text.color = new Color(.96f, .94f, .86f); text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false; return text;
        }

        private static Image Panel(string name, Transform parent, Vector2 size, Vector2 position, Color color)
        { var image = Rect(name, parent, size, position).gameObject.AddComponent<Image>(); image.color = color; return image; }

        private static Button Button(string name, Transform parent, string value, Vector2 size, Vector2 position, out TextMeshProUGUI label)
        {
            var image = Panel(name, parent, size, position, new Color(.20f, .32f, .36f));
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            label = Text(name + "Label", image.transform, value, size - new Vector2(12, 8), Vector2.zero, 24); return button;
        }

        private static void Bind(SerializedObject owner, string field, Object value) => owner.FindProperty(field).objectReferenceValue = value;
        private static void Select(MvcBindNode node, Type type)
        { node.selectedComponentType = type; node.selectedComponentTypeName = type.FullName; node.selectedComponentTypes.Clear(); node.selectedMethodNames.Clear(); node.selectedMethodNamesByComponentTypeName.Clear(); }
    }
}
