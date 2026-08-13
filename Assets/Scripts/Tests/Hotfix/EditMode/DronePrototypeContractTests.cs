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
        public void Prefab_ContainsLandingGearSingleLinkSuspensionAndSixScaledPhysicalClaws()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);

            Assert.That(prefab.GetComponent<DroneLandingGearController>(), Is.Not.Null);
            var landingGear = prefab.transform.Find("LandingGear");
            Assert.That(landingGear, Is.Not.Null);
            Assert.That(landingGear.Cast<Transform>().Count(), Is.EqualTo(4));
            Assert.That(prefab.transform.Find("SuspensionRig/DynamicBodies/ChainLink_1"), Is.Not.Null);
            Assert.That(prefab.transform.Find("SuspensionRig/DynamicBodies/ChainLink_2"), Is.Null);
            Assert.That(prefab.transform.Find("PayloadMount"), Is.Null, "旧双爪视觉节点必须被六爪抓斗替换。");
            Assert.That(prefab.GetComponentsInChildren<DroneGrappleContactSensor>(true), Has.Length.EqualTo(6));
            var joints = prefab.GetComponentsInChildren<HingeJoint>(true);
            Assert.That(joints, Has.Length.EqualTo(6));
            Assert.That(joints.All(joint => joint.useSpring && !joint.useMotor), Is.True);
            Assert.That(joints.All(joint => float.IsPositiveInfinity(joint.breakForce)), Is.True);

            var claws = prefab.GetComponentsInChildren<DroneGrappleContactSensor>(true)
                .Select(sensor => sensor.transform)
                .ToArray();
            Assert.That(claws.All(claw => claw.localScale == Vector3.one), Is.True);
            Assert.That(claws.All(claw => claw.Find("UpperSegment") != null), Is.True);
            Assert.That(claws.All(claw => claw.Find("TipSegment") != null), Is.True);
            Assert.That(claws.Count(claw => claw.Find("UpperSegment").GetComponent<MeshFilter>() != null), Is.EqualTo(6));
            Assert.That(claws.Count(claw => claw.Find("TipSegment").GetComponent<MeshFilter>() != null), Is.EqualTo(6));
            Assert.That(claws.All(claw => claw.Find("UpperSegment").localScale == Vector3.one * 0.7f), Is.True);
            Assert.That(claws.All(claw => claw.Find("TipSegment").localScale == Vector3.one * 0.7f), Is.True);

            var suspensionJoints = prefab.transform.Find("SuspensionRig/DynamicBodies")
                .GetComponentsInChildren<ConfigurableJoint>(true);
            Assert.That(suspensionJoints, Has.Length.EqualTo(2));
            Assert.That(suspensionJoints.All(joint => float.IsPositiveInfinity(joint.breakForce)), Is.True);
            Assert.That(suspensionJoints.All(joint => float.IsPositiveInfinity(joint.breakTorque)), Is.True);
            Assert.That(suspensionJoints.All(joint => joint.projectionMode == JointProjectionMode.PositionAndRotation), Is.True);

            var mechanismMass = prefab.transform.Find("SuspensionRig/DynamicBodies")
                .GetComponentsInChildren<Rigidbody>(true)
                .Sum(body => body.mass);
            var config = AssetDatabase.LoadAssetAtPath<DroneFlightConfig>(ConfigPath);
            Assert.That(mechanismMass, Is.EqualTo(config.GrappleHardwareMassKilograms).Within(0.0001f));
            Assert.That(prefab.transform.Find("SuspensionRig/DynamicBodies")
                .GetComponentsInChildren<Rigidbody>(true)
                .All(body => body.interpolation == RigidbodyInterpolation.Interpolate), Is.True,
                "吊链、抓斗与爪体必须启用 Rigidbody 插值，避免 Game 视图呈现 50 Hz 阶跃抖动。");
            Assert.That(suspensionJoints.All(joint => !joint.enablePreprocessing), Is.True);
            Assert.That(suspensionJoints.All(joint => joint.massScale > 0f && joint.connectedMassScale > 0f), Is.True);
            Assert.That(prefab.transform.Find("SuspensionRig/DynamicBodies")
                .GetComponentsInChildren<Collider>(true).All(collider => !collider.enabled), Is.True);
            Assert.That(prefab.transform.Find("SuspensionRig/DynamicBodies")
                .GetComponentsInChildren<Joint>(true).All(joint => joint.connectedBody != null), Is.True,
                "Prefab 必须保留全部内部 Joint 的原始连接，供运行时部署恢复。");
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
            var chainOne = instance.transform.Find("SuspensionRig/DynamicBodies/ChainLink_1").GetComponent<Rigidbody>();
            var grapple = instance.transform.Find("SuspensionRig/DynamicBodies/GrappleBody").GetComponent<Rigidbody>();
            var mechanismJoints = instance.transform.Find("SuspensionRig/DynamicBodies")
                .GetComponentsInChildren<Joint>(true);
            var previousMode = Physics.simulationMode;

            try
            {
                Physics.simulationMode = SimulationMode.Script;
                rig.SetDeploymentProgress(1f);
                rig.SetPhysicsActive(true);
                Assert.That(mechanismJoints.All(joint => joint.connectedBody != null), Is.True);
                var topJoint = chainOne.GetComponent<ConfigurableJoint>();
                Assert.That(
                    Vector3.Distance(
                        topJoint.transform.TransformPoint(topJoint.anchor),
                        droneBody.transform.TransformPoint(topJoint.connectedAnchor)),
                    Is.LessThan(0.001f),
                    "真实 Prefab 启用吊挂物理前必须按当前卷扬长度对齐顶端锚点。");
                for (var step = 0; step < 150; step++)
                {
                    Physics.Simulate(0.02f);
                }

                Assert.That(chainOne.GetComponent<ConfigurableJoint>(), Is.Not.Null);
                Assert.That(grapple.GetComponent<ConfigurableJoint>(), Is.Not.Null);
                Assert.That(Vector3.Distance(droneBody.position, chainOne.position), Is.LessThan(0.9f));
                Assert.That(Vector3.Distance(droneBody.position, grapple.position), Is.LessThan(1.1f));
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
