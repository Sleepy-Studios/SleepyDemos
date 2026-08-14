using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证姿态误差最短路径、目标角速度限制、航向目标约束和按实际航向解释水平输入的数学基础。
     */
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

        [Test]
        public void AdvanceBoundedYawTarget_LongCommandCannotWindUpBeyondActualHeading()
        {
            var target = 0f;
            const float actual = 15f;
            for (var index = 0; index < 500; index++)
            {
                target = DroneAttitudeMath.AdvanceBoundedYawTarget(target, actual, 3f, 45f);
            }

            Assert.That(Mathf.Abs(Mathf.DeltaAngle(actual, target)), Is.EqualTo(45f).Within(0.001f));
        }

        [Test]
        public void HeadingRelativeVelocity_UsesActualHeadingInsteadOfStaleYawTarget()
        {
            var velocity = DroneAttitudeMath.CalculateHeadingRelativeWorldVelocity(
                Vector2.up,
                actualYawDegrees: 90f,
                maximumSpeed: 7f);

            Assert.That(velocity.x, Is.EqualTo(7f).Within(0.001f));
            Assert.That(velocity.z, Is.EqualTo(0f).Within(0.001f));
        }
    }
}
