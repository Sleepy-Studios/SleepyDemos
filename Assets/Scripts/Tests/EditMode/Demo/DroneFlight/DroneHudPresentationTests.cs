using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class DroneHudPresentationTests
    {
        [Test]
        public void CameraReadout_ShowsAnglesOnlyForGimbalAndAimUsesItsOwnExitAction()
        {
            var gimbal = Snapshot(DroneCameraMode.Gimbal);
            StringAssert.Contains("Y 12° / P -20°", DroneHudPresentation.Camera(gimbal, "C", "V"));
            StringAssert.DoesNotContain("Y 12°", DroneHudPresentation.Camera(Snapshot(DroneCameraMode.ThirdPerson), "C", "V"));
            StringAssert.Contains("V 退出瞄准", DroneHudPresentation.Camera(Snapshot(DroneCameraMode.HarpoonAim), "C", "V"));
            StringAssert.DoesNotContain("C 切换", DroneHudPresentation.Camera(Snapshot(DroneCameraMode.HarpoonAim), "C", "V"));
        }
        private static DroneHudSnapshot Snapshot(DroneCameraMode mode) => new(
            DroneFlightOperationState.Flying, DroneResponseProfile.Normal, true, 0, 2, 3, 0, 5, false, mode, 12, -20, 60);
    }
}
