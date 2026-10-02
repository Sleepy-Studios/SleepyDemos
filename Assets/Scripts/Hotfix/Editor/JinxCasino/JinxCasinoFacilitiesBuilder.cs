using System;
using System.Collections.Generic;
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hotfix.Editor.JinxCasino
{
    public static partial class JinxCasinoPrototypeBuilder
    {
        private static void ConfigureSavedFacilities(JinxCasinoController controller, JinxCasinoSceneEffects effects,
            Transform player, JinxCasinoWorldArea[] areas, Transform[] buddies, TMP_FontAsset font)
        {
            var facilities = new List<JinxCasinoAreaFacilities>();
            foreach (var area in areas)
            {
                var contents = area.transform.Find(area.Index == 0 ? "StreetContent" : "Contents");
                var lamps = new List<Light>();
                foreach (float depth in new[] { -4f, 4f })
                {
                    var lamp = new GameObject("AreaLight").AddComponent<Light>(); lamp.transform.SetParent(area.transform, false);
                    lamp.transform.localPosition = new Vector3(0, 3.3f, depth); lamp.type = LightType.Point;
                    lamp.range = 15; lamp.intensity = 2; lamp.color = area.Index == 2 ? new Color(1, 0.72f, 0.42f) : new Color(0.6f, 0.85f, 1);
                    lamp.shadows = LightShadows.None; lamps.Add(lamp);
                }
                var shortcut = new GameObject("InternalShortcut").transform; shortcut.SetParent(contents, false);
                // 内部通道以独立短隔墙绕行；钥匙只移除内部小门，跨区额度门不受影响。
                var side = area.SafePosition + new Vector3(7, -0.03f, 4);
                Primitive("ShortcutSide", PrimitiveType.Cube, side + new Vector3(-0.7f, 1.2f, 0), new Vector3(0.2f, 2.4f, 4), Material("ShortcutWall", new Color(0.12f, 0.18f, 0.24f)), shortcut);
                var gate = Primitive("ShortcutDoor", PrimitiveType.Cube, side + new Vector3(0.25f, 1.1f, -1.8f), new Vector3(1.7f, 2.2f, 0.2f), Material("ShortcutDoor", new Color(0.34f, 0.74f, 0.57f)), shortcut);
                WorldLabel("ShortcutLabel", font, "钥匙捷径", gate.transform.position + Vector3.up * 1.4f, 2.8f, shortcut);
                var tables = new List<JinxCasinoMovingTable>();
                var stations = contents.GetComponentsInChildren<JinxCasinoStation>(true);
                foreach (var station in stations)
                {
                    var collider = station.GetComponentsInChildren<BoxCollider>().OrderByDescending(value => value.size.x * Mathf.Abs(value.transform.lossyScale.x)
                        * value.size.z * Mathf.Abs(value.transform.lossyScale.z)).FirstOrDefault();
                    if (collider == null) continue;
                    var moving = station.gameObject.AddComponent<JinxCasinoMovingTable>();
                    moving.Configure(station.transform, collider, new Vector3(0.7f, 0, 0), station.Game); tables.Add(moving);
                }
                var facility = area.gameObject.AddComponent<JinxCasinoAreaFacilities>();
                facility.Configure(area.Index, area.transform.Find(area.Index == 0 ? "StreetSafeSpawn" : "SafeSpawn"), lamps.ToArray(), gate,
                    tables.ToArray(), stations.Select(value => value.transform.Find("Interaction") ?? value.transform).ToArray());
                facilities.Add(facility);
            }
            var costume = Material("DisguisePink", new Color(1, 0.3f, 0.65f));
            var ink = Material("InkPaint", new Color(0.055f, 0.015f, 0.095f));
            effects.ConfigureFacilities(facilities.ToArray(), costume, EnsureCarriedGoldPrefab(), null, -1);
            effects.ConfigureMaterials(costume, ink);
            var bindings = new List<CasinoActorEffectBinding>();
            var localVisual = new GameObject("LocalVisual").transform; localVisual.SetParent(player, false);
            bindings.Add(new CasinoActorEffectBinding { Target = player, VisualRoot = localVisual, CostumeRenderers = Array.Empty<Renderer>() });
            foreach (var buddy in buddies)
            {
                var visual = buddy.Find("Visual");
                if (visual == null) throw new InvalidOperationException("助手缺少独立Visual根。");
                bindings.Add(new CasinoActorEffectBinding { Target = buddy, VisualRoot = visual, CostumeRenderers = visual.GetComponentsInChildren<Renderer>() });
            }
            effects.ConfigureActors(bindings.ToArray());
            var light = GameObject.Find("MainLight"); if (light != null) light.GetComponent<Light>().intensity = 0.45f;
            RenderSettings.ambientLight = new Color(0.13f, 0.15f, 0.2f);
        }

        private static CasinoEffectPrefabBinding[] EnsureSceneEffectPrefabs(TMP_FontAsset font)
        {
            string[] kinds = { "Bubble", "BubbleStorm", "Banana", "SpringPunch", "FakeJackpot", "Ink", "Magnet", "Horn", "Disguise", "Teleport", "PortalFault", "SlipperyFloor", "CooperationHint" };
            return kinds.Select(kind => new CasinoEffectPrefabBinding { Kind = kind, Prefab = kind == "Bubble" ? EnsureBubbleEffectPrefab() : EnsureEffectPrefab(kind, font) }).ToArray();
        }

        private static GameObject EnsureEffectPrefab(string kind, TMP_FontAsset font)
        {
            EnsureFolder(Root + "/Prefabs/Effects");
            string path = Root + "/Prefabs/Effects/" + kind + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject(kind);
            try
            {
                var gold = Material("TaskGold", new Color(1, 0.71f, 0.15f));
                var pink = Material("EffectPink", new Color(1, 0.24f, 0.62f));
                var blue = Material("EffectBlue", new Color(0.17f, 0.68f, 1));
                if (kind == "Banana" || kind == "SlipperyFloor")
                {
                    for (int index = 0; index < 3; index++)
                    {
                        var blade = Primitive("Peel" + index, PrimitiveType.Capsule, new Vector3(0, -0.82f, 0), new Vector3(0.12f, 0.3f, 0.12f), gold, root.transform);
                        blade.transform.rotation = Quaternion.Euler(80, index * 120, 0);
                    }
                }
                else if (kind == "Horn")
                {
                    var bell = Primitive("Bell", PrimitiveType.Cylinder, new Vector3(0, 0.3f, -0.65f), new Vector3(0.55f, 0.08f, 0.55f), gold, root.transform);
                    bell.transform.rotation = Quaternion.Euler(90, 0, 0);
                    Primitive("Bulb", PrimitiveType.Sphere, new Vector3(0, 0.3f, -0.3f), Vector3.one * 0.4f, pink, root.transform);
                }
                else if (kind == "SpringPunch")
                {
                    Primitive("Glove", PrimitiveType.Sphere, new Vector3(0, 0, -0.55f), new Vector3(0.45f, 0.4f, 0.5f), pink, root.transform);
                    Primitive("Wrist", PrimitiveType.Cylinder, new Vector3(0, 0, -0.25f), new Vector3(0.23f, 0.2f, 0.23f), gold, root.transform).transform.rotation = Quaternion.Euler(90, 0, 0);
                }
                else
                {
                    for (int index = 0; index < 7; index++)
                    {
                        float angle = index * Mathf.PI * 2 / 7;
                        var paper = Primitive("Paper" + index, kind == "BubbleStorm" ? PrimitiveType.Sphere : PrimitiveType.Cube,
                            new Vector3(Mathf.Cos(angle) * 0.9f, Mathf.Sin(angle) * 0.8f, 0), kind == "BubbleStorm" ? Vector3.one * 0.3f : new Vector3(0.17f, 0.25f, 0.04f), index % 2 == 0 ? blue : pink, root.transform);
                        paper.transform.localRotation = Quaternion.Euler(index * 15, index * 33, index * 47);
                    }
                }
                if (kind == "FakeJackpot") WorldLabel("PrankLabel", font, "假大奖 · 整蛊", new Vector3(0, 1.4f, 0), 5, root.transform);
                foreach (var collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                string clipName = kind == "Horn" ? "Horn" : kind == "FakeJackpot" ? "Win" : kind == "Teleport" || kind == "PortalFault" ? "Charge" : "Boing";
                var audio = root.AddComponent<AudioSource>(); audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + clipName + ".wav");
                audio.playOnAwake = false; audio.volume = 0.25f; audio.spatialBlend = 0.8f; audio.minDistance = 1; audio.maxDistance = 14;
                if (audio.clip == null) throw new InvalidOperationException("缺少原创效果音频：" + clipName);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static GameObject EnsureCarriedGoldPrefab()
        {
            EnsureFolder(Root + "/Prefabs/Missions"); string path = Root + "/Prefabs/Missions/CarriedGold.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing;
            var root = new GameObject("CarriedGold");
            try
            {
                Primitive("GoldCase", PrimitiveType.Cube, Vector3.zero, new Vector3(0.65f, 0.35f, 0.4f), Material("TaskGold", new Color(1, 0.71f, 0.15f)), root.transform);
                Primitive("CaseLock", PrimitiveType.Cube, new Vector3(0, 0, 0.22f), new Vector3(0.14f, 0.2f, 0.07f), Material("MachineDark", new Color(0.045f, 0.055f, 0.075f)), root.transform);
                foreach (var collider in root.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
                return PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static CasinoMissionPrefabBinding[] EnsureMissionPrefabs(TMP_FontAsset font)
        {
            return new[]
            {
                new CasinoMissionPrefabBinding { EventId = "chip_rain", Prefab = EnsureMissionTargetPrefab(font) },
                new CasinoMissionPrefabBinding { EventId = "mascot_chase", Prefab = EnsureSpecificMissionPrefab("Mascot", font) },
                new CasinoMissionPrefabBinding { EventId = "gold_delivery", PointIndex = 0, Prefab = EnsureSpecificMissionPrefab("GoldSource", font) },
                new CasinoMissionPrefabBinding { EventId = "gold_delivery", PointIndex = 1, Prefab = EnsureSpecificMissionPrefab("GoldDestination", font) },
                new CasinoMissionPrefabBinding { EventId = "power_relay", Prefab = EnsureSpecificMissionPrefab("Relay", font) },
                new CasinoMissionPrefabBinding { EventId = "power_repair", Prefab = EnsureSpecificMissionPrefab("Repair", font) },
                new CasinoMissionPrefabBinding { EventId = "sync_buttons", Prefab = EnsureSpecificMissionPrefab("Sync", font) }
            };
        }

        private static JinxCasinoMissionTarget EnsureSpecificMissionPrefab(string kind, TMP_FontAsset font)
        {
            EnsureFolder(Root + "/Prefabs/Missions"); string path = Root + "/Prefabs/Missions/" + kind + ".prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (existing != null) return existing.GetComponent<JinxCasinoMissionTarget>();
            var root = new GameObject(kind, typeof(SphereCollider), typeof(JinxCasinoMissionTarget));
            try
            {
                var trigger = root.GetComponent<SphereCollider>(); trigger.isTrigger = true; trigger.radius = 0.7f;
                var dark = Material("MachineDark", new Color(0.045f, 0.055f, 0.075f));
                var gold = Material("TaskGold", new Color(1, 0.71f, 0.15f));
                var blue = Material("EffectBlue", new Color(0.17f, 0.68f, 1));
                if (kind == "Mascot")
                {
                    Primitive("MascotBody", PrimitiveType.Capsule, new Vector3(0, -0.15f, 0), new Vector3(0.55f, 0.5f, 0.55f), gold, root.transform);
                    Primitive("MascotFace", PrimitiveType.Cube, new Vector3(0, 0.3f, -0.3f), new Vector3(0.55f, 0.35f, 0.12f), dark, root.transform);
                }
                else if (kind == "GoldSource")
                {
                    var model = (GameObject)PrefabUtility.InstantiatePrefab(EnsureCarriedGoldPrefab(), root.transform);
                    model.transform.localPosition = Vector3.zero;
                }
                else if (kind == "GoldDestination")
                {
                    Primitive("DeliveryBase", PrimitiveType.Cylinder, new Vector3(0, -0.45f, 0), new Vector3(1.2f, 0.16f, 1.2f), blue, root.transform);
                    Primitive("DeliveryFlag", PrimitiveType.Cube, new Vector3(0.5f, 0.2f, 0), new Vector3(0.08f, 1.3f, 0.08f), gold, root.transform);
                }
                else
                {
                    Primitive("Console", PrimitiveType.Cube, new Vector3(0, -0.35f, 0), new Vector3(0.65f, 0.7f, 0.65f), dark, root.transform);
                    var top = Primitive("Button", kind == "Sync" ? PrimitiveType.Cylinder : PrimitiveType.Sphere, new Vector3(0, 0.1f, 0), new Vector3(0.45f, 0.12f, 0.45f), kind == "Repair" ? gold : blue, root.transform);
                    if (kind == "Relay") Primitive("Battery", PrimitiveType.Cube, new Vector3(0, 0.2f, 0), new Vector3(0.3f, 0.35f, 0.3f), blue, root.transform);
                }
                foreach (var collider in root.GetComponentsInChildren<Collider>()) if (collider != trigger) Object.DestroyImmediate(collider);
                WorldLabel("TaskLabel", font, kind, Vector3.up * 0.9f, 3, root.transform);
                var serialized = new SerializedObject(root.GetComponent<JinxCasinoMissionTarget>());
                SetReference(serialized, "label", root.GetComponentInChildren<TextMeshPro>()); serialized.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, path).GetComponent<JinxCasinoMissionTarget>();
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
