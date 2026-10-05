#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.BlockPorters;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class BlockPortersFlowTests
    {
        private BlockPortersLevel stressAsset;
        private BlockPortersLevel completionAsset;
        private BlockPortersLevel carrierAsset;
        private BlockPortersLevel paletteAsset;
        private GameViewResolution resolution;
        private readonly List<BlockPortersLevelCatalog> testCatalogs = new();

        [UnityTest]
        public IEnumerator ThemesAlignTilesAndRestartPreservesAppliedBackground()
        {
            yield return EnterHub(); Click(UIManager.Instance.Get<MainMenuView>(), "BlockPortersButton");
            yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersHudView>()?.State == ViewState.Visible, "主题 HUD");
            var controller = Object.FindFirstObjectByType<BlockPortersController>();
            resolution = new GameViewResolution(540, 960);
            yield return WaitUntil(() => Screen.width == 540 && Screen.height == 960, "主题竖屏");
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<BlockPortersThemeCatalog>("Assets/LoadResources/Demos/block_porters/Data/ThemeCatalog.asset");
            controller.LoadLevel(1); yield return null;
            foreach (var theme in catalog.Themes)
            {
                var loading = controller.ApplyThemeAsync(theme).AsTask();
                yield return WaitUntil(() => loading.IsCompleted, "加载 " + theme.DisplayName);
                Assert.That(loading.Result, Is.True); Assert.That(controller.CurrentThemeId, Is.EqualTo(theme.Id));
                yield return Capture("Theme_" + theme.Id);
                controller.Restart(); yield return null; yield return null;
                Assert.That(controller.CurrentThemeId, Is.EqualTo(theme.Id), "重开必须保持主题");
                var renderer = (Renderer)typeof(BlockPortersController).GetField("backgroundRenderer", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                var properties = new MaterialPropertyBlock(); renderer.GetPropertyBlock(properties);
                Assert.That(properties.GetTexture("_BaseMap").name, Is.EqualTo(theme.BackgroundAddress.Split('/').Last()));
                Assert.That(properties.GetVector("_BaseMap_ST").x, Is.GreaterThan(0), "主题不能覆盖安全区 UV 参数");
            }
            string previous = controller.CurrentThemeId;
            controller.LoadLevel(2);
            yield return WaitUntil(() => controller.CurrentThemeId != previous, "新关排除上一主题");
            previous = controller.CurrentThemeId;
            resolution.Dispose(); resolution = new GameViewResolution(540, 1200);
            yield return WaitUntil(() => Screen.height == 1200, "主题长屏");
            yield return Capture("Theme_Tall");
            controller.ReturnToHub();
            yield return WaitUntil(() => Object.FindFirstObjectByType<BlockPortersController>() == null, "主题退出");
            yield return EnterHub();
            Click(UIManager.Instance.Get<MainMenuView>(), "BlockPortersButton");
            yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersHudView>()?.State == ViewState.Visible, "再次进入");
            var reentered = Object.FindFirstObjectByType<BlockPortersController>();
            yield return WaitUntil(() => reentered.CurrentThemeId != null, "再次进入主题加载");
            Assert.That(reentered.CurrentThemeId, Is.Not.EqualTo(previous));
            // 新关成功应用主题后才成为跨 Hub 的上一主题。
        }

        [UnityTest]
        public IEnumerator FiveColumnsAndIndependentExtraSlotsRespectRewardsAndRestart()
        {
            yield return EnterHub();
            Click(UIManager.Instance.Get<MainMenuView>(), "BlockPortersButton");
            yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersHudView>()?.State == ViewState.Visible, "五列 HUD");
            var controller = Object.FindFirstObjectByType<BlockPortersController>();
            var hud = UIManager.Instance.Get<BlockPortersHudView>();
            resolution = new GameViewResolution(540, 960);
            yield return WaitUntil(() => Screen.width == 540 && Screen.height == 960, "五列竖屏");
            controller.LoadLevel(1); yield return null; yield return null;
            Assert.That(controller.Session.ColumnCount, Is.EqualTo(5));
            var images = hud.gameObject.GetComponentsInChildren<Image>(true);
            Assert.That(images.Count(i => i.name.StartsWith("Preview")), Is.EqualTo(15));
            foreach (var image in images.Where(i => i.name.StartsWith("Preview")))
            { Assert.That(image.raycastTarget, Is.False); Assert.That(image.GetComponent<Button>(), Is.Null); }
            yield return Capture("FiveColumnsInitial");
            var queue = hud.gameObject.GetComponentsInChildren<Button>(true).Single(b => b.name == "Queue4");
            Click(hud, "Queue4"); queue.onClick.Invoke();
            Assert.That(controller.Session.Teams.Count, Is.EqualTo(1));
            Assert.That(queue.interactable, Is.False);
            yield return new WaitForSecondsRealtime(.3f);
            yield return Capture("FiveColumnsCarrying");
            var reward = new PendingReward(); controller.SetRewardProvider(reward);
            Click(hud, "TaskSlot6"); controller.RequestUnlockSlot(1);
            Assert.That(reward.Calls, Is.EqualTo(1)); Assert.That(reward.LastSide, Is.EqualTo(1));
            reward.Complete(PorterRewardResult.Canceled); yield return null; yield return null;
            Assert.That(controller.Session.Capacity, Is.EqualTo(5));
            reward.Reset(); Click(hud, "TaskSlot6"); reward.Complete(PorterRewardResult.Unavailable);
            yield return null; yield return null; Assert.That(controller.Session.UnlockedExtraSlots, Is.Zero);
            reward.Reset(); Click(hud, "TaskSlot6"); reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null;
            Assert.That(controller.Session.UnlockedExtraSlots, Is.EqualTo(2)); Assert.That(controller.Session.Capacity, Is.EqualTo(6));
            Assert.That(images.Single(i => i.name == "ExtraSlotIcon1").gameObject.activeSelf, Is.False);
            Assert.That(images.Single(i => i.name == "ExtraSlotIcon0").gameObject.activeSelf, Is.True);
            yield return Capture("FiveColumnsRightUnlocked");
            int index = 0;
            while (controller.Session.Teams.Count < 6)
            { Assert.That(index, Is.LessThan(20)); controller.Dispatch(index++ % 5); }
            Assert.That(controller.Session.Teams.Any(t => t.Slot == 6), Is.True);
            Assert.That(controller.Session.Teams.Any(t => t.Slot == 5), Is.False);
            reward.Reset(); controller.RequestUnlockSlot(0); reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null;
            Assert.That(controller.Session.Capacity, Is.EqualTo(7));
            controller.Dispatch(0); yield return null; yield return null;
            Assert.That(controller.Session.Teams.Any(t => t.Slot == 5), Is.True);
            yield return Capture("FiveColumnsBothUnlocked");
            controller.RequestUnlockSlot(1); Assert.That(reward.Calls, Is.EqualTo(4));
            controller.Restart(); yield return null; yield return null;
            Assert.That(controller.Session.Capacity, Is.EqualTo(5)); Assert.That(controller.ActorCount, Is.Zero);
            reward.Reset(); controller.RequestUnlockSlot(1); controller.Restart(); reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null; Assert.That(controller.Session.UnlockedExtraSlots, Is.Zero);
            resolution.Dispose(); resolution = new GameViewResolution(540, 1200);
            yield return WaitUntil(() => Screen.height == 1200, "五列长屏");
            yield return Capture("FiveColumnsTall");
            controller.ReturnToHub();
            yield return WaitUntil(() => Object.FindFirstObjectByType<BlockPortersController>() == null, "五列返回清理");
        }

        [UnityTest]
        public IEnumerator ProgressAndQueueAdvanceStayAlignedAcrossPauseAndRestart()
        {
            yield return EnterHub();
            Click(UIManager.Instance.Get<MainMenuView>(), "BlockPortersButton");
            yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersHudView>()?.State == ViewState.Visible, "进度 HUD 启动");
            var controller = Object.FindFirstObjectByType<BlockPortersController>();
            var hud = UIManager.Instance.Get<BlockPortersHudView>();
            var queue = hud.gameObject.GetComponentsInChildren<Button>(true).Single(b => b.name == "Queue0");
            var queueRect = (RectTransform)queue.transform;
            Vector2 rest = queueRect.anchoredPosition;
            Click(hud, "Queue0");
            queue.onClick.Invoke();
            Assert.That(controller.Session.Teams.Count, Is.EqualTo(1), "同帧重复事件只派出一队");
            Assert.That(queue.interactable, Is.False, "递补时锁住同列");
            Assert.That(queueRect.anchoredPosition.y, Is.LessThan(rest.y));
            Click(hud, "Pause"); yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersSettingsView>()?.State == ViewState.Visible, "设置页显示");
            Assert.That(Vector2.Distance(queueRect.anchoredPosition, rest), Is.LessThan(.001f), "暂停清理递补动画");
            Click(UIManager.Instance.Get<BlockPortersSettingsView>(), "SettingsContinue"); yield return null;
            Click(hud, "Queue0"); controller.Restart(); yield return null; yield return null;
            Assert.That(Vector2.Distance(queueRect.anchoredPosition, rest), Is.LessThan(.001f), "重开清理移动卡片");
            Assert.That(queue.interactable, Is.True);

            resolution = new GameViewResolution(540, 960);
            yield return WaitUntil(() => Screen.width == 540 && Screen.height == 960, "9:16 进度验收");
            controller.LoadLevel(1); yield return null; yield return null;
            var fill = hud.gameObject.GetComponentsInChildren<Image>(true).Single(i => i.name == "ProgressFill");
            Assert.That(fill.fillAmount, Is.Zero);
            Assert.That(controller.Session.Total, Is.EqualTo(256));
            yield return Capture("Progress0");
            var scheduler = GlobalData.Get<BlockPortersData>().Scheduler;
            bool quarter = false, half = false;
            // 手动逐事件推进时暂停自动Update，避免同一帧再次推进跳过采样点。
            var automaticPause = typeof(BlockPortersController).GetField("isApplicationPaused", BindingFlags.Instance | BindingFlags.NonPublic);
            bool wasApplicationPaused = (bool)automaticPause.GetValue(controller);
            automaticPause.SetValue(controller, true);
            try
            {
                foreach (int column in controller.CurrentLevel.Solution)
                {
                    controller.Dispatch(column);
                    while (!scheduler.IsStable)
                    {
                        // 同刻交付会成批处理；跨过四分之一和半程时按实际交付数验收。
                        double at = scheduler.Transports.Min(t => t.Job.IsPickedUp ? t.Delivered : t.Pickup);
                        scheduler.AdvanceTo(at);
                        int delivered = controller.Session.Delivered;
                        if ((delivered >= 64 && !quarter) || (delivered >= 128 && !half))
                        {
                            controller.TogglePause();
                            yield return new WaitForSecondsRealtime(.3f);
                            Assert.That(fill.fillAmount, Is.EqualTo(delivered / 256f).Within(.001));
                            Assert.That(fill.rectTransform.rect.width, Is.LessThan(fill.transform.parent.parent.GetComponent<RectTransform>().rect.width));
                            yield return Capture(!quarter ? "Progress25" : "Progress50");
                            if (!quarter)
                            {
                                quarter = true;
                                resolution.Dispose(); resolution = new GameViewResolution(540, 1200);
                                yield return WaitUntil(() => Screen.height == 1200, "长屏进度验收");
                                yield return Capture("Progress25Tall");
                                resolution.Dispose(); resolution = new GameViewResolution(540, 960);
                                yield return WaitUntil(() => Screen.height == 960, "恢复进度竖屏");
                            }
                            else half = true;
                            controller.TogglePause();
                        }
                        yield return null;
                    }
                }
            }
            finally { if (controller != null) automaticPause.SetValue(controller, wasApplicationPaused); }
            Assert.That(quarter && half, Is.True, "回放必须实际覆盖四分之一和半程的交付进度：" + $"delivered={controller.Session.Delivered},status={controller.Session.Status},quarter={quarter},half={half},solution={controller.CurrentLevel.Solution.Length}");
            Assert.That(controller.Session.Status, Is.EqualTo(BlockPortersStatus.Won));
            yield return null; yield return null;
            Assert.That(fill.fillAmount, Is.EqualTo(1), "通关立即满格");
            yield return Capture("Progress100");
            controller.Restart(); yield return null; yield return null;
            Assert.That(fill.fillAmount, Is.Zero, "重开立即归零");
            Click(hud, "Queue0");
            yield return new WaitForSecondsRealtime(.3f);
            // Canvas 缩放后的 RectTransform 读回值可能有亚像素浮点误差。
            Assert.That(Vector2.Distance(queueRect.anchoredPosition, rest), Is.LessThan(.001f));
            Assert.That(queue.interactable, Is.True, "递补后解除同列锁");
            Assert.That(hud.gameObject.GetComponentsInChildren<Image>(true).Single(i => i.name == "Preview0").gameObject.activeSelf, Is.True);
            controller.LoadLevel(0); yield return null; yield return null;
            Assert.That(fill.fillAmount, Is.Zero, "切关立即归零");
            Assert.That(Vector2.Distance(queueRect.anchoredPosition, rest), Is.LessThan(.001f));
            controller.ReturnToHub();
            yield return WaitUntil(() => Object.FindFirstObjectByType<BlockPortersController>() == null, "进度验收返回");
        }

        [UnityTest]
        public IEnumerator SettingsRestorePauseBlockInputAndTwelveColorsRender()
        {
            yield return EnterHub();
            var hubLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled).ToArray();
            Click(UIManager.Instance.Get<MainMenuView>(), "BlockPortersButton");
            yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersHudView>()?.State == ViewState.Visible, "HUD 启动");
            var controller = Object.FindFirstObjectByType<BlockPortersController>();
            Assert.That(hubLights.All(light => !light.enabled), Is.True, "Demo 内暂停其他场景灯光");
            var hud = UIManager.Instance.Get<BlockPortersHudView>();
            resolution = new GameViewResolution(540, 960);
            yield return WaitUntil(() => Screen.width == 540 && Screen.height == 960, "竖屏");
            Click(hud, "Pause"); yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersSettingsView>()?.State == ViewState.Visible, "设置页显示");
            Assert.That(controller.IsPaused, Is.True);
            var settings = UIManager.Instance.Get<BlockPortersSettingsView>().gameObject.GetComponentsInChildren<Transform>(true).Single(t => t.name == "SettingsPanel");
            Assert.That(settings.gameObject.activeInHierarchy, Is.True);
            var queue = hud.gameObject.GetComponentsInChildren<Button>(true).Single(b => b.name == "Queue0");
            Assert.That(queue.interactable, Is.False);
            yield return Capture("Settings");
            var canvas = queue.GetComponentInParent<Canvas>().rootCanvas;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            { position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, queue.transform.position) }, hits);
            Assert.That(hits.First().gameObject.transform.IsChildOf(settings), Is.True, "设置页拦截前排点击");
            yield return Capture("Settings");
            resolution.Dispose(); resolution = new GameViewResolution(540, 1200);
            yield return WaitUntil(() => Screen.height == 1200, "长屏设置"); yield return null; yield return null;
            hits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current)
            { position = new Vector2(Screen.width - 40, Screen.height - 24) }, hits);
            Assert.That(hits.First().gameObject.name, Is.EqualTo("SettingsPanel"), "遮罩覆盖长屏留白和公共画质入口");
            yield return Capture("SettingsTall");
            Click(UIManager.Instance.Get<BlockPortersSettingsView>(), "Sound"); Assert.That(controller.IsMuted, Is.True);
            Click(UIManager.Instance.Get<BlockPortersSettingsView>(), "Sound"); Assert.That(controller.IsMuted, Is.False);
            Click(UIManager.Instance.Get<BlockPortersSettingsView>(), "SettingsContinue"); yield return null;
            Assert.That(controller.IsPaused, Is.False); yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersSettingsView>() == null, "设置页释放");
            controller.TogglePause();

            resolution.Dispose(); resolution = new GameViewResolution(540, 960);
            yield return WaitUntil(() => Screen.height == 960, "恢复竖屏");
            Click(hud, "Pause"); yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersSettingsView>()?.State == ViewState.Visible, "设置页显示");
            Click(UIManager.Instance.Get<BlockPortersSettingsView>(), "SettingsClose"); yield return null;
            Assert.That(controller.IsPaused, Is.True, "关闭设置不能撤销之前已有的暂停");
            controller.TogglePause();

            var palette = Enumerable.Range(0, 12).Select(i => Color.HSVToRGB(i / 12f, .68f, .88f)).ToArray();
            palette[11] = new Color(.08f, .12f, .22f);
            var cells = Enumerable.Range(0, 144).Select(i => i % 12).ToArray();
            var queues = Enumerable.Range(0, 4).Select(c => Enumerable.Range(0, 6)
                .Select(i => new PorterTeamDefinition(c + i % 3 * 4, 6)).ToArray()).ToArray();
            var solution = Enumerable.Range(0, 24).Select(i => i % 4).ToArray();
            paletteAsset = ScriptableObject.CreateInstance<BlockPortersLevel>();
            paletteAsset.Configure("十二色搬运", new BlockPortersLevelData(12, 12, cells, queues, 5, 12), palette, solution);
            SetLevels(controller, new[] { paletteAsset }); controller.LoadLevel(0); yield return null;
            var materials = (Material[])typeof(BlockPortersController).GetField("levelMaterials", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.That(materials.Length, Is.EqualTo(12));
            for (int i = 0; i < 12; i++) Assert.That(Vector4.Distance(materials[i].color, palette[i]), Is.LessThan(.00001f), "共享材质保持关卡色表");
            yield return Capture("TwelveColors");
            var scheduler = GlobalData.Get<BlockPortersData>().Scheduler;
            foreach (int column in solution)
            {
                controller.Dispatch(column);
                while (!scheduler.IsStable) { double nextEvent = scheduler.Transports.Min(item => item.Job.IsPickedUp ? item.Delivered : item.Pickup);
                        scheduler.AdvanceTo(nextEvent); yield return null; }
                Assert.That(controller.Session.Status, Is.Not.EqualTo(BlockPortersStatus.Failed));
            }
            Assert.That(controller.Session.Status, Is.EqualTo(BlockPortersStatus.Won));
            controller.Restart(); yield return null;
            Assert.That(controller.Session.Delivered, Is.Zero);
            controller.ReturnToHub();
            yield return WaitUntil(() => Object.FindFirstObjectByType<BlockPortersController>() == null, "返回清理");
            Assert.That(hubLights.All(light => light != null && light.enabled), Is.True, "返回 Hub 恢复原灯光");
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator GeneratedCatalogReferencePlaybackUsesRuntimeActors()
        {
            yield return EnterHub();
            Click(UIManager.Instance.Get<MainMenuView>(), "BlockPortersButton");
            yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersHudView>()?.State == ViewState.Visible, "正式关卡集启动");
            var controller = Object.FindFirstObjectByType<BlockPortersController>();
            var catalog = (BlockPortersLevelCatalog)typeof(BlockPortersController).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            Assert.That(catalog.Levels.Length, Is.GreaterThanOrEqualTo(8));
            var authored = catalog.Levels;
            var materialsField = typeof(BlockPortersController).GetField("levelMaterials", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int index = 0; index < authored.Length; index++)
            {
                var previousMaterials = (Material[])materialsField.GetValue(controller);
                controller.LoadLevel(index);
                yield return Capture(authored[index].name);
                Assert.That(previousMaterials.All(material => material == null), Is.True, "切关销毁旧共享材质");
                var scheduler = GlobalData.Get<BlockPortersData>().Scheduler;
                bool carried = false;
                foreach (int column in authored[index].Solution)
                {
                    controller.Dispatch(column);
                    Assert.That(controller.Session.Status, Is.Not.EqualTo(BlockPortersStatus.Failed));
                    while (!scheduler.IsStable)
                    {
                        // 推进到下一搬运事件；角色仍由正式控制器处理事件和逐帧姿态，不改规则或 Time.timeScale。
                        double nextEvent = scheduler.Transports.Min(item => item.Job.IsPickedUp ? item.Delivered : item.Pickup);
                        scheduler.AdvanceTo(nextEvent);
                        carried |= Object.FindObjectsByType<PorterAvatar>(FindObjectsSortMode.None).Any(a => a.CarryAnchor.childCount > 0);
                        Assert.That(controller.ActorCount, Is.EqualTo(controller.Session.InFlight));
                        Assert.That(controller.ActorCount, Is.LessThanOrEqualTo(56));
                        yield return null;
                    }
                }
                Assert.That(carried, Is.True);
                Assert.That(controller.Session.Status, Is.EqualTo(BlockPortersStatus.Won), authored[index].name);
                Assert.That(controller.ActorCount, Is.Zero);
                controller.NextLevel();
                Assert.That(controller.Session.Delivered, Is.Zero);
            }
            var exitingMaterials = (Material[])materialsField.GetValue(controller);
            controller.ReturnToHub();
            yield return WaitUntil(() => Object.FindFirstObjectByType<BlockPortersController>() == null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub, "关卡集返回清理");
            yield return null;
            Assert.That(exitingMaterials.All(material => material == null), Is.True, "退出销毁当前关卡材质");
        }

        [UnityTest]
        public IEnumerator HubDispatchPauseRestartRewardAndReturn()
        {
            yield return EnterHub();
            Click(UIManager.Instance.Get<MainMenuView>(), "BlockPortersButton");
            yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersHudView>()?.State == ViewState.Visible, "小小搬豆工 HUD");
            var controller = Object.FindFirstObjectByType<BlockPortersController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(GameSceneNavigator.Instance.CurrentScene, Is.EqualTo(GameSceneId.BlockPorters));
            yield return null;
            resolution = new GameViewResolution(540, 960);
            yield return WaitUntil(() => Screen.width == 540 && Screen.height == 960, "9:16 分辨率");
            yield return Capture("Portrait");
            var hud = UIManager.Instance.Get<BlockPortersHudView>();
            int expected = controller.Session.Peek(0).Value.Count;
            Click(hud, "Queue0");
            Assert.That(controller.ActorCount, Is.EqualTo(expected));
            yield return new WaitForSeconds(0.6f);
            Click(hud, "Pause");
            int delivered = controller.Session.Delivered;
            var position = Object.FindObjectsByType<PorterAvatar>(FindObjectsSortMode.None).First().transform.position;
            yield return new WaitForSeconds(0.25f);
            Assert.That(controller.Session.Delivered, Is.EqualTo(delivered));
            Assert.That(Object.FindObjectsByType<PorterAvatar>(FindObjectsSortMode.None).First().transform.position, Is.EqualTo(position));
            Click(UIManager.Instance.Get<BlockPortersSettingsView>(), "SettingsContinue");
            yield return WaitUntil(() => Object.FindObjectsByType<PorterAvatar>(FindObjectsSortMode.None)
                .Any(avatar => avatar.CarryAnchor.childCount > 0), "抬砖姿态");
            yield return Capture("Carrying");
            yield return WaitUntil(() => controller.Session.Delivered == expected, "第一队全部跳坑", 30);
            Assert.That(controller.ActorCount, Is.Zero);
            Assert.That(controller.Session.Teams, Is.Empty);
            Click(hud, "Pause");
            yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersSettingsView>()?.State == ViewState.Visible, "重开设置页");
            Click(UIManager.Instance.Get<BlockPortersSettingsView>(), "Restart");
            Assert.That(controller.Session.Delivered, Is.Zero);
            Assert.That(controller.ActorCount, Is.Zero);
            Assert.That(controller.Session.Peek(0).Value.Count, Is.EqualTo(expected));
            resolution.Dispose(); resolution = new GameViewResolution(540, 1200);
            yield return WaitUntil(() => Screen.height == 1200, "长竖屏分辨率");
            yield return Capture("TallPortrait");

            var originalLevels = ((BlockPortersLevelCatalog)typeof(BlockPortersController).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller)).Levels;
            var partial = ScriptableObject.CreateInstance<BlockPortersLevel>();
            partial.Configure("部分成员等待", new BlockPortersLevelData(3, 3, new[] { 0, 0, 0, 1, 0, 1, 1, 1, 1 },
                new[] { new[] { new PorterTeamDefinition(0, 4) }, new[] { new PorterTeamDefinition(1, 5) },
                    Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() }, 5, 2),
                controller.CurrentLevel.Palette.Take(2).ToArray(), new[] { 0, 1 });
            SetLevels(controller, new[] { partial });
            controller.LoadLevel(0); controller.Dispatch(0);
            Assert.That(controller.ActorCount, Is.EqualTo(3), "只能生成已预约到方块的三人");
            controller.Dispatch(1);
            yield return WaitUntil(() => controller.Session.Status == BlockPortersStatus.Won, "开路后剩余成员自动搬运", 30);
            SetLevels(controller, originalLevels);
            controller.LoadLevel(0);
            Object.Destroy(partial);

            completionAsset = ScriptableObject.CreateInstance<BlockPortersLevel>();
            completionAsset.Configure("最后一块验收", new BlockPortersLevelData(1, 1, new[] { 0 },
                new[] { new[] { new PorterTeamDefinition(0, 1) }, Array.Empty<PorterTeamDefinition>(),
                    Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() }, 5, 1),
                controller.CurrentLevel.Palette.Take(1).ToArray(), new[] { 0 });
            var authoredLevels = originalLevels;
            SetLevels(controller, new[] { completionAsset, authoredLevels[1] });
            controller.LoadLevel(0); controller.Dispatch(0);
            yield return WaitUntil(() => controller.Session.Status == BlockPortersStatus.Won, "最后一块入坑后通关", 30);
            Assert.That(controller.ActorCount, Is.Zero);
            yield return Capture("Win");
            Click(UIManager.Instance.Get<BlockPortersResultView>(), "Next");
            Assert.That(controller.LevelIndex, Is.EqualTo(1));
            Assert.That(controller.CurrentLevel, Is.SameAs(authoredLevels[1]));
            Assert.That(controller.Session.Delivered, Is.Zero);

            stressAsset = CreateStressLevel(controller.CurrentLevel.Palette);
            SetLevels(controller, new[] { stressAsset });
            controller.LoadLevel(0);
            Assert.That(controller.Session.Total, Is.EqualTo(1024));
            foreach (int column in new[] { 0, 1, 2, 3, 0 }) controller.Dispatch(column);
            yield return WaitUntil(() => controller.Session.Status == BlockPortersStatus.Failed, "五队堵满");
            Assert.That(controller.ActorCount, Is.Zero);
            var reward = new PendingReward();
            controller.SetRewardProvider(reward);
            controller.RequestUnlockSlot(0); controller.RequestUnlockSlot(0);
            Assert.That(reward.Calls, Is.EqualTo(1), "快速连点不能重复请求奖励。");
            reward.Complete(PorterRewardResult.Canceled);
            yield return null; yield return null;
            Assert.That(controller.Session.Capacity, Is.EqualTo(5));
            reward.Reset();
            controller.RequestUnlockSlot(0); reward.Complete(PorterRewardResult.Unavailable);
            yield return null; yield return null;
            Assert.That(controller.Session.Status, Is.EqualTo(BlockPortersStatus.Failed));
            reward.Reset();
            controller.RequestUnlockSlot(0); reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null;
            Assert.That(controller.Session.Capacity, Is.EqualTo(6));
            reward.Reset(); controller.RequestUnlockSlot(1); reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null;
            Assert.That(controller.Session.Capacity, Is.EqualTo(7));
            controller.RequestUnlockSlot(0);
            Assert.That(reward.Calls, Is.EqualTo(4));
            controller.Dispatch(0); controller.Dispatch(1);
            Assert.That(controller.ActorCount, Is.EqualTo(8), "复活后只激活能搬外围砖的队伍");
            yield return Capture("BlockedWaiting");
            carrierAsset = CreateCarrierStressLevel(controller.CurrentLevel.Palette);
            SetLevels(controller, new[] { carrierAsset }); controller.LoadLevel(0);
            foreach (int column in new[] { 0, 1, 2, 3, 0 }) controller.Dispatch(column);
            yield return WaitUntil(() => controller.Session.Status == BlockPortersStatus.Failed, "封闭图案五队等待");
            Assert.That(controller.ActorCount, Is.Zero);
            reward.Reset(); controller.RequestUnlockSlot(0); reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null;
            reward.Reset(); controller.RequestUnlockSlot(1); reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null;
            controller.Dispatch(0); controller.Dispatch(1);
            Assert.That(controller.ActorCount, Is.EqualTo(16));
            yield return WaitUntil(() => controller.ActorCount == 56, "开路后56人真实在途");
            Assert.That(controller.Session.InFlight, Is.EqualTo(56));
            yield return Capture("Stress56Transport");
            float elapsed = 0, longest = 0;
            int frames = 0;
            double memoryBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime; longest = Mathf.Max(longest, Time.unscaledDeltaTime); frames++;
            }
            Debug.Log($"[BlockPorters 验收] 32×32 棋盘/峰值56人在途：{frames / elapsed:F1} FPS，最长帧 {longest * 1000:F1} ms，主线程累计分配 {GC.GetAllocatedBytesForCurrentThread() - memoryBefore:F0} bytes（含 Editor/TestRunner/UI）。");
            Assert.That(controller.ActorCount, Is.LessThanOrEqualTo(56));
            controller.Restart();
            Assert.That(controller.ActorCount, Is.Zero);
            Assert.That(controller.Session.UnlockedExtraSlots, Is.Zero);
            foreach (int column in new[] { 0, 1, 2, 3, 0 }) controller.Dispatch(column);
            yield return WaitUntil(() => controller.Session.Status == BlockPortersStatus.Failed, "第二次堵满");
            reward.Reset(); controller.RequestUnlockSlot(0); controller.Restart();
            reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null;
            Assert.That(controller.Session.UnlockedExtraSlots, Is.Zero, "旧关卡奖励不能复活新会话。");
            Assert.That(controller.Session.Capacity, Is.EqualTo(5));
            resolution.Dispose(); resolution = null;
            controller.ReturnToHub();
            yield return WaitUntil(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub &&
                UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "返回 Hub");
            Assert.That(Object.FindFirstObjectByType<BlockPortersController>(), Is.Null);
            Assert.That(Object.FindObjectsByType<PorterAvatar>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(UIManager.Instance.Get<BlockPortersHudView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<BlockPortersSettingsView>(), Is.Null);
            Assert.That(UIManager.Instance.Get<BlockPortersResultView>(), Is.Null);
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener => listener.enabled), Is.EqualTo(1));
            yield return Capture("HubReturn");
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            resolution?.Dispose(); resolution = null;
            var controller = Object.FindFirstObjectByType<BlockPortersController>();
            if (controller != null)
            {
                controller.ReturnToHub();
                yield return WaitUntil(() => Object.FindFirstObjectByType<BlockPortersController>() == null, "清理 Demo", 45);
            }
            foreach (var catalog in testCatalogs) Object.Destroy(catalog);
            testCatalogs.Clear();
            if (stressAsset != null) Object.Destroy(stressAsset);
            if (completionAsset != null) Object.Destroy(completionAsset);
            if (carrierAsset != null) Object.Destroy(carrierAsset);
            if (paletteAsset != null) Object.Destroy(paletteAsset);
        }

        private static IEnumerator EnterHub()
        {
            // 同一个 PlayMode 运行内启动壳保持存活，不能绕过导航重载其场景。
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                while (!startup.isDone) yield return null;
            }
            yield return WaitUntil(() => GameSceneNavigator.Instance?.CurrentScene == GameSceneId.Hub &&
                !GameSceneNavigator.Instance.IsTransitioning &&
                UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "Hub 启动 / 复用");
            yield return null;
        }

        private void SetLevels(BlockPortersController controller, BlockPortersLevel[] definitions)
        {
            var catalog = ScriptableObject.CreateInstance<BlockPortersLevelCatalog>(); catalog.Configure(definitions); testCatalogs.Add(catalog);
            typeof(BlockPortersController).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, catalog);
        }

        private static BlockPortersLevel CreateCarrierStressLevel(Color[] palette)
        {
            var cells = Enumerable.Repeat(-1, 1024).ToArray();
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                if (x == 0 || x == 31 || y == 0 || y == 31) cells[y * 32 + x] = 0;
            for (int y = 15; y <= 16; y++) for (int x = 6; x < 26; x++) cells[y * 32 + x] = 1;
            var queues = Enumerable.Range(0, 4).Select(_ => new List<PorterTeamDefinition>()).ToArray();
            queues[0].AddRange(Enumerable.Repeat(new PorterTeamDefinition(1, 8), 2));
            for (int col = 1; col < 4; col++) queues[col].Add(new PorterTeamDefinition(1, 8));
            queues[0].Add(new PorterTeamDefinition(0, 8));
            int remaining = 116;
            while (remaining > 0) { int n = Math.Min(8, remaining); queues[1].Add(new PorterTeamDefinition(0, n)); remaining -= n; }
            var level = ScriptableObject.CreateInstance<BlockPortersLevel>();
            level.Configure("56人运输压力", new BlockPortersLevelData(32, 32, cells, queues.Select(c => c.ToArray()).ToArray(), 5, 2), palette.Take(2).ToArray(), Array.Empty<int>());
            return level;
        }

        private static BlockPortersLevel CreateStressLevel(Color[] palette)
        {
            var cells = new int[1024];
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) cells[y * 32 + x] = x == 0 || x == 31 || y == 0 || y == 31 ? 0 : 1;
            var columns = Enumerable.Range(0, 4).Select(_ => new List<PorterTeamDefinition>()).ToArray();
            columns[0].AddRange(Enumerable.Repeat(new PorterTeamDefinition(1, 8), 3));
            for (int i = 1; i < 4; i++) columns[i].Add(new PorterTeamDefinition(1, 8));
            int red = 124; while (red > 0) { int count = Math.Min(8, red); columns[1].Add(new PorterTeamDefinition(0, count)); red -= count; }
            int green = 852; while (green > 0) { int count = Math.Min(8, green); columns[0].Add(new PorterTeamDefinition(1, count)); green -= count; }
            var data = new BlockPortersLevelData(32, 32, cells, columns.Select(column => column.ToArray()).ToArray(), 5, 2);
            var level = ScriptableObject.CreateInstance<BlockPortersLevel>();
            level.Configure("性能与复活验收", data, palette.Take(2).ToArray(), Array.Empty<int>());
            return level;
        }

        private static void Click(View view, string name)
        {
            // Hub 已使用稳定 Key 的循环卡片；玩法控件仍按保存的固定节点定位。
            bool isDemoEntry = view is MainMenuView && name == "BlockPortersButton";
            var button = isDemoEntry
                ? view.gameObject.GetComponentsInChildren<LoopScrollMenuButton>(true).Single(item =>
                    item.GetComponentInParent<SleepyStudios.LoopScroll.LoopCell>().Context.IsCurrent &&
                    item.GetComponentInParent<SleepyStudios.LoopScroll.LoopCell>().Context.Key == "block_porters")
                : view.gameObject.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
            Assert.That(button.interactable, Is.True, name + " 不可交互");
            button.onClick.Invoke();
            // 鼠标/触屏点击卡片只选择；键盘/手柄确认可能已经开始导航。
            if (isDemoEntry && !GameSceneNavigator.Instance.IsTransitioning) Click(view, "Start");
        }
        private static IEnumerator WaitUntil(Func<bool> predicate, string reason, float timeout = 20)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!predicate()) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), reason + " 超时"); yield return null; }
        }
        private static IEnumerator Capture(string name)
        {
            yield return null;
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            string folder = Path.GetFullPath("Library/BlockPorters/Evidence"); Directory.CreateDirectory(folder);
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
        }
        private sealed class PendingReward : IBlockPortersReward
        {
            private UniTaskCompletionSource<PorterRewardResult> completion = new();
            public int Calls { get; private set; }
            public int LastSide { get; private set; }
            public UniTask<PorterRewardResult> RequestExtraSlotAsync(int side, CancellationToken token) { Calls++; LastSide = side; return completion.Task.AttachExternalCancellation(token); }
            public void Complete(PorterRewardResult result) => completion.TrySetResult(result);
            public void Reset() => completion = new UniTaskCompletionSource<PorterRewardResult>();
        }
    }
}
#endif
