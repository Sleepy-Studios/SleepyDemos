using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Hotfix.Tests
{
    public sealed class DroneWinchControllerTests
    {
        [Test]
        public void Step_CompletesDeployAndRetractWithoutUpdateLoop()
        {
            var droneObject = new GameObject("Controller");
            var controller = droneObject.AddComponent<DroneFlightController>();
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            controller.Configure(config, false);
            var winch = droneObject.AddComponent<DroneWinchController>();
            winch.Configure(controller, null, null);

            winch.Toggle();
            winch.Step(2f);
            Assert.That(winch.State, Is.EqualTo(DroneWinchState.Deployed));
            Assert.That(winch.CurrentLengthMeters, Is.EqualTo(config.WinchDeployedLengthMeters));

            winch.Toggle();
            winch.Step(2f);
            Assert.That(winch.State, Is.EqualTo(DroneWinchState.Stowed));
            Assert.That(winch.CurrentLengthMeters, Is.EqualTo(config.WinchStowedLengthMeters));

            Object.DestroyImmediate(droneObject);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void SuspensionRig_UsesSingleHardwareBodyAndInheritsOwnerPointVelocity()
        {
            var droneObject = new GameObject("Drone");
            var droneBody = droneObject.AddComponent<Rigidbody>();
            droneBody.useGravity = false;
            droneBody.linearVelocity = new Vector3(2f, 0.5f, -1f);
            droneBody.angularVelocity = new Vector3(0f, 1f, 0f);
            var parking = new GameObject("Parking").transform;
            parking.SetParent(droneObject.transform, false);
            parking.localPosition = new Vector3(0f, -0.1f, 0f);
            var grappleObject = new GameObject("GrappleBody");
            grappleObject.transform.SetParent(droneObject.transform, false);
            var grappleBody = grappleObject.AddComponent<Rigidbody>();
            grappleBody.mass = 0.05f;
            var collider = grappleObject.AddComponent<BoxCollider>();
            var joint = grappleObject.AddComponent<ConfigurableJoint>();
            joint.connectedAnchor = new Vector3(0f, -0.12f, 0f);
            var rig = droneObject.AddComponent<DroneSuspensionRig>();
            rig.Configure(droneBody, parking, grappleBody, new Collider[] { collider }, null, joint, null);
            rig.SetCableLength(0.45f);
            var expectedVelocity = droneBody.GetPointVelocity(droneBody.transform.TransformPoint(joint.connectedAnchor));

            rig.SetPhysicsActive(true);

            Assert.That(rig.HardwareMassKilograms, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(Vector3.Distance(grappleBody.linearVelocity, expectedVelocity), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(grappleBody.angularVelocity, droneBody.angularVelocity), Is.LessThan(0.0001f));
            Assert.That(joint.connectedBody, Is.EqualTo(droneBody));
            Assert.That(joint.projectionMode, Is.EqualTo(JointProjectionMode.None));
            Assert.That(Vector3.Distance(
                joint.transform.TransformPoint(joint.anchor),
                droneBody.transform.TransformPoint(joint.connectedAnchor)), Is.LessThan(0.001f));

            Object.DestroyImmediate(droneObject);
        }

        [Test]
        public void SuspensionRig_CanDockUsesGrappleRootInsteadOfOffsetCenterOfMass()
        {
            var droneObject = new GameObject("DockingDrone");
            var droneBody = droneObject.AddComponent<Rigidbody>();
            droneBody.isKinematic = true;
            var parking = new GameObject("Parking").transform;
            parking.SetParent(droneObject.transform, false);
            var grappleObject = new GameObject("GrappleBody");
            var grappleBody = grappleObject.AddComponent<Rigidbody>();
            grappleBody.useGravity = false;
            var offsetColliderObject = new GameObject("OffsetCollider");
            offsetColliderObject.transform.SetParent(grappleObject.transform, false);
            offsetColliderObject.transform.localPosition = Vector3.down * 0.08f;
            var collider = offsetColliderObject.AddComponent<BoxCollider>();
            var joint = grappleObject.AddComponent<ConfigurableJoint>();
            joint.connectedAnchor = new Vector3(0f, -0.12f, 0f);
            var rig = droneObject.AddComponent<DroneSuspensionRig>();
            rig.Configure(droneBody, parking, grappleBody, new Collider[] { collider }, null, joint, null);
            rig.SetCableLength(0.08f);
            grappleBody.position = droneBody.transform.TransformPoint(joint.connectedAnchor) - Vector3.up * 0.08f;
            grappleBody.linearVelocity = Vector3.zero;

            Assert.That(Vector3.Distance(grappleBody.worldCenterOfMass, grappleBody.position), Is.GreaterThan(0.02f));
            Assert.That(rig.CanDock(0.02f, 0.15f), Is.True,
                "复合 Collider 会让质心偏离抓斗根；停靠判定应使用实际停靠根位置。");

            Object.DestroyImmediate(droneObject);
            Object.DestroyImmediate(grappleObject);
        }

        [Test]
        public void LoadTransferEstimator_FollowsGroundSupportThenSettlesAirborne()
        {
            var estimator = new DronePayloadLoadTransferEstimator();
            var weight = 0.75f * Mathf.Abs(Physics.gravity.y);

            estimator.Step(0.75f, true, true, weight, 0.15f, 0.02f);
            Assert.That(estimator.SupportedFraction, Is.Zero);
            Assert.That(estimator.State, Is.EqualTo(DronePayloadSupportState.GroundSupported));

            for (var step = 0; step < 20; step++)
            {
                estimator.Step(0.75f, true, true, weight * 0.5f, 0.15f, 0.02f);
            }
            Assert.That(estimator.SupportedFraction, Is.EqualTo(0.5f).Within(0.04f));

            for (var step = 0; step < 60; step++)
            {
                estimator.Step(0.75f, true, false, 0f, 0.15f, 0.02f);
            }
            Assert.That(estimator.SupportedFraction, Is.EqualTo(1f).Within(0.01f));
            Assert.That(estimator.State, Is.EqualTo(DronePayloadSupportState.AirborneSupported));

            for (var step = 0; step < 60; step++)
            {
                estimator.Step(0.75f, true, true, weight, 0.15f, 0.02f);
            }
            Assert.That(estimator.SupportedFraction, Is.EqualTo(0f).Within(0.01f));
            Assert.That(estimator.State, Is.EqualTo(DronePayloadSupportState.GroundSupported));
        }

        [Test]
        public void Winch_ReportsInstalledHardwareExactlyOnceAcrossDeployment()
        {
            var droneObject = new GameObject("MassInvariantDrone");
            var droneBody = droneObject.AddComponent<Rigidbody>();
            droneBody.isKinematic = true;
            var controller = droneObject.AddComponent<DroneFlightController>();
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            controller.Configure(config, false);
            var parking = new GameObject("Parking").transform;
            parking.SetParent(droneObject.transform, false);
            var grappleObject = new GameObject("GrappleBody");
            grappleObject.transform.SetParent(droneObject.transform, false);
            var grappleBody = grappleObject.AddComponent<Rigidbody>();
            grappleBody.mass = config.GrappleHardwareMassKilograms;
            var collider = grappleObject.AddComponent<BoxCollider>();
            var joint = grappleObject.AddComponent<ConfigurableJoint>();
            var rig = droneObject.AddComponent<DroneSuspensionRig>();
            rig.Configure(droneBody, parking, grappleBody, new Collider[] { collider }, config, joint, null);
            var winch = droneObject.AddComponent<DroneWinchController>();
            winch.Configure(controller, joint, null, rig);
            controller.ConfigureExternalMassProvider(winch);

            Assert.That(winch.InstalledHardwareMassKilograms, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(winch.HardwareMassKilograms, Is.Zero);
            Assert.That(droneBody.mass, Is.EqualTo(config.BodyMassKilograms + 0.05f).Within(0.0001f));
            var stowedTotalMass = droneBody.mass + winch.HardwareMassKilograms;
            winch.Toggle();
            winch.Step(2f);
            Assert.That(winch.HardwareMassKilograms, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(winch.SupportedMassKilograms, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(droneBody.mass, Is.EqualTo(config.BodyMassKilograms).Within(0.0001f));
            Assert.That(droneBody.mass + winch.HardwareMassKilograms,
                Is.EqualTo(stowedTotalMass).Within(0.001f));

            Object.DestroyImmediate(droneObject);
            Object.DestroyImmediate(config);
        }
    }
}
