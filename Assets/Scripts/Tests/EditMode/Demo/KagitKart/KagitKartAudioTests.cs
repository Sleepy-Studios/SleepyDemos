using Hotfix.KagitKart;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class KagitKartAudioTests
    {
        [TestCase(0, 0, 0.2f)]
        [TestCase(0, 1, 0.7f)]
        [TestCase(0.24f, 1, 0.41f)]
        [TestCase(0.48f, 1, 0.5f)]
        [TestCase(0.72f, 1, 0.59f)]
        [TestCase(1.17f, 1, 1f)]
        public void EngineRpmPreservesOriginalGearDrops(float speed, float throttle, float expected)
        {
            Assert.That(KagitKartAudio.Rpm(speed, throttle), Is.EqualTo(expected).Within(0.0001f));
        }

    }
}
