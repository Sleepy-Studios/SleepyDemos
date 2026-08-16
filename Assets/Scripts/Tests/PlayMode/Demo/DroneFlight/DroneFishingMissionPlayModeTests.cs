using System.Collections;
using Hotfix.DroneFlight;
using Hotfix.DroneFlight.Adapters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Demo
{
    /*
     * 测试说明：验证捕鱼 MVP 固定机位追踪和渔叉世界坐标自动瞄准的运行时行为。
     */
    public sealed class DroneFishingMissionPlayModeTests
    {
        [UnityTest]
        public IEnumerator CinematicTracker_RotatesAndZoomsWithoutMovingCamera()
        {
            var cameraObject = new GameObject("MissionCamera", typeof(Camera));
            var target = new GameObject("DroneTarget");
            try
            {
                cameraObject.transform.position = new Vector3(2f, 5f, -10f);
                cameraObject.transform.rotation = Quaternion.identity;
                var camera = cameraObject.GetComponent<Camera>();
                camera.fieldOfView = 60f;
                var tracker = cameraObject.AddComponent<DroneCinematicCameraTracker>();
                tracker.CaptureInitialPose();
                target.transform.position = new Vector3(-3f, 3f, 2f);
                var initialPosition = cameraObject.transform.position;
                var initialForward = cameraObject.transform.forward;

                tracker.BeginTracking(target.transform);
                for (var index = 0; index < 12; index++)
                {
                    yield return null;
                }

                Assert.That(cameraObject.transform.position, Is.EqualTo(initialPosition));
                Assert.That(camera.fieldOfView, Is.LessThan(60f));
                Assert.That(Vector3.Dot(cameraObject.transform.forward, initialForward), Is.LessThan(0.999f));
            }
            finally
            {
                Object.Destroy(cameraObject);
                Object.Destroy(target);
            }
        }

        [UnityTest]
        public IEnumerator HarpoonVariant_AutomatedWorldAimCanFireAtTargetBelow()
        {
            const string path =
                "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneHarpoonVariant.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Object.Instantiate(prefab, new Vector3(0f, 2f, 0f), Quaternion.identity);
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "AutomatedHarpoonTarget";
            target.transform.position = new Vector3(0f, -4f, 0f);
            target.AddComponent<Rigidbody>().isKinematic = true;
            try
            {
                var droneBody = instance.GetComponent<Rigidbody>();
                droneBody.isKinematic = true;
                var host = instance.GetComponent<DroneEquipmentHost>();
                Physics.SyncTransforms();

                Assert.That(host.TrySetAutomatedAimTarget(target.transform.position), Is.True);
                for (var index = 0; index < 5 && !host.Snapshot.CanUsePrimary; index++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(host.Snapshot.CanUsePrimary, Is.True, host.LastHint);
                host.PrimaryAction();
                Assert.That(host.State, Is.EqualTo(DroneEquipmentState.Fired));
            }
            finally
            {
                Object.Destroy(instance);
                Object.Destroy(target);
            }
        }
    }
}
