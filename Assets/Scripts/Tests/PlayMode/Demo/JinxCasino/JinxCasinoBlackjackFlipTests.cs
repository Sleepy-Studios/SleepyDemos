#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 保存场景牌槽的真实帧循环回归；不重建牌面、不运行宿主、不读取隐藏牌或私有动画计时。
    public sealed class JinxCasinoBlackjackFlipTests
    {
        private const string ScenePath = "Assets/LoadResources/Demos/jinx_casino/Scenes/Immersion.unity";
        private Scene loaded;
        private Scene originalActive;
        private SceneHandle[] originalScenes;
        private int originalListeners;
        private JinxCasinoS1BlackjackPresentation presentation;
        private CasinoS1CardSlot hole;
        private CasinoS1CardFace back;
        private CasinoS1CardFace[] faces;
        private object dealerCounter;
        private Quaternion poseRest;
        private Quaternion meshRest;
        private Vector3 faceNormal;
        private float holeRestHeight;
        private CasinoMiniGameRound round;
        private JinxCasinoTableView initial;
        private JinxCasinoTableView settled;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            originalActive = SceneManager.GetActiveScene();
            originalScenes = SceneHandles(); originalListeners = EnabledListenerCount();
            Assert.That(SceneManager.GetSceneByPath(ScenePath).isLoaded, Is.False,
                "隔离测试不能接管用户或另一测试已加载的Immersion场景。");
            // Unity6在下一帧完成此入口的加载；sceneLoaded发生于Start前，必须在回调中隔离宿主。
            SceneManager.sceneLoaded += IsolateLoadedScene;
            loaded = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            yield return Until(() => loaded.IsValid() && loaded.isLoaded, "保存场景完成加载", 10);
            presentation = Components<JinxCasinoS1BlackjackPresentation>().Single();
            Assert.That(Components<JinxCasinoController>().All(owner => owner.AdventureState == null), Is.True,
                "夹具只测试表现，不能启动冒险后由OnDestroy自动写存档。");
            presentation.enabled = true;
            // 只读序列化资源绑定；这些字段不是规则/计时内部状态。TMP文本使用其公开text属性。
            hole = Binding<CasinoS1CardSlot[]>("dealerCards")[1];
            back = Binding<CasinoS1CardFace>("back"); faces = Binding<CasinoS1CardFace[]>("faces");
            dealerCounter = Binding<object>("dealerTotal");
            Assert.That(hole.Filter.transform.parent, Is.SameAs(hole.Pose));
            Assert.That(hole.Renderer.transform, Is.SameAs(hole.Filter.transform));
            poseRest = hole.Pose.localRotation; meshRest = hole.Filter.transform.localRotation;
            faceNormal = hole.Filter.transform.up; holeRestHeight = hole.Pose.localPosition.y;
            Assert.That(dealerCounter, Is.Not.Null, "使用场景保存的庄家点数铭牌。");
            Assert.That(EnabledListenerCount(), Is.EqualTo(originalListeners), "附加场景的Listener必须隔离。");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SceneManager.sceneLoaded -= IsolateLoadedScene;
            if (originalActive.IsValid() && originalActive.isLoaded && SceneManager.GetActiveScene() != originalActive)
                SceneManager.SetActiveScene(originalActive);
            if (loaded.IsValid() && loaded.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(loaded);
                if (unload != null) yield return Until(() => unload.isDone, "只卸载夹具新增场景", 10);
            }
            CollectionAssert.AreEquivalent(originalScenes, SceneHandles(), "用户原有场景应全部保留且不多出夹具场景。");
            Assert.That(EnabledListenerCount(), Is.EqualTo(originalListeners));
            if (originalActive.IsValid() && originalActive.isLoaded)
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(originalActive));
            presentation = null; loaded = default;
        }

        private void IsolateLoadedScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.path != ScenePath) return;
            loaded = scene;
            foreach (var behaviour in Components<Behaviour>())
                if (!(behaviour is JinxCasinoS1BlackjackPresentation)) behaviour.enabled = false;
        }

        [UnityTest]
        public IEnumerator SavedHoleCardPassesThroughBackAndEdgeThenPublicRankAndFreezesWhenPaused()
        {
            yield return StartRealRound();
            Assert.That(hole.Filter.sharedMesh, Is.SameAs(back.Mesh));
            Assert.That(CounterText(), Does.Contain("+?"));
            ResolveRealRound();
            presentation.Present(settled, false);
            Assert.That(presentation.IsAnimating, Is.True, "结算不能瞬切已有暗牌。");
            yield return Until(() => Tilt() > 8 && Tilt() < 80, "看到翻牌前半程真实倾斜", 3);
            Assert.That(hole.Filter.sharedMesh, Is.SameAs(back.Mesh), "侧面之前保持真实保存的牌背。");
            Assert.That(CounterText(), Does.Contain("+?"), "半程前铭牌不能提前显出暗牌点数。");
            Assert.That(hole.Pose.localPosition.y, Is.GreaterThan(holeRestHeight), "纸牌需离开保存的桌面高度。");
            Quaternion beforeRepeat = hole.Pose.localRotation;
            float beforeRepeatTilt = Tilt();
            Vector3 beforeRepeatPosition = hole.Pose.localPosition;
            presentation.Present(settled, false); presentation.Present(settled, false);
            Assert.That(hole.Pose.localRotation, Is.EqualTo(beforeRepeat), "同帧相同公开DTO不能重启翻牌。");
            Assert.That(hole.Pose.localPosition, Is.EqualTo(beforeRepeatPosition));
            yield return null;
            Assert.That(Tilt(), Is.GreaterThanOrEqualTo(beforeRepeatTilt - .01f),
                "重复DTO之后继续真实帧循环，不能因私有计时重置退回开头。");

            presentation.Present(settled, true);
            yield return null;
            Quaternion frozenPose = hole.Pose.localRotation, frozenMesh = hole.Filter.transform.localRotation;
            Vector3 frozenPosition = hole.Pose.localPosition;
            Mesh frozenFace = hole.Filter.sharedMesh; string frozenTotal = CounterText();
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(presentation.IsAnimating, Is.True);
            Assert.That(hole.Pose.localRotation, Is.EqualTo(frozenPose));
            Assert.That(hole.Filter.transform.localRotation, Is.EqualTo(frozenMesh));
            Assert.That(hole.Pose.localPosition, Is.EqualTo(frozenPosition));
            Assert.That(hole.Filter.sharedMesh, Is.SameAs(frozenFace));
            Assert.That(CounterText(), Is.EqualTo(frozenTotal));

            presentation.Present(settled, false);
            yield return null;
            Assert.That(Tilt(), Is.GreaterThan(0).And.LessThan(179), "恢复不能累计暂停墙钟并直接完成。");
            var actualFace = faces[settled.Presentation.SecondaryValues[1] - 1];
            yield return Until(() => hole.Filter.sharedMesh == actualFace.Mesh && Tilt() > 100 && Tilt() < 175,
                "看到侧面后的公开点数与第二阶段倾斜", 3);
            Assert.That(Quaternion.Angle(hole.Filter.transform.localRotation, meshRest), Is.GreaterThan(175),
                "两种模板都朝+Y，独立网格要转到实体另一侧。");
            Assert.That(Mathf.Abs(Vector3.Dot(hole.Filter.transform.up, faceNormal)), Is.LessThan(.98f),
                "真实网格正面法线需要经历侧向姿态，不能只瞬切mesh。");
            presentation.Present(settled, false); presentation.Present(settled, false);
            yield return Until(() => !presentation.IsAnimating, "翻牌与庄家补牌全部完成", 6);
            AssertSettledFace();
        }

        [UnityTest]
        public IEnumerator RestoreCancelsHalfFlipWithoutReplayAndNewRunClearsBothRotations()
        {
            yield return StartRealRound(); ResolveRealRound();
            presentation.Present(settled, false);
            yield return Until(() => Tilt() > 8 && Tilt() < 80, "恢复发生于真实翻牌中间", 3);
            presentation.Restore(settled);
            Assert.That(presentation.IsAnimating, Is.False); AssertSettledFace();
            Quaternion stable = hole.Pose.localRotation;
            presentation.Present(settled, false); presentation.Present(settled, false);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(presentation.IsAnimating, Is.False);
            Assert.That(hole.Pose.localRotation, Is.EqualTo(stable)); AssertSettledFace();

            // 恢复真正未揭示的公开局，不直接把已结算DTO改成隐藏牌。
            presentation.Restore(initial);
            Assert.That(hole.Filter.sharedMesh, Is.SameAs(back.Mesh));
            Assert.That(CounterText(), Does.Contain("+?"));
            Assert.That(presentation.IsAnimating, Is.False);
            presentation.Present(settled, false);
            yield return Until(() => Tilt() > 100 && Tilt() < 175, "早期活动快照仍能在真结算后揭示一次", 3);
            presentation.BeginRun("blackjack-flip-next-run");
            Assert.That(presentation.IsAnimating, Is.False);
            Assert.That(hole.Pose.gameObject.activeSelf, Is.False);
            AssertNeutralRotations();
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(hole.Pose.gameObject.activeSelf, Is.False, "旧队列不能在新Run重新出现。");
            AssertNeutralRotations();
        }

        private IEnumerator StartRealRound()
        {
            // 有界选择一个非天然21点的真实局，判断只使用公开IsComplete，不读取牌堆。
            for (uint seed = 1; seed <= 32; seed++)
            {
                round = CasinoMiniGameRound.Create(CasinoGameKind.Blackjack, seed, 100, 0);
                if (!round.IsComplete) break;
            }
            Assert.That(round.IsComplete, Is.False, "夹具需要有合法停牌动作的真实初局。");
            initial = View(round.GetPresentation(), 0);
            Assert.That(initial.Presentation.SecondaryValues.Length, Is.EqualTo(1), "公开投影不能提供未揭示暗牌。");
            presentation.BeginRun("blackjack-flip-fixture-run"); presentation.Present(initial, false);
            yield return Until(() => !presentation.IsAnimating && hole.Pose.gameObject.activeInHierarchy,
                "保存场景的玩家/庄家初始发牌完成", 6);
        }

        private void ResolveRealRound()
        {
            Assert.That(round.TryAct(CasinoMiniGameAction.Stand), Is.True);
            Assert.That(round.IsComplete, Is.True);
            settled = View(round.GetPresentation(), 1);
            Assert.That(settled.Presentation.SecondaryValues.Length, Is.GreaterThanOrEqualTo(2));
        }

        private JinxCasinoTableView View(CasinoMiniGamePresentation publicRound, int sequence)
        {
            var view = new JinxCasinoTableView();
            // DTO只有internal setter；反射填薄宿主外壳，实际牌值始终来自规则公开GetPresentation。
            SetView(view, nameof(view.StationId), presentation.StationId);
            SetView(view, nameof(view.Game), CasinoGameKind.Blackjack);
            SetView(view, nameof(view.HasOwnActiveRound), !publicRound.IsComplete);
            SetView(view, nameof(view.SettlementSequence), sequence);
            SetView(view, nameof(view.RulesText), CasinoMiniGameRound.DescribeRules(CasinoGameKind.Blackjack));
            SetView(view, nameof(view.Presentation), publicRound);
            return view;
        }

        private void AssertSettledFace()
        {
            int rank = settled.Presentation.SecondaryValues[1];
            Assert.That(hole.Filter.sharedMesh, Is.SameAs(faces[rank - 1].Mesh));
            CollectionAssert.AreEqual(faces[rank - 1].Materials, hole.Renderer.sharedMaterials);
            AssertNeutralRotations();
            Assert.That(Vector3.Dot(hole.Filter.transform.up, faceNormal), Is.GreaterThan(.9999f));
            Assert.That(hole.Pose.localPosition.y, Is.EqualTo(holeRestHeight).Within(.0001f), "恢复保存牌槽层叠高度，不能留悬浮牌。");
            Assert.That(CounterText(), Is.EqualTo(PublicTotal(settled.Presentation.SecondaryValues).ToString()));
        }

        private void AssertNeutralRotations()
        {
            Assert.That(Quaternion.Angle(hole.Pose.localRotation, poseRest), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(hole.Filter.transform.localRotation, meshRest), Is.LessThan(.001f));
        }
        private float Tilt() => Quaternion.Angle(hole.Pose.localRotation, poseRest);
        private string CounterText() => (string)dealerCounter.GetType().GetProperty("text").GetValue(dealerCounter);
        private T Binding<T>(string field) => (T)typeof(JinxCasinoS1BlackjackPresentation)
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(presentation);
        private static void SetView(JinxCasinoTableView view, string property, object value) =>
            typeof(JinxCasinoTableView).GetProperty(property).SetValue(view, value);
        private T[] Components<T>() where T : Component => loaded.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();
        private static int EnabledListenerCount() => Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
            .Count(listener => listener.isActiveAndEnabled);
        private static SceneHandle[] SceneHandles() => Enumerable.Range(0, SceneManager.sceneCount).Select(i => SceneManager.GetSceneAt(i).handle).ToArray();
        private static int PublicTotal(int[] ranks)
        {
            // 玩家可观察的21点计数；不镜像翻牌角度、时间或队列实现。
            int total = ranks.Sum(rank => rank == 1 ? 11 : Math.Min(rank, 10));
            int aces = ranks.Count(rank => rank == 1);
            while (total > 21 && aces-- > 0) total -= 10;
            return total;
        }
        private IEnumerator Until(Func<bool> condition, string description, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            string observed = string.Empty; int samples = 0;
            while (!condition() && Time.realtimeSinceStartup < deadline)
            {
                if (hole?.Pose != null && presentation != null && samples++ < 12)
                    observed += $" [angle={Tilt():F1},dt={Time.unscaledDeltaTime:F3},busy={presentation.IsAnimating}]";
                yield return null;
            }
            Assert.That(condition(), Is.True, description + observed);
        }
    }
}
#endif
