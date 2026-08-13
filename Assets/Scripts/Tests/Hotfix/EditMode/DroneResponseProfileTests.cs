using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Hotfix.Tests
{
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
