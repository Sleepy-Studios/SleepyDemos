using System.Collections;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hotfix.Tests
{
    public sealed class DroneRotorPhysicsTests
    {
        [UnityTest]
        public IEnumerator Disarmed_DoesNotProduceRotorThrust()
        {
            var fixture = CreateFixture(armOnStart: false);
            var initialY = fixture.Body.position.y;

            for (var index = 0; index < 25; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Controller.IsArmed, Is.False);
            Assert.That(fixture.Body.position.y, Is.LessThan(initialY - 0.5f));
            Assert.That(fixture.Controller.LastMotorOutput.FrontLeft, Is.Zero);
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator ArmedHover_UsesFiniteSymmetricRotorForcesWithoutFlipping()
        {
            var fixture = CreateFixture(armOnStart: true);

            // 先在无重力条件下完成电机 spool-up，避免把阶段 3 的垂直速度恢复能力混入本测试。
            for (var index = 0; index < 25; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Body.useGravity = true;
            fixture.Body.linearVelocity = Vector3.zero;
            var initialY = fixture.Body.position.y;

            for (var index = 0; index < 100; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            var rotationError = Quaternion.Angle(Quaternion.identity, fixture.Body.rotation);
            Assert.That(fixture.Controller.IsArmed, Is.True);
            Assert.That(IsFinite(fixture.Body.position), Is.True);
            Assert.That(IsFinite(fixture.Body.linearVelocity), Is.True);
            Assert.That(IsFinite(fixture.Body.angularVelocity), Is.True);
            Assert.That(fixture.Controller.LastMotorOutput.FrontLeft, Is.GreaterThan(0.5f));
            var bodyWeight = fixture.Body.mass * -Physics.gravity.y;
            Assert.That(
                fixture.Controller.LastTotalThrustNewtons,
                Is.InRange(bodyWeight * 0.9f, bodyWeight * 1.1f));
            Assert.That(Mathf.Abs(fixture.Body.position.y - initialY), Is.LessThan(1f));
            Assert.That(rotationError, Is.LessThan(2f));
            Assert.That(fixture.Controller.LastMotorOutput.FrontLeft, Is.InRange(0f, 1f));
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator AltitudeHold_StableWindowMeetsNinetyFivePercentThreshold()
        {
            var fixture = CreateFixture(armOnStart: true);
            yield return SpoolUpAndEnableGravity(fixture);
            var targetHeight = fixture.Body.position.y;
            fixture.Controller.SetTargetHeight(targetHeight);

            // 先给控制器 2 秒消除 spool-up 与重力接管瞬态。
            for (var index = 0; index < 100; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            var acceptedSamples = 0;
            const int sampleCount = 500;
            for (var index = 0; index < sampleCount; index++)
            {
                yield return new WaitForFixedUpdate();
                var heightAccepted = Mathf.Abs(fixture.Body.position.y - targetHeight) <= 0.2f;
                var tiltAccepted = Vector3.Angle(fixture.Body.transform.up, Vector3.up) <= 3f;
                if (heightAccepted && tiltAccepted)
                {
                    acceptedSamples++;
                }
            }

            Assert.That(acceptedSamples, Is.GreaterThanOrEqualTo(Mathf.CeilToInt(sampleCount * 0.95f)));
            Assert.That(IsFinite(fixture.Body.position), Is.True);
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator DisturbanceAndCenterPayload_RecoverWithinDocumentedWindows()
        {
            var fixture = CreateFixture(armOnStart: true);
            yield return SpoolUpAndEnableGravity(fixture);
            var targetHeight = fixture.Body.position.y;
            fixture.Controller.SetTargetHeight(targetHeight);

            for (var index = 0; index < 150; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Body.AddForce(Vector3.right * 2f, ForceMode.Impulse);
            var impulsePosition = fixture.Body.position;
            for (var index = 0; index < 300; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            var horizontalDisplacement = Vector2.Distance(
                new Vector2(fixture.Body.position.x, fixture.Body.position.z),
                new Vector2(impulsePosition.x, impulsePosition.z));
            var horizontalSpeed = new Vector2(fixture.Body.linearVelocity.x, fixture.Body.linearVelocity.z).magnitude;
            Assert.That(
                Vector3.Angle(fixture.Body.transform.up, Vector3.up),
                Is.LessThanOrEqualTo(5f),
                $"水平位移={horizontalDisplacement:F3}m，水平速度={horizontalSpeed:F3}m/s，高度误差={Mathf.Abs(fixture.Body.position.y - targetHeight):F3}m");
            Assert.That(
                horizontalDisplacement,
                Is.LessThanOrEqualTo(0.75f));
            Assert.That(Mathf.Abs(fixture.Body.position.y - targetHeight), Is.LessThanOrEqualTo(0.25f));

            var originalMass = fixture.Body.mass;
            fixture.Body.mass = originalMass * 1.2f;
            for (var index = 0; index < 400; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(Mathf.Abs(fixture.Body.position.y - targetHeight), Is.LessThanOrEqualTo(0.3f));

            fixture.Body.mass = originalMass;
            for (var index = 0; index < 400; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(Mathf.Abs(fixture.Body.position.y - targetHeight), Is.LessThanOrEqualTo(0.3f));
            Assert.That(IsFinite(fixture.Body.linearVelocity), Is.True);
            Assert.That(fixture.Controller.LastMotorOutput.FrontLeft, Is.InRange(0f, 1f));
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator AutomaticTakeoffAndLanding_TwoCyclesReachHeightAndDisarmOnGround()
        {
            var fixture = CreateFixture(armOnStart: false);
            fixture.Body.position = new Vector3(0f, 0.06f, 0f);
            var ground = new GameObject("AutomaticFlightGround");
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            var groundCollider = ground.AddComponent<BoxCollider>();
            groundCollider.size = new Vector3(20f, 0.1f, 20f);

            for (var cycle = 0; cycle < 2; cycle++)
            {
                for (var index = 0; index < 25; index++)
                {
                    yield return new WaitForFixedUpdate();
                }

                fixture.Controller.BeginAutomaticTakeoff();
                for (var index = 0; index < 450; index++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(fixture.Controller.OperationState, Is.EqualTo(DroneFlightOperationState.Flying));
                Assert.That(fixture.Body.position.y, Is.EqualTo(1.5f).Within(0.2f));

                fixture.Controller.BeginAutomaticLanding();
                for (var index = 0; index < 500 && fixture.Controller.IsArmed; index++)
                {
                    yield return new WaitForFixedUpdate();
                }

                Assert.That(fixture.Controller.IsArmed, Is.False);
                Assert.That(fixture.Controller.OperationState, Is.EqualTo(DroneFlightOperationState.Disarmed));
                Assert.That(fixture.Body.position.y, Is.LessThan(0.2f));
            }

            Object.Destroy(ground);
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator UnsafeTilt_EntersFaultAndStopsMotors()
        {
            var fixture = CreateFixture(armOnStart: true);
            fixture.Body.useGravity = false;
            fixture.Body.rotation = Quaternion.Euler(100f, 0f, 0f);
            fixture.Body.constraints = RigidbodyConstraints.FreezeRotation;

            for (var index = 0; index < 35; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Controller.OperationState, Is.EqualTo(DroneFlightOperationState.Fault));
            Assert.That(fixture.Controller.IsArmed, Is.False);
            Assert.That(fixture.Controller.LastTotalThrustNewtons, Is.Zero);
            fixture.Dispose();
        }

        private static DroneFixture CreateFixture(bool armOnStart)
        {
            var config = ScriptableObject.CreateInstance<DroneFlightConfig>();
            var root = new GameObject("DronePhysicsFixture");
            root.SetActive(false);
            root.transform.position = new Vector3(0f, 3f, 0f);

            var body = root.AddComponent<Rigidbody>();
            body.mass = 1.2f;
            body.useGravity = !armOnStart;
            body.linearDamping = 0f;
            body.angularDamping = 0.05f;
            var bodyCollider = root.AddComponent<BoxCollider>();
            bodyCollider.size = new Vector3(0.34f, 0.1f, 0.26f);

            CreateRotor(root.transform, DroneRotorPosition.FrontLeft, DroneRotorDirection.CounterClockwise, -0.25f, 0.25f);
            CreateRotor(root.transform, DroneRotorPosition.FrontRight, DroneRotorDirection.Clockwise, 0.25f, 0.25f);
            CreateRotor(root.transform, DroneRotorPosition.RearLeft, DroneRotorDirection.Clockwise, -0.25f, -0.25f);
            CreateRotor(root.transform, DroneRotorPosition.RearRight, DroneRotorDirection.CounterClockwise, 0.25f, -0.25f);

            var controller = root.AddComponent<DroneFlightController>();
            controller.Configure(config, armOnStart);
            root.SetActive(true);
            return new DroneFixture(root, body, controller, config);
        }

        private static IEnumerator SpoolUpAndEnableGravity(DroneFixture fixture)
        {
            for (var index = 0; index < 25; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Body.useGravity = true;
            fixture.Body.linearVelocity = Vector3.zero;
        }

        private static void CreateRotor(
            Transform parent,
            DroneRotorPosition position,
            DroneRotorDirection direction,
            float x,
            float z)
        {
            var rotorObject = new GameObject(position.ToString());
            rotorObject.transform.SetParent(parent, false);
            rotorObject.transform.localPosition = new Vector3(x, 0f, z);
            rotorObject.AddComponent<DroneRotor>().Configure(position, direction);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private readonly struct DroneFixture
        {
            internal DroneFixture(
                GameObject root,
                Rigidbody body,
                DroneFlightController controller,
                DroneFlightConfig config)
            {
                Root = root;
                Body = body;
                Controller = controller;
                Config = config;
            }

            internal GameObject Root { get; }

            internal Rigidbody Body { get; }

            internal DroneFlightController Controller { get; }

            internal DroneFlightConfig Config { get; }

            internal void Dispose()
            {
                Object.Destroy(Root);
                Object.Destroy(Config);
            }
        }
    }
}
