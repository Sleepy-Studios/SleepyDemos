using System.Collections;
using System.Linq;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hotfix.Tests
{
    public sealed class DronePayloadMountTests
    {
        [UnityTest]
        public IEnumerator ConfiguredGate_UpdatesFromRatedPayloadMultiplierWithoutResidualJoint()
        {
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            config.ConfigureAutomaticPayloadTuning(1f, 1.25f, 0.9f);
            var gripObject = new GameObject("DynamicGateGrip");
            var gripBody = gripObject.AddComponent<Rigidbody>();
            var mount = gripObject.AddComponent<PayloadMount>();
            mount.Configure(gripObject.transform, config, gripBody);
            var payloadObject = new GameObject("DynamicGatePayload");
            var payloadBody = payloadObject.AddComponent<Rigidbody>();
            payloadBody.mass = 1.3f;
            var payload = payloadObject.AddComponent<DronePayload>();
            payload.Configure("Cargo");

            Assert.That(mount.TryAssistGrip(payload, 3), Is.False);
            Assert.That(mount.ActiveJoint, Is.Null);
            Assert.That(mount.LastReleaseReason, Is.EqualTo(PayloadReleaseReason.Overload));

            config.ConfigureAutomaticPayloadTuning(1f, 1.5f, 0.9f);
            Assert.That(mount.TryAssistGrip(payload, 3), Is.True);
            Assert.That(mount.MaximumPayloadMassKilograms, Is.EqualTo(1.5f).Within(0.0001f));
            mount.Release();
            yield return null;
            Assert.That(mount.ActiveJoint, Is.Null);

            Object.Destroy(gripObject);
            Object.Destroy(payloadObject);
            Object.Destroy(config);
        }

        [UnityTest]
        public IEnumerator AttachAndRelease_KeepsPayloadIndependentAndLeavesNoJoint()
        {
            var carrier = new GameObject("Carrier");
            carrier.AddComponent<Rigidbody>().useGravity = false;
            var gripObject = new GameObject("GrappleBody");
            gripObject.transform.SetParent(carrier.transform, false);
            var gripBody = gripObject.AddComponent<Rigidbody>();
            gripBody.useGravity = false;
            var mountPoint = new GameObject("MountPoint").transform;
            mountPoint.SetParent(gripObject.transform, false);
            var mount = carrier.AddComponent<PayloadMount>();
            mount.Configure(mountPoint, 0.6f, gripBody);

            var payloadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            payloadObject.name = "Payload";
            var payloadBody = payloadObject.AddComponent<Rigidbody>();
            payloadBody.mass = 0.3f;
            payloadBody.useGravity = false;
            var payload = payloadObject.AddComponent<DronePayload>();
            payload.Configure("Cargo");

            for (var cycle = 0; cycle < 3; cycle++)
            {
                var worldContactPoint = payloadBody.worldCenterOfMass + Vector3.up * 0.1f;
                Assert.That(mount.TryAssistGrip(new DroneGripContactSnapshot(payload, 3, worldContactPoint)), Is.True);
                Assert.That(mount.HasPayload, Is.True);
                Assert.That(payload.transform.parent, Is.Null);
                var joint = gripObject.GetComponent<ConfigurableJoint>();
                Assert.That(joint.connectedBody, Is.EqualTo(payloadBody));
                Assert.That(joint.xMotion, Is.EqualTo(ConfigurableJointMotion.Limited));
                Assert.That(joint.angularXMotion, Is.EqualTo(ConfigurableJointMotion.Free));
                Assert.That(joint.angularYMotion, Is.EqualTo(ConfigurableJointMotion.Free));
                Assert.That(joint.angularZMotion, Is.EqualTo(ConfigurableJointMotion.Free));
                Assert.That(joint.enableCollision, Is.False);
                Assert.That(joint.projectionMode, Is.EqualTo(JointProjectionMode.None));
                Assert.That(Vector3.Distance(
                    joint.transform.TransformPoint(joint.anchor),
                    payloadBody.transform.TransformPoint(joint.connectedAnchor)), Is.LessThan(0.0001f));
                Assert.That(joint.linearLimit.limit, Is.EqualTo(0.025f).Within(0.0001f));
                Assert.That(joint.linearLimitSpring.spring, Is.Zero);
                Assert.That(joint.linearLimitSpring.damper, Is.Zero);
                mount.StepTakeup(0.3f);
                Assert.That(joint.linearLimitSpring.spring, Is.EqualTo(250f).Within(0.1f));
                Assert.That(joint.linearLimitSpring.damper, Is.EqualTo(25f).Within(0.1f));
                Assert.That(payloadBody.interpolation, Is.EqualTo(RigidbodyInterpolation.Interpolate));

                mount.Release();
                yield return null;
                Assert.That(mount.HasPayload, Is.False);
                Assert.That(gripObject.GetComponent<ConfigurableJoint>(), Is.Null);
                Assert.That(payloadBody.isKinematic, Is.False);
            }

            Object.Destroy(carrier);
            Object.Destroy(payloadObject);
        }

        [Test]
        public void PayloadSupport_RequiresStableExternalUpwardContactAndIgnoresMechanismColliders()
        {
            var groundObject = new GameObject("Ground");
            var groundCollider = groundObject.AddComponent<BoxCollider>();
            var mechanismObject = new GameObject("Claw");
            var mechanismCollider = mechanismObject.AddComponent<BoxCollider>();
            var payloadObject = new GameObject("SupportAwarePayload");
            payloadObject.AddComponent<Rigidbody>().useGravity = false;
            var payload = payloadObject.AddComponent<DronePayload>();
            payload.ConfigureIgnoredSupportColliders(new[] { mechanismCollider });

            for (var index = 0; index < 2; index++)
            {
                payload.ReportSupportContact(groundCollider, Vector3.up);
                payload.ReportSupportContact(mechanismCollider, Vector3.up);
                payload.CompleteSupportPhysicsStep();
            }

            Assert.That(payload.IsGroundSupported, Is.False, "两步接触还不能确认落地。");
            payload.ReportSupportContact(groundCollider, Vector3.up);
            payload.ReportSupportContact(mechanismCollider, Vector3.up);
            payload.CompleteSupportPhysicsStep();
            Assert.That(payload.IsGroundSupported, Is.True);
            Assert.That(payload.EffectiveSupportContactCount, Is.EqualTo(1), "抓斗自身 Collider 不得计为地面支撑。");

            payload.ReportSupportContact(groundCollider, Vector3.right);
            payload.CompleteSupportPhysicsStep();
            payload.CompleteSupportPhysicsStep();
            Assert.That(payload.IsGroundSupported, Is.True, "短暂失去支撑不能抖动切换状态。");
            payload.CompleteSupportPhysicsStep();
            Assert.That(payload.IsGroundSupported, Is.False);

            Object.DestroyImmediate(groundObject);
            Object.DestroyImmediate(mechanismObject);
            Object.DestroyImmediate(payloadObject);
        }

        [UnityTest]
        public IEnumerator MechanicalHook_ClosesOnNearbyPayloadAndOpensToRelease()
        {
            var carrier = new GameObject("Carrier");
            carrier.AddComponent<Rigidbody>().useGravity = false;
            var gripObject = new GameObject("GrappleBody");
            gripObject.transform.SetParent(carrier.transform, false);
            var gripBody = gripObject.AddComponent<Rigidbody>();
            gripBody.useGravity = false;
            var mount = carrier.AddComponent<PayloadMount>();
            mount.Configure(gripObject.transform, 0.6f, gripBody);
            var hookObject = new GameObject("Hook");
            hookObject.transform.SetParent(carrier.transform, false);
            var hook = hookObject.AddComponent<DroneMechanicalHook>();

            var payloadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            payloadObject.name = "NearbyPayload";
            payloadObject.transform.position = hookObject.transform.position + Vector3.right * 0.1f;
            var payloadBody = payloadObject.AddComponent<Rigidbody>();
            payloadBody.mass = 0.3f;
            payloadBody.useGravity = false;
            var payload = payloadObject.AddComponent<DronePayload>();
            payload.Configure("NearbyCargo");
            var collector = gripObject.AddComponent<DroneGrappleContactCollector>();
            collector.Configure(System.Array.Empty<Collider[]>());
            var clawRoots = new Transform[3];
            for (var index = 0; index < clawRoots.Length; index++)
            {
                clawRoots[index] = new GameObject($"Claw{index}").transform;
                clawRoots[index].SetParent(gripObject.transform, false);
                collector.ReportContact(index, payload, payload.transform.position, true);
            }

            hook.Configure(mount, clawRoots, collector, hookObject.transform);
            Physics.SyncTransforms();

            Assert.That(hook.CloseAndTryAttach(), Is.True);
            Assert.That(hook.IsClosed, Is.True);
            Assert.That(mount.AttachedPayload, Is.EqualTo(payload));

            hook.OpenAndRelease();
            yield return null;
            Assert.That(hook.IsClosed, Is.False);
            Assert.That(mount.HasPayload, Is.False);
            Assert.That(gripObject.GetComponent<ConfigurableJoint>(), Is.Null);

            Object.Destroy(carrier);
            Object.Destroy(payloadObject);
        }

        [UnityTest]
        public IEnumerator GrappleCollector_ReceivesCompoundChildColliderContactsOnRigidbodyRoot()
        {
            var grappleObject = new GameObject("CompoundGrapple");
            var grappleBody = grappleObject.AddComponent<Rigidbody>();
            grappleBody.useGravity = false;
            grappleBody.constraints = RigidbodyConstraints.FreezeAll;
            var collector = grappleObject.AddComponent<DroneGrappleContactCollector>();
            var clawGroups = new Collider[3][];
            var positions = new[]
            {
                new Vector3(0.48f, 0f, 0f),
                new Vector3(-0.48f, 0f, 0f),
                new Vector3(0f, 0f, 0.48f)
            };
            for (var index = 0; index < positions.Length; index++)
            {
                var clawObject = new GameObject($"Claw_{index + 1}");
                clawObject.transform.SetParent(grappleObject.transform, false);
                clawObject.transform.localPosition = positions[index];
                var clawCollider = clawObject.AddComponent<BoxCollider>();
                clawCollider.size = new Vector3(0.1f, 0.4f, 0.4f);
                clawGroups[index] = new Collider[] { clawCollider };
            }

            collector.Configure(clawGroups);
            var payloadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            payloadObject.name = "CompoundContactPayload";
            var payloadBody = payloadObject.AddComponent<Rigidbody>();
            payloadBody.useGravity = false;
            payloadBody.isKinematic = true;
            var payload = payloadObject.AddComponent<DronePayload>();
            payload.Configure("Cargo");
            Physics.SyncTransforms();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(collector.TryGetBestSnapshot(Vector3.zero, 2f, out var snapshot), Is.True,
                "复合 Collider 的碰撞消息必须抵达抓斗 Rigidbody 根上的统一收集器。");
            Assert.That(snapshot.Payload, Is.EqualTo(payload));
            Assert.That(snapshot.DistinctClawCount, Is.EqualTo(3));

            Object.Destroy(grappleObject);
            Object.Destroy(payloadObject);
        }

        [Test]
        public void Overload_IsRejectedWithoutCreatingJoint()
        {
            var carrier = new GameObject("Carrier");
            carrier.AddComponent<Rigidbody>();
            var mount = carrier.AddComponent<PayloadMount>();
            mount.Configure(carrier.transform, 0.5f);
            var payloadObject = new GameObject("HeavyPayload");
            payloadObject.AddComponent<BoxCollider>();
            payloadObject.AddComponent<Rigidbody>().mass = 1f;
            var payload = payloadObject.AddComponent<DronePayload>();

            LogAssert.Expect(
                LogType.Warning,
                "[DroneFlight] 载荷 HeavyPayload 质量 1.00 kg 超过上限 0.50 kg。");
            Assert.That(mount.TryAttach(payload), Is.False);
            Assert.That(mount.LastReleaseReason, Is.EqualTo(PayloadReleaseReason.Overload));
            Assert.That(carrier.GetComponent<ConfigurableJoint>(), Is.Null);

            Object.DestroyImmediate(carrier);
            Object.DestroyImmediate(payloadObject);
        }

        [Test]
        public void TwoDistinctClawContacts_AreRejectedWithoutCreatingWeakJoint()
        {
            var carrier = new GameObject("Carrier");
            var body = carrier.AddComponent<Rigidbody>();
            var mount = carrier.AddComponent<PayloadMount>();
            mount.Configure(carrier.transform, 0.5f, body);
            var payloadObject = new GameObject("Payload");
            payloadObject.AddComponent<BoxCollider>();
            payloadObject.AddComponent<Rigidbody>().mass = 0.2f;
            var payload = payloadObject.AddComponent<DronePayload>();

            Assert.That(mount.TryAssistGrip(payload, 2), Is.False);
            Assert.That(mount.HasPayload, Is.False);
            Assert.That(carrier.GetComponent<ConfigurableJoint>(), Is.Null);

            Object.DestroyImmediate(carrier);
            Object.DestroyImmediate(payloadObject);
        }

        [Test]
        public void ContactCollector_OneColliderExitKeepsSameClawContactFromAnotherCollider()
        {
            var collectorObject = new GameObject("Collector");
            var collector = collectorObject.AddComponent<DroneGrappleContactCollector>();
            var payloadObject = new GameObject("MultiColliderPayload");
            payloadObject.AddComponent<Rigidbody>();
            var payload = payloadObject.AddComponent<DronePayload>();
            var first = payloadObject.AddComponent<BoxCollider>();
            var secondChild = new GameObject("SecondCollider");
            secondChild.transform.SetParent(payloadObject.transform);
            var second = secondChild.AddComponent<SphereCollider>();

            collector.ReportContact(0, payload, first, Vector3.left, true);
            collector.ReportContact(0, payload, second, Vector3.right, true);
            collector.ReportContact(1, payload, first, Vector3.forward, true);
            collector.ReportContact(2, payload, first, Vector3.back, true);
            collector.ReportContact(0, payload, first, Vector3.zero, false);

            Assert.That(collector.TryGetBestSnapshot(Vector3.zero, 2f, out var snapshot), Is.True);
            Assert.That(snapshot.DistinctClawCount, Is.EqualTo(3));
            collector.ReportContact(0, payload, second, Vector3.zero, false);
            Assert.That(collector.TryGetBestSnapshot(Vector3.zero, 2f, out snapshot), Is.True);
            Assert.That(snapshot.DistinctClawCount, Is.EqualTo(2));
            Object.DestroyImmediate(collectorObject);
            Object.DestroyImmediate(payloadObject);
        }

        [Test]
        public void PayloadSnapshot_RestoresTransformVelocityAndActiveState()
        {
            var payloadObject = new GameObject("ResetPayload");
            var body = payloadObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            var payload = payloadObject.AddComponent<DronePayload>();
            payloadObject.transform.SetPositionAndRotation(new Vector3(1f, 2f, 3f), Quaternion.Euler(0f, 30f, 0f));
            var snapshot = payload.CaptureSnapshot();

            body.position = Vector3.one * 9f;
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.one;
            body.angularVelocity = Vector3.one;
            payloadObject.SetActive(false);
            snapshot.Restore();

            Assert.That(payloadObject.activeSelf, Is.True);
            Assert.That(body.position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(body.rotation.eulerAngles.y, Is.EqualTo(30f).Within(0.1f));
            Assert.That(body.linearVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(body.angularVelocity, Is.EqualTo(Vector3.zero));
            Object.DestroyImmediate(payloadObject);
        }
    }
}
