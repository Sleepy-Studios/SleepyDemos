#if UNITY_EDITOR
using System;
using Core.Runtime;
using Hotfix;
using SleepyStudios.LoopScroll;
using UnityEngine.EventSystems;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Hotfix.HowToFish;
using Hotfix.SceneManagement;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.Demo
{
    /// 真正从启动场景和 Hub 按钮进入，使用独立临时存档与虚拟输入设备。
    public sealed class HowToFishRuntimeTests
    {
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Mouse mouse;
        private string saveDirectory;
        private HowToFishWorld testWorld;

        [UnityTest, Timeout(360000)]
        public IEnumerator SaveSlots_AllThreeMenusContinueRestartAndRecoverIndependently()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "HowToFishRuntimeTests", Guid.NewGuid().ToString("N"));
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            mouse = InputSystem.AddDevice<Mouse>();
            var positions = new Vector3[3];
            var records = new string[3];
            yield return EnterThreeSlotMenu();
            for (int index = 0; index < 3; index++)
            {
                Assert.That(testWorld.InspectSlot(index).Status, Is.EqualTo(HowToFishLoadStatus.Empty));
                bool pad = index == 1;
                yield return SubmitInputSettingsControl("Slot" + index, pad);
                yield return WaitFor(() => testWorld.HasSession && !testWorld.IsPaused, "空槽没有通过菜单开始新航程。");
                yield return WaitFor(() => testWorld.Player.GetComponent<CharacterController>().isGrounded, "新航程角色未落地。");
                // 用真实短距离步行区分槽快照，不赋予金钱、物品或任务。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(.35f + index * .25f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                if (pad) yield return PressInputSettingsPad(GamepadButton.Start); else yield return PressKey(Key.Escape);
                yield return SubmitInputSettingsControl("Save", pad);
                var saved = testWorld.InspectSlot(index);
                Assert.That(saved.Status, Is.EqualTo(HowToFishLoadStatus.Ready));
                positions[index] = saved.Data.safePosition;
                records[index] = File.ReadAllText(Path.Combine(saveDirectory, "Slot" + (index + 1) + ".json"));
                for (int previous = 0; previous < index; previous++)
                    Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "Slot" + (previous + 1) + ".json")), Is.EqualTo(records[previous]), "保存当前槽改写了另一个槽。");
                yield return SubmitInputSettingsControl("ReturnHub", pad);
                yield return EnterThreeSlotMenu();
                records[index] = File.ReadAllText(Path.Combine(saveDirectory, "Slot" + (index + 1) + ".json"));
            }
            Assert.That(Vector3.Distance(positions[0], positions[1]), Is.GreaterThan(.3f));
            Assert.That(Vector3.Distance(positions[1], positions[2]), Is.GreaterThan(.3f));
            for (int index = 0; index < 3; index++)
            {
                bool pad = index != 1;
                Assert.That(ObjectFind<Button>("Slot" + index).GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("继续"));
                yield return SubmitInputSettingsControl("Slot" + index, pad);
                yield return WaitFor(() => testWorld.HasSession, "继续按钮没有载入槽位。");
                Assert.That(testWorld.Session.State.safePosition, Is.EqualTo(positions[index]), "菜单继续到了错误槽位。");
                if (pad) yield return PressInputSettingsPad(GamepadButton.Start); else yield return PressKey(Key.Escape);
                yield return SubmitInputSettingsControl("ReturnHub", pad);
                yield return EnterThreeSlotMenu();
                records[index] = File.ReadAllText(Path.Combine(saveDirectory, "Slot" + (index + 1) + ".json"));
            }

            // 重开只在第二次确认后覆盖目标槽，其它槽原始字节保持不变。
            yield return SubmitInputSettingsControl("New1");
            Assert.That(testWorld.HasSession, Is.False);
            Assert.That(testWorld.Notice, Does.Contain("再次点击"));
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "Slot2.json")), Is.EqualTo(records[1]));
            yield return SubmitInputSettingsControl("New1", true);
            yield return WaitFor(() => testWorld.HasSession, "重开确认没有建立新航程。");
            Assert.That(Vector3.Distance(testWorld.Session.State.safePosition, positions[1]), Is.GreaterThan(.3f));
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "Slot1.json")), Is.EqualTo(records[0]));
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "Slot3.json")), Is.EqualTo(records[2]));
            Assert.That(File.Exists(Path.Combine(saveDirectory, "Slot2.json.bak")), Is.True);
            yield return PressKey(Key.Escape);
            yield return SubmitInputSettingsControl("ReturnHub");
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "重开后未返回Hub。");

            // 仅损坏此次测试临时目录的文件，随后必须通过恢复按钮使用已有备份。
            File.WriteAllText(Path.Combine(saveDirectory, "Slot2.json"), "broken-primary-for-ui-test");
            yield return EnterThreeSlotMenu();
            Assert.That(testWorld.InspectSlot(1).Status, Is.EqualTo(HowToFishLoadStatus.RecoveryAvailable));
            Vector3 recoveryPosition = testWorld.InspectSlot(1).Data.safePosition;
            Assert.That(ObjectFind<Button>("Slot1").GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("恢复备份"));
            Assert.That(ObjectFind<Button>("New1").interactable, Is.False, "待恢复槽不能被重开按钮静默覆盖。");
            yield return SubmitInputSettingsControl("Slot1", true);
            yield return WaitFor(() => testWorld.HasSession, "恢复按钮没有载入备份。");
            Assert.That(testWorld.Session.State.safePosition, Is.EqualTo(recoveryPosition));
            Assert.That(testWorld.InspectSlot(1).Status, Is.EqualTo(HowToFishLoadStatus.Ready));
            yield return PressInputSettingsPad(GamepadButton.Start);
            yield return SubmitInputSettingsControl("ReturnHub", true);
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "恢复后未返回Hub。");
            File.WriteAllText(Path.Combine(saveDirectory, "Slot3.json"), "broken-primary-for-ui-test");
            File.WriteAllText(Path.Combine(saveDirectory, "Slot3.json.bak"), "broken-backup-for-ui-test");
            yield return EnterThreeSlotMenu();
            Assert.That(ObjectFind<Button>("Slot2").GetComponentInChildren<TextMeshProUGUI>().text, Does.Contain("数据损坏"));
            Assert.That(ObjectFind<Button>("Slot2").interactable, Is.False);
            Assert.That(ObjectFind<Button>("New2").interactable, Is.False);
            Assert.That(ObjectFind<Button>("Slot0").interactable && ObjectFind<Button>("Slot1").interactable, Is.True);
            Assert.That(testWorld.HasSession, Is.False);
        }

        private IEnumerator EnterThreeSlotMenu()
        {
            yield return EnterHub();
            yield return EnterHowToFish();
            yield return WaitFor(() => UnityEngine.Object.FindAnyObjectByType<HowToFishHudPresenter>() != null &&
                !GameSceneNavigator.Instance.IsTransitioning, "三槽菜单未出现。");
            testWorld = UnityEngine.Object.FindAnyObjectByType<HowToFishWorld>();
            testWorld.Input.Asset.devices = new InputDevice[] { keyboard, gamepad, mouse };
            typeof(HowToFishWorld).GetField("saves", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(testWorld, new HowToFishSaveStore(saveDirectory));
            testWorld.ApplyPreferences(new HowToFishLocalPreferences());
            testWorld.Notify("三槽菜单测试使用独立临时存档。");
            yield return null; yield return null;
            Assert.That(testWorld.HasSession, Is.False);
        }


        [UnityTest, Timeout(240000)]
        public IEnumerator Environment_CasinoWalkInFacilitiesPierAndFiveIslandViews()
        {
            yield return StartNewGame();
            var world = testWorld;
            var water = world.gameObject.scene.GetRootGameObjects().Single(root => root.name == "Ocean").GetComponent<Renderer>();
            Assert.That(water.sharedMaterial.shader.name, Is.EqualTo("HowToFish/CoastalWater"));
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(water.sharedMaterial.shader), Is.False);
            Assert.That(world.Player.Eye.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
            Assert.That(RenderSettings.skybox, Is.Not.Null);
            // 环境夹具：解锁区域并传送到各验证起点；不是五岛新档通关，也不保存输入偏好。
            world.Session.State.unlockedIsland = 4;
            world.ApplyPreferences(new HowToFishLocalPreferences());
            if (world.Player.Equipment != null) yield return PressKey(Key.H);
            var rocks = world.Islands.Single(island => island.Index == 3);
            var casino = rocks.transform.Find("EnvironmentArchitecture/LuckyBaitCasinoShell");
            var pier = rocks.transform.Find("EnvironmentArchitecture/RocksPierModule");
            Assert.That(casino, Is.Not.Null, "赌场环境尚未装配。");
            Assert.That(pier, Is.Not.Null, "栈桥环境尚未装配。");
            var table = rocks.GetComponentInChildren<HowToFishRoulette>();
            var machine = rocks.GetComponentsInChildren<HowToFishSlotMachine>().Single(value => value.Island == 3);
            Assert.That(Vector3.ProjectOnPlane(table.transform.localPosition - new Vector3(-13, 0, -30), Vector3.up).magnitude, Is.LessThan(.01f));
            Assert.That(Vector3.ProjectOnPlane(machine.transform.parent.localPosition - new Vector3(-8, 0, -30), Vector3.up).magnitude, Is.LessThan(.01f));
            var marker = casino.GetComponentsInChildren<Transform>().Single(node => node.name == "DoorFront");
            var destination = casino.GetComponentsInChildren<Transform>().Single(node => node.name == "InteriorCenter").position;
            var approach = marker.position + casino.forward * 2;
            var ground = EnvironmentTestGround(rocks.gameObject);
            Assert.That(ground.Raycast(new Ray(approach + Vector3.up * 20, Vector3.down), out var entrance, 40), Is.True);
            world.Player.Teleport(entrance.point + Vector3.up * .08f, 0);
            yield return new WaitForSeconds(.25f);
            Assert.That(Vector3.Dot(world.Player.transform.position - marker.position, casino.forward), Is.GreaterThan(1), "夹具必须从门外开始。");
            yield return WalkEnvironmentRoute(world, destination);
            Assert.That(Vector3.Distance(Vector3.ProjectOnPlane(world.Player.transform.position, Vector3.up),
                Vector3.ProjectOnPlane(destination, Vector3.up)), Is.LessThan(.65f), "角色没有真正穿过门廊与门框。");

            // 轮盘通过既有真实焦点与 E 路径响应；空押提示证明门框没有截断交互射线。
            yield return WalkEnvironmentRoute(world, table.transform.position + Vector3.forward * 2.2f);
            yield return AimAt(world.Player, table.transform.TransformPoint(new Vector3(1.15f, .8f, .85f)));
            Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishRoulette>(), Is.SameAs(table));
            world.Notify("环境交互检测");
            yield return PressKey(Key.E);
            Assert.That(world.Notice, Does.Contain("可卖死鱼"), "轮盘未收到真实交互。");
            Assert.That(table.IsSpinning, Is.False, "本用例不替代轮盘开奖回归。");

            // 走到机台正面，手持鱼作为夹具；瞄准和 G 投料触发现有老虎机，不替代门口移动。
            yield return WalkEnvironmentRoute(world, machine.transform.parent.position + Vector3.forward * 2.2f);
            yield return AimAt(world.Player, machine.transform.position);
            Assert.That(world.Player.Focus, Is.Not.Null);
            Assert.That(world.Player.Focus.transform == machine.transform.parent || world.Player.Focus.transform.IsChildOf(machine.transform.parent),
                Is.True, "门框或其他结构挡住机台正面。");
            var fish = world.Spawn("Shrimp", world.Player.transform.position + Vector3.up, true);
            fish.Hit(1000, Vector3.zero);
            PlaceForPickup(world.Player, fish);
            Assert.That(world.Player.PickUp(fish), Is.True);
            Assert.That(world.Player.HeldItem, Is.SameAs(fish));
            yield return AimAt(world.Player, machine.transform.position);
            yield return new WaitForSeconds(.35f);
            yield return PressKey(Key.G);
            yield return WaitFor(() => fish == null || fish.IsConsumed, "真实投料没有触发老虎机。");
            yield return WaitFor(() => !machine.Busy, "老虎机演出未结束。");

            // 第二个独立起点在岸上，剩余距离由真实移动越过接岸边缘；不传送到甲板上。
            var shoreStart = rocks.transform.position + new Vector3(26, 30, -49.5f);
            Assert.That(ground.Raycast(new Ray(shoreStart, Vector3.down), out var shore, 60), Is.True);
            world.Player.Teleport(shore.point + Vector3.up * .08f, 180);
            yield return new WaitForSeconds(.25f);
            yield return WalkEnvironmentRoute(world, pier.position);
            yield return new WaitForSeconds(.5f);
            // 甲板保留1.5厘米板缝；用脚底宽度取支撑，单根射线会从缝隙穿到地形。
            var support = Physics.SphereCastAll(world.Player.transform.position + Vector3.up * .5f, .12f, Vector3.down, 1,
                    ~0, QueryTriggerInteraction.Ignore).Where(hit => hit.collider.GetComponentInParent<HowToFishPlayer>() == null)
                .OrderBy(hit => hit.distance).FirstOrDefault();
            Assert.That(support.collider, Is.Not.Null, "甲板下没有支撑碰撞。");
            Assert.That(support.collider.transform.IsChildOf(pier), Is.True,
                $"支撑不是栈桥：player={world.Player.transform.position}, support={support.collider.name}, hit={support.point}");
            Assert.That(support.collider, Is.TypeOf<MeshCollider>());
            Assert.That(world.Player.GetComponent<CharacterController>().isGrounded, Is.True);
            Assert.That(world.Player.transform.position.y, Is.EqualTo(pier.position.y).Within(.15f));
            float deckY = world.Player.transform.position.y;
            yield return new WaitForSeconds(.75f);
            Assert.That(world.Player.transform.position.y, Is.EqualTo(deckY).Within(.12f), "静止后穿过甲板下坠。");

            // 固定机位复用玩家相机，逐岛拍环境；这些传送仅用于截图，不构成主线推进证据。
            string evidence = Path.GetFullPath("Library/HowToFish/Evidence"); Directory.CreateDirectory(evidence);
            var views = new[]
            {
                (0, "Lighthouse", new Vector2(0, -16), new Vector3(-5, 6, 2)),
                (1, "Forest", new Vector2(0, -47), new Vector3(0, 3, -23)),
                (2, "Desert", new Vector2(0, -52), new Vector3(0, 3.5f, -32)),
                (3, "Rocks", new Vector2(-10.5f, -42), new Vector3(-10.5f, 7, -26)),
                (4, "Volcano", new Vector2(23, -80), new Vector3(0, 11, -35))
            };
            foreach (var view in views)
            {
                var island = world.Islands.Single(value => value.Index == view.Item1);
                var terrain = EnvironmentTestGround(island.gameObject);
                var origin = island.transform.position + new Vector3(view.Item3.x, 90, view.Item3.y);
                Assert.That(terrain.Raycast(new Ray(origin, Vector3.down), out var floor, 120), Is.True, view.Item2 + " 截图起点不在地形上。");
                world.Player.Teleport(floor.point + Vector3.up * .08f, 0);
                yield return new WaitForSeconds(.25f);
                yield return AimAt(world.Player, island.transform.position + view.Item4);
                yield return WaitFor(() => !UnityEditor.ShaderUtil.anythingCompiling, view.Item2 + " 材质仍在编译。");
                yield return null; yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "G5-Environment-" + view.Item2 + ".png"));
                yield return null; yield return null;
            }
            world.SetPaused(true);
        }

        private IEnumerator WalkEnvironmentRoute(HowToFishWorld world, Vector3 destination)
        {
            float deadline = Time.realtimeSinceStartup + 15;
            float distance = Vector3.ProjectOnPlane(destination - world.Player.transform.position, Vector3.up).magnitude;
            while (distance > .55f && Time.realtimeSinceStartup < deadline)
            {
                PointMouseAt(world.Player, new Vector3(destination.x, world.Player.Eye.transform.position.y, destination.z));
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up * Mathf.Clamp(distance / 2, .3f, 1) });
                yield return null;
                distance = Vector3.ProjectOnPlane(destination - world.Player.transform.position, Vector3.up).magnitude;
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(distance, Is.LessThanOrEqualTo(.55f), $"真实移动路线被碰撞挡住：player={world.Player.transform.position}, destination={destination}");
        }

        private static MeshCollider EnvironmentTestGround(GameObject island)
        {
            var terrain = island.GetComponent<MeshCollider>();
            if (terrain != null) return terrain;
            return island.GetComponentsInChildren<MeshCollider>().Single(value => value.name == "Terrain");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator InputSettings_MenuPreviewCancelRebindAndHubPersistence()
        {
            const string key = HowToFishLocalPreferencesStore.DefaultKey;
            bool hadPreferences = PlayerPrefs.HasKey(key);
            string originalPreferences = hadPreferences ? PlayerPrefs.GetString(key) : null;
            try
            {
                // World 启动固定读取正式键；完整保存原字节串，finally 原样恢复，不改变生产注入接口。
                PlayerPrefs.DeleteKey(key); PlayerPrefs.Save();
                yield return StartNewGame();
                var world = testWorld;
                var original = world.GetPreferences();
                var eventSystem = EventSystem.current;
                yield return PressKey(Key.Escape);
                yield return SubmitInputSettingsControl("OpenSettings");
                Assert.That(world.IsEditingSettings, Is.True);
                yield return ChangeInputSettingsControls();
                Assert.That(world.Player.MouseSensitivity, Is.GreaterThan(original.MouseSensitivity));
                Assert.That(world.Player.GamepadSensitivity, Is.GreaterThan(original.GamepadSensitivity));
                Assert.That(world.Player.DeadZone, Is.GreaterThan(original.DeadZone));
                Assert.That(world.Player.InvertY, Is.Not.EqualTo(original.InvertY));

                // 键鼠候选不能被手柄夺走；冲突不能覆盖旧绑定或触发背景菜单。
                string baselineBindings = world.Input.SaveBindings();
                yield return BeginInputSettingsBinding(0, "陆地 · 交互");
                Assert.That(eventSystem.sendNavigationEvents, Is.False);
                Assert.That(ObjectFind<CanvasGroup>("SettingsContent").interactable, Is.False);
                yield return PressInputSettingsPad(GamepadButton.West);
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(world.Input.IsRebinding, Is.True, "键鼠绑定错误地接受了手柄输入。");
                yield return PressKey(Key.Space);
                yield return WaitInputSettingsCaptureEnd(world);
                Assert.That(world.Input.SaveBindings(), Is.EqualTo(baselineBindings), "冲突污染了原绑定。");
                Assert.That(ObjectFind<TextMeshProUGUI>("SettingsStatus").text, Does.Contain("其他操作"));
                Assert.That(world.IsEditingSettings && world.IsPaused, Is.True);

                // 常驻快捷键不能抢占菜单确认键，否则确认菜单时会同时切换暂停。
                yield return BeginInputSettingsBinding(0, "通用 · 暂停");
                yield return PressKey(Key.Enter);
                yield return WaitInputSettingsCaptureEnd(world);
                Assert.That(world.Input.SaveBindings(), Is.EqualTo(baselineBindings));
                Assert.That(ObjectFind<TextMeshProUGUI>("SettingsStatus").text, Does.Contain("其他操作"));

                yield return BeginInputSettingsBinding(0, "陆地 · 交互");
                yield return PressKey(Key.K);
                yield return WaitInputSettingsCaptureEnd(world);
                Assert.That(InputSettingsBinding(world, "Gameplay/Interact", "KeyboardMouse"), Is.EqualTo("<Keyboard>/k"));
                // 长按退出键跨过多个 Update：关闭设置后必须等松键，不能穿透为取消暂停。
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(world.Input.Held("Pause"), Is.True, "暂停状态也必须读取 UI Map 的按住状态。");
                Assert.That(world.IsPaused, Is.True, "退出键尚未松开就取消了暂停。");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                Assert.That(world.IsEditingSettings, Is.False);
                Assert.That(world.IsPaused, Is.True, "关闭设置不应穿透为恢复游戏。");
                Assert.That(world.Player.MouseSensitivity, Is.EqualTo(original.MouseSensitivity));
                Assert.That(world.Player.GamepadSensitivity, Is.EqualTo(original.GamepadSensitivity));
                Assert.That(world.Player.DeadZone, Is.EqualTo(original.DeadZone));
                Assert.That(world.Player.InvertY, Is.EqualTo(original.InvertY));
                Assert.That(world.Input.SaveBindings(), Is.EqualTo(baselineBindings), "取消必须清除原本不存在的绑定覆盖。");
                Assert.That(PlayerPrefs.HasKey(key), Is.False, "预览或取消不应写盘。");

                // 使用真实手柄 Submit 再进设置，证明前次捕获恢复了同一个 Core UI。
                yield return SubmitInputSettingsControl("OpenSettings", true);
                Assert.That(EventSystem.current, Is.SameAs(eventSystem));
                Assert.That(world.IsEditingSettings, Is.True);
                yield return ChangeInputSettingsControls();
                yield return BeginInputSettingsBinding(0, "陆地 · 交互");
                yield return PressKey(Key.K);
                yield return WaitInputSettingsCaptureEnd(world);

                // 驾驶组与陆地组独立；只接受手柄按钮，连续摇杆和键盘均不能结束捕获。
                string beforePad = world.Input.SaveBindings();
                yield return BeginInputSettingsBinding(3, "驾驶 · 交互");
                yield return PressKey(Key.L);
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.right });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(world.Input.IsRebinding, Is.True,
                    "手柄按钮捕获接受了错误设备或连续轴，绑定=" + InputSettingsBinding(world, "Boat/Interact", "Gamepad") +
                    "，提示=" + ObjectFind<TextMeshProUGUI>("SettingsStatus").text);
                yield return PressInputSettingsPad(GamepadButton.East);
                yield return WaitInputSettingsCaptureEnd(world);
                Assert.That(world.Input.SaveBindings(), Is.EqualTo(beforePad));
                Assert.That(world.IsEditingSettings && world.IsPaused, Is.True, "取消捕获不能同时关闭设置。");
                yield return BeginInputSettingsBinding(3, "驾驶 · 交互");
                yield return PressInputSettingsPad(GamepadButton.South);
                yield return WaitInputSettingsCaptureEnd(world);
                Assert.That(InputSettingsBinding(world, "Boat/Interact", "Gamepad"), Is.EqualTo("<Gamepad>/buttonSouth"));
                Assert.That(InputSettingsBinding(world, "Gameplay/Interact", "Gamepad"), Is.EqualTo("<Gamepad>/buttonWest"));

                var expected = world.GetPreferences();
                yield return null; yield return null;
                ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/InputSettings-Menu.png"));
                yield return null; yield return null;
                yield return SubmitInputSettingsControl("SettingsSave", true);
                Assert.That(world.IsEditingSettings, Is.False);
                var stored = new HowToFishLocalPreferencesStore().Load();
                Assert.That(stored.MouseSensitivity, Is.EqualTo(expected.MouseSensitivity).Within(.0001f));
                Assert.That(stored.GamepadSensitivity, Is.EqualTo(expected.GamepadSensitivity));
                Assert.That(stored.DeadZone, Is.EqualTo(expected.DeadZone).Within(.0001f));
                Assert.That(stored.InvertY, Is.EqualTo(expected.InvertY));
                Assert.That(stored.Bindings, Is.EqualTo(expected.Bindings));

                yield return SubmitInputSettingsControl("ReturnHub");
                yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub &&
                    !GameSceneNavigator.Instance.IsTransitioning, "设置验证未返回 Hub。");
                Assert.That(EventSystem.current, Is.SameAs(eventSystem));
                // Hub 的 OnShow 在导航事务结束前创建松键门闩，后续帧才恢复菜单输入。
                yield return WaitFor(() => eventSystem.sendNavigationEvents, "退出后遗留了菜单输入锁。");
                yield return EnterWorld(); world = testWorld;
                Assert.That(world.Player.MouseSensitivity, Is.EqualTo(expected.MouseSensitivity).Within(.0001f));
                Assert.That(world.Player.GamepadSensitivity, Is.EqualTo(expected.GamepadSensitivity));
                Assert.That(world.Player.DeadZone, Is.EqualTo(expected.DeadZone).Within(.0001f));
                Assert.That(world.Player.InvertY, Is.EqualTo(expected.InvertY));
                Assert.That(InputSettingsBinding(world, "Gameplay/Interact", "KeyboardMouse"), Is.EqualTo("<Keyboard>/k"));
                Assert.That(InputSettingsBinding(world, "Boat/Interact", "Gamepad"), Is.EqualTo("<Gamepad>/buttonSouth"));
                world.SetPaused(true);
            }
            finally
            {
                try { if (testWorld != null) testWorld.Input.CancelRebind(); }
                finally
                {
                    if (hadPreferences) PlayerPrefs.SetString(key, originalPreferences); else PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.Save();
                }
            }
        }

        private IEnumerator SubmitInputSettingsControl(string name, bool useGamepad = false)
        {
            yield return WaitFor(() => EventSystem.current != null && EventSystem.current.sendNavigationEvents,
                "Core 菜单导航没有恢复：" + name);
            var control = ObjectFind<Selectable>(name);
            Assert.That(control, Is.Not.Null, "设置控件未显示：" + name);
            Assert.That(control.IsInteractable(), Is.True, "设置控件被锁定：" + name);
            EventSystem.current.SetSelectedGameObject(control.gameObject);
            yield return null;
            if (useGamepad) yield return PressInputSettingsPad(GamepadButton.South);
            else yield return PressKey(Key.Enter);
        }

        private IEnumerator ChangeInputSettingsControls()
        {
            yield return WaitFor(() => EventSystem.current.sendNavigationEvents, "设置参数的方向键导航未恢复。");
            foreach (string name in new[] { "MouseSensitivity", "GamepadSensitivity", "InputDeadZone" })
            {
                var slider = ObjectFind<Slider>(name);
                Assert.That(slider, Is.Not.Null);
                float before = slider.value;
                EventSystem.current.SetSelectedGameObject(slider.gameObject);
                yield return PressKey(Key.RightArrow);
                Assert.That(slider.value, Is.GreaterThan(before), "方向键未改变真实 Slider：" + name);
            }
            yield return SubmitInputSettingsControl("InvertLook");
        }

        private IEnumerator BeginInputSettingsBinding(int group, string labelPrefix)
        {
            yield return SubmitInputSettingsControl("SettingsGroup" + group);
            for (int page = 0; page < 10; page++)
            {
                var row = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                    .FirstOrDefault(button => button.gameObject.activeInHierarchy && button.name.StartsWith("BindingRow", StringComparison.Ordinal) &&
                        button.GetComponentInChildren<TextMeshProUGUI>().text.StartsWith(labelPrefix + " ", StringComparison.Ordinal));
                if (row != null)
                {
                    yield return SubmitInputSettingsControl(row.name);
                    Assert.That(testWorld.Input.IsRebinding, Is.True, "菜单没有开始捕获：" + labelPrefix);
                    yield break;
                }
                var next = ObjectFind<Button>("SettingsNext");
                Assert.That(next != null && next.interactable, Is.True, "按键列表缺少：" + labelPrefix);
                yield return SubmitInputSettingsControl("SettingsNext");
            }
            Assert.Fail("按键列表翻页未找到：" + labelPrefix);
        }

        private IEnumerator PressInputSettingsPad(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null; yield return null;
        }

        private static IEnumerator WaitInputSettingsCaptureEnd(HowToFishWorld world)
        {
            yield return WaitFor(() => !world.Input.IsRebinding && EventSystem.current.sendNavigationEvents &&
                ObjectFind<CanvasGroup>("SettingsContent")?.interactable == true, "捕获结束后没有恢复设置菜单输入。");
            yield return null; yield return null;
        }

        private static string InputSettingsBinding(HowToFishWorld world, string actionPath, string group)
        {
            var action = world.Input.Asset.FindAction(actionPath, true);
            return action.bindings.First(binding => !binding.isComposite &&
                (binding.groups ?? "").Split(';').Contains(group)).effectivePath;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Drip_ColorSurvivesCookingAndRestoreWithoutChangingSharedMaterials()
        {
            yield return StartNewGame();
            var world = testWorld;
            foreach (string id in new[] { "Cod", "Eel" })
            {
                var fish = world.Spawn(id, world.Player.transform.position + Vector3.up * 2, true);
                fish.Body.isKinematic = true;
                var surfaces = fish.VisualRoot.GetComponentsInChildren<MeshRenderer>(true);
                var originals = surfaces.Select(value => value.sharedMaterials.Select(material => material.GetColor("_BaseColor")).ToArray()).ToArray();
                var block = new MaterialPropertyBlock();
                bool changed = false;
                for (int i = 0; i < surfaces.Length; i++)
                    for (int slot = 0; slot < originals[i].Length; slot++)
                    {
                        surfaces[i].GetPropertyBlock(block, slot);
                        changed |= Vector4.Distance(block.GetColor("_BaseColor"), originals[i][slot]) > .1f;
                    }
                Assert.That(changed, Is.True, id + "的Drip外观不能仅有数据标志。");
                fish.Hit(100000, Vector3.zero);
                var snapshot = fish.Snapshot();
                foreach (float cooking in new[] { 0f, .5f, 1f, 0f })
                {
                    snapshot.cooking = cooking; snapshot.isCooked = cooking >= .5f;
                    fish.Restore(snapshot);
                    for (int i = 0; i < surfaces.Length; i++)
                        for (int slot = 0; slot < originals[i].Length; slot++)
                        {
                            Assert.That(surfaces[i].sharedMaterials[slot].GetColor("_BaseColor"), Is.EqualTo(originals[i][slot]), "不能染色共享材质。");
                            surfaces[i].GetPropertyBlock(block, slot);
                            var color = block.GetColor("_BaseColor");
                            if (cooking == 0) Assert.That(color.r, Is.GreaterThanOrEqualTo(.59f), id);
                            if (cooking == 1) Assert.That(Mathf.Max(color.r, color.g, color.b), Is.LessThan(.03f), "焦化必须覆盖珍稀颜色。");
                        }
                }
                if (id == "Cod")
                {
                    PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
                    yield return WaitFor(() => !UnityEditor.ShaderUtil.anythingCompiling, "Drip材质仍在编译。");
                    yield return null; yield return null;
                    ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/Drip-Cod.png"));
                    yield return null; yield return null;
                    world.Player.Drop(false);
                }
                Assert.That(fish.TryConsume(() => { }), Is.True);
                yield return null;
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Outfits_MenuInputsSharedSelectionAndIndependentRemains()
        {
            yield return StartNewGame();
            var world = testWorld;
            Assert.That(world.SelectedOutfitId, Is.EqualTo("Fisherman"));
            Assert.That(world.Session.State.tracksPausedPlaytime, Is.True);
            world.Session.GrantItem("Knife");
            world.Player.SelectEquipmentSlot(world.Session.State.equipmentSlots.IndexOf("Knife"));
            yield return PressKey(Key.Escape);
            Assert.That(world.IsPaused, Is.True);
            float before = world.Session.State.playedSeconds;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(world.Session.State.playedSeconds, Is.GreaterThan(before + .2f), "限时成就包含暂停时间。");
            EventSystem.current.SetSelectedGameObject(ObjectFind<Button>("OpenOutfits").gameObject);
            yield return PressKey(Key.Enter);
            EventSystem.current.SetSelectedGameObject(ObjectFind<Button>("OutfitSailor").gameObject);
            yield return PressKey(Key.Enter);
            EventSystem.current.SetSelectedGameObject(ObjectFind<Button>("WearOutfit").gameObject);
            yield return PressKey(Key.Enter);
            Assert.That(world.SelectedOutfitId, Is.EqualTo("Sailor"));
            var view = world.Player.GetComponentInChildren<HowToFishEquipmentView>();
            var source = world.Catalog.FindOutfit("Sailor").Prefab.GetComponentsInChildren<MeshRenderer>()
                .Single(value => value.name == "ForearmSkinRight" || value.name == "ForearmSleeveRight");
            Assert.That(view.transform.Find("HandVisual").GetComponentsInChildren<MeshRenderer>(true).Single(value => value.name == "Sleeve").sharedMaterial,
                Is.SameAs(source.sharedMaterial));
            // 手柄导航可选中锁定卡并读取条件，但不能穿戴。
            EventSystem.current.SetSelectedGameObject(ObjectFind<Button>("OutfitScientist").gameObject);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadRight)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return new WaitForSecondsRealtime(.1f);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("OutfitBean"));
            Assert.That(ObjectFind<TextMeshProUGUI>("OutfitDetails").text, Does.Contain("新航程"));
            Assert.That(ObjectFind<Button>("WearOutfit").interactable, Is.False);
            Assert.That(world.TrySelectOutfit("Bean"), Is.False);
            yield return WaitFor(() => !UnityEditor.ShaderUtil.anythingCompiling, "服装材质仍在编译。"); yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/Outfits-Menu.png")); yield return null; yield return null;
            EventSystem.current.SetSelectedGameObject(ObjectFind<Button>("CloseOutfits").gameObject);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("OpenOutfits"));
            using (var blocked = new FileStream(Path.Combine(saveDirectory, "PlayerSkins.json.tmp"), FileMode.Create, FileAccess.Write, FileShare.None))
                Assert.That(world.TrySelectOutfit("Badman"), Is.False, "选择写入失败不能先换外观。");
            Assert.That(world.SelectedOutfitId, Is.EqualTo("Sailor"));
            world.SetPaused(false);
            world.Player.Damage(200);
            yield return null;
            var remains = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.DefinitionId == "PlayerRemains");
            string remainsId = remains.InstanceId;
            Assert.That(remains.OutfitId, Is.EqualTo("Sailor"));
            Assert.That(world.TrySelectOutfit("Badman"), Is.True);
            Assert.That(remains.OutfitId, Is.EqualTo("Sailor"), "新服装不能改写旧遗体。");
            Assert.That(remains.VisualRoot.GetComponentsInChildren<MeshRenderer>().Any(value => value.name == "PalmRight"), Is.True);
            world.Save(); world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "换装未返回Hub。");
            yield return EnterWorld(); world = testWorld;
            Assert.That(world.SelectedOutfitId, Is.EqualTo("Badman"));
            remains = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.InstanceId == remainsId);
            Assert.That(remains.OutfitId, Is.EqualTo("Sailor"));
            world.SetPaused(true);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Outfits_ConsumeGreenWinAndEndingPersistOnlyEarnedRewards()
        {
            yield return StartNewGame();
            var world = testWorld;
            var fish = world.Spawn("Shrimp", world.Player.transform.position + Vector3.up, false);
            fish.Hit(1000, Vector3.zero); Assert.That(fish.Heat(1), Is.True);
            PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return WaitFor(() => fish == null || fish.IsConsumed, "烧焦生物未通过长按进食消费。");
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            var store = new HowToFishSaveStore(saveDirectory);
            Assert.That(store.LoadSkinProfile(out _).unlockedOutfits, Does.Contain("KioskLady"));
            world.Session.State.unlockedIsland = 4;
            var roulette = UnityEngine.Object.FindAnyObjectByType<HowToFishRoulette>();
            fish = world.Spawn("Shrimp", world.Player.transform.position + Vector3.up, false);
            fish.Hit(1000, Vector3.zero); PlaceForPickup(world.Player, fish);
            Assert.That(world.Player.PickUp(fish), Is.True); world.Player.Drop(false);
            var bets = new System.Collections.Generic.Dictionary<HowToFishWorldItem, HowToFishRouletteColor> { [fish] = HowToFishRouletteColor.Green };
            var boss = world.Spawn("Tuna", world.Player.transform.position + Vector3.one * 25, false);
            Assert.That(world.TrySettleRoulette(roulette, bets, HowToFishRouletteColor.Green, out _), Is.False);
            Assert.That(world.Session.State.unlockedOutfits, Does.Not.Contain("Andrei"));
            Assert.That(fish.IsConsumed, Is.False); Assert.That(fish.BettingMultiplier, Is.EqualTo(1));
            Assert.That(boss.TryConsume(() => { }), Is.True);
            Assert.That(world.TrySettleRoulette(roulette, bets, HowToFishRouletteColor.Green, out _), Is.True);
            Assert.That(store.LoadSkinProfile(out _).unlockedOutfits, Does.Contain("Andrei"));
            Assert.That(fish.BettingMultiplier, Is.EqualTo(35));
            foreach (var reward in HowToFishSkinCatalog.Rewards(0, HowToFishSkinRarity.Legendary)) world.Session.UnlockSkin(reward.Id);
            fish = world.Spawn("Shrimp", world.Player.transform.position + Vector3.up, true);
            fish.Hit(1000, Vector3.zero); PlaceForPickup(world.Player, fish);
            Assert.That(world.Player.PickUp(fish), Is.True); world.Player.Drop(false);
            var randomState = UnityEngine.Random.state;
            try
            {
                int seed = 0;
                for (; seed < 1000; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    if (UnityEngine.Random.Range(0, 100) >= 90) break;
                }
                Assert.That(seed, Is.LessThan(1000));
                UnityEngine.Random.InitState(seed);
                Assert.That(world.TryPlaySlotMachine(0, fish, out var rewardText), Is.True);
                Assert.That(rewardText, Does.Contain("重复"));
            }
            finally { UnityEngine.Random.state = randomState; }
            Assert.That(store.LoadSkinProfile(out _).unlockedOutfits, Does.Contain("Jacob"), "重复传奇奖励也解锁人物服装。");
            var departure = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                .Single(value => value.Kind == HowToFishStationKind.MilitaryDeparture);
            world.Session.State.hasMilitaryBoatKey = true;
            world.Session.State.playedSeconds = 3590;
            // 隔离最终互动；新航程的时钟可信，仍必须通过实际 Interact 入口完成离岛。
            typeof(HowToFishWorld).GetMethod("Interact", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(world, new object[] { departure.GetComponent<Collider>() });
            Assert.That(world.ShowEnding, Is.True);
            Assert.That(store.LoadSkinProfile(out _).unlockedOutfits, Does.Contain("Scientist").And.Contain("Bean"));
            Assert.That(world.InspectSlot(0).Data.hasFinished, Is.True);
            float finishedAt = world.Session.State.playedSeconds;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(world.Session.State.playedSeconds, Is.EqualTo(finishedAt));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator IndividualWeight_HeavyWhaleCarryAndCraterOffer()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 4;
            var island = world.Islands.Single(value => value.Index == 4);
            var ascent = island.transform.Find("WoodenAscent");
            var crater = island.GetComponentInChildren<HowToFishVolcanoCrater>();
            // 隔离最大个体重量与坡道投掷：尸体和登山进度为夹具，移动/抓持/投掷仍走真实物理。
            int first = ascent.childCount - 8;
            world.Player.Teleport(ascent.GetChild(first).position + Vector3.up * .2f, 0);
            yield return new WaitForSeconds(.4f);
            var whale = world.Spawn("BowheadWhale", world.Player.Eye.transform.position + Vector3.forward * 2, false);
            var data = whale.Snapshot(); data.health = 0; data.weightMultiplier = 1.2f;
            whale.Restore(data);
            Assert.That(whale.Body.mass, Is.EqualTo(whale.Creature.BaseWeight * 1.2f).Within(.01f));
            Assert.That(whale.Body.mass, Is.GreaterThan(12000));
            PlaceForPickup(world.Player, whale);
            Assert.That(world.Player.PickUp(whale), Is.True);
            for (int i = first + 1; i < ascent.childCount; i++)
            {
                var target = ascent.GetChild(i).position;
                float deadline = Time.realtimeSinceStartup + 10;
                while (Vector3.ProjectOnPlane(target - world.Player.transform.position, Vector3.up).magnitude > .65f &&
                    Time.realtimeSinceStartup < deadline)
                {
                    PointMouseAt(world.Player, new Vector3(target.x, world.Player.Eye.transform.position.y, target.z));
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
                    yield return null;
                }
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                Assert.That(Vector3.ProjectOnPlane(target - world.Player.transform.position, Vector3.up).magnitude, Is.LessThan(.9f));
                Assert.That(whale.IsHeld, Is.True, "最大重量鲸尸在木板路上脱手。");
            }
            yield return AimAt(world.Player, crater.transform.position + Vector3.up * 2.5f);
            yield return PressKey(Key.G);
            yield return WaitFor(() => world.ActiveBoss?.Item.DefinitionId == "MutatedBowheadWhale", "最大重量鲸尸投掷未触发变异阶段。");
            Assert.That(whale == null || whale.IsConsumed, Is.True);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator IndividualWeight_ScalesMassValueAndSurvivesReloadWithoutCompounding()
        {
            yield return StartNewGame();
            var world = testWorld;
            var fish = world.Spawn("Mackerel", world.Player.transform.position + Vector3.forward * 2 + Vector3.up, true);
            Assert.That(fish.WeightMultiplier, Is.InRange(.8f, 1.2f));
            Assert.That(fish.Health, Is.EqualTo(fish.Creature.Health), "体型不改变基础生命。");
            Assert.That(fish.Weight, Is.EqualTo(fish.Creature.BaseWeight * fish.WeightMultiplier).Within(.0001f));
            Assert.That(fish.Body.mass, Is.EqualTo(fish.Weight).Within(.0001f));
            var prefabScale = fish.Creature.Prefab.transform.localScale;
            var snapshot = fish.Snapshot(); snapshot.weightMultiplier = .8f;
            fish.Restore(snapshot); fish.Restore(snapshot);
            Assert.That(Vector3.Distance(fish.transform.localScale, prefabScale * Mathf.Pow(.8f, 1f / 3)), Is.LessThan(.0001f), "反复恢复不能累乘体型。");
            fish.Hit(1000, Vector3.zero, 2); Assert.That(fish.Heat(.5f), Is.True);
            int expectedValue = (int)Math.Round(fish.Creature.Value * 2d * 3 * 1.5 * .8f);
            Assert.That(fish.SaleValue, Is.EqualTo(expectedValue));
            PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True); world.Player.Drop(false);
            fish.Body.isKinematic = true;
            string id = fish.InstanceId; float weight = fish.Weight;
            foreach (string fixedId in new[] { "Clam", "Leech", "FootSnail" })
            {
                var fixedItem = world.Spawn(fixedId, world.Player.transform.position + Vector3.up * 2, false);
                Assert.That(fixedItem.WeightMultiplier, Is.EqualTo(1), fixedId);
                Assert.That(fixedItem.Body.mass, Is.EqualTo(fixedItem.Creature.BaseWeight).Within(.0001f));
            }
            world.Save();
            Assert.That(world.InspectSlot(0).Data.worldItems.Single(item => item.instanceId == id).weightMultiplier, Is.EqualTo(.8f));
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "重量存档未返回Hub。");
            yield return EnterWorld(); world = testWorld;
            fish = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(item => item.InstanceId == id);
            Assert.That(fish.Weight, Is.EqualTo(weight).Within(.0001f)); Assert.That(fish.Body.mass, Is.EqualTo(weight).Within(.0001f));
            Assert.That(fish.SaleValue, Is.EqualTo(expectedValue));
            Assert.That(Vector3.Distance(fish.transform.localScale, prefabScale * Mathf.Pow(.8f, 1f / 3)), Is.LessThan(.0001f));
            Assert.That(fish.TrySell(out int amount), Is.True); Assert.That(amount, Is.EqualTo(expectedValue));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Roulette_PhysicalBetsPausePayoutAndReload()
        {
            yield return StartNewGame();
            var world = testWorld;
            // 隔离轮盘与存档边界；五岛新档推进由独立全程验收覆盖。
            world.Session.State.unlockedIsland = 3;
            world.Player.Island = 3;
            var table = UnityEngine.Object.FindAnyObjectByType<HowToFishRoulette>();
            Assert.That(table, Is.Not.Null);
            world.Player.Teleport(table.transform.position + new Vector3(0, .1f, 2.6f), 180);
            yield return new WaitForSeconds(.2f);
            var colors = new[] { "Red", "Black", "Green" };
            var fish = new HowToFishWorldItem[3];
            var ids = new string[3];
            var values = new int[3];
            for (int i = 0; i < fish.Length; i++)
            {
                fish[i] = world.Spawn("Shrimp", world.Player.transform.position + Vector3.up, false);
                fish[i].Hit(1000, Vector3.zero);
                PlaceForPickup(world.Player, fish[i]); Assert.That(world.Player.PickUp(fish[i]), Is.True); world.Player.Drop(false);
                var zone = table.GetComponentsInChildren<Transform>().Single(node => node.name == colors[i] + "Bet");
                fish[i].Body.useGravity = false; fish[i].Body.linearVelocity = Vector3.zero;
                fish[i].Body.position = zone.position + Vector3.up * .2f;
                ids[i] = fish[i].InstanceId; values[i] = world.Session.CatchValue("Shrimp", 0, false, 1, i == 2 ? 35 : 2, fish[i].WeightMultiplier);
            }
            Physics.SyncTransforms(); yield return new WaitForFixedUpdate();
            var target = table.transform.TransformPoint(new Vector3(1.15f, .8f, .85f));
            yield return AimAt(world.Player, target);
            Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishRoulette>(), Is.SameAs(table));
            int money = world.Session.State.money;
            var activeBoss = world.Spawn("Tuna", table.transform.position + new Vector3(20, 5, 0), false);
            Assert.That(table.TrySpin(), Is.False, "首领战斗不能保存，因此不能消费下注物。");
            Assert.That(fish.All(item => !item.IsConsumed && item.BettingMultiplier == 1), Is.True, "保存拒绝必须保留整组原物品。");
            Assert.That(activeBoss.TryConsume(() => { }), Is.True);
            yield return PressKey(Key.E);
            Assert.That(table.IsSpinning, Is.True, "键盘交互没有启动轮盘。");
            int winner = table.LastPocket == 0 ? 2 : table.LastPocket % 2 == 1 ? 0 : 1;
            int multiplier = winner == 2 ? 35 : 2;
            var saved = world.InspectSlot(0).Data;
            Assert.That(saved.worldItems.Single(item => item.instanceId == ids[winner]).bettingMultiplier, Is.EqualTo(multiplier));
            Assert.That(saved.worldItems.Count(item => ids.Contains(item.instanceId)), Is.EqualTo(1), "开奖动画前应只保存中奖实体。");
            for (int i = 0; i < fish.Length; i++)
                if (i != winner) Assert.That(fish[i] == null || fish[i].IsConsumed, Is.True);
            Assert.That(fish[winner].SaleValue, Is.EqualTo(values[winner]));
            Assert.That(world.Session.State.money, Is.EqualTo(money), "轮盘应改变物品价值，不直接发钱。");
            world.SetPaused(true);
            var wheel = table.GetComponentsInChildren<Transform>().Single(node => node.name == "Wheel");
            var rotation = wheel.localRotation;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(table.IsSpinning, Is.True); Assert.That(Quaternion.Angle(wheel.localRotation, rotation), Is.LessThan(.001f));
            world.SetPaused(false); yield return WaitFor(() => !table.IsSpinning, "轮盘没有结束。");
            yield return AimAt(world.Player, table.transform.position + new Vector3(0, 1.05f, -.1f));
            yield return WaitFor(() => !UnityEditor.ShaderUtil.anythingCompiling, "轮盘材质仍在编译。"); yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/Roulette-InGame.png")); yield return null; yield return null;
            // 把唯一中奖物移出台面，手柄交互应拒绝空桌，不能再次结算。
            fish[winner].Body.position = table.transform.position + new Vector3(2.2f, 1, 0);
            fish[winner].Body.isKinematic = true; Physics.SyncTransforms();
            yield return AimAt(world.Player, target);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.West)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(table.IsSpinning, Is.False); Assert.That(fish[winner].BettingMultiplier, Is.EqualTo(multiplier));
            world.Save(); world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "轮盘保存未返回Hub。");
            yield return EnterWorld(); world = testWorld;
            var restored = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(item => item.InstanceId == ids[winner]);
            Assert.That(restored.BettingMultiplier, Is.EqualTo(multiplier)); Assert.That(restored.SaleValue, Is.EqualTo(values[winner]));
            Assert.That(restored.TrySell(out int amount), Is.True); Assert.That(amount, Is.EqualTo(values[winner]));
            Assert.That(world.Session.State.money, Is.EqualTo(money + amount));
            table = UnityEngine.Object.FindAnyObjectByType<HowToFishRoulette>();
            world.Player.Teleport(table.transform.position + new Vector3(0, .1f, 2.6f), 180);
            var second = world.Spawn("Shrimp", world.Player.transform.position + Vector3.up, false); second.Hit(1000, Vector3.zero);
            PlaceForPickup(world.Player, second); Assert.That(world.Player.PickUp(second), Is.True); world.Player.Drop(false);
            second.Body.useGravity = false; second.Body.linearVelocity = Vector3.zero;
            second.Body.position = table.GetComponentsInChildren<Transform>().Single(node => node.name == "RedBet").position + Vector3.up * .2f;
            Physics.SyncTransforms(); yield return new WaitForFixedUpdate();
            yield return AimAt(world.Player, table.transform.TransformPoint(new Vector3(1.15f, .8f, .85f)));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.West)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(table.IsSpinning, Is.True, "手柄交互没有启动轮盘。");
            yield return WaitFor(() => !table.IsSpinning, "手柄轮盘没有结束。");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Skins_PhysicalIntakePauseDuplicateEquipmentBoatAndReload()
        {
            yield return StartNewGame();
            var world = testWorld;
            foreach (string map in new[] { "Gameplay", "Boat" })
                Assert.That(world.Input.Asset.FindActionMap(map, true).FindAction("ChangeSkin", true).bindings.Select(binding => binding.path),
                    Is.EquivalentTo(new[] { "<Keyboard>/c", "<Gamepad>/rightStickPress" }));
            var machine = UnityEngine.Object.FindObjectsByType<HowToFishSlotMachine>(FindObjectsSortMode.None).Single(value => value.Island == 0);
            var fish = world.Spawn("Shrimp", world.Player.transform.position + Vector3.up, true);
            Assert.That(world.TryPlaySlotMachine(0, fish, out _), Is.False, "活物不可投入。");
            fish.Hit(1000, Vector3.zero);
            Assert.That(world.TryPlaySlotMachine(0, fish, out _), Is.False, "没有拿过的尸体不可投入。");
            PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
            Assert.That(world.TryPlaySlotMachine(0, fish, out _), Is.False, "仍在手中的尸体不可投入。");
            world.Player.Drop(false);
            fish.Body.useGravity = false; fish.Body.position = machine.transform.position; fish.Body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return WaitFor(() => machine.Busy, "实体投入没有触发老虎机。");
            Assert.That(fish == null || fish.IsConsumed, Is.True);
            Assert.That(world.Session.State.unlockedSkins.Count, Is.EqualTo(1));
            string won = world.Session.State.unlockedSkins[0];
            Assert.That(HowToFishSkinCatalog.Find(won).ItemId, Is.EqualTo("Knife").Or.EqualTo("BrassKnuckles"));
            Assert.That(world.InspectSlot(0).Data.unlockedSkins, Does.Contain(won), "滚动结束前应保存奖励。");
            world.SetPaused(true);
            var reel = machine.transform.parent.GetComponentsInChildren<Transform>().Single(value => value.name == "Reel0");
            var pausedRotation = reel.localRotation;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(machine.Busy, Is.True); Assert.That(Quaternion.Angle(reel.localRotation, pausedRotation), Is.LessThan(.001f));
            world.SetPaused(false); yield return WaitFor(() => !machine.Busy, "开奖动画没有结束。");
            Assert.That(Quaternion.Angle(reel.localRotation, Quaternion.identity), Is.LessThan(.001f), "停转应让中奖符号朝前显示。");
            var ordinary = world.Spawn("Shrimp", world.Player.transform.position + Vector3.up, false); ordinary.Hit(1000, Vector3.zero);
            PlaceForPickup(world.Player, ordinary); Assert.That(world.Player.PickUp(ordinary), Is.True); world.Player.Drop(false);
            Assert.That(world.TryPlaySlotMachine(0, ordinary, out _), Is.False); Assert.That(ordinary.IsConsumed, Is.False);
            // 全解锁首岛奖池以确定性覆盖重复中奖，不操纵生产随机数。
            foreach (var skin in HowToFishSkinCatalog.All.Where(value => value.Rarity != HowToFishSkinRarity.Default &&
                (value.ItemId == "Knife" || value.ItemId == "BrassKnuckles"))) world.Session.UnlockSkin(skin.Id);
            int unlocked = world.Session.State.unlockedSkins.Count; int money = world.Session.State.money;
            fish = world.Spawn("Shrimp", world.Player.transform.position + Vector3.up, true); fish.Hit(1000, Vector3.zero);
            PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True); world.Player.Drop(false);
            Assert.That(world.TryPlaySlotMachine(0, fish, out var duplicate), Is.True); Assert.That(duplicate, Does.Contain("重复"));
            Assert.That(world.Session.State.unlockedSkins.Count, Is.EqualTo(unlocked)); Assert.That(world.Session.State.money, Is.EqualTo(money));
            Assert.That(world.TryPlaySlotMachine(0, fish, out _), Is.False, "同一实体不得再次开奖。");
            world.Player.Teleport(machine.transform.parent.position + Vector3.forward * 3, 180);
            yield return AimAt(world.Player, machine.transform.parent.position + Vector3.up * 1.2f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/SlotMachine-InGame.png")); yield return null; yield return null;
            world.Session.GrantItem("Pistol"); world.Session.UnlockSkin("Pistol/Gold"); world.Session.UnlockSkin("Boat/Gold");
            yield return PressKey(Key.C);
            var owned = world.Session.State.inventory.Single(value => value.id == "Pistol");
            Assert.That(owned.skinId, Is.EqualTo("Pistol/Gold"));
            var view = world.Player.GetComponentInChildren<HowToFishEquipmentView>();
            var surface = view.GetComponentsInChildren<MeshRenderer>().First(renderer => renderer.sharedMaterial.shader.name == "HowToFish/SelfAuthoredSkin");
            Assert.That(surface.sharedMaterial.GetColor("_ColorA").r, Is.GreaterThan(.5f));
            var hand = view.transform.Find("HandVisual");
            Assert.That(hand.GetComponentsInChildren<Renderer>().All(renderer => renderer.sharedMaterial.shader.name != "HowToFish/SelfAuthoredSkin"), Is.True);
            // 首次变体编译期间Editor会临时绘制青色，须等待真正Shader出图再做视觉验收。
            yield return WaitFor(() => !UnityEditor.ShaderUtil.anythingCompiling, "外观Shader异步编译未完成。");
            yield return null; yield return null;
            Assert.That(UnityEditor.ShaderUtil.ShaderHasError(surface.sharedMaterial.shader), Is.False);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/Skin-GoldPistol.png")); yield return null; yield return null;
            owned.cooking = .6f; yield return null; yield return null;
            var tint = new MaterialPropertyBlock(); surface.GetPropertyBlock(tint, 0);
            Assert.That(tint.GetColor("_BaseColor").r, Is.LessThan(.8f), "换肤后仍需显示烹饪叠色。");
            yield return PressKey(Key.G);
            var dropped = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.DefinitionId == "Pistol");
            Assert.That(dropped.SkinId, Is.EqualTo("Pistol/Gold"));
            Assert.That(dropped.Cooking, Is.EqualTo(.6f));
            PlaceForPickup(world.Player, dropped); Assert.That(world.Player.PickUp(dropped), Is.True); yield return null;
            Assert.That(world.Session.State.inventory.Single(value => value.id == "Pistol").skinId, Is.EqualTo("Pistol/Gold"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightStick)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(world.Session.State.inventory.Single(value => value.id == "Pistol").skinId, Is.Null.Or.Empty);
            yield return PressKey(Key.C); yield return PressKey(Key.G);
            dropped = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.DefinitionId == "Pistol");
            string droppedId = dropped.InstanceId; dropped.Body.isKinematic = true;
            world.Session.State.hasBoatKey = true;
            var boat = UnityEngine.Object.FindAnyObjectByType<HowToFishBoat>(); world.Player.Board(boat.Seat); boat.SetDriver(world.Input);
            yield return PressKey(Key.C);
            Assert.That(boat.SkinId, Is.EqualTo("Boat/Gold"));
            world.Save(); world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "皮肤保存未返回Hub。");
            yield return EnterWorld(); world = testWorld;
            Assert.That(world.Session.State.unlockedSkins, Does.Contain(won));
            Assert.That(world.Player.IsDriving, Is.True);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<HowToFishBoat>().SkinId, Is.EqualTo("Boat/Gold"));
            dropped = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.InstanceId == droppedId);
            Assert.That(dropped.SkinId, Is.EqualTo("Pistol/Gold"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.RightStick)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(UnityEngine.Object.FindAnyObjectByType<HowToFishBoat>().SkinId, Is.Null.Or.Empty);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Dynamite_UnderwaterFuseSpawnsOnlyUnlockedOrdinaryCatch()
        {
            yield return StartNewGame();
            var world = testWorld;
            var forest = world.Islands.Single(value => value.Index == 1);
            bool unsupportedDistanceQuery = false;
            Application.LogCallback captureDistanceWarning = (message, stack, type) =>
            {
                if (message.Contains("Physics.ClosestPoint can only")) unsupportedDistanceQuery = true;
            };
            Application.logMessageReceived += captureDistanceWarning;
            // 暂停环境定时补生，不替换 Spawn、炸药回调或引信 Update。
            world.enabled = false;
            try
            {
                var offshore = Enumerable.Range(0, 16)
                    .Select(index => forest.Position + Quaternion.Euler(0, index * 22.5f, 0) * Vector3.forward * (forest.Radius + 12))
                    .First(position => world.Islands.All(value => value.DistanceToShore(position) > 0) &&
                        world.Islands.OrderBy(value => value.DistanceToShore(position)).First() == forest);
                offshore.y = -.3f;
                var above = offshore; above.y = 1;
                var surface = offshore; surface.y = 0;
                var inland = forest.Position; inland.y = -.3f;
                world.Session.State.unlockedIsland = 0;
                yield return CheckExplosion(above, -1);
                yield return CheckExplosion(surface, -1);
                yield return CheckExplosion(inland, -1);
                // 相同的森林岸外爆点：森林未解锁时只取灯塔普通池，解锁后才切换森林池。
                yield return CheckExplosion(offshore, 0);
                world.Session.State.unlockedIsland = 1;
                yield return CheckExplosion(offshore, 1);
                Assert.That(unsupportedDistanceQuery, Is.False, "不能对岸内非凸地形调用ClosestPoint。");
            }
            finally
            {
                Application.logMessageReceived -= captureDistanceWarning;
                world.enabled = true; world.SetPaused(true);
            }

            IEnumerator CheckExplosion(Vector3 center, int expectedIsland)
            {
                var before = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                    .Select(value => value.InstanceId).ToArray();
                var baitCounts = world.Catalog.Items.Where(value => value.Kind == HowToFishItemKind.Bait)
                    .ToDictionary(value => value.Id, value => world.Session.Count(value.Id));
                var explosive = world.Spawn("Dynamite", center, false);
                // 只固定爆心，阻止浮力改变水上/水下边界；真实 Dynamite 组件继续更新。
                explosive.enabled = false;
                explosive.Body.isKinematic = true;
                Physics.SyncTransforms();
                var fuse = explosive.GetComponent<HowToFishDynamite>();
                fuse.Ignite();
                Assert.That(fuse.RemainingFuse, Is.EqualTo(3));
                yield return WaitFor(() => explosive == null, "真实炸药引信到期未消费实体。");
                var spawned = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                    .Where(value => !before.Contains(value.InstanceId)).ToArray();
                Assert.That(spawned.Length, Is.EqualTo(expectedIsland < 0 ? 0 : 1),
                    "水上/岸内不能产鱼，岸外水下每枚只能产一条。");
                foreach (var entry in baitCounts)
                    Assert.That(world.Session.Count(entry.Key), Is.EqualTo(entry.Value), "炸鱼不能消耗当前鱼饵。");
                if (expectedIsland >= 0)
                {
                    var fish = spawned.Single();
                    Assert.That(fish.Creature, Is.Not.Null);
                    Assert.That(fish.Creature.Island, Is.EqualTo(expectedIsland));
                    Assert.That(fish.Creature.IsBoss, Is.False);
                    Assert.That(fish.Creature.IsGroundPickup, Is.False);
                    Assert.That(fish.Creature.Baits.Intersect(new[] { "FreeLure", "HotDog", "BeginnerLure", "StandardLure", "ProfessionalLure", "ScientificLure" }), Is.Not.Empty);
                    Assert.That(new[] { "Leech", "Seagull", "BingBong" }, Does.Not.Contain(fish.DefinitionId));
                    Assert.That(fish.IsDrip, Is.False);
                    Assert.That(fish.IsAlive, Is.True, "新鱼不能被同一枚炸药的已缓存范围伤害击杀。");
                    Assert.That(fish.Health, Is.EqualTo(fish.Creature.Health));
                }
                yield return new WaitForSeconds(.1f);
                Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                    .Count(value => !before.Contains(value.InstanceId)), Is.EqualTo(spawned.Length), "后续帧不能重复生成。");
                foreach (var item in spawned) Assert.That(item.TryConsume(() => { }), Is.True);
                yield return null;
            }
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Dynamite_BuyThrowPauseResumeChainAndSelfDamage()
        {
            yield return StartNewGame();
            var world = testWorld;
            var initialEffects = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Where(value => value.gameObject.scene == world.gameObject.scene).ToArray();
            var craterSmoke = initialEffects.Single(value => value.name == "CraterSmoke");
            Assert.That(craterSmoke.main.loop, Is.True);
            Assert.That(craterSmoke.main.useUnscaledTime, Is.False);
            yield return WaitFor(() => craterSmoke.isPlaying && craterSmoke.particleCount > 0, "火山烟未通过原生模拟发射。");
            yield return new WaitForSeconds(4.5f);
            var craterParticles = new ParticleSystem.Particle[craterSmoke.main.maxParticles];
            int craterCount = craterSmoke.GetParticles(craterParticles);
            Assert.That(craterSmoke.isPlaying, Is.True);
            Assert.That(craterCount, Is.GreaterThan(0));
            float highestSmoke = craterParticles.Take(craterCount).Max(value => value.position.y);
            float volcanoY = world.Islands.Single(value => value.Index == 4).Position.y;
            Assert.That(highestSmoke, Is.GreaterThan(volcanoY + 36), "循环烟必须越过岸边机位的山口遮挡线；粒子存在不足证明可见。");
            Debug.Log($"[HowToFish] CraterSmoke native count={craterCount}, maxY={highestSmoke:0.00}");
            var volcano = world.Islands.Single(value => value.Index == 4);
            Assert.That(EnvironmentTestGround(volcano.gameObject).Raycast(new Ray(volcano.Position + new Vector3(23, 90, -80),
                Vector3.down), out var volcanoFloor, 120), Is.True);
            world.SetPaused(true);
            yield return CapturePausedEffectsView(world, volcanoFloor.point + Vector3.up * (world.Player.Eye.transform.localPosition.y + .08f),
                volcano.Position + new Vector3(0, 11, -35), "G5-Volcano-Smoke-Visible.png");
            world.SetPaused(false);
            // 隔离炸药购买和爆炸规则；不是从零赚钱的新档流程。
            world.Session.State.unlockedIsland = 1; world.Session.State.money = 100;
            yield return AimAtProduct(world, "Dynamite", 1);
            for (int i = 0; i < 4; i++) yield return PressKey(Key.E);
            Assert.That(world.Session.Count("Dynamite"), Is.EqualTo(4));
            Assert.That(world.Session.State.money, Is.Zero);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Dynamite"));
            yield return PressKey(Key.G);
            var thrown = UnityEngine.Object.FindObjectsByType<HowToFishDynamite>(FindObjectsSortMode.None).Single();
            Assert.That(thrown.IsArmed, Is.False, "普通丢弃不点燃。");
            Assert.That(world.Session.Count("Dynamite"), Is.EqualTo(3));
            var item = thrown.GetComponent<HowToFishWorldItem>();
            PlaceForPickup(world.Player, item);
            Assert.That(world.Player.PickUp(item), Is.True);
            yield return null;
            Assert.That(world.Session.Count("Dynamite"), Is.EqualTo(4));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            world.SetPaused(true);
            thrown = UnityEngine.Object.FindObjectsByType<HowToFishDynamite>(FindObjectsSortMode.None).Single();
            item = thrown.GetComponent<HowToFishWorldItem>();
            Assert.That(world.Session.Count("Dynamite"), Is.EqualTo(3));
            Assert.That(thrown.RemainingFuse, Is.InRange(2.7f, 3));
            float pausedFuse = thrown.RemainingFuse;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(thrown.RemainingFuse, Is.EqualTo(pausedFuse));
            PlaceForPickup(world.Player, item);
            Assert.That(world.Player.PickUp(item), Is.True);
            Assert.That(item.IsHeld, Is.True);
            Assert.That(world.Session.Count("Dynamite"), Is.EqualTo(3), "点燃后不能回库存重置引信。");
            world.Player.Drop(true);
            item.Body.isKinematic = true;
            item.transform.position = world.Player.transform.position + Vector3.right * 15 + Vector3.up;
            Physics.SyncTransforms();
            world.Save();
            string savedId = item.InstanceId;
            var saved = world.InspectSlot(0).Data.worldItems.Single(value => value.instanceId == savedId);
            Assert.That(saved.dynamiteFuseSeconds, Is.EqualTo(pausedFuse));
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "炸药保存后未返回Hub。");
            Assert.That(initialEffects.All(value => value == null), Is.True, "第一次返回Hub应释放全部原场景粒子源。");
            yield return EnterWorld();
            world = testWorld; world.SetPaused(true);
            var audio = UnityEngine.Object.FindObjectsByType<HowToFishAudioDirector>(FindObjectsSortMode.None)
                .Single(value => value.gameObject.scene == world.gameObject.scene);
            audio.SetVolume(audio.Volume, true);

            item = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.InstanceId == savedId);
            thrown = item.GetComponent<HowToFishDynamite>();
            Assert.That(thrown.RemainingFuse, Is.GreaterThan(0).And.LessThanOrEqualTo(pausedFuse));
            var center = new Vector3(3000, 3, 3000);
            item.Body.isKinematic = true; item.transform.position = center;
            var chain = world.Spawn("Dynamite", center + Vector3.right, false);
            chain.Body.isKinematic = true;
            var tuna = world.Spawn("Tuna", center + Vector3.forward, false);
            tuna.GetComponent<HowToFishTuna>().enabled = false; tuna.Body.isKinematic = true;
            tuna.gameObject.AddComponent<BoxCollider>();
            var fish = world.Spawn("Mackerel", center + Vector3.back, false);
            fish.Body.isKinematic = true;
            var outside = world.Spawn("Mackerel", center + Vector3.right * 12, false);
            outside.Body.isKinematic = true;
            var chainFuse = chain.GetComponent<HowToFishDynamite>();
            float firstAt = -1, chainAt = -1, chainFrameDelta = 0;
            int firstBlasts = 0, chainBlasts = 0;
            var healthSequence = new System.Collections.Generic.List<float>();
            thrown.Exploded += _ => { firstBlasts++; firstAt = Time.time; };
            chainFuse.Exploded += _ => { chainBlasts++; chainAt = Time.time; chainFrameDelta = Time.deltaTime; };
            tuna.Damaged += (target, damage) => healthSequence.Add(target.Health);
            Physics.SyncTransforms();
            world.SetPaused(false);
            // 记录同步事件与伤害序列，不要求协程恢复时0.2秒链爆实体仍存活。
            yield return WaitFor(() => thrown == null && chain == null, "读档引信或相邻真实链爆没有完成。");
            Assert.That(firstBlasts, Is.EqualTo(1));
            Assert.That(chainBlasts, Is.EqualTo(1));
            Assert.That(chainAt, Is.GreaterThanOrEqualTo(firstAt).And.LessThanOrEqualTo(firstAt + .2f + chainFrameDelta + .01f));
            Assert.That(healthSequence, Is.EqualTo(new[] { 4550f, 4100f }), "每枚炸药只能对多碰撞体目标结算一次450伤害。");
            Assert.That(tuna.Health, Is.EqualTo(4100));
            Assert.That(fish.IsAlive, Is.False);
            Assert.That(fish.KillMultiplier, Is.EqualTo(1.25f));
            Assert.That(outside.Health, Is.EqualTo(outside.Creature.Health));
            UnityEngine.Object.Destroy(tuna.gameObject);
            UnityEngine.Object.Destroy(fish.gameObject);
            UnityEngine.Object.Destroy(outside.gameObject);
            yield return new WaitForSeconds(2.3f);

            // 用独立真实引信验证静音视觉和暂停，避免截图与短链爆中间态相互干扰。
            var smoke = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Single(value => value.name == "ExplosionSmoke" && value.gameObject.scene == world.gameObject.scene);
            var fire = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Single(value => value.name == "ExplosionFire" && value.gameObject.scene == world.gameObject.scene);
            Assert.That(smoke.particleCount + fire.particleCount, Is.Zero, "前一次爆炸未消散。");
            world.SetPaused(true);
            var visualBomb = world.Spawn("Dynamite", center, false);
            visualBomb.Body.isKinematic = true;
            var visualFuse = visualBomb.GetComponent<HowToFishDynamite>();
            int emittedFire = 0, emittedSmoke = 0;
            visualFuse.Exploded += _ =>
            {
                emittedFire = fire.particleCount; emittedSmoke = smoke.particleCount;
                world.SetPaused(true);
            };
            visualFuse.Ignite(); visualFuse.TriggerChainReaction();
            float shorter = visualFuse.RemainingFuse;
            Assert.That(shorter, Is.GreaterThan(0).And.LessThanOrEqualTo(.2f));
            visualFuse.Ignite(); visualFuse.TriggerChainReaction();
            Assert.That(visualFuse.RemainingFuse, Is.EqualTo(shorter), "重复点燃或链触发不能延长短引信。");
            world.SetPaused(false);
            yield return WaitFor(() => visualBomb == null, "独立视觉炸药引信没有到期。");
            Assert.That(audio.Muted, Is.True);
            Assert.That(emittedFire, Is.GreaterThan(0), "静音不能阻止真实引信爆炸的火团。");
            Assert.That(emittedSmoke, Is.GreaterThan(0), "静音不能阻止真实引信爆炸的烟。");
            Assert.That(smoke.particleCount, Is.GreaterThan(0));
            Assert.That(smoke.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            // 本帧粒子模拟可能已取得旧deltaTime；从暂停后的完整帧开始检查冻结。
            yield return null;
            Assert.That(Time.timeScale, Is.Zero);
            var particles = new ParticleSystem.Particle[smoke.main.maxParticles];
            int particleCount = smoke.GetParticles(particles);
            float lifetime = particles[0].remainingLifetime;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(smoke.GetParticles(particles), Is.EqualTo(particleCount));
            Assert.That(particles[0].remainingLifetime, Is.EqualTo(lifetime).Within(.001f), "暂停不能继续消耗粒子寿命。");
            yield return CapturePausedEffectsView(world, center + new Vector3(0, 1.5f, -7), center, "G5-Explosion-Muted-Closeup.png");
            world.SetPaused(false);
            var danger = world.Spawn("Dynamite", world.Player.transform.position + Vector3.up, false);
            danger.Body.isKinematic = true;
            int deaths = 0;
            void CountDeath() => deaths++;
            world.Player.Died += CountDeath;
            try
            {
                danger.GetComponent<HowToFishDynamite>().TriggerChainReaction();
                yield return new WaitForSeconds(.3f);
                Assert.That(deaths, Is.EqualTo(1), "玩家自己的炸药必须能致死且只结算一次。");
                Assert.That(world.Session.State.health, Is.EqualTo(100));
            }
            finally { world.Player.Died -= CountDeath; }
            while (world.Session.Count("Dynamite") > 1) Assert.That(world.Session.TryConsume("Dynamite"), Is.True);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            Assert.That(world.Session.Count("Dynamite"), Is.Zero);
            Assert.That(world.Player.Equipment, Is.Null, "最后一枚耗尽后应正常空手。");
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishDynamite>(FindObjectsSortMode.None).Single().IsArmed, Is.True);
            var finalEffects = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Where(value => value.gameObject.scene == world.gameObject.scene).ToArray();
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "效果验证后未返回Hub。");
            Assert.That(finalEffects.All(value => value == null), Is.True, "第二次返回Hub不能留下爆炸或火山烟源。");
        }

        private static IEnumerator CapturePausedEffectsView(HowToFishWorld world, Vector3 cameraPosition, Vector3 target, string filename)
        {
            Assert.That(world.IsPaused, Is.True, "截图不能推进连爆/粒子时钟。");
            var eye = world.Player.Eye.transform;
            Vector3 localPosition = eye.localPosition;
            Quaternion localRotation = eye.localRotation;
            var hud = UnityEngine.Object.FindAnyObjectByType<HowToFishHudPresenter>();
            var canvas = hud == null ? null : hud.GetComponentInParent<Canvas>();
            bool visible = canvas != null && canvas.enabled;
            try
            {
                // 只临时改变相机取景并关闭HUD渲染，不移动玩家/爆点、不直接制造粒子。
                eye.position = cameraPosition;
                eye.LookAt(target);
                if (canvas != null) canvas.enabled = false;
                yield return null; yield return null;
                string folder = Path.GetFullPath("Library/HowToFish/Evidence"); Directory.CreateDirectory(folder);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder, filename));
                yield return null; yield return null;
                Assert.That(world.IsPaused, Is.True, "效果取景期间暂停状态改变。");
                Assert.That(Time.timeScale, Is.Zero, "效果取景期间世界时钟恢复。");
            }
            finally
            {
                eye.localPosition = localPosition; eye.localRotation = localRotation;
                if (canvas != null) canvas.enabled = visible;
            }
        }

        [UnityTest]
        public IEnumerator Albatross_FiveShotVolleyAndTerrainBlockedPursuit()
        {
            yield return StartNewGame();
            var world = testWorld;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(3000, 0, 3000);
            floor.transform.localScale = new Vector3(40, 2, 40);
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.position = new Vector3(3000, 6, 3000);
            roof.transform.localScale = new Vector3(20, 1, 20);
            HowToFishWorldItem bird = null;
            try
            {
                world.Player.Teleport(new Vector3(3000, 1.2f, 3000), 0);
                bird = world.Spawn("Albatross", new Vector3(3000, 18, 3000), false);
                var behavior = bird.GetComponent<HowToFishAlbatross>();
                var check = typeof(HowToFishAlbatross).GetField("diveCheckIn", BindingFlags.Instance | BindingFlags.NonPublic);
                var diving = typeof(HowToFishAlbatross).GetField("diving", BindingFlags.Instance | BindingFlags.NonPublic);
                var dropTimer = typeof(HowToFishAlbatross).GetField("dropIn", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That((float)check.GetValue(behavior), Is.InRange(8, 12));
                dropTimer.SetValue(behavior, 100f);
                yield return WaitFor(() => bird.Body.position.y >= 23, "信天翁没有实际爬升到巡航高度。");
                bird.Body.constraints = RigidbodyConstraints.FreezeAll;
                dropTimer.SetValue(behavior, 0f);
                check.SetValue(behavior, 0f);
                yield return new WaitForSeconds(.7f);
                Assert.That((bool)diving.GetValue(behavior), Is.False, "屋顶遮挡时不应锁定俯冲。");
                Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishProjectile>(FindObjectsSortMode.None).Length, Is.EqualTo(5), "每波应为五颗投射物。");
                yield return new WaitForSeconds(1.6f);
                Assert.That(world.Session.State.health, Is.EqualTo(100), "实体屋顶没有挡住落物。");
                dropTimer.SetValue(behavior, 100f);
                foreach (var drop in UnityEngine.Object.FindObjectsByType<HowToFishProjectile>(FindObjectsSortMode.None))
                    UnityEngine.Object.Destroy(drop.gameObject);
                UnityEngine.Object.Destroy(roof); yield return null;
                bird.Body.constraints = RigidbodyConstraints.FreezeRotation;
                check.SetValue(behavior, 0f);
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                var target = world.Player.transform.position + Vector3.up * .85f;
                string obstacles = string.Join(",", Physics.RaycastAll(bird.Body.position, (target - bird.Body.position).normalized,
                    Vector3.Distance(target, bird.Body.position), ~0, QueryTriggerInteraction.Ignore).Select(hit => hit.collider.name));
                var home = typeof(HowToFishAlbatross).GetField("center", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(behavior);
                Assert.That((bool)diving.GetValue(behavior), Is.True,
                    $"移除遮挡后未开始追逐可见玩家。bird={bird.Body.position} home={home} target={target} check={check.GetValue(behavior)} hits={obstacles}");
                yield return WaitFor(() => world.Session.State.health < 100, "信天翁没有通过实际俯冲接近并命中玩家。");
                Assert.That(world.Session.State.health, Is.EqualTo(55).Within(.01f), "近距离俯冲应造成45伤害。");
                bird.Hit(100000, Vector3.zero);
                Assert.That(bird.IsAlive, Is.False, "信天翁没有半血保护阶段。");
            }
            finally
            {
                if (bird != null) UnityEngine.Object.Destroy(bird.gameObject);
                if (roof != null) UnityEngine.Object.Destroy(roof);
                UnityEngine.Object.Destroy(floor);
            }
        }

        [UnityTest]
        public IEnumerator MutatedWhale_ChainsThreePhysicalJumpsAndKeepsTransitionBarrage()
        {
            yield return StartNewGame();
            var world = testWorld;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(3000, 0, 3000);
            floor.transform.localScale = new Vector3(300, 2, 300);
            var perch = GameObject.CreatePrimitive(PrimitiveType.Cube);
            perch.transform.position = new Vector3(3100, 10, 3000);
            perch.transform.localScale = new Vector3(5, 20, 5);
            HowToFishWorldItem whale = null;
            try
            {
                world.Player.Teleport(new Vector3(3100, 20.2f, 3000), 0);
                whale = world.Spawn("MutatedBowheadWhale", new Vector3(3000, 5, 3000), false);
                var behavior = whale.GetComponent<HowToFishWhale>();
                var jumps = typeof(HowToFishWhale).GetField("jumpsRemaining", BindingFlags.Instance | BindingFlags.NonPublic);
                var phase = typeof(HowToFishWhale).GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic);
                var shots = typeof(HowToFishWhale).GetField("lavaShotsRemaining", BindingFlags.Instance | BindingFlags.NonPublic);
                whale.Hit(whale.Creature.Health, Vector3.zero);
                bool first = false, second = false, third = false, rested = false, extendedBarrage = false;
                float deadline = Time.time + 12;
                while (Time.time < deadline && !rested)
                {
                    yield return new WaitForFixedUpdate();
                    string current = phase.GetValue(behavior).ToString();
                    int remainingJumps = (int)jumps.GetValue(behavior);
                    if (current == "Jump")
                    {
                        first |= remainingJumps == 2;
                        second |= remainingJumps == 1;
                        third |= remainingJumps == 0;
                    }
                    else if (first)
                    {
                        Assert.That(third, Is.True, "第三次起跳前不应插入停顿或冲撞阶段。");
                        rested = current == "Rest";
                    }
                    extendedBarrage |= !whale.GetComponent<HowToFishBossTransition>().IsProtected && (int)shots.GetValue(behavior) > 4;
                }
                Assert.That(first && second && third && rested, Is.True, "没有完成三连跳后再休息。");
                Assert.That(extendedBarrage, Is.True, "转阶段熔岩齐射应持续超过保护动画。");
            }
            finally
            {
                if (whale != null) UnityEngine.Object.Destroy(whale.gameObject);
                UnityEngine.Object.Destroy(floor); UnityEngine.Object.Destroy(perch);
            }
        }

        [UnityTest]
        public IEnumerator BossTransitions_ProtectHalfHealthAndKeepSummonWavesSeparate()
        {
            yield return StartNewGame();
            var world = testWorld;
            foreach (string id in new[] { "GiantPiranha", "Pufferfish", "MutatedBowheadWhale" })
            {
                var boss = world.Spawn(id, new Vector3(3000, 20, 3000), false);
                boss.Body.constraints = RigidbodyConstraints.FreezeAll;
                var phase = boss.GetComponent<HowToFishBossTransition>();
                Assert.That(phase, Is.Not.Null, id);
                int deaths = 0;
                boss.Defeated += _ => deaths++;
                if (id == "GiantPiranha")
                {
                    yield return new WaitForSeconds(1.4f);
                    Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                        .Count(fish => fish.DefinitionId == "Piranha" && fish.IsAlive), Is.EqualTo(6));
                }
                else boss.Body.isKinematic = true;
                Assert.That(boss.DamageToApply(1000000), Is.EqualTo(boss.Creature.Health * .5f));
                boss.Hit(1000000, Vector3.zero);
                Assert.That(boss.Health, Is.EqualTo(boss.Creature.Health * .5f));
                Assert.That(phase.IsProtected, Is.True);
                Assert.That(boss.DamageToApply(1000000), Is.Zero, "受保护命中不能预估为击杀。");
                boss.Hit(1000000, Vector3.zero, 12.5f, true);
                Assert.That(boss.HasBeenHitByPlayer, Is.False, "受保护命中不应消耗首次有效玩家命中。");
                Assert.That(deaths, Is.Zero);
                world.SetPaused(true);
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(phase.IsProtected, Is.True);
                world.SetPaused(false);
                yield return new WaitForSeconds(3.1f);
                Assert.That(phase.IsProtected, Is.False);
                if (id == "GiantPiranha")
                {
                    Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                        .Count(fish => fish.DefinitionId == "Piranha" && fish.IsAlive), Is.EqualTo(12), "转阶段额外群应独立于第一阶段六条。");
                    var behavior = boss.GetComponent<HowToFishGiantPiranha>();
                    typeof(HowToFishGiantPiranha).GetField("summonAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(behavior, Time.time);
                    yield return new WaitForSeconds(1.4f);
                    Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                        .Count(fish => fish.DefinitionId == "Piranha" && fish.IsAlive), Is.EqualTo(18), "第二阶段十二条常规鱼不包含六条额外鱼。");
                }
                boss.Hit(1000000, Vector3.zero, 1.5f, true);
                boss.Hit(1000000, Vector3.zero);
                Assert.That(deaths, Is.EqualTo(1));
                Assert.That(boss.KillMultiplier, Is.EqualTo(1.5f));
                UnityEngine.Object.Destroy(boss.gameObject);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator OrdinaryFish_JumpContactHoldAndDeath()
        {
            yield return StartNewGame();
            var world = testWorld;
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.transform.position = new Vector3(3000, 9, 3000);
            platform.transform.localScale = new Vector3(30, 2, 30);
            try
            {
                world.Player.Teleport(new Vector3(3000, 10.2f, 3000), 0);
                yield return new WaitForSeconds(.5f);
                var fish = world.Spawn("Mackerel", new Vector3(3005, 11, 3000), false);
                Assert.That(fish.GetComponent<HowToFishFishMotion>(), Is.Not.Null);
                bool jumped = false;
                for (int i = 0; i < 120; i++)
                {
                    yield return new WaitForFixedUpdate();
                    jumped |= fish.Body.linearVelocity.y > 1;
                }
                Assert.That(jumped, Is.True, "普通鱼落地后没有自主跃动。");
                fish.Hit(fish.Health, Vector3.zero);
                fish.Body.useGravity = false;
                fish.Body.linearVelocity = Vector3.zero;
                var corpse = fish.Body.position;
                yield return new WaitForSeconds(.7f);
                Assert.That(Vector3.Distance(corpse, fish.Body.position), Is.LessThan(.05f), "死亡后仍在驱动鱼体。");
                var attacker = world.Spawn("Piranha", world.Player.transform.position + Vector3.up * .8f, false);
                attacker.Body.constraints = RigidbodyConstraints.FreezeAll;
                float health = world.Session.State.health;
                yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
                Assert.That(world.Session.State.health, Is.EqualTo(health - 15).Within(.01f), "食人鱼不能在空中发起跃击，但鱼体接触仍应造成15伤害。");
                Assert.That(attacker.TryHold(world.Player.transform, null), Is.True);
                health = world.Session.State.health;
                yield return new WaitForSeconds(1.1f);
                Assert.That(world.Session.State.health, Is.EqualTo(health), "持握状态仍在施加接触伤害。");
                UnityEngine.Object.Destroy(attacker.gameObject);
                UnityEngine.Object.Destroy(fish.gameObject);
                var airborne = world.Spawn("RedSnapper", new Vector3(3005, 20, 3000), false);
                airborne.Body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
                airborne.Body.linearVelocity = Vector3.down * 4;
                yield return new WaitForSeconds(.7f);
                Assert.That(airborne.Body.position.y, Is.LessThan(18), "允许空中攻击不应反复重置向上速度，导致鱼永远悬空。");
                UnityEngine.Object.Destroy(airborne.gameObject);
            }
            finally { UnityEngine.Object.Destroy(platform); }
        }

        [UnityTest]
        public IEnumerator Input_SingleDeadzoneAndHeldMapTransition()
        {
            gamepad = InputSystem.AddDevice<Gamepad>();
            var template = HowToFishInput.CreateDefaultAsset();
            template.devices = new InputDevice[] { gamepad };
            using (var input = new HowToFishInput(template))
            {
                try
                {
                    yield return null;
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.6f, 0) });
                    yield return null;
                    Assert.That(input.ReadMove(.2f).x, Is.EqualTo(.5f).Within(.02f), "原始摇杆只能应用一次死区。");
                    input.SetMode(true, false);
                    Assert.That(input.ReadMove(.2f), Is.EqualTo(Vector2.zero), "切换到驾驶不能沿用按住的移动轴。");
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    yield return null;
                    input.ReadMove(.2f);
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.6f, 0) });
                    yield return null;
                    Assert.That(input.ReadMove(.2f).x, Is.EqualTo(.5f).Within(.02f));
                    input.SetMode(true, true);
                    Assert.That(input.ReadMove(.2f), Is.EqualTo(Vector2.zero));
                    Assert.That(template.enabled, Is.False, "会话不能启用原模板。");
                }
                finally { UnityEngine.Object.Destroy(template); }
            }
        }

        [UnityTest]
        public IEnumerator HubEntry_KeyboardGamepadFishingTradeSaveAndReturn()
        {
            yield return StartNewGame();
            var world = testWorld;
            Assert.That(world.InspectSlot(0).Status, Is.EqualTo(HowToFishLoadStatus.Ready));
            yield return new WaitForSeconds(.3f);
            var position = world.Player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.35f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(Vector3.Dot(world.Player.transform.position - position, world.Player.transform.forward), Is.GreaterThan(.4f), "键盘前进未生效。");
            position = world.Player.transform.position;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.8f, 0) });
            yield return new WaitForSeconds(.35f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            Assert.That(Vector3.Dot(world.Player.transform.position - position, world.Player.transform.right), Is.GreaterThan(.3f), "手柄横移未生效。");
            Assert.That(world.Session.Count("CrabRod"), Is.Zero);
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Any(item => item.DefinitionId == "CrabRod"), Is.False);
            world.Player.Teleport(new Vector3(-4, 2.4f, -6), 0);
            yield return AimAt(world.Player, new Vector3(-4, 3.4f, -3));
            for (int delivery = 0; delivery < 3; delivery++)
            {
                var clam = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                    .First(item => item.DefinitionId == "Clam" && !item.IsConsumed);
                PlaceForPickup(world.Player, clam);
                Assert.That(world.Player.PickUp(clam), Is.True);
                yield return new WaitForSeconds(.25f);
                // 第一枚实际投掷到人物，随后两枚使用交互快捷交付。
                yield return PressKey(delivery == 0 ? Key.G : Key.E);
                int expectedMoney = delivery + 1;
                yield return WaitFor(() => world.Session.State.money == expectedMoney, "蛤蜊交付没有换到钱。");
            }
            yield return AimAtProduct(world, "CrabRod");
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.money, Is.Zero, "鱼竿应花费三枚蛤蜊换来的钱。");
            typeof(HowToFishWorld).GetField("clamRegrowAt", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(world, Time.time);
            yield return WaitFor(() => UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                .Count(item => item.DefinitionId == "Clam" && !item.IsConsumed) == 12, "被取走的蛤蜊没有重新生长。");
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("CrabRod"));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<HowToFishEquipmentView>(), Is.Not.Null);
            world.Player.Teleport(new Vector3(0, 2, -24), 180);
            yield return new WaitForSeconds(.3f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.3f);
            Assert.That(world.Player.Fishing.State.Phase, Is.EqualTo(HowToFishFishingPhase.Charging));
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "浮漂没有进入咬钩阶段。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling, "手柄提竿未生效。");
            bool reeling = true;
            float reelDeadline = Time.realtimeSinceStartup + 25;
            while (world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling && Time.realtimeSinceStartup < reelDeadline)
            {
                bool next = reeling;
                if (world.Player.Fishing.State.Tension > .65f) next = false;
                if (world.Player.Fishing.State.Tension < .25f) next = true;
                if (next != reeling) InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = next ? 1 : 0 });
                reeling = next;
                yield return null;
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            Assert.That(world.Player.Fishing.State.Phase, Is.EqualTo(HowToFishFishingPhase.Landed), "实际鱼线操作未将鱼获收上岸。");
            var fish = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                .Single(item => item.Creature != null && !item.Creature.IsGroundPickup);
            Assert.That(world.Player.PickUp(fish), Is.True);
            yield return new WaitForSeconds(.6f);
            for (int hit = 0; hit < 20 && fish.IsAlive; hit++)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return new WaitForSeconds(.1f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return new WaitForSeconds(.45f);
            }
            Assert.That(fish.IsAlive, Is.False, "攻击输入未命中手持鱼获。");
            world.Player.Drop(false);
            world.Player.Teleport(new Vector3(-4, 2.4f, -6), 0);
            PlaceForPickup(world.Player, fish);
            Assert.That(world.Player.PickUp(fish), Is.True);
            yield return AimAt(world.Player, new Vector3(-4, 3.4f, -3));
            Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>()?.Kind,
                Is.EqualTo(HowToFishStationKind.Keeper), "手持物不应挡住人物交互射线。");
            Assert.That(world.Player.HeldItem, Is.SameAs(fish), "柜台操作前持有物已释放，无法验证出售。");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(world.Session.State.money, Is.GreaterThanOrEqualTo(3), world.Notice);
            yield return AimAtProduct(world, "HotDog");
            Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>()?.ItemId, Is.EqualTo("HotDog"),
                $"商品瞄准失败：focus={world.Player.Focus?.name}, player={world.Player.transform.position}, eye={world.Player.Eye.transform.position}, forward={world.Player.Eye.transform.forward}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return new WaitForSeconds(.1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(world.Session.Count("HotDog"), Is.EqualTo(1), "实体商店交互未购买鱼饵。");
            world.Save();
            Assert.That(world.InspectSlot(0).Data.inventory.Single(item => item.id == "CrabRod").count, Is.EqualTo(1));
            var evidence = Path.GetFullPath("Library/HowToFish/Evidence");
            Directory.CreateDirectory(evidence);
            // 首次导入后的异步 Shader 编译会暂时使用青色占位材质，不能将它记作视觉证据。
            for (int frame = 0; frame < 5; frame++) yield return null;
            yield return WaitFor(() => !UnityEditor.ShaderUtil.anythingCompiling, "着色器仍在编译，无法采集有效画面。");
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "FirstIslandGameplay.png"));
            for (int frame = 0; frame < 3; frame++) yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(world.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning,
                "无法返回 Hub。");
            yield return null;
            Assert.That(UnityEngine.Object.FindAnyObjectByType<HowToFishWorld>(), Is.Null);
            Assert.That(Time.timeScale, Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.enabled), Is.EqualTo(1));
            yield return EnterHowToFish();
            yield return WaitFor(() => UnityEngine.Object.FindAnyObjectByType<HowToFishHudPresenter>() != null &&
                !GameSceneNavigator.Instance.IsTransitioning, "再次进入未显示 HUD。");
            world = UnityEngine.Object.FindAnyObjectByType<HowToFishWorld>();
            typeof(HowToFishWorld).GetField("saves", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world, new HowToFishSaveStore(saveDirectory));
            world.Input.Asset.devices = new InputDevice[] { keyboard, gamepad, mouse };
            yield return null;
            ObjectFind<Button>("Slot0").onClick.Invoke();
            yield return WaitFor(() => world.HasSession && !world.IsPaused, "保存的航程无法继续。");
            Assert.That(world.Session.Count("CrabRod"), Is.EqualTo(1));
            Assert.That(world.Session.Count("HotDog"), Is.EqualTo(1));
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("CrabRod"));
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                .Count(item => item.DefinitionId == "CrabRod"), Is.Zero, "购买与读档均不应额外赠送地面鱼竿。");
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning,
                "读档后无法返回 Hub。");
        }

        [UnityTest]
        public IEnumerator Boat_FloatsDrivesPausesAndExitsOntoDeck()
        {
            yield return StartNewGame();
            var world = testWorld;
            var boat = UnityEngine.Object.FindAnyObjectByType<HowToFishBoat>();
            var body = boat.GetComponent<Rigidbody>();
            // 将真实船体放到深水区，排除海床托住船体导致的假浮力通过。
            body.position = new Vector3(0, .3f, -60);
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            yield return new WaitForSeconds(3);
            Assert.That(body.position.y, Is.InRange(-.2f, .5f));
            Assert.That(Vector3.Dot(boat.transform.up, Vector3.up), Is.GreaterThan(.95f));
            world.Session.State.hasBoatKey = true;
            world.Player.Board(boat.Seat);
            boat.SetDriver(world.Input);
            var start = body.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.4f);
            Assert.That(boat.StartupRemaining, Is.GreaterThan(1));
            Assert.That(Vector3.Distance(start, body.position), Is.LessThan(.2f), "马达启动前不应驱动船体。");
            world.SetPaused(true);
            float startup = boat.StartupRemaining;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(boat.StartupRemaining, Is.EqualTo(startup));
            world.SetPaused(false);
            // 公共输入门闩要求暂停前的方向键先松开，再接受新输入。
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return new WaitForFixedUpdate();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(1.2f);
            yield return new WaitForSeconds(1);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(body.position.z - start.z, Is.GreaterThan(1), "键盘无法驱动船体。");
            var forward = boat.transform.forward;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.8f, .8f) });
            yield return new WaitForSeconds(1);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            Assert.That(Vector3.Angle(forward, boat.transform.forward), Is.GreaterThan(15), "手柄无法转向。");
            world.SetPaused(true);
            start = body.position;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Vector3.Distance(start, body.position), Is.LessThan(.001f));
            world.SetPaused(false);
            // 等待船体减速，离开驾驶位应落在甲板，而不是海水中。
            yield return new WaitForSeconds(2);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(world.Player.IsDriving, Is.False);
            yield return new WaitForSeconds(1);
            Assert.That(Vector3.Distance(world.Player.transform.position, boat.transform.position), Is.LessThan(3), "离开驾驶位后应留在船上，不能落海或死亡回岛。");
            Assert.That(world.Player.transform.position.y, Is.GreaterThan(-.2f), "离舵位置在船舷外，玩家落入海水。");
            Assert.That(world.Player.GetComponent<CharacterController>().isGrounded, Is.True);
            world.Save();
            var standingSave = world.InspectSlot(0).Data;
            Assert.That(standingSave.isOnBoat, Is.True);
            Assert.That(standingSave.isDriving, Is.False);
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "甲板存档后无法返回 Hub。");
            yield return EnterWorld();
            world = testWorld;
            boat = UnityEngine.Object.FindAnyObjectByType<HowToFishBoat>();
            yield return new WaitForSeconds(.5f);
            Assert.That(world.Player.IsDriving, Is.False);
            Assert.That(Vector3.Distance(world.Player.transform.position, boat.transform.position), Is.LessThan(3), "甲板读档不能把玩家留在旧岸上。");
            world.Player.Damage(100);
            yield return null;
            Assert.That(world.Session.State.health, Is.EqualTo(100));
            Assert.That(Vector3.Distance(world.Player.transform.position, boat.transform.position), Is.LessThan(3), "甲板检查点死亡后应回到同一艘船。");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator BoatUpgrades_PurchaseDriveRadarAndResume()
        {
            yield return StartNewGame();
            var world = testWorld;
            var boat = UnityEngine.Object.FindAnyObjectByType<HowToFishBoat>();
            var body = boat.GetComponent<Rigidbody>();
            world.Session.State.hasBoatKey = true; world.Session.State.unlockedIsland = 3; world.Session.State.money = 2000;
            float[] speeds = new float[3];
            for (int tier = 0; tier <= 2; tier++)
            {
                if (tier > 0)
                {
                    boat.SetDriver(null); world.Player.Teleport(new Vector3(0, 3, 0), 0);
                    var station = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                        .Single(value => value.Kind == HowToFishStationKind.MotorUpgrade && value.MotorTier == tier && value.Island == (tier == 1 ? 1 : 3));
                    var shape = station.GetComponentsInChildren<Collider>().OrderByDescending(value => value.bounds.size.sqrMagnitude).First();
                    world.Player.Teleport(station.transform.position + Vector3.back * 2.2f + Vector3.up * .1f, 0);
                    yield return new WaitForSeconds(.35f); yield return AimAt(world.Player, shape.bounds.center);
                    Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(station));
                    int money = world.Session.State.money;
                    yield return PressKey(Key.E);
                    Assert.That(world.Session.State.money, Is.EqualTo(money - (tier == 1 ? 230 : 860)));
                }
                Assert.That(boat.MotorTier, Is.EqualTo(tier));
                Assert.That(boat.transform.Find(tier == 0 ? "SmallMotor" : tier == 1 ? "MediumMotor" : "BigMotor").gameObject.activeSelf, Is.True);
                body.position = new Vector3(0, .3f, -100); body.rotation = Quaternion.identity;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                world.Player.Board(boat.Seat); boat.SetDriver(world.Input);
                Assert.That(boat.StartupRemaining, Is.EqualTo(tier == 0 ? 1.6f : .7f));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(5);
                speeds[tier] = boat.Speed;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            }
            Assert.That(speeds[1], Is.GreaterThan(speeds[0] * 1.15f));
            Assert.That(speeds[2], Is.GreaterThan(speeds[1] * 1.1f));
            Debug.Log($"Boat measured speeds: {speeds[0]:F2}, {speeds[1]:F2}, {speeds[2]:F2} m/s");
            boat.SetDriver(null); world.Player.Teleport(new Vector3(0, 3, 0), 0);
            var radarShop = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                .Single(value => value.Kind == HowToFishStationKind.BoatRadar && value.Island == 2);
            world.Player.Teleport(radarShop.transform.position + Vector3.back * 2.2f + Vector3.up * .1f, 0);
            yield return new WaitForSeconds(.35f);
            yield return AimAt(world.Player, radarShop.GetComponentsInChildren<Collider>().OrderByDescending(value => value.bounds.size.sqrMagnitude).First().bounds.center);
            Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(radarShop));
            yield return PressKey(Key.E);
            Assert.That(boat.HasRadar, Is.True);
            Assert.That(world.Session.Count("Radar"), Is.Zero);
            Assert.That(world.Session.EquipmentCapacity, Is.EqualTo(3));
            body.linearVelocity = Vector3.zero;
            world.Player.Board(boat.Seat); boat.SetDriver(world.Input);
            yield return new WaitForSeconds(.5f);
            var radar = boat.transform.Find("BoatRadar");
            Assert.That(radar.Find("Screen/Island3").gameObject.activeSelf, Is.True);
            Assert.That(radar.Find("Screen/Island4").gameObject.activeSelf, Is.False);
            yield return AimAt(world.Player, radar.position + Vector3.up * .2f);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/BoatRadar.png")); yield return null; yield return null;
            world.Save(); world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "船只升级无法返回Hub。");
            yield return EnterWorld();
            boat = UnityEngine.Object.FindAnyObjectByType<HowToFishBoat>();
            Assert.That(boat.MotorTier, Is.EqualTo(2)); Assert.That(boat.HasRadar, Is.True);
            Assert.That(testWorld.Session.State.money, Is.EqualTo(710));
        }

        [UnityTest]
        public IEnumerator SpiderCrab_ChargesStunsDiesAndEscapes()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Player.Teleport(new Vector3(0, 2.8f, -10), 0);
            var crab = world.Spawn("SpiderCrab", new Vector3(0, 3, -6), false);
            var behavior = crab.GetComponent<HowToFishSpiderCrab>();
            Assert.That(behavior, Is.Not.Null, "真实首领 Prefab 缺少战斗组件。");
            Assert.That(world.Player.PickUp(crab), Is.False, "活动首领不能直接抓取。");
            world.Save();
            StringAssert.Contains("首领战斗", world.Notice);
            yield return WaitFor(() => behavior.Phase == HowToFishCrabPhase.Charging, "首领未从预警转为冲撞。");
            yield return WaitFor(() => behavior.IsClawQueued, "接近玩家后没有锁定爪击。");
            world.Player.Teleport(new Vector3(0, 2.8f, -20), 0);
            yield return WaitFor(() => world.Session.State.health < 100, "退离距离不能取消已锁定爪击。");
            yield return WaitFor(() => behavior.Phase == HowToFishCrabPhase.Stunned, "冲撞结束未进入攻击窗口。");
            yield return new WaitForSeconds(2.1f);
            Assert.That(behavior.Phase, Is.EqualTo(HowToFishCrabPhase.Stunned), "两阶段硬直均应保持2.5秒。");
            Assert.That(ObjectFind<UnityEngine.UI.Image>("BossHealth"), Is.Not.Null, "首领条未显示。");
            var evidence = Path.GetFullPath("Library/HowToFish/Evidence");
            Directory.CreateDirectory(evidence);
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "SpiderCrabFight.png"));
            yield return null; yield return null;
            float escape = behavior.EscapeFraction;
            world.SetPaused(true);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(behavior.EscapeFraction, Is.EqualTo(escape));
            world.SetPaused(false);
            crab.Hit(crab.Health, Vector3.zero);
            Assert.That(behavior.IsFighting, Is.False);
            Assert.That(world.Player.PickUp(crab), Is.False, "主线首领尸体不能拾取，应取走掉落的战利品。");
            world.Player.Drop(false);
            var meat = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                .Single(item => item.DefinitionId == "CrabMeat");
            crab.Hit(100, Vector3.zero);
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                .Count(item => item.DefinitionId == "CrabMeat"), Is.EqualTo(1), "重复伤害不能重复掉落任务物品。");
            Assert.That(world.Player.PickUp(meat), Is.True);
            Assert.That(world.Player.HeldItem, Is.SameAs(meat));
            var second = world.Spawn("SpiderCrab", new Vector3(10, 3, -6), false);
            behavior = second.GetComponent<HowToFishSpiderCrab>();
            yield return WaitFor(() => behavior.Phase == HowToFishCrabPhase.Jumping, "远距离未触发跳跃。");
            // 加速超时尾段，仍由正式 FixedUpdate 执行逃脱，不等待完整 90 秒。
            typeof(HowToFishSpiderCrab).GetField("remaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(behavior, .05f);
            yield return WaitFor(() => second == null, "逃脱条归零未释放首领。");
            world.Save();
            StringAssert.Contains("已保存", world.Notice);
            Assert.That(world.InspectSlot(0).Data.worldItems.Count(item => item.definitionId == "SpiderCrab"), Is.EqualTo(1));
            world.Player.Drop(false);
            world.Player.Teleport(new Vector3(-4, 2.4f, -5), 0);
            PlaceForPickup(world.Player, meat);
            Assert.That(world.Player.PickUp(meat), Is.True);
            yield return null; yield return null;
            Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>()?.Kind, Is.EqualTo(HowToFishStationKind.Keeper));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.E));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(world.Session.State.hasBoatKey, Is.True);
            Assert.That(world.Session.Count("CrabMeat"), Is.Zero);
            Assert.That(meat == null || meat.IsConsumed, Is.True);
            Assert.That(world.InspectSlot(0).Data.hasBoatKey, Is.True, "任务奖励必须同时保存。");
        }

        [UnityTest]
        public IEnumerator BeerRadar_SailToForestAndResumeAboard()
        {
            yield return StartNewGame();
            var world = testWorld;
            // 本用例聚焦购买/投掷/航行，起始赚钱与首领拿钥匙由另外两个真实流程覆盖。
            world.Session.State.money = 25;
            foreach (string id in new[] { "CrabRod", "Beer", "Radar" })
            {
                yield return AimAtProduct(world, id);
                yield return PressKey(Key.E);
                Assert.That(world.Session.Count(id), Is.EqualTo(1), "购买失败：" + id);
            }
            Assert.That(world.Session.State.money, Is.Zero);
            yield return PressKey(Key.Q);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Beer"));
            world.Player.Teleport(new Vector3(-4, 2.4f, -6), 0);
            yield return AimAt(world.Player, new Vector3(-4, 3.4f, -3));
            yield return PressKey(Key.G);
            yield return WaitFor(() => world.Session.Count("EmptyBeerCan") == 1, "投掷啤酒没有换到空罐。");
            Assert.That(world.Session.Count("Beer"), Is.Zero);
            yield return PressKey(Key.Q);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Radar"));
            yield return WaitFor(() => ObjectFind<RectTransform>("RadarStatus") != null, "装备雷达后没有显示坐标。");
            Assert.That(ObjectFind<RectTransform>("RadarIsland1"), Is.Null, "未获得坐标前不能显示森林岛。");
            var boat = UnityEngine.Object.FindAnyObjectByType<HowToFishBoat>();
            var wheel = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                .Single(station => station.Kind == HowToFishStationKind.BoatWheel).GetComponent<Collider>();
            world.Player.Teleport(new Vector3(wheel.bounds.center.x, boat.transform.position.y + .3f, boat.transform.position.z - 1.8f), boat.transform.eulerAngles.y);
            yield return new WaitForSeconds(.3f);
            yield return AimAt(world.Player, wheel.bounds.center);
            yield return PressKey(Key.E);
            Assert.That(world.Player.IsDriving, Is.False);
            world.Session.State.hasBoatKey = true;
            world.Session.State.unlockedIsland = 1;
            yield return PressKey(Key.E);
            Assert.That(world.Player.IsDriving, Is.True, "真实船舵交互未接管驾驶。");
            var forest = world.Islands.Single(island => island.Index == 1);
            Vector3 departure = boat.transform.position;
            float deadline = Time.realtimeSinceStartup + 180;
            float departureTime = Time.time;
            bool clearedLighthouse = false;
            while (world.Player.Island != 1 && Time.realtimeSinceStartup < deadline)
            {
                // 首船在灯塔南岸；先绕过岛的西南侧，直线指向森林会把船开上灯塔岛。
                var waypoint = new Vector3(-50, 0, -45);
                if (Vector3.ProjectOnPlane(boat.transform.position - waypoint, Vector3.up).magnitude < 8) clearedLighthouse = true;
                var direction = Vector3.ProjectOnPlane((clearedLighthouse ? forest.Position : waypoint) - boat.transform.position, Vector3.up);
                float angle = Vector3.SignedAngle(boat.transform.forward, direction, Vector3.up);
                InputSystem.QueueStateEvent(gamepad, new GamepadState
                { leftStick = new Vector2(Mathf.Clamp(angle / 35, -1, 1), Mathf.Abs(angle) > 60 ? 0 : 1) });
                yield return null;
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            Assert.That(world.Player.Island, Is.EqualTo(1), $"持续手柄驾驶未抵达森林水域：船={boat.transform.position}，已航行 {Time.time - departureTime:0.0} 游戏秒。");
            Assert.That(Vector3.Distance(departure, boat.transform.position), Is.GreaterThan(150), "航行必须实际移动船体。");
            yield return new WaitForSeconds(1);
            yield return AimAt(world.Player, forest.Position + Vector3.up * 1.8f);
            world.Save();
            var saved = world.InspectSlot(0).Data;
            Assert.That(saved.isDriving && saved.isOnBoat, Is.True);
            Assert.That(saved.equippedItemId, Is.EqualTo("Radar"));
            var evidence = Path.GetFullPath("Library/HowToFish/Evidence");
            Directory.CreateDirectory(evidence);
            yield return WaitFor(() => !UnityEditor.ShaderUtil.anythingCompiling, "截图前着色器仍在编译。");
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "ForestRadarArrival.png"));
            yield return null; yield return null;
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "航行后无法返回 Hub。");
            yield return EnterWorld();
            world = testWorld;
            yield return WaitFor(() => world.Player.Island == 1 && world.Player.IsDriving, "海上读档把玩家留在旧岸上。");
            boat = UnityEngine.Object.FindAnyObjectByType<HowToFishBoat>();
            Assert.That(Vector3.Distance(saved.boatPosition, boat.transform.position), Is.LessThan(1));
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Radar"));
            Assert.That(world.Input.Asset.FindActionMap("Boat").enabled, Is.True);
            Assert.That(world.Input.Asset.FindActionMap("Gameplay").enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator GroundTeleportAndResume_RemainAboveIsland()
        {
            yield return StartNewGame();
            var world = testWorld;
            var motor = world.Player.GetComponent<CharacterController>();
            foreach (var point in new[] { new Vector3(-3.45f, 5, -5.35f), new Vector3(-4.66f, 5, -2.08f), new Vector3(0, 5, -15) })
            {
                Assert.That(Physics.Raycast(point, Vector3.down, out var ground, 10, ~0, QueryTriggerInteraction.Ignore), Is.True);
                world.Player.Teleport(ground.point - Vector3.up * .2f, 0);
                yield return new WaitForSeconds(.5f);
                Assert.That(motor.isGrounded, Is.True, "略低于地面的传送落点必须恢复为有效站立位置。");
                Assert.That(Vector3.Distance(world.Player.transform.position, ground.point), Is.LessThan(.4f));
                Assert.That(world.Session.State.health, Is.EqualTo(100));
            }
            var position = world.Player.transform.position;
            world.Save();
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "陆地存档后无法返回 Hub。");
            yield return EnterWorld();
            yield return new WaitForSeconds(.5f);
            Assert.That(testWorld.Player.GetComponent<CharacterController>().isGrounded, Is.True);
            Assert.That(Vector3.Distance(testWorld.Player.transform.position, position), Is.LessThan(.4f));
        }

        [UnityTest]
        public IEnumerator Forest_LeechProgressResumesAndBossDropsCoordinates()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 1;
            world.Session.State.hasBoatKey = true;
            var lady = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                .Single(station => station.Kind == HowToFishStationKind.ForestLady);
            world.Player.Teleport(lady.transform.position + Vector3.back * 2.5f, 0);
            yield return new WaitForSeconds(.5f);
            for (int delivered = 0; delivered < 3; delivered++)
            {
                var leech = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                    .First(item => item.DefinitionId == "Leech" && !item.IsConsumed);
                PlaceForPickup(world.Player, leech);
                Assert.That(world.Player.PickUp(leech), Is.True);
                yield return AimAt(world.Player, lady.transform.position + Vector3.up * 1.2f);
                if (delivered == 2)
                {
                    world.Session.GrantItem("ModifiedLeech", 100000);
                    yield return PressKey(Key.E);
                    Assert.That(world.Session.State.forestLeeches, Is.EqualTo(2));
                    Assert.That(leech.IsConsumed, Is.False, "奖励已满时不能吞掉第三条水蛭。");
                    world.Session.State.inventory.RemoveAll(item => item.id == "ModifiedLeech");
                }
                yield return PressKey(Key.E);
                Assert.That(leech == null || leech.IsConsumed, Is.True);
                if (delivered != 1) continue;
                Assert.That(world.InspectSlot(0).Data.forestLeeches, Is.EqualTo(2));
                world.ReturnToHub();
                yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "水蛭任务中途无法返回 Hub。");
                yield return EnterWorld();
                world = testWorld;
                lady = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                    .Single(station => station.Kind == HowToFishStationKind.ForestLady);
                Assert.That(world.Session.State.forestLeeches, Is.EqualTo(2));
                yield return new WaitForSeconds(.3f);
            }
            Assert.That(world.Session.State.forestLeeches, Is.Zero);
            Assert.That(world.Session.Count("ModifiedLeech"), Is.EqualTo(1));
            Assert.That(world.Catalog.RollCatch(1, "ModifiedLeech", 0, "FishingRod").Id, Is.EqualTo("GiantPiranha"));
            world.Session.GrantItem("FishingRod");
            world.Session.GrantItem("Shotgun");
            world.Player.Teleport(lady.transform.position + Vector3.forward * 2, 0);
            yield return new WaitForSeconds(.4f);
            yield return PressKey(Key.B);
            Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo("ModifiedLeech"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.6f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Waiting, "普通鱼竿浮漂没有入水。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "改造水蛭抛入湖泊后没有咬钩。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling, "巨型食人鱼未进入收线。");
            yield return PullBackCatch(world);
            Assert.That(world.Session.Count("ModifiedLeech"), Is.Zero);
            var boss = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                .Single(item => item.DefinitionId == "GiantPiranha");
            var behavior = boss.GetComponent<HowToFishGiantPiranha>();
            Assert.That(world.Player.PickUp(boss), Is.False);
            yield return PressKey(Key.Q);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Shotgun"));
            yield return WaitFor(() => UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                .Count(item => item.DefinitionId == "Piranha" && item.IsAlive) == 3, "食人鱼首领没有召唤鱼群。");
            var evidence = Path.GetFullPath("Library/HowToFish/Evidence");
            Directory.CreateDirectory(evidence);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A, Key.S));
            yield return new WaitForSeconds(1);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return AimAt(world.Player, boss.transform.position);
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "ForestPiranhaFight.png"));
            yield return null; yield return null;
            world.SetPaused(true);
            float escape = behavior.EscapeFraction;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(behavior.EscapeFraction, Is.EqualTo(escape));
            world.SetPaused(false);
            world.Save();
            StringAssert.Contains("首领战斗", world.Notice);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Shotgun"));
            bool died = false;
            void RecordDeath() => died = true;
            world.Player.Died += RecordDeath;
            var forest = world.Islands.Single(island => island.Index == 1);
            float battleDeadline = Time.realtimeSinceStartup + 70;
            var samples = new System.Collections.Generic.Queue<string>();
            while (boss != null && boss.IsAlive && !died && Time.realtimeSinceStartup < battleDeadline)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                var target = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                    .Where(item => item.DefinitionId == "Piranha" && item.IsAlive && Vector3.Distance(item.transform.position, world.Player.transform.position) < 2)
                    .OrderBy(item => Vector3.Distance(item.transform.position, world.Player.transform.position)).FirstOrDefault() ?? boss;
                var sight = target.GetComponent<Collider>().bounds.center - world.Player.Eye.transform.position;
                if (Physics.Raycast(world.Player.Eye.transform.position, sight.normalized, out var obstruction, sight.magnitude, ~0, QueryTriggerInteraction.Ignore) &&
                    obstruction.collider.GetComponentInParent<HowToFishWorldItem>() != target) target = boss;
                // 单帧瞄准后开火；循环等待移动目标精确重合会在低帧率下错失攻击窗口。
                PointMouseAt(world.Player, target.GetComponent<Collider>().bounds.center + target.Body.linearVelocity * Time.deltaTime);
                // 通过真实摇杆绕湖移动，留在 27–43 米的陆地环带，避免自动控制把人带进湖里。
                var radial = Vector3.ProjectOnPlane(world.Player.transform.position - forest.Position, Vector3.up);
                var tangent = Vector3.Cross(Vector3.up, radial).normalized;
                var direction = (tangent + radial.normalized * (radial.magnitude < 27 ? 1 : radial.magnitude > 43 ? -1 : 0)).normalized;
                var pad = new GamepadState();
                if (world.Player.Ammo == 0) pad = pad.WithButton(GamepadButton.North);
                else if (!world.Player.IsReloading) pad.rightTrigger = 1;
                InputSystem.QueueStateEvent(gamepad, pad);
                yield return new WaitForSeconds(.08f);
                var movement = new Vector2(Vector3.Dot(direction, world.Player.transform.right), Vector3.Dot(direction, world.Player.transform.forward)) * .9f;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = movement });
                yield return new WaitForSeconds(.2f);
                samples.Enqueue($"p={world.Player.transform.position}, hp={world.Session.State.health:0}, boss={boss.Health:0}, ammo={world.Player.Ammo}, target={target.DefinitionId}/{target.Health:0}");
                if (samples.Count > 12) samples.Dequeue();
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            world.Player.Died -= RecordDeath;
            world.SetPaused(true);
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "ForestGunBattleResult.png"));
            yield return null; yield return null;
            world.SetPaused(false);
            Assert.That(died, Is.False, "实际枪战中角色死亡，未完成战斗。\n" + string.Join("\n", samples));
            Assert.That(boss != null && !boss.IsAlive, Is.True, $"实际射击未击败巨型食人鱼，剩余生命 {boss?.Health}。");
            boss.Hit(100, Vector3.zero);
            yield return null;
            var skeletons = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                .Where(item => item.DefinitionId == "PiranhaSkeleton").ToArray();
            Assert.That(skeletons.Length, Is.EqualTo(1));
            world.Player.Teleport(lady.transform.position + Vector3.back * 2.5f, 0);
            PlaceForPickup(world.Player, skeletons[0]);
            Assert.That(world.Player.PickUp(skeletons[0]), Is.True);
            yield return AimAt(world.Player, lady.transform.position + Vector3.up * 1.2f);
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.unlockedIsland, Is.EqualTo(2));
            Assert.That(world.InspectSlot(0).Data.completedQuests, Does.Contain("ForestPiranha"));
            Assert.That(world.InspectSlot(0).Data.worldItems.Any(item => item.definitionId == "PiranhaSkeleton"), Is.False);
        }

        [UnityTest]
        public IEnumerator KillScore_ActualShotsRecordFractionalRewardAndPersistBody()
        {
            yield return StartNewGame();
            var world = testWorld;
            // 隔离武器和靶鱼准备；伤害、致死判定及评分只通过实际鼠标射击产生。
            world.Session.GrantItem("Pistol");
            world.Player.Teleport(new Vector3(8, 2.4f, -8), 0);
            yield return new WaitForSeconds(.5f);
            var fish = world.Spawn("Mackerel", world.Player.Eye.transform.position + Vector3.forward * 5, false);
            fish.Body.isKinematic = true;
            Physics.SyncTransforms();
            yield return AimAt(world.Player, fish.GetComponent<Collider>().bounds.center);
            yield return PressKey(Key.F);
            for (int shot = 0; shot < 2; shot++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                yield return new WaitForSeconds(.2f);
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return new WaitForSeconds(.35f);
                if (shot == 0)
                {
                    Assert.That(fish.Health, Is.EqualTo(15));
                    Assert.That(fish.HasBeenHitByPlayer, Is.True);
                    Assert.That(fish.KillMultiplier, Is.EqualTo(1), "未致死命中不应提前结算奖励。");
                }
                yield return AimAt(world.Player, fish.GetComponent<Collider>().bounds.center);
            }
            Assert.That(fish.IsAlive, Is.False);
            Assert.That(world.Player.Ammo, Is.EqualTo(8));
            Assert.That(fish.KillMultiplier, Is.GreaterThan(1));
            Assert.That(Mathf.Abs(fish.KillMultiplier - 2), Is.GreaterThan(.01f), "翻转武器动作不能直接赋予两倍奖励。");
            StringAssert.Contains("盲射", world.Notice);
            StringAssert.DoesNotContain("旋转", world.Notice);
            float multiplier = fish.KillMultiplier;
            string instance = fish.InstanceId;
            int value = fish.SaleValue;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/KillScore.png"));
            yield return null; yield return null;
            world.Save();
            var saved = world.InspectSlot(0).Data.worldItems.Single(item => item.instanceId == instance);
            Assert.That(saved.styleMultiplier, Is.EqualTo(multiplier));
            Assert.That(saved.hasBeenHitByPlayer, Is.True);
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "计分后无法返回Hub。");
            yield return EnterWorld();
            var restored = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(item => item.InstanceId == instance);
            Assert.That(restored.KillMultiplier, Is.EqualTo(multiplier));
            Assert.That(restored.SaleValue, Is.EqualTo(value));
            Assert.That(restored.HasBeenHitByPlayer, Is.True);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Audio_RealActionsPauseResumeAndHubRelease()
        {
            yield return StartNewGame();
            var world = testWorld;
            var player = world.Player;
            var fishing = player.Fishing;
            var director = UnityEngine.Object.FindObjectsByType<HowToFishAudioDirector>(FindObjectsSortMode.None)
                .Single(value => value.gameObject.scene == world.gameObject.scene);
            var audio = new UnityEditor.SerializedObject(director);
            var clips = audio.FindProperty("clips");
            Assert.That(clips.arraySize, Is.EqualTo(20));
            foreach (HowToFishSound sound in Enum.GetValues(typeof(HowToFishSound)))
            {
                var clip = clips.GetArrayElementAtIndex((int)sound).objectReferenceValue as AudioClip;
                Assert.That(clip, Is.Not.Null, sound.ToString());
                Assert.That(clip.name, Is.EqualTo(sound.ToString()));
                Assert.That(clip.length, Is.GreaterThan(0));
            }
            var sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .Where(value => value.gameObject.scene == world.gameObject.scene).ToArray();
            Assert.That(sources.Length, Is.EqualTo(10), "必须使用装配好的10个源，不在动作时临时创建。");
            Assert.That(sources.Count(value => value.loop), Is.EqualTo(5));
            var sea = (AudioSource)audio.FindProperty("sea").objectReferenceValue;
            var wind = (AudioSource)audio.FindProperty("wind").objectReferenceValue;
            var ui = (AudioSource)audio.FindProperty("ui").objectReferenceValue;
            var effects = sources.Where(value => value.name.StartsWith("Effect", StringComparison.Ordinal)).ToArray();
            Assert.That(effects.Length, Is.EqualTo(4));
            var counts = new int[20];
            var sourcePlayed = new bool[20];
            void CountSound(HowToFishSound sound, Vector3 position)
            {
                counts[(int)sound]++;
                // 同步观察真实事件后的音源状态，不调用Director.Play。
                sourcePlayed[(int)sound] |= sound == HowToFishSound.UiClick ? ui.isPlaying : effects.Any(value => value.isPlaying);
            }
            player.SoundRequested += CountSound; fishing.SoundRequested += CountSound; world.SoundRequested += CountSound;
            try
            {
                yield return WaitFor(() => sea.isPlaying && wind.isPlaying, "开始游戏后海风循环未播放。");
                var island = world.Islands.Single(value => value.Index == 0);
                var ground = island.GetComponent<MeshCollider>();
                Assert.That(ground.Raycast(new Ray(island.Position + new Vector3(10, 30, 8), Vector3.down), out var hit, 60), Is.True);
                player.Teleport(hit.point + Vector3.up * .1f, 0);
                yield return new WaitForSeconds(.3f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return new WaitForSeconds(.65f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.That(counts[(int)HowToFishSound.Footstep], Is.GreaterThan(0));
                Assert.That(sourcePlayed[(int)HowToFishSound.Footstep], Is.True);
                // 仅准备交易资格/资金；购买、射击、换弹仍走真实输入。
                world.Session.State.unlockedIsland = 1; world.Session.State.money = 1000;
                yield return AimAtProduct(world, "Pistol", 1); yield return PressKey(Key.E);
                Assert.That(player.Equipment.Id, Is.EqualTo("Pistol"));
                Assert.That(counts[(int)HowToFishSound.Trade], Is.EqualTo(1));
                int ammo = player.Ammo;
                InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
                Assert.That(player.Ammo, Is.EqualTo(ammo - 1));
                Assert.That(counts[(int)HowToFishSound.GunShot], Is.EqualTo(1));
                Assert.That(sourcePlayed[(int)HowToFishSound.GunShot], Is.True);
                yield return PressKey(Key.R);
                Assert.That(counts[(int)HowToFishSound.Reload], Is.EqualTo(1));
                Assert.That(sourcePlayed[(int)HowToFishSound.Reload], Is.True);
                yield return WaitFor(() => !player.IsReloading, "有效换弹未结束。");
                Assert.That(player.Ammo, Is.EqualTo(player.AmmoCapacity));
                yield return PressKey(Key.R);
                Assert.That(counts[(int)HowToFishSound.Reload], Is.EqualTo(1), "满弹匣不能重复发换弹音。");
                world.SetPaused(true); yield return null;
                Assert.That(sources.Where(value => value != ui).All(value => !value.isPlaying), Is.True,
                    "暂停后所有玩法循环和单次音源必须停止。");
                EventSystem.current.SetSelectedGameObject(ObjectFind<Button>("Resume").gameObject);
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
                Assert.That(world.IsPaused, Is.False);
                Assert.That(counts[(int)HowToFishSound.UiClick], Is.EqualTo(1), "真实手柄Submit应触发一次UI音。");
                Assert.That(sourcePlayed[(int)HowToFishSound.UiClick], Is.True);
                yield return WaitFor(() => sea.isPlaying && wind.isPlaying, "恢复后海风循环未继续。");
                Assert.That(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                    .Count(value => value.gameObject.scene == world.gameObject.scene), Is.EqualTo(10));
                world.ReturnToHub();
                yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub &&
                    !GameSceneNavigator.Instance.IsTransitioning, "声音验收未返回Hub。");
                Assert.That(director == null, Is.True);
                Assert.That(sources.All(value => value == null), Is.True, "返回Hub后不能残留场景音源。");
            }
            finally
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState());
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                if (player != null) player.SoundRequested -= CountSound;
                if (fishing != null) fishing.SoundRequested -= CountSound;
                if (world != null) world.SoundRequested -= CountSound;
            }
        }

        [UnityTest]
        public IEnumerator ForestGuns_PurchaseShootReloadAndPersistAmmo()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 1;
            world.Session.State.hasBoatKey = true;
            world.Session.State.money = 200;
            foreach (var id in new[] { "Pistol", "Shotgun" })
            {
                var station = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                    .Single(item => item.Kind == HowToFishStationKind.Product && item.ItemId == id && item.Island == 1);
                var target = station.GetComponent<Collider>().bounds.center;
                var approach = new Vector3(target.x, target.y + 3, target.z - 2.2f);
                Assert.That(Physics.Raycast(approach, Vector3.down, out var ground, 20, ~0, QueryTriggerInteraction.Ignore), Is.True);
                world.Player.Teleport(ground.point, 0);
                yield return new WaitForSeconds(.3f);
                yield return AimAt(world.Player, target);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(station));
                yield return PressKey(Key.E);
            }
            Assert.That(world.Session.State.money, Is.Zero);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Pistol"));
            Assert.That(world.Player.Ammo, Is.EqualTo(10));
            var forest = world.Islands.Single(island => island.Index == 1);
            world.Player.Teleport(forest.Position + new Vector3(0, 2, -45), 180);
            yield return new WaitForSeconds(.5f);
            var fish = world.Spawn("GiantPiranha", world.Player.Eye.transform.position + world.Player.transform.forward * 6, false);
            fish.Body.isKinematic = true; // 此例验证枪械射线和弹药；活动首领另由森林遭遇例验证。
            Physics.SyncTransforms();
            yield return AimAt(world.Player, fish.GetComponent<Collider>().bounds.center);
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 });
            yield return new WaitForSeconds(.2f);
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            Assert.That(world.Player.Ammo, Is.EqualTo(9));
            Assert.That(fish.Health, Is.EqualTo(fish.Creature.Health - 25));
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            SceneManager.MoveGameObjectToScene(wall, world.gameObject.scene);
            wall.transform.position = world.Player.Eye.transform.position + world.Player.Eye.transform.forward * 2;
            wall.transform.localScale = Vector3.one * 2;
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.5f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            Assert.That(world.Player.Ammo, Is.EqualTo(8), "半自动枪持续按住只应开一枪。");
            Assert.That(fish.Health, Is.EqualTo(fish.Creature.Health - 25), "子弹不应穿过实体墙面。");
            UnityEngine.Object.Destroy(wall);
            yield return null;
            yield return PressKey(Key.R);
            Assert.That(world.Player.IsReloading, Is.True);
            world.SetPaused(true);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(world.Player.Ammo, Is.EqualTo(8));
            world.SetPaused(false);
            yield return new WaitForSeconds(1.5f);
            Assert.That(world.Player.Ammo, Is.EqualTo(10));
            yield return PressKey(Key.Q);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Shotgun"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.3f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            Assert.That(world.Player.Ammo, Is.EqualTo(1));
            Assert.That(fish.Health, Is.InRange(fish.Creature.Health - 100, fish.Creature.Health - 50), "霰弹应产生单独弹丸命中。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.North));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            Assert.That(world.Player.IsReloading, Is.True, "手柄 Y 未开始换弹。");
            yield return PressKey(Key.Q);
            Assert.That(world.Player.IsReloading, Is.False);
            yield return PressKey(Key.Q);
            Assert.That(world.Player.Ammo, Is.EqualTo(1), "切枪不能凭空填满弹匣。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.3f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            float health = fish.Health;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.3f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            Assert.That(world.Player.Ammo, Is.Zero);
            Assert.That(fish.Health, Is.EqualTo(health), "空弹匣不能造成伤害。");
            fish.Hit(fish.Health, Vector3.zero);
            yield return new WaitForSeconds(3.1f);
            fish.Hit(fish.Health, Vector3.zero);
            world.Save();
            Assert.That(world.InspectSlot(0).Data.inventory.Single(item => item.id == "Shotgun").ammo, Is.Zero);
            var evidence = Path.GetFullPath("Library/HowToFish/Evidence");
            Directory.CreateDirectory(evidence);
            ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "ForestShotgun.png"));
            yield return null; yield return null;
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "枪械存档后无法返回 Hub。");
            yield return EnterWorld();
            Assert.That(testWorld.Player.Equipment.Id, Is.EqualTo("Shotgun"));
            Assert.That(testWorld.Player.Ammo, Is.Zero, "空弹匣读档必须仍为空。");
        }

        [UnityTest]
        public IEnumerator ForestFishing_BuyRodRetrieveTwoPoolsSellAndResume()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 1;
            world.Session.State.hasBoatKey = true;
            world.Session.State.money = world.Catalog.FindItem("FishingRod").Price + world.Catalog.FindItem("BeginnerLure").Price;
            foreach (var id in new[] { "FishingRod", "BeginnerLure" })
            {
                yield return AimAtProduct(world, id, 1);
                yield return PressKey(Key.E);
                Assert.That(world.Session.Count(id), Is.EqualTo(1));
            }
            Assert.That(world.Session.State.money, Is.Zero);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("FishingRod"));
            var forest = world.Islands.Single(island => island.Index == 1);
            var seller = forest.GetComponentsInChildren<HowToFishStation>()
                .Single(station => station.Kind == HowToFishStationKind.Sell);
            for (int cast = 0; cast < 2; cast++)
            {
                world.Player.Teleport(forest.Position + new Vector3(-12, 3, -26), 0);
                yield return new WaitForSeconds(.6f);
                if (cast == 1) yield return PressKey(Key.B);
                Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo(cast == 0 ? "FreeLure" : "BeginnerLure"));
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return new WaitForSeconds(.6f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Waiting, "森林浮漂没有进入湖水。");
                if (cast == 0)
                {
                    yield return new WaitForSeconds(6);
                    Assert.That(world.Player.Fishing.State.Phase, Is.EqualTo(HowToFishFishingPhase.Waiting), "普通鱼竿不能原地静置就咬钩。");
                }
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "慢收没有吸引鱼咬钩。");
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling, "森林普通鱼没有进入收线。");
                float initialProgress = world.Player.Fishing.State.Progress;
                yield return new WaitForSeconds(.5f);
                Assert.That(world.Player.Fishing.State.Progress, Is.EqualTo(initialProgress));
                yield return PullBackCatch(world);
                var fish = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                    .Single(item => item.Creature != null && !item.Creature.IsGroundPickup && !item.Creature.IsBoss);
                Assert.That(fish.Creature.Baits, Does.Contain(cast == 0 ? "FreeLure" : "BeginnerLure"));
                fish.Hit(fish.Health, Vector3.zero);
                var counter = seller.GetComponent<Collider>().bounds.center;
                world.Player.Teleport(new Vector3(counter.x, counter.y - .9f, counter.z - 2), 0);
                yield return new WaitForSeconds(.3f);
                PlaceForPickup(world.Player, fish);
                Assert.That(world.Player.PickUp(fish), Is.True);
                yield return AimAt(world.Player, counter);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(seller));
                Assert.That(world.Player.HeldItem, Is.SameAs(fish), "到柜台后鱼获不应从手中脱落。");
                Assert.That(fish.HasBeenHeld, Is.True, "玩家拾取必须记录可出售资格。");
                int before = world.Session.State.money;
                yield return PressKey(Key.E);
                Assert.That(world.Session.State.money, Is.GreaterThan(before), $"第{cast + 1}次出售{fish.DefinitionId}失败：{world.Notice}");
                Assert.That(world.Session.State.defeatedCreatures, Does.Contain(fish.DefinitionId));
            }
            int remainingBait = world.Session.Count("BeginnerLure");
            Assert.That(remainingBait, Is.InRange(0, 1), "普通鱼饵仅按丢失概率扣除。");
            world.Save();
            int money = world.Session.State.money;
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "森林钓鱼后无法返回 Hub。");
            yield return EnterWorld();
            Assert.That(testWorld.Session.State.money, Is.EqualTo(money));
            Assert.That(testWorld.Session.Count("FishingRod"), Is.EqualTo(1));
            Assert.That(testWorld.Session.Count("BeginnerLure"), Is.EqualTo(remainingBait));
            Assert.That(testWorld.Player.Equipment.Id, Is.EqualTo("FishingRod"));
        }

        [UnityTest]
        public IEnumerator BaitLifecycle_EmptyCancelPreservesBaitAndAttachedReleaseConsumesOnce()
        {
            yield return StartNewGame();
            var world = testWorld;
            // 隔离鱼饵库存准备；在首岛使用蟹竿，实际输入验证共享鱼池及耗饵时机。
            world.Session.GrantItem("CrabRod");
            world.Session.GrantItem("BeginnerBossLure", 2);
            yield return null;
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("CrabRod"));
            yield return PressKey(Key.B);
            Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo("BeginnerBossLure"));
            for (int cast = 0; cast < 3; cast++)
            {
                world.Player.Teleport(new Vector3(-12, 3, -26), 180);
                yield return new WaitForSeconds(.4f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return new WaitForSeconds(.6f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Waiting, "付费鱼饵无法在首岛用蟹竿入水。");
                int before = world.Session.Count("BeginnerBossLure");
                if (cast == 0)
                {
                    yield return new WaitForSeconds(4);
                    Assert.That(world.Player.Fishing.State.Phase, Is.EqualTo(HowToFishFishingPhase.Waiting), "是否需要移动取决于鱼饵，不能只看蟹竿类型。");
                }
                else
                {
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                    yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "收动鱼饵没有触发咬钩。");
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    yield return null;
                    Assert.That(world.Session.Count("BeginnerBossLure"), Is.EqualTo(before), "咬钩本身不能提前扣饵。");
                }
                if (cast < 2)
                {
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1 });
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    yield return null;
                    Assert.That(world.Player.Fishing.State.Phase, Is.EqualTo(HowToFishFishingPhase.Idle));
                }
                else yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Escaped, "错过咬钩窗口没有脱钩。");
                int expected = cast == 0 ? before : before - 1;
                Assert.That(world.Session.Count("BeginnerBossLure"), Is.EqualTo(expected));
                world.Player.Fishing.Cancel();
                world.Player.Fishing.Cancel();
                Assert.That(world.Session.Count("BeginnerBossLure"), Is.EqualTo(expected), "反复收线不能重复扣饵。");
            }
            Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo("FreeLure"));
            world.Save();
            Assert.That(world.InspectSlot(0).Data.inventory.Any(item => item.id == "BeginnerBossLure" && item.count > 0), Is.False);
        }

        [UnityTest]
        public IEnumerator Desert_TradeCookPoisonAndResumeProgress()
        {
            yield return StartNewGame();
            var world = testWorld;
            // 独立验证第三岛规则；完整新档航程仍由端到端验收覆盖。
            world.Session.State.unlockedIsland = 2; world.Session.State.hasBoatKey = true;
            world.Session.State.money = 1000;
            yield return AimAtProduct(world, "SMG", 2);
            yield return PressKey(Key.E);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("SMG"));
            int ammo = world.Player.Ammo;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.55f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            Assert.That(ammo - world.Player.Ammo, Is.GreaterThan(1), "持续扣扳机应连续开火，不能退化为每次按下只开一枪。");
            yield return PressKey(Key.R);
            yield return WaitFor(() => !world.Player.IsReloading, "冲锋枪换弹未结束。");
            Assert.That(world.Player.Ammo, Is.EqualTo(world.Player.Equipment.MagazineSize));
            yield return AimAtProduct(world, "StandardLure", 2); yield return PressKey(Key.E);
            Assert.That(world.Session.Count("StandardLure"), Is.EqualTo(1));
            var tourist = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None).Single(s => s.Kind == HowToFishStationKind.Tourist);
            var chef = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None).Single(s => s.Kind == HowToFishStationKind.GrillMaster && s.Island == 2);
            var grill = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None).Single(s => s.Kind == HowToFishStationKind.Grill && s.Island == 2);
            IEnumerator Approach(HowToFishStation station)
            {
                world.Player.Teleport(station.transform.position + Vector3.back * 2.3f, 0);
                yield return new WaitForSeconds(.35f);
                yield return AimAt(world.Player, station.GetComponent<Collider>().bounds.center);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(station));
            }
            yield return Approach(tourist);
            var endangered = world.Spawn("Seahorse", world.Player.transform.position + Vector3.up, false);
            PlaceForPickup(world.Player, endangered); Assert.That(world.Player.PickUp(endangered), Is.True);
            yield return PressKey(Key.E);
            Assert.That(endangered.IsConsumed, Is.False, "游客不应接受活鱼。");
            endangered.Hit(1000, Vector3.zero);
            world.Session.GrantItem("Carrot", 100000);
            yield return PressKey(Key.E);
            Assert.That(endangered.IsConsumed, Is.False, "奖励满额时必须保留鱼获。");
            world.Session.State.inventory.RemoveAll(item => item.id == "Carrot");
            yield return PressKey(Key.E);
            Assert.That(world.Session.Count("Carrot"), Is.EqualTo(1));
            Assert.That(world.InspectSlot(0).Data.completedQuests, Does.Contain("DesertCarrot"));
            Assert.That(world.Catalog.RollCatch(2, "Carrot", 0, "FishingRod").Id, Is.EqualTo("Pufferfish"));
            Assert.That(world.Catalog.RollCatch(2, "StandardBossLure", 0, "FishingRod").Id, Is.EqualTo("BlueShark"));
            yield return Approach(grill);
            var meat = world.Spawn("FishMeat", world.Player.transform.position + Vector3.up, false);
            PlaceForPickup(world.Player, meat); Assert.That(world.Player.PickUp(meat), Is.True);
            yield return PressKey(Key.E); Assert.That(meat.IsCooked, Is.False, "取得打火机前不能烹饪。");
            world.Player.Drop(false);
            var shark = world.Spawn("BlueShark", chef.transform.position + Vector3.left * 6 + Vector3.up, false);
            yield return new WaitForSeconds(.1f);
            Assert.That(world.ActiveBoss?.Item, Is.SameAs(shark));
            shark.Hit(10000, Vector3.zero);
            yield return Approach(chef); yield return PressKey(Key.E);
            Assert.That(world.Session.State.hasGrill, Is.False, "仅击杀鲨鱼不能替代实际交付。");
            PlaceForPickup(world.Player, shark); Assert.That(world.Player.PickUp(shark), Is.True);
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.completedQuests, Does.Contain("GrillSharkDelivered"));
            Assert.That(world.Session.State.hasGrill, Is.False, "交付后应单独领取打火机。");
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.hasGrill, Is.True);
            Assert.That(world.Session.Count("Lighter"), Is.EqualTo(1));
            yield return Approach(grill);
            PlaceForPickup(world.Player, meat); Assert.That(world.Player.PickUp(meat), Is.True);
            yield return PressKey(Key.E); Assert.That(meat.IsCooked, Is.False, "交互不能瞬间烤熟鱼获。");
            world.Player.Drop(false);
            var heat = grill.GetComponentInChildren<HowToFishGrill>();
            meat.Body.position = heat.CookingPosition; meat.Body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(13);
            Assert.That(meat.Cooking, Is.InRange(.48f, .6f), "真实烤架热区没有持续加热鱼获。");
            var desert = world.Islands.Single(island => island.Index == 2);
            world.Player.Teleport(desert.Position + new Vector3(0, 4, -15), 0);
            var puffer = world.Spawn("Pufferfish", desert.Position + new Vector3(0, 4, -7), false);
            var behavior = puffer.GetComponent<HowToFishPufferfish>();
            yield return new WaitForSeconds(.4f);
            Assert.That(world.Player.PickUp(puffer), Is.False);
            Assert.That(puffer.transform.localScale, Is.EqualTo(Vector3.one));
            float radius = puffer.GetComponent<SphereCollider>().radius;
            puffer.Hit(puffer.Creature.Health * .55f, Vector3.zero);
            yield return WaitFor(() => UnityEngine.Object.FindAnyObjectByType<HowToFishDamagePool>() != null, "半血河豚没有产生毒区。");
            Assert.That(behavior.IsEnraged, Is.True);
            Assert.That(puffer.GetComponent<SphereCollider>().radius, Is.GreaterThan(radius * 1.4f));
            world.SetPaused(true);
            float escape = behavior.EscapeFraction;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(behavior.EscapeFraction, Is.EqualTo(escape));
            world.SetPaused(false);
            world.Save(); StringAssert.Contains("首领战斗", world.Notice);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/DesertPufferfish.png"));
            yield return null; yield return null;
            yield return WaitFor(() => !puffer.GetComponent<HowToFishBossTransition>().IsProtected, "河豚阶段保护没有结束。");
            puffer.Hit(10000, Vector3.zero);
            yield return null; yield return null;
            Assert.That(UnityEngine.Object.FindAnyObjectByType<HowToFishDamagePool>(), Is.Null, "首领死亡后毒区应清理。");
            var fin = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(i => i.DefinitionId == "PufferfishFin");
            yield return Approach(tourist);
            PlaceForPickup(world.Player, meat); Assert.That(world.Player.PickUp(meat), Is.True);
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.unlockedIsland, Is.EqualTo(2), "普通肉块不能替代河豚鱼鳍。");
            world.Player.Drop(false);
            PlaceForPickup(world.Player, fin); Assert.That(world.Player.PickUp(fin), Is.True);
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.unlockedIsland, Is.EqualTo(3));
            Assert.That(world.InspectSlot(0).Data.completedQuests, Does.Contain("DesertPufferfish"));
            Assert.That(world.InspectSlot(0).Data.worldItems.Any(item => item.definitionId == "PufferfishFin" || item.definitionId == "Pufferfish"), Is.False);
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "沙漠任务完成后无法返回 Hub。");
            yield return EnterWorld();
            Assert.That(testWorld.Session.State.unlockedIsland, Is.EqualTo(3));
            Assert.That(testWorld.Session.State.hasGrill, Is.True);
            Assert.That(testWorld.Session.Count("SMG"), Is.EqualTo(1));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Cooking_HeatsBurnsWashesToolsPersistsAndRequiresHeldEating()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 2; world.Session.State.hasBoatKey = true; world.Session.State.hasGrill = true;
            var grill = UnityEngine.Object.FindObjectsByType<HowToFishGrill>(FindObjectsSortMode.None)
                .Single(value => value.GetComponentInParent<HowToFishStation>().Island == 2);
            var standing = grill.transform.position + Vector3.back * 2.3f;
            world.Player.Teleport(standing, 0); yield return new WaitForSeconds(.4f);
            var fish = world.Spawn("Mackerel", world.Player.transform.position + Vector3.up, false);
            fish.Hit(10000, Vector3.zero, 2);
            PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True); world.Player.Drop(false);
            string fishId = fish.InstanceId;
            fish.Body.position = grill.CookingPosition; fish.Body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(6.5f);
            Assert.That(fish.Cooking, Is.InRange(.18f, .32f));
            float cooking = fish.Cooking;
            world.SetPaused(true); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(fish.Cooking, Is.EqualTo(cooking)); world.SetPaused(false);
            yield return new WaitForSeconds(6.2f);
            Assert.That(fish.Cooking, Is.InRange(.48f, .56f));
            Assert.That(fish.SaleValue, Is.EqualTo(world.Session.CatchValue("Mackerel", fish.Cooking, false, 2, 1, fish.WeightMultiplier)));
            int cookedValue = fish.SaleValue;
            fish.Body.position = grill.CookingPosition + Vector3.back * 3; fish.Body.linearVelocity = Vector3.zero;
            yield return new WaitForFixedUpdate(); cooking = fish.Cooking;
            yield return new WaitForSeconds(1);
            Assert.That(fish.Cooking, Is.EqualTo(cooking), "离开烤架必须停止受热。");
            fish.Body.position = grill.CookingPosition; fish.Body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(13);
            Assert.That(fish.Cooking, Is.EqualTo(1)); Assert.That(fish.IsBurnt, Is.True);
            Assert.That(fish.SaleValue, Is.LessThan(cookedValue / 4));
            fish.Body.position = new Vector3(0, -1, -90); fish.Body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(.3f);
            Assert.That(fish.Cooking, Is.EqualTo(1), "烧焦鱼获不能用海水恢复。");
            fish.Body.position = standing + Vector3.right * 2 + Vector3.up;
            world.Session.GrantItem("Pistol");
            yield return PressKey(Key.G);
            var gun = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.DefinitionId == "Pistol");
            gun.Body.position = grill.CookingPosition; gun.Body.rotation = Quaternion.Euler(0, 90, 0); gun.Body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(7);
            Assert.That(gun.EquipmentState.cooking, Is.GreaterThan(.2f));
            gun.Body.position = new Vector3(0, -.5f, -90); gun.Body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(.1f);
            Assert.That(gun.EquipmentState.cooking, Is.Zero);
            // 真实热区与浸水已覆盖；准备固定程度的烧焦工具以隔离拾取和存档路径。
            PlaceForPickup(world.Player, gun); Assert.That(gun.Heat(.7f), Is.True);
            Assert.That(world.Player.PickUp(gun), Is.True);
            Assert.That(world.Session.State.inventory.Single(value => value.id == "Pistol").cooking, Is.EqualTo(.7f));
            int burntValue = fish.SaleValue;
            world.Save(); world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "烹饪存档无法返回Hub。");
            yield return EnterWorld(); world = testWorld;
            fish = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.InstanceId == fishId);
            Assert.That(fish.Cooking, Is.EqualTo(1)); Assert.That(fish.SaleValue, Is.EqualTo(burntValue)); Assert.That(fish.HasBeenHeld, Is.True);
            var owned = world.Session.State.inventory.Single(value => value.id == "Pistol");
            Assert.That(owned.cooking, Is.EqualTo(.7f));
            world.Player.Teleport(new Vector3(0, -1.8f, -90), 0);
            yield return new WaitForSeconds(.1f);
            Assert.That(owned.cooking, Is.Zero, "手持工具浸没后也应清洗。");
            world.Player.Teleport(standing, 0); yield return new WaitForSeconds(.4f);
            world.Session.State.health = 50; world.Session.State.hunger = 50;
            PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
            int ammo = world.Player.Ammo;
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); yield return new WaitForSeconds(.5f);
            Assert.That(fish.IsConsumed, Is.False); Assert.That(world.Player.EatingProgress, Is.GreaterThan(.2f));
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSeconds(.1f);
            Assert.That(world.Player.EatingProgress, Is.Zero);
            InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 1 }); yield return new WaitForSeconds(.4f);
            yield return PressKey(Key.H); yield return new WaitForSeconds(1.7f);
            Assert.That(fish.IsConsumed, Is.False, "切换装备后必须中断原进食。");
            InputSystem.QueueStateEvent(mouse, new MouseState()); yield return new WaitForSeconds(.1f);
            yield return PressKey(Key.H); Assert.That(world.Player.Ammo, Is.EqualTo(ammo), "拿着死鱼进食不应开枪。");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/BurntFish.png")); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 }); yield return new WaitForSeconds(1.7f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            Assert.That(fish == null || fish.IsConsumed, Is.True);
            Assert.That(world.Session.State.health, Is.InRange(51.7f, 51.9f));
            Assert.That(world.Session.State.hunger, Is.GreaterThan(50));
        }

        [UnityTest]
        public IEnumerator DesertPoison_DamagesInsideFreezesOnPauseAndExpires()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Player.Teleport(new Vector3(0, 2.4f, -8), 0);
            yield return new WaitForSeconds(.3f);
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/how_to_fish/Prefabs/Items/PoisonPool.prefab");
            var pool = UnityEngine.Object.Instantiate(source, world.Player.transform.position + Vector3.right * 4, Quaternion.identity).GetComponent<HowToFishDamagePool>();
            try
            {
                pool.Initialize(world.Player);
                float health = world.Session.State.health;
                yield return new WaitForSeconds(.3f);
                Assert.That(world.Session.State.health, Is.EqualTo(health));
                pool.transform.position = world.Player.transform.position;
                yield return WaitFor(() => world.Session.State.poisonSeconds > 0, "毒区没有施加中毒。");
                health = world.Session.State.health;
                yield return new WaitForSeconds(.5f); yield return null;
                Assert.That(world.Session.State.health, Is.LessThan(health - 2), "毒区内应持续扣血。");
                world.SetPaused(true); health = world.Session.State.health;
                float remaining = (float)typeof(HowToFishDamagePool).GetField("remaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(world.Session.State.health, Is.EqualTo(health));
                Assert.That((float)typeof(HowToFishDamagePool).GetField("remaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool), Is.EqualTo(remaining));
                world.SetPaused(false);
                pool.transform.position += Vector3.right * 4;
                yield return new WaitForSeconds(.4f);
                Assert.That(world.Session.State.health, Is.LessThan(health - 2), "离开毒区后残留中毒仍应扣血。");
                Assert.That(world.Session.State.poisonSeconds, Is.GreaterThan(2));
                world.Save();
                Assert.That(world.InspectSlot(0).Data.poisonSeconds, Is.GreaterThan(0), "存档丢失了残留中毒。");
                yield return new WaitForSeconds(3.5f);
                Assert.That(pool == null, Is.True, "毒区寿命结束应销毁。");
                Assert.That(world.Session.State.poisonSeconds, Is.Zero);
                health = world.Session.State.health;
                yield return new WaitForSeconds(.3f);
                Assert.That(world.Session.State.health, Is.EqualTo(health));
                var lava = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/how_to_fish/Prefabs/Items/LavaPool.prefab");
                pool = UnityEngine.Object.Instantiate(lava, world.Player.transform.position, Quaternion.identity).GetComponent<HowToFishDamagePool>();
                pool.Initialize(world.Player);
                yield return new WaitForSeconds(.2f);
                Assert.That(world.Session.State.burningSeconds, Is.GreaterThan(0));
                remaining = (float)typeof(HowToFishDamagePool).GetField("remaining", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(pool);
                Assert.That(remaining, Is.InRange(19, 20), "熔岩区域应持续20秒。");
                pool.transform.position += Vector3.right * 4;
                health = world.Session.State.health;
                yield return new WaitForSeconds(.3f);
                Assert.That(world.Session.State.health, Is.LessThan(health - 2), "离开熔岩后仍应有燃烧残留。");
                world.Player.Damage(1000);
                Assert.That(world.Session.State.burningSeconds, Is.Zero, "复活不能继承上次死亡的燃烧。");
                Assert.That(world.Session.State.poisonSeconds, Is.Zero);
            }
            finally { if (pool != null) UnityEngine.Object.Destroy(pool.gameObject); }
        }

        [UnityTest]
        public IEnumerator DesertPufferfish_ReelFightWithSmgAndDropFin()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 2; world.Session.State.hasBoatKey = true;
            world.Session.GrantItem("FishingRod"); world.Session.GrantItem("SMG"); world.Session.GrantItem("Carrot");
            var desert = world.Islands.Single(island => island.Index == 2);
            world.Player.Teleport(desert.Position + new Vector3(0, 1, -61), 180);
            yield return new WaitForSeconds(.5f);
            yield return PressKey(Key.B);
            Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo("Carrot"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.6f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Waiting, "河豚鱼饵未入海。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "胡萝卜慢收未引出河豚。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling, "河豚未进入收线。");
            yield return PullBackCatch(world);
            Assert.That(world.Session.Count("Carrot"), Is.Zero);
            var boss = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(item => item.DefinitionId == "Pufferfish");
            yield return PressKey(Key.Q);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("SMG"));
            bool died = false;
            void RecordDeath() => died = true;
            world.Player.Died += RecordDeath;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            var samples = new System.Collections.Generic.Queue<string>();
            float deadline = Time.realtimeSinceStartup + 100;
            while (boss != null && boss.IsAlive && !died && Time.realtimeSinceStartup < deadline)
            {
                PointMouseAt(world.Player, boss.GetComponent<Collider>().bounds.center + boss.Body.linearVelocity * Time.deltaTime);
                yield return null;
                var radial = Vector3.ProjectOnPlane(world.Player.transform.position - desert.Position, Vector3.up);
                var tangent = Vector3.Cross(Vector3.up, radial).normalized;
                var direction = (tangent + radial.normalized * (radial.magnitude > 29 ? -2 : radial.magnitude < 17 ? 1 : 0)).normalized;
                var pad = new GamepadState { leftStick = new Vector2(Vector3.Dot(direction, world.Player.transform.right), Vector3.Dot(direction, world.Player.transform.forward)) };
                if (world.Player.Ammo == 0) pad = pad.WithButton(GamepadButton.North);
                else if (!world.Player.IsReloading) pad.rightTrigger = 1;
                InputSystem.QueueStateEvent(gamepad, pad);
                yield return null;
                samples.Enqueue($"health={world.Session.State.health:F1}, boss={boss?.Health}, distance={(boss == null ? 0 : Vector3.Distance(boss.transform.position, world.Player.transform.position)):F1}, radius={radial.magnitude:F1}, ammo={world.Player.Ammo}");
                if (samples.Count > 12) samples.Dequeue();
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            world.Player.Died -= RecordDeath;
            Assert.That(died, Is.False, "真实河豚战斗中角色死亡。\n" + string.Join("\n", samples));
            Assert.That(world.Session.State.defeatedCreatures, Does.Contain("Pufferfish"), "持续移动与真实射击未完成河豚击杀。\n" + string.Join("\n", samples));
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Count(item => item.DefinitionId == "PufferfishFin"), Is.EqualTo(1));
            world.SetPaused(true);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/DesertSmgBattleResult.png"));
            yield return null; yield return null;
        }

        [UnityTest]
        public IEnumerator Rocks_BaitRoofHeadDeliveryAndResume()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 3; world.Session.State.hasBoatKey = true; world.Session.State.money = 5000;
            var rocks = world.Islands.Single(island => island.Index == 3);
            var shop = rocks.transform.Find("RocksShop");
            var stations = shop.GetComponentsInChildren<HowToFishStation>();
            foreach (string id in new[] { "SniperRifle", "ProfessionalBossLure" })
            {
                var station = stations.Single(value => value.ItemId == id);
                world.Player.Teleport(shop.TransformPoint(id == "SniperRifle" ? new Vector3(-1.1f, .2f, .8f) : new Vector3(.2f, .2f, 1.1f)), 0);
                yield return new WaitForSeconds(.3f);
                yield return AimAt(world.Player, station.GetComponent<Collider>().bounds.center);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(station), "无法从店内瞄准商品 " + id);
                yield return PressKey(Key.E);
                Assert.That(world.Session.Count(id), Is.EqualTo(1));
            }
            Assert.That(world.Session.State.money, Is.Zero);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("SniperRifle"));
            Assert.That(world.Player.Ammo, Is.EqualTo(5));
            var dropPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/Demos/how_to_fish/Prefabs/Items/BirdDropping.prefab");
            foreach (float side in new[] { -1f, 1f })
            {
                world.Player.Teleport(shop.TransformPoint(new Vector3(side, .2f, .9f)), 0);
                yield return new WaitForSeconds(.3f);
                float health = world.Session.State.health;
                var drop = UnityEngine.Object.Instantiate(dropPrefab, world.Player.Eye.transform.position + Vector3.up * 8, Quaternion.identity).GetComponent<HowToFishProjectile>();
                drop.Initialize(world.Player, Vector3.down * 5);
                yield return WaitFor(() => drop == null, "屋顶上方落物未命中并释放。");
                Assert.That(world.Session.State.health, Is.EqualTo(health), "两侧屋顶必须真实挡住落物。");
            }
            world.Player.Teleport(rocks.Position + new Vector3(8, 4.8f, -30), 0);
            yield return new WaitForSeconds(.3f);
            float beforeHit = world.Session.State.health;
            var exposed = UnityEngine.Object.Instantiate(dropPrefab, world.Player.Eye.transform.position + Vector3.up * 8, Quaternion.identity).GetComponent<HowToFishProjectile>();
            exposed.Initialize(world.Player, Vector3.down * 5);
            world.SetPaused(true);
            var suspendedPosition = exposed.transform.position;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(exposed.transform.position, Is.EqualTo(suspendedPosition));
            Assert.That(world.Session.State.health, Is.EqualTo(beforeHit));
            world.SetPaused(false);
            yield return WaitFor(() => exposed == null, "露天落物没有结束。");
            Assert.That(world.Session.State.health, Is.EqualTo(beforeHit - 12), "露天落物应只造成一次伤害。");

            var tuna = world.Spawn("Tuna", world.Player.transform.position + Vector3.forward * 2, false);
            PlaceForPickup(world.Player, tuna);
            Assert.That(world.Player.PickUp(tuna), Is.True, "活金枪鱼应允许搬运。");
            yield return new WaitForSeconds(4.2f);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<HowToFishAlbatross>(), Is.Null, "活鱼不能诱鸟。");
            world.Player.Drop(false);
            tuna.Hit(10000, Vector3.zero);
            Assert.That(tuna.Heat(.5f), Is.True);
            tuna.Body.position = rocks.Position + new Vector3(10, 4.9f, -30); tuna.Body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(4.3f);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<HowToFishAlbatross>(), Is.Null, "熟鱼不能诱鸟。");
            tuna.TryConsume(() => { });
            var wetTuna = world.Spawn("Tuna", rocks.Position + new Vector3(0, -1, -85), false);
            wetTuna.Hit(10000, Vector3.zero);
            yield return new WaitForSeconds(4.3f);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<HowToFishAlbatross>(), Is.Null, "海里鱼身不能诱鸟。");
            wetTuna.TryConsume(() => { });
            tuna = world.Spawn("Tuna", rocks.Position + new Vector3(10, 4.9f, -30), false);
            tuna.Hit(10000, Vector3.zero);
            yield return WaitFor(() => UnityEngine.Object.FindAnyObjectByType<HowToFishAlbatross>() != null, "岸上完整生金枪鱼未召唤信天翁。");
            Assert.That(tuna == null || tuna.IsConsumed, Is.True, "用于诱鸟的鱼身必须消费，不能重复召唤。");
            var bird = UnityEngine.Object.FindAnyObjectByType<HowToFishAlbatross>();
            Assert.That(world.ActiveBoss, Is.SameAs(bird));
            yield return new WaitForSeconds(1);
            world.Session.GrantItem("FishingRod");
            yield return PressKey(Key.Q); yield return PressKey(Key.B);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("FishingRod"));
            Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo("ProfessionalBossLure"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.3f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(world.Session.Count("ProfessionalBossLure"), Is.EqualTo(1), "诱鸟首领活动时不能消耗鱼饵再次召唤首领。");
            Assert.That(world.Notice, Does.Contain("先结束当前首领"));
            yield return PressKey(Key.Q);
            yield return AimAt(world.Player, () => bird.Item.Body.worldCenterOfMass);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/RocksAlbatross.png"));
            yield return null; yield return null;
            world.Save(); Assert.That(world.Notice, Does.Contain("首领战斗"));
            world.SetPaused(true);
            float fraction = bird.EscapeFraction;
            var birdPosition = bird.transform.position;
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(bird.EscapeFraction, Is.EqualTo(fraction));
            Assert.That(bird.transform.position, Is.EqualTo(birdPosition));
            world.SetPaused(false);
            bird.Item.Hit(10000, Vector3.zero);
            yield return null; yield return null;
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishProjectile>(FindObjectsSortMode.None), Is.Empty, "战斗结束需清理飞行落物。");
            var head = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.DefinitionId == "AlbatrossHead");
            var islander = stations.Single(value => value.Kind == HowToFishStationKind.Islander);
            var meat = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).First(value => value.DefinitionId == "FishMeat");
            world.Player.Teleport(shop.TransformPoint(new Vector3(.7f, .2f, .65f)), 0);
            yield return new WaitForSeconds(.3f);
            yield return AimAt(world.Player, islander.GetComponent<Collider>().bounds.center);
            Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(islander));
            PlaceForPickup(world.Player, meat); Assert.That(world.Player.PickUp(meat), Is.True);
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.unlockedIsland, Is.EqualTo(3), "普通肉块不能替代鸟头。");
            world.Player.Drop(false);
            PlaceForPickup(world.Player, head); Assert.That(world.Player.PickUp(head), Is.True);
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.unlockedIsland, Is.EqualTo(4));
            Assert.That(world.Session.State.completedQuests, Does.Contain("RocksAlbatross"));
            Assert.That(world.InspectSlot(0).Data.worldItems.Any(value => value.definitionId == "AlbatrossHead" || value.definitionId == "Albatross" || value.definitionId == "Tuna"), Is.False);
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "岩石任务后无法回到 Hub。");
            yield return EnterWorld();
            Assert.That(testWorld.Session.State.unlockedIsland, Is.EqualTo(4));
            Assert.That(testWorld.Session.Count("SniperRifle"), Is.EqualTo(1));
            Assert.That(testWorld.Session.State.completedQuests, Does.Contain("RocksAlbatross"));
        }

        [UnityTest]
        public IEnumerator Rocks_ReelTunaAndDefeatAlbatrossWithSniper()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 3; world.Session.State.hasBoatKey = true;
            world.Session.GrantItem("FishingRod"); world.Session.GrantItem("SniperRifle"); world.Session.GrantItem("ProfessionalBossLure");
            var rocks = world.Islands.Single(island => island.Index == 3);
            world.Player.Teleport(rocks.Position + new Vector3(0, 1, -61), 180);
            yield return new WaitForSeconds(.5f);
            yield return PressKey(Key.B);
            Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo("ProfessionalBossLure"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.6f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Waiting, "专业首领饵未入海。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "专业首领饵慢收未引出金枪鱼。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling, "金枪鱼未进入收线。");
            yield return PullBackCatch(world);
            Assert.That(world.Session.Count("ProfessionalBossLure"), Is.Zero);
            yield return PressKey(Key.Q);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("SniperRifle"));
            bool died = false;
            void RecordDeath() => died = true;
            world.Player.Died += RecordDeath;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
            var samples = new System.Collections.Generic.Queue<string>();
            // 战斗路径留在商店东侧空地，避免自动绕圈撞进房屋后持续对着屋顶开枪。
            var arena = rocks.Position + new Vector3(18, 0, -25);
            float deadline = Time.realtimeSinceStartup + 130;
            bool fire = false;
            while (!world.Session.State.defeatedCreatures.Contains("Albatross") && !died && Time.realtimeSinceStartup < deadline)
            {
                var boss = world.ActiveBoss?.Item;
                var radial = Vector3.ProjectOnPlane(world.Player.transform.position - arena, Vector3.up);
                var tangent = Vector3.Cross(Vector3.up, radial).normalized;
                var direction = (tangent + radial.normalized * (radial.magnitude > 10 ? -2 : radial.magnitude < 6 ? 1 : 0)).normalized;
                var moveForward = world.Player.transform.forward;
                if (boss != null)
                {
                    var predicted = boss.GetComponent<Collider>().bounds.center + (boss.Body.linearVelocity - direction * 6.8f) * Time.deltaTime;
                    PointMouseAt(world.Player, predicted);
                    moveForward = Vector3.ProjectOnPlane(predicted - world.Player.Eye.transform.position, Vector3.up).normalized;
                }
                var pad = new GamepadState { leftStick = new Vector2(Vector3.Dot(direction, Vector3.Cross(Vector3.up, moveForward)), Vector3.Dot(direction, moveForward)) };
                fire = !fire;
                if (world.Player.Ammo == 0) pad = pad.WithButton(GamepadButton.North);
                else if (!world.Player.IsReloading && fire && boss != null) pad.rightTrigger = 1;
                InputSystem.QueueStateEvent(gamepad, pad);
                yield return null;
                float aimError = boss == null ? 0 : Vector3.Angle(world.Player.Eye.transform.forward, boss.GetComponent<Collider>().bounds.center - world.Player.Eye.transform.position);
                samples.Enqueue($"dt={Time.deltaTime:F3}, aim={aimError:F1}, health={world.Session.State.health:F1}, boss={boss?.DefinitionId}/{boss?.Health}, player={world.Player.transform.position}, ammo={world.Player.Ammo}");
                if (samples.Count > 12) samples.Dequeue();
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            world.Player.Died -= RecordDeath;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/RocksSniperBattleResult.png"));
            yield return null; yield return null;
            Assert.That(died, Is.False, "真实岩石岛战斗中角色死亡。\n" + string.Join("\n", samples));
            Assert.That(world.Session.State.defeatedCreatures, Does.Contain("Tuna"), "狙击枪未完成金枪鱼击杀。\n" + string.Join("\n", samples));
            Assert.That(world.Session.State.defeatedCreatures, Does.Contain("Albatross"), "诱鸟与真实射击未完成信天翁击杀。\n" + string.Join("\n", samples));
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Count(value => value.DefinitionId == "AlbatrossHead"), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator RocksProfessionalFishing_BuyCatchSellAndResume() => FishingTradeAndResume(3, "ProfessionalLure", -61);

        [UnityTest]
        public IEnumerator StandardFishing_BuyCatchSellAndResume() => FishingTradeAndResume(2, "StandardLure", -61);

        [UnityTest]
        public IEnumerator ScientificFishing_BuyCatchSellAndResume() => FishingTradeAndResume(4, "ScientificLure", -88);

        private IEnumerator FishingTradeAndResume(int islandIndex, string baitId, float shoreZ)
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = islandIndex; world.Session.State.hasBoatKey = true;
            world.Session.State.money = world.Catalog.FindItem(baitId).Price * 2;
            world.Session.GrantItem("FishingRod"); world.Session.GrantItem("SniperRifle");
            var island = world.Islands.Single(value => value.Index == islandIndex);
            var shop = island.transform.Find("RocksShop");
            var seller = island.GetComponentsInChildren<HowToFishStation>().Single(value => value.Kind == HowToFishStationKind.Sell);
            var sellerShape = seller.GetComponentsInChildren<Collider>().OrderByDescending(value => value.bounds.size.sqrMagnitude).First();
            if (islandIndex == 3)
            {
                var product = shop.GetComponentsInChildren<HowToFishStation>().Single(value => value.ItemId == baitId);
                world.Player.Teleport(shop.TransformPoint(new Vector3(1.3f, .2f, 1.1f)), 0);
                yield return new WaitForSeconds(.3f);
                yield return AimAt(world.Player, product.GetComponent<Collider>().bounds.center);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(product));
            }
            else yield return AimAtProduct(world, baitId, islandIndex);
            yield return PressKey(Key.E); yield return PressKey(Key.E);
            Assert.That(world.Session.Count(baitId), Is.EqualTo(2));
            Assert.That(world.Session.State.money, Is.Zero);
            yield return PressKey(Key.B);
            Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo(baitId));
            for (int catchIndex = 0; catchIndex < 2; catchIndex++)
            {
                if (world.Player.Equipment.Id != "FishingRod") yield return PressKey(Key.Q);
                world.Player.Teleport(island.Position + new Vector3(0, 1, shoreZ), 180);
                yield return new WaitForSeconds(.3f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return new WaitForSeconds(.6f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Waiting, "专业普通饵未入海。");
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "专业普通鱼池未产生鱼讯。");
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling, "专业普通鱼未进入收线。");
                yield return PullBackCatch(world);
                var fish = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None)
                    .Single(value => value.Creature?.Island == islandIndex && !value.Creature.IsBoss && !value.Creature.IsGroundPickup && !value.IsConsumed);
                Assert.That(fish.Creature.Baits, Does.Contain(baitId));
                yield return PressKey(Key.Q);
                Assert.That(world.Player.Equipment.Id, Is.EqualTo("SniperRifle"));
                PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
                yield return new WaitForSeconds(.6f);
                for (int shot = 0; fish.IsAlive && shot < 5; shot++)
                {
                    if (world.Player.Ammo == 0) { yield return PressKey(Key.R); yield return WaitFor(() => !world.Player.IsReloading, "狙击枪换弹未完成。"); }
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                    yield return new WaitForSeconds(.12f);
                    InputSystem.QueueStateEvent(gamepad, new GamepadState());
                    yield return new WaitForSeconds(1.05f);
                }
                Assert.That(fish.IsAlive, Is.False, "实际射击未处理手持鱼获 " + fish.DefinitionId);
                if (islandIndex == 3) world.Player.Teleport(shop.TransformPoint(new Vector3(.65f, .2f, 1.1f)), 0);
                else
                {
                    var approach = sellerShape.bounds.center + Vector3.back * 2.2f;
                    Assert.That(Physics.Raycast(approach + Vector3.up * 3, Vector3.down, out var ground, 20, ~0, QueryTriggerInteraction.Ignore), Is.True);
                    world.Player.Teleport(ground.point, 0);
                }
                yield return new WaitForSeconds(.3f);
                yield return AimAt(world.Player, sellerShape.bounds.center);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(seller));
                PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
                int before = world.Session.State.money;
                yield return PressKey(Key.E);
                Assert.That(world.Session.State.money, Is.GreaterThan(before));
                Assert.That(fish == null || fish.IsConsumed, Is.True);
                int after = world.Session.State.money;
                yield return PressKey(Key.E);
                Assert.That(world.Session.State.money, Is.EqualTo(after), "不能重复出售已消费鱼获。");
            }
            Assert.That(world.Session.Count(baitId), Is.Zero);
            int money = world.Session.State.money, ammo = world.Player.Ammo;
            world.Save();
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "专业鱼出售后无法返回 Hub。");
            yield return EnterWorld();
            Assert.That(testWorld.Session.State.money, Is.EqualTo(money));
            Assert.That(testWorld.Player.Equipment.Id, Is.EqualTo("SniperRifle"));
            Assert.That(testWorld.Player.Ammo, Is.EqualTo(ammo));
            Assert.That(testWorld.Session.Count(baitId), Is.Zero);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Wildlife_SeagullCarriesDropsAndRestoresRemains()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Player.Teleport(new Vector3(18, 3, 5), 0);
            var fish = world.Spawn("Mackerel", new Vector3(19, 3, 8), false);
            fish.Hit(10000, Vector3.zero);
            var bird = world.Spawn("Seagull", fish.transform.position + Vector3.up * 3, false);
            var flight = bird.GetComponent<HowToFishSeagull>();
            yield return WaitFor(() => flight.CarriedItem == fish, "海鸥没有叼起鱼尸。");
            world.SetPaused(true);
            Vector3 birdPosition = bird.Body.position, fishPosition = fish.Body.position;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(Vector3.Distance(bird.Body.position, birdPosition), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(fish.Body.position, fishPosition), Is.LessThan(.001f));
            world.SetPaused(false);
            bird.Hit(50, Vector3.zero); yield return null;
            Assert.That(fish.IsHeld, Is.False, "海鸥死亡后必须松开携带物。");
            Assert.That(fish.Body.useGravity && bird.Body.useGravity, Is.True);
            string birdId = bird.InstanceId;
            var heldBird = world.Spawn("Seagull", world.Player.transform.position + Vector3.up * 2, false);
            PlaceForPickup(world.Player, heldBird); Assert.That(world.Player.PickUp(heldBird), Is.True);
            yield return new WaitForSeconds(.2f);
            heldBird.Hit(50, Vector3.zero); world.Player.Drop(false);
            Assert.That(heldBird.Body.useGravity, Is.True, "在手中杀死飞行生物后释放，不能继续悬空。");
            Assert.That(world.Session.State.discoveredCreatures, Does.Not.Contain("Seagull"));
            world.Save(); world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "环境生物保存后无法返回 Hub。");
            yield return EnterWorld(); world = testWorld;
            var restored = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.InstanceId == birdId);
            Assert.That(restored.IsAlive, Is.False);
            Assert.That(restored.Body.useGravity, Is.True);
            // 清除前半段测试准备的可叼物，避免它们与玩家遗体竞争目标。
            foreach (var item in UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None))
                if (item.DefinitionId == "Mackerel" || item.DefinitionId == "Seagull") item.TryConsume(() => { });
            world.Player.Teleport(new Vector3(18, 3, 5), 0);
            world.Player.Damage(10000);
            var remains = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.DefinitionId == "PlayerRemains");
            Assert.That(world.Session.State.health, Is.EqualTo(100));
            var carrier = world.Spawn("Seagull", remains.transform.position + Vector3.up * 3, false).GetComponent<HowToFishSeagull>();
            yield return WaitFor(() => carrier.CarriedItem == remains, "海鸥没有叼起玩家遗体。");
            Vector3 before = remains.Body.position;
            yield return new WaitForSeconds(1);
            Assert.That(Vector3.Distance(remains.Body.position, before), Is.GreaterThan(.5f));
            UnityEngine.Object.Destroy(carrier.gameObject); yield return null;
            Assert.That(remains.IsHeld, Is.False);
            Assert.That(remains.Body.useGravity, Is.True, "海鸥卸载时也必须释放遗体。");
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Wildlife_CoconutCatchShootAndDeliverBingBong()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 2; world.Session.State.hasBoatKey = true; world.Session.State.money = 350;
            world.Session.GrantItem("FishingRod"); world.Session.GrantItem("SMG");
            yield return AimAtProduct(world, "Coconut", 2); yield return PressKey(Key.E);
            Assert.That(world.Session.Count("Coconut"), Is.EqualTo(1));
            Assert.That(world.Session.State.money, Is.Zero);
            var desert = world.Islands.Single(value => value.Index == 2);
            world.Player.Teleport(desert.Position + new Vector3(0, 1, -61), 180);
            yield return new WaitForSeconds(.3f); yield return PressKey(Key.B);
            Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo("Coconut"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 }); yield return new WaitForSeconds(.6f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Waiting, "椰子未入水。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "椰子未产生特殊鱼讯。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling, "特殊鱼未进入收线。");
            yield return PullBackCatch(world);
            var fish = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.DefinitionId == "BingBong");
            Assert.That(world.ActiveBoss, Is.Null, "普通攻击生物不能开启或覆盖首领条。");
            Assert.That(world.Session.Count("Coconut"), Is.Zero);
            PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
            yield return PressKey(Key.Q); Assert.That(world.Player.Equipment.Id, Is.EqualTo("SMG"));
            yield return new WaitForSeconds(.6f);
            bool died = false;
            void OnDeath() => died = true;
            world.Player.Died += OnDeath;
            float deadline = Time.realtimeSinceStartup + 60;
            while (fish.IsAlive && !died && Time.realtimeSinceStartup < deadline)
            {
                var state = new GamepadState { rightTrigger = 1 };
                if (world.Player.Ammo == 0) state = state.WithButton(GamepadButton.North);
                InputSystem.QueueStateEvent(gamepad, state);
                yield return null;
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            world.Player.Died -= OnDeath;
            Assert.That(died, Is.False);
            Assert.That(fish.IsAlive, Is.False, "冲锋枪未处理手持 Bing Bong。");
            Assert.That(world.Session.State.discoveredCreatures, Does.Not.Contain("BingBong"));
            var tourist = desert.GetComponentsInChildren<HowToFishStation>().Single(value => value.Kind == HowToFishStationKind.Tourist);
            world.Player.Teleport(tourist.transform.position + Vector3.back * 2.3f, 0);
            yield return new WaitForSeconds(.3f);
            yield return AimAt(world.Player, tourist.GetComponent<Collider>().bounds.center);
            PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
            yield return PressKey(Key.E);
            Assert.That(world.Session.Count("Carrot"), Is.EqualTo(1));
            Assert.That(fish == null || fish.IsConsumed, Is.True);
            Assert.That(world.InspectSlot(0).Data.inventory.Any(value => value.id == "Carrot"), Is.True);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator MiniBoss_ReelShootAndEatThreeSpecies()
        {
            yield return StartNewGame();
            var world = testWorld;
            // 隔离装备和区域准备；用确定性随机种子选物种，随后只通过实际输入完成钓获和战斗。
            world.Session.State.unlockedIsland = 4; world.Session.State.hasBoatKey = true;
            foreach (string id in new[] { "FishingRod", "Shotgun", "AssaultRifle", "ScientificBossLure" }) world.Session.GrantItem(id);
            world.Session.GrantItem("BeginnerBossLure", 2);
            var island = world.Islands.Single(value => value.Index == 4);
            var arena = island.Position + new Vector3(32, 0, -62);
            bool died = false;
            void OnDeath() => died = true;
            world.Player.Died += OnDeath;
            foreach (string id in new[] { "Sunfish", "OldPike", "GoblinShark" })
            {
                for (int i = 0; i < 4 && world.Player.Equipment.Id != "FishingRod"; i++) yield return PressKey(Key.Q);
                Assert.That(world.Player.Equipment.Id, Is.EqualTo("FishingRod"));
                string bait = id == "GoblinShark" ? "ScientificBossLure" : "BeginnerBossLure";
                for (int i = 0; i < 5 && world.Player.Fishing.SelectedBait != bait; i++) yield return PressKey(Key.B);
                Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo(bait));
                world.Player.Teleport(island.Position + new Vector3(33, 2, -82), 180);
                yield return new WaitForSeconds(.3f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return new WaitForSeconds(.6f);
                int seed = 0;
                for (; seed < 1000; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    if (world.Catalog.RollCatch(4, bait, UnityEngine.Random.value, "FishingRod").Id == id) break;
                }
                Assert.That(seed, Is.LessThan(1000));
                UnityEngine.Random.InitState(seed);
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                // 在放线这一输入步消费已选种子，避免其它帧回调提前消耗全局随机数。
                InputSystem.Update();
                world.Player.Fishing.Step(world.Input, 4);
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Waiting, "可选首领鱼饵没有入水。");
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "可选首领没有咬钩。");
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling, "可选首领未进入收线。");
                yield return PullBackCatch(world);
                var target = world.ActiveBoss.Item;
                Assert.That(target.DefinitionId, Is.EqualTo(id));
                string weapon = id == "Sunfish" ? "Shotgun" : "AssaultRifle";
                for (int i = 0; i < 4 && world.Player.Equipment.Id != weapon; i++) yield return PressKey(Key.Q);
                Assert.That(world.Player.Equipment.Id, Is.EqualTo(weapon));
                var samples = new System.Collections.Generic.Queue<string>();
                float deadline = Time.realtimeSinceStartup + 150;
                bool fire = false;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                while (target != null && target.IsAlive && !died && Time.realtimeSinceStartup < deadline)
                {
                    var radial = Vector3.ProjectOnPlane(world.Player.transform.position - arena, Vector3.up);
                    var tangent = Vector3.Cross(Vector3.up, radial).normalized;
                    var direction = (tangent + radial.normalized * (radial.magnitude > 18 ? -2 : radial.magnitude < 12 ? 2 : 0)).normalized;
                    var predicted = target.GetComponent<Collider>().bounds.center + (target.Body.linearVelocity - direction * 6.8f) * Time.deltaTime;
                    PointMouseAt(world.Player, predicted);
                    var forward = Vector3.ProjectOnPlane(predicted - world.Player.Eye.transform.position, Vector3.up).normalized;
                    var state = new GamepadState { leftStick = new Vector2(Vector3.Dot(direction, Vector3.Cross(Vector3.up, forward)), Vector3.Dot(direction, forward)) };
                    fire = !fire;
                    if (world.Player.Ammo == 0) state = state.WithButton(GamepadButton.North);
                    else if (!world.Player.IsReloading && (world.Player.Equipment.Automatic || fire)) state.rightTrigger = 1;
                    InputSystem.QueueStateEvent(gamepad, state);
                    yield return null;
                    samples.Enqueue($"{id}: health={world.Session.State.health:F1}, boss={target?.Health}, ammo={world.Player.Ammo}, player={world.Player.transform.position}");
                    if (samples.Count > 10) samples.Dequeue();
                }
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(died, Is.False, string.Join("\n", samples));
                Assert.That(target != null && !target.IsAlive, Is.True, "未通过射击完成击杀。\n" + string.Join("\n", samples));
                Assert.That(world.Session.State.defeatedCreatures, Does.Contain(id));
                Assert.That(world.Session.State.defeatedDripCreatures, Does.Contain(id));
                PlaceForPickup(world.Player, target); Assert.That(world.Player.PickUp(target), Is.True);
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 }); yield return new WaitForSeconds(1.7f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                Assert.That(target == null || target.IsConsumed, Is.True, "可选首领尸体不能进食。");
            }
            world.Player.Died -= OnDeath;
            Assert.That(world.Session.Count("BeginnerBossLure") + world.Session.Count("ScientificBossLure"), Is.Zero);
            world.SetPaused(true);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/OptionalFishBattle.png"));
            yield return null; yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator InventorySlots_BuyStowDropGroundSaveAndRecoverWeapon()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 2; world.Session.State.hasBoatKey = true; world.Session.State.money = 3000;
            foreach (var row in new[] { ("CrabRod",0), ("Knife",0), ("Radar",0), ("Pistol",1) })
            { yield return AimAtProduct(world, row.Item1, row.Item2); yield return PressKey(Key.E); }
            Assert.That(world.Session.EquipmentCapacity, Is.EqualTo(3));
            Assert.That(world.Session.UnstoredEquipment.id, Is.EqualTo("Pistol"));
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Pistol"));
            StringAssert.Contains("手持未收纳", ObjectFind<TextMeshProUGUI>("EquipmentSlots").text);
            int money = world.Session.State.money;
            yield return AimAtProduct(world, "Shotgun", 1); yield return PressKey(Key.E);
            Assert.That(world.Session.Count("Shotgun"), Is.Zero); Assert.That(world.Session.State.money, Is.EqualTo(money));
            IEnumerator Expand()
            {
                var station = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                    .Single(value => value.Kind == HowToFishStationKind.InventoryUpgrade && value.Island == 1);
                var shape = station.GetComponentsInChildren<Collider>().OrderByDescending(value => value.bounds.size.sqrMagnitude).First();
                world.Player.Teleport(station.transform.position + Vector3.back * 2.2f + Vector3.up * .1f, 0);
                yield return new WaitForSeconds(.35f); yield return AimAt(world.Player, shape.bounds.center);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(station));
                int cost = world.Session.NextSlotCost, before = world.Session.State.money;
                yield return PressKey(Key.E); Assert.That(world.Session.State.money, Is.EqualTo(before - cost));
            }
            yield return Expand(); yield return PressKey(Key.Digit4);
            Assert.That(world.Session.State.equipmentSlots[3], Is.EqualTo("Pistol")); Assert.That(world.Session.UnstoredEquipment, Is.Null);
            Assert.That(world.Session.TryUpgrade("Pistol", 1, out _), Is.True);
            foreach (var part in new[] { HowToFishAttachment.RedDotSight, HowToFishAttachment.Compensator, HowToFishAttachment.LaserSight, HowToFishAttachment.ExtendedMag })
                Assert.That(world.Session.TryBuyAttachment("Pistol", part, 2, out _), Is.True);
            yield return PressKey(Key.R); yield return WaitFor(() => !world.Player.IsReloading, "背包测试换弹未结束。");
            world.Player.Teleport(new Vector3(18, 2.4f, 8), 0); yield return new WaitForSeconds(.3f);
            for (int i = 0; i < 3; i++)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 }); yield return new WaitForSeconds(.04f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return new WaitForSeconds(.18f);
            }
            Assert.That(world.Player.Ammo, Is.EqualTo(14));
            yield return PressKey(Key.G); yield return new WaitForSeconds(.1f);
            Assert.That(world.Player.Equipment, Is.Null); Assert.That(world.Session.Count("Pistol"), Is.Zero);
            var dropped = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(item => item.DefinitionId == "Pistol");
            string instanceId = dropped.InstanceId;
            Assert.That(dropped.EquipmentState.ammo, Is.EqualTo(14)); Assert.That(dropped.EquipmentState.upgrade, Is.EqualTo(1));
            Assert.That(dropped.VisualRoot.GetComponentsInChildren<Transform>(true).Single(node => node.name == "Attachment_RedDotSight").gameObject.activeSelf, Is.True);
            yield return AimAtProduct(world, "Shotgun", 1); yield return PressKey(Key.E);
            yield return AimAtProduct(world, "SMG", 2); yield return PressKey(Key.E);
            Assert.That(world.Session.UnstoredEquipment.id, Is.EqualTo("SMG"));
            yield return PressKey(Key.Q);
            Assert.That(world.Session.Count("SMG"), Is.Zero, "满栏切换时，未收纳武器必须实际落地。");
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Any(item => item.DefinitionId == "SMG"), Is.True);
            world.Save();
            Assert.That(world.InspectSlot(0).Data.worldItems.Single(item => item.instanceId == instanceId).equipment.ammo, Is.EqualTo(14));
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "装备落地保存后未返回 Hub。");
            yield return EnterWorld(); world = testWorld;
            Assert.That(world.Session.EquipmentCapacity, Is.EqualTo(4));
            dropped = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(item => item.InstanceId == instanceId);
            PlaceForPickup(world.Player, dropped); Assert.That(world.Player.PickUp(dropped), Is.True); yield return null;
            Assert.That(world.Session.UnstoredEquipment.id, Is.EqualTo("Pistol"));
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Pistol")); Assert.That(world.Player.Ammo, Is.EqualTo(14));
            Assert.That(world.Player.AmmoCapacity, Is.EqualTo(17)); Assert.That(world.Session.UpgradeLevel("Pistol"), Is.EqualTo(1));
            var owned = world.Session.State.inventory.Single(item => item.id == "Pistol");
            Assert.That(owned.sight, Is.EqualTo(HowToFishAttachment.RedDotSight)); Assert.That(owned.barrel, Is.EqualTo(HowToFishAttachment.Compensator));
            Assert.That(owned.hasLaser && owned.hasExtendedMag, Is.True);
            yield return Expand(); yield return PressKey(Key.Digit5);
            Assert.That(world.Session.State.equipmentSlots[4], Is.EqualTo("Pistol")); Assert.That(world.Session.UnstoredEquipment, Is.Null);
            yield return PressKey(Key.H); Assert.That(world.Player.Equipment, Is.Null);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadDown)); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Pistol")); Assert.That(world.Player.Ammo, Is.EqualTo(14));
            world.Save();
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/EquipmentSlots.png")); yield return null; yield return null;
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Attachments_BuyAimReloadRejectAndResume()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 3; world.Session.State.hasBoatKey = true; world.Session.State.money = 10000;
            world.Session.GrantItem("Pistol"); world.Session.GrantItem("Shotgun");
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("Pistol"));
            IEnumerator Approach(HowToFishAttachment attachment, int island)
            {
                var station = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                    .Single(value => value.Kind == HowToFishStationKind.Attachment && value.Attachment == attachment && value.Island == island);
                var shape = station.GetComponentsInChildren<Collider>().Single(value => value.name == "Lining");
                world.Player.Teleport(station.transform.position + Vector3.back * 1.8f + Vector3.up * .1f, 0);
                yield return new WaitForSeconds(.35f);
                yield return AimAt(world.Player, shape.bounds.center);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(station));
            }
            foreach (var row in new[] { (HowToFishAttachment.LaserSight, 1), (HowToFishAttachment.RedDotSight, 2),
                (HowToFishAttachment.Compensator, 2), (HowToFishAttachment.ExtendedMag, 2) })
            {
                yield return Approach(row.Item1, row.Item2);
                int before = world.Session.State.money, cost = world.Player.Equipment.AttachmentPrice(row.Item1);
                StringAssert.Contains("$" + cost, world.FocusText());
                yield return PressKey(Key.E);
                Assert.That(world.Session.State.money, Is.EqualTo(before - cost));
                Assert.That(world.Session.State.inventory.Find(item => item.id == "Pistol").HasAttachment(row.Item1), Is.True);
            }
            var view = world.Player.GetComponentInChildren<HowToFishEquipmentView>();
            Assert.That(view.RecoilMultiplier, Is.LessThan(1));
            var laser = view.GetComponentsInChildren<LineRenderer>().Single();
            Assert.That(laser.enabled, Is.True);
            Assert.That(Physics.Raycast(world.Player.Eye.transform.position, world.Player.Eye.transform.forward, out var laserHit, 60, ~0, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(Vector3.Distance(laser.GetPosition(1), laserHit.point), Is.LessThan(.02f));
            Assert.That(world.Player.AmmoCapacity, Is.EqualTo(17));
            yield return PressKey(Key.R); yield return WaitFor(() => !world.Player.IsReloading, "扩容后的换弹未完成。");
            Assert.That(world.Player.Ammo, Is.EqualTo(17));
            world.Player.Teleport(new Vector3(18, 2.4f, 8), 0); yield return new WaitForSeconds(.3f);
            yield return AimAt(world.Player, world.Player.Eye.transform.position + Vector3.forward * 20);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1 }); yield return new WaitForSeconds(.5f);
            Assert.That(world.Player.IsAiming, Is.True); Assert.That(world.Player.Eye.fieldOfView, Is.EqualTo(50).Within(.1f));
            var target = world.Spawn("Catfish", world.Player.Eye.transform.position + world.Player.Eye.transform.forward, false);
            PlaceForPickup(world.Player, target); Assert.That(world.Player.PickUp(target), Is.True); yield return new WaitForSeconds(.3f);
            float health = target.Health;
            var beforeShot = world.Player.Eye.transform.rotation;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1, rightTrigger = 1 }); yield return new WaitForSeconds(.08f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1 }); yield return new WaitForSeconds(.08f);
            Assert.That(target.Health, Is.EqualTo(health - 25).Within(.01f));
            Assert.That(Quaternion.Angle(beforeShot, world.Player.Eye.transform.rotation), Is.EqualTo(world.Player.Equipment.RecoilAngle * view.RecoilMultiplier).Within(.06f));
            Assert.That(world.Player.Ammo, Is.EqualTo(16)); world.Player.Drop(false);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            yield return PressKey(Key.Q); Assert.That(world.Player.Equipment.Id, Is.EqualTo("Shotgun"));
            Assert.That(world.Player.AmmoCapacity, Is.EqualTo(2));
            yield return Approach(HowToFishAttachment.ExtendedMag, 2);
            int money = world.Session.State.money;
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.money, Is.EqualTo(money)); StringAssert.Contains("不支持", world.Notice);
            yield return PressKey(Key.Q); Assert.That(world.Player.Equipment.Id, Is.EqualTo("Pistol"));
            yield return Approach(HowToFishAttachment.SniperScope, 3); yield return PressKey(Key.E);
            yield return Approach(HowToFishAttachment.Suppressor, 3); yield return PressKey(Key.E);
            var owned = world.Session.State.inventory.Find(item => item.id == "Pistol");
            Assert.That(owned.sight, Is.EqualTo(HowToFishAttachment.SniperScope)); Assert.That(owned.barrel, Is.EqualTo(HowToFishAttachment.Suppressor));
            view = world.Player.GetComponentInChildren<HowToFishEquipmentView>();
            Assert.That(view.RecoilMultiplier, Is.LessThan(.65f));
            var parts = view.GetComponentsInChildren<Transform>(true);
            Assert.That(parts.Single(part => part.name == "Attachment_RedDotSight").gameObject.activeSelf, Is.False);
            Assert.That(parts.Single(part => part.name == "Attachment_SniperScope").gameObject.activeSelf, Is.True);
            yield return Approach(HowToFishAttachment.Compensator, 2); money = world.Session.State.money; yield return PressKey(Key.E);
            Assert.That(world.Session.State.money, Is.EqualTo(money)); Assert.That(owned.barrel, Is.EqualTo(HowToFishAttachment.Suppressor));
            world.Player.Teleport(new Vector3(18, 2.4f, 8), 0); yield return new WaitForSeconds(.3f);
            yield return AimAt(world.Player, world.Player.Eye.transform.position + Vector3.forward * 20);
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftTrigger = 1 }); yield return new WaitForSeconds(.5f);
            Assert.That(world.Player.Eye.fieldOfView, Is.EqualTo(20).Within(.1f));
            var eyeRotation = world.Player.Eye.transform.rotation;
            world.SetPaused(true); yield return new WaitForSecondsRealtime(.2f);
            Assert.That(world.Player.Eye.transform.rotation, Is.EqualTo(eyeRotation)); Assert.That(world.Player.Eye.fieldOfView, Is.EqualTo(20).Within(.1f));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/PistolScope.png")); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; world.SetPaused(false);
            yield return new WaitForSeconds(.5f);
            Assert.That(world.Player.Eye.fieldOfView, Is.EqualTo(75).Within(.1f));
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/PistolAttachments.png")); yield return null; yield return null;
            world.Save(); world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "配件保存后未返回 Hub。");
            yield return EnterWorld();
            owned = testWorld.Session.State.inventory.Find(item => item.id == "Pistol");
            Assert.That(owned.sight, Is.EqualTo(HowToFishAttachment.SniperScope)); Assert.That(owned.barrel, Is.EqualTo(HowToFishAttachment.Suppressor));
            Assert.That(owned.hasExtendedMag && owned.hasLaser, Is.True);
            Assert.That(testWorld.Player.AmmoCapacity, Is.EqualTo(17)); Assert.That(testWorld.Player.Ammo, Is.EqualTo(16));
            Assert.That(testWorld.Session.State.money, Is.EqualTo(money));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator WeaponUpgrades_BuyAcrossIslandsAttackAndResume()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 4; world.Session.State.hasBoatKey = true; world.Session.State.money = 200000;
            foreach (string id in new[] { "BrassKnuckles", "Pistol" })
            {
                bool melee = id == "BrassKnuckles";
                yield return AimAtProduct(world, id, melee ? 0 : 1); yield return PressKey(Key.E);
                Assert.That(world.Session.Count(id), Is.EqualTo(1));
                for (int i = 0; world.Player.Equipment?.Id != id && i < 10; i++) yield return PressKey(Key.Q);
                Assert.That(world.Player.Equipment?.Id, Is.EqualTo(id));
                var definition = world.Catalog.FindItem(id);
                for (int island = melee ? 0 : 1; island <= 4; island++)
                {
                    var kind = melee ? HowToFishStationKind.Anvil : HowToFishStationKind.AmmoUpgrade;
                    var station = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                        .Single(value => value.Kind == kind && value.Island == island);
                    var shape = station.GetComponentsInChildren<Collider>().Single(value => value.name == (melee ? "AnvilWaist" : "AmmoCrate"));
                    world.Player.Teleport(station.transform.position + Vector3.back * 1.8f + Vector3.up * .1f, 0);
                    yield return new WaitForSeconds(.35f);
                    yield return AimAt(world.Player, shape.bounds.center);
                    Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(station));
                    for (int tier = 0; tier < 3; tier++)
                    {
                        int before = world.Session.State.money, level = world.Session.UpgradeLevel(id);
                        int cost = definition.NextUpgradeCost(level);
                        StringAssert.Contains("$" + cost, world.FocusText());
                        yield return PressKey(Key.E);
                        Assert.That(world.Session.State.money, Is.EqualTo(before - cost));
                        Assert.That(world.Session.UpgradeLevel(id), Is.EqualTo(level + 1));
                    }
                    int money = world.Session.State.money;
                    yield return PressKey(Key.E);
                    Assert.That(world.Session.State.money, Is.EqualTo(money), "区域上限或满级后不能再扣钱。");
                }
                Assert.That(world.Session.UpgradeLevel(id), Is.EqualTo(melee ? 15 : 12));
                world.Player.Teleport(new Vector3(18, 2.4f, 8), 0);
                yield return new WaitForSeconds(.2f);
                yield return AimAt(world.Player, world.Player.Eye.transform.position + Vector3.forward * 10);
                var fish = world.Spawn("Catfish", world.Player.Eye.transform.position + Vector3.forward, false);
                PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
                yield return new WaitForSeconds(.3f);
                float health = fish.Health;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return new WaitForSeconds(.12f);
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                Assert.That(fish.Health, Is.EqualTo(Mathf.Max(0, health - definition.DamageAtLevel(world.Session.UpgradeLevel(id)))).Within(.01f));
                world.Player.Drop(false);
            }
            int remaining = world.Session.State.money;
            world.Save();
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "升级后未返回 Hub。");
            yield return EnterWorld();
            Assert.That(testWorld.Session.UpgradeLevel("BrassKnuckles"), Is.EqualTo(15));
            Assert.That(testWorld.Session.UpgradeLevel("Pistol"), Is.EqualTo(12));
            Assert.That(testWorld.Session.State.money, Is.EqualTo(remaining));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator MiniBoss_PurchaseContactEscapeAndCorpseResume()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 1; world.Session.State.hasBoatKey = true; world.Session.State.money = 6000;
            yield return AimAtProduct(world, "BeginnerBossLure", 1); yield return PressKey(Key.E);
            Assert.That(world.Session.Count("BeginnerBossLure"), Is.EqualTo(1));
            var forest = world.Islands.Single(value => value.Index == 1);
            world.Player.Teleport(forest.Position + new Vector3(0, 2, -52), 0);
            yield return new WaitForSeconds(.3f);
            var sunfish = world.Spawn("Sunfish", world.Player.transform.position + Vector3.forward * 5, false);
            var behavior = sunfish.GetComponent<HowToFishJumpingFish>();
            world.SetPaused(true); float escape = behavior.EscapeFraction;
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(behavior.EscapeFraction, Is.EqualTo(escape));
            world.SetPaused(false);
            yield return WaitFor(() => sunfish.Body.linearVelocity.y > 4, "翻车鱼没有跃起。");
            float health = world.Session.State.health;
            var shape = sunfish.GetComponent<BoxCollider>();
            sunfish.Body.position = world.Player.transform.position + Vector3.up * .8f - shape.center;
            sunfish.Body.linearVelocity = Vector3.zero;
            yield return new WaitForSeconds(.2f);
            Assert.That(world.Session.State.health, Is.EqualTo(health), "翻车鱼不应使用攻击鱼的接触伤害。");
            sunfish.Hit(100000, Vector3.zero); yield return null;
            Assert.That(sunfish.IsConsumed, Is.False);
            Assert.That(world.Session.State.defeatedDripCreatures, Does.Contain("Sunfish"));
            Assert.That(world.Session.State.unlockedIsland, Is.EqualTo(1));
            var pike = world.Spawn("OldPike", world.Player.transform.position + Vector3.forward * 5, false);
            yield return WaitFor(() => pike.Body.linearVelocity.y > 4, "老狗鱼没有跃起。");
            shape = pike.GetComponent<BoxCollider>();
            pike.Body.position = world.Player.transform.position + Vector3.up * .8f - shape.center;
            pike.Body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.15f);
            Assert.That(world.Session.State.health, Is.EqualTo(health - 25).Within(.1f), "接触伤害应命中一次，不能每帧扣血。");
            pike.Hit(100000, Vector3.zero); yield return null;
            Assert.That(world.Session.State.defeatedDripCreatures, Does.Contain("OldPike"));
            Assert.That(world.Session.State.unlockedIsland, Is.EqualTo(1), "可选首领不能替代主线解锁。");
            Assert.That(pike.Heat(.5f), Is.True);
            string corpseId = pike.InstanceId;
            world.Session.State.unlockedIsland = 4;
            yield return AimAtProduct(world, "ScientificBossLure", 4); yield return PressKey(Key.E);
            Assert.That(world.Session.Count("ScientificBossLure"), Is.EqualTo(1));
            Assert.That(world.Session.State.money, Is.EqualTo(160));
            var goblin = world.Spawn("GoblinShark", world.Player.transform.position + Vector3.forward * 8, false);
            world.Save(); StringAssert.Contains("首领战斗", world.Notice);
            typeof(HowToFishJumpingFish).GetField("remaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(goblin.GetComponent<HowToFishJumpingFish>(), .01f);
            yield return new WaitForSeconds(.15f);
            Assert.That(goblin == null && world.ActiveBoss == null, Is.True);
            Assert.That(world.Session.State.defeatedCreatures, Does.Not.Contain("GoblinShark"));
            world.Save();
            Assert.That(world.InspectSlot(0).Data.worldItems.Any(value => value.instanceId == corpseId && value.health == 0 && value.isCooked), Is.True);
            world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "可选遭遇后未返回 Hub。");
            yield return EnterWorld();
            var restored = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.InstanceId == corpseId);
            Assert.That(restored.IsCooked && !restored.IsAlive, Is.True);
            Assert.That(testWorld.Session.State.defeatedDripCreatures, Does.Contain("OldPike"));
            Assert.That(testWorld.Session.State.money, Is.EqualTo(160));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator Volcano_QuestWhaleCraterEndingAndResume()
        {
            yield return StartNewGame();
            var world = testWorld;
            // 此测试隔离第五岛规则；资源与击杀准备不是完整新档通关证据。
            world.Session.State.unlockedIsland = 4; world.Session.State.hasBoatKey = true; world.Session.State.money = 24000;
            yield return AimAtProduct(world, "AssaultRifle", 4); yield return PressKey(Key.E);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("AssaultRifle"));
            int ammo = world.Player.Ammo;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.5f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            Assert.That(ammo - world.Player.Ammo, Is.GreaterThan(1));
            yield return PressKey(Key.R); yield return WaitFor(() => !world.Player.IsReloading, "步枪未完成换弹。");
            yield return AimAtProduct(world, "ScientificLure", 4); yield return PressKey(Key.E);
            Assert.That(world.Session.Count("ScientificLure"), Is.EqualTo(1));
            var pool = Enumerable.Range(0, 100).Select(index => world.Catalog.RollCatch(4, "ScientificLure", index / 100f, "FishingRod").Id).Distinct();
            Assert.That(pool, Is.EquivalentTo(new[] { "Blobfish", "Oarfish", "Anglerfish", "Stonefish", "SuperdwarfFish" }));
            var island = world.Islands.Single(value => value.Index == 4);
            var scientist = island.GetComponentsInChildren<HowToFishStation>().Single(value => value.Kind == HowToFishStationKind.Scientist);
            var departure = island.GetComponentsInChildren<HowToFishStation>().Single(value => value.Kind == HowToFishStationKind.MilitaryDeparture);
            IEnumerator ApproachScientist()
            {
                world.Player.Teleport(scientist.transform.position + Vector3.back * 2.3f, 0);
                yield return new WaitForSeconds(.5f);
                yield return AimAt(world.Player, scientist.GetComponent<Collider>().bounds.center);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(scientist));
            }
            IEnumerator ApproachDeparture()
            {
                var military = island.transform.Find("MilitaryBoat");
                world.Player.Teleport(military.TransformPoint(new Vector3(.7f, .35f, -.7f)), 0);
                yield return new WaitForSeconds(.5f);
                yield return AimAt(world.Player, departure.GetComponent<Collider>().bounds.center);
                Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(departure));
            }
            yield return ApproachDeparture(); yield return PressKey(Key.E);
            Assert.That(world.ShowEnding, Is.False, "无钥匙不能返航。");
            yield return ApproachScientist();
            for (int i = 0; i < 5; i++)
            {
                var fish = world.Spawn("Mackerel", world.Player.transform.position + Vector3.up, false);
                PlaceForPickup(world.Player, fish); Assert.That(world.Player.PickUp(fish), Is.True);
                if (i == 0)
                {
                    yield return PressKey(Key.E);
                    Assert.That(fish.IsConsumed, Is.False, "科学家不接受活鱼。");
                }
                fish.Hit(10000, Vector3.zero);
                yield return PressKey(Key.E);
                Assert.That(world.Session.State.volcanoFish, Is.EqualTo(i + 1));
                Assert.That(world.InspectSlot(0).Data.volcanoFish, Is.EqualTo(i + 1), "每次交付必须进入存档。");
            }
            Assert.That(world.Session.Count("FishBucket"), Is.Zero, "交满后需要主动领取鱼桶。");
            yield return PressKey(Key.E); yield return PressKey(Key.E);
            Assert.That(world.Session.Count("FishBucket"), Is.EqualTo(1), "重复交谈不能堆叠免费鱼桶。");
            Assert.That(world.Catalog.RollCatch(4, "FishBucket", 0, "FishingRod").Id, Is.EqualTo("BowheadWhale"));
            Assert.That(world.Catalog.RollCatch(3, "FishBucket", 0, "FishingRod"), Is.Null);
            world.Player.Teleport(island.Position + new Vector3(25, 5, -71), 0);
            var whale = world.Spawn("BowheadWhale", island.Position + new Vector3(0, 7, -71), false);
            yield return null;
            Assert.That(world.Player.PickUp(whale), Is.False, "活鲸不可直接搬走。");
            var behavior = whale.GetComponent<HowToFishWhale>();
            world.SetPaused(true); float escape = behavior.EscapeFraction;
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(behavior.EscapeFraction, Is.EqualTo(escape));
            world.SetPaused(false); world.Save(); StringAssert.Contains("首领战斗", world.Notice);
            yield return WaitFor(() => whale.Body.linearVelocity.y > 8, "鲸鱼未进入跃击阶段。");
            whale.Hit(100000, Vector3.zero); yield return null;
            Assert.That(whale.IsConsumed, Is.False, "普通鲸必须保留完整尸体用于下一阶段。");
            world.Save();
            Assert.That(world.InspectSlot(0).Data.worldItems.Any(item => item.definitionId == "BowheadWhale" && item.health == 0), Is.True);
            var crater = island.GetComponentInChildren<HowToFishVolcanoCrater>();
            whale.Body.position = crater.transform.position + Vector3.up * .25f; whale.Body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            yield return WaitFor(() => world.ActiveBoss?.Item.DefinitionId == "MutatedBowheadWhale", "完整鲸尸投入火山未开启第二阶段。");
            Assert.That(whale == null || whale.IsConsumed, Is.True);
            var mutated = world.ActiveBoss.Item;
            yield return WaitFor(() => UnityEngine.Object.FindObjectsByType<HowToFishProjectile>(FindObjectsSortMode.None).Length > 0, "变异鲸没有发射熔岩。");
            yield return WaitFor(() => UnityEngine.Object.FindObjectsByType<HowToFishDamagePool>(FindObjectsSortMode.None).Length > 0, "熔岩没有形成地面危险区。");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/VolcanoMutatedWhale.png"));
            yield return null; yield return null;
            mutated.Hit(100000, Vector3.zero);
            yield return WaitFor(() => !mutated.GetComponent<HowToFishBossTransition>().IsProtected, "变异鲸阶段保护没有结束。");
            mutated.Hit(100000, Vector3.zero);
            yield return null; yield return null;
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishProjectile>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishDamagePool>(FindObjectsSortMode.None), Is.Empty);
            var fin = UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Single(value => value.DefinitionId == "WhaleFin");
            yield return ApproachScientist();
            PlaceForPickup(world.Player, fin); Assert.That(world.Player.PickUp(fin), Is.True);
            yield return PressKey(Key.E);
            Assert.That(world.Session.State.hasMilitaryBoatKey, Is.True);
            Assert.That(world.Session.State.hasFinished, Is.False);
            Assert.That(world.InspectSlot(0).Data.hasMilitaryBoatKey, Is.True);
            yield return ApproachDeparture(); yield return PressKey(Key.E);
            Assert.That(world.ShowEnding && world.IsPaused, Is.True);
            Assert.That(world.InspectSlot(0).Data.hasFinished, Is.True, "结局出现前必须保存完成状态。");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/VolcanoEnding.png"));
            yield return null; yield return null;
            ObjectFind<Button>("Resume").onClick.Invoke();
            Assert.That(world.ShowEnding || world.IsPaused, Is.False);
            int money = world.Session.State.money;
            world.Save(); world.ReturnToHub();
            yield return WaitFor(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "结局后无法返回 Hub。");
            yield return EnterWorld();
            Assert.That(testWorld.Session.State.hasFinished && testWorld.Session.State.hasMilitaryBoatKey, Is.True);
            Assert.That(testWorld.Session.State.volcanoFish, Is.EqualTo(5));
            Assert.That(testWorld.Session.State.money, Is.EqualTo(money));
            Assert.That(testWorld.Player.Equipment.Id, Is.EqualTo("AssaultRifle"));
            Assert.That(testWorld.ShowEnding, Is.False, "继续存档不应强制再次播放结局。");
        }

        [UnityTest]
        public IEnumerator Volcano_EscapeClearsHazardsAndAllowsRetry()
        {
            yield return StartNewGame();
            var world = testWorld;
            world.Session.State.unlockedIsland = 4;
            world.Session.State.volcanoFish = 5;
            var island = world.Islands.Single(value => value.Index == 4);
            var scientist = island.GetComponentsInChildren<HowToFishStation>().Single(value => value.Kind == HowToFishStationKind.Scientist);
            world.Player.Teleport(scientist.transform.position + Vector3.back * 2.3f, 0);
            yield return new WaitForSeconds(.5f);
            yield return AimAt(world.Player, scientist.GetComponent<Collider>().bounds.center);
            yield return PressKey(Key.E);
            Assert.That(world.Session.Count("FishBucket"), Is.EqualTo(1));
            Assert.That(world.Session.TryConsume("FishBucket"), Is.True);
            var crater = island.GetComponentInChildren<HowToFishVolcanoCrater>();
            var cooked = world.Spawn("BowheadWhale", crater.transform.position, false);
            cooked.Hit(100000, Vector3.zero);
            Assert.That(cooked.Heat(.5f), Is.True);
            yield return new WaitForSeconds(.2f);
            Assert.That(cooked != null && !cooked.IsConsumed, Is.True, "熟鲸尸不能触发变异阶段。");
            Assert.That(world.ActiveBoss, Is.Null);
            var mutated = world.Spawn("MutatedBowheadWhale", crater.BossSpawnPosition, false);
            yield return WaitFor(() => UnityEngine.Object.FindObjectsByType<HowToFishProjectile>(FindObjectsSortMode.None).Length > 0, "变异鲸未发射熔岩。");
            // 加速逃脱计时，验证生产清理路径，不等待完整三分钟。
            typeof(HowToFishWhale).GetField("remaining", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mutated.GetComponent<HowToFishWhale>(), .01f);
            yield return new WaitForSeconds(.15f);
            Assert.That(mutated == null, Is.True);
            Assert.That(world.ActiveBoss, Is.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishProjectile>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishDamagePool>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Any(value => value.DefinitionId == "WhaleFin"), Is.False);
            yield return AimAt(world.Player, scientist.GetComponent<Collider>().bounds.center);
            yield return PressKey(Key.E); yield return PressKey(Key.E);
            Assert.That(world.Session.Count("FishBucket"), Is.EqualTo(1), "失败后可重领，但不能重复领取。");
            world.Save();
            Assert.That(world.InspectSlot(0).Data.inventory.Single(value => value.id == "FishBucket").count, Is.EqualTo(1));
        }

        [UnityTest, Timeout(720000)]
        public IEnumerator Volcano_ReelShootCarryWhaleAndDefeatMutation()
        {
            yield return StartNewGame();
            var world = testWorld;
            // 预置到达第五岛所需装备；钓获、射击、搬运和投掷均走实际输入和物理。
            world.Session.State.unlockedIsland = 4; world.Session.State.hasBoatKey = true;
            world.Session.GrantItem("FishingRod"); world.Session.GrantItem("AssaultRifle"); world.Session.GrantItem("FishBucket");
            // 来源数值更新后两阶段为20000/38000生命；使用火山可购买的满级步枪验证实战与搬运。
            world.Session.State.money = 100000;
            for (int upgrade = 0; upgrade < world.Catalog.FindItem("AssaultRifle").MaxUpgrade; upgrade++)
                Assert.That(world.Session.TryUpgrade("AssaultRifle", 4, out var upgradeReason), Is.True, upgradeReason);
            var island = world.Islands.Single(value => value.Index == 4);
            var crater = island.GetComponentInChildren<HowToFishVolcanoCrater>();
            world.Player.Teleport(island.Position + new Vector3(33, 2, -82), 180);
            yield return new WaitForSeconds(.5f);
            yield return PressKey(Key.B);
            Assert.That(world.Player.Fishing.SelectedBait, Is.EqualTo("FishBucket"));
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return new WaitForSeconds(.6f);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Waiting, "鱼桶未入海。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Bite, "鲸鱼没有咬钩。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
            yield return WaitFor(() => world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling, "鲸鱼未进入收线。");
            yield return PullBackCatch(world);
            Assert.That(world.Session.Count("FishBucket"), Is.Zero);
            var whale = world.ActiveBoss.Item;
            Assert.That(whale.DefinitionId, Is.EqualTo("BowheadWhale"));
            yield return PressKey(Key.Q);
            Assert.That(world.Player.Equipment.Id, Is.EqualTo("AssaultRifle"));
            bool died = false;
            void RecordDeath() => died = true;
            world.Player.Died += RecordDeath;
            var samples = new System.Collections.Generic.Queue<string>();
            IEnumerator Fight(HowToFishWorldItem target, Vector3 arena, float inner, float outer)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                float deadline = Time.realtimeSinceStartup + 330;
                while (target != null && target.IsAlive && !died && Time.realtimeSinceStartup < deadline)
                {
                    var radial = Vector3.ProjectOnPlane(world.Player.transform.position - arena, Vector3.up);
                    var tangent = Vector3.Cross(Vector3.up, radial).normalized;
                    var direction = (tangent + radial.normalized * (radial.magnitude > outer ? -2 : radial.magnitude < inner ? 2 : 0)).normalized;
                    var predicted = target.GetComponent<Collider>().bounds.center + (target.Body.linearVelocity - direction * 6.8f) * Time.deltaTime;
                    PointMouseAt(world.Player, predicted);
                    var forward = Vector3.ProjectOnPlane(predicted - world.Player.Eye.transform.position, Vector3.up).normalized;
                    var pad = new GamepadState { leftStick = new Vector2(Vector3.Dot(direction, Vector3.Cross(Vector3.up, forward)), Vector3.Dot(direction, forward)) };
                    if (world.Player.Ammo == 0) pad = pad.WithButton(GamepadButton.North);
                    else if (!world.Player.IsReloading) pad.rightTrigger = 1;
                    InputSystem.QueueStateEvent(gamepad, pad);
                    yield return null;
                    samples.Enqueue($"health={world.Session.State.health:F1}, target={target?.Health}, player={world.Player.transform.position}, ammo={world.Player.Ammo}");
                    if (samples.Count > 12) samples.Dequeue();
                }
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                Assert.That(died, Is.False, "鲸鱼实战中角色死亡。\n" + string.Join("\n", samples));
                Assert.That(target == null || !target.IsAlive, Is.True, "射击未完成击杀。\n" + string.Join("\n", samples));
            }
            IEnumerator WalkTo(Vector3 destination, float stopDistance = .8f)
            {
                float deadline = Time.realtimeSinceStartup + 35;
                float sampleAt = Time.realtimeSinceStartup + .75f;
                var samplePosition = world.Player.transform.position;
                while (Vector3.ProjectOnPlane(destination - world.Player.transform.position, Vector3.up).magnitude > stopDistance &&
                    !died && Time.realtimeSinceStartup < deadline)
                {
                    PointMouseAt(world.Player, new Vector3(destination.x, world.Player.Eye.transform.position.y, destination.z));
                    InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = Vector2.up });
                    yield return null;
                    if (Time.realtimeSinceStartup >= sampleAt)
                    {
                        // 木板入口与地形边缘可能高于自动迈步；使用玩家已有跳跃，不瞬移穿过障碍。
                        bool blocked = Vector3.ProjectOnPlane(world.Player.transform.position - samplePosition, Vector3.up).sqrMagnitude < .04f;
                        samplePosition = world.Player.transform.position;
                        sampleAt = Time.realtimeSinceStartup + .75f;
                        if (blocked) yield return PressKey(Key.Space);
                    }
                }
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                Assert.That(died, Is.False);
                Assert.That(Vector3.ProjectOnPlane(destination - world.Player.transform.position, Vector3.up).magnitude, Is.LessThanOrEqualTo(stopDistance + .15f),
                    $"搬运路线受阻：player={world.Player.transform.position}, target={destination}");
            }
            yield return Fight(whale, island.Position + new Vector3(32, 0, -62), 12, 18);
            Assert.That(world.Session.State.defeatedCreatures, Does.Contain("BowheadWhale"));
            yield return new WaitForSeconds(1);
            yield return WalkTo(whale.GetComponent<Collider>().ClosestPoint(world.Player.Eye.transform.position), 1f);
            yield return AimAt(world.Player, whale.GetComponent<Collider>().bounds.center);
            yield return PressKey(Key.E);
            Assert.That(world.Player.HeldItem, Is.SameAs(whale));
            yield return WalkTo(new Vector3(world.Player.transform.position.x, 0, island.Position.z - 79));
            yield return WalkTo(island.Position + new Vector3(-16, 0, -79));
            var ascent = island.transform.Find("WoodenAscent");
            for (int i = 0; i < ascent.childCount; i += 3)
            {
                yield return WalkTo(ascent.GetChild(i).position);
                Assert.That(whale.IsHeld, Is.True, "完整鲸尸在木板路上脱手。");
            }
            yield return WalkTo(ascent.GetChild(ascent.childCount - 1).position);
            yield return AimAt(world.Player, crater.transform.position + Vector3.up * 2.5f);
            yield return PressKey(Key.G);
            yield return WaitFor(() => world.ActiveBoss?.Item.DefinitionId == "MutatedBowheadWhale", "实际投掷鲸尸未触发变异阶段。");
            Assert.That(whale == null || whale.IsConsumed, Is.True);
            var mutated = world.ActiveBoss.Item;
            yield return Fight(mutated, island.Position, 18, 23);
            world.Player.Died -= RecordDeath;
            Assert.That(world.Session.State.defeatedCreatures, Does.Contain("MutatedBowheadWhale"));
            Assert.That(UnityEngine.Object.FindObjectsByType<HowToFishWorldItem>(FindObjectsSortMode.None).Count(value => value.DefinitionId == "WhaleFin"), Is.EqualTo(1));
            world.SetPaused(true);
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Library/HowToFish/Evidence/VolcanoRifleBattleResult.png"));
            yield return null; yield return null;
        }

        private IEnumerator PullBackCatch(HowToFishWorld world)
        {
            float deadline = Time.realtimeSinceStartup + 30;
            while (world.Player.Fishing.State.Phase == HowToFishFishingPhase.Reeling && Time.realtimeSinceStartup < deadline)
            {
                InputSystem.QueueStateEvent(gamepad, new GamepadState());
                yield return null; yield return null;
                InputSystem.QueueStateEvent(gamepad, new GamepadState { rightTrigger = 1 });
                yield return null; yield return null;
            }
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            Assert.That(world.Player.Fishing.State.Phase, Is.EqualTo(HowToFishFishingPhase.Landed));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            var world = UnityEngine.Object.FindAnyObjectByType<HowToFishWorld>();
            if (world != null)
            {
                world.ReturnToHub();
                float deadline = Time.realtimeSinceStartup + 20;
                while (world != null && Time.realtimeSinceStartup < deadline) yield return null;
            }
            Time.timeScale = 1;
            if (saveDirectory != null && Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }

        private IEnumerator StartNewGame()
        {
            saveDirectory = Path.Combine(Path.GetTempPath(), "HowToFishRuntimeTests", Guid.NewGuid().ToString("N"));
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            mouse = InputSystem.AddDevice<Mouse>();
            yield return EnterWorld();
        }

        private IEnumerator EnterWorld()
        {
            yield return EnterHub();
            yield return EnterHowToFish();
            yield return WaitFor(() => UnityEngine.Object.FindAnyObjectByType<HowToFishHudPresenter>() != null &&
                !GameSceneNavigator.Instance.IsTransitioning, "Demo HUD 未出现。");
            testWorld = UnityEngine.Object.FindAnyObjectByType<HowToFishWorld>();
            testWorld.Input.Asset.devices = new InputDevice[] { keyboard, gamepad, mouse };
            // 保留真实菜单和存档实现，仅把存档目录换成此次测试的临时目录。
            typeof(HowToFishWorld).GetField("saves", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(testWorld, new HowToFishSaveStore(saveDirectory));
            yield return null;
            ObjectFind<Button>("Slot0").onClick.Invoke();
            yield return WaitFor(() => testWorld.HasSession && !testWorld.IsPaused, "无法开始新航程。");
        }

        private IEnumerator EnterHowToFish()
        {
            var menu = UIManager.Instance.Get<MainMenuView>();
            var list = menu.gameObject.GetComponentInChildren<LoopScrollView>();
            Assert.That(list.TryGetIndex("how_to_fish", out int index), Is.True, "Hub 缺少渔力全开入口。");
            list.ScrollToCell(index, ScrollAlignment.End);
            yield return WaitFor(() => list.GetVisibleCell(index)?.Context.IsCurrent == true, "目标卡片未显示。");
            var button = list.GetVisibleCell(index).GetComponentInChildren<LoopScrollMenuButton>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return WaitFor(() => EventSystem.current.sendNavigationEvents && button.IsInteractable(),
                "Hub导航门闩尚未释放或入口仍不可交互。");
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            yield return null; yield return null;
            yield return PressKey(Key.Enter);
        }

        private IEnumerator PressKey(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null; yield return null;
        }

        private IEnumerator AimAtProduct(HowToFishWorld world, string id, int island = 0)
        {
            var station = UnityEngine.Object.FindObjectsByType<HowToFishStation>(FindObjectsSortMode.None)
                .Single(item => item.Kind == HowToFishStationKind.Product && item.ItemId == id && item.Island == island);
            var target = station.GetComponent<Collider>().bounds.center;
            var position = new Vector3(target.x, 2.4f, target.z - 2.2f);
            if (island != 0)
            {
                Assert.That(Physics.Raycast(new Vector3(position.x, target.y + 3, position.z), Vector3.down, out var ground, 20, ~0, QueryTriggerInteraction.Ignore), Is.True);
                position = ground.point;
            }
            world.Player.Teleport(position, 0);
            if (island != 0) yield return new WaitForSeconds(.3f);
            yield return AimAt(world.Player, target);
            if (world.Player.Focus?.GetComponentInParent<HowToFishStation>() != station)
            {
                var evidence = Path.GetFullPath("Library/HowToFish/Evidence"); Directory.CreateDirectory(evidence);
                ScreenCapture.CaptureScreenshot(Path.Combine(evidence, "ShopAim-" + id + ".png"));
                yield return null; yield return null;
            }
            Assert.That(world.Player.Focus?.GetComponentInParent<HowToFishStation>(), Is.SameAs(station),
                $"未瞄准 {id}：focus={world.Player.Focus?.name}, eye={world.Player.Eye.transform.position}, forward={world.Player.Eye.transform.forward}, target={target}");
        }

        private static void PlaceForPickup(HowToFishPlayer player, HowToFishWorldItem item)
        {
            item.Body.position = player.Eye.transform.position + player.Eye.transform.forward * 1.5f;
            item.Body.rotation = Quaternion.identity;
            item.Body.linearVelocity = Vector3.zero;
            item.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private IEnumerator AimAt(HowToFishPlayer player, Vector3 point) => AimAt(player, () => point);

        private IEnumerator AimAt(HowToFishPlayer player, Func<Vector3> point)
        {
            // 根据实际视线反馈调整鼠标，兼容传送后角色落地与船体轻微起伏。
            for (int attempt = 0; attempt < 5; attempt++)
            {
                if (PointMouseAt(player, point())) break;
                yield return null; yield return null;
            }
            yield return null;
        }

        private bool PointMouseAt(HowToFishPlayer player, Vector3 point)
        {
            var offset = point - player.Eye.transform.position;
            var forward = player.Eye.transform.forward;
            float yaw = Vector3.SignedAngle(Vector3.ProjectOnPlane(forward, Vector3.up), Vector3.ProjectOnPlane(offset, Vector3.up), Vector3.up);
            float pitch = Mathf.Atan2(offset.y, new Vector2(offset.x, offset.z).magnitude) * Mathf.Rad2Deg - Mathf.Asin(forward.y) * Mathf.Rad2Deg;
            if (Mathf.Abs(yaw) + Mathf.Abs(pitch) < .15f) return true;
            InputSystem.QueueDeltaStateEvent(mouse.delta, new Vector2(yaw, pitch) / player.MouseSensitivity);
            return false;
        }

        private static IEnumerator EnterHub()
        {
            // 与 BlockPortersFlowTests 一致：同轮 PlayMode 保留启动壳，只通过导航进出 Demo。
            if (GameSceneNavigator.Instance == null)
                yield return SceneManager.LoadSceneAsync("Assets/Scenes/AppEntrance.unity", LoadSceneMode.Single);
            yield return WaitFor(() => GameSceneNavigator.Instance?.CurrentScene == GameSceneId.Hub &&
                !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "Hub 未就绪。");
        }

        private static T ObjectFind<T>(string name) where T : Component =>
            UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None).FirstOrDefault(component => component.name == name && component.gameObject.activeInHierarchy);

        private static IEnumerator WaitFor(Func<bool> condition, string failure)
        {
            float deadline = Time.realtimeSinceStartup + 40;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, failure);
        }
    }
}
#endif
