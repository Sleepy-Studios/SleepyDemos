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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 同任务回退必须刷新真实触发器；本地交流只产生本机反馈和保存模型，不碰资金或虚构网络消息。
    public sealed class JinxCasinoMissionRestoreAndSocialTests
    {
        private JinxCasinoController controller;
        private JinxCasinoAdventurePresenter presenter;
        private JinxCasinoHudView hud;
        private CharacterController body;
        private JinxCasinoGameSettings settings;
        private GameViewResolution resolution;
        private string directory;

        [UnityTest, Timeout(180000)]
        public IEnumerator EarlierChipRainSaveReactivatesActuallyCollectibleTriggerWithoutDoubleReward()
        { yield return MissionRestore("chip_rain"); }

        [UnityTest, Timeout(180000)]
        public IEnumerator EarlierGoldDeliverySaveAllowsRealPickupAndDeliveryAgainWithoutDoubleReward()
        { yield return MissionRestore("gold_delivery"); }

        private IEnumerator MissionRestore(string eventId)
        {
            yield return Enter(eventId); Click(Field<Button>(presenter, "standardButton")); yield return null;
            Assert.That(controller.PurchaseItem("event_remote").Success, Is.True); yield return null;
            Assert.That(controller.UseAdventureItem("event_remote").Success, Is.True); yield return null;
            yield return Wait(() => controller.AdventureState.ActiveMission?.EventId == eventId && Targets().Length > 0, "真实任务保存模板生成", 5);
            string taskId = controller.AdventureState.ActiveMission.Id; long beforeReward = controller.AdventureState.Coins;
            Assert.That(controller.SaveAdventure(1), Is.True);
            var oldTarget = Target(0); yield return Touch(oldTarget);
            Assert.That(eventId == "chip_rain" ? controller.AdventureState.ActiveMission.Progress == 1 : controller.AdventureState.ActiveMission.Carrying, Is.True);
            Assert.That(controller.LoadAdventure(1), Is.True); yield return null;
            Assert.That(controller.AdventureState.ActiveMission.Id, Is.EqualTo(taskId));
            Assert.That(controller.AdventureState.ActiveMission.Progress, Is.Zero); Assert.That(controller.AdventureState.ActiveMission.Carrying, Is.False);
            Assert.That(oldTarget == null || !oldTarget.gameObject.activeInHierarchy, Is.True, "恢复先停用旧目标，延迟Destroy不能再次提交");
            var renewed = Target(0); yield return Touch(renewed);
            Assert.That(eventId == "chip_rain" ? controller.AdventureState.ActiveMission.Progress == 1 : controller.AdventureState.ActiveMission.Carrying, Is.True,
                "恢复前成功过的同ID目标仍必须接受实际CharacterController触发，不能保留submitted锁");
            if (eventId == "chip_rain")
                for (int point = 1; point < 5; point++) yield return Touch(Target(point));
            else yield return Touch(Target(1));
            Assert.That(controller.AdventureState.ActiveMission.Completed, Is.True);
            long expected = beforeReward + controller.AdventureState.ActiveMission.RewardCoins;
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(expected)); Assert.That(controller.SaveAdventure(2), Is.True);
            Assert.That(controller.LoadAdventure(2), Is.True); yield return null; yield return null;
            Assert.That(Targets(), Is.Empty); Assert.That(controller.AdventureState.Coins, Is.EqualTo(expected));
            Assert.That(controller.AdventureState.ActiveMission.Completed, Is.True);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator SavedSocialControlsThrottleLocalFeedbackAndMaintainRealMarkerWithoutPayments()
        {
            yield return Enter(); Click(Field<Button>(presenter, "practiceButton")); yield return null;
            var fixedStation = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None)
                .Where(value => value.Game == CasinoGameKind.Roulette && value.GetComponent<JinxCasinoRotationStand>() == null).First();
            Place(fixedStation.InteractionPosition + Vector3.up * 0.03f); yield return null;
            long coins = controller.AdventureState.Coins; int requests = controller.AdventureState.ProcessedRequests.Count;
            Click(Field<Button>(presenter, "socialButton")); yield return null;
            var social = Field<JinxCasinoSocialPresenter>(presenter, "socialPresenter"); var consumer = controller.GetComponent<JinxCasinoLocalSocialFeedback>();
            Assert.That(consumer, Is.Not.Null);
            var dropdown = Field<object>(social, "messageDropdown"); CollectionAssert.AreEqual(CasinoLocalMessages.Labels, Labels(dropdown));
            SetValue(dropdown, 1); Click(Field<Button>(social, "sendButton")); yield return null;
            Assert.That(consumer.VisibleMessage, Does.Contain("单人提示：我需要帮忙"));
            Click(Field<Button>(social, "markButton")); yield return null;
            Assert.That(consumer.Marker, Is.Null); Assert.That(consumer.Status, Does.Contain("一秒"), "消息/标记共享节流，不假成功");
            yield return new WaitForSecondsRealtime(1.05f); Click(Field<Button>(social, "markButton")); yield return null;
            var marker = consumer.Marker; Assert.That(marker, Is.Not.Null); Assert.That(consumer.MarkedStation, Is.SameAs(fixedStation));
            var mount = fixedStation.transform.Find("FormalVisual"); var actualPosition = mount != null ? mount.position : fixedStation.InteractionPosition;
            Assert.That(Vector2.Distance(new Vector2(actualPosition.x, actualPosition.z), Vector2.zero), Is.GreaterThan(0.5f), "轮盘实际位置远离共用根原点，不能弱化错误居中的回归");
            Assert.That(Vector2.Distance(new Vector2(marker.position.x, marker.position.z), new Vector2(actualPosition.x, actualPosition.z)), Is.LessThan(0.1f));
            Assert.That(marker.position.y, Is.GreaterThan(actualPosition.y + 0.5f));
            Assert.That(marker.GetComponentsInChildren<Collider>(true), Is.Empty); Assert.That(marker.GetComponentsInChildren<Camera>(true), Is.Empty); Assert.That(marker.GetComponentsInChildren<AudioListener>(true), Is.Empty);
            Click(Field<Button>(social, "closeButton")); yield return null;
            Assert.That(marker.gameObject.activeInHierarchy, Is.True, "返回场地才能看见世界标记，关闭模态不销毁它");
            Click(Field<Button>(presenter, "socialButton")); yield return null; Click(Field<Button>(social, "clearButton"));
            Assert.That(marker == null || !marker.gameObject.activeSelf, Is.True); Assert.That(consumer.Marker, Is.Null); yield return null;
            Place(controller.CurrentAdventureSafePosition + Vector3.up * 10); yield return null;
            Click(Field<Button>(social, "markButton")); yield return null;
            Assert.That(consumer.Marker, Is.Null); Assert.That(consumer.Status, Does.Contain("暂无附近机台"));
            Assert.That(controller.AdventureState.Coins, Is.EqualTo(coins)); Assert.That(controller.AdventureState.ProcessedRequests.Count, Is.EqualTo(requests));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator NewRunRestoreAndViewExitClearSocialMarkerWithoutReplay()
        {
            yield return Enter(); Click(Field<Button>(presenter, "practiceButton")); yield return null;
            var station = Object.FindObjectsByType<JinxCasinoStation>(FindObjectsSortMode.None).First(value => value.Game == CasinoGameKind.Roulette && value.GetComponent<JinxCasinoRotationStand>() == null);
            Place(station.InteractionPosition + Vector3.up * 0.03f); yield return null;
            Assert.That(controller.MarkNearbyStation(), Is.True); var consumer = controller.GetComponent<JinxCasinoLocalSocialFeedback>(); var first = consumer.Marker;
            Assert.That(controller.SaveAdventure(1), Is.True); Assert.That(controller.LoadAdventure(1), Is.True);
            Assert.That(consumer.Marker, Is.Null); Assert.That(first == null || !first.gameObject.activeSelf, Is.True); yield return null;
            Place(station.InteractionPosition + Vector3.up * 0.03f); yield return null;
            Assert.That(controller.MarkNearbyStation(), Is.True); var second = consumer.Marker;
            controller.StartAdventure(CasinoAdventureMode.Practice); yield return null;
            Assert.That(consumer.Marker, Is.Null); Assert.That(second == null || !second.gameObject.activeSelf, Is.True);
            Place(station.InteractionPosition + Vector3.up * 0.03f); yield return null;
            Assert.That(controller.MarkNearbyStation(), Is.True); var third = consumer.Marker;
            Click(Field<Button>(presenter, "socialButton")); yield return null;
            var previous = controller; var previousHud = hud;
            Click(Field<Button[]>(presenter, "panelBackButtons").Single(button => button.gameObject.activeInHierarchy));
            yield return Wait(() => previous == null && IsStableHub(), "正式UI与场景生命周期退出", 45);
            Assert.That(previousHud.State, Is.EqualTo(ViewState.Destroyed)); Assert.That(third == null, Is.True, "世界标记原生对象必须已销毁");
            Assert.That(Object.FindObjectsByType<JinxCasinoLocalSocialFeedback>(FindObjectsSortMode.None), Is.Empty);
        }

        private JinxCasinoMissionTarget[] Targets() => Object.FindObjectsByType<JinxCasinoMissionTarget>(FindObjectsSortMode.None);
        private JinxCasinoMissionTarget Target(int index) => Targets().Single(value => Field<int>(value, "index") == index);
        private IEnumerator Touch(JinxCasinoMissionTarget target)
        {
            var mission = controller.AdventureState.ActiveMission; int progress = mission.Progress; bool carrying = mission.Carrying;
            Assert.That(target.GetComponent<Collider>().isTrigger, Is.True);
            Place(target.transform.position + new Vector3(2.2f, -0.7f, 0)); yield return null;
            body.Move(Vector3.left * 2.2f); Physics.SyncTransforms();
            yield return Wait(() => controller.AdventureState.ActiveMission?.Id == mission.Id &&
                (controller.AdventureState.ActiveMission.Progress != progress || controller.AdventureState.ActiveMission.Carrying != carrying), "真实触发任务目标", 5);
        }
        private void Place(Vector3 position)
        { body.enabled = false; body.transform.position = position; body.enabled = true; Physics.SyncTransforms(); }

        private IEnumerator Enter(string eventId = null)
        {
            if (GameSceneNavigator.Instance == null)
            { var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single); yield return Wait(() => startup.isDone, "正式启动", 90); }
            yield return Wait(IsStableHub, "正式Hub", 90); resolution = new GameViewResolution(1280, 720);
            yield return Wait(() => Screen.width == 1280 && Screen.height == 720, "720p GameView", 15);
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask(); yield return Wait(() => travel.IsCompleted, "正式赌场", 45);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(() => UIManager.Instance.Get<JinxCasinoHudView>()?.State == ViewState.Visible && !GameSceneNavigator.Instance.IsTransitioning, "正式保存HUD", 30);
            controller = Object.FindFirstObjectByType<JinxCasinoController>(); hud = UIManager.Instance.Get<JinxCasinoHudView>();
            presenter = hud.gameObject.GetComponentInChildren<JinxCasinoAdventurePresenter>(true); body = Field<CharacterController>(controller, "body");
            var groups = hud.gameObject.GetComponentsInParent<CanvasGroup>(true); yield return Wait(() => groups.All(value => value.alpha >= 0.99f), "Core入场淡出完成", 2);
            settings = Object.Instantiate(Field<JinxCasinoGameSettings>(controller, "gameSettings"));
            var config = settings.CreateConfig(); config.StageCount = 1; config.Targets = new long[] { 10000 }; config.EventIntervalMilliseconds = 0;
            config.ShopItemIds = Array.Empty<string>(); config.AllowedGames = Array.Empty<CasinoGameKind>();
            if (eventId != null) config.EventWeights = CasinoContentCatalog.Events.Select(value => new CasinoEventWeight { EventId = value.Id, Weight = value.Id == eventId ? 1 : 0 }).ToArray();
            SetField(settings, "adventure", config);
            controller.ConfigureAdventure(settings, Field<JinxCasinoWorldArea[]>(controller, "areas"), Field<JinxCasinoSceneEffects>(controller, "sceneEffects"));
            directory = Path.GetFullPath(Path.Combine("Library/JinxCasino/TestSaves", "MissionSocial-" + Guid.NewGuid().ToString("N")));
            controller.SetLocalSaveStore(new CasinoLocalSaveStore(directory)); controller.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(directory, "Profile"))); yield return null;
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                var live = Object.FindFirstObjectByType<JinxCasinoController>();
                if (live != null) { live.RequestExit(); yield return Wait(() => live == null && IsStableHub(), "回归清理并返回Hub", 45); }
            }
            finally
            {
                resolution?.Dispose(); if (settings != null) Object.Destroy(settings);
                if (!string.IsNullOrEmpty(directory) && Object.FindFirstObjectByType<JinxCasinoController>() == null)
                {
                    string allowed = Path.GetFullPath("Library/JinxCasino/TestSaves") + Path.DirectorySeparatorChar;
                    Assert.That(directory.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) && Path.GetFileName(directory).StartsWith("MissionSocial-", StringComparison.Ordinal), Is.True);
                    if (Directory.Exists(directory)) Directory.Delete(directory, true);
                }
            }
        }
        private static void Click(Button button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True); Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button, Is.True, "保存按钮中心射线必须无遮挡");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private static void SetValue(object component, int value) => component.GetType().GetProperty("value").SetValue(component, value);
        private static string[] Labels(object dropdown) => ((IEnumerable)dropdown.GetType().GetProperty("options").GetValue(dropdown)).Cast<object>().Select(value => (string)value.GetType().GetProperty("text").GetValue(value)).ToArray();
        private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner);
        private static void SetField(object owner, string name, object value) => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static bool IsStableHub() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        private static IEnumerator Wait(Func<bool> predicate, string reason, float seconds)
        { double deadline = Time.realtimeSinceStartupAsDouble + seconds; while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null; Assert.That(predicate(), Is.True, reason + "超时"); }
    }
}
#endif
