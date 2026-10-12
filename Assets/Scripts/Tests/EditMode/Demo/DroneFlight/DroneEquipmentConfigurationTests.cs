using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证紧凑抓斗、渔叉的有效默认配置及绳长、断裂张力约束。
     */
    public sealed class DroneEquipmentConfigurationTests
    {
        [Test]
        public void Grapple_DefaultsAreCompactFourClawValues()
        {
            var config = ScriptableObject.CreateInstance<DroneGrappleConfig>();
            try
            {
                Assert.That(config.TryValidate(out var diagnostic), Is.True, diagnostic);
                Assert.That(config.ArmLengthMeters, Is.EqualTo(0.08f).Within(0.0001f));
                Assert.That(config.MaximumLiftTravelMeters, Is.EqualTo(0.35f).Within(0.0001f));
                Assert.That(config.LiftSpeedMetersPerSecond, Is.EqualTo(0.18f).Within(0.0001f));
                Assert.That(config.LiftAccelerationMetersPerSecondSquared, Is.EqualTo(0.45f));
                Assert.That(config.SwingLimitDegrees, Is.EqualTo(35f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void Harpoon_DefaultsHaveStableRecoveryAndBreakableTension()
        {
            var config = ScriptableObject.CreateInstance<DroneHarpoonConfig>();
            try
            {
                Assert.That(config.TryValidate(out var diagnostic), Is.True, diagnostic);
                Assert.That(config.ProjectileMassKilograms, Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(config.RopeBreakForceNewtons, Is.GreaterThan(config.MaximumTensionNewtons));
                Assert.That(config.MaximumRopeLengthMeters, Is.GreaterThan(config.MinimumRopeLengthMeters));
                Assert.That(config.MaximumAimRadiusMeters, Is.EqualTo(3f));
                Assert.That(config.MaximumAimConeDegrees, Is.EqualTo(25f));
                Assert.That(config.LaunchImpulseNewtonSeconds, Is.EqualTo(0.12f).Within(0.0001f));
                Assert.That(config.AutomaticRecoverySpeedMetersPerSecond, Is.EqualTo(2f));
                Assert.That(config.RecoveryResponseSeconds, Is.EqualTo(0.18f).Within(0.0001f));
                Assert.That(config.MaximumRecoveryAccelerationMetersPerSecondSquared, Is.EqualTo(15f));
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }

    }
}
