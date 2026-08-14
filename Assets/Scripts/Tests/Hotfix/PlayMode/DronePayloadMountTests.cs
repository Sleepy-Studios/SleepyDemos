using System.Collections;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证抓斗接触门禁、渔叉等量反向冲量以及只受拉绳索，确保装备物理不会制造额外能量。
     */
    public sealed class DroneEquipmentPhysicsPlayModeTests
    {
        [Test]
        public void HarpoonImpulse_IsEqualAndOppositeForProjectileAndDrone()
        {
            var impulse = DroneEquipmentPhysicsMath.CalculateHarpoonImpulse(Vector3.forward, 0.02f, 18f);
            Assert.That(impulse.magnitude, Is.EqualTo(0.36f).Within(0.0001f));
            Assert.That((impulse + -impulse).magnitude, Is.Zero.Within(0.0001f));
        }

        [Test]
        public void Rope_IsTensionOnlyAndClamped()
        {
            Assert.That(DroneEquipmentPhysicsMath.CalculateTension(1f, 2f, 10f, 90f, 12f, 180f), Is.Zero);
            Assert.That(DroneEquipmentPhysicsMath.CalculateTension(3f, 2f, 20f, 90f, 12f, 180f),
                Is.EqualTo(180f));
            Assert.That(DroneEquipmentPhysicsMath.CalculateRawTension(3f, 2f, 20f, 90f, 12f),
                Is.EqualTo(330f));
        }

        [UnityTest]
        public IEnumerator GrappleCollector_RequiresOpposingClawsInsideEnclosure()
        {
            var grappleObject = new GameObject("Grapple", typeof(DroneGrappleContactCollector));
            var payloadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            payloadObject.name = "Payload";
            payloadObject.transform.position = Vector3.zero;
            payloadObject.AddComponent<Rigidbody>().isKinematic = true;
            payloadObject.AddComponent<DronePayload>();
            yield return null;

            var collector = grappleObject.GetComponent<DroneGrappleContactCollector>();
            var collider = payloadObject.GetComponent<Collider>();
            collector.Report(0, collider, new Vector3(-0.1f, 0f, 0f));
            collector.Report(1, collider, new Vector3(0f, 0f, 0.1f));
            Assert.That(collector.TryGetOpposingCandidate(
                grappleObject.transform, 0.3f, 0.3f, 1,
                out _, out _, out _), Is.False);

            collector.Report(2, collider, new Vector3(0.1f, 0f, 0f));
            Assert.That(collector.TryGetOpposingCandidate(
                grappleObject.transform, 0.3f, 0.3f, 1,
                out var payload, out var centroid, out var claws), Is.True);
            Assert.That(payload, Is.EqualTo(payloadObject.GetComponent<DronePayload>()));
            Assert.That(claws, Is.EqualTo(3));
            Assert.That(centroid.x, Is.EqualTo(0f).Within(0.1f));

            Object.Destroy(grappleObject);
            Object.Destroy(payloadObject);
        }
    }
}
