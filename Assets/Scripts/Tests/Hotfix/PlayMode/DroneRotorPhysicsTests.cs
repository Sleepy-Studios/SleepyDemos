using System.Collections;
using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hotfix.Tests
{
    /*
     * 测试说明：使用可见的四旋翼夹具和正式 DronePrototype，验证电机施力、悬停、自动起降、载荷变化及高速机动稳定性。
     * 运行时可在 Scene 视图观察“机身 Cube + X 形机臂 + 四个彩色旋翼标记”，正式起飞用例则显示真实无人机模型。
     */
    public sealed class DroneRotorPhysicsTests
    {
#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator FormalDronePrototype_AutomaticTakeoffProducesRealLift()
        {
            const string prefabPath =
                "Assets/LoadResources/Demos/drone_flight/Prefabs/DronePrototype.prefab";
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(source, Is.Not.Null, "正式 DronePrototype Prefab 必须可加载。");

            var drone = Object.Instantiate(source);
            try
            {
                drone.name = "FormalDronePrototypeFlightFixture";
                drone.transform.SetPositionAndRotation(new Vector3(0f, 2f, 0f), Quaternion.identity);
                var body = drone.GetComponent<Rigidbody>();
                var controller = drone.GetComponent<DroneFlightController>();
                Assert.That(body, Is.Not.Null);
                Assert.That(controller, Is.Not.Null);
                Assert.That(controller.enabled, Is.True, "正式 Prefab 的飞控初始化失败。");

                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                var initialHeight = body.position.y;
                var maximumHeight = initialHeight;
                controller.BeginAutomaticTakeoff();
                for (var index = 0; index < 250; index++)
                {
                    yield return new WaitForFixedUpdate();
                    maximumHeight = Mathf.Max(maximumHeight, body.position.y);
                }

                Assert.That(controller.IsArmed, Is.True);
                Assert.That(controller.LastTotalThrustNewtons,
                    Is.GreaterThan(body.mass * Mathf.Abs(Physics.gravity.y) * 0.5f));
                Assert.That(maximumHeight, Is.GreaterThan(initialHeight + 0.35f),
                    $"正式无人机未实际升空：初始 {initialHeight:F3}m，最高 {maximumHeight:F3}m。");
                Assert.That(Vector3.Angle(Vector3.up, drone.transform.up), Is.LessThan(20f));
                Assert.That(float.IsFinite(body.linearVelocity.x)
                            && float.IsFinite(body.linearVelocity.y)
                            && float.IsFinite(body.linearVelocity.z), Is.True);
            }
            finally
            {
                Object.Destroy(drone);
            }
        }
#endif

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

        [UnityTest]
        public IEnumerator PlayModeAutomaticTuningUpdate_ChangesMassWithoutResetTeleportOrInvalidMotorState()
        {
            var fixture = CreateFixture(armOnStart: true);
            for (var index = 0; index < 40; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            var positionBefore = fixture.Body.position;
            var rpmCommandBefore = fixture.Controller.CurrentAverageMotorCommand;
            var normalBefore = fixture.Config.GetProfile(DroneResponseProfile.Normal);
            fixture.Config.ConfigureAutomaticPayloadTuning(10f, 1.25f, 0.9f);

            yield return new WaitForFixedUpdate();

            var normalAfter = fixture.Config.GetProfile(DroneResponseProfile.Normal);
            Assert.That(fixture.Body.mass, Is.EqualTo(12f).Within(0.001f));
            Assert.That(fixture.Controller.CurrentAverageMotorCommand, Is.GreaterThan(0f));
            Assert.That(rpmCommandBefore, Is.GreaterThan(0f));
            Assert.That(Vector3.Distance(positionBefore, fixture.Body.position), Is.LessThan(0.15f));
            Assert.That(IsFinite(fixture.Body.position), Is.True);
            Assert.That(IsFinite(fixture.Body.linearVelocity), Is.True);
            Assert.That(normalAfter.MaximumHorizontalSpeed, Is.EqualTo(normalBefore.MaximumHorizontalSpeed));
            Assert.That(normalAfter.MaximumHorizontalAcceleration, Is.EqualTo(normalBefore.MaximumHorizontalAcceleration));
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator RatedCapacity_ChangesReserveWhileMaximumMultiplierOnlyChangesGate()
        {
            var fixture = CreateFixture(armOnStart: false);
            fixture.MassProvider.HardwareMassKilograms = 0.05f;
            fixture.MassProvider.PayloadMassKilograms = 0.95f;
            fixture.MassProvider.SupportedPayloadMassKilograms = 0.95f;
            yield return new WaitForFixedUpdate();
            var oneKilogramHover = fixture.Controller.CurrentHoverCommand;
            var profileBefore = fixture.Config.GetProfile(DroneResponseProfile.Normal);

            fixture.Config.ConfigureAutomaticPayloadTuning(1f, 1.5f, 0.9f);
            yield return new WaitForFixedUpdate();
            var multiplierOnlyHover = fixture.Controller.CurrentHoverCommand;

            fixture.Config.ConfigureAutomaticPayloadTuning(10f, 1.25f, 0.9f);
            yield return new WaitForFixedUpdate();
            var tenKilogramHover = fixture.Controller.CurrentHoverCommand;
            var profileAfter = fixture.Config.GetProfile(DroneResponseProfile.Normal);

            Assert.That(oneKilogramHover, Is.GreaterThan(0.85f));
            Assert.That(multiplierOnlyHover, Is.EqualTo(oneKilogramHover).Within(0.0001f));
            Assert.That(oneKilogramHover - tenKilogramHover, Is.GreaterThan(0.15f));
            Assert.That(fixture.Controller.CurrentPayloadMassKilograms, Is.EqualTo(0.95f));
            Assert.That(fixture.Controller.CurrentHardwareMassKilograms, Is.EqualTo(0.05f));
            Assert.That(profileAfter.MaximumHorizontalSpeed, Is.EqualTo(profileBefore.MaximumHorizontalSpeed));
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator ForwardInput_AfterSustainedYawUsesCurrentBodyHeadingImmediately()
        {
            var fixture = CreateFixture(armOnStart: true);
            fixture.Controller.SetResponseProfile(DroneResponseProfile.Sport);
            fixture.Body.rotation = Quaternion.Euler(0f, 90f, 0f);
            fixture.Body.constraints = RigidbodyConstraints.FreezeRotation;
            fixture.Controller.SetControlInput(DroneControlInput.Create(0f, 1f, 0f, 0f));
            for (var index = 0; index < 500; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Controller.SetControlInput(DroneControlInput.Create(0f, -1f, 0f, 0f));
            for (var index = 0; index < 50; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Controller.SetControlInput(DroneControlInput.Create(0f, 0f, 1f, 0f));
            yield return new WaitForFixedUpdate();

            Assert.That(fixture.Controller.LastDesiredWorldVelocity.x, Is.GreaterThan(0f),
                "Jerk 限制只允许目标速度在首个 FixedUpdate 出现小幅正值，但不得等待偏航目标追平。");
            Assert.That(fixture.Controller.LastDesiredWorldVelocity.x, Is.LessThanOrEqualTo(7f));
            Assert.That(Mathf.Abs(fixture.Controller.LastDesiredWorldVelocity.z), Is.LessThan(0.01f));
            Assert.That(fixture.Controller.LastDesiredWorldAcceleration.x, Is.GreaterThan(0f));
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator DeployedEmptyGrappleMass_HoverFeedForwardMatchesPhysicalWeightAndRecoversHeight()
        {
            var fixture = CreateFixture(armOnStart: true);
            var hardwareObject = new GameObject("DeployedGrappleMass");
            hardwareObject.transform.position = fixture.Body.position + Vector3.down * 0.5f;
            var hardwareBody = hardwareObject.AddComponent<Rigidbody>();
            hardwareBody.mass = 0.05f;
            hardwareBody.useGravity = false;
            hardwareBody.interpolation = RigidbodyInterpolation.Interpolate;
            var joint = hardwareObject.AddComponent<ConfigurableJoint>();
            joint.connectedBody = fixture.Body;
            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            fixture.MassProvider.HardwareMassKilograms = hardwareBody.mass;

            for (var index = 0; index < 50; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Body.useGravity = true;
            hardwareBody.useGravity = true;
            fixture.Body.linearVelocity = Vector3.zero;
            hardwareBody.linearVelocity = Vector3.zero;
            var targetHeight = fixture.Body.position.y;
            fixture.Controller.SetTargetHeight(targetHeight);
            for (var index = 0; index < 500; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            var expectedWeight = (fixture.Body.mass + hardwareBody.mass) * -Physics.gravity.y;
            Assert.That(fixture.Controller.CurrentSupportedMassKilograms,
                Is.EqualTo(fixture.Body.mass + hardwareBody.mass).Within(0.001f));
            Assert.That(fixture.Controller.LastTotalThrustNewtons,
                Is.EqualTo(expectedWeight).Within(expectedWeight * 0.08f));
            Assert.That(fixture.Body.position.y, Is.EqualTo(targetHeight).Within(0.3f));

            Object.Destroy(hardwareObject);
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator ReleasingPayload_UsesResidualMotorRpmForNaturalBriefClimbAndRecoversHeight()
        {
            var fixture = CreateFixture(armOnStart: true);
            fixture.Body.constraints = RigidbodyConstraints.FreezeAll;
            fixture.MassProvider.PayloadMassKilograms = 0.75f;
            fixture.MassProvider.SupportedPayloadMassKilograms = 0.75f;
            var payloadObject = new GameObject("ReleaseResponsePayload");
            payloadObject.transform.position = fixture.Body.position + Vector3.down * 0.4f;
            var payloadBody = payloadObject.AddComponent<Rigidbody>();
            payloadBody.mass = 0.75f;
            payloadBody.useGravity = false;
            payloadBody.constraints = RigidbodyConstraints.FreezeAll;
            var payloadJoint = payloadObject.AddComponent<FixedJoint>();
            payloadJoint.connectedBody = fixture.Body;

            for (var index = 0; index < 100; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Body.constraints = RigidbodyConstraints.FreezeRotation;
            payloadBody.constraints = RigidbodyConstraints.FreezeRotation;
            fixture.Body.useGravity = true;
            payloadBody.useGravity = true;
            fixture.Body.linearVelocity = Vector3.zero;
            payloadBody.linearVelocity = Vector3.zero;
            var targetHeight = fixture.Body.position.y;
            fixture.Controller.SetTargetHeight(targetHeight);
            for (var index = 0; index < 250; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Object.Destroy(payloadJoint);
            yield return null;
            fixture.MassProvider.PayloadMassKilograms = 0f;
            fixture.MassProvider.SupportedPayloadMassKilograms = 0f;
            var maximumUpwardSpeed = 0f;
            for (var index = 0; index < 100; index++)
            {
                yield return new WaitForFixedUpdate();
                maximumUpwardSpeed = Mathf.Max(maximumUpwardSpeed, fixture.Body.linearVelocity.y);
            }

            Assert.That(maximumUpwardSpeed, Is.GreaterThan(0.03f),
                "释放时不得清零电机 RPM；残余转速应自然产生可见但短暂的上窜。");
            for (var index = 0; index < 250; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(fixture.Body.position.y, Is.EqualTo(targetHeight).Within(0.3f));
            Assert.That(IsFinite(fixture.Body.linearVelocity), Is.True);
            Object.Destroy(payloadObject);
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator HighSpeedYawThenForward_DoesNotEnterRepeatedLateralBankOscillation()
        {
            var fixture = CreateFixture(armOnStart: true);
            yield return SpoolUpAndEnableGravity(fixture);
            fixture.Controller.SetResponseProfile(DroneResponseProfile.Sport);
            for (var index = 0; index < 100; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Controller.SetControlInput(DroneControlInput.Create(0f, 0f, 1f, 0f));
            for (var index = 0; index < 250; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Controller.SetControlInput(DroneControlInput.Create(0f, 1f, 0f, 0f));
            for (var index = 0; index < 100; index++)
            {
                yield return new WaitForFixedUpdate();
            }

            fixture.Controller.SetControlInput(DroneControlInput.Create(0f, 0f, 1f, 0f));
            var signChanges = 0;
            var previousSign = 0;
            var maximumBankDegrees = 0f;
            for (var index = 0; index < 400; index++)
            {
                yield return new WaitForFixedUpdate();
                var bankDegrees = Vector3.SignedAngle(Vector3.up, fixture.Body.transform.up, fixture.Body.transform.forward);
                maximumBankDegrees = Mathf.Max(maximumBankDegrees, Mathf.Abs(bankDegrees));
                if (index < 50 || Mathf.Abs(bankDegrees) < 2f)
                {
                    continue;
                }

                var sign = bankDegrees > 0f ? 1 : -1;
                if (previousSign != 0 && sign != previousSign)
                {
                    signChanges++;
                }

                previousSign = sign;
            }

            Assert.That(signChanges, Is.LessThanOrEqualTo(2),
                $"高速转向后横滚方向反复切换 {signChanges} 次，最大侧倾 {maximumBankDegrees:F1}°。");
            Assert.That(maximumBankDegrees, Is.LessThan(45f));
            Assert.That(IsFinite(fixture.Body.linearVelocity), Is.True);
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

            CreateVisibleDroneFixture(root.transform);

            CreateRotor(root.transform, DroneRotorPosition.FrontLeft, DroneRotorDirection.CounterClockwise, -0.25f, 0.25f);
            CreateRotor(root.transform, DroneRotorPosition.FrontRight, DroneRotorDirection.Clockwise, 0.25f, 0.25f);
            CreateRotor(root.transform, DroneRotorPosition.RearLeft, DroneRotorDirection.Clockwise, -0.25f, -0.25f);
            CreateRotor(root.transform, DroneRotorPosition.RearRight, DroneRotorDirection.CounterClockwise, 0.25f, -0.25f);

            var controller = root.AddComponent<DroneFlightController>();
            controller.Configure(config, armOnStart);
            var massProvider = root.AddComponent<DroneTestExternalMassProvider>();
            controller.ConfigureExternalMassProvider(massProvider);
            root.SetActive(true);
            return new DroneFixture(root, body, controller, config, massProvider);
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
            CreateVisualPrimitive(
                rotorObject.transform,
                $"{position}_RotorMarker",
                PrimitiveType.Cylinder,
                Vector3.zero,
                new Vector3(0.065f, 0.008f, 0.065f),
                direction == DroneRotorDirection.CounterClockwise
                    ? new Color(1f, 0.35f, 0.08f)
                    : new Color(0.15f, 0.65f, 1f));
        }

        private static void CreateVisibleDroneFixture(Transform root)
        {
            CreateVisualPrimitive(root, "BodyVisual_机身", PrimitiveType.Cube, Vector3.zero,
                new Vector3(0.34f, 0.10f, 0.26f), new Color(0.22f, 0.24f, 0.27f));
            CreateVisualPrimitive(root, "ArmVisual_FL_RR_机臂", PrimitiveType.Cube,
                new Vector3(0f, 0.015f, 0f), new Vector3(0.035f, 0.035f, 0.68f),
                new Color(0.06f, 0.07f, 0.08f), Quaternion.Euler(0f, 45f, 0f));
            CreateVisualPrimitive(root, "ArmVisual_FR_RL_机臂", PrimitiveType.Cube,
                new Vector3(0f, 0.015f, 0f), new Vector3(0.035f, 0.035f, 0.68f),
                new Color(0.06f, 0.07f, 0.08f), Quaternion.Euler(0f, -45f, 0f));
        }

        private static void CreateVisualPrimitive(
            Transform parent,
            string name,
            PrimitiveType primitiveType,
            Vector3 localPosition,
            Vector3 localScale,
            Color color,
            Quaternion? localRotation = null)
        {
            var visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.SetLocalPositionAndRotation(localPosition, localRotation ?? Quaternion.identity);
            visual.transform.localScale = localScale;
            var collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            var renderer = visual.GetComponent<Renderer>();
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_BaseColor", color);
            properties.SetColor("_Color", color);
            renderer.SetPropertyBlock(properties);
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
                DroneFlightConfig config,
                DroneTestExternalMassProvider massProvider)
            {
                Root = root;
                Body = body;
                Controller = controller;
                Config = config;
                MassProvider = massProvider;
            }

            internal GameObject Root { get; }

            internal Rigidbody Body { get; }

            internal DroneFlightController Controller { get; }

            internal DroneFlightConfig Config { get; }

            internal DroneTestExternalMassProvider MassProvider { get; }

            internal void Dispose()
            {
                Object.Destroy(Root);
                Object.Destroy(Config);
            }
        }
    }

    internal sealed class DroneTestExternalMassProvider : MonoBehaviour, IDroneExternalMassProvider
    {
        public float HardwareMassKilograms { get; set; }

        public float PayloadMassKilograms { get; set; }

        public float SupportedPayloadMassKilograms { get; set; }

        public float SupportedMassKilograms => HardwareMassKilograms + SupportedPayloadMassKilograms;
        public float InstalledHardwareMassKilograms => HardwareMassKilograms;
    }
}
