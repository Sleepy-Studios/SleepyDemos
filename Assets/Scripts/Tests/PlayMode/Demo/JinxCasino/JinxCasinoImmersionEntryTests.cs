#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Adapters.Persistence;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 正式启动与保存样板的一台入口烟测；输入交给Core UI模块与Demo路由，不直接调用业务或监听器。
    public sealed class JinxCasinoImmersionEntryTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private InputSettings originalInputSettings;
        private InputSettings testInputSettings;
        private GameViewResolution resolution;
        private JinxCasinoController owner;
        private string saveDirectory;

        [UnityTest, Timeout(180000)]
        public IEnumerator SavedEntryStartsByRealInputFocusesSlotsAndRestoresCameraAfterBackAndPause()
        {
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                Assert.That(startup, Is.Not.Null); yield return Wait(() => startup.isDone, "唯一AppEntrance启动", 90);
            }
            yield return Wait(IsStableHub, "正式Hub稳定", 90);
            Assert.That(GameSceneNavigator.Instance.IsEditorDirect, Is.False);
            resolution = new GameViewResolution(1280, 720);
            yield return Wait(() => Screen.width == 1280 && Screen.height == 720, "720p实际GameView", 15);
            originalInputSettings = InputSystem.settings; testInputSettings = Object.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask();
            yield return Wait(() => travel.IsCompleted, "正式赌场导航", 45);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoImmersionHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning, "保存的沉浸HUD", 30);
            owner = Object.FindFirstObjectByType<JinxCasinoController>();
            Assert.That(owner, Is.Not.Null); Assert.That(owner.UsesImmersion, Is.True);
            yield return Wait(() => UIManager.Instance.Get<DlssSettingsView>()?.State == ViewState.Visible, "公共画质Widget已初始化", 5);
            Assert.That(GraphicsSettingsUI.IsEntrySuppressed, Is.True);
            Assert.That(UIManager.Instance.Get<DlssSettingsView>().transform.Find("OpenButton").gameObject.activeInHierarchy, Is.False);
            saveDirectory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TestSaves", "S1Entry-" + Guid.NewGuid().ToString("N")));
            ValidateSavePath();
            owner.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory));
            owner.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")));
            Assert.That(owner.gameObject.scene.path, Is.EqualTo("Assets/LoadResources/Demos/jinx_casino/Scenes/Immersion.unity"));
            var hud = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            var presenter = hud.gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            Assert.That(presenter, Is.Not.Null); Assert.That(UIManager.Instance.Get<JinxCasinoHudView>(), Is.Null);
            var body = Field<CharacterController>(owner, "body"); var camera = Field<Camera>(owner, "worldCamera");
            Assert.That(UIRootManager.Instance.BaseCamera, Is.SameAs(camera)); AssertSingleListener();
            Assert.That(EventSystem.current.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            yield return Wait(() => hud.gameObject.GetComponentsInParent<CanvasGroup>(true).All(group => group.alpha >= .99f), "Core入场淡出结束", 2);
            var start = Field<Button>(presenter, "start"); var resume = Field<Button>(presenter, "resume");
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == start.gameObject && EventSystem.current.sendNavigationEvents, "首次菜单焦点及Core导航", 3);
            Assert.That(owner.HasAdventure, Is.False);
            yield return Screenshot("S1MainMenu");
            yield return MouseClick(start);
            yield return Wait(() => owner.HasAdventure && !owner.IsAdventureInputBlocked, "实际开始按钮进入探索", 3);
            Assert.That(owner.AdventureState.StageIndex, Is.Zero); Assert.That(owner.AdventureState.Coins, Is.EqualTo(1000));
            Assert.That(owner.IsImmersionPaused, Is.False); Assert.That(EventSystem.current.sendNavigationEvents, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            var counter = Object.FindFirstObjectByType<JinxCasinoShopCounter>();
            Assert.That(counter, Is.Not.Null);
            yield return MoveUntil(Key.A, () => body.transform.position.x <= counter.InteractionPosition.x + .15f);
            Vector3 beforeShop = camera.transform.position; Quaternion beforeShopRotation = camera.transform.rotation;
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.HasShopFocus && Vector3.Distance(camera.transform.position, counter.FocusPose.position) < .01f,
                "真实E进入补给柜台", 3);
            yield return Screenshot("S1SupplyCounter");
            yield return ClickTarget(camera, counter.Targets.Single(value => value.TargetId == "s1.supply.product0"));
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(1000), "选实物不扣款。");
            yield return ClickTarget(camera, counter.Targets.Single(value => value.TargetId == "s1.supply.action0"));
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(900));
            Assert.That(owner.AdventureState.Inventory.Single(value => value.ItemId == "duo_wrench").Count, Is.EqualTo(1));
            yield return ClickTarget(camera, counter.Targets.Single(value => value.TargetId == "s1.supply.action1"));
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(900));
            Assert.That(owner.AdventureState.Inventory.Any(value => value.ItemId == "duo_wrench"), Is.False);
            Assert.That(owner.AdventureState.CooperationHelpCharges, Is.EqualTo(1));
            yield return KeyPress(Key.Escape);
            yield return Wait(() => !owner.HasShopFocus && Cursor.lockState == CursorLockMode.Locked, "离开柜台恢复探索", 3);
            Assert.That(Vector3.Distance(camera.transform.position, beforeShop), Is.LessThan(.04f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, beforeShopRotation), Is.LessThan(.1f));
            yield return MoveUntil(Key.D, () => body.transform.position.x >= -.1f);
            var station = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None).Single(value => value.Game == Hotfix.JinxCasino.Rules.CasinoGameKind.Slots);
            Assert.That(station.HasTableInteraction, Is.True);
            // 保存样板朝向+Z；沿正交通道用实际W/A或D接近，不直接搬角色/调用Interact。
            Assert.That(Quaternion.Angle(body.transform.rotation, Quaternion.identity), Is.LessThan(.1f));
            var position = body.transform.position;
            yield return MoveUntil(Key.W, () => body.transform.position.z >= station.InteractionPosition.z - .12f);
            Key horizontal = station.InteractionPosition.x < body.transform.position.x ? Key.A : Key.D;
            yield return MoveUntil(horizontal, () => Mathf.Abs(body.transform.position.x - station.InteractionPosition.x) <= .2f);
            Assert.That(Vector3.Distance(position, body.transform.position), Is.GreaterThan(1));
            Assert.That(owner.GetNearbyLocalSocialStation(), Is.SameAs(station));
            Vector3 explorationPosition = camera.transform.position; Quaternion explorationRotation = camera.transform.rotation; float fieldOfView = camera.fieldOfView;
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.TableView?.StationId == station.StationId && Mathf.Abs(camera.fieldOfView - station.FocusFieldOfView) < .01f, "E进入具体水果机桌面", 3);
            Assert.That(Vector3.Distance(camera.transform.position, station.FocusPose.position), Is.LessThan(.02f));
            Assert.That(owner.TableView.DraftStake, Is.Zero, "探索E不能跨上下文变成加筹码或确认。");
            yield return Screenshot("S1SlotsFocus");
            var rulesLabel = typeof(JinxCasinoS1Presentation).GetField("rulesText", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(station.GetComponent<JinxCasinoS1Presentation>());
            Assert.That(rulesLabel, Is.Not.Null, "投入前必须有本机台完整规则铭牌。");
            Assert.That(rulesLabel.GetType().GetProperty("text").GetValue(rulesLabel), Is.EqualTo(owner.TableView.RulesText));
            Assert.That((bool)rulesLabel.GetType().GetProperty("isTextTruncated").GetValue(rulesLabel), Is.False,
                "收益规则和当前加成不得在机台铭牌中被裁掉。");
            long initialCoins = owner.AdventureState.Coins;
            yield return ClickTarget(camera, station, "chip10");
            yield return Wait(() => owner.TableView.DraftStake == 10, "真实筹码物件增加10筹码", 2);
            yield return ClickTarget(camera, station, "commit");
            yield return Wait(() => owner.TableView.IsSlotsPrepared, "真实确认物件准备本次投入", 2);
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(initialCoins));
            Assert.That(owner.AdventureState.SettledRoundSequence, Is.Zero, "确认仅准备，拉柄前不能开奖扣款。");
            yield return ClickTarget(camera, station, "primary");
            var visual = station.GetComponent<JinxCasinoS1SlotsPresentation>(); Assert.That(visual, Is.Not.Null);
            yield return Wait(() => owner.AdventureState.SettledRoundSequence == 1 && visual.IsAnimating, "真实拉柄提交且开始机台演出", 3);
            yield return Wait(() => !visual.IsAnimating, "拉轮停稳及出币演出完成", 5);
            Assert.That(owner.AdventureState.SettledRoundSequence, Is.EqualTo(1));
            Assert.That(owner.AdventureState.LastStationId, Is.EqualTo(station.StationId));
            Assert.That(owner.AdventureState.LastRoundCost, Is.EqualTo(10));
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(initialCoins - 10 + owner.AdventureState.LastRoundPayout));
            // 桌面光标可见时，暂停按钮中心必须可由真实指针点击，不能被公共入口覆盖。
            yield return MouseClick(Field<Button>(presenter, "pause"));
            yield return Wait(() => owner.IsImmersionPaused, "真实桌面暂停按钮", 3);
            yield return MouseClick(resume);
            yield return Wait(() => !owner.IsImmersionPaused, "桌面显式继续", 3);
            yield return KeyPress(Key.Escape);
            yield return Wait(() => owner.TableView == null && Mathf.Abs(camera.fieldOfView - fieldOfView) < .01f && Cursor.lockState == CursorLockMode.Locked,
                "Esc离桌完成过渡并恢复探索输入", 3);
            Assert.That(Vector3.Distance(camera.transform.position, explorationPosition), Is.LessThan(.04f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, explorationRotation), Is.LessThan(.1f));
            yield return KeyPress(Key.Escape);
            yield return Wait(() => owner.IsImmersionPaused, "探索Esc暂停", 3);
            yield return Wait(() => Field<GameObject>(presenter, "pauseMenu").activeInHierarchy && EventSystem.current.currentSelectedGameObject == resume.gameObject,
                "暂停菜单及继续焦点", 3);
            Vector3 pausedPosition = camera.transform.position; Quaternion pausedRotation = camera.transform.rotation;
            int remaining = owner.AdventureState.RemainingMilliseconds;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(camera.transform.position, Is.EqualTo(pausedPosition)); Assert.That(camera.transform.rotation, Is.EqualTo(pausedRotation));
            Assert.That(owner.AdventureState.RemainingMilliseconds, Is.EqualTo(remaining));
            yield return Screenshot("S1Paused");
            yield return MouseClick(resume);
            yield return Wait(() => !owner.IsImmersionPaused && !owner.IsAdventureInputBlocked, "真实继续按钮显式恢复", 3);
            Assert.That(Vector3.Distance(camera.transform.position, pausedPosition), Is.LessThan(.04f));
            Assert.That(Mathf.Abs(camera.fieldOfView - fieldOfView), Is.LessThan(.01f));
            yield return KeyPress(Key.Escape);
            yield return Wait(() => Field<Button>(presenter, "leave").gameObject.activeInHierarchy, "保存的暂停返回按钮", 3);
            yield return MouseClick(Field<Button>(presenter, "leave"));
            yield return Wait(() => owner == null && IsStableHub(), "实际返回按钮卸载样板并回Hub", 45);
            Assert.That(UIManager.Instance.Get<JinxCasinoImmersionHudView>(), Is.Null); Assert.That(hud.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(GraphicsSettingsUI.IsEntrySuppressed, Is.False);
            Assert.That(UIManager.Instance.Get<DlssSettingsView>().transform.Find("OpenButton").gameObject.activeInHierarchy, Is.True);
            AssertSingleListener();
        }

        private IEnumerator KeyPress(Key key)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null; }
        private IEnumerator MoveUntil(Key key, Func<bool> arrived)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return Wait(arrived, "实际" + key + "通道移动", 8);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        private IEnumerator MouseClick(Button button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True);
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)); point = new Vector2(Mathf.Round(point.x), Mathf.Round(point.y));
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty); Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), "真实保存按钮中心射线不得被覆盖。");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
        }
        private IEnumerator ClickTarget(Camera camera, JinxCasinoStation station, string suffix)
        {
            var target = station.Targets.Single(value => value.TargetId == station.StationId + "." + suffix);
            yield return ClickTarget(camera, target);
        }
        private IEnumerator ClickTarget(Camera camera, JinxCasinoTableTarget target)
        {
            yield return Wait(() => target.IsAvailable, "实体目标可操作：" + target.TargetId, 3);
            Physics.SyncTransforms(); var collider = target.GetComponent<Collider>(); Assert.That(collider, Is.Not.Null);
            Vector3 screen = camera.WorldToScreenPoint(collider.bounds.center);
            Assert.That(screen.z, Is.GreaterThan(0)); Assert.That(screen.x, Is.InRange(0, Screen.width)); Assert.That(screen.y, Is.InRange(0, Screen.height));
            var point = new Vector2(screen.x, screen.y);
            Assert.That(Physics.Raycast(camera.ScreenPointToRay(point), out var hit, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.collider.GetComponentInParent<JinxCasinoTableTarget>(), Is.SameAs(target),
                "真实相机射线不得穿透桌体或其它目标。首个命中：" + hit.collider.name + "，位置：" + hit.point);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                if (mouse != null && mouse.added) InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
                var live = Object.FindFirstObjectByType<JinxCasinoController>();
                if (live != null) { live.RequestExit(); yield return Wait(() => live == null && IsStableHub(), "失败路径清理样板", 45); }
            }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
                if (testInputSettings != null) Object.Destroy(testInputSettings);
                resolution?.Dispose(); resolution = null;
                if (Object.FindFirstObjectByType<JinxCasinoController>() == null && !string.IsNullOrEmpty(saveDirectory))
                { ValidateSavePath(); if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true); }
            }
        }
        private void ValidateSavePath()
        {
            string allowed = Path.GetFullPath("Library/JinxCasino/TestSaves") + Path.DirectorySeparatorChar;
            Assert.That(Path.GetFullPath(saveDirectory).StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(saveDirectory).StartsWith("S1Entry-", StringComparison.Ordinal), Is.True);
        }
        private static IEnumerator Screenshot(string name)
        {
            yield return null; yield return null; yield return null;
            string directory = Path.GetFullPath("Library/JinxCasino/Verification"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N") + ".png");
            int width = Screen.width, height = Screen.height;
            ScreenCapture.CaptureScreenshot(path); yield return null; yield return null;
            yield return Wait(() => File.Exists(path) && new FileInfo(path).Length > 24, "样板截图落盘", 10);
            byte[] bytes = File.ReadAllBytes(path);
            Assert.That(bytes[0], Is.EqualTo(137)); Assert.That(bytes[1], Is.EqualTo((byte)'P'));
            Assert.That(bytes[16] << 24 | bytes[17] << 16 | bytes[18] << 8 | bytes[19], Is.EqualTo(width));
            Assert.That(bytes[20] << 24 | bytes[21] << 16 | bytes[22] << 8 | bytes[23], Is.EqualTo(height));
            Debug.Log("[JinxCasinoImmersionEntryTests] " + width + "x" + height + " 保存样板截图：" + path);
        }
        private static T Field<T>(object value, string name)
        { var field = value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(field, Is.Not.Null); return (T)field.GetValue(value); }
        private static bool IsStableHub() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        private static void AssertSingleListener() => Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
        private static IEnumerator Wait(Func<bool> predicate, string reason, float timeout)
        { double deadline = Time.realtimeSinceStartupAsDouble + timeout; while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null; Assert.That(predicate(), Is.True, reason + "超时"); }
    }
}
#endif
