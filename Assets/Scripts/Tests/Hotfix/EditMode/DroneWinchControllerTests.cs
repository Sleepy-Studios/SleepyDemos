using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Hotfix.Tests
{
    public sealed class DroneWinchControllerTests
    {
        [Test]
        public void Step_CompletesDeployAndRetractWithoutWaitingForUpdateLoop()
        {
            var controllerObject = new GameObject("Controller");
            var controller = controllerObject.AddComponent<DroneFlightController>();
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            controller.Configure(config, false);

            var winchObject = new GameObject("Winch");
            winchObject.AddComponent<Rigidbody>().isKinematic = true;
            var joint = winchObject.AddComponent<ConfigurableJoint>();
            joint.connectedAnchor = new Vector3(0f, -0.12f, 0f);
            var winch = winchObject.AddComponent<DroneWinchController>();
            winch.Configure(controller, joint, null);

            winch.Toggle();
            Assert.That(winch.State, Is.EqualTo(DroneWinchState.Deploying));
            winch.Step(2f);
            Assert.That(winch.State, Is.EqualTo(DroneWinchState.Deployed));
            Assert.That(winch.CurrentLengthMeters, Is.EqualTo(config.WinchDeployedLengthMeters));

            winch.Toggle();
            Assert.That(winch.State, Is.EqualTo(DroneWinchState.Retracting));
            winch.Step(2f);
            Assert.That(winch.State, Is.EqualTo(DroneWinchState.Stowed));
            Assert.That(winch.CurrentLengthMeters, Is.EqualTo(config.WinchStowedLengthMeters));

            Object.DestroyImmediate(winchObject);
            Object.DestroyImmediate(controllerObject);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void SuspensionPreview_InterpolatesFromParkingToDeployedPose()
        {
            var droneObject = new GameObject("Drone");
            var droneBody = droneObject.AddComponent<Rigidbody>();
            droneBody.isKinematic = true;
            var parking = new GameObject("Parking").transform;
            parking.SetParent(droneObject.transform, false);
            parking.localPosition = new Vector3(0f, -0.1f, 0f);
            var dynamicRoot = new GameObject("DynamicRoot").transform;
            dynamicRoot.SetParent(droneObject.transform, false);
            var linkObject = new GameObject("Link");
            linkObject.transform.SetParent(dynamicRoot, false);
            linkObject.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            var linkBody = linkObject.AddComponent<Rigidbody>();
            var collider = linkObject.AddComponent<BoxCollider>();
            var rig = droneObject.AddComponent<DroneSuspensionRig>();
            rig.Configure(droneBody, parking, new[] { linkBody }, new Collider[] { collider });

            rig.SetDeploymentProgress(0.5f);

            Assert.That(linkBody.position.y, Is.EqualTo(-0.3f).Within(0.001f));
            Assert.That(linkObject.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(linkBody.isKinematic, Is.True);
            Assert.That(collider.enabled, Is.False);
            Object.DestroyImmediate(droneObject);
        }

        [Test]
        public void DeployingNearCompletion_RampsHardwareFeedForwardBeforePhysicsActivation()
        {
            var droneObject = new GameObject("Drone");
            var droneBody = droneObject.AddComponent<Rigidbody>();
            droneBody.isKinematic = true;
            var controller = droneObject.AddComponent<DroneFlightController>();
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            controller.Configure(config, false);
            var parking = new GameObject("Parking").transform;
            parking.SetParent(droneObject.transform, false);
            var hardwareObject = new GameObject("Hardware");
            hardwareObject.transform.SetParent(droneObject.transform, false);
            hardwareObject.transform.localPosition = Vector3.down * 0.5f;
            var hardwareBody = hardwareObject.AddComponent<Rigidbody>();
            hardwareBody.mass = 0.05f;
            var hardwareCollider = hardwareObject.AddComponent<BoxCollider>();
            var rig = droneObject.AddComponent<DroneSuspensionRig>();
            rig.Configure(droneBody, parking, new[] { hardwareBody }, new Collider[] { hardwareCollider });
            var joint = hardwareObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = droneBody;
            var winch = droneObject.AddComponent<DroneWinchController>();
            winch.Configure(controller, joint, null, rig);

            winch.Toggle();
            winch.Step(0.95f);

            Assert.That(winch.State, Is.EqualTo(DroneWinchState.Deploying));
            Assert.That(rig.IsPhysicsActive, Is.False);
            Assert.That(winch.HardwareMassKilograms, Is.GreaterThan(0f));
            Assert.That(winch.HardwareMassKilograms, Is.LessThanOrEqualTo(0.05f));
            Object.DestroyImmediate(droneObject);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void RuntimeConfiguredHardwareMass_IsRedistributedAcrossPhysicalBodies()
        {
            var droneObject = new GameObject("Drone");
            var droneBody = droneObject.AddComponent<Rigidbody>();
            droneBody.isKinematic = true;
            var first = new GameObject("First").AddComponent<Rigidbody>();
            first.transform.SetParent(droneObject.transform, false);
            first.mass = 0.02f;
            var second = new GameObject("Second").AddComponent<Rigidbody>();
            second.transform.SetParent(droneObject.transform, false);
            second.mass = 0.08f;
            var rig = droneObject.AddComponent<DroneSuspensionRig>();
            rig.Configure(droneBody, droneObject.transform, new[] { first, second }, System.Array.Empty<Collider>());

            rig.SetTotalHardwareMass(0.05f);

            Assert.That(first.mass + second.mass, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(first.mass / second.mass, Is.EqualTo(0.25f).Within(0.0001f));
            Object.DestroyImmediate(droneObject);
        }

        [Test]
        public void PhysicsActivation_AlignsTopJointWithCurrentWinchAnchor()
        {
            var droneObject = new GameObject("AlignedDrone");
            var droneBody = droneObject.AddComponent<Rigidbody>();
            droneBody.isKinematic = true;
            var parking = new GameObject("Parking").transform;
            parking.SetParent(droneObject.transform, false);
            var linkObject = new GameObject("Link");
            linkObject.transform.SetParent(droneObject.transform, false);
            linkObject.transform.localPosition = new Vector3(0f, -0.66f, 0f);
            var linkBody = linkObject.AddComponent<Rigidbody>();
            var joint = linkObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = droneBody;
            joint.autoConfigureConnectedAnchor = false;
            joint.anchor = new Vector3(0f, 0.09f, 0f);
            joint.connectedAnchor = new Vector3(0f, -0.17f, 0f);
            var rig = droneObject.AddComponent<DroneSuspensionRig>();
            rig.Configure(droneBody, parking, new[] { linkBody }, System.Array.Empty<Collider>());

            rig.SetDeploymentProgress(1f);
            rig.SetPhysicsActive(true);

            var anchorDistance = Vector3.Distance(
                joint.transform.TransformPoint(joint.anchor),
                droneBody.transform.TransformPoint(joint.connectedAnchor));
            Assert.That(anchorDistance, Is.LessThan(0.001f),
                $"启用物理前顶端锚点必须与当前卷扬长度一致，不能依赖 Joint 投影瞬间纠偏。"
                + $" link={linkBody.position} bodyAnchor={joint.transform.TransformPoint(joint.anchor)}"
                + $" ownerAnchor={droneBody.transform.TransformPoint(joint.connectedAnchor)} connected={joint.connectedBody?.name}");
            Assert.That(linkBody.position.y, Is.EqualTo(-0.26f).Within(0.001f));
            Object.DestroyImmediate(droneObject);
        }
    }
}
