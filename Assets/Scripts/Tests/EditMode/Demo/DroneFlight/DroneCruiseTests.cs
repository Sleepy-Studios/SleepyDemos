using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证通用航点路线的有效性、循环方式和纯索引推进，不启动物理场景。
     */
    public sealed class DroneCruiseTests
    {
        [Test]
        public void Route_RequiresAtLeastTwoFiniteWaypoints()
        {
            var root = new GameObject("RouteFixture");
            try
            {
                var route = root.AddComponent<DroneCruiseRoute>();
                route.Configure(DroneCruiseMode.Once, 4f, new[]
                {
                    CreateWaypoint(root.transform, "A", Vector3.zero)
                });

                Assert.That(route.IsValid(out var error), Is.False);
                StringAssert.Contains("至少需要两个", error);

                route.Configure(DroneCruiseMode.Once, 4f, new[]
                {
                    CreateWaypoint(root.transform, "A", Vector3.zero),
                    CreateWaypoint(root.transform, "B", new Vector3(3f, 2f, 1f))
                });
                Assert.That(route.IsValid(out error), Is.True, error);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Progression_OnceCompletesAtLastWaypoint()
        {
            var progression = new DroneCruiseProgression();
            progression.Reset(3, DroneCruiseMode.Once);

            Assert.That(progression.CurrentIndex, Is.EqualTo(0));
            Assert.That(progression.TryAdvance(out _), Is.True);
            Assert.That(progression.CurrentIndex, Is.EqualTo(1));
            Assert.That(progression.TryAdvance(out _), Is.True);
            Assert.That(progression.CurrentIndex, Is.EqualTo(2));
            Assert.That(progression.TryAdvance(out var completed), Is.False);
            Assert.That(completed, Is.True);
        }

        [Test]
        public void Progression_LoopAndPingPongFollowConfiguredOrder()
        {
            var loop = new DroneCruiseProgression();
            loop.Reset(3, DroneCruiseMode.Loop);
            loop.TryAdvance(out _);
            loop.TryAdvance(out _);
            loop.TryAdvance(out _);
            Assert.That(loop.CurrentIndex, Is.EqualTo(0));

            var pingPong = new DroneCruiseProgression();
            pingPong.Reset(3, DroneCruiseMode.PingPong);
            var visited = new int[5];
            for (var index = 0; index < visited.Length; index++)
            {
                pingPong.TryAdvance(out _);
                visited[index] = pingPong.CurrentIndex;
            }
            Assert.That(visited, Is.EqualTo(new[] { 1, 2, 1, 0, 1 }));
        }

        private static DroneCruiseWaypoint CreateWaypoint(Transform parent, string name, Vector3 position)
        {
            var target = new GameObject(name).transform;
            target.SetParent(parent, false);
            target.localPosition = position;
            var waypoint = new DroneCruiseWaypoint();
            waypoint.Configure(target, 0f, 0f, DroneCruiseHeadingMode.AlongRoute);
            return waypoint;
        }
    }
}
