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
using Hotfix.BlockPorters.Adapters;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class BlockPortersFlowTests
    {
        private BlockPortersLevel stressAsset;
        private BlockPortersLevel completionAsset;
        private GameViewResolution resolution;

        [UnityTest]
        public IEnumerator HubDispatchPauseRestartRewardAndReturn()
        {
            var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
            while (!startup.isDone) yield return null;
            yield return WaitUntil(() => UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "Hub 启动");
            Click(UIManager.Instance.Get<MainMenuView>(), "BlockPortersButton");
            yield return WaitUntil(() => UIManager.Instance.Get<BlockPortersHudView>()?.State == ViewState.Visible, "小人搬砖 HUD");
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
            Click(hud, "Pause");
            yield return WaitUntil(() => Object.FindObjectsByType<PorterAvatar>(FindObjectsSortMode.None)
                .Any(avatar => avatar.CarryAnchor.childCount > 0), "抬砖姿态");
            yield return Capture("Carrying");
            yield return WaitUntil(() => controller.Session.Delivered == expected, "第一队全部跳坑", 30);
            Assert.That(controller.ActorCount, Is.Zero);
            Assert.That(controller.Session.Teams, Is.Empty);
            Click(hud, "Restart");
            Assert.That(controller.Session.Delivered, Is.Zero);
            Assert.That(controller.ActorCount, Is.Zero);
            Assert.That(controller.Session.Peek(0).Value.Count, Is.EqualTo(expected));
            resolution.Dispose(); resolution = new GameViewResolution(540, 1200);
            yield return WaitUntil(() => Screen.height == 1200, "长竖屏分辨率");
            yield return Capture("TallPortrait");

            var originalLevels = (BlockPortersLevel[])typeof(BlockPortersController).GetField("levels", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
            var partial = ScriptableObject.CreateInstance<BlockPortersLevel>();
            partial.Configure("部分成员等待", new BlockPortersLevelData(3, 3, new[] { 0, 0, 0, 1, 0, 1, 1, 1, 1 },
                new[] { new[] { new PorterTeamDefinition(0, 4) }, new[] { new PorterTeamDefinition(1, 5) },
                    Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() }, 5, 2),
                controller.CurrentLevel.Palette.Take(2).ToArray(), new[] { 0, 1 });
            typeof(BlockPortersController).GetField("levels", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, new[] { partial });
            controller.LoadLevel(0); controller.Dispatch(0);
            Assert.That(controller.ActorCount, Is.EqualTo(3), "只能生成已预约到方块的三人");
            controller.Dispatch(1);
            yield return WaitUntil(() => controller.Session.Status == BlockPortersStatus.Won, "开路后剩余成员自动搬运", 30);
            typeof(BlockPortersController).GetField("levels", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(controller, originalLevels);
            controller.LoadLevel(0);
            Object.Destroy(partial);

            completionAsset = ScriptableObject.CreateInstance<BlockPortersLevel>();
            completionAsset.Configure("最后一块验收", new BlockPortersLevelData(1, 1, new[] { 0 },
                new[] { new[] { new PorterTeamDefinition(0, 1) }, Array.Empty<PorterTeamDefinition>(),
                    Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() }, 5, 1),
                controller.CurrentLevel.Palette.Take(1).ToArray(), new[] { 0 });
            var levelsField = typeof(BlockPortersController).GetField("levels", BindingFlags.Instance | BindingFlags.NonPublic);
            var authoredLevels = (BlockPortersLevel[])levelsField.GetValue(controller);
            levelsField.SetValue(controller, new[] { completionAsset, authoredLevels[1] });
            controller.LoadLevel(0); controller.Dispatch(0);
            yield return WaitUntil(() => controller.Session.Status == BlockPortersStatus.Won, "最后一块入坑后通关", 30);
            Assert.That(controller.ActorCount, Is.Zero);
            yield return Capture("Win");
            Click(hud, "Next");
            Assert.That(controller.LevelIndex, Is.EqualTo(1));
            Assert.That(controller.CurrentLevel, Is.SameAs(authoredLevels[1]));
            Assert.That(controller.Session.Delivered, Is.Zero);

            stressAsset = CreateStressLevel(controller.CurrentLevel.Palette);
            typeof(BlockPortersController).GetField("levels", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(controller, new[] { stressAsset });
            controller.LoadLevel(0);
            Assert.That(controller.Session.Total, Is.EqualTo(1024));
            foreach (int column in new[] { 0, 1, 2, 3, 0 }) controller.Dispatch(column);
            yield return WaitUntil(() => controller.Session.Status == BlockPortersStatus.Failed, "五队堵满");
            Assert.That(controller.ActorCount, Is.Zero);
            var reward = new PendingReward();
            controller.SetRewardProvider(reward);
            controller.RequestRevive(); controller.RequestRevive();
            Assert.That(reward.Calls, Is.EqualTo(1), "快速连点不能重复请求奖励。");
            reward.Complete(PorterRewardResult.Canceled);
            yield return null; yield return null;
            Assert.That(controller.Session.Capacity, Is.EqualTo(5));
            reward.Reset();
            controller.RequestRevive(); reward.Complete(PorterRewardResult.Unavailable);
            yield return null; yield return null;
            Assert.That(controller.Session.Status, Is.EqualTo(BlockPortersStatus.Failed));
            reward.Reset();
            controller.RequestRevive(); reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null;
            Assert.That(controller.Session.Capacity, Is.EqualTo(7));
            controller.RequestRevive();
            Assert.That(reward.Calls, Is.EqualTo(3));
            controller.Dispatch(0); controller.Dispatch(1);
            Assert.That(controller.ActorCount, Is.EqualTo(8), "复活后只激活能搬外围砖的队伍");
            yield return Capture("BlockedWaiting");
            float elapsed = 0, longest = 0;
            int frames = 0;
            double memoryBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                elapsed += Time.unscaledDeltaTime; longest = Mathf.Max(longest, Time.unscaledDeltaTime); frames++;
            }
            Debug.Log($"[BlockPorters 验收] 1024 方块/逻辑等待队伍：{frames / elapsed:F1} FPS，最长帧 {longest * 1000:F1} ms，主线程累计分配 {GC.GetAllocatedBytesForCurrentThread() - memoryBefore:F0} bytes（含 Editor/TestRunner/UI）。");
            Assert.That(controller.ActorCount, Is.LessThanOrEqualTo(56));
            controller.Restart();
            Assert.That(controller.ActorCount, Is.Zero);
            Assert.That(controller.Session.HasRevived, Is.False);
            foreach (int column in new[] { 0, 1, 2, 3, 0 }) controller.Dispatch(column);
            yield return WaitUntil(() => controller.Session.Status == BlockPortersStatus.Failed, "第二次堵满");
            reward.Reset(); controller.RequestRevive(); controller.Restart();
            reward.Complete(PorterRewardResult.Completed);
            yield return null; yield return null;
            Assert.That(controller.Session.HasRevived, Is.False, "旧关卡奖励不能复活新会话。");
            Assert.That(controller.Session.Capacity, Is.EqualTo(5));
            resolution.Dispose(); resolution = null;
            controller.ReturnToHub();
            yield return WaitUntil(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub &&
                UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "返回 Hub");
            Assert.That(Object.FindFirstObjectByType<BlockPortersController>(), Is.Null);
            Assert.That(Object.FindObjectsByType<PorterAvatar>(FindObjectsSortMode.None), Is.Empty);
            Assert.That(UIManager.Instance.Get<BlockPortersHudView>(), Is.Null);
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
                yield return WaitUntil(() => Object.FindFirstObjectByType<BlockPortersController>() == null, "清理 Demo");
            }
            if (stressAsset != null) Object.Destroy(stressAsset);
            if (completionAsset != null) Object.Destroy(completionAsset);
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
            var button = view.gameObject.GetComponentsInChildren<Button>(true).Single(item => item.name == name);
            Assert.That(button.interactable, Is.True, name + " 不可交互");
            button.onClick.Invoke();
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
            public UniTask<PorterRewardResult> RequestReviveAsync(CancellationToken token) { Calls++; return completion.Task.AttachExternalCancellation(token); }
            public void Complete(PorterRewardResult result) => completion.TrySetResult(result);
            public void Reset() => completion = new UniTaskCompletionSource<PorterRewardResult>();
        }
    }
}
#endif
