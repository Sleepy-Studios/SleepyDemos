using System.Collections;
using System.Linq;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hotfix.Tests
{
    public sealed class DroneCameraLifecycleTests
    {
        [UnityTest]
        public IEnumerator FDirectlyActivatesThirdPersonAndExitReturnsWaitingWithoutRenderTexture()
        {
            var root = new GameObject("RemoteExperienceFixture");
            var playerObject = new GameObject("PlayerCamera");
            playerObject.transform.SetParent(root.transform);
            var playerCamera = playerObject.AddComponent<Camera>();

            var droneBody = new GameObject("DroneBody");
            droneBody.transform.SetParent(root.transform);
            var droneCameraObject = new GameObject("DroneCamera");
            droneCameraObject.transform.SetParent(root.transform);
            var droneCamera = droneCameraObject.AddComponent<Camera>();
            droneCamera.enabled = false;
            var cameraRig = droneCameraObject.AddComponent<DroneCameraRig>();
            cameraRig.Configure(droneCamera, droneBody.transform, null, null, null, null);

            var experience = root.AddComponent<DroneRemoteControllerExperience>();
            experience.Configure(playerCamera, cameraRig, null);
            experience.Activate();
            yield return null;

            Assert.That(experience.State, Is.EqualTo(DroneControlSessionState.Active));
            Assert.That(playerCamera.enabled, Is.False);
            Assert.That(droneCamera.enabled, Is.True);
            Assert.That(droneCamera.targetTexture, Is.Null);
            Assert.That(cameraRig.Mode, Is.EqualTo(DroneCameraMode.ThirdPerson));
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>()
                .Any(texture => texture.name == "DroneRemotePreviewRT"), Is.False);

            experience.ReturnToWaiting();
            yield return null;

            Assert.That(experience.State, Is.EqualTo(DroneControlSessionState.Waiting));
            Assert.That(playerCamera.enabled, Is.True);
            Assert.That(droneCamera.enabled, Is.False);
            Object.Destroy(root);
        }

        [UnityTest]
        public IEnumerator SwitchingEveryCameraMode_DoesNotChangeRigidbodyState()
        {
            var root = new GameObject("CameraModeFixture");
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = new Vector3(1f, 0f, 0.5f);

            var cameraObject = new GameObject("SingleDroneCamera");
            var camera = cameraObject.AddComponent<Camera>();
            var rig = cameraObject.AddComponent<DroneCameraRig>();
            rig.Configure(camera, root.transform, null, null, root.transform, root.transform);
            var initialVelocity = body.linearVelocity;

            foreach (DroneCameraMode mode in System.Enum.GetValues(typeof(DroneCameraMode)))
            {
                rig.SetMode(mode);
                yield return null;
            }

            Assert.That(body.linearVelocity, Is.EqualTo(initialVelocity));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Object.Destroy(cameraObject);
            Object.Destroy(root);
        }
    }
}
