using System.Collections;
using System.Linq;
using System.Reflection;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Demo
{
    /*
     * 测试说明：验证抓斗包围吸附、渔叉停靠与等量反向冲量以及只受拉绳索，确保两套装备形成可玩闭环。
     */
    public sealed class DroneEquipmentPhysicsPlayModeTests
    {
        [Test]
        public void HarpoonImpulse_IsEqualAndOppositeForProjectileAndDrone()
        {
            var impulse = DroneEquipmentPhysicsMath.CalculateHarpoonImpulse(Vector3.forward, 0.12f);
            Assert.That(impulse.magnitude, Is.EqualTo(0.12f).Within(0.0001f));
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

        [Test]
        public void HarpoonAimEnvelope_UsesHorizontalRadiusAndDownwardCone()
        {
            Assert.That(DroneEquipmentPhysicsMath.IsWithinHarpoonAimEnvelope(
                Vector3.zero, Vector3.zero, Vector3.down, new Vector3(1f, -4f, 0f), 3f, 25f), Is.True);
            Assert.That(DroneEquipmentPhysicsMath.IsWithinHarpoonAimEnvelope(
                Vector3.zero, Vector3.zero, Vector3.down, new Vector3(3.1f, -8f, 0f), 3f, 25f), Is.False);
            Assert.That(DroneEquipmentPhysicsMath.IsWithinHarpoonAimEnvelope(
                Vector3.zero, Vector3.zero, Vector3.down, new Vector3(2f, -2f, 0f), 3f, 25f), Is.False);
        }

        [UnityTest]
        public IEnumerator GrappleVariant_ClosingInsideCaptureVolumeCreatesAndReleasesFixedJoint()
        {
            const string path = "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneGrappleVariant.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Object.Instantiate(source, new Vector3(0f, 2f, 0f), Quaternion.identity);
            instance.GetComponent<Rigidbody>().isKinematic = true;
            var module = instance.GetComponentInChildren<DroneGrappleModule>(true);
            var grappleBody = module.transform.Find("GrappleBase").GetComponent<Rigidbody>();
            var captureVolume = grappleBody.transform.Find("GrappleCaptureVolume").GetComponent<BoxCollider>();
            for (var index = 0; index < 4; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            var payloadObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            payloadObject.name = "Payload";
            payloadObject.transform.localScale = Vector3.one * 0.3f;
            payloadObject.transform.position = captureVolume.bounds.center;
            var payloadBody = payloadObject.AddComponent<Rigidbody>();
            payloadBody.mass = 0.15f;
            payloadObject.AddComponent<DronePayload>();
            Physics.SyncTransforms();

            module.PrimaryAction();
            yield return new WaitForFixedUpdate();
            Assert.That(module.State, Is.EqualTo(DroneEquipmentState.Carrying));
            var grip = grappleBody.GetComponent<FixedJoint>();
            Assert.That(grip, Is.Not.Null);
            Assert.That(grip.connectedBody, Is.EqualTo(payloadBody));

            var releaseVelocity = new Vector3(0.3f, -0.2f, 0.1f);
            payloadBody.linearVelocity = releaseVelocity;
            module.PrimaryAction();
            Assert.That(module.State, Is.EqualTo(DroneEquipmentState.Ready));
            Assert.That(payloadBody.linearVelocity, Is.EqualTo(releaseVelocity));

            Object.Destroy(instance);
            Object.Destroy(payloadObject);
        }

        [UnityTest]
        public IEnumerator HarpoonVariant_StaysDockedForTwoSecondsBeforeFiring()
        {
            const string path = "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneHarpoonVariant.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Object.Instantiate(source, new Vector3(0f, 2f, 0f), Quaternion.identity);
            instance.GetComponent<Rigidbody>().isKinematic = true;
            var module = instance.GetComponentInChildren<DroneHarpoonModule>(true);
            var muzzle = module.transform.Find("HarpoonLauncher/HarpoonGimbal/Muzzle");
            var projectile = module.GetComponentInChildren<DroneHarpoonProjectile>(true);
            var projectileBody = projectile.GetComponent<Rigidbody>();
            var projectileCollider = projectile.GetComponent<Collider>();
            var rope = module.transform.Find("HarpoonRopeVisual").GetComponent<LineRenderer>();

            for (var index = 0; index < Mathf.CeilToInt(2f / Time.fixedDeltaTime); index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(module.State, Is.EqualTo(DroneEquipmentState.Stowed));
            Assert.That(Vector3.Distance(projectileBody.position, muzzle.position), Is.LessThan(0.001f));
            Assert.That(Vector3.Angle(projectileBody.transform.forward, muzzle.forward), Is.LessThan(0.1f));
            Assert.That(projectileBody.isKinematic, Is.True);
            Assert.That(projectileBody.useGravity, Is.False);
            Assert.That(projectileCollider.enabled, Is.False);
            Assert.That(rope.enabled, Is.False);
            Object.Destroy(instance);
        }

        [UnityTest]
        public IEnumerator GrappleVariant_InitialJointKeepsFixedArmAttachedAcrossPhysicsSteps()
        {
            const string path = "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneGrappleVariant.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Object.Instantiate(source, new Vector3(0f, 2f, 0f), Quaternion.identity);
            var droneBody = instance.GetComponent<Rigidbody>();
            droneBody.isKinematic = true;
            var module = instance.GetComponentInChildren<DroneGrappleModule>(true);
            var baseBody = module.transform.Find("GrappleBase").GetComponent<Rigidbody>();
            var suspension = baseBody.GetComponent<ConfigurableJoint>();

            for (var index = 0; index < 4; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            var localAnchor = baseBody.transform.TransformPoint(suspension.anchor);
            var connectedAnchor = droneBody.transform.TransformPoint(suspension.connectedAnchor);
            Assert.That(module.State, Is.EqualTo(DroneEquipmentState.Ready));
            Assert.That(Vector3.Distance(localAnchor, connectedAnchor), Is.LessThan(0.01f));
            Assert.That(module.transform.Find("GrappleBase/GrappleArm").GetComponent<Rigidbody>(), Is.Null);
            Object.Destroy(instance);
        }

        [UnityTest]
        public IEnumerator GrappleVariant_KLowersAndJRetractsOnlyConnectedAnchorHeight()
        {
            const string path = "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneGrappleVariant.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Object.Instantiate(source, new Vector3(0f, 2f, 0f), Quaternion.identity);
            instance.GetComponent<Rigidbody>().isKinematic = true;
            var module = instance.GetComponentInChildren<DroneGrappleModule>(true);
            var suspension = module.transform.Find("GrappleBase").GetComponent<ConfigurableJoint>();
            yield return new WaitForFixedUpdate();
            var initialAnchor = suspension.connectedAnchor;

            module.SetLineInput(1f);
            for (var index = 0; index < 10; index++)
            {
                yield return new WaitForFixedUpdate();
            }
            module.SetLineInput(0f);
            var loweredAnchor = suspension.connectedAnchor;
            Assert.That(loweredAnchor.x, Is.EqualTo(initialAnchor.x).Within(0.0001f));
            Assert.That(loweredAnchor.z, Is.EqualTo(initialAnchor.z).Within(0.0001f));
            Assert.That(loweredAnchor.y, Is.LessThan(initialAnchor.y));
            Assert.That(module.Snapshot.TravelMeters, Is.GreaterThan(0f));
            Assert.That(suspension.angularXMotion, Is.EqualTo(ConfigurableJointMotion.Locked));
            Assert.That(suspension.angularYMotion, Is.EqualTo(ConfigurableJointMotion.Limited));
            Assert.That(suspension.angularZMotion, Is.EqualTo(ConfigurableJointMotion.Limited));

            module.SetLineInput(-1f);
            for (var index = 0; index < 20; index++)
            {
                yield return new WaitForFixedUpdate();
            }
            module.SetLineInput(0f);
            Assert.That(module.Snapshot.TravelMeters, Is.Zero.Within(0.001f));
            Object.Destroy(instance);
        }

        [UnityTest]
        public IEnumerator EmptyVariants_KeepConfiguredVehicleMassWithoutAdditionalHardwareWeight()
        {
            var paths = new[]
            {
                "Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab",
                "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneGrappleVariant.prefab",
                "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneHarpoonVariant.prefab"
            };
            foreach (var path in paths)
            {
                var instance = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(path),
                    new Vector3(0f, 3f, 0f),
                    Quaternion.identity);
                var controller = instance.GetComponent<DroneFlightController>();
                yield return new WaitForFixedUpdate();
                var integratedBodies = instance.GetComponentsInChildren<Rigidbody>(true)
                    .Where(body => body != controller.Body && !body.isKinematic)
                    .Sum(body => body.mass);
                Assert.That(controller.Body.mass + integratedBodies,
                    Is.EqualTo(controller.Config.BodyMassKilograms).Within(0.001f), path);
                Assert.That(controller.CurrentSupportedMassKilograms,
                    Is.EqualTo(controller.Config.BodyMassKilograms).Within(0.001f), path);
                Assert.That(controller.CurrentHardwareMassKilograms, Is.Zero, path);
                Object.Destroy(instance);
            }
        }

        [UnityTest]
        public IEnumerator HarpoonRecovery_DampsTangentialVelocityAndReturnsToDock()
        {
            const string path = "Assets/LoadResources/Demos/drone_flight/Prefabs/DroneHarpoonVariant.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = Object.Instantiate(source, new Vector3(0f, 3f, 0f), Quaternion.identity);
            var droneBody = instance.GetComponent<Rigidbody>();
            droneBody.isKinematic = true;
            var controller = instance.GetComponent<DroneFlightController>();
            var module = instance.GetComponentInChildren<DroneHarpoonModule>(true);
            var projectile = module.GetComponentInChildren<DroneHarpoonProjectile>(true).GetComponent<Rigidbody>();
            var muzzle = module.transform.Find("HarpoonLauncher/HarpoonGimbal/Muzzle");
            yield return new WaitForFixedUpdate();
            var beginRecovery = typeof(DroneHarpoonModule).GetMethod(
                "BeginRecovery",
                BindingFlags.Instance | BindingFlags.NonPublic);
            for (var cycle = 0; cycle < 3; cycle++)
            {
                projectile.isKinematic = false;
                projectile.useGravity = true;
                projectile.position = muzzle.position + Vector3.right * (1.5f + cycle * 0.25f);
                projectile.linearVelocity = Vector3.forward * (4f + cycle);
                beginRecovery?.Invoke(module, new object[] { $"测试回收 {cycle + 1}" });

                var initialTangentialSpeed = Mathf.Abs(projectile.linearVelocity.z);
                yield return new WaitForFixedUpdate();
                Assert.That(droneBody.mass + projectile.mass,
                    Is.EqualTo(controller.Config.BodyMassKilograms).Within(0.001f),
                    "射出后的弹体质量必须从主刚体等额扣除");
                for (var index = 0; index < Mathf.CeilToInt(5f / Time.fixedDeltaTime)
                                    && module.State != DroneEquipmentState.Stowed; index++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(module.State, Is.EqualTo(DroneEquipmentState.Stowed), $"第 {cycle + 1} 次回收未完成停靠");
                Assert.That(Vector3.Distance(projectile.position, muzzle.position), Is.LessThan(0.001f));
                Assert.That(Mathf.Abs(projectile.linearVelocity.z), Is.LessThan(initialTangentialSpeed));
                Assert.That(module.transform.Find("HarpoonRopeVisual").GetComponent<LineRenderer>().enabled, Is.False);
            }
            Object.Destroy(instance);
        }
    }
}
