using System.Collections;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hotfix.Tests
{
    public sealed class DronePayloadMountTests
    {
        [UnityTest]
        public IEnumerator AttachAndRelease_KeepsPayloadIndependentAndLeavesNoJoint()
        {
            var carrier = new GameObject("Carrier");
            carrier.AddComponent<Rigidbody>().useGravity = false;
            var mountPoint = new GameObject("MountPoint").transform;
            mountPoint.SetParent(carrier.transform, false);
            var mount = carrier.AddComponent<PayloadMount>();
            mount.Configure(mountPoint, 0.6f);

            var payloadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            payloadObject.name = "Payload";
            var payloadBody = payloadObject.AddComponent<Rigidbody>();
            payloadBody.mass = 0.3f;
            payloadBody.useGravity = false;
            var payload = payloadObject.AddComponent<DronePayload>();
            payload.Configure("Cargo");

            for (var cycle = 0; cycle < 3; cycle++)
            {
                Assert.That(mount.TryAttach(payload), Is.True);
                Assert.That(mount.HasPayload, Is.True);
                Assert.That(payload.transform.parent, Is.Null);
                Assert.That(carrier.GetComponent<ConfigurableJoint>().connectedBody, Is.EqualTo(payloadBody));

                mount.Release();
                yield return null;
                Assert.That(mount.HasPayload, Is.False);
                Assert.That(carrier.GetComponent<ConfigurableJoint>(), Is.Null);
                Assert.That(payloadBody.isKinematic, Is.False);
            }

            Object.Destroy(carrier);
            Object.Destroy(payloadObject);
        }

        [UnityTest]
        public IEnumerator MechanicalHook_ClosesOnNearbyPayloadAndOpensToRelease()
        {
            var carrier = new GameObject("Carrier");
            carrier.AddComponent<Rigidbody>().useGravity = false;
            var mount = carrier.AddComponent<PayloadMount>();
            mount.Configure(carrier.transform, 0.6f);
            var hookObject = new GameObject("Hook");
            hookObject.transform.SetParent(carrier.transform, false);
            var leftClaw = new GameObject("LeftClaw").transform;
            leftClaw.SetParent(hookObject.transform, false);
            var rightClaw = new GameObject("RightClaw").transform;
            rightClaw.SetParent(hookObject.transform, false);
            var hook = hookObject.AddComponent<DroneMechanicalHook>();
            hook.Configure(mount, leftClaw, rightClaw);

            var payloadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            payloadObject.name = "NearbyPayload";
            payloadObject.transform.position = hookObject.transform.position + Vector3.right * 0.1f;
            var payloadBody = payloadObject.AddComponent<Rigidbody>();
            payloadBody.mass = 0.3f;
            payloadBody.useGravity = false;
            var payload = payloadObject.AddComponent<DronePayload>();
            payload.Configure("NearbyCargo");
            Physics.SyncTransforms();

            Assert.That(hook.CloseAndTryAttach(), Is.True);
            Assert.That(hook.IsClosed, Is.True);
            Assert.That(mount.AttachedPayload, Is.EqualTo(payload));

            hook.OpenAndRelease();
            yield return null;
            Assert.That(hook.IsClosed, Is.False);
            Assert.That(mount.HasPayload, Is.False);
            Assert.That(carrier.GetComponent<ConfigurableJoint>(), Is.Null);

            Object.Destroy(carrier);
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
    }
}
