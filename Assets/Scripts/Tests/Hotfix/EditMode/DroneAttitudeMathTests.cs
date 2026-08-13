using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Hotfix.Tests
{
    public sealed class DroneAttitudeMathTests
    {
        [Test]
        public void CalculateLocalRotationVector_EqualRotationsReturnZero()
        {
            var error = DroneAttitudeMath.CalculateLocalRotationVector(Quaternion.identity, Quaternion.identity);

            Assert.That(error, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CalculateLocalRotationVector_UsesShortestYawAcrossEulerBoundary()
        {
            var current = Quaternion.Euler(0f, 170f, 0f);
            var target = Quaternion.Euler(0f, -170f, 0f);

            var error = DroneAttitudeMath.CalculateLocalRotationVector(current, target);

            Assert.That(error.y * Mathf.Rad2Deg, Is.EqualTo(20f).Within(0.01f));
            Assert.That(error.x, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(error.z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void CalculateTargetRate_AppliesGainAndMagnitudeLimit()
        {
            var targetRate = DroneAttitudeMath.CalculateTargetRate(
                Quaternion.identity,
                Quaternion.Euler(60f, 0f, 0f),
                gain: 4f,
                maximumRate: 2f);

            Assert.That(targetRate.magnitude, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(targetRate.x, Is.GreaterThan(0f));
        }
    }
}
