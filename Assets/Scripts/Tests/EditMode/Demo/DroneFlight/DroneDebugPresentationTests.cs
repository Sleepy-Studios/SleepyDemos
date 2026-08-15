using Hotfix.DroneFlight;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证 F2/F3 调试快捷键相互独立，以及动力矢量显示平滑不改变物理真值。
     */
    public sealed class DroneDebugPresentationTests
    {
        [TestCase(false, false, false, false)]
        [TestCase(true, false, true, false)]
        [TestCase(false, true, false, true)]
        [TestCase(true, true, true, true)]
        public void ShortcutRequest_RoutesF2AndF3Independently(
            bool f2Pressed,
            bool f3Pressed,
            bool expectedDraw,
            bool expectedPanel)
        {
            var request = DroneFlightDebugShortcutRequest.FromPressedKeys(f2Pressed, f3Pressed);

            Assert.That(request.ToggleDraw, Is.EqualTo(expectedDraw));
            Assert.That(request.TogglePanel, Is.EqualTo(expectedPanel));
        }

        [Test]
        public void Smoother_FirstFrameCapturesTargetAndResetClearsHistory()
        {
            var smoother = new DroneDebugVectorSmoother();
            var target = new Vector3(3f, -2f, 1f);

            Assert.That(smoother.Step(target, 18f, 1f / 60f), Is.EqualTo(target));
            Assert.That(smoother.HasValue, Is.True);

            smoother.Reset();

            Assert.That(smoother.HasValue, Is.False);
            Assert.That(smoother.Current, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Smoother_ConvergesMonotonicallyAndIsFrameRateIndependent()
        {
            var thirtyFps = CreateInitializedSmoother();
            var oneTwentyFps = CreateInitializedSmoother();
            var target = new Vector3(10f, 0f, 0f);
            var previous = 0f;
            for (var index = 0; index < 30; index++)
            {
                var current = thirtyFps.Step(target, 18f, 1f / 30f).x;
                Assert.That(current, Is.GreaterThanOrEqualTo(previous));
                Assert.That(current, Is.LessThanOrEqualTo(target.x));
                previous = current;
            }

            for (var index = 0; index < 120; index++)
            {
                oneTwentyFps.Step(target, 18f, 1f / 120f);
            }

            Assert.That(thirtyFps.Current.x, Is.EqualTo(oneTwentyFps.Current.x).Within(0.0001f));
            Assert.That(thirtyFps.Current.x, Is.EqualTo(target.x).Within(0.001f));
        }

        [Test]
        public void Smoother_NonFiniteTargetImmediatelyBecomesZero()
        {
            var smoother = CreateInitializedSmoother();

            var result = smoother.Step(new Vector3(float.NaN, 1f, 2f), 18f, 1f / 60f);

            Assert.That(result, Is.EqualTo(Vector3.zero));
            Assert.That(smoother.Current, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Renderer_UsesWorldObjectsWithoutGuiOrPhysicsComponents()
        {
            var owner = new GameObject("DebugRendererTest");
            try
            {
                var renderer = owner.AddComponent<DroneFlightDebugDrawRenderer>();
                typeof(DroneFlightDebugDrawRenderer).GetMethod(
                    "EnsureVisuals", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(renderer, null);
                Assert.That(typeof(DroneFlightDebugDrawRenderer).GetMethod(
                    "OnGUI", BindingFlags.Instance | BindingFlags.NonPublic), Is.Null);
                Assert.That(owner.GetComponentsInChildren<LineRenderer>(true).Length, Is.EqualTo(27));
                Assert.That(owner.GetComponentsInChildren<TextMesh>(true).Length, Is.EqualTo(9));
                Assert.That(owner.GetComponentInChildren<Rigidbody>(true), Is.Null);
                Assert.That(owner.GetComponentInChildren<Collider>(true), Is.Null);

                renderer.enabled = false;
                typeof(DroneFlightDebugDrawRenderer).GetMethod(
                    "SetVisualsActive", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(
                    renderer, new object[] { false });
                Assert.That(owner.transform.Find("F2WorldVectors").gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private static DroneDebugVectorSmoother CreateInitializedSmoother()
        {
            var smoother = new DroneDebugVectorSmoother();
            smoother.Step(Vector3.zero, 18f, 1f / 60f);
            return smoother;
        }
    }
}
