using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证旋翼视觉严格根据真实 RPM 和 CW/CCW 方向累计角度，并在零转速时停止。
     */
    public sealed class DroneRotorVisualTests
    {
        [Test]
        public void StepVisual_IntegratesRealRpmAndDirectionWithoutModuloAmbiguity()
        {
            var root = new GameObject("RotorVisualFixture");
            var blade = new GameObject("BladeRoot").transform;
            blade.SetParent(root.transform, false);
            var visual = root.AddComponent<DroneRotorVisual>();
            visual.Configure(blade, DroneRotorDirection.CounterClockwise);

            visual.SetRpm(1200f);
            visual.StepVisual(0.5f);

            Assert.That(visual.CurrentRpm, Is.EqualTo(1200f));
            Assert.That(visual.AccumulatedDegrees, Is.EqualTo(3600d).Within(0.001d));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void StepVisual_StopsAtZeroAndClockwiseAccumulatesNegativeAngle()
        {
            var root = new GameObject("RotorVisualFixture");
            var visual = root.AddComponent<DroneRotorVisual>();
            visual.Configure(root.transform, DroneRotorDirection.Clockwise);

            visual.SetRpm(0f);
            visual.StepVisual(1f);
            Assert.That(visual.AccumulatedDegrees, Is.Zero);

            visual.SetRpm(60f);
            visual.StepVisual(1f);
            Assert.That(visual.AccumulatedDegrees, Is.EqualTo(-360d).Within(0.001d));
            Object.DestroyImmediate(root);
        }
    }
}
