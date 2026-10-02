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
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// P1保存资源的正式入口回归；专有配置和存档目录不修改场景资产、用户存档或画质偏好。
    public sealed class JinxCasinoAdventureFlowTests
    {
        private readonly List<JinxCasinoGameSettings> configurations = new List<JinxCasinoGameSettings>();
        private GameViewResolution resolution;
        private JinxCasinoController controller;
        private JinxCasinoAdventurePresenter presenter;
        private JinxCasinoHudView hud;
        private CasinoLocalSaveStore store;
        private string saveDirectory;

        [UnityTest]
        public IEnumerator SavedButtonsConfirmResumeBuyUseAndRestoreAllThreeSlots()
        {
            yield return EnterP1();
            Configure(config => { config.EventIntervalMilliseconds = 0; config.Targets = new long[] { 10000 }; });
            yield return Screenshot("P1Menu");
            Click(Field<Button>(presenter, "standardButton"));
            yield return Wait(() => controller.HasAdventure, "标准冒险菜单按钮", 5);
            Assert.That(controller.AdventureState.Mode, Is.EqualTo(CasinoAdventureMode.Standard));
            controller.StartAdventure(CasinoAdventureMode.Standard, 1);
            yield return null;

            Click(Field<Button>(presenter, "shopButton"));
            yield return null;
            var cards = presenter.GetComponentsInChildren<JinxCasinoItemCard>().Where(card => card.gameObject.activeInHierarchy).ToArray();
            CollectionAssert.AreEquivalent(new[] { "bubble_gun", "extra_time", "stop_loss" }, cards.Select(card => card.ItemId));
            foreach (string id in new[] { "bubble_gun", "extra_time", "stop_loss" })
            {
                var card = cards.Single(value => value.ItemId == id);
                Click(Field<Button>(card, "purchaseButton"));
                yield return Wait(() => Inventory(id) == 1, "保存商店按钮购买 " + id, 5);
                yield return null;
                int remaining = controller.AdventureState.RemainingMilliseconds;
                Click(Field<Button>(card, "useButton"));
                yield return null;
                if (id == "stop_loss") Assert.That(controller.AdventureState.PreparedItems, Does.Contain(id));
                else Assert.That(Inventory(id), Is.Zero);
                if (id == "extra_time") Assert.That(controller.AdventureState.RemainingMilliseconds, Is.GreaterThan(remaining + 29000));
                if (id == "bubble_gun") Assert.That(controller.AdventureState.Effects.Any(effect => effect.EffectKind == "Bubble"), Is.True);
            }
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(730));
            yield return Screenshot("P1Shop");
            Click(Field<Button>(presenter, "shopCloseButton"));
            yield return null;
            presenter.ShowStation(CasinoGameKind.CoinFlip);
            SetText(Field<object>(presenter, "stakeInput"), "999999");
            SetText(Field<object>(presenter, "choiceInput"), "2");
            yield return null;
            Click(Field<Button>(presenter, "confirmBetButton"));
            long failedAt = controller.AdventureState.ElapsedMilliseconds;
            yield return null;
            Assert.That(controller.HasActiveAdventureRound, Is.False);
            Assert.That(Text(Field<object>(presenter, "machineResult")), Does.Contain("最高投入"));
            yield return Wait(() => controller.AdventureState.ElapsedMilliseconds >= failedAt + 200, "失败提示经过自动HUD刷新", 5);
            Assert.That(Text(Field<object>(presenter, "machineResult")), Does.Contain("最高投入"));
            SetText(Field<object>(presenter, "stakeInput"), "100");
            yield return null;
            Click(Field<Button>(presenter, "confirmBetButton"));
            yield return Wait(() => controller.HasActiveAdventureRound, "改投入后新请求编号成功提交", 5);
            Assert.That(controller.AdventureState.LockedCoins, Is.EqualTo(100));
            Assert.That(Inventory("stop_loss"), Is.Zero);
            Assert.That(controller.AdventureState.PreparedItems, Is.Empty);
            yield return null;
            ClickAction(CasinoMiniGameAction.GuessHeads);
            yield return null;
            Assert.That(controller.HasActiveAdventureRound, Is.True, "种子1的首枚连胜硬币为正面，赢后须明确收手。");
            string round = controller.AdventureState.ActiveRoundJson;
            Assert.That(controller.GetAdventureActions().Any(action => action.Kind == CasinoMiniGameAction.CashOut), Is.True);
            yield return Screenshot("P1CoinChain");
            Click(Field<Button>(presenter, "machineCloseButton"));
            yield return null;
            Assert.That(controller.HasActiveAdventureRound, Is.True);
            Assert.That(controller.IsAdventureInputBlocked, Is.False);
            Click(Field<Button>(presenter, "resumeRoundButton"));
            yield return null;
            Assert.That(controller.AdventureState.ActiveRoundJson, Is.EqualTo(round), "关闭面板不能取消或重开机台。");
            Click(Field<Button>(presenter, "machineCloseButton"));
            yield return null;
            Click(Field<Button>(presenter, "slotsButton"));
            yield return null;
            var saves = Field<Button[]>(presenter, "saveButtons");
            for (int index = 0; index < 3; index++)
            {
                Click(saves[index]); yield return null;
                Assert.That(store.GetInfo(index + 1).Error, Is.Null);
                Assert.That(store.Load(index + 1).State.ActiveRoundJson, Is.EqualTo(round));
            }
            yield return Screenshot("P1ThreeSlots");
            Click(Field<Button>(presenter, "slotsCloseButton")); yield return null;
            Click(Field<Button>(presenter, "resumeRoundButton")); yield return null;
            ClickAction(CasinoMiniGameAction.CashOut); yield return null;
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(830));

            for (int index = 0; index < 3; index++)
            {
                Click(Field<Button>(presenter, "machineCloseButton")); yield return null;
                Click(Field<Button>(presenter, "slotsButton")); yield return null;
                Click(Field<Button[]>(presenter, "loadButtons")[index]); yield return null;
                Assert.That(controller.SelectedSaveSlot, Is.EqualTo(index + 1));
                Assert.That(controller.AdventureState.Coins, Is.EqualTo(730));
                Assert.That(controller.AdventureState.LockedCoins, Is.EqualTo(100));
                Assert.That(controller.AdventureState.ActiveRoundJson, Is.EqualTo(round));
                Click(Field<Button>(presenter, "resumeRoundButton")); yield return null;
                ClickAction(CasinoMiniGameAction.CashOut); yield return null;
                Assert.That(controller.AdventureState.Coins, Is.EqualTo(830), "恢复后的同一局仅结算一次。");
            }
            yield return ExitViaPanelButton();
        }

        [UnityTest]
        public IEnumerator EventChoiceButtonsAndActualChipRainTriggersAwardMissionOnce()
        {
            yield return EnterP1();
            Configure(config => { config.Targets = new long[] { 10000 }; config.EventIntervalMilliseconds = 2000; OnlyEvent(config, "mystery_merchant"); });
            controller.StartAdventure(CasinoAdventureMode.Standard, 27);
            yield return Wait(() => controller.AdventureState.EventChoicePending && Field<GameObject>(presenter, "eventPanel").activeInHierarchy, "神秘商人保存事件面板", 10);
            Assert.That(controller.IsAdventureInputBlocked, Is.True);
            Assert.That(Field<Button[]>(presenter, "eventButtons").Length, Is.EqualTo(2));
            yield return Screenshot("P1EventChoice");
            long coins = controller.AdventureState.Coins;
            int count = controller.AdventureState.Inventory.Sum(item => item.Count);
            Click(Field<Button[]>(presenter, "eventButtons")[1]);
            // 同步按钮提交即验证本次事件；下一帧可能合法触发新的周期事件，不能将它误判成旧选择未关闭。
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(coins - 80));
            Assert.That(controller.AdventureState.Inventory.Sum(item => item.Count), Is.EqualTo(count + 1));
            Assert.That(controller.AdventureState.Inventory.All(item => Array.IndexOf(controller.AdventureState.Config.ShopItemIds, item.ItemId) >= 0), Is.True);
            Assert.That(controller.AdventureState.EventChoicePending, Is.False);
            yield return null;

            Configure(config => { config.Targets = new long[] { 10000 }; config.EventIntervalMilliseconds = 5000; OnlyEvent(config, "chip_rain"); });
            controller.StartAdventure(CasinoAdventureMode.Standard, 43);
            yield return Wait(() => controller.AdventureState.ActiveMission?.EventId == "chip_rain" &&
                Object.FindObjectsByType<JinxCasinoMissionTarget>(FindObjectsSortMode.None).Length == 5, "筹码雨保存目标实例生成", 15);
            var mission = controller.AdventureState.ActiveMission;
            string missionId = mission.Id;
            long beforeReward = controller.AdventureState.Coins;
            Assert.That(Text(Field<object>(presenter, "missionText")), Does.Contain("0/5"));
            var body = Field<CharacterController>(controller, "body");
            Assert.That(controller.IsLocalAdventureActor(body.transform), Is.True);
            var targets = Object.FindObjectsByType<JinxCasinoMissionTarget>(FindObjectsSortMode.None).OrderBy(target => Field<int>(target, "index")).ToArray();
            for (int index = 0; index < targets.Length; index++)
            {
                var target = targets[index];
                Assert.That(target.GetComponent<SphereCollider>().isTrigger, Is.True);
                Vector3 outside = target.transform.position + new Vector3(2, -0.7f, 0);
                body.enabled = false; body.transform.position = outside; body.enabled = true;
                Physics.SyncTransforms(); yield return null;
                // 真实CharacterController穿过保存的Trigger，不用SendMessage或直接领域发奖代替回调。
                body.Move(Vector3.left * 2); Physics.SyncTransforms();
                int expected = index + 1;
                yield return Wait(() => controller.AdventureState.ActiveMission?.Id == missionId && controller.AdventureState.ActiveMission.Progress >= expected,
                    "本地玩家实际进入第" + expected + "个任务Trigger", 5);
                if (expected < targets.Length) Assert.That(controller.AdventureState.Coins, Is.EqualTo(beforeReward));
            }
            Assert.That(controller.AdventureState.ActiveMission.Completed, Is.True);
            Assert.That(controller.AdventureState.ActiveMission.Visited.Distinct().Count(), Is.EqualTo(5));
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(beforeReward + mission.RewardCoins));
            Assert.That(Text(Field<object>(presenter, "missionText")), Does.Contain("5/5"));
            yield return null; yield return null;
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(beforeReward + mission.RewardCoins));
            yield return Screenshot("P1ChipRainCompleted");
            Click(Field<Button>(presenter, "slotsButton")); yield return null;
            yield return ExitViaPanelButton();
        }

        [UnityTest]
        public IEnumerator SingleStageFinaleHidesUnavailableVaultAndSavedEndingButtonReturnsHub()
        {
            yield return EnterP1();
            Configure(config => { config.Targets = new long[] { 900 }; config.EventIntervalMilliseconds = 0; });
            controller.StartAdventure(CasinoAdventureMode.Standard, 57); yield return null;
            Click(Field<Button>(presenter, "finishStageButton"));
            yield return Wait(() => controller.AdventureState.Phase == CasinoAdventurePhase.Finale, "一阶段额度结算", 5);
            Assert.That(Field<GameObject>(presenter, "endingPanel").activeInHierarchy, Is.True);
            Assert.That(Field<Button>(presenter, "vaultChallengeButton").gameObject.activeInHierarchy, Is.False);
            Assert.That(Field<Button>(presenter, "takeoverButton").gameObject.activeInHierarchy, Is.False);
            yield return Screenshot("P1Finale");
            Click(Field<Button>(presenter, "leaveEndingButton")); yield return null;
            Assert.That(controller.AdventureState.Phase, Is.EqualTo(CasinoAdventurePhase.Ended));
            Assert.That(controller.AdventureState.Ending, Is.EqualTo(CasinoAdventureEnding.LeaveWithDignity));
            yield return ExitViaPanelButton();
        }

        [UnityTest]
        public IEnumerator FailedStageWithdrawsAndSavedPanelBackReleasesHud()
        {
            yield return EnterP1();
            Configure(config => { config.Targets = new long[] { 10000 }; config.StageDurationMilliseconds = 3000; config.EventIntervalMilliseconds = 0; });
            controller.StartAdventure(CasinoAdventureMode.Standard, 61);
            yield return Wait(() => controller.AdventureState.Phase == CasinoAdventurePhase.Failed, "真实时钟进入未达标失败", 10);
            Assert.That(Field<GameObject>(presenter, "endingPanel").activeInHierarchy, Is.True);
            Assert.That(Field<Button>(presenter, "withdrawButton").gameObject.activeInHierarchy, Is.True);
            yield return Screenshot("P1Failed");
            Click(Field<Button>(presenter, "withdrawButton")); yield return null;
            Assert.That(controller.AdventureState.Ending, Is.EqualTo(CasinoAdventureEnding.Withdraw));
            Assert.That(controller.AdventureState.Phase, Is.EqualTo(CasinoAdventurePhase.Ended));
            yield return ExitViaPanelButton();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                var live = Object.FindFirstObjectByType<JinxCasinoController>();
                if (live != null)
                {
                    yield return Wait(() => live == null || !live.IsBusy, "清理在途P1操作", 15);
                    if (live != null) live.RequestExit();
                    yield return Wait(() => live == null && IsStableHub(), "清理P1并回到Hub", 30);
                }
            }
            finally
            {
                resolution?.Dispose(); resolution = null;
                foreach (var settings in configurations) if (settings != null) Object.Destroy(settings);
                configurations.Clear();
                // 仅删除本用例创建的GUID目录；Demo尚活着时保留证据，避免退出自动保存与删除并发。
                if (Object.FindFirstObjectByType<JinxCasinoController>() == null && !string.IsNullOrEmpty(saveDirectory))
                {
                    string allowed = Path.GetFullPath("Library/JinxCasino/TestSaves") + Path.DirectorySeparatorChar;
                    string resolved = Path.GetFullPath(saveDirectory);
                    Assert.That(resolved.StartsWith(allowed, StringComparison.OrdinalIgnoreCase), Is.True);
                    if (Directory.Exists(resolved)) Directory.Delete(resolved, true);
                }
            }
        }

        private IEnumerator EnterP1()
        {
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                Assert.That(startup, Is.Not.Null);
                yield return Wait(() => startup.isDone, "唯一正式启动入口", 90);
            }
            yield return Wait(IsStableHub, "正式Hub稳定", 90);
            Assert.That(GameSceneNavigator.Instance.IsEditorDirect, Is.False);
            resolution = new GameViewResolution(1280, 720);
            yield return Wait(() => Screen.width == 1280 && Screen.height == 720, "P1真实720p GameView", 15);
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask();
            yield return Wait(() => travel.IsCompleted, "正式导航P1场景", 30);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning,
                "保存HUD初始化", 30);
            controller = Object.FindFirstObjectByType<JinxCasinoController>(); hud = UIManager.Instance.Get<JinxCasinoHudView>();
            presenter = hud.gameObject.GetComponentInChildren<JinxCasinoAdventurePresenter>(true);
            Assert.That(presenter != null && controller != null, Is.True);
            // P1交互回归使用独立范围配置；正式场景扩展到四区后不再锁死生产StageCount。
            Configure(config => { });
            saveDirectory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TestSaves", "P1Flow-" + Guid.NewGuid().ToString("N")));
            store = new CasinoLocalSaveStore(saveDirectory); controller.SetLocalSaveStore(store);
            controller.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory, "Profile")));
            AssertSingleListener(); yield return null; yield return null;
        }

        private void Configure(Action<CasinoAdventureConfig> change)
        {
            var clone = Object.Instantiate(Field<JinxCasinoGameSettings>(controller, "gameSettings")); configurations.Add(clone);
            var config = clone.CreateConfig();
            config.StageCount = 1;
            config.AllowedGames = new[] { CasinoGameKind.Slots, CasinoGameKind.Roulette, CasinoGameKind.CoinFlip };
            config.ShopItemIds = new[] { "bubble_gun", "extra_time", "stop_loss" };
            change(config); SetField(clone, "adventure", config);
            controller.ConfigureAdventure(clone, Field<JinxCasinoWorldArea[]>(controller, "areas"), Field<JinxCasinoSceneEffects>(controller, "sceneEffects"));
        }

        private static void OnlyEvent(CasinoAdventureConfig config, string id)
            => config.EventWeights = CasinoContentCatalog.Events.Select(entry => new CasinoEventWeight { EventId = entry.Id, Weight = entry.Id == id ? 1 : 0 }).ToArray();
        private int Inventory(string id) => controller.AdventureState.Inventory.Find(item => item.ItemId == id)?.Count ?? 0;
        private void ClickAction(CasinoMiniGameAction kind)
        {
            int index = Array.FindIndex(controller.GetAdventureActions(), action => action.Kind == kind);
            Assert.That(index, Is.GreaterThanOrEqualTo(0)); Click(Field<Button[]>(presenter, "actionButtons")[index]);
        }

        private IEnumerator ExitViaPanelButton()
        {
            // 结算刷新会重新启用控件，等待Canvas实际绘制后再验证中心射线。
            yield return null;
            yield return new WaitForEndOfFrame();
            var button = Field<Button[]>(presenter, "panelBackButtons").Single(value => value.gameObject.activeInHierarchy);
            yield return Screenshot("P4PanelExit");
            var oldHud = hud; var oldController = controller;
            Click(button);
            yield return Wait(() => oldController == null && IsStableHub(), "保存面板返回按钮回到Hub", 30);
            Assert.That(oldHud.State, Is.EqualTo(ViewState.Destroyed)); Assert.That(oldHud.gameObject == null, Is.True);
            Assert.That(UIManager.Instance.Get<JinxCasinoHudView>(), Is.Null); AssertSingleListener();
        }

        private static void Click(Button button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True, "必须点击保存的可用按钮。");
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
            Vector2 point = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center));
            // 合成指针使用屏幕像素坐标，避免中心投影的小数误差落入原生矩形判定的离散空隙。
            point = new Vector2(Mathf.Round(point.x), Mathf.Round(point.y));
            var data = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
            Assert.That(hits.Count, Is.GreaterThan(0));
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), "按钮中心不得被模态层或全局Overlay遮挡，实际命中：" + hits[0].gameObject.name);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        // Tests.PlayMode不引入TMP程序集，反射保存组件的公开text属性仍触发真实输入onValueChanged绑定。
        private static string Text(object component) => (string)component.GetType().GetProperty("text").GetValue(component);
        private static void SetText(object component, string value) => component.GetType().GetProperty("text").SetValue(component, value);
        private static T Field<T>(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "保存字段不存在：" + name); return (T)field.GetValue(owner);
        }
        private static void SetField(object owner, string name, object value)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null); field.SetValue(owner, value);
        }
        private static bool IsStableHub() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub &&
            !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        private static void AssertSingleListener() => Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.isActiveAndEnabled), Is.EqualTo(1));
        private static IEnumerator Wait(Func<bool> predicate, string reason, float timeout)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, reason + " 超时。");
        }
        private static IEnumerator Screenshot(string name)
        {
            yield return null; yield return null; yield return null;
            string directory = Path.GetFullPath("Library/JinxCasino/Verification"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name + "-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Guid.NewGuid().ToString("N") + ".png");
            ScreenCapture.CaptureScreenshot(path); yield return null; yield return null;
            yield return Wait(() => File.Exists(path) && new FileInfo(path).Length > 8, "P1截图落盘", 15);
            byte[] bytes = File.ReadAllBytes(path); Assert.That(bytes[0], Is.EqualTo(137)); Assert.That(bytes[1], Is.EqualTo((byte)'P'));
            Debug.Log("[JinxCasinoAdventureFlowTests] 720p正式UI证据：" + path);
        }
    }
}
#endif
