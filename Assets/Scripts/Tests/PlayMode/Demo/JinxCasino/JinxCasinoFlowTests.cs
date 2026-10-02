#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using Core.Runtime;
using Core.Runtime.Networking;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 正式入口的离线会话与场景生命周期回归；截图只是运行证据，不代表互联网或视觉验收。
    public sealed class JinxCasinoFlowTests
    {
        private GameViewResolution resolution;

        [UnityTest]
        public IEnumerator FormalStartupOfflineBetLeaveAndReentryCreateIndependentRun()
        {
            yield return EnterFormalHub();
            resolution = new GameViewResolution(1280, 720);
            yield return WaitUntil(() => Screen.width == 1280 && Screen.height == 720, "切换 P0 720p 验证分辨率", 15);
            yield return EnterCasino();

            var firstController = Object.FindFirstObjectByType<JinxCasinoController>();
            var firstHud = UIManager.Instance.Get<JinxCasinoHudView>();
            Assert.That(firstController != null, Is.True, "正式场景必须有 Controller。");
            Assert.That(firstHud, Is.Not.Null);
            Assert.That(firstHud.gameObject != null && firstHud.gameObject.activeInHierarchy, Is.True);
            Assert.That(firstController.HasRun, Is.False, "进入场景不能隐式连接或沿用旧局。");
            Assert.That(firstController.TransportKind.HasValue, Is.False);
            Assert.That(firstController.Balance, Is.Zero);
            AssertSingleAudioListener();

            firstController.StartOffline("P0生命周期验证");
            yield return WaitUntil(() => !firstController.IsBusy && firstController.HasRun, "显式创建单人离线局", 15);
            Assert.That(firstController.TransportKind, Is.EqualTo(NetworkTransportKind.Offline));
            Assert.That(firstController.Balance, Is.EqualTo(1000));
            Assert.That(firstController.LastReceipt, Is.Null);

            firstController.Bet(CasinoGameKind.CoinFlip, 100, 0);
            yield return WaitUntil(() => !firstController.IsBusy && firstController.LastReceipt != null, "第一笔硬币下注提交回执", 15);
            var firstReceipt = firstController.LastReceipt;
            Assert.That(firstReceipt.Accepted, Is.True, firstReceipt.Error.ToString());
            Assert.That(firstReceipt.Request.Game, Is.EqualTo(CasinoGameKind.CoinFlip));
            Assert.That(firstReceipt.Request.Stake, Is.EqualTo(100));
            Assert.That(firstReceipt.Request.Choice, Is.Zero);
            // P0 宿主种子 20261001 的第一枚硬币为正面；这是规则与宿主种子接入的固定基线。
            Assert.That(firstReceipt.Outcome, Is.Zero);
            Assert.That(firstReceipt.Payout, Is.EqualTo(200));
            Assert.That(firstReceipt.BalanceBefore, Is.EqualTo(1000));
            Assert.That(firstReceipt.BalanceAfter, Is.EqualTo(1100));
            Assert.That(firstReceipt.Revision, Is.EqualTo(1));
            Assert.That(firstController.Balance, Is.EqualTo(1100));
            string firstRunId = firstReceipt.Request.RunId;
            yield return CaptureStableHudEvidence(firstController);

            firstController.LeaveRoom();
            yield return WaitUntil(() => !firstController.IsBusy && !firstController.TransportKind.HasValue,
                "离房完成并清空网络会话", 15);
            Assert.That(firstController.HasRun, Is.False);
            Assert.That(firstController.Balance, Is.Zero);
            Assert.That(firstController.LastReceipt, Is.Null);
            Assert.That(firstController.RoomCode, Is.Empty);
            Assert.That(firstHud.State, Is.EqualTo(ViewState.Visible), "离房保留场景 HUD，允许重新入房。");

            firstController.RequestExit();
            yield return WaitUntil(() => IsStableHub() && firstController == null, "返回 Hub 并卸载旧 Controller", 30);
            Assert.That(firstHud.State, Is.EqualTo(ViewState.Destroyed), "返回前关闭持有的具体 View 实例。");
            Assert.That(firstHud.gameObject == null, Is.True);
            Assert.That(UIManager.Instance.Get<JinxCasinoHudView>(), Is.Null);
            AssertSingleAudioListener();

            yield return EnterCasino();
            var secondController = Object.FindFirstObjectByType<JinxCasinoController>();
            var secondHud = UIManager.Instance.Get<JinxCasinoHudView>();
            Assert.That(secondController != null, Is.True);
            Assert.That(ReferenceEquals(firstController, secondController), Is.False);
            Assert.That(secondHud, Is.Not.SameAs(firstHud));
            Assert.That(secondController.HasRun, Is.False);
            Assert.That(secondController.Balance, Is.Zero);
            Assert.That(secondController.LastReceipt, Is.Null);
            Assert.That(secondController.TransportKind.HasValue, Is.False);

            secondController.StartOffline("P0再次进入验证");
            yield return WaitUntil(() => !secondController.IsBusy && secondController.HasRun, "重入创建新局", 15);
            Assert.That(secondController.Balance, Is.EqualTo(1000), "旧局的 1100 筹码不得复用。");
            secondController.Bet(CasinoGameKind.CoinFlip, 100, 0);
            yield return WaitUntil(() => !secondController.IsBusy && secondController.LastReceipt != null, "新局第一笔下注", 15);
            var secondReceipt = secondController.LastReceipt;
            Assert.That(secondReceipt.Accepted, Is.True);
            Assert.That(secondReceipt.Request.RunId, Is.Not.EqualTo(firstRunId));
            Assert.That(secondReceipt.Revision, Is.EqualTo(1));
            Assert.That(secondReceipt.BalanceBefore, Is.EqualTo(1000));
            Assert.That(secondReceipt.Outcome, Is.Zero);
            Assert.That(secondController.Balance, Is.EqualTo(1100));

            secondController.RequestExit();
            yield return WaitUntil(() => IsStableHub() && secondController == null, "新局退出并收口会话", 30);
            Assert.That(secondHud.State, Is.EqualTo(ViewState.Destroyed));
            Assert.That(UIManager.Instance.Get<JinxCasinoHudView>(), Is.Null);
            AssertSingleAudioListener();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                var controller = Object.FindFirstObjectByType<JinxCasinoController>();
                if (controller != null)
                {
                    yield return WaitUntil(() => controller == null || !controller.IsBusy, "等待失败测试的在途操作收口", 15);
                    if (controller != null) controller.RequestExit();
                    yield return WaitUntil(() => controller == null && IsStableHub(), "清理失败测试遗留的 Demo", 30);
                }
            }
            finally
            {
                resolution?.Dispose();
                resolution = null;
            }
            // 与现有正式入口 FlowTests 一致，收口到 Hub；退出 Play 后原 Editor 场景由 Test Runner 恢复。
        }

        private static IEnumerator EnterFormalHub()
        {
            // 同一 PlayMode 运行的常驻启动壳只启动一次，不能直接重载已经运行的 AppEntrance。
            if (GameSceneNavigator.Instance == null)
            {
                var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single);
                Assert.That(startup, Is.Not.Null);
                yield return WaitUntil(() => startup.isDone, "加载唯一启动场景 AppEntrance", 90);
            }
            yield return WaitUntil(IsStableHub, "正式启动 / 复用 Hub", 90);
            Assert.That(GameSceneNavigator.Instance.IsEditorDirect, Is.False, "本测试只能使用正式 AppEntrance 入口。");
        }

        private static IEnumerator EnterCasino()
        {
            var travel = GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask();
            yield return WaitUntil(() => travel.IsCompleted, "导航加载赌场正式场景", 30);
            Assert.That(travel.GetAwaiter().GetResult().Status, Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return WaitUntil(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.JinxCasino &&
                !GameSceneNavigator.Instance.IsTransitioning &&
                UIManager.Instance.Get<JinxCasinoHudView>()?.State == ViewState.Visible &&
                Object.FindFirstObjectByType<JinxCasinoController>() != null, "赌场 Controller / HUD 初始化完成", 30);
            AssertSingleAudioListener();
        }

        private static bool IsStableHub()
        {
            var navigator = GameSceneNavigator.Instance;
            return navigator != null && navigator.CurrentScene == GameSceneId.Hub && !navigator.IsTransitioning &&
                UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible;
        }

        private static void AssertSingleAudioListener()
        {
            int enabled = 0;
            foreach (var listener in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (listener.isActiveAndEnabled) enabled++;
            Assert.That(enabled, Is.EqualTo(1), "稳定时刻只能存在一个启用的 AudioListener。");
        }

        private static IEnumerator CaptureStableHudEvidence(JinxCasinoController controller)
        {
            yield return WaitUntil(() => !controller.IsBusy && UIManager.Instance.Get<JinxCasinoHudView>()?.State == ViewState.Visible,
                "截图前等待已提交 HUD", 10);
            // 给 Canvas、安全区和最终渲染帧时间；不依赖 Editor 必须停留在 Game View 的 WaitForEndOfFrame。
            yield return null;
            yield return null;
            yield return null;
            string folder = Path.GetFullPath("Library/JinxCasino/Verification");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "P0OfflineFlow-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") +
                "-" + Guid.NewGuid().ToString("N") + ".png");
            ScreenCapture.CaptureScreenshot(path);
            yield return null;
            yield return null;
            yield return WaitUntil(() => File.Exists(path) && new FileInfo(path).Length > 8, "P0 截图文件写入", 15);
            byte[] header = File.ReadAllBytes(path);
            Assert.That(header[0], Is.EqualTo(137));
            Assert.That(header[1], Is.EqualTo((byte)'P'));
            Assert.That(header[2], Is.EqualTo((byte)'N'));
            Assert.That(header[3], Is.EqualTo((byte)'G'));
            Debug.Log("[JinxCasinoFlowTests] P0 离线正式流程截图：" + path);
        }

        private static IEnumerator WaitUntil(Func<bool> predicate, string reason, float timeout)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            while (!predicate() && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(predicate(), Is.True, reason + " 超时。");
        }
    }
}
#endif
