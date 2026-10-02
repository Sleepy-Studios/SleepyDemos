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

namespace Tests.Demo
{
    /// 正式启动与保存样板的一台入口烟测；输入交给Core UI模块与Demo路由，不直接调用业务或监听器。
    public sealed class JinxCasinoImmersionEntryTests
    {
        private Keyboard keyboard;
        private Mouse mouse;
        private Gamepad gamepad;
        private InputSettings originalInputSettings;
        private InputSettings testInputSettings;
        private GameViewResolution resolution;
        private JinxCasinoController owner;
        private string saveDirectory;
        private string settingsTestKey;
        private bool standaloneOverride;

        [UnityTest, Timeout(180000)]
        public IEnumerator SavedEntryStartsByRealInputFocusesSlotsAndRestoresCameraAfterBackAndPause()
        {
            yield return EnterSample();
            var hud = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            var presenter = hud.gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            var body = Field<CharacterController>(owner, "body"); var camera = Field<Camera>(owner, "worldCamera");
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

        [UnityTest, Timeout(240000)]
        public IEnumerator SavedTutorialAdvancesOnlyThroughRealMovementObjectsAndSettlements()
        {
            yield return EnterSample();
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>().gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            var body = Field<CharacterController>(owner, "body"); var camera = Field<Camera>(owner, "worldCamera");
            yield return MouseClick(Field<Button>(presenter, "tutorialStartButton"));
            yield return Wait(() => owner.HasAdventure && !owner.IsAdventureInputBlocked && Cursor.lockState == CursorLockMode.Locked,
                "真实教学入口进入探索", 3);
            Assert.That(owner.AdventureState.Teaching.Step, Is.EqualTo(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Look));
            Assert.That(owner.AdventureState.Mode, Is.EqualTo(Hotfix.JinxCasino.Rules.CasinoAdventureMode.Practice));
            Assert.That(owner.AdventureState.Config.MaximumStake, Is.EqualTo(10));
            string tutorialRun = owner.AdventureState.RunId;
            yield return KeyPress(Key.Escape);
            yield return Wait(() => owner.IsImmersionPaused, "教学暂停", 3);
            yield return MouseClick(Field<Button>(presenter, "tutorialRetryButton"));
            yield return Wait(() => Field<Button>(presenter, "tutorialCancelButton").gameObject.activeInHierarchy, "重玩确认", 3);
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.Start)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            yield return Wait(() => Field<GameObject>(presenter, "pauseMenu").activeInHierarchy, "手柄Menu取消确认后返回暂停", 3);
            Assert.That(owner.IsImmersionPaused, Is.True, "关闭确认不能悄悄恢复时钟。");
            Assert.That(owner.AdventureState.RunId, Is.EqualTo(tutorialRun));
            yield return MouseClick(Field<Button>(presenter, "resume"));
            yield return Wait(() => !owner.IsImmersionPaused && !owner.IsAdventureInputBlocked, "显式继续教学", 3);
            yield return null; yield return null;
            for (int i = 0; i < 15 && owner.AdventureState.Teaching.Step == Hotfix.JinxCasino.Rules.CasinoTutorialStep.Look; i++)
            {
                InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(0, 200) }); yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState()); yield return null;
            }
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Walk);
            var stations = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None);
            var fruit = stations.Single(value => value.StationId == "s1.fruit");
            var cards = stations.Single(value => value.StationId == "s1.cards");
            var levers = stations.Single(value => value.StationId == "s1.sync");
            yield return MoveUntil(Key.W, () => body.transform.position.z >= fruit.InteractionPosition.z - .12f);
            yield return MoveUntil(Key.A, () => body.transform.position.x <= fruit.InteractionPosition.x + .15f);
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.EnterSlots);
            yield return KeyPress(Key.E); yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.AddChips);
            yield return ClickTarget(camera, fruit, "chip10"); yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Confirm);
            yield return ClickTarget(camera, fruit, "commit"); yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.SlotsResult);
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(1000));
            yield return ClickTarget(camera, fruit, "primary");
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.LeaveSlots);
            Assert.That(fruit.GetComponent<JinxCasinoS1SlotsPresentation>().IsAnimating, Is.False);
            Assert.That(owner.AdventureState.SettledRoundSequence, Is.EqualTo(1));
            yield return Screenshot("S1TutorialFruitResult");
            yield return KeyPress(Key.Escape); yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Blackjack);
            yield return MoveUntil(Key.D, () => body.transform.position.x >= -.10f);
            yield return MoveUntil(Key.W, () => body.transform.position.z >= cards.InteractionPosition.z - .12f);
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.TableView?.StationId == cards.StationId && Vector3.Distance(camera.transform.position, cards.FocusPose.position) < .001f, "教学聚焦二十一点", 3);
            yield return ClickTarget(camera, cards, "chip10"); yield return ClickTarget(camera, cards, "commit");
            if (owner.HasActiveAdventureRound) yield return ClickTarget(camera, cards, "secondary");
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.BuyWrench);
            Assert.That(cards.GetComponent<JinxCasinoS1BlackjackPresentation>().IsAnimating, Is.False);
            yield return KeyPress(Key.Escape);
            yield return Wait(() => Cursor.lockState == CursorLockMode.Locked, "牌桌恢复探索", 3);
            yield return MoveUntil(Key.S, () => body.transform.position.z <= -4.6f);
            var counter = Object.FindFirstObjectByType<JinxCasinoShopCounter>();
            yield return MoveUntil(Key.A, () => body.transform.position.x <= counter.InteractionPosition.x + .15f);
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.HasShopFocus && Vector3.Distance(camera.transform.position, counter.FocusPose.position) < .01f,
                "教学柜台聚焦完成", 3);
            yield return ClickTarget(camera, counter.Targets.Single(target => target.TargetId == "s1.supply.action0"));
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.UseWrench);
            yield return ClickTarget(camera, counter.Targets.Single(target => target.TargetId == "s1.supply.action1"));
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Levers);
            Assert.That(owner.AdventureState.CooperationHelpCharges, Is.EqualTo(1));
            yield return KeyPress(Key.Escape);
            yield return Wait(() => Cursor.lockState == CursorLockMode.Locked, "柜台恢复探索", 3);
            yield return MoveUntil(Key.D, () => body.transform.position.x >= -.10f);
            yield return MoveUntil(Key.W, () => body.transform.position.z >= levers.InteractionPosition.z - .12f);
            yield return MoveUntil(Key.D, () => body.transform.position.x >= levers.InteractionPosition.x - .15f);
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.TableView?.StationId == levers.StationId && Vector3.Distance(camera.transform.position, levers.FocusPose.position) < .001f, "教学聚焦合拍台", 3);
            yield return ClickTarget(camera, levers, "chip10"); yield return ClickTarget(camera, levers, "commit");
            yield return Wait(() => owner.TableView.LeverWindowOpen, "实际绿灯窗口", 4);
            yield return ClickTarget(camera, levers, "primary");
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Ready);
            Assert.That(owner.AdventureState.SettledRoundSequence, Is.EqualTo(3));
            yield return Screenshot("S1TutorialReady");
            yield return KeyPress(Key.Escape);
            yield return Wait(() => Field<Button>(presenter, "tutorialCompleteButton").gameObject.activeInHierarchy, "明确完成教学按钮", 3);
            yield return MouseClick(Field<Button>(presenter, "tutorialReadyBackButton"));
            yield return Wait(() => !owner.IsAdventureInputBlocked, "稍后完成仍可继续练习", 3);
            yield return KeyPress(Key.Escape);
            yield return Wait(() => owner.IsImmersionPaused && Field<Button>(presenter, "tutorialReviewButton").gameObject.activeInHierarchy,
                "暂停提供重新打开教学结果入口", 3);
            yield return MouseClick(Field<Button>(presenter, "tutorialReviewButton"));
            yield return MouseClick(Field<Button>(presenter, "tutorialCompleteButton"));
            yield return WaitStep(Hotfix.JinxCasino.Rules.CasinoTutorialStep.Completed);
            long coins = owner.AdventureState.Coins; uint random = owner.AdventureState.RandomState;
            yield return MouseClick(Field<Button>(presenter, "tutorialContinueButton"));
            yield return Wait(() => !owner.IsAdventureInputBlocked, "明确继续当前练习", 3);
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(coins)); Assert.That(owner.AdventureState.RandomState, Is.EqualTo(random));
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator SavedSlotsSaveCancelAndLoadThroughActualMenusWithoutRepeatingPayment()
        {
            yield return EnterSample();
            var hud = UIManager.Instance.Get<JinxCasinoImmersionHudView>();
            var presenter = hud.gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            yield return MouseClick(Field<Button>(presenter, "saveMainLoadButton"));
            Assert.That(Field<Button>(presenter, "saveSlot1Button").interactable, Is.False);
            Assert.That(Field<Button>(presenter, "saveSlot2Button").interactable, Is.False);
            Assert.That(Field<Button>(presenter, "saveSlot3Button").interactable, Is.False);
            gamepad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == Field<Button>(presenter, "saveBackButton").gameObject,
                "空槽列表的手柄焦点落在返回", 3);
            yield return PadPress(GamepadButton.South);
            Assert.That(owner.HasAdventure, Is.False);
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return Wait(() => owner.HasAdventure && !owner.IsAdventureInputBlocked, "开始用于存档回归的正式冒险", 3);
            string run = owner.AdventureState.RunId;
            yield return KeyPress(Key.Escape);
            yield return Wait(() => owner.IsImmersionPaused, "保存前暂停", 3);
            yield return MouseClick(Field<Button>(presenter, "savePauseSaveButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            Assert.That(owner.SelectedSaveSlot, Is.EqualTo(1));
            Assert.That(owner.GetSaveSlotInfo(1).Coins, Is.EqualTo(1000));
            string firstBytes = File.ReadAllText(Path.Combine(saveDirectory, "save-1.json"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            yield return MouseClick(Field<Button>(presenter, "saveCancelButton"));
            Assert.That(File.ReadAllText(Path.Combine(saveDirectory, "save-1.json")), Is.EqualTo(firstBytes), "取消覆盖不写入原件。");
            Assert.That(owner.IsImmersionPaused, Is.True);
            yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            yield return MouseClick(Field<Button>(presenter, "resume"));
            yield return Wait(() => !owner.IsImmersionPaused && Cursor.lockState == CursorLockMode.Locked, "真实继续后探索", 3);
            var body = Field<CharacterController>(owner, "body"); var camera = Field<Camera>(owner, "worldCamera");
            var fruit = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None).Single(station => station.StationId == "s1.fruit");
            yield return MoveUntil(Key.W, () => body.transform.position.z >= fruit.InteractionPosition.z - .12f);
            yield return MoveUntil(Key.A, () => body.transform.position.x <= fruit.InteractionPosition.x + .15f);
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.TableView?.StationId == fruit.StationId && Vector3.Distance(camera.transform.position, fruit.FocusPose.position) < .001f,
                "具体水果机聚焦完成", 3);
            yield return ClickTarget(camera, fruit, "chip10"); yield return ClickTarget(camera, fruit, "commit"); yield return ClickTarget(camera, fruit, "primary");
            yield return Wait(() => owner.AdventureState.SettledRoundSequence == 1 && !owner.IsTableAnimating, "实际一次水果机结算", 6);
            long paidCoins = owner.AdventureState.Coins; uint paidRandom = owner.AdventureState.RandomState;
            yield return MouseClick(Field<Button>(presenter, "pause"));
            yield return MouseClick(Field<Button>(presenter, "savePauseSaveButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot2Button"));
            Assert.That(owner.SelectedSaveSlot, Is.EqualTo(2));
            Assert.That(owner.GetSaveSlotInfo(2).Coins, Is.EqualTo(paidCoins));
            yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            yield return MouseClick(Field<Button>(presenter, "savePauseLoadButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            yield return MouseClick(Field<Button>(presenter, "saveCancelButton"));
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(paidCoins));
            Assert.That(owner.AdventureState.RandomState, Is.EqualTo(paidRandom));
            Assert.That(owner.SelectedSaveSlot, Is.EqualTo(2)); Assert.That(owner.IsImmersionPaused, Is.True);
            yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
            yield return MouseClick(Field<Button>(presenter, "saveConfirmButton"));
            yield return Wait(() => !owner.IsImmersionPaused && !owner.IsAdventureInputBlocked, "确认读取后显式继续", 3);
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(1000)); Assert.That(owner.AdventureState.SettledRoundSequence, Is.Zero);
            Assert.That(owner.AdventureState.RunId, Is.EqualTo(run)); Assert.That(owner.SelectedSaveSlot, Is.EqualTo(1));
            yield return KeyPress(Key.Escape);
            yield return MouseClick(Field<Button>(presenter, "savePauseSaveButton"));
            yield return MouseClick(Field<Button>(presenter, "saveSlot3Button"));
            Assert.That(owner.SelectedSaveSlot, Is.EqualTo(3));
            yield return Screenshot("S1ThreeSaveSlots");
            yield return MouseClick(Field<Button>(presenter, "saveBackButton"));
            yield return MouseClick(Field<Button>(presenter, "leave"));
            yield return Wait(() => owner == null && IsStableHub(), "返回Hub保留独立三槽", 45);
            // 从新实例主菜单继续同一独立目录，输入设备和真实持久化文件保持不变。
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask();
            yield return Wait(() => travel.IsCompleted, "再次进入赌场", 45);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoImmersionHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning, "主菜单重新出现", 30);
            owner = Object.FindFirstObjectByType<JinxCasinoController>();
            owner.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory)); owner.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")));
            hud = UIManager.Instance.Get<JinxCasinoImmersionHudView>(); presenter = hud.gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            yield return Wait(() => hud.gameObject.GetComponentsInParent<CanvasGroup>(true).All(group => group.alpha >= .99f), "重新入场淡出结束", 3);
            yield return MouseClick(Field<Button>(presenter, "saveMainLoadButton"));
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == Field<Button>(presenter, "saveSlot1Button").gameObject,
                "有存档时选择首个可用槽", 3);
            yield return PadPress(GamepadButton.DpadDown);
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == Field<Button>(presenter, "saveSlot2Button").gameObject,
                "手柄方向选择第二槽", 3);
            yield return PadPress(GamepadButton.South);
            yield return Wait(() => owner.HasAdventure && !owner.IsAdventureInputBlocked, "主菜单读取已结算局", 3);
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(paidCoins)); Assert.That(owner.AdventureState.RandomState, Is.EqualTo(paidRandom));
            Assert.That(owner.AdventureState.SettledRoundSequence, Is.EqualTo(1)); Assert.That(owner.AdventureState.LastStationId, Is.EqualTo("s1.fruit"));
            Assert.That(owner.SelectedSaveSlot, Is.EqualTo(2)); Assert.That(owner.HasActiveAdventureRound, Is.False);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator StandaloneReturnReloadsGameMenuWithoutShowingHub()
        {
            yield return EnterSample();
            // Editor夹具只覆盖包启动方式；真正StartupScene解析仍由Player冷启动另行验收。
            typeof(GameSceneNavigator).GetProperty("StandaloneScene").SetValue(GameSceneNavigator.Instance, GameSceneId.JinxCasino);
            standaloneOverride = true;
            var initial = owner; owner.RequestExit();
            yield return Wait(() => initial == null && UIManager.Instance.Get<JinxCasinoImmersionHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning,
                "模拟独立包主菜单", 45);
            owner = Object.FindFirstObjectByType<JinxCasinoController>();
            owner.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory)); owner.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")));
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>().gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            Assert.That(owner.IsStandalonePlayer, Is.True);
            Assert.That(Field<Button>(presenter, "quitGameButton").gameObject.activeInHierarchy, Is.True);
            Assert.That(owner.HasAdventure, Is.False);
            yield return Screenshot("S1StandaloneMainMenu");
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return KeyPress(Key.Escape);
            var old = owner;
            yield return MouseClick(Field<Button>(presenter, "leave"));
            yield return Wait(() => old == null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.JinxCasino &&
                !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<JinxCasinoImmersionHudView>()?.State == ViewState.Visible,
                "独立包返回新的游戏主菜单", 45);
            owner = Object.FindFirstObjectByType<JinxCasinoController>(); Assert.That(owner.HasAdventure, Is.False);
            Assert.That(UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, Is.False);
            AssertSingleListener();
            typeof(GameSceneNavigator).GetProperty("StandaloneScene").SetValue(GameSceneNavigator.Instance, null); standaloneOverride = false;
            owner.RequestExit(); yield return Wait(IsStableHub, "恢复Editor Hub夹具", 45);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SettingsPreviewSaveAndCancelUseRealControlsAndKeepPause()
        {
            yield return EnterSample();
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>().gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            var local = Field<JinxCasinoLocalSettingsPresenter>(presenter, "localSettings"); Assert.That(local, Is.Not.Null);
            settingsTestKey = "JinxCasino.SettingsEntry." + Guid.NewGuid().ToString("N");
            var store = new CasinoLocalPreferencesStore(settingsTestKey);
            store.Save(new CasinoLocalPreferences { PcLookMultiplier = 1.2f, Volume = .4f });
            owner.LoadLocalPreferences(store);
            var router = Field<Core.Runtime.Inputs.GameplayInputRouter>(owner, "immersionInput");
            Assert.That(router.Settings.MouseLookMultiplier, Is.EqualTo(1.2f));
            var audio = owner.GetComponent<JinxCasinoAudioDirector>(); Assert.That(audio, Is.Not.Null);
            Assert.That(audio.Volume, Is.EqualTo(.4f));
            Assert.That(Field<AudioSource>(audio, "music").clip, Is.Not.Null);
            gamepad = InputSystem.AddDevice<Gamepad>(); InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            yield return MouseClick(Field<Button>(presenter, "settingsMainButton"));
            var pc = Field<Slider>(local, "pcSensitivity");
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == pc.gameObject, "设置默认焦点", 3);
            yield return PadPress(GamepadButton.DpadRight);
            Assert.That(owner.LocalPreferences.PcLookMultiplier, Is.GreaterThan(1.2f));
            Assert.That(store.Load().PcLookMultiplier, Is.EqualTo(1.2f), "预览不写盘");
            yield return PadPress(GamepadButton.East);
            Assert.That(Field<GameObject>(local, "settingsPanel").activeInHierarchy, Is.False);
            Assert.That(owner.LocalPreferences.PcLookMultiplier, Is.EqualTo(1.2f));
            yield return MouseClick(Field<Button>(presenter, "settingsMainButton"));
            yield return MouseClick(Field<Button>(local, "gamepadTabButton"));
            var padLook = Field<Slider>(local, "gamepadLookMultiplier");
            yield return Wait(() => EventSystem.current.currentSelectedGameObject == padLook.gameObject, "手柄页可操作焦点", 3);
            yield return PadPress(GamepadButton.DpadRight);
            float chosen = owner.LocalPreferences.GamepadLookMultiplier;
            Assert.That(chosen, Is.GreaterThan(1)); Assert.That(router.Settings.GamepadLookMultiplier, Is.EqualTo(chosen));
            yield return MouseClick(Field<Toggle>(local, "gamepadInvertY"));
            yield return MouseClick(Field<Button>(local, "saveButton"));
            Assert.That(store.Load().GamepadLookMultiplier, Is.EqualTo(chosen)); Assert.That(store.Load().GamepadInvertY, Is.True);
            yield return Screenshot("S1GamepadSettings");
            yield return PadPress(GamepadButton.East);
            yield return MouseClick(Field<Button>(presenter, "start")); yield return KeyPress(Key.Escape);
            long time = owner.AdventureState.RemainingMilliseconds;
            yield return MouseClick(Field<Button>(presenter, "settingsPauseButton"));
            yield return MouseClick(Field<Button>(local, "audioTabButton"));
            yield return MouseClick(Field<Toggle>(local, "muted")); Assert.That(owner.LocalPreferences.Muted, Is.True);
            Assert.That(Field<AudioSource>(audio, "music").mute && Field<AudioSource>(audio, "sfx").mute, Is.True);
            yield return Screenshot("S1AudioSettings");
            yield return PadPress(GamepadButton.Start);
            Assert.That(owner.IsImmersionPaused, Is.True); Assert.That(owner.LocalPreferences.Muted, Is.False);
            Assert.That(Field<AudioSource>(audio, "music").mute || Field<AudioSource>(audio, "sfx").mute, Is.False);
            Assert.That(owner.AdventureState.RemainingMilliseconds, Is.EqualTo(time));
            Assert.That(Field<GameObject>(local, "settingsPanel").activeInHierarchy, Is.False);
            owner.LoadLocalPreferences(store);
            Assert.That(router.Settings.GamepadLookMultiplier, Is.EqualTo(chosen)); Assert.That(router.Settings.GamepadInvertY, Is.True);
            yield return MouseClick(Field<Button>(presenter, "leave"));
            yield return Wait(() => owner == null && IsStableHub(), "设置退出后正常返回Hub", 45);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator UnsupportedSavesLeaveCurrentRunCameraAndFilesUntouched()
        {
            yield return EnterSample();
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>().gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return KeyPress(Key.Escape);
            string run = owner.AdventureState.RunId;
            long coins = owner.AdventureState.Coins; uint random = owner.AdventureState.RandomState;
            var camera = Field<Camera>(owner, "worldCamera"); Vector3 position = camera.transform.position;
            var store = new CasinoLocalSaveStore(saveDirectory);
            // 文件领域有效，但需要未装配的区域、模式、人数或具体机台。
            for (int sample = 0; sample < 5; sample++)
            {
                var config = Field<JinxCasinoGameSettings>(owner, "gameSettings").CreateConfig();
                if (sample == 0) { config.StageCount = 4; config.Targets = new long[] { 1200, 2000, 3500, 5000 }; }
                if (sample == 3) { config.AllowedGames = new[] { CasinoGameKind.HighLow }; config.InitiallyAvailableGames = config.AllowedGames; }
                var mode = sample == 0 ? CasinoAdventureMode.Standard : sample == 1 ? CasinoAdventureMode.Endless : CasinoAdventureMode.Practice;
                var fixture = CasinoAdventureSession.Start(7, mode, sample == 2 ? 2 : 1, config);
                if (sample >= 3)
                {
                    Assert.That(fixture.BeginGame("restore-fixture", sample == 3 ? CasinoGameKind.HighLow : CasinoGameKind.CooperativeLevers,
                        10, 0, sample == 3 ? "old.cards" : "old.levers").Success, Is.True);
                    Assert.That(fixture.HasActiveRound, Is.True);
                }
                store.Save(1, fixture);
                var files = Directory.GetFiles(saveDirectory, "*", SearchOption.TopDirectoryOnly).ToDictionary(path => path, File.ReadAllBytes);
                yield return MouseClick(Field<Button>(presenter, "savePauseLoadButton"));
                yield return MouseClick(Field<Button>(presenter, "saveSlot1Button"));
                yield return MouseClick(Field<Button>(presenter, "saveConfirmButton"));
                Assert.That(owner.AdventureStatus, Does.Contain("原存档和当前旅程均已保留"), "兼容样例 " + sample);
                Assert.That(owner.AdventureState.RunId, Is.EqualTo(run)); Assert.That(owner.AdventureState.Coins, Is.EqualTo(coins));
                Assert.That(owner.AdventureState.RandomState, Is.EqualTo(random)); Assert.That(owner.SelectedSaveSlot, Is.Zero);
                Assert.That(owner.IsImmersionPaused, Is.True); Assert.That(camera.transform.position, Is.EqualTo(position));
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
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>().gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return Wait(() => owner.HasAdventure && !owner.IsAdventureInputBlocked, "标准局进入探索", 3);
            var body = Field<CharacterController>(owner, "body"); var camera = Field<Camera>(owner, "worldCamera");
            yield return MoveUntil(Key.D, () => body.transform.position.x >= 2.1f);
            Assert.That(owner.IsExitTerminalNearby, Is.False, "背对入口不能交互。");
            yield return LookYaw(180);
            yield return Wait(() => owner.IsExitTerminalNearby, "可见验票物件", 3);
            yield return KeyPress(Key.E);
            Assert.That(owner.AdventureState.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(1000));
            Assert.That(owner.ExitFeedback, Does.Contain("未达到"));
            yield return LookYaw(0);
            var levers = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None).Single(station => station.StationId == "s1.sync");
            yield return MoveUntil(Key.W, () => body.transform.position.z >= levers.InteractionPosition.z - .12f);
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.TableView?.StationId == levers.StationId && Vector3.Distance(camera.transform.position, levers.FocusPose.position) < .001f,
                "标准局聚焦合拍台", 3);
            var leverPresentation = levers.GetComponentInChildren<JinxCasinoS1LeversPresentation>(true);
            var assistantArm = Field<Transform>(leverPresentation, "assistantArm");
            var assistantPalm = Field<Transform>(leverPresentation, "assistantPalm");
            var assistantGrip = Field<Transform>(leverPresentation, "assistantGrip");
            Assert.That(assistantArm, Is.Not.Null, "合拍台需要真实可见助手。");
            Quaternion waitingArm = assistantArm.localRotation;
            yield return ClickTarget(camera, levers, "chip100"); yield return ClickTarget(camera, levers, "commit");
            yield return Screenshot("S1LeverAssistantWaiting");
            Assert.That(owner.TableView.HasOwnActiveRound, Is.True, "实体确认后必须建立已投入局：" + owner.TableFeedback);
            yield return Wait(() => owner.TableView.LeverWindowOpen, "实际合拍绿灯", 4);
            yield return ClickTarget(camera, levers, "primary");
            yield return Wait(() => owner.AdventureState.SettledRoundSequence == 1, "真实合拍结算", 6);
            Assert.That(owner.AdventureState.Coins, Is.GreaterThanOrEqualTo(owner.AdventureTarget));
            long earned = owner.AdventureState.Coins;
            yield return Wait(() => Quaternion.Angle(waitingArm, assistantArm.localRotation) > 5, "助手跟随真实NPC拉杆", 2);
            yield return PadPress(GamepadButton.Start);
            Assert.That(owner.IsImmersionPaused, Is.True);
            Quaternion pausedArm = assistantArm.localRotation;
            Vector3 pausedPalm = assistantPalm.position;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(assistantArm.localRotation, Is.EqualTo(pausedArm));
            Assert.That(assistantPalm.position, Is.EqualTo(pausedPalm));
            yield return PadPress(GamepadButton.Start);
            yield return Wait(() => !owner.IsImmersionPaused, "助手与机台明确继续", 2);
            leverPresentation.Restore(owner.TableView);
            Assert.That(Vector3.Distance(assistantPalm.position, assistantGrip.position), Is.LessThan(.025f), "恢复握点仍贴合已拉下的杆。");
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(earned), "恢复助手表现不能再次发奖。");
            yield return Screenshot("S1LeverAssistantCompleted");
            yield return KeyPress(Key.Escape);
            yield return Wait(() => Cursor.lockState == CursorLockMode.Locked, "离桌恢复探索", 3);
            yield return MoveUntil(Key.S, () => body.transform.position.z <= -4.6f);
            yield return LookYaw(180); yield return Wait(() => owner.IsExitTerminalNearby, "再次接近验票器", 3);
            yield return Screenshot("S1QuotaVerifier");
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.AdventureState.Phase == CasinoAdventurePhase.Finale, "实体核验达标", 3);
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(earned), "额度只作阈值，不能再次扣款。");
            Assert.That(owner.ProfileData.FinishedRuns, Is.Zero, "验票不等于已选择结局。");
            yield return MoveUntil(Key.D, () => body.transform.position.x <= -2.1f);
            yield return Wait(() => owner.IsExitTerminalNearby, "离场口可见", 3);
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.HasStandardEnding && Field<GameObject>(presenter, "standardEndingPanel").activeInHierarchy, "明确领取离场券后展示结局", 3);
            Assert.That(owner.AdventureState.Ending, Is.EqualTo(CasinoAdventureEnding.LeaveWithDignity));
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(earned));
            Assert.That(owner.ProfileData.FinishedRuns, Is.EqualTo(1)); Assert.That(owner.ProfileData.DignifiedExits, Is.EqualTo(1));
            yield return Screenshot("S1DignifiedEnding");
            yield return KeyPress(Key.Escape);
            Assert.That(Field<GameObject>(presenter, "standardEndingPanel").activeInHierarchy, Is.True);
            Assert.That(owner.ProfileData.FinishedRuns, Is.EqualTo(1));
            yield return MouseClick(Field<Button>(presenter, "standardEndingReturnButton"));
            yield return Wait(() => owner == null && IsStableHub(), "结局按钮返回Hub", 45);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator WithdrawalRequiresVisibleRepeatedIntentAndEndingCanBeSaved()
        {
            yield return EnterSample();
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>().gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
            yield return MouseClick(Field<Button>(presenter, "start"));
            yield return Wait(() => owner.HasAdventure && !owner.IsAdventureInputBlocked, "标准局进入探索", 3);
            var body = Field<CharacterController>(owner, "body");
            var terminal = Object.FindObjectsByType<JinxCasinoExitTerminal>(FindObjectsSortMode.None).Single(value => value.Action == JinxCasinoExitAction.Leave);
            yield return MoveUntil(Key.A, () => body.transform.position.x <= -2.1f);
            yield return LookYaw(180); yield return Wait(() => owner.IsExitTerminalNearby, "离场口进入视野", 3);
            yield return KeyPress(Key.E);
            Assert.That(owner.IsExitWithdrawalArmed(terminal), Is.True);
            Assert.That(owner.AdventureState.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            yield return KeyPress(Key.Escape); yield return Wait(() => owner.IsImmersionPaused, "确认意图期间暂停", 3);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(owner.IsExitWithdrawalArmed(terminal), Is.True);
            yield return MouseClick(Field<Button>(presenter, "resume"));
            yield return Wait(() => !owner.IsImmersionPaused && !owner.IsAdventureInputBlocked, "显式继续", 3);
            yield return MoveUntil(Key.A, () => body.transform.position.x >= -.1f);
            yield return Wait(() => !owner.IsExitWithdrawalArmed(terminal), "走远清除撤离意图", 3);
            yield return MoveUntil(Key.D, () => body.transform.position.x <= -2.1f);
            yield return KeyPress(Key.E);
            Assert.That(owner.AdventureState.Phase, Is.EqualTo(CasinoAdventurePhase.Playing));
            yield return KeyPress(Key.E);
            yield return Wait(() => owner.HasStandardEnding && Field<GameObject>(presenter, "standardEndingPanel").activeInHierarchy, "再次确认才结束", 3);
            Assert.That(owner.AdventureState.Ending, Is.EqualTo(CasinoAdventureEnding.Withdraw));
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(1000)); Assert.That(owner.ProfileData.Withdrawals, Is.EqualTo(1));
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
            var presenter = UIManager.Instance.Get<JinxCasinoImmersionHudView>().gameObject.GetComponentInChildren<JinxCasinoImmersionHudPresenter>(true);
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
            yield return Wait(() => owner.HasActiveAdventureRound && !owner.IsAdventureInputBlocked, "真实菜单恢复Closing牌局", 3);
            Assert.That(Field<GameObject>(presenter, "standardEndingPanel").activeInHierarchy, Is.False);
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(beforeCoins)); Assert.That(owner.AdventureState.RandomState, Is.EqualTo(beforeRandom));
            var body = Field<CharacterController>(owner, "body"); var camera = Field<Camera>(owner, "worldCamera");
            yield return MoveUntil(Key.A, () => body.transform.position.x <= -2.1f); yield return LookYaw(180);
            yield return Wait(() => owner.IsExitTerminalNearby, "带活动牌局来到离场口", 3); yield return KeyPress(Key.E);
            Assert.That(owner.ExitFeedback, Does.Contain("完成这一局"));
            Assert.That(owner.AdventureState.Phase, Is.EqualTo(CasinoAdventurePhase.Closing));
            Assert.That(owner.HasActiveAdventureRound, Is.True); Assert.That(owner.AdventureState.LockedCoins, Is.EqualTo(10));
            Assert.That(Field<GameObject>(presenter, "standardEndingPanel").activeInHierarchy, Is.False);
            yield return LookYaw(0); yield return MoveUntil(Key.D, () => body.transform.position.x >= -.1f);
            var cards = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None).Single(station => station.StationId == "s1.cards");
            yield return MoveUntil(Key.W, () => body.transform.position.z >= cards.InteractionPosition.z - .12f); yield return KeyPress(Key.E);
            yield return Wait(() => owner.TableView?.StationId == cards.StationId && Vector3.Distance(camera.transform.position, cards.FocusPose.position) < .001f,
                "Closing仍可聚焦原牌桌", 3);
            yield return ClickTarget(camera, cards, "secondary");
            yield return Wait(() => owner.AdventureState.SettledRoundSequence == 1 && !owner.IsTableAnimating, "真实停牌并展示已付牌局结果", 6);
            Assert.That(owner.AdventureState.Phase, Is.EqualTo(CasinoAdventurePhase.Failed));
            Assert.That(owner.AdventureState.Coins, Is.EqualTo(beforeCoins - 10 + owner.AdventureState.LastRoundPayout));
            Assert.That(owner.ProfileData.FinishedRuns, Is.Zero); Assert.That(owner.HasStandardEnding, Is.False);
            yield return Screenshot("S1ClosingRoundSettled");
            yield return KeyPress(Key.Escape); yield return Wait(() => Cursor.lockState == CursorLockMode.Locked, "离开已结算牌桌", 3);
            yield return MoveUntil(Key.S, () => body.transform.position.z <= -4.6f); yield return MoveUntil(Key.A, () => body.transform.position.x <= -2.1f);
            yield return LookYaw(180); yield return KeyPress(Key.E); yield return KeyPress(Key.E);
            yield return Wait(() => owner.HasStandardEnding, "失败后明确撤离", 3);
            Assert.That(owner.AdventureState.Ending, Is.EqualTo(CasinoAdventureEnding.Withdraw));
            Assert.That(owner.AdventureState.SettledRoundSequence, Is.EqualTo(1)); Assert.That(owner.ProfileData.Withdrawals, Is.EqualTo(1));
            yield return MouseClick(Field<Button>(presenter, "standardEndingReturnButton"));
            yield return Wait(() => owner == null && IsStableHub(), "Closing结算后返回Hub", 45);
        }

        private IEnumerator LookYaw(float target)
        {
            var body = Field<CharacterController>(owner, "body");
            float gain = Field<JinxCasinoGameSettings>(owner, "gameSettings").LookSensitivity * owner.LocalPreferences.PcLookMultiplier;
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
        { yield return Wait(() => owner.AdventureState.Teaching.Step == step, "真实教学步骤：" + step, 10); }

        private IEnumerator EnterSample()
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
        }

        private IEnumerator PadPress(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
        }
        private IEnumerator KeyPress(Key key)
        { InputSystem.QueueStateEvent(keyboard, new KeyboardState(key)); yield return null; InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null; }
        private IEnumerator MoveUntil(Key key, Func<bool> arrived)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return Wait(arrived, "实际" + key + "通道移动", 8);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
        }
        private IEnumerator MouseClick(Selectable button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True);
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)); point = new Vector2(Mathf.Round(point.x), Mathf.Round(point.y));
            var pointer = new PointerEventData(EventSystem.current) { position = point };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty); Assert.That(hits[0].gameObject.GetComponentInParent<Selectable>(), Is.SameAs(button), "真实保存按钮中心射线不得被覆盖。");
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
            Assert.That(screen.z, Is.GreaterThan(0)); Assert.That(screen.x, Is.InRange(0, Screen.width), target.TargetId); Assert.That(screen.y, Is.InRange(0, Screen.height), target.TargetId);
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
                if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
                if (testInputSettings != null) Object.Destroy(testInputSettings);
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
        private static T Field<T>(object value, string name)
        { var field = value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); Assert.That(field, Is.Not.Null); return (T)field.GetValue(value); }
        private static bool IsStableHub() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        private static void AssertSingleListener() => Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
        private static IEnumerator Wait(Func<bool> predicate, string reason, float timeout)
        { double deadline = Time.realtimeSinceStartupAsDouble + timeout; while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null; Assert.That(predicate(), Is.True, reason + "超时"); }
    }
}
#endif
