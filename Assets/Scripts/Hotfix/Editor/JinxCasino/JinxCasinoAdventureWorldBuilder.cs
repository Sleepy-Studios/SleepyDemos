using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        private static bool buildFormalArt;

        /// 保存四区完整内容及正式原创模型、图标与音频的离线版本。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/生成P4正式离线冒险")]
        public static void BuildFormalAdventure()
        {
            buildAdventure = true; buildFullRegions = true; buildFormalArt = true;
            try { BackupContentSource(); PrepareFormalImports(); BuildPrototypeAndHub(); }
            finally { buildAdventure = false; buildFullRegions = false; buildFormalArt = false; }
        }
        /// 保存一区三机台、购物/事件/额度主循环的离线场景与界面。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/生成P1离线完整局")]
        public static void BuildFirstAdventure()
        {
            buildAdventure = true; buildFullRegions = false;
            try { BackupContentSource(); BuildPrototypeAndHub(); }
            finally { buildAdventure = false; buildFullRegions = false; }
        }

        /// 保存四区和全部机台的离线冒险，联网仍独立接入。
        [MenuItem("Tools/SleepyDemos/整蛊赌场/生成P2四区冒险")]
        public static void BuildFourAreaAdventure()
        {
            buildAdventure = true; buildFullRegions = true;
            try { BackupContentSource(); BuildPrototypeAndHub(); }
            finally { buildAdventure = false; buildFullRegions = false; }
        }

        private static void BackupContentSource()
        {
            // 阶段升级保留旧生成资源的可恢复副本，不删除此前验证产物或覆盖其它Demo。
            string backupRoot = "Library/JinxCasino/ContentBaseline/" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            foreach (string file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(Root.Length).TrimStart('/', '\\');
                string target = Path.Combine(backupRoot, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)); File.Copy(file, target, false);
            }
            Directory.CreateDirectory(backupRoot + "/Hub"); File.Copy(HubPath, backupRoot + "/Hub/MainMenuView.prefab", false);
        }

        private static void ConfigureAdventureWorld(JinxCasinoController controller, Transform player, Transform[] anchors, TMP_FontAsset font)
        {
            var config = EnsureAdventureSettings();
            var areaRoot = new GameObject("StreetLuckyHall");
            var safe = new GameObject("StreetSafeSpawn").transform; safe.SetParent(areaRoot.transform, false); safe.position = new Vector3(0, 0.03f, buildFullRegions ? -7.5f : -4.5f);
            var contents = new GameObject("StreetContent"); contents.transform.SetParent(areaRoot.transform, false);
            for (int index = 0; index < anchors.Length; index++)
            {
                anchors[index].parent.SetParent(contents.transform, true);
                anchors[index].parent.gameObject.AddComponent<JinxCasinoStation>().Configure((CasinoGameKind)index, 0, anchors[index]);
            }
            var area = areaRoot.AddComponent<JinxCasinoWorldArea>(); area.Configure(0, safe, contents, null);
            var sign = GameObject.Find("CasinoSign");
            if (sign != null) sign.GetComponent<TextMeshPro>().text = "街角幸运厅 · 倒霉蛋俱乐部";
            var buddy = BuildFirstBuddy(contents.transform);
            WorldLabel("BuddyName", font, "助手 · 小霉", new Vector3(-6, 2.6f, -1), 3, contents.transform);
            var effects = controller.gameObject.AddComponent<JinxCasinoSceneEffects>();
            var worldAreas = new List<JinxCasinoWorldArea> { area };
            var buddies = new List<Transform> { buddy };
            if (buildFullRegions)
            {
                BuildAdditionalAreas(font, contents.transform, area, worldAreas, buddies);
                foreach (var region in worldAreas)
                {
                    var content = region.transform.Find(region.Index == 0 ? "StreetContent" : "Contents");
                    var stand = BuildExtraStation(new CasinoGameDefinition { Kind = CasinoGameKind.Slots, AreaIndex = region.Index, Name = "轮换展位 · 等待事件" },
                        region.SafePosition + new Vector3(-6.5f, -0.03f, 2), font, content);
                    stand.name = "RotationStand"; stand.enabled = false;
                    stand.gameObject.AddComponent<JinxCasinoRotationStand>().Configure(controller, stand, stand.GetComponentInChildren<TextMeshPro>());
                }
            }
            effects.Configure(player, buddies.ToArray(), worldAreas.Select(value => value.transform.Find(value.Index == 0 ? "StreetSafeSpawn" : "SafeSpawn")).ToArray(),
                buildFullRegions ? EnsureSceneEffectPrefabs(font) : new[] { new CasinoEffectPrefabBinding { Kind = "Bubble", Prefab = EnsureBubbleEffectPrefab() } });
            if (buildFullRegions) ConfigureSavedFacilities(controller, effects, player, worldAreas.ToArray(), buddies.ToArray(), font);
            controller.ConfigureAdventure(config, worldAreas.ToArray(), effects);
            var missions = controller.gameObject.AddComponent<JinxCasinoMissionDirector>();
            missions.Configure(controller, EnsureMissionTargetPrefab(font), effects, buildFullRegions ? EnsureMissionPrefabs(font) : null);
            if (buildFormalArt) ConfigureFormalArt(controller, player, worldAreas.ToArray(), buddies.ToArray(), font);
        }

        private static void BuildAdditionalAreas(TMP_FontAsset font, Transform firstContents, JinxCasinoWorldArea firstArea,
            List<JinxCasinoWorldArea> areas, List<Transform> buddies)
        {
            // 建筑与跨区门不属于按进度隐藏的内容，锁门始终承担物理阻挡。
            var floor = GameObject.Find("Floor"); floor.transform.localScale = new Vector3(18, 0.3f, 24);
            var oldRight = GameObject.Find("RightWall"); Object.DestroyImmediate(oldRight);
            var left = GameObject.Find("LeftWall"); left.transform.localScale = new Vector3(0.25f, 4, 24);
            var back = GameObject.Find("BackWall"); back.transform.position = new Vector3(0, 2, 11.9f);
            var trim = GameObject.Find("BackGoldTrim"); trim.transform.position = new Vector3(0, 2.75f, 11.7f);
            var sign = GameObject.Find("CasinoSign"); sign.transform.position = new Vector3(0, 3.8f, 11.5f);
            var wall = Material("CasinoWall", new Color(0.12f, 0.18f, 0.24f));
            DoorwayWall(firstArea.transform, new Vector3(9, 0, 0), wall);
            var frontMaterial = Material("ClubFront", new Color(0.06f, 0.09f, 0.16f));
            Primitive("StreetFront", PrimitiveType.Cube, new Vector3(0, 2, -11.9f), new Vector3(18, 4, 0.25f), frontMaterial, firstArea.transform);
            var firstGames = CasinoContentCatalog.Games.Where(game => game.AreaIndex == 0 && (int)game.Kind > 2).ToArray();
            for (int index = 0; index < firstGames.Length; index++) BuildExtraStation(firstGames[index], new Vector3((index - 1) * 5, 0, -1), font, firstContents);
            buddies[0].position = new Vector3(6.5f, 0, -6);
            GameObject.Find("BuddyName").transform.position = buddies[0].position + Vector3.up * 2.6f;
            var colors = new[] { new Color(0.05f, 0.21f, 0.3f), new Color(0.29f, 0.13f, 0.07f), new Color(0.14f, 0.13f, 0.34f) };
            for (int areaIndex = 1; areaIndex < 4; areaIndex++)
            {
                var origin = new Vector3(areaIndex * 24, 0, 0);
                var root = new GameObject("Area" + areaIndex).transform; root.position = origin;
                var contents = new GameObject("Contents").transform; contents.SetParent(root, false);
                var spawn = new GameObject("SafeSpawn").transform; spawn.SetParent(root, false); spawn.localPosition = new Vector3(0, 0.03f, -7.5f);
                var floorMaterial = Material("AreaFloor" + areaIndex, colors[areaIndex - 1]);
                Primitive("Floor", PrimitiveType.Cube, origin + new Vector3(0, -0.15f, 0), new Vector3(18, 0.3f, 24), floorMaterial, root);
                Primitive("BackWall", PrimitiveType.Cube, origin + new Vector3(0, 2, 11.9f), new Vector3(18, 4, 0.25f), wall, root);
                Primitive("FrontWall", PrimitiveType.Cube, origin + new Vector3(0, 2, -11.9f), new Vector3(18, 4, 0.25f), wall, root);
                DoorwayWall(root, origin + new Vector3(-9, 0, 0), wall);
                if (areaIndex < 3) DoorwayWall(root, origin + new Vector3(9, 0, 0), wall);
                else Primitive("OuterWall", PrimitiveType.Cube, origin + new Vector3(9, 2, 0), new Vector3(0.25f, 4, 24), wall, root);
                var gate = Primitive("StageGate", PrimitiveType.Cube, origin + new Vector3(-9, 1.8f, -7.5f), new Vector3(0.4f, 3.6f, 3), Material("LockedGate", new Color(0.8f, 0.2f, 0.25f)), root);
                var area = root.gameObject.AddComponent<JinxCasinoWorldArea>(); area.Configure(areaIndex, spawn, contents.gameObject, gate); areas.Add(area);
                WorldLabel("AreaName", font, CasinoContentCatalog.Areas[areaIndex].Name, origin + new Vector3(0, 3.8f, 11.5f), 8, root);
                var games = CasinoContentCatalog.Games.Where(game => game.AreaIndex == areaIndex).ToArray();
                for (int index = 0; index < games.Length; index++)
                    BuildExtraStation(games[index], origin + new Vector3((index % 3 - 1) * 5, 0, 5 - index / 3 * 6), font, contents);
                var buddy = BuildFirstBuddy(contents); buddy.position = origin + new Vector3(6.5f, 0, -6); buddies.Add(buddy);
                WorldLabel("BuddyName", font, "助手 · 小霉", buddy.position + Vector3.up * 2.6f, 3, contents);
                var passage = origin + new Vector3(-12, 0, -7.5f);
                Primitive("PassageFloor", PrimitiveType.Cube, passage + Vector3.down * 0.15f, new Vector3(6, 0.3f, 3.4f), floorMaterial, root);
                foreach (float side in new[] { -1.8f, 1.8f }) Primitive("PassageRail", PrimitiveType.Cube, passage + new Vector3(0, 1, side), new Vector3(6, 2, 0.2f), wall, root);
            }
        }

        private static void DoorwayWall(Transform parent, Vector3 center, Material material)
        {
            Primitive("DoorWallFront", PrimitiveType.Cube, center + new Vector3(0, 2, -10.5f), new Vector3(0.25f, 4, 3), material, parent);
            Primitive("DoorWallBack", PrimitiveType.Cube, center + new Vector3(0, 2, 3), new Vector3(0.25f, 4, 18), material, parent);
        }

        private static JinxCasinoStation BuildExtraStation(CasinoGameDefinition game, Vector3 origin, TMP_FontAsset font, Transform parent)
        {
            var root = new GameObject(game.Kind + "Station").transform; root.SetParent(parent, false); root.position = origin;
            var anchor = new GameObject("Interaction").transform; anchor.SetParent(root, false); anchor.localPosition = new Vector3(0, 0, -1.8f);
            var station = root.gameObject.AddComponent<JinxCasinoStation>(); station.Configure(game.Kind, game.AreaIndex, anchor);
            Primitive("Cabinet", PrimitiveType.Cube, origin + Vector3.up * 0.6f, new Vector3(2.7f, 1.2f, 1.6f), Material("MachineDark", new Color(0.045f, 0.055f, 0.075f)), root);
            Primitive("PlaySurface", PrimitiveType.Cube, origin + Vector3.up * 1.25f, new Vector3(3, 0.15f, 2), Material("MachineDisplay", new Color(0.32f, 0.72f, 0.86f)), root);
            WorldLabel("MachineName", font, game.Name, origin + new Vector3(0, 2.7f, 0.2f), 3.8f, root);
            return station;
        }

        private static JinxCasinoGameSettings EnsureAdventureSettings()
        {
            string path = Root + "/Data/AdventureSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<JinxCasinoGameSettings>(path);
            if (settings == null) { settings = ScriptableObject.CreateInstance<JinxCasinoGameSettings>(); AssetDatabase.CreateAsset(settings, path); }
            var serialized = new SerializedObject(settings);
            var config = serialized.FindProperty("adventure");
            config.FindPropertyRelative("StageCount").intValue = buildFullRegions ? 4 : 1;
            var allowed = config.FindPropertyRelative("AllowedGames");
            allowed.arraySize = buildFullRegions ? 0 : 3;
            for (int index = 0; index < allowed.arraySize; index++) allowed.GetArrayElementAtIndex(index).intValue = index;
            var shop = config.FindPropertyRelative("ShopItemIds");
            string[] firstItems = { "bubble_gun", "extra_time", "stop_loss" };
            shop.arraySize = buildFullRegions ? 0 : firstItems.Length;
            for (int index = 0; index < shop.arraySize; index++) shop.GetArrayElementAtIndex(index).stringValue = firstItems[index];
            var weights = config.FindPropertyRelative("EventWeights");
            var events = CasinoContentCatalog.Events;
            weights.arraySize = buildFullRegions ? 0 : events.Length;
            for (int index = 0; index < weights.arraySize; index++)
            {
                var weight = weights.GetArrayElementAtIndex(index);
                weight.FindPropertyRelative("EventId").stringValue = events[index].Id;
                weight.FindPropertyRelative("Weight").intValue = events[index].Id == "low_stakes" || events[index].Id == "mystery_merchant" || events[index].Id == "chip_rain" ? 1 : 0;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(settings);
            return settings;
        }

        private static Transform BuildFirstBuddy(Transform parent)
        {
            var buddy = new GameObject("Buddy").transform; buddy.SetParent(parent, false); buddy.position = new Vector3(-6, 0, -1);
            var visual = new GameObject("Visual").transform; visual.SetParent(buddy, false);
            var coat = Material("BuddyCoat", new Color(0.85f, 0.23f, 0.44f));
            var face = Material("BuddyFace", new Color(1, 0.8f, 0.48f));
            var eye = Material("BuddyEyes", new Color(0.025f, 0.035f, 0.06f));
            Primitive("Body", PrimitiveType.Capsule, buddy.position + new Vector3(0, 0.85f, 0), new Vector3(0.65f, 0.7f, 0.65f), coat, visual);
            Primitive("PaperHead", PrimitiveType.Cube, buddy.position + new Vector3(0, 1.75f, 0), new Vector3(0.75f, 0.6f, 0.28f), face, visual);
            for (int index = 0; index < 2; index++) Primitive("Eye" + index, PrimitiveType.Sphere, buddy.position + new Vector3(-0.17f + index * 0.34f, 1.85f, -0.19f), Vector3.one * 0.13f, eye, visual);
            foreach (var collider in visual.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            var proxy = buddy.gameObject.AddComponent<CapsuleCollider>(); proxy.height = 1.8f; proxy.radius = 0.3f; proxy.center = new Vector3(0, 0.9f, 0);
            return buddy;
        }

        private static GameObject EnsureBubbleEffectPrefab()
        {
            EnsureFolder(Root + "/Prefabs/Effects");
            string path = Root + "/Prefabs/Effects/Bubble.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing;
            var root = new GameObject("Bubble");
            try
            {
                var material = Material("BubbleFilm", new Color(0.24f, 0.78f, 0.97f, 0.25f));
                material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000; EditorUtility.SetDirty(material);
                var shell = Primitive("Film", PrimitiveType.Sphere, Vector3.zero, Vector3.one * 1.8f, material, root.transform);
                Object.DestroyImmediate(shell.GetComponent<Collider>());
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static JinxCasinoMissionTarget EnsureMissionTargetPrefab(TMP_FontAsset font)
        {
            EnsureFolder(Root + "/Prefabs/Missions");
            string path = Root + "/Prefabs/Missions/TaskTarget.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null) return existing.GetComponent<JinxCasinoMissionTarget>();
            var root = new GameObject("TaskTarget", typeof(SphereCollider), typeof(JinxCasinoMissionTarget));
            try
            {
                var trigger = root.GetComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = 0.65f;
                var body = Primitive("Coin", PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.55f, 0.07f, 0.55f), Material("TaskGold", new Color(1, 0.71f, 0.15f)), root.transform);
                body.transform.localRotation = Quaternion.Euler(90, 0, 0); Object.DestroyImmediate(body.GetComponent<Collider>());
                WorldLabel("TaskLabel", font, "筹码目标", new Vector3(0, 0.7f, 0), 2.4f, root.transform);
                var serialized = new SerializedObject(root.GetComponent<JinxCasinoMissionTarget>());
                SetReference(serialized, "label", root.GetComponentInChildren<TextMeshPro>()); serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<JinxCasinoMissionTarget>();
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
