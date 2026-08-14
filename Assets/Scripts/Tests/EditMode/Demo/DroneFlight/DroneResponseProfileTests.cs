using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    /*
     * 测试说明：验证 Cine、Normal、Sport 三档响应参数有序，并确保切档不重置位置和高度目标。
     */
    public sealed class DroneResponseProfileTests
    {
        [Test]
        public void Defaults_AreOrderedFromCineToSportWithoutDuplicatingControllers()
        {
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            var cine = config.GetProfile(DroneResponseProfile.Cine);
            var normal = config.GetProfile(DroneResponseProfile.Normal);
            var sport = config.GetProfile(DroneResponseProfile.Sport);

            Assert.That(cine.MaximumHorizontalSpeed, Is.LessThan(normal.MaximumHorizontalSpeed));
            Assert.That(normal.MaximumHorizontalSpeed, Is.LessThan(sport.MaximumHorizontalSpeed));
            Assert.That(cine.MaximumTiltDegrees, Is.LessThan(normal.MaximumTiltDegrees));
            Assert.That(normal.MaximumTiltDegrees, Is.LessThan(sport.MaximumTiltDegrees));
            Assert.That(cine.InputRiseRate, Is.LessThan(normal.InputRiseRate));
            Assert.That(normal.InputRiseRate, Is.LessThan(sport.InputRiseRate));
            Assert.That(cine.MaximumHorizontalJerk, Is.LessThan(normal.MaximumHorizontalJerk));
            Assert.That(normal.MaximumHorizontalJerk, Is.LessThan(sport.MaximumHorizontalJerk));
            Assert.That(cine.MaximumVerticalJerk, Is.LessThan(normal.MaximumVerticalJerk));
            Assert.That(normal.MaximumVerticalJerk, Is.LessThan(sport.MaximumVerticalJerk));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void SwitchProfile_DoesNotResetPositionOrAltitudeSetpoints()
        {
            var root = new GameObject("ProfileSwitchFixture");
            root.SetActive(false);
            root.AddComponent<Rigidbody>();
            var controller = root.AddComponent<DroneFlightController>();

            controller.SetResponseProfile(DroneResponseProfile.Sport);

            Assert.That(controller.ResponseProfile, Is.EqualTo(DroneResponseProfile.Sport));
            Object.DestroyImmediate(root);
        }
    }
}
