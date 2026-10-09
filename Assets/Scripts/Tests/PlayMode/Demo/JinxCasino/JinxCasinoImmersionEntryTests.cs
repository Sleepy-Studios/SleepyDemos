using Hotfix.JinxCasino;
using Hotfix.JinxCasino.Interaction;
using Hotfix.JinxCasino.Presentation;
using Hotfix.JinxCasino.UI;
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
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Rules;
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
using SleepyStudios.LoopScroll;

namespace Tests.Demo
{
    /// 正式启动与保存样板的一台入口回归；UI用实际InputSystem，音效另验已提交请求，不能代替交互验收。
    public sealed class JinxCasinoImmersionEntryTests
    {
        private AudioClip testAudioClip;
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private Touchscreen touchscreen;
        private InputSettings originalInputSettings;
        private InputSettings testInputSettings;
        private GameViewResolution resolution;
        private JinxCasinoController owner;
        private string saveDirectory;
        private string settingsTestKey;
        private bool standaloneOverride;
        private int touchSequence;

        [UnityTest, Timeout(180000)]
        public IEnumerator SavedEntryStartsByRealInputFocusesSlotsAndRestoresCameraAfterBackAndPause()
        {
            yield return EnterSample(enterWithHeldGamepad: true);
            var hud = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            var presenter = hud;
            var body = owner.Player.Body; var camera = owner.Player.Camera;
            var start = Field<Button>(presenter, "start"); Button resume;
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == start.gameObject && EventSystem.current.sendNavigationEvents, "首次菜单焦点及Core导航", 3);
            Assert.That(owner.Game.HasAdventure, Is.False);
            yield return Screenshot("S1MainMenu");
            yield return MouseClick(start);
            yield return Wait(() => owner.Game.HasAdventure && !owner.Player.IsMenuOpen, "实际开始按钮进入探索", 3);
            Assert.That(owner.Game.State.StageIndex, Is.Zero); Assert.That(owner.Game.State.Coins, Is.EqualTo(1000));
            Assert.That(owner.Player.IsPaused, Is.False); Assert.That(EventSystem.current.sendNavigationEvents, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            touchscreen = InputSystem.AddDevice<Touchscreen>();
            var touchPosition = new Vector2(Screen.width * .5f, Screen.height * .75f);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = touchPosition, phase = UnityEngine.InputSystem.TouchPhase.Began });
            yield return Wait(() => owner.Player.DeviceKind == Core.Runtime.Inputs.InputDeviceKind.Touch, "触屏实际按下切换提示", 3);
            Assert.That(Field<Core.Runtime.Inputs.TouchInputPad>(presenter, "movePad").gameObject.activeInHierarchy, Is.True);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = 1, position = touchPosition, phase = UnityEngine.InputSystem.TouchPhase.Ended }); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.01f, 0) }); yield return null; yield return null;
            Assert.That(owner.Player.DeviceKind, Is.EqualTo(Core.Runtime.Inputs.InputDeviceKind.Touch), "手柄漂移不能抢走触控提示。");
            InputSystem.QueueStateEvent(gamepad, new GamepadState { leftStick = new Vector2(.4f, 0) });
            yield return Wait(() => owner.Player.DeviceKind == Core.Runtime.Inputs.InputDeviceKind.Gamepad, "真实摇杆切换手柄", 3);
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            Assert.That(Field<Core.Runtime.Inputs.TouchInputPad>(presenter, "movePad").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<Core.Runtime.Inputs.TouchInputPad>(presenter, "lookPad").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<Button>(presenter, "interact").gameObject.activeInHierarchy, Is.False);
            yield return Screenshot("S1GamepadExplorationHud");
            yield return KeyPress(Key.W);
            Assert.That(owner.Player.DeviceKind, Is.EqualTo(Core.Runtime.Inputs.InputDeviceKind.KeyboardMouse));
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            var counter = Object.FindFirstObjectByType<JinxCasinoShopCounter>();
            Assert.That(counter, Is.Not.Null);
            yield return MoveUntil(Key.A, () => body.transform.position.x <= counter.InteractionPosition.x + .15f);
            Vector3 beforeShop = camera.transform.position; Quaternion beforeShopRotation = camera.transform.rotation;
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.Player.HasShopFocus && Vector3.Distance(camera.transform.position, counter.FocusPose.position) < .01f,
                "真实E进入补给柜台", 3);
            yield return Screenshot("S1SupplyCounter");
            yield return ClickTarget(camera, counter.Targets.Single(value => value.TargetId == "s1.supply.product0"));
            Assert.That(owner.Game.State.Coins, Is.EqualTo(1000), "选实物不扣款。");
            yield return ClickTarget(camera, counter.Targets.Single(value => value.TargetId == "s1.supply.action0"));
            Assert.That(owner.Game.State.Coins, Is.EqualTo(900));
            Assert.That(owner.Game.State.Inventory.Single(value => value.ItemId == "duo_wrench").Count, Is.EqualTo(1));
            yield return ClickTarget(camera, counter.Targets.Single(value => value.TargetId == "s1.supply.action1"));
            Assert.That(owner.Game.State.Coins, Is.EqualTo(900));
            Assert.That(owner.Game.State.Inventory.Any(value => value.ItemId == "duo_wrench"), Is.False);
            Assert.That(owner.Game.State.CooperationHelpCharges, Is.EqualTo(1));
            yield return KeyPress(Key.Escape);
            yield return Wait(() => !owner.Player.HasShopFocus && Cursor.lockState == CursorLockMode.Locked, "离开柜台恢复探索", 3);
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
            Assert.That(owner.Player.FindNearbyStation(), Is.SameAs(station));
            Vector3 explorationPosition = camera.transform.position; Quaternion explorationRotation = camera.transform.rotation; float fieldOfView = camera.fieldOfView;
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.Player.TableView?.StationId == station.StationId && Mathf.Abs(camera.fieldOfView - station.FocusFieldOfView) < .01f, "E进入具体水果机桌面", 3);
            Assert.That(Vector3.Distance(camera.transform.position, station.FocusPose.position), Is.LessThan(.02f));
            Assert.That(owner.Player.TableView.DraftStake, Is.Zero, "探索E不能跨上下文变成加筹码或确认。");
            yield return Screenshot("S1SlotsFocus");
            var rulesLabel = typeof(JinxCasinoS1Presentation).GetField("rulesText", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(station.GetComponent<JinxCasinoS1Presentation>());
            Assert.That(rulesLabel, Is.Not.Null, "投入前必须有本机台完整规则铭牌。");
            Assert.That(rulesLabel.GetType().GetProperty("text").GetValue(rulesLabel), Is.EqualTo(owner.Player.TableView.RulesText));
            Assert.That((bool)rulesLabel.GetType().GetProperty("isTextTruncated").GetValue(rulesLabel), Is.False,
                "收益规则和当前加成不得在机台铭牌中被裁掉。");
            long initialCoins = owner.Game.State.Coins;
            yield return ClickTarget(camera, station, "chip10");
            yield return Wait(() => owner.Player.TableView.DraftStake == 10, "真实筹码物件增加10筹码", 2);
            yield return ClickTarget(camera, station, "commit");
            yield return Wait(() => owner.Player.TableView.IsSlotsPrepared, "真实确认物件准备本次投入", 2);
            Assert.That(owner.Game.State.Coins, Is.EqualTo(initialCoins));
            Assert.That(owner.Game.State.SettledRoundSequence, Is.Zero, "确认仅准备，拉柄前不能开奖扣款。");
            yield return ClickTarget(camera, station, "primary");
            var visual = station.GetComponent<JinxCasinoS1SlotsPresentation>(); Assert.That(visual, Is.Not.Null);
            yield return Wait(() => owner.Game.State.SettledRoundSequence == 1 && visual.IsAnimating, "真实拉柄提交且开始机台演出", 3);
            yield return Wait(() => !visual.IsAnimating, "拉轮停稳及出币演出完成", 5);
            Assert.That(owner.Game.State.SettledRoundSequence, Is.EqualTo(1));
            Assert.That(owner.Game.State.LastStationId, Is.EqualTo(station.StationId));
            Assert.That(owner.Game.State.LastRoundCost, Is.EqualTo(10));
            Assert.That(owner.Game.State.Coins, Is.EqualTo(initialCoins - 10 + owner.Game.State.LastRoundPayout));
            // 桌面光标可见时，暂停按钮中心必须可由真实指针点击，不能被公共入口覆盖。
            yield return MouseClick(Field<Button>(presenter, "pause"));
            yield return Wait(() => owner.Player.IsPaused, "真实桌面暂停按钮", 3);
            resume = Field<Button>(presenter, "resume");
            yield return MouseClick(resume);
            yield return Wait(() => !owner.Player.IsPaused, "桌面显式继续", 3);
            yield return KeyPress(Key.Escape);
            yield return Wait(() => owner.Player.TableView == null && Mathf.Abs(camera.fieldOfView - fieldOfView) < .01f && Cursor.lockState == CursorLockMode.Locked,
                "Esc离桌完成过渡并恢复探索输入", 3);
            Assert.That(Vector3.Distance(camera.transform.position, explorationPosition), Is.LessThan(.04f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, explorationRotation), Is.LessThan(.1f));
            yield return KeyPress(Key.Escape);
            yield return Wait(() => owner.Player.IsPaused, "探索Esc暂停", 3);
            resume = Field<Button>(presenter, "resume");
            yield return Wait(() => Field<GameObject>(presenter, "pauseMenu").activeInHierarchy && EventSystem.current.currentSelectedGameObject == resume.gameObject,
                "暂停菜单及继续焦点", 3);
            Vector3 pausedPosition = camera.transform.position; Quaternion pausedRotation = camera.transform.rotation;
            int remaining = owner.Game.State.RemainingMilliseconds;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(camera.transform.position, Is.EqualTo(pausedPosition)); Assert.That(camera.transform.rotation, Is.EqualTo(pausedRotation));
            Assert.That(owner.Game.State.RemainingMilliseconds, Is.EqualTo(remaining));
            yield return Screenshot("S1Paused");
            resume = Field<Button>(presenter, "resume");
            yield return MouseClick(resume);
            yield return Wait(() => !owner.Player.IsPaused && !owner.Player.IsMenuOpen, "真实继续按钮显式恢复", 3);
            Assert.That(Vector3.Distance(camera.transform.position, pausedPosition), Is.LessThan(.04f));
            Assert.That(Mathf.Abs(camera.fieldOfView - fieldOfView), Is.LessThan(.01f));
            yield return KeyPress(Key.Escape);
            yield return Wait(() => Field<Button>(presenter, "leave").gameObject.activeInHierarchy, "保存的暂停返回按钮", 3);
            yield return MouseClick(Field<Button>(presenter, "leave"));
            yield return Wait(() => owner == null && IsStableHub(), "实际返回按钮卸载样板并回Hub", 45);
            Assert.That(UIManager.Instance.Get<JinxCasinoImmersionHudView>(), Is.Null); Assert.That(hud.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(UIManager.Instance.Get<DlssSettingsView>(), Is.Null, "返回Hub不自动显示画面设置。");
            AssertSingleListener();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator SavedTutorialAdvancesOnlyThroughRealMovementObjectsAndSettlements()
        {
            yield return EnterSample(touchEntry: true);
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            var body = owner.Player.Body; var camera = owner.Player.Camera;
            yield return TouchTap(Field<Button>(presenter, "tutorialStartButton"));
            yield return Wait(() => owner.Game.HasAdventure && !owner.Player.IsMenuOpen && Cursor.lockState == CursorLockMode.None,
                "真实教学入口进入探索", 3);
            Assert.That(owner.Game.State.Teaching.Step, Is.EqualTo(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Look));
            Assert.That(owner.Game.State.Mode, Is.EqualTo(Hotfix.JinxCasino.Rules.CasinoAdventureMode.Practice));
            Assert.That(owner.Game.State.Config.MaximumStake, Is.EqualTo(10));
            string tutorialRun = owner.Game.State.RunId;
            yield return TouchTap(Field<Button>(presenter, "pause"));
            yield return Wait(() => owner.Player.IsPaused, "教学暂停", 3);
            yield return TouchTap(Field<Button>(presenter, "tutorialRetryButton"));
            yield return Wait(() => Field<Button>(presenter, "tutorialCancelButton").gameObject.activeInHierarchy, "重玩确认", 3);
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            yield return Wait(() => Field<GameObject>(presenter, "pauseMenu").activeInHierarchy, "手柄Menu取消确认后返回暂停", 3);
            Assert.That(owner.Player.IsPaused, Is.True, "关闭确认不能悄悄恢复时钟。");
            Assert.That(owner.Game.State.RunId, Is.EqualTo(tutorialRun));
            yield return TouchTap(Field<Button>(presenter, "resume"));
            yield return Wait(() => !owner.Player.IsPaused && !owner.Player.IsMenuOpen, "显式继续教学", 3);
            yield return null; yield return null;
            for (int i = 0; i < 5 && owner.Game.State.Teaching.Step == Hotfix.JinxCasino.Rules.CasinoTutorialStep.Look; i++)
            {
                yield return TouchLook(Field<Core.Runtime.Inputs.TouchInputPad>(presenter, "lookPad"), Vector2.up * (Screen.height * .2f));
            }
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Walk);
            var stations = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None);
            var fruit = stations.Single(value => value.StationId == "s1.fruit");
            var cards = stations.Single(value => value.StationId == "s1.cards");
            var levers = stations.Single(value => value.StationId == "s1.sync");
            var movePad = Field<Core.Runtime.Inputs.TouchInputPad>(presenter, "movePad");
            yield return TouchMoveUntil(movePad, Vector2.up, () => body.transform.position.z >= fruit.InteractionPosition.z - .12f);
            yield return TouchMoveUntil(movePad, Vector2.left, () => body.transform.position.x <= fruit.InteractionPosition.x + .15f);
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.EnterSlots);
            yield return TouchTap(Field<Button>(presenter, "interact")); yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.AddChips);
            yield return Wait(() => owner.Player.IsFocusReady, "触屏水果机聚焦完成", 3);
            AssertDeskLabelsVisible(camera, fruit);
            yield return TouchTarget(camera, fruit, "chip10"); yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Confirm);
            yield return TouchTarget(camera, fruit, "commit"); yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.SlotsResult);
            Assert.That(owner.Game.State.Coins, Is.EqualTo(1000));
            yield return TouchTarget(camera, fruit, "primary");
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.LeaveSlots);
            Assert.That(fruit.GetComponent<JinxCasinoS1SlotsPresentation>().IsAnimating, Is.False);
            Assert.That(owner.Game.State.SettledRoundSequence, Is.EqualTo(1));
            yield return Screenshot("S1TutorialFruitResult");
            yield return TouchTap(Field<Button>(presenter, "exitTable")); yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Blackjack);
            Assert.That(owner.Player.HasFocus && movePad.gameObject.activeInHierarchy, Is.False, "离桌过渡期间不能提前开放摇杆。");
            yield return TouchMoveUntil(movePad, Vector2.right, () => body.transform.position.x >= -.10f);
            yield return TouchMoveUntil(movePad, Vector2.up, () => body.transform.position.z >= cards.InteractionPosition.z - .12f);
            yield return TouchTap(Field<Button>(presenter, "interact"));
            yield return Wait(() => owner.Player.TableView?.StationId == cards.StationId && Vector3.Distance(camera.transform.position, cards.FocusPose.position) < .001f, "教学聚焦二十一点", 3);
            AssertDeskLabelsVisible(camera, cards);
            yield return TouchTarget(camera, cards, "chip10"); yield return TouchTarget(camera, cards, "commit");
            if (owner.Game.HasActiveRound) yield return TouchTarget(camera, cards, "secondary");
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.BuyWrench);
            Assert.That(cards.GetComponent<JinxCasinoS1BlackjackPresentation>().IsAnimating, Is.False);
            yield return TouchTap(Field<Button>(presenter, "exitTable"));
            yield return Wait(() => owner.Player.TableView == null && movePad.gameObject.activeInHierarchy, "牌桌恢复触屏探索", 3);
            yield return TouchMoveUntil(movePad, Vector2.down, () => body.transform.position.z <= -4.6f);
            var counter = Object.FindFirstObjectByType<JinxCasinoShopCounter>();
            yield return TouchMoveUntil(movePad, Vector2.left, () => body.transform.position.x <= counter.InteractionPosition.x + .15f);
            yield return TouchTap(Field<Button>(presenter, "interact"));
            yield return Wait(() => owner.Player.HasShopFocus && Vector3.Distance(camera.transform.position, counter.FocusPose.position) < .01f,
                "教学柜台聚焦完成", 3);
            yield return TouchTarget(camera, counter.Targets.Single(target => target.TargetId == "s1.supply.action0"));
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.UseWrench);
            yield return TouchTarget(camera, counter.Targets.Single(target => target.TargetId == "s1.supply.action1"));
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Levers);
            Assert.That(owner.Game.State.CooperationHelpCharges, Is.EqualTo(1));
            yield return TouchTap(Field<Button>(presenter, "exitTable"));
            yield return Wait(() => !owner.Player.HasShopFocus && movePad.gameObject.activeInHierarchy, "柜台恢复触屏探索", 3);
            yield return TouchMoveUntil(movePad, Vector2.right, () => body.transform.position.x >= -.10f);
            yield return TouchMoveUntil(movePad, Vector2.up, () => body.transform.position.z >= levers.InteractionPosition.z - .12f);
            yield return TouchMoveUntil(movePad, Vector2.right, () => body.transform.position.x >= levers.InteractionPosition.x - .15f);
            yield return TouchTap(Field<Button>(presenter, "interact"));
            yield return Wait(() => owner.Player.TableView?.StationId == levers.StationId && Vector3.Distance(camera.transform.position, levers.FocusPose.position) < .001f, "教学聚焦合拍台", 3);
            AssertDeskLabelsVisible(camera, levers);
            yield return TouchTarget(camera, levers, "chip10"); yield return TouchTarget(camera, levers, "commit");
            yield return Wait(() => owner.Player.TableView.LeverWindowOpen, "实际绿灯窗口", 4);
            yield return TouchTarget(camera, levers, "primary");
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Ready);
            Assert.That(owner.Game.State.SettledRoundSequence, Is.EqualTo(3));
            yield return Screenshot("S1TutorialReady");
            yield return TouchTap(Field<Button>(presenter, "exitTable"));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoTutorialView>()?.State == ViewState.Visible && Field<Button>(presenter, "tutorialCompleteButton").gameObject.activeInHierarchy, "明确完成教学按钮", 3);
            yield return TouchTap(Field<Button>(presenter, "tutorialReadyBackButton"));
            yield return Wait(() => !owner.Player.IsMenuOpen, "稍后完成仍可继续练习", 3);
            yield return TouchTap(Field<Button>(presenter, "pause"));
            yield return Wait(() => owner.Player.IsPaused && UIManager.Instance.Get<JinxCasinoPauseView>()?.State == ViewState.Visible && Field<Button>(presenter, "tutorialReviewButton").gameObject.activeInHierarchy,
                "暂停提供重新打开教学结果入口", 3);
            yield return TouchTap(Field<Button>(presenter, "tutorialReviewButton"));
            yield return TouchTap(Field<Button>(presenter, "tutorialCompleteButton"));
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Completed);
            long coins = owner.Game.State.Coins; uint random = owner.Game.State.RandomState;
            yield return TouchTap(Field<Button>(presenter, "tutorialContinueButton"));
            yield return Wait(() => !owner.Player.IsMenuOpen, "明确继续当前练习", 3);
            Assert.That(owner.Game.State.Coins, Is.EqualTo(coins)); Assert.That(owner.Game.State.RandomState, Is.EqualTo(random));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator SavedSlotsSaveCancelAndLoadThroughActualMenusWithoutRepeatingPayment()
        {
            yield return EnterSample();
            var hud = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            var presenter = hud;
            yield return MouseClick(Field<Button>(presenter, "saveMainLoadButton"));
            Assert.That(Field<Button>(presenter, "saveSlot1Button").interactable, Is.False);
            Assert.That(Field<Button>(presenter, "saveSlot2Button").interactable, Is.False);
            Assert.That(Field<Button>(presenter, "saveSlot3Button").interactable, Is.False);
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == Field<Button>(presenter, "saveBackButton").gameObject,
                "空槽列表的手柄焦点落在返回", 3);
            yield return PadPress(GamepadButton.South);
            Assert.That(owner.Game.HasAdventure, Is.False);
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return Wait(() => owner.Game.HasAdventure && !owner.Player.IsMenuOpen, "开始用于存档回归的正式冒险", 3);
            string run = owner.Game.State.RunId;
            yield return KeyPress(Key.Escape);
            yield return Wait(() => owner.Player.IsPaused, "保存前暂停", 3);
            yield return MouseClick(Field<Button>(presenter, "savePauseSaveButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            Assert.That(owner.Game.SelectedSaveSlot, Is.EqualTo(1));
            Assert.That(owner.Game.GetSaveSlotInfo(1).Coins, Is.EqualTo(1000));
            string firstBytes = File.ReadAllText(Path.Combine(saveDirectory, "save-1.json"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            yield return MouseClick(Field<Button>(presenter, "saveCancelButton"));
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "save-1.json")), Is.EqualTo(firstBytes), "取消覆盖不写入原件。");
            Assert.That(owner.Player.IsPaused, Is.True);
            yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            yield return MouseClick(Field<Button>(presenter, "resume"));
            yield return Wait(() => !owner.Player.IsPaused && Cursor.lockState == CursorLockMode.Locked, "真实继续后探索", 3);
            var body = owner.Player.Body; var camera = owner.Player.Camera;
            var fruit = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None).Single(station => station.StationId == "s1.fruit");
            yield return MoveUntil(Key.W, () => body.transform.position.z >= fruit.InteractionPosition.z - .12f);
            yield return MoveUntil(Key.A, () => body.transform.position.x <= fruit.InteractionPosition.x + .15f);
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.Player.TableView?.StationId == fruit.StationId && Vector3.Distance(camera.transform.position, fruit.FocusPose.position) < .001f,
                "具体水果机聚焦完成", 3);
            yield return ClickTarget(camera, fruit, "chip10"); yield return ClickTarget(camera, fruit, "commit"); yield return ClickTarget(camera, fruit, "primary");
            yield return Wait(() => owner.Game.State.SettledRoundSequence == 1 && !owner.Player.IsTableAnimating, "实际一次水果机结算", 6);
            long paidCoins = owner.Game.State.Coins; uint paidRandom = owner.Game.State.RandomState;
            yield return MouseClick(Field<Button>(presenter, "pause"));
            yield return MouseClick(Field<Button>(presenter, "savePauseSaveButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot2Button"));
            Assert.That(owner.Game.SelectedSaveSlot, Is.EqualTo(2));
            Assert.That(owner.Game.GetSaveSlotInfo(2).Coins, Is.EqualTo(paidCoins));
            yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            yield return MouseClick(Field<Button>(presenter, "savePauseLoadButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            yield return MouseClick(Field<Button>(presenter, "saveCancelButton"));
            Assert.That(owner.Game.State.Coins, Is.EqualTo(paidCoins));
            Assert.That(owner.Game.State.RandomState, Is.EqualTo(paidRandom));
            Assert.That(owner.Game.SelectedSaveSlot, Is.EqualTo(2)); Assert.That(owner.Player.IsPaused, Is.True);
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            yield return MouseClick(Field<Button>(presenter, "saveConfirmButton"));
            yield return Wait(() => !owner.Player.IsPaused && !owner.Player.IsMenuOpen, "确认读取后显式继续", 3);
            Assert.That(owner.Game.State.Coins, Is.EqualTo(1000)); Assert.That(owner.Game.State.SettledRoundSequence, Is.Zero);
            Assert.That(owner.Game.State.RunId, Is.EqualTo(run)); Assert.That(owner.Game.SelectedSaveSlot, Is.EqualTo(1));
            yield return KeyPress(Key.Escape);
            yield return MouseClick(Field<Button>(presenter, "savePauseSaveButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot3Button"));
            Assert.That(owner.Game.SelectedSaveSlot, Is.EqualTo(3));
            yield return Screenshot("S1ThreeSaveSlots");
            yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            yield return MouseClick(Field<Button>(presenter, "leave"));
            yield return Wait(() => owner == null && IsStableHub(), "返回Hub保留独立三槽", 45);
            // 从新实例主菜单继续同一独立目录，输入设备和真实持久化文件保持不变。
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask();
            yield return Wait(() => travel.IsCompleted, "再次进入赌场", 45);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoImmersionHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning, "主菜单重新出现", 30);
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoMainMenuView>()?.State == ViewState.Visible, "独立主菜单", 10);
            owner = Object.FindFirstObjectByType<JinxCasinoController>();
            owner.Game.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory)); owner.Game.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")));
            hud = UIManager.Instance.Get<JinxCasinoImmersionHudView>(); presenter = hud;
            yield return Wait(() => hud.gameObject.GetComponentsInParent<CanvasGroup>(true).All(group => group.alpha >= .99f), "重新入场淡出结束", 3);
            yield return MouseClick(Field<Button>(presenter, "saveMainLoadButton"));
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == Field<Button>(presenter, "saveSlot1Button").gameObject,
                "有存档时选择首个可用槽", 3);
            yield return PadPress(GamepadButton.DpadDown);
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == Field<Button>(presenter, "saveSlot2Button").gameObject,
                "手柄方向选择第二槽", 3);
            yield return PadPress(GamepadButton.South);
            yield return Wait(() => owner.Game.HasAdventure && !owner.Player.IsMenuOpen, "主菜单读取已结算局", 3);
            Assert.That(owner.Game.State.Coins, Is.EqualTo(paidCoins)); Assert.That(owner.Game.State.RandomState, Is.EqualTo(paidRandom));
            Assert.That(owner.Game.State.SettledRoundSequence, Is.EqualTo(1)); Assert.That(owner.Game.State.LastStationId, Is.EqualTo("s1.fruit"));
            Assert.That(owner.Game.SelectedSaveSlot, Is.EqualTo(2)); Assert.That(owner.Game.HasActiveRound, Is.False);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator StandaloneReturnReloadsGameMenuWithoutShowingHub()
        {
            yield return EnterSample();
            // Editor夹具只覆盖包启动方式；真正StartupScene解析仍由Player冷启动另行验收。
            typeof(GameSceneNavigator).GetProperty("StandaloneScene").SetValue(GameSceneNavigator.Instance, GameSceneId.JinxCasino);
            standaloneOverride = true;
            var initial = owner; owner.RequestExit();
            yield return Wait(() => initial == null && UIManager.Instance.Get<JinxCasinoImmersionHudView>()?.State == ViewState.Visible && UIManager.Instance.Get<JinxCasinoMainMenuView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning,
                "模拟独立包主菜单", 45);
            owner = Object.FindFirstObjectByType<JinxCasinoController>();
            owner.Game.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory)); owner.Game.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")));
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            Assert.That(owner.IsStandalonePlayer, Is.True);
            Assert.That(Field<Button>(presenter, "quitGameButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(owner.Game.HasAdventure, Is.False);
            yield return Screenshot("S1StandaloneMainMenu");
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return KeyPress(Key.Escape);
            var old = owner;
            yield return MouseClick(Field<Button>(presenter, "leave"));
            yield return Wait(() => old == null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.JinxCasino &&
                !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<JinxCasinoImmersionHudView>()?.State == ViewState.Visible && UIManager.Instance.Get<JinxCasinoMainMenuView>()?.State == ViewState.Visible,
                "独立包返回新的游戏主菜单", 45);
            owner = Object.FindFirstObjectByType<JinxCasinoController>(); Assert.That(owner.Game.HasAdventure, Is.False);
            Assert.That(UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, Is.False);
            AssertSingleListener();
            typeof(GameSceneNavigator).GetProperty("StandaloneScene").SetValue(GameSceneNavigator.Instance, null); standaloneOverride = false;
            owner.RequestExit(); yield return Wait(IsStableHub, "恢复Editor Hub夹具", 45);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SettingsPreviewSaveAndCancelUseRealControlsAndKeepPause()
        {
            yield return EnterSample();
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            JinxCasinoSettingsView local;
            settingsTestKey = "JinxCasino.SettingsEntry." + Guid.NewGuid().ToString("N");
            var preferences = new JinxCasinoLocalSettings(settingsTestKey);
            preferences.Save(new CasinoLocalPreferences { PcLookMultiplier = 1.2f, Volume = .4f });
            owner.Settings.Load(settingsTestKey);
            Assert.That(owner.Player.InputSettings.MouseLookMultiplier, Is.EqualTo(1.2f));
            var audio = owner.GetComponent<JinxCasinoAudioDirector>(); Assert.That(audio, Is.Not.Null);
            Assert.That(audio.Volume, Is.EqualTo(.4f));
            Assert.That(Field<AudioSource>(audio, "music").clip, Is.Not.Null);
            gamepad = InputSystem.AddDevice<Gamepad>(); InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            yield return MouseClick(Field<Button>(presenter, "settingsMainButton"));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoSettingsView>()?.State == ViewState.Visible, "独立设置页", 5);
            local = UIManager.Instance.Get<JinxCasinoSettingsView>();
            Assert.That(local, Is.Not.Null);
            var pc = Field<Slider>(local, "pcSensitivity");
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == pc.gameObject, "设置默认焦点", 3);
            yield return PadPress(GamepadButton.DpadRight);
            Assert.That(owner.Settings.Value.PcLookMultiplier, Is.GreaterThan(1.2f));
            Assert.That(LocalDataManager.LoadData(settingsTestKey, new CasinoLocalPreferences(), CasinoLocalPreferences.Validate).PcLookMultiplier, Is.EqualTo(1.2f), "预览不写盘");
            yield return PadPress(GamepadButton.East);
            Assert.That(UIManager.Instance.Get<JinxCasinoSettingsView>(), Is.Null);
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == Field<Button>(presenter, "settingsMainButton").gameObject, "设置取消恢复入口焦点", 3);
            Assert.That(owner.Settings.Value.PcLookMultiplier, Is.EqualTo(1.2f));
            GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.OpenSettings));
            GlobalData.Dispatch(new JinxCasinoUiAction(owner, JinxCasinoUiCommand.CloseSettings));
            yield return WaitWindowNavigation();
            Assert.That(UIManager.Instance.Get<JinxCasinoMainMenuView>()?.State, Is.EqualTo(ViewState.Visible), "连续开关保留底页");
            yield return MouseClick(Field<Button>(presenter, "settingsMainButton"));
            local = UIManager.Instance.Get<JinxCasinoSettingsView>();
            var closing = UIManager.Instance.CloseAsync(local, false).AsTask();
            yield return Wait(() => closing.IsCompleted, "直接关闭设置", 5);
            Assert.That(closing.Result.Status, Is.EqualTo(UIOperationStatus.Succeeded));
            yield return WaitWindowNavigation();
            Assert.That(owner.Data.SettingsOpen, Is.False, "直接关闭同步撤销页面请求");
            yield return MouseClick(Field<Button>(presenter, "settingsMainButton"));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoSettingsView>()?.State == ViewState.Visible, "独立设置页", 5);
            local = UIManager.Instance.Get<JinxCasinoSettingsView>();
            Assert.That(local, Is.Not.Null);
            yield return MouseClick(Field<Button>(local, "gamepadTabButton"));
            var padLook = Field<Slider>(local, "gamepadLookMultiplier");
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == padLook.gameObject, "手柄页可操作焦点", 3);
            yield return PadPress(GamepadButton.DpadRight);
            float chosen = owner.Settings.Value.GamepadLookMultiplier;
            Assert.That(chosen, Is.GreaterThan(1)); Assert.That(owner.Player.InputSettings.GamepadLookMultiplier, Is.EqualTo(chosen));
            yield return MouseClick(Field<Toggle>(local, "gamepadInvertY"));
            yield return MouseClick(Field<Button>(local, "saveButton"));
            Assert.That(LocalDataManager.LoadData(settingsTestKey, new CasinoLocalPreferences(), CasinoLocalPreferences.Validate).GamepadLookMultiplier, Is.EqualTo(chosen)); Assert.That(LocalDataManager.LoadData(settingsTestKey, new CasinoLocalPreferences(), CasinoLocalPreferences.Validate).GamepadInvertY, Is.True);
            yield return Screenshot("S1GamepadSettings");
            yield return PadPress(GamepadButton.East);
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return Wait(() => owner.Player.IsExplorationInputReady, "新局探索输入释放", 5);
            yield return KeyPress(Key.Escape);
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoPauseView>()?.State == ViewState.Visible, "独立暂停页面", 5);
            long time = owner.Game.State.RemainingMilliseconds;
            yield return MouseClick(Field<Button>(presenter, "settingsPauseButton"));
            local = UIManager.Instance.Get<JinxCasinoSettingsView>();
            yield return MouseClick(Field<Button>(local, "audioTabButton"));
            yield return MouseClick(Field<Toggle>(local, "muted")); Assert.That(owner.Settings.Value.Muted, Is.True);
            Assert.That(Field<AudioSource>(audio, "music").mute && Field<AudioSource>(audio, "sfx").mute, Is.True);
            yield return Screenshot("S1AudioSettings");
            yield return PadPress(GamepadButton.Start);
            Assert.That(owner.Player.IsPaused, Is.True); Assert.That(owner.Settings.Value.Muted, Is.False);
            Assert.That(Field<AudioSource>(audio, "music").mute || Field<AudioSource>(audio, "sfx").mute, Is.False);
            Assert.That(owner.Game.State.RemainingMilliseconds, Is.EqualTo(time));
            Assert.That(UIManager.Instance.Get<JinxCasinoSettingsView>(), Is.Null);
            owner.Settings.Load(settingsTestKey);
            Assert.That(owner.Player.InputSettings.GamepadLookMultiplier, Is.EqualTo(chosen)); Assert.That(owner.Player.InputSettings.GamepadInvertY, Is.True);
            yield return MouseClick(Field<Button>(presenter, "leave"));
            yield return Wait(() => owner == null && IsStableHub(), "设置退出后正常返回Hub", 45);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator IndependentWindows_CancelConfirmPreserveRunSlotsAndReleaseAtHub()
        {
            yield return EnterSample();
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            gamepad = InputSystem.AddDevice<Gamepad>(); InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            yield return MouseClick(Field<Button>(presenter, "saveMainLoadButton"));
            Assert.That(Field<Button>(presenter, "saveSlot1Button").interactable, Is.False);
            yield return PadPress(GamepadButton.East);
            Assert.That(UIManager.Instance.Get<JinxCasinoSaveView>(), Is.Null);
            Assert.That(owner.Game.HasAdventure, Is.False);
            yield return MouseClick(Field<Button>(presenter, "tutorialStartButton"));
            yield return Wait(() => owner.Player.IsExplorationInputReady, "教学探索输入", 5);
            string run = owner.Game.State.RunId;
            yield return KeyPress(Key.Escape);
            yield return MouseClick(Field<Button>(presenter, "tutorialRetryButton"));
            Assert.That(UIManager.Instance.Get<JinxCasinoTutorialView>()?.State, Is.EqualTo(ViewState.Visible));
            yield return Screenshot("SplitTutorialConfirm");
            yield return PadPress(GamepadButton.East);
            Assert.That(UIManager.Instance.Get<JinxCasinoTutorialView>(), Is.Null);
            Assert.That(owner.Player.IsPaused, Is.True); Assert.That(owner.Game.State.RunId, Is.EqualTo(run));
            yield return MouseClick(Field<Button>(presenter, "savePauseSaveButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            string original = File.ReadAllText(Path.Combine(saveDirectory, "save-1.json"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            yield return PadPress(GamepadButton.East);
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "save-1.json")), Is.EqualTo(original));
            Assert.That(owner.Game.State.RunId, Is.EqualTo(run));
            yield return Screenshot("SplitSaveSlots");
            yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            yield return MouseClick(Field<Button>(presenter, "tutorialSkipButton"));
            yield return MouseClick(Field<Button>(presenter, "tutorialStandardButton"));
            yield return PadPress(GamepadButton.East);
            Assert.That(owner.Game.State.RunId, Is.EqualTo(run));
            yield return MouseClick(Field<Button>(presenter, "tutorialStandardButton"));
            yield return MouseClick(Field<Button>(presenter, "tutorialConfirmButton"));
            yield return Wait(() => owner.Player.IsExplorationInputReady, "明确开始标准局", 5);
            Assert.That(owner.Game.State.RunId, Is.Not.EqualTo(run));
            // 结局数据使用领域命令夹具，页面操作仍走真实指针/手柄，不替代现场离场流程验收。
            Assert.That(owner.Game.ChooseEnding(CasinoAdventureEnding.Withdraw, Time.frameCount).Success, Is.True);
            yield return WaitWindowNavigation();
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoEndingView>()?.State == ViewState.Visible, "独立结局页", 5);
            yield return Screenshot("SplitEnding");
            yield return MouseClick(Field<Button>(presenter, "standardEndingSaveButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot2Button"));
            Assert.That(new CasinoLocalSaveStore(saveDirectory).Load(2).State.Phase, Is.EqualTo(CasinoAdventurePhase.Ended));
            yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            yield return MouseClick(Field<Button>(presenter, "standardEndingReturnButton"));
            yield return Wait(() => owner == null && IsStableHub(), "关闭全部赌场页面返回Hub", 45);
            Assert.That(UIManager.Instance.Get<JinxCasinoImmersionHudView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<JinxCasinoMainMenuView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<JinxCasinoPauseView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<JinxCasinoSaveView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<JinxCasinoTutorialView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<JinxCasinoEndingView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<JinxCasinoSettingsView>(), Is.Null);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator UnsupportedSavesLeaveCurrentRunCameraAndFilesUntouched()
        {
            yield return EnterSample();
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return KeyPress(Key.Escape);
            string run = owner.Game.State.RunId;
            long coins = owner.Game.State.Coins; uint random = owner.Game.State.RandomState;
            var camera = owner.Player.Camera; Vector3 position = camera.transform.position;
            var store = new CasinoLocalSaveStore(saveDirectory);
            // 文件领域有效，但缺少已装配区域、单人模式或准确的原机台身份。
            for (int sample = 0; sample < 6; sample++)
            {
                var config = Field<JinxCasinoGameSettings>(owner, "gameSettings").CreateConfig();
                if (sample == 0) { config.StageCount = 4; config.Targets = new long[] { 1200, 2000, 3500, 5000 }; }
                if (sample == 3) { config.AllowedGames = new[] { CasinoGameKind.HighLow }; config.InitiallyAvailableGames = config.AllowedGames; }
                var mode = sample == 0 ? CasinoAdventureMode.Standard : sample == 1 ? CasinoAdventureMode.Endless : CasinoAdventureMode.Practice;
                var fixture = CasinoAdventureSession.Start(7, mode, sample == 2 ? 2 : 1, config);
                if (sample >= 3)
                {
                    Assert.That(fixture.BeginGame("restore-fixture", sample == 3 ? CasinoGameKind.HighLow : CasinoGameKind.CooperativeLevers,
                        10, 0, sample == 3 ? "absent.cards" : sample == 4 ? "absent.levers" : null).Success, Is.True);
                    Assert.That(fixture.HasActiveRound, Is.True);
                }
                store.Save(1, fixture);
                var files = Directory.GetFiles(saveDirectory, "*", SearchOption.TopDirectoryOnly).ToDictionary(path => path, File.ReadAllBytes);
                yield return MouseClick(Field<Button>(presenter, "savePauseLoadButton"));
                yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
                yield return MouseClick(Field<Button>(presenter, "saveConfirmButton"));
                Assert.That(owner.Game.Status, Does.Contain("原存档和当前旅程均已保留"), "兼容样例 " + sample);
                Assert.That(owner.Game.State.RunId, Is.EqualTo(run)); Assert.That(owner.Game.State.Coins, Is.EqualTo(coins));
                Assert.That(owner.Game.State.RandomState, Is.EqualTo(random)); Assert.That(owner.Game.SelectedSaveSlot, Is.Zero);
                Assert.That(owner.Player.IsPaused, Is.True); Assert.That(camera.transform.position, Is.EqualTo(position));
                foreach (var file in files) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key));
                yield return MouseClick(Field<Button>(presenter, "saveCancelButton"));
                yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            }
            yield return MouseClick(Field<Button>(presenter, "leave"));
            yield return Wait(() => owner == null && IsStableHub(), "兼容拒绝后正常退出", 45);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator StandardWinUsesVisibleVerifierAndDepartureBeforeRecordingOneEnding()
        {
            yield return EnterSample();
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return Wait(() => owner.Game.HasAdventure && !owner.Player.IsMenuOpen, "标准局进入探索", 3);
            var body = owner.Player.Body; var camera = owner.Player.Camera;
            yield return MoveUntil(Key.D, () => body.transform.position.x >= 2.1f);
            Assert.That(owner.Player.Exit.IsNearby, Is.False, "背对入口不能交互。");
            yield return LookYaw(180);
            yield return Wait(() => owner.Player.Exit.IsNearby, "可见验票物件", 3);
            yield return KeyPress(Key.E);
            Assert.That(owner.Game.State.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            Assert.That(owner.Game.State.Coins, Is.EqualTo(1000));
            Assert.That(owner.Player.Exit.Feedback, Does.Contain("未达到"));
            yield return LookYaw(0);
            var levers = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None).Single(station => station.StationId == "s1.sync");
            yield return MoveUntil(Key.W, () => body.transform.position.z >= levers.InteractionPosition.z - .12f);
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.Player.TableView?.StationId == levers.StationId && Vector3.Distance(camera.transform.position, levers.FocusPose.position) < .001f,
                "标准局聚焦合拍台", 3);
            var leverPresentation = levers.GetComponentInChildren<JinxCasinoS1LeversPresentation>(true);
            var assistantArm = Field<Transform>(leverPresentation, "assistantArm");
            var assistantPalm = Field<Transform>(leverPresentation, "assistantPalm");
            var assistantGrip = Field<Transform>(leverPresentation, "assistantGrip");
            Assert.That(assistantArm, Is.Not.Null, "合拍台需要真实可见助手。");
            Quaternion waitingArm = assistantArm.localRotation;
            yield return ClickTarget(camera, levers, "chip100"); yield return ClickTarget(camera, levers, "commit");
            yield return Screenshot("S1LeverAssistantWaiting");
            Assert.That(owner.Player.TableView.HasOwnActiveRound, Is.True, "实体确认后必须建立已投入局：" + owner.Player.TableFeedback);
            yield return Wait(() => owner.Player.TableView.LeverWindowOpen, "实际合拍绿灯", 4);
            yield return ClickTarget(camera, levers, "primary");
            yield return Wait(() => owner.Game.State.SettledRoundSequence == 1, "真实合拍结算", 6);
            Assert.That(owner.Game.State.Coins, Is.GreaterThanOrEqualTo(owner.Game.Target));
            long earned = owner.Game.State.Coins;
            yield return Wait(() => Quaternion.Angle(waitingArm, assistantArm.localRotation) > 5, "助手跟随真实NPC拉杆", 2);
            yield return PadPress(GamepadButton.Start);
            Assert.That(owner.Player.IsPaused, Is.True);
            Quaternion pausedArm = assistantArm.localRotation;
            Vector3 pausedPalm = assistantPalm.position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(assistantArm.localRotation, Is.EqualTo(pausedArm));
            Assert.That(assistantPalm.position, Is.EqualTo(pausedPalm));
            yield return PadPress(GamepadButton.Start);
            yield return Wait(() => !owner.Player.IsPaused, "助手与机台明确继续", 2);
            leverPresentation.Restore(owner.Player.TableView);
            Assert.That(Vector3.Distance(assistantPalm.position, assistantGrip.position), Is.LessThan(.025f), "恢复握点仍贴合已拉下的杆。");
            Assert.That(owner.Game.State.Coins, Is.EqualTo(earned), "恢复助手表现不能再次发奖。");
            yield return Screenshot("S1LeverAssistantCompleted");
            yield return KeyPress(Key.Escape);
            yield return Wait(() => Cursor.lockState == CursorLockMode.Locked, "离桌恢复探索", 3);
            yield return MoveUntil(Key.S, () => body.transform.position.z <= -4.6f);
            yield return LookYaw(180); yield return Wait(() => owner.Player.Exit.IsNearby, "再次接近验票器", 3);
            yield return Screenshot("S1QuotaVerifier");
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.Game.State.Phase == CasinoAdventurePhase.Finale, "实体核验达标", 3);
            Assert.That(owner.Game.State.Coins, Is.EqualTo(earned), "额度只作阈值，不能再次扣款。");
            Assert.That(owner.Game.ProfileData.FinishedRuns, Is.Zero, "验票不等于已选择结局。");
            yield return MoveUntil(Key.D, () => body.transform.position.x <= -2.1f);
            yield return Wait(() => owner.Player.Exit.IsNearby, "离场口可见", 3);
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.Player.Exit.HasEnding && Field<GameObject>(presenter, "standardEndingPanel").activeInHierarchy, "明确领取离场券后展示结局", 3);
            Assert.That(owner.Game.State.Ending, Is.EqualTo(CasinoAdventureEnding.LeaveWithDignity));
            Assert.That(owner.Game.State.Coins, Is.EqualTo(earned));
            Assert.That(owner.Game.ProfileData.FinishedRuns, Is.EqualTo(1)); Assert.That(owner.Game.ProfileData.DignifiedExits, Is.EqualTo(1));
            yield return Screenshot("S1DignifiedEnding");
            yield return KeyPress(Key.Escape);
            Assert.That(Field<GameObject>(presenter, "standardEndingPanel").activeInHierarchy, Is.True);
            Assert.That(owner.Game.ProfileData.FinishedRuns, Is.EqualTo(1));
            yield return MouseClick(Field<Button>(presenter, "standardEndingReturnButton"));
            yield return Wait(() => owner == null && IsStableHub(), "结局按钮返回Hub", 45);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator WithdrawalRequiresVisibleRepeatedIntentAndEndingCanBeSaved()
        {
            yield return EnterSample();
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return Wait(() => owner.Game.HasAdventure && !owner.Player.IsMenuOpen, "标准局进入探索", 3);
            var body = owner.Player.Body;
            var terminal = Object.FindObjectsByType<JinxCasinoExitTerminal>(FindObjectsSortMode.None).Single(value => value.Action == JinxCasinoExitAction.Leave);
            yield return MoveUntil(Key.A, () => body.transform.position.x <= -2.1f);
            yield return LookYaw(180); yield return Wait(() => owner.Player.Exit.IsNearby, "离场口进入视野", 3);
            yield return KeyPress(Key.E);
            Assert.That(owner.Player.Exit.IsWithdrawalArmed(terminal), Is.True);
            Assert.That(owner.Game.State.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            yield return KeyPress(Key.Escape); yield return Wait(() => owner.Player.IsPaused, "确认意图期间暂停", 3);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(owner.Player.Exit.IsWithdrawalArmed(terminal), Is.True);
            yield return MouseClick(Field<Button>(presenter, "resume"));
            yield return Wait(() => !owner.Player.IsPaused && !owner.Player.IsMenuOpen, "显式继续", 3);
            yield return MoveUntil(Key.A, () => body.transform.position.x >= -.1f);
            yield return Wait(() => !owner.Player.Exit.IsWithdrawalArmed(terminal), "走远清除撤离意图", 3);
            yield return MoveUntil(Key.D, () => body.transform.position.x <= -2.1f);
            yield return KeyPress(Key.E);
            Assert.That(owner.Game.State.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.Player.Exit.HasEnding && Field<GameObject>(presenter, "standardEndingPanel").activeInHierarchy, "再次确认才结束", 3);
            Assert.That(owner.Game.State.Ending, Is.EqualTo(CasinoAdventureEnding.Withdraw));
            Assert.That(owner.Game.State.Coins, Is.EqualTo(1000)); Assert.That(owner.Game.ProfileData.Withdrawals, Is.EqualTo(1));
            yield return MouseClick(Field<Button>(presenter, "standardEndingSaveButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            var saved = new CasinoLocalSaveStore(saveDirectory).Load(1).State;
            Assert.That(saved.Ending, Is.EqualTo(CasinoAdventureEnding.Withdraw)); Assert.That(saved.Phase, Is.EqualTo(CasinoAdventurePhase.Ended));
            yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            Assert.That(Field<GameObject>(presenter, "standardEndingPanel").activeInHierarchy, Is.True);
            yield return Screenshot("S1WithdrawalEnding");
            yield return MouseClick(Field<Button>(presenter, "standardEndingReturnButton"));
            yield return Wait(() => owner == null && IsStableHub(), "撤离结果明确返回Hub", 45);
            Assert.That(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")).LoadOrCreate().Data.FinishedRuns, Is.EqualTo(1));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator RestoredClosingBlackjackKeepsItsDeskUntilSettledAndThenAllowsWithdrawal()
        {
            yield return EnterSample();
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            // 用真实领域构造超时活动牌局恢复夹具；不把它宣称为等待完整四分钟的实玩。
            var config = Field<JinxCasinoGameSettings>(owner, "gameSettings").CreateConfig();
            CasinoAdventureSession fixture = null;
            for (uint seed = 1; seed <= 32; seed++)
            {
                var candidate = CasinoAdventureSession.Start(seed, CasinoAdventureMode.Standard, 1, config);
                Assert.That(candidate.BeginGame("closing-fixture", CasinoGameKind.Blackjack, 10, 0, "s1.cards").Success, Is.True);
                if (candidate.HasActiveRound) { fixture = candidate; break; }
            }
            Assert.That(fixture, Is.Not.Null);
            Assert.That(fixture.Advance(fixture.State.RemainingMilliseconds).Success, Is.True);
            Assert.That(fixture.State.Phase, Is.EqualTo(CasinoAdventurePhase.Closing));
            long beforeCoins = fixture.State.Coins; uint beforeRandom = fixture.State.RandomState;
            new CasinoLocalSaveStore(saveDirectory).Save(1, fixture);
            yield return MouseClick(Field<Button>(presenter, "saveMainLoadButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            yield return Wait(() => owner.Game.HasActiveRound && !owner.Player.IsMenuOpen, "真实菜单恢复Closing牌局", 3);
            Assert.That(UIManager.Instance.Get<JinxCasinoEndingView>(), Is.Null);
            Assert.That(owner.Game.State.Coins, Is.EqualTo(beforeCoins)); Assert.That(owner.Game.State.RandomState, Is.EqualTo(beforeRandom));
            var body = owner.Player.Body; var camera = owner.Player.Camera;
            yield return MoveUntil(Key.A, () => body.transform.position.x <= -2.1f); yield return LookYaw(180);
            yield return Wait(() => owner.Player.Exit.IsNearby, "带活动牌局来到离场口", 3); yield return KeyPress(Key.E);
            Assert.That(owner.Player.Exit.Feedback, Does.Contain("完成这一局"));
            Assert.That(owner.Game.State.Phase, Is.EqualTo(CasinoAdventurePhase.Closing));
            Assert.That(owner.Game.HasActiveRound, Is.True); Assert.That(owner.Game.State.LockedCoins, Is.EqualTo(10));
            Assert.That(UIManager.Instance.Get<JinxCasinoEndingView>(), Is.Null);
            yield return LookYaw(0); yield return MoveUntil(Key.D, () => body.transform.position.x >= -.1f);
            var cards = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None).Single(station => station.StationId == "s1.cards");
            yield return MoveUntil(Key.W, () => body.transform.position.z >= cards.InteractionPosition.z - .12f); yield return KeyPress(Key.E);
            yield return Wait(() => owner.Player.TableView?.StationId == cards.StationId && Vector3.Distance(camera.transform.position, cards.FocusPose.position) < .001f,
                "Closing仍可聚焦原牌桌", 3);
            yield return ClickTarget(camera, cards, "secondary");
            yield return Wait(() => owner.Game.State.SettledRoundSequence == 1 && !owner.Player.IsTableAnimating, "真实停牌并展示已付牌局结果", 6);
            Assert.That(owner.Game.State.Phase, Is.EqualTo(CasinoAdventurePhase.Failed));
            Assert.That(owner.Game.State.Coins, Is.EqualTo(beforeCoins - 10 + owner.Game.State.LastRoundPayout));
            Assert.That(owner.Game.ProfileData.FinishedRuns, Is.Zero); Assert.That(owner.Player.Exit.HasEnding, Is.False);
            yield return Screenshot("S1ClosingRoundSettled");
            yield return KeyPress(Key.Escape); yield return Wait(() => Cursor.lockState == CursorLockMode.Locked, "离开已结算牌桌", 3);
            yield return MoveUntil(Key.S, () => body.transform.position.z <= -4.6f); yield return MoveUntil(Key.A, () => body.transform.position.x <= -2.1f);
            yield return LookYaw(180); yield return KeyPress(Key.E); yield return KeyPress(Key.E);
            yield return Wait(() => owner.Player.Exit.HasEnding, "失败后明确撤离", 3);
            Assert.That(owner.Game.State.Ending, Is.EqualTo(CasinoAdventureEnding.Withdraw));
            Assert.That(owner.Game.State.SettledRoundSequence, Is.EqualTo(1)); Assert.That(owner.Game.ProfileData.Withdrawals, Is.EqualTo(1));
            yield return MouseClick(Field<Button>(presenter, "standardEndingReturnButton"));
            yield return Wait(() => owner == null && IsStableHub(), "Closing结算后返回Hub", 45);
        }

        private IEnumerator LookYaw(float target)
        {
            var body = owner.Player.Body;
            float gain = Field<JinxCasinoGameSettings>(owner, "gameSettings").LookSensitivity * owner.Settings.Value.PcLookMultiplier;
            yield return null; yield return null;
            for (int i = 0; i < 6 && Mathf.Abs(Mathf.DeltaAngle(body.transform.eulerAngles.y, target)) > .1f; i++)
            {
                float error = Mathf.DeltaAngle(body.transform.eulerAngles.y, target);
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(error / gain, 0) }); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            }
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(body.transform.eulerAngles.y, target)), Is.LessThan(.2f), "真实鼠标转向目标朝向。");
        }

        private IEnumerator WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep step)
        { yield return Wait(() => owner.Game.State.Teaching.Step == step, "真实教学步骤：" + step, 10); }

        private IEnumerator EnterSample(bool enterWithHeldGamepad = false, bool touchEntry = false)
        {
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                Assert.That(startup, Is.Not.Null); yield return Wait(() => startup.isDone, "唯一AppEntrance启动", 90);
            }
            yield return Wait(IsStableHub, "正式Hub稳定", 90);
            Assert.That(GameSceneNavigator.Instance.IsEditorDirect, Is.False);
            int width = touchEntry ? 960 : 1280, height = touchEntry ? 600 : 720;
            resolution = new GameViewResolution(width, height);
            yield return Wait(() => Screen.width == width && Screen.height == height, "实际横屏GameView", 15);
            originalInputSettings = InputSystem.settings; testInputSettings = Object.Instantiate(originalInputSettings);
            testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testInputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testInputSettings;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            var hubMenu = UIManager.Instance.Get<MainMenuView>();
            var hubEntries = Field<List<MainMenuDemoEntry>>(hubMenu, "entries");
            var casinoIndex = hubEntries.FindIndex(entry => entry.SceneId == GameSceneId.JinxCasino);
            Assert.That(casinoIndex, Is.GreaterThanOrEqualTo(0));
            var hubList = Field<LoopScrollView>(hubMenu, "LoopScrollView_DemoList");
            hubList.ScrollToCell(casinoIndex, ScrollAlignment.Center);
            yield return null;
            var casinoButton = hubList.GetVisibleCell(casinoIndex).GetComponentInChildren<LoopScrollMenuButton>(true);
            if (enterWithHeldGamepad)
            {
                gamepad = InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                yield return Wait(() => EventSystem.current.sendNavigationEvents && EventSystem.current.currentSelectedGameObject != null,
                    "Hub 默认手柄焦点", 3);
                // 方向按实际控件位置选择，不固定旧按钮顺序，也不直接设置焦点或调用监听器。
                for (int attempt = 0; attempt < 10 && EventSystem.current.currentSelectedGameObject != casinoButton.gameObject; attempt++)
                {
                    var selected = EventSystem.current.currentSelectedGameObject;
                    Assert.That(selected, Is.Not.Null);
                    Vector3 delta = casinoButton.transform.position - selected.transform.position;
                    var direction = Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                        ? (delta.x > 0 ? GamepadButton.DpadRight : GamepadButton.DpadLeft)
                        : (delta.y > 0 ? GamepadButton.DpadUp : GamepadButton.DpadDown);
                    yield return PadPress(direction);
                }
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(casinoButton.gameObject), "实际方向导航可到达赌场入口。");
                InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            }
            else if (touchEntry)
            {
                touchscreen = InputSystem.AddDevice<Touchscreen>();
                yield return TouchTap(casinoButton);
                yield return TouchTap(Field<Button>(hubMenu, "Button_Start"));
            }
            else
            {
                yield return Wait(() => EventSystem.current.sendNavigationEvents && casinoButton.IsInteractable(), "Hub输入门闩释放", 5);
                yield return MouseClick(casinoButton);
                yield return MouseClick(Field<Button>(hubMenu, "Button_Start"));
            }
            yield return Wait(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.JinxCasino && !GameSceneNavigator.Instance.IsTransitioning,
                "实际 Hub 按钮进入赌场", 45);
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoImmersionHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning, "保存的沉浸HUD", 30);
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoMainMenuView>()?.State == ViewState.Visible, "独立主菜单", 10);
            owner = Object.FindFirstObjectByType<JinxCasinoController>();
            Assert.That(owner, Is.Not.Null); Assert.That(owner.HasInputConfiguration, Is.True);
            Assert.That(UIManager.Instance.Get<DlssSettingsView>(), Is.Null, "赌场入场不创建公共画面设置或悬浮入口。");
            saveDirectory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TestSaves", "S1Entry-" + Guid.NewGuid().ToString("N")));
            ValidateSavePath();
            owner.Game.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory));
            owner.Game.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")));
            Assert.That(owner.gameObject.scene.path, Is.EqualTo("Assets/LoadResources/Demos/jinx_casino/Scenes/Immersion.unity"));
            var hud = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            var presenter = hud;
            Assert.That(presenter, Is.Not.Null);
            if (enterWithHeldGamepad)
            {
                yield return null; yield return null;
                Assert.That(owner.Game.HasAdventure, Is.False, "Hub 的 A 不能同时确认赌场开始。");
                Assert.That(EventSystem.current.sendNavigationEvents, Is.False, "进入赌场时仍按住 A，应等待明确释放。");
                Assert.That(EventSystem.current.currentSelectedGameObject, Is.Null);
                InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
                yield return Wait(() => EventSystem.current.sendNavigationEvents &&
                    EventSystem.current.currentSelectedGameObject == Field<Button>(presenter, "start").gameObject, "松开 A 后赌场菜单恢复焦点", 3);
            }
            var body = owner.Player.Body; var camera = owner.Player.Camera;
            Assert.That(UIRootManager.Instance.BaseCamera, Is.SameAs(camera)); AssertSingleListener();
            Assert.That(EventSystem.current.GetComponent<InputSystemUIInputModule>(), Is.Not.Null);
            yield return Wait(() => hud.gameObject.GetComponentsInParent<CanvasGroup>(true).All(group => group.alpha >= .99f), "Core入场淡出结束", 2);
        }

        private IEnumerator PadPress(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            yield return WaitWindowNavigation();
        }
        private IEnumerator KeyPress(Key key)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null; yield return WaitWindowNavigation(); }
        private IEnumerator MoveUntil(Key key, Func<bool> arrived)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            double deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (!arrived() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(arrived(), Is.True, $"实际{key}通道移动超时；位置={owner.Player.Body.transform.position}，朝向={owner.Player.Body.transform.eulerAngles}，" +
                $"设备={owner.Player.DeviceKind}，聚焦={owner.Player.HasFocus}，暂停={owner.Player.IsPaused}，菜单={owner.Player.IsMenuOpen}，" +
                $"上下文={Field<object>(owner.Player, "appliedInputContext")}，按键={keyboard[key].isPressed}");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        private IEnumerator MouseClick(Selectable button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True, $"点击前控件必须可用：{button?.name}，菜单={owner?.Player.IsMenuOpen}，暂停={owner?.Player.IsPaused}，页面={owner?.Data.Page}");
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)); point = new Vector2(Mathf.Round(point.x), Mathf.Round(point.y));
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty); Assert.That(hits[0].gameObject.GetComponentInParent<Selectable>(), Is.SameAs(button), "真实保存按钮中心射线不得被覆盖。");
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            yield return WaitWindowNavigation();
        }
        private IEnumerator ClickTarget(Camera camera, JinxCasinoStation station, string suffix)
        {
            var target = station.Targets.Single(value => value.TargetId == station.StationId + "." + suffix);
            yield return ClickTarget(camera, target);
        }
        private IEnumerator ClickTarget(Camera camera, JinxCasinoTableTarget target)
        {
            yield return Wait(() => target.IsAvailable, "实体目标可操作：" + target.TargetId, 3);
            var point = TargetPosition(camera, target);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left)); yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }); yield return null; yield return null;
            yield return WaitWindowNavigation();
        }

        private static Vector2 TargetPosition(Camera camera, JinxCasinoTableTarget target)
        {
            Physics.SyncTransforms(); var collider = target.GetComponent<Collider>(); Assert.That(collider, Is.Not.Null);
            Vector3 screen = camera.WorldToScreenPoint(collider.bounds.center);
            Assert.That(screen.z, Is.GreaterThan(0)); Assert.That(screen.x, Is.InRange(0, Screen.width), target.TargetId); Assert.That(screen.y, Is.InRange(0, Screen.height), target.TargetId);
            var point = new Vector2(screen.x, screen.y);
            Assert.That(Physics.Raycast(camera.ScreenPointToRay(point), out var hit, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.collider.GetComponentInParent<JinxCasinoTableTarget>(), Is.SameAs(target),
                "真实相机射线不得穿透桌体或其它目标。首个命中：" + hit.collider.name + "，位置：" + hit.point);
            return point;
        }

        private static void AssertDeskLabelsVisible(Camera camera, JinxCasinoStation station)
        {
            var presentation = station.GetComponent<JinxCasinoS1Presentation>();
            foreach (string field in new[] { "rulesText", "amountText" })
            {
                var label = (TMPro.TMP_Text)typeof(JinxCasinoS1Presentation).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presentation);
                Assert.That(label, Is.Not.Null);
                label.ForceMeshUpdate();
                var corners = new Vector3[4]; label.rectTransform.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 viewport = camera.WorldToViewportPoint(corner);
                    Assert.That(viewport.z, Is.GreaterThan(0), station.StationId + "." + field);
                    Assert.That(viewport.x, Is.InRange(0, 1), station.StationId + "." + field + "横向被视口裁掉");
                    Assert.That(viewport.y, Is.InRange(0, 1), station.StationId + "." + field + "纵向被视口裁掉");
                }
            }
        }

        private IEnumerator TouchTap(Selectable button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True, $"点击前控件必须可用：{button?.name}，菜单={owner?.Player.IsMenuOpen}，暂停={owner?.Player.IsPaused}，页面={owner?.Data.Page}");
            yield return null; yield return null;
            var point = UiPosition(button);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits, Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Selectable>(), Is.SameAs(button), "触屏按钮不得被遮挡。");
            yield return TouchTap(point);
        }

        private IEnumerator TouchTarget(Camera camera, JinxCasinoStation station, string suffix) =>
            TouchTarget(camera, station.Targets.Single(value => value.TargetId == station.StationId + "." + suffix));

        private IEnumerator TouchTarget(Camera camera, JinxCasinoTableTarget target)
        {
            yield return Wait(() => target.IsAvailable, "触屏实体目标可操作：" + target.TargetId, 3);
            var point = TargetPosition(camera, target);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
            Assert.That(hits, Is.Empty, "实体触区不得被 HUD 或摇杆遮挡。");
            yield return TouchTap(point);
            Assert.That(owner.Player.DeviceKind, Is.EqualTo(Core.Runtime.Inputs.InputDeviceKind.Touch));
        }

        private IEnumerator TouchTap(Vector2 point)
        {
            int id = ++touchSequence;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = point, phase = UnityEngine.InputSystem.TouchPhase.Began }); yield return null;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = point, phase = UnityEngine.InputSystem.TouchPhase.Ended }); yield return null; yield return null;
            yield return WaitWindowNavigation();
        }

        private static Vector2 UiPosition(Component component)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)component.transform;
            return RectTransformUtility.WorldToScreenPoint(component.GetComponentInParent<Canvas>().worldCamera, rect.TransformPoint(rect.rect.center));
        }

        private IEnumerator TouchMoveUntil(Core.Runtime.Inputs.TouchInputPad pad, Vector2 direction, Func<bool> arrived)
        {
            if (arrived()) yield break;
            yield return Wait(() => pad.gameObject.activeInHierarchy, "触屏摇杆已随探索上下文恢复", 3);
            int id = ++touchSequence;
            var origin = UiPosition(pad);
            var held = origin + direction * (80f * Screen.height / 720f);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = origin, phase = UnityEngine.InputSystem.TouchPhase.Began }); yield return null; yield return null;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = held, phase = UnityEngine.InputSystem.TouchPhase.Moved }); yield return null; yield return null;
            yield return Wait(arrived, "实际触屏摇杆移动", 8);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = held, phase = UnityEngine.InputSystem.TouchPhase.Ended }); yield return null; yield return null;
            Assert.That(pad.Move, Is.EqualTo(Vector2.zero), "松开摇杆后不能残留移动。");
        }

        private IEnumerator TouchLook(Core.Runtime.Inputs.TouchInputPad pad, Vector2 delta)
        {
            Assert.That(pad.gameObject.activeInHierarchy, Is.True);
            int id = ++touchSequence;
            var origin = UiPosition(pad);
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = origin, phase = UnityEngine.InputSystem.TouchPhase.Began }); yield return null; yield return null;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = origin + delta, phase = UnityEngine.InputSystem.TouchPhase.Moved }); yield return null; yield return null;
            InputSystem.QueueStateEvent(touchscreen, new TouchState { touchId = id, position = origin + delta, phase = UnityEngine.InputSystem.TouchPhase.Ended }); yield return null; yield return null;
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SavedAudioDoesNotReplayRestoredOrRepeatedResultsAndRegisteredSourcesFollowVolume()
        {
            yield return EnterSample();
            var hud = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            var presenter = hud;
            var director = owner.GetComponent<JinxCasinoAudioDirector>();
            Assert.That(director, Is.Not.Null);
            var music = Field<AudioSource>(director, "music");
            var feedback = Field<AudioSource>(director, "sfx");
            // 4秒静音片段用于观察是否触发，不用短生产音效的偶然播放时长作断言。
            testAudioClip = AudioClip.Create("JinxAudioRegressionSilence", 44100 * 4, 1, 44100, false);
            string[] ids = { "UiClick", "MachineBegin", "Win", "Lose", "Coin", "TaskComplete", "Event", "EndingDignity", "EndingTakeover", "EndingWithdraw", "Horn", "Boing", "Charge", "ClubLoop" };
            director.Setup(owner, music, feedback, ids.Select(id => new CasinoAudioClipBinding { Id = id, Clip = testAudioClip }).ToArray());
            director.SetVolume(0);
            yield return MouseClick(Field<Button>(presenter, "start"));
            Assert.That(owner.Game.BeginGame("audio-spin", CasinoGameKind.Slots, 10, 0, "s1.fruit", Time.frameCount).Success, Is.True);
            yield return null;
            Assert.That(feedback.isPlaying, Is.True);
            int sequence = owner.Game.State.SettledRoundSequence;
            Assert.That(sequence, Is.EqualTo(1));
            Assert.That(owner.Game.SaveAdventure(1), Is.True); feedback.Stop(); yield return null;
            Assert.That(owner.Game.BeginGame("audio-spin", CasinoGameKind.Slots, 10, 0, "s1.fruit", Time.frameCount).Success, Is.True);
            yield return null;
            Assert.That(owner.Game.State.SettledRoundSequence, Is.EqualTo(sequence));
            Assert.That(feedback.isPlaying, Is.False, "同编号重发不重播结算。");
            Assert.That(owner.LoadAdventure(1), Is.True); yield return null;
            Assert.That(feedback.isPlaying, Is.False, "当前已结算快照只建立音效基线。");
            Assert.That(owner.Game.EquipProfile("color_blue"), Is.True); yield return null;
            Assert.That(feedback.isPlaying, Is.False, "换装通知不重播旧结果。");
            Assert.That(owner.Game.ChooseEnding(CasinoAdventureEnding.Withdraw, Time.frameCount).Success, Is.True); yield return null;
            Assert.That(feedback.isPlaying, Is.True);
            Assert.That(owner.Game.SaveAdventure(2), Is.True); feedback.Stop(); yield return null;
            Assert.That(owner.LoadAdventure(2), Is.True); yield return null;
            Assert.That(feedback.isPlaying, Is.False, "已结束旅程恢复不重播结局。");
            var effectRoot = new GameObject("AudioRegressionEffect"); effectRoot.transform.SetParent(owner.transform, false);
            var effect = effectRoot.AddComponent<AudioSource>(); effect.playOnAwake = false;
            director.SetVolume(.4f); director.RegisterEffectSource(effect, .25f);
            Assert.That(effect.volume, Is.EqualTo(.1f).Within(.0001f));
            director.SetVolume(.2f, true);
            Assert.That(effect.volume, Is.EqualTo(.05f).Within(.0001f)); Assert.That(effect.mute, Is.True);
            Assert.That(music.mute && feedback.mute, Is.True);
            director.SetVolume(.7f); director.RegisterEffectSource(effect, .25f);
            Assert.That(effect.volume, Is.EqualTo(.175f).Within(.0001f));
            Assert.That(effect.mute || music.mute || feedback.mute, Is.False);
            director.SetVolume(0);
            yield return MouseClick(Field<Button>(presenter, "standardEndingReturnButton"));
            yield return Wait(() => owner == null && IsStableHub(), "音效回归退出", 45);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if (keyboard != null && keyboard.added) InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                if (mouse != null && mouse.added) InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
                if (standaloneOverride && GameSceneNavigator.Instance != null)
                { typeof(GameSceneNavigator).GetProperty("StandaloneScene").SetValue(GameSceneNavigator.Instance, null); standaloneOverride = false; }
                var live = Object.FindFirstObjectByType<JinxCasinoController>();
                if (live != null) { live.RequestExit(); yield return Wait(() => live == null && IsStableHub(), "失败路径清理样板", 45); }
            }
            finally
            {
                if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
                if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
                if (gamepad != null && gamepad.added) InputSystem.RemoveDevice(gamepad);
                if (touchscreen != null && touchscreen.added) InputSystem.RemoveDevice(touchscreen);
                if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
                if (testInputSettings != null) Object.Destroy(testInputSettings);
                if (testAudioClip != null) { Object.Destroy(testAudioClip); testAudioClip = null; }
                resolution?.Dispose(); resolution = null;
                if (settingsTestKey != null) { PlayerPrefs.DeleteKey(settingsTestKey); PlayerPrefs.Save(); settingsTestKey = null; }
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
        private IEnumerator WaitWindowNavigation()
        {
            yield return Wait(() =>
            {
                if (owner == null)
                    return true;
                Type expected = owner.Data.Page switch
                {
                    JinxCasinoPage.MainMenu => typeof(JinxCasinoMainMenuView),
                    JinxCasinoPage.Pause => typeof(JinxCasinoPauseView),
                    JinxCasinoPage.Settings => typeof(JinxCasinoSettingsView),
                    JinxCasinoPage.SaveSlots or JinxCasinoPage.SaveConfirm => typeof(JinxCasinoSaveView),
                    JinxCasinoPage.TutorialReady or JinxCasinoPage.TutorialChoice or JinxCasinoPage.TutorialConfirm => typeof(JinxCasinoTutorialView),
                    JinxCasinoPage.Ending => typeof(JinxCasinoEndingView),
                    _ => null
                };
                if (expected != null)
                    return UIManager.Instance.cacheStack.GetView(expected)?.State == ViewState.Visible;
                return UIManager.Instance.Get<JinxCasinoPauseView>()?.IsEnable != true
                    && UIManager.Instance.Get<JinxCasinoTutorialView>()?.IsEnable != true
                    && UIManager.Instance.Get<JinxCasinoSaveView>()?.IsEnable != true;
            }, "当前业务页面可见", 10);
        }
        private static T Field<T>(object value, string name)
        {
            var field = value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null) return (T)field.GetValue(value);
            if (value is JinxCasinoImmersionHudView)
            {
                // 拆分后只查当前正式页面的控件，不再从HUD读取未加载的窗口。
                foreach (var view in new View[] { UIManager.Instance.Get<JinxCasinoMainMenuView>(), UIManager.Instance.Get<JinxCasinoPauseView>(),
                    UIManager.Instance.Get<JinxCasinoTutorialView>(), UIManager.Instance.Get<JinxCasinoSaveView>(), UIManager.Instance.Get<JinxCasinoEndingView>(), UIManager.Instance.Get<JinxCasinoSettingsView>() })
                {
                    if (view?.gameObject == null) continue;
                    field = view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                    if (field != null) return (T)field.GetValue(view);
                }
            }
            Assert.Fail("当前页面没有绑定控件：" + name);
            return default;
        }
        private static bool IsStableHub() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        private static void AssertSingleListener() => Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
        private static IEnumerator Wait(Func<bool> predicate, string reason, float timeout)
        { double deadline = Time.realtimeSinceStartupAsDouble + timeout; while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null; Assert.That(predicate(), Is.True, reason + "超时"); }
    }
}
#endif
