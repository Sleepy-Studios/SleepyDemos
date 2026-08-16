using System.Collections;
using System.Linq;
using Hotfix.DroneFlight;
using Hotfix.DroneFlight.Adapters;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
        public IEnumerator GimbalView_UsesBakedUnityAxesAndPreservesImportedBindPose()
        {
            var root = new GameObject("GimbalOpticalAxisFixture");
            var yaw = new GameObject("GimbalYaw").transform;
            yaw.SetParent(root.transform, false);
            var pitch = new GameObject("GimbalPitch").transform;
            pitch.SetParent(yaw, false);
            var cameraBody = new GameObject("CameraBody").transform;
            cameraBody.SetParent(pitch, false);
            cameraBody.localPosition = new Vector3(0f, 0f, 0.05f);
            var yawBindRotation = Quaternion.Euler(0f, 5f, 0f);
            var pitchBindRotation = Quaternion.Euler(3f, 0f, 0f);
            yaw.localRotation = yawBindRotation;
            pitch.localRotation = pitchBindRotation;

            var cameraObject = new GameObject("GimbalOutput", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var rig = cameraObject.AddComponent<DroneCameraRig>();
            rig.Configure(camera, root.transform, yaw, pitch, null, null, cameraBody);
            rig.SetMode(DroneCameraMode.Gimbal);
            rig.ApplyLookInput(1f, 0f, 1.5f);

            yield return new WaitForSecondsRealtime(0.8f);

            var visibleLensDirection = cameraBody.forward;
            Assert.That(Vector3.Angle(rig.GimbalOpticalForward, visibleLensDirection), Is.LessThan(0.01f));
            Assert.That(Vector3.Angle(camera.transform.forward, visibleLensDirection), Is.LessThan(1f));
            Assert.That(Vector3.Distance(camera.transform.position, cameraBody.position), Is.LessThan(0.01f));
            Assert.That(Quaternion.Angle(
                    yaw.localRotation,
                    yawBindRotation * Quaternion.AngleAxis(90f, Vector3.up)),
                Is.LessThan(0.01f));

            rig.ApplyLookInput(0f, 1f, 1.5f);
            yield return new WaitForSecondsRealtime(0.8f);

            visibleLensDirection = cameraBody.forward;
            Assert.That(Vector3.Angle(camera.transform.forward, visibleLensDirection), Is.LessThan(1f));
            Assert.That(Quaternion.Angle(
                    pitch.localRotation,
                    pitchBindRotation * Quaternion.AngleAxis(-90f, Vector3.right)),
                Is.LessThan(0.01f));

            Object.Destroy(cameraObject);
            Object.Destroy(root);
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator FormalDronePrototype_GimbalModelAndOutputUseBakedUnityAxes()
        {
            const string prefabPath =
                "Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var drone = Object.Instantiate(source);
            try
            {
                var yaw = drone.GetComponentsInChildren<Transform>(true)
                    .Single(node => node.name == DroneFlightModelContract.GimbalYawName);
                var pitch = drone.GetComponentsInChildren<Transform>(true)
                    .Single(node => node.name == DroneFlightModelContract.GimbalPitchName);
                var cameraBody = drone.GetComponentsInChildren<Transform>(true)
                    .Single(node => node.name == DroneFlightModelContract.CameraBodyName);
                var rig = drone.GetComponentInChildren<DroneCameraRig>(true);
                var camera = rig.OutputCamera;
                var yawBindRotation = yaw.localRotation;
                var pitchBindRotation = pitch.localRotation;

                rig.SetMode(DroneCameraMode.Gimbal);
                rig.ApplyLookInput(1f, 0f, 1.5f);
                yield return new WaitForSecondsRealtime(0.8f);

                Assert.That(Quaternion.Angle(
                        yaw.localRotation,
                        yawBindRotation * Quaternion.AngleAxis(90f, DroneFlightModelContract.GimbalYawAxis)),
                    Is.LessThan(0.01f));
                Assert.That(Vector3.Angle(cameraBody.forward, drone.transform.right), Is.LessThan(0.1f));
                Assert.That(Vector3.Angle(camera.transform.forward, cameraBody.forward), Is.LessThan(1f));

                rig.ApplyLookInput(-1f, 0f, 3f);
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.That(Vector3.Angle(cameraBody.forward, -drone.transform.right), Is.LessThan(0.1f));
                Assert.That(Vector3.Angle(camera.transform.forward, cameraBody.forward), Is.LessThan(1f));

                rig.ApplyLookInput(1f, 0f, 1.5f);
                rig.ApplyLookInput(0f, 1f, 1.5f);
                yield return new WaitForSecondsRealtime(0.8f);
                Assert.That(Quaternion.Angle(
                        pitch.localRotation,
                        pitchBindRotation * Quaternion.AngleAxis(-90f, DroneFlightModelContract.GimbalPitchAxis)),
                    Is.LessThan(0.01f));
                Assert.That(Vector3.Angle(cameraBody.forward, drone.transform.up), Is.LessThan(0.1f));
                Assert.That(Vector3.Angle(rig.GimbalOpticalForward, cameraBody.forward), Is.LessThan(0.01f));
            }
            finally
            {
                Object.Destroy(drone);
            }
        }
#endif

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
            yield return new WaitForSecondsRealtime(0.6f);
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

        [UnityTest]
        public IEnumerator ThirdPerson_UsesCollisionSafePositionWithoutChangingBodyPhysics()
        {
            var root = new GameObject("CameraCollisionFixture", typeof(Rigidbody));
            var body = root.GetComponent<Rigidbody>();
            body.useGravity = false;
            body.linearVelocity = Vector3.right;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 0.85f, -1.2f);
            wall.transform.localScale = new Vector3(3f, 3f, 0.2f);
            var cameraObject = new GameObject("CollisionSafeCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var rig = cameraObject.AddComponent<DroneCameraRig>();
            rig.Configure(camera, root.transform, null, null, root.transform, root.transform);
            var initialVelocity = body.linearVelocity;
            yield return new WaitForSecondsRealtime(0.6f);

            Assert.That(camera.transform.position.z, Is.GreaterThan(-1.2f));
            Assert.That(body.linearVelocity, Is.EqualTo(initialVelocity));
            Object.Destroy(cameraObject);
            Object.Destroy(wall);
            Object.Destroy(root);
        }
    }
}
