using Hotfix.DroneFlight;
using NUnit.Framework;

namespace Tests.Demo
{
    public sealed class DroneHudPresentationTests
    {
        [Test]
        public void CameraReadout_PreservesModeFovAndGimbalAnglesWithoutEmbeddingOperationKeys()
        {
            var gimbal = Snapshot(DroneCameraMode.Gimbal);
            StringAssert.Contains("Y 12° / P -20°", DroneHudPresentation.Camera(gimbal));
            string thirdPerson = DroneHudPresentation.Camera(Snapshot(DroneCameraMode.ThirdPerson));
            StringAssert.Contains("第三人称", thirdPerson);
            StringAssert.DoesNotContain("Y 12°", thirdPerson);
            StringAssert.Contains("FOV 60°", thirdPerson);
            string aim = DroneHudPresentation.Camera(Snapshot(DroneCameraMode.HarpoonAim));
            StringAssert.Contains("渔叉瞄准", aim);
            StringAssert.Contains("FOV 60°", aim);
            StringAssert.DoesNotContain("退出瞄准", aim);
            StringAssert.DoesNotContain("切换", aim);
        }
        private static DroneHudSnapshot Snapshot(DroneCameraMode mode) => new(
            DroneFlightOperationState.Flying, DroneResponseProfile.Normal, true, 0, 2, 3, 0, 5, false, mode, 12, -20, 60);
    }
}
