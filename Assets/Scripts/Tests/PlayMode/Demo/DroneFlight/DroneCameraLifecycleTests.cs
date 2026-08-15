using System.Collections;
using System.Linq;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Demo
{
    /*
     * 测试说明：验证无人机进入第三人称、切换全部镜头模式及返回 Waiting 的生命周期，并确保镜头操作不改动刚体状态。
     */
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

        [UnityTest]
        public IEnumerator HarpoonAim_RestoresSavedModeWithoutChangingCameraOrBodyOwnership()
        {
            var root = new GameObject("HarpoonAimCameraFixture", typeof(Rigidbody));
            root.GetComponent<Rigidbody>().useGravity = false;
            var cameraObject = new GameObject("SingleDroneCamera", typeof(Camera), typeof(AudioListener));
            var camera = cameraObject.GetComponent<Camera>();
            var rig = cameraObject.AddComponent<DroneCameraRig>();
            rig.Configure(camera, root.transform, null, null, root.transform, root.transform);
            rig.SetMode(DroneCameraMode.Orbit);

            var belly = new GameObject("BellyMount").transform;
            belly.SetParent(root.transform, false);
            belly.localPosition = new Vector3(0f, -0.12f, 0f);
            belly.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rig.Configure(camera, root.transform, null, null, root.transform, belly);

            rig.EnterHarpoonAim();
            for (var index = 0; index < 30; index++)
            {
                yield return null;
            }
            Assert.That(rig.Mode, Is.EqualTo(DroneCameraMode.HarpoonAim));
            Assert.That(cameraObject.GetComponents<AudioListener>(), Has.Length.EqualTo(1));
            Assert.That(Vector3.Distance(camera.transform.position, belly.position),
                Is.LessThan(Vector3.Distance(camera.transform.position, root.transform.position)));
            Assert.That(Vector3.Angle(camera.transform.forward, Vector3.down), Is.LessThan(1f));

            rig.ExitHarpoonAim();
            yield return null;
            Assert.That(rig.Mode, Is.EqualTo(DroneCameraMode.Orbit));
            Assert.That(root.GetComponent<Rigidbody>().angularVelocity, Is.EqualTo(Vector3.zero));
            Object.Destroy(cameraObject);
            Object.Destroy(root);
        }
    }
}
