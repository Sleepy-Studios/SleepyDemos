using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证飞行遥测环形缓冲仅统计最近窗口，并正确汇总电机饱和等关键状态。
     */
    public sealed class DroneTelemetryBufferTests
    {
        [Test]
        public void RingBuffer_SummaryUsesLatestFixedWindowAndReportsSaturation()
        {
            var buffer = new DroneTelemetryBuffer(3);
            for (var index = 0; index < 4; index++)
            {
                buffer.Add(new DroneTelemetrySample(
                    index,
                    1f + index * 0.1f,
                    1.5f,
                    index,
                    0f,
                    index,
                    Vector3.one,
                    Vector3.zero,
                    default,
                    default,
                    default,
                    new QuadrotorMotorOutput(0.5f, 0.5f, 0.5f, 0.5f, 1f, index == 3)));
            }

            var summary = buffer.BuildSummary();

            Assert.That(buffer.Count, Is.EqualTo(3));
            StringAssert.Contains("samples=3", summary);
            StringAssert.Contains("duration=2.00s", summary);
            StringAssert.Contains("motor.saturated=1/3", summary);
            StringAssert.Contains("invalid=0", summary);
        }
    }
}
