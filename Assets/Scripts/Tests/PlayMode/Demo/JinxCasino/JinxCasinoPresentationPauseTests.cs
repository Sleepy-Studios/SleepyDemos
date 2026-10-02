using System;
using System.Collections;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 真实组件演出剩余时间；不经过另一测试入口，不依赖机台内部动画字段。
    public sealed class JinxCasinoPresentationPauseTests
    {
        private GameObject root;
        private JinxCasinoPresentationClock clock;
        private JinxCasinoPresentationClockTestDriver driver;

        [SetUp]
        public void Setup()
        {
            root = new GameObject("Jinx presentation pause tests");
            driver = root.AddComponent<JinxCasinoPresentationClockTestDriver>();
            clock = new JinxCasinoPresentationClock(); driver.Clock = clock;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PausedThreeSecondEffectAndFiveSecondProtectionKeepTheirRemainingTime()
        {
            var player = Child("Player").transform;
            var effects = root.AddComponent<JinxCasinoSceneEffects>();
            var prefab = Child("Saved bubble test prefab"); prefab.SetActive(false);
            var system = prefab.AddComponent<ParticleSystem>();
            var main = system.main; main.loop = true; main.startLifetime = 5; main.playOnAwake = true;
            effects.Configure(player, Array.Empty<Transform>(), new[] { player },
                new[] { new CasinoEffectPrefabBinding { Kind = "Bubble", Prefab = prefab } });
            effects.BindPresentationClock(clock);
            effects.SynchronizeState(CasinoAdventureSession.Start(1, CasinoAdventureMode.Practice, 1).CaptureState());
            yield return null; yield return null;
            effects.ApplyEffects(new[] { new CasinoSceneEffect { Id = "bubble", EffectKind = "Bubble", TargetId = "local", DurationMilliseconds = 3000, ProtectionMilliseconds = 5000 } });
            yield return null; yield return null;
            ParticleSystem live = null;
            foreach (var candidate in effects.GetComponentsInChildren<ParticleSystem>(true))
                if (candidate != system && candidate.gameObject.activeInHierarchy) live = candidate;
            Assert.That(live, Is.Not.Null, "必须测试接收器实际创建的演出实例。");
            clock.SetPaused(true); yield return null;
            float time = clock.TimeSeconds, particleTime = live.time;
            Assert.That(live.isPaused, Is.True);
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(clock.TimeSeconds, Is.EqualTo(time));
            Assert.That(live.time, Is.EqualTo(particleTime).Within(.001f));
            Assert.That(effects.ActiveVisualCount, Is.EqualTo(1));
            Assert.That(effects.LocalMovementMultiplier, Is.Zero);
            Assert.That(effects.CanApplyPrank("local"), Is.False);
            clock.SetPaused(false); driver.Speed = 30; driver.Limit = time + 3.1f;
            yield return Until(() => effects.ActiveVisualCount == 0, "恢复后效果剩余时间正常耗尽");
            Assert.That(effects.LocalMovementMultiplier, Is.EqualTo(1));
            Assert.That(effects.CanApplyPrank("local"), Is.False, "三秒演出结束不能提前解除五秒保护。");
            driver.Limit = time + 5.1f;
            yield return Until(() => effects.CanApplyPrank("local"), "恢复后保护剩余时间正常耗尽");
        }

        [UnityTest]
        public IEnumerator MovingBoardAndPressedTargetFreezeWithoutDisableThenResume()
        {
            var origin = new Vector3(4100, 0, 4100);
            var floor = Child("Floor").AddComponent<BoxCollider>(); floor.size = new Vector3(20, 1, 20); floor.transform.position = origin + Vector3.down * .5f;
            // 桌体根按真实资源契约位于地面；抬高的是碰撞中心，避免触发悬空安全拦截。
            var board = Child("Board"); board.transform.position = origin;
            var collider = board.AddComponent<BoxCollider>(); collider.size = new Vector3(1, 1, 1);
            collider.center = Vector3.up * .6f;
            var table = board.AddComponent<JinxCasinoMovingTable>();
            table.Configure(board.transform, collider, Vector3.right * .6f, CasinoGameKind.Roulette);
            var facilities = root.AddComponent<JinxCasinoAreaFacilities>();
            facilities.Configure(0, floor.transform, Array.Empty<Light>(), null, new[] { table }, Array.Empty<Transform>());
            facilities.BindPresentationClock(clock);
            var visual = Child("ButtonVisual").transform;
            var target = Child("Target").AddComponent<JinxCasinoTableTarget>();
            target.Configure("pull", JinxCasinoTableAction.Primary, 0, 0, Array.Empty<Renderer>(), visual);
            target.BindPresentationClock(clock); Physics.SyncTransforms();
            yield return null; yield return null;
            driver.Limit = clock.TimeSeconds + .02f;
            table.BeginMotion(3);
            Assert.That(target.TryInvoke(), Is.True); yield return null;
            Assert.That(Vector3.Distance(board.transform.position, origin), Is.GreaterThan(.0001f));
            Assert.That(visual.localPosition.y, Is.LessThan(0));
            clock.SetPaused(true);
            Vector3 frozenBoard = board.transform.position, frozenPress = visual.localPosition;
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(table.enabled, Is.True); Assert.That(target.enabled, Is.True);
            Assert.That(table.IsMoving, Is.True);
            Assert.That(board.transform.position, Is.EqualTo(frozenBoard));
            Assert.That(visual.localPosition, Is.EqualTo(frozenPress));
            Assert.That(target.TryInvoke(), Is.False, "暂停时不能让公共物件入口提交操作。");
            clock.SetPaused(false); driver.Speed = 30; driver.Limit = float.PositiveInfinity;
            yield return Until(() => !table.IsMoving && visual.localPosition == Vector3.zero, "桌体安全归位且按压反馈正常释放");
            Assert.That(Vector3.Distance(board.transform.position, origin), Is.LessThan(.011f));
        }

        private GameObject Child(string name)
        { var child = new GameObject(name); child.transform.SetParent(root.transform, false); return child; }

        private static IEnumerator Until(Func<bool> condition, string description)
        {
            float deadline = Time.realtimeSinceStartup + 2;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, description);
        }
    }

    /// 用实际Update驱动共享时钟；Limit令五秒保护测试不依赖机器帧率。
    [DefaultExecutionOrder(-10000)]
    public sealed class JinxCasinoPresentationClockTestDriver : MonoBehaviour
    {
        public JinxCasinoPresentationClock Clock;
        public float Speed = 1;
        public float Limit = float.PositiveInfinity;
        private void Update()
        {
            if (Clock != null) Clock.Advance(Mathf.Min(Time.unscaledDeltaTime * Speed, Mathf.Max(0, Limit - Clock.TimeSeconds)), Time.frameCount);
        }
    }
}
