using System.Linq;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Tests
{
    public sealed class DronePrototypeContractTests
    {
        private const string PrefabPath = "Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab";
        private const string ConfigPath = "Assets/LoadResources/Demos/drone_flight/Data/DroneFlightConfig.asset";

        [Test]
        public void Prefab_ContainsExactlyOneConfiguredRotorForEveryPosition()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab, Is.Not.Null);
            var rotors = prefab.GetComponentsInChildren<DroneRotor>(true);
            Assert.That(rotors, Has.Length.EqualTo(4));
            Assert.That(rotors.Select(rotor => rotor.Position).Distinct().Count(), Is.EqualTo(4));
            Assert.That(rotors.All(rotor => rotor.VisualPropeller != null), Is.True);
            Assert.That(rotors.All(rotor => rotor.VisualPropeller.GetComponent<DroneRotorVisual>() != null), Is.True);
            Assert.That(rotors.All(rotor => rotor.VisualPropeller.Find("Hub") != null), Is.True);
            Assert.That(rotors.All(rotor => rotor.VisualPropeller.Find("Blade") != null), Is.True);
        }

        [Test]
        public void Prefab_ContainsLandingGearSinglePendulumAndSixCompoundColliderClaws()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab.GetComponent<DroneLandingGearController>(), Is.Not.Null);
            var landingGear = prefab.transform.Find("LandingGear");
            Assert.That(landingGear, Is.Not.Null);
            Assert.That(landingGear.Cast<Transform>().Count(), Is.EqualTo(4));
            Assert.That(prefab.transform.Find("SuspensionRig/DynamicBodies/ChainLink_1"), Is.Null);
            Assert.That(prefab.transform.Find("PayloadMount"), Is.Null, "旧双爪视觉节点必须被六爪抓斗替换。");
            var contactCollectors = prefab.GetComponentsInChildren<DroneGrappleContactCollector>(true);
            Assert.That(contactCollectors, Has.Length.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<DroneGrappleContactSensor>(true), Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<HingeJoint>(true), Is.Empty);

            var suspensionRig = prefab.GetComponent<DroneSuspensionRig>();
            var grappleRoot = suspensionRig.GrappleBody != null
                ? suspensionRig.GrappleBody.transform
                : null;
            Assert.That(grappleRoot, Is.Not.Null);
            var claws = Enumerable.Range(1, 6)
                .Select(index => grappleRoot.Find($"Claw_{index}"))
                .ToArray();
            Assert.That(claws.All(claw => claw != null), Is.True);
            Assert.That(claws.All(claw => claw.localScale == Vector3.one), Is.True);
            Assert.That(claws.All(claw => claw.Find("UpperSegment") != null), Is.True);
            Assert.That(claws.All(claw => claw.Find("TipSegment") != null), Is.True);
            Assert.That(claws.Count(claw => claw.Find("UpperSegment").GetComponent<MeshFilter>() != null), Is.EqualTo(6));
            Assert.That(claws.Count(claw => claw.Find("TipSegment").GetComponent<MeshFilter>() != null), Is.EqualTo(6));
            Assert.That(claws.All(claw => claw.Find("UpperSegment").localScale == Vector3.one * 0.7f), Is.True);
            Assert.That(claws.All(claw => claw.Find("TipSegment").localScale == Vector3.one * 0.7f), Is.True);
            Assert.That(contactCollectors[0].ConfiguredClawCount, Is.EqualTo(6),
                "六爪 Collider 编号必须写入 Prefab，不能只保存在生成器进程的运行时字典中。");
            for (var clawIndex = 0; clawIndex < claws.Length; clawIndex++)
            {
                foreach (var clawCollider in claws[clawIndex].GetComponentsInChildren<Collider>(true))
                {
                    Assert.That(contactCollectors[0].TryGetClawIndex(clawCollider, out var resolvedIndex), Is.True);
                    Assert.That(resolvedIndex, Is.EqualTo(clawIndex));
                }
            }

            var hook = prefab.GetComponent<DroneMechanicalHook>();
            var baseRotations = new UnityEditor.SerializedObject(hook).FindProperty("clawBaseRotations");
            Assert.That(baseRotations.arraySize, Is.EqualTo(6),
                "爪动画中性姿态必须持久化，避免运行时把已张开的姿态再次当作旋转基准。");

            var suspensionJoints = grappleRoot.GetComponents<ConfigurableJoint>();
            Assert.That(suspensionJoints, Has.Length.EqualTo(1));
            var suspensionJoint = suspensionJoints.Single();
            Assert.That(float.IsPositiveInfinity(suspensionJoint.breakForce), Is.True);
            Assert.That(float.IsPositiveInfinity(suspensionJoint.breakTorque), Is.True);
            Assert.That(suspensionJoint.projectionMode, Is.EqualTo(JointProjectionMode.None));
            Assert.That(suspensionJoint.axis, Is.EqualTo(Vector3.up));
            Assert.That(suspensionJoint.secondaryAxis, Is.EqualTo(Vector3.forward));
            Assert.That(suspensionJoint.rotationDriveMode, Is.EqualTo(RotationDriveMode.Slerp));
            Assert.That(suspensionJoint.slerpDrive.useAcceleration, Is.True);

            var mechanismMass = suspensionRig.HardwareMassKilograms;
            var config = AssetDatabase.LoadAssetAtPath<DroneFlightConfig>(ConfigPath);
            Assert.That(suspensionJoint.highAngularXLimit.limit, Is.EqualTo(config.SuspensionTwistLimitDegrees));
            Assert.That(suspensionJoint.angularYLimit.limit, Is.EqualTo(config.SuspensionSwingLimitDegrees));
            Assert.That(suspensionJoint.angularZLimit.limit, Is.EqualTo(config.SuspensionSwingLimitDegrees));
            Assert.That(suspensionJoint.anchor.y, Is.EqualTo(config.WinchStowedLengthMeters).Within(0.0001f));
            Assert.That(mechanismMass, Is.EqualTo(config.GrappleHardwareMassKilograms).Within(0.0001f));
            Assert.That(config.GrappleHardwareMassKilograms, Is.EqualTo(0.05f).Within(0.0001f));
            Assert.That(config.WinchDeployedLengthMeters, Is.EqualTo(0.45f).Within(0.0001f));
            Assert.That(prefab.GetComponentsInChildren<Rigidbody>(true)
                .Count(body => body != prefab.GetComponent<Rigidbody>()), Is.EqualTo(1));
            Assert.That(grappleRoot.GetComponent<Rigidbody>().interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate));
            Assert.That(grappleRoot.GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True);
            Assert.That(suspensionJoint.connectedBody, Is.Null,
                "抓斗在 Prefab 收纳态必须断开单摆关节，部署时再连接无人机刚体。");
            Assert.That(prefab.GetComponent<DroneWinchController>().SupportedMassKilograms, Is.Zero,
                "抓斗收纳时不得向飞控贡献设备或载荷质量。");
        }

        [Test]
        public void Prefab_RotorDirectionsMatchDocumentedXLayout()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var rotors = prefab.GetComponentsInChildren<DroneRotor>(true)
                .ToDictionary(rotor => rotor.Position);

            Assert.That(rotors[DroneRotorPosition.FrontLeft].Direction, Is.EqualTo(DroneRotorDirection.CounterClockwise));
            Assert.That(rotors[DroneRotorPosition.FrontRight].Direction, Is.EqualTo(DroneRotorDirection.Clockwise));
            Assert.That(rotors[DroneRotorPosition.RearLeft].Direction, Is.EqualTo(DroneRotorDirection.Clockwise));
            Assert.That(rotors[DroneRotorPosition.RearRight].Direction, Is.EqualTo(DroneRotorDirection.CounterClockwise));

            Assert.That(rotors[DroneRotorPosition.FrontLeft].transform.localPosition.x, Is.LessThan(0f));
            Assert.That(rotors[DroneRotorPosition.FrontLeft].transform.localPosition.z, Is.GreaterThan(0f));
            Assert.That(rotors[DroneRotorPosition.RearRight].transform.localPosition.x, Is.GreaterThan(0f));
            Assert.That(rotors[DroneRotorPosition.RearRight].transform.localPosition.z, Is.LessThan(0f));
        }

        [Test]
        public void Prefab_DynamicBodyUsesPrimitiveCompositeColliders()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab.GetComponentsInChildren<BoxCollider>(true).Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(prefab.GetComponentsInChildren<MeshCollider>(true), Is.Empty);
        }

        [Test]
        public void Config_DefaultValuesArePhysicallyValid()
        {
            var config = AssetDatabase.LoadAssetAtPath<DroneFlightConfig>(ConfigPath);

            Assert.That(config, Is.Not.Null);
            Assert.That(config.TryValidate(out var diagnostic), Is.True, diagnostic);
            Assert.That(config.MaximumRpm, Is.GreaterThan(0f));
            Assert.That(config.ThrustCoefficient, Is.GreaterThan(0f));
        }

        [Test]
        public void Prefab_DeployedSuspensionRemainsConnectedDuringPhysicsSimulation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(
                instance,
                PrefabUnpackMode.Completely,
                InteractionMode.AutomatedAction);
            var droneBody = instance.GetComponent<Rigidbody>();
            droneBody.isKinematic = true;
            instance.transform.position = Vector3.up * 5f;
            var rig = instance.GetComponent<DroneSuspensionRig>();
            var grapple = rig.GrappleBody;
            var mechanismJoints = grapple.GetComponentsInChildren<Joint>(true);
            var previousMode = Physics.simulationMode;

            try
            {
                Physics.simulationMode = SimulationMode.Script;
                rig.SetDeploymentProgress(1f);
                rig.SetCableLength(0.45f);
                rig.SetPhysicsActive(true);
                Assert.That(mechanismJoints.All(joint => joint.connectedBody != null), Is.True);
                var suspensionJoint = grapple.GetComponent<ConfigurableJoint>();
                Assert.That(
                    Vector3.Distance(
                        suspensionJoint.transform.TransformPoint(suspensionJoint.anchor),
                        droneBody.transform.TransformPoint(suspensionJoint.connectedAnchor)),
                    Is.LessThan(0.001f),
                    "真实 Prefab 启用单摆物理前必须按当前卷扬长度对齐锚点。");
                for (var step = 0; step < 150; step++)
                {
                    Physics.Simulate(0.02f);
                }

                Assert.That(grapple.GetComponent<ConfigurableJoint>(), Is.Not.Null);
                Assert.That(Vector3.Distance(droneBody.position, grapple.position), Is.LessThan(0.8f));
            }
            finally
            {
                rig.SetPhysicsActive(false);
                Physics.simulationMode = previousMode;
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Prefab_SinglePendulumPassiveDampingSettlesTwentyDegreeSwingWithinFiveSeconds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var droneBody = instance.GetComponent<Rigidbody>();
            droneBody.isKinematic = true;
            instance.transform.position = Vector3.up * 5f;
            var rig = instance.GetComponent<DroneSuspensionRig>();
            var grapple = rig.GrappleBody;
            var joint = grapple.GetComponent<ConfigurableJoint>();
            var previousMode = Physics.simulationMode;

            try
            {
                Physics.simulationMode = SimulationMode.Script;
                rig.SetCableLength(0.45f);
                rig.SetPhysicsActive(true);
                var ownerAnchor = droneBody.transform.TransformPoint(joint.connectedAnchor);
                var tiltedRotation = Quaternion.AngleAxis(20f, droneBody.transform.forward) * droneBody.rotation;
                grapple.transform.SetPositionAndRotation(ownerAnchor - tiltedRotation * joint.anchor, tiltedRotation);
                grapple.linearVelocity = Vector3.zero;
                grapple.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();
                Assert.That(rig.JointTelemetry.SwingDegrees, Is.EqualTo(20f).Within(1f));

                for (var step = 0; step < 250; step++)
                {
                    rig.ApplyPassiveDampingDrive(0f);
                    Physics.Simulate(0.02f);
                }

                Assert.That(rig.JointTelemetry.SwingDegrees, Is.LessThan(3f));
            }
            finally
            {
                rig.SetPhysicsActive(false);
                Physics.simulationMode = previousMode;
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void Prefab_StowedSuspensionDoesNotTipDroneOnLandingGear()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var droneBody = instance.GetComponent<Rigidbody>();
            var landingGear = instance.transform.Find("LandingGear");
            var feet = landingGear.GetComponentsInChildren<Collider>(true);
            var ground = new GameObject("DroneLandingStabilityGround");
            var groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(20f, 0.1f, 20f);
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            var previousMode = Physics.simulationMode;

            try
            {
                instance.GetComponent<DroneSuspensionRig>().SetPhysicsActive(false);
                Assert.That(instance.GetComponentsInChildren<Joint>(true)
                    .All(joint => joint.connectedBody == null), Is.True,
                    "停靠状态必须断开所有内部 Joint 与无人机的求解连接。");
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Physics.SyncTransforms();
                var lowestFoot = feet.Min(collider => collider.bounds.min.y);
                instance.transform.position = Vector3.up * (0.015f - lowestFoot);
                droneBody.linearVelocity = Vector3.zero;
                droneBody.angularVelocity = Vector3.zero;
                Physics.SyncTransforms();

                Physics.simulationMode = SimulationMode.Script;
                for (var step = 0; step < 150; step++)
                {
                    Physics.Simulate(0.02f);
                }

                Assert.That(Vector3.Angle(instance.transform.up, Vector3.up), Is.LessThan(3f),
                    "无人机应能依靠四个放下的脚架稳定停在水平地面。");
                Assert.That(droneBody.angularVelocity.magnitude, Is.LessThan(0.2f));
                Assert.That(instance.GetComponentsInChildren<Joint>(true)
                    .All(joint => joint.connectedBody == null), Is.True);
            }
            finally
            {
                Physics.simulationMode = previousMode;
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(ground);
            }
        }
    }
}
