using System;
using System.Collections;
using System.Collections.Generic;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 场景接收器的幂等、恢复和安全物理边界，不替代正式Prefab与真机表现验收。
    public sealed class JinxCasinoSceneEffectsTests
    {
        private readonly List<Object> resources = new List<Object>();
        private GameObject root;
        private JinxCasinoSceneEffects effects;
        private Transform player;
        private Transform buddy;
        private Transform safe0;
        private Transform safe1;
        private BoxCollider floor;
        private JinxCasinoAreaFacilities area0;
        private JinxCasinoAreaFacilities area1;
        private GameObject gate;
        private Light light;
        private Renderer outfit;
        private Material original;
        private Material temporary;
        private CasinoAdventureState state;
        private readonly Vector3 island = new Vector3(3000, 0, 3000);

        [SetUp]
        public void Setup()
        {
            root = new GameObject("JinxCasino scene effect tests");
            effects = root.AddComponent<JinxCasinoSceneEffects>();
            floor = Child("Floor", island + Vector3.down * 0.5f).AddComponent<BoxCollider>(); floor.size = new Vector3(30, 1, 30);
            player = Child("LocalPlayer", island + Vector3.up * 0.03f).transform;
            var controller = player.gameObject.AddComponent<CharacterController>(); controller.height = 2; controller.radius = 0.3f; controller.center = Vector3.up;
            buddy = Child("Buddy", island + new Vector3(0, 0.03f, 3)).transform;
            safe0 = Child("Area0Safe", island + new Vector3(-5, 0.03f, 0)).transform;
            safe1 = Child("Area1Safe", island + new Vector3(5, 0.03f, 0)).transform;
            gate = Child("ShortcutGate", island + new Vector3(8, 0, 0));
            light = Child("AreaLight", island + Vector3.up * 4).AddComponent<Light>(); light.intensity = 2.5f;
            area0 = Child("Area0Facilities", island).AddComponent<JinxCasinoAreaFacilities>();
            area0.Configure(0, safe0, new[] { light }, gate, Array.Empty<JinxCasinoMovingTable>(), Array.Empty<Transform>());
            area1 = Child("Area1Facilities", island).AddComponent<JinxCasinoAreaFacilities>();
            area1.Configure(1, safe1, Array.Empty<Light>(), null, Array.Empty<JinxCasinoMovingTable>(), Array.Empty<Transform>());
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Assert.That(shader, Is.Not.Null);
            original = new Material(shader); temporary = new Material(shader);
            resources.Add(original); resources.Add(temporary);
            outfit = Child("Outfit", player.position + Vector3.up).AddComponent<MeshRenderer>(); outfit.transform.SetParent(player, true); outfit.sharedMaterial = original;
            effects.Configure(player, new[] { buddy }, new[] { safe0, safe1 }, Array.Empty<CasinoEffectPrefabBinding>());
            effects.ConfigureFacilities(new[] { area0, area1 }, temporary, null);
            effects.ConfigureActors(new[] { new CasinoActorEffectBinding { Target = player, CostumeRenderers = new[] { outfit } } });
            state = CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1).CaptureState();
            effects.SynchronizeState(state);
            Physics.SyncTransforms();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (effects != null) effects.ClearEffects();
            Object.Destroy(root);
            foreach (var resource in resources) if (resource != null) Object.Destroy(resource);
            resources.Clear();
            yield return null;
        }

        [Test]
        public void DuplicateEffectIsAppliedOnceAndClearingRestoresCostume()
        {
            var disguise = Effect("one", "Disguise", "local", 3000, 5000);
            effects.ApplyEffects(new[] { disguise, disguise });
            Assert.That(effects.ActiveVisualCount, Is.EqualTo(1));
            Assert.That(outfit.sharedMaterial, Is.SameAs(temporary));
            effects.ClearEffects();
            Assert.That(outfit.sharedMaterial, Is.SameAs(original));
            Assert.That(effects.ActiveVisualCount, Is.Zero);
            Assert.That(effects.LocalInkIntensity, Is.Zero);
        }

        [Test]
        public void TeamAliasCannotBypassPhysicalPlayersPrankProtection()
        {
            effects.ApplyEffects(new[] { Effect("bubble", "Bubble", "local", 2000, 5000) });
            effects.ApplyEffects(new[] { Effect("disguise", "Disguise", "team", 3000, 5000) });
            Assert.That(effects.LocalMovementMultiplier, Is.Zero);
            Assert.That(outfit.sharedMaterial, Is.SameAs(original), "team不能再次整蛊刚受到local整蛊的同一实体");
            effects.ClearEffects();
            Assert.That(effects.LocalMovementMultiplier, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TeleportUsesCurrentAreaRatherThanNearestOtherArea()
        {
            player.position = safe1.position + Vector3.forward;
            Physics.SyncTransforms();
            effects.ApplyEffects(new[] { Effect("teleport", "PortalFault", "local", 1000) });
            Assert.That(Vector3.Distance(player.position, safe0.position), Is.LessThan(0.1f));
            Vector3 destination = player.position;
            effects.ApplyEffects(new[] { Effect("teleport", "PortalFault", "local", 1000) });
            Assert.That(player.position, Is.EqualTo(destination));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpringPunchStopsAtSolidWallAndAtUnsupportedFloorEdge()
        {
            player.rotation = Quaternion.Euler(0, 90, 0);
            var wall = Child("Wall", island + new Vector3(0.6f, 1, 0)).AddComponent<BoxCollider>(); wall.size = new Vector3(0.1f, 2, 3);
            Physics.SyncTransforms();
            effects.ApplyEffects(new[] { Effect("wall-punch", "SpringPunch", "local", 1000, 5000) });
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(player.position.x - island.x, Is.LessThan(0.3f));
            effects.ClearEffects();
            wall.gameObject.SetActive(false);
            player.position = island + Vector3.up * 0.03f;
            floor.size = new Vector3(0.9f, 1, 30);
            Physics.SyncTransforms();
            effects.SynchronizeState(state);
            effects.ApplyEffects(new[] { Effect("edge-punch", "SpringPunch", "local", 1000, 5000) });
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(player.position.x - island.x, Is.LessThanOrEqualTo(0.46f));
            Assert.That(player.position.y, Is.GreaterThanOrEqualTo(0));
        }

        [UnityTest]
        public IEnumerator MovingTablesExcludeCommittedGameAndFreezeIfTableBecomesOccupied()
        {
            var occupied = Table("Slots", CasinoGameKind.Slots, island + new Vector3(-3, 0.03f, 3));
            var idle = Table("Roulette", CasinoGameKind.Roulette, island + new Vector3(3, 0.03f, 3));
            area0.Configure(0, safe0, new[] { light }, gate, new[] { occupied, idle }, Array.Empty<Transform>());
            state.ActiveRoundJson = "committed"; state.ActiveGame = CasinoGameKind.Slots;
            effects.SynchronizeState(state);
            Vector3 occupiedOrigin = occupied.transform.position, idleOrigin = idle.transform.position;
            Physics.SyncTransforms();
            effects.ApplyEffects(new[] { Effect("tables", "MovingTables", "team", 1000, 5000) });
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(occupied.transform.position, Is.EqualTo(occupiedOrigin));
            Assert.That(Vector3.Distance(idle.transform.position, idleOrigin), Is.GreaterThan(0.03f));
            state.ActiveGame = CasinoGameKind.Roulette; effects.SynchronizeState(state);
            Vector3 paused = idle.transform.position;
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(idle.transform.position, Is.EqualTo(paused));
        }

        [Test]
        public void RestoreRebuildsRadarShortcutCarryAndLightingWithoutRepeatingOldTeleport()
        {
            var goldTemplate = Child("SavedGoldTemplate", island); goldTemplate.SetActive(false);
            effects.ConfigureFacilities(new[] { area0, area1 }, temporary, goldTemplate);
            state.Revision = 1;
            state.ElapsedMilliseconds = 5000;
            state.ActiveMission = new CasinoAdventureMission { Id = "carry", EventId = "gold_delivery", Carrying = true, DeadlineMilliseconds = 45000, TargetCount = 1 };
            state.Effects.Add(Effect("radar", "MapRadar", "buddy", 15000));
            state.Effects.Add(Effect("shortcut", "OpenShortcut", "buddy", 0));
            state.Effects.Add(Effect("old-teleport", "Teleport", "local", 2000));
            Vector3 origin = player.position;
            effects.SynchronizeState(state, restore: true);
            Assert.That(effects.IsRadarActive, Is.True);
            Assert.That(area0.IsShortcutOpen, Is.True);
            Assert.That(gate.activeSelf, Is.False);
            Assert.That(effects.IsCarryingGold, Is.True);
            Assert.That(effects.ActiveVisualCount, Is.EqualTo(1));
            effects.ApplyEffects(new[] { state.Effects[2] });
            Assert.That(player.position, Is.EqualTo(origin));
            state.ActiveMission = new CasinoAdventureMission { Id = "repair", EventId = "power_repair", DeadlineMilliseconds = 45000, TargetCount = 3 };
            effects.SynchronizeState(state);
            Assert.That(light.intensity, Is.EqualTo(2.5f * 0.15f).Within(0.001f));
            Assert.That(effects.IsCarryingGold, Is.False);
            state.ElapsedMilliseconds = 16000; state.ActiveMission.Completed = true;
            effects.SynchronizeState(state);
            Assert.That(effects.IsRadarActive, Is.False);
            Assert.That(light.intensity, Is.EqualTo(2.5f));
            state.Phase = CasinoAdventurePhase.Shopping;
            effects.SynchronizeState(state);
            Assert.That(gate.activeSelf, Is.True);
        }

        [Test]
        public void DisablingReceiverRestoresSceneAndReleasesTransientInputEffects()
        {
            effects.ApplyEffects(new[] { Effect("ink", "Ink", "local", 2000, 5000) });
            Assert.That(effects.LocalInkIntensity, Is.GreaterThan(0));
            area0.SetPowerFaulted(true); area0.SetShortcutOpen(true);
            effects.enabled = false;
            Assert.That(effects.LocalInkIntensity, Is.Zero);
            Assert.That(effects.Announcement, Is.Empty);
            Assert.That(light.intensity, Is.EqualTo(2.5f));
            Assert.That(gate.activeSelf, Is.True);
        }

        [UnityTest]
        public IEnumerator FullBoardCollisionCannotIntrudeIntoPlayersCapsule()
        {
            var table = Table("WideBoard", CasinoGameKind.Roulette, island + new Vector3(-2.3f, 0.03f, 0));
            var board = table.GetComponentInChildren<BoxCollider>(); board.size = new Vector3(3.1f, 0.16f, 2.1f);
            Physics.SyncTransforms();
            Vector3 playerOrigin = player.position;
            table.BeginMotion(2);
            float deadline = Time.unscaledTime + 1.4f;
            while (Time.unscaledTime < deadline)
            {
                yield return null;
                Physics.SyncTransforms();
                Assert.That(board.bounds.max.x, Is.LessThanOrEqualTo(player.GetComponent<CharacterController>().bounds.min.x - 0.002f), "真实桌板不能进入玩家胶囊");
                Assert.That(player.position, Is.EqualTo(playerOrigin));
            }
        }

        [UnityTest]
        public IEnumerator NonAnimatingHintCannotRestoreBubbleIntermediatePose()
        {
            var model = Child("BuddyVisualModel", buddy.position).transform; model.SetParent(buddy, false);
            model.localPosition = Vector3.zero; model.localRotation = Quaternion.Euler(0, 25, 0);
            Vector3 originalPosition = model.localPosition; Quaternion originalRotation = model.localRotation;
            effects.ConfigureActors(new[] { new CasinoActorEffectBinding { Target = buddy, VisualRoot = model } });
            effects.ApplyEffects(new[] { Effect("bubble", "Bubble", "buddy", 400, 5000) });
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(Vector3.Distance(model.localPosition, originalPosition), Is.GreaterThan(0.05f));
            effects.ApplyEffects(new[] { Effect("hint", "CooperationHint", "buddy", 800) });
            yield return new WaitForSecondsRealtime(0.9f);
            Assert.That(model.localPosition, Is.EqualTo(originalPosition));
            Assert.That(Quaternion.Angle(model.localRotation, originalRotation), Is.LessThan(0.01f));
        }

        [UnityTest]
        public IEnumerator ClearEffectsLetsPreviouslyOccupiedTableReturnSafely()
        {
            var table = Table("ReturningTable", CasinoGameKind.Roulette, island + new Vector3(3, 0.03f, 3));
            area0.Configure(0, safe0, new[] { light }, gate, new[] { table }, Array.Empty<Transform>());
            Vector3 origin = table.transform.position;
            Physics.SyncTransforms(); table.BeginMotion(1);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Vector3.Distance(table.transform.position, origin), Is.GreaterThan(0.05f));
            table.SetOccupied(true);
            effects.ClearEffects();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.That(Vector3.Distance(table.transform.position, origin), Is.LessThan(0.02f));
            Assert.That(table.IsMoving, Is.False);
        }

        [Test]
        public void PrankPreflightUsesNearestCurrentAreaBuddyAndRejectsProtectedAliases()
        {
            var home = Child("CurrentWorldArea", island).AddComponent<JinxCasinoWorldArea>(); home.Configure(0, safe0, null, null);
            var foreign = Child("AlreadyOpenOtherWorldArea", island).AddComponent<JinxCasinoWorldArea>(); foreign.Configure(1, safe1, null, null);
            var nearest = Child("NearestCurrentBuddy", player.position + Vector3.forward).transform; nearest.SetParent(home.transform, true);
            var remote = Child("OtherAreaBuddySpatiallyCloser", player.position + Vector3.forward * 0.1f).transform; remote.SetParent(foreign.transform, true);
            var nearOutfit = nearest.gameObject.AddComponent<MeshRenderer>(); nearOutfit.sharedMaterial = original;
            var remoteOutfit = remote.gameObject.AddComponent<MeshRenderer>(); remoteOutfit.sharedMaterial = original;
            effects.Configure(player, new[] { buddy, nearest, remote }, new[] { safe0, safe1 }, Array.Empty<CasinoEffectPrefabBinding>());
            Assert.That(effects.CanApplyPrank("buddy"), Is.True);
            Assert.That(effects.CanApplyPrank("team"), Is.True);
            Assert.That(effects.CanApplyPrank("invalid-target"), Is.False);
            effects.ApplyEffects(new[] { Effect("first", "Disguise", "buddy", 3000, 5000) });
            Assert.That(effects.ActiveVisualCount, Is.EqualTo(1));
            Assert.That(nearOutfit.sharedMaterial, Is.SameAs(temporary));
            Assert.That(remoteOutfit.sharedMaterial, Is.SameAs(original));
            Assert.That(effects.CanApplyPrank("buddy"), Is.False);
            Assert.That(effects.CanApplyPrank("team"), Is.False, "team不能绕过被保护的同一助手");
            Assert.That(effects.CanApplyPrank("local"), Is.True);
            effects.ApplyEffects(new[] { Effect("local", "Bubble", "local", 2000, 5000) });
            Assert.That(effects.CanApplyPrank("local"), Is.False);
            Assert.That(effects.CanApplyPrank("team"), Is.False);
        }

        [UnityTest]
        public IEnumerator RestorePreservesRemainingAliasProtectionWithoutReplayingPrank()
        {
            state.Revision = 1; state.ElapsedMilliseconds = 10000;
            state.TargetProtection.Add(new CasinoTargetProtection { TargetId = "team", UntilMilliseconds = 10150 });
            state.TargetProtection.Add(new CasinoTargetProtection { TargetId = "local", UntilMilliseconds = 9999 });
            var previous = Effect("old-bubble", "Bubble", "team", 2000, 5000); state.Effects.Add(previous);
            effects.SynchronizeState(state, restore: true);
            effects.ApplyEffects(new[] { previous });
            Assert.That(effects.ActiveVisualCount, Is.Zero);
            Assert.That(effects.LocalMovementMultiplier, Is.EqualTo(1));
            Assert.That(effects.CanApplyPrank("local"), Is.False);
            Assert.That(effects.CanApplyPrank("buddy"), Is.False);
            Assert.That(effects.CanApplyPrank("team"), Is.False);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.That(effects.CanApplyPrank("local"), Is.True);
            Assert.That(effects.CanApplyPrank("buddy"), Is.True);
            Assert.That(effects.CanApplyPrank("team"), Is.True);
            Assert.That(effects.ActiveVisualCount, Is.Zero);
        }

        private GameObject Child(string name, Vector3 position)
        {
            var child = new GameObject(name); child.transform.SetParent(root.transform, false); child.transform.position = position; return child;
        }
        private JinxCasinoMovingTable Table(string name, CasinoGameKind kind, Vector3 position)
        {
            var table = Child(name, position).AddComponent<JinxCasinoMovingTable>();
            var body = Child(name + "Body", position + Vector3.up).AddComponent<BoxCollider>(); body.size = new Vector3(1.8f, 0.2f, 1.4f); body.transform.SetParent(table.transform, true);
            table.Configure(table.transform, body, Vector3.right, kind); return table;
        }
        private static CasinoSceneEffect Effect(string id, string kind, string target, int duration, int protection = 0)
            => new CasinoSceneEffect { Id = id, EffectKind = kind, TargetId = target, DurationMilliseconds = duration, ProtectionMilliseconds = protection, Description = kind };
    }
}
