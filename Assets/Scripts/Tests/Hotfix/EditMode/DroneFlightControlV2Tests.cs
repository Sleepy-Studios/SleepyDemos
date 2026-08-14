using Hotfix.DroneFlight;
using NUnit.Framework;
using UnityEngine;

namespace Hotfix.Tests
{
    /*
     * 测试说明：验证第二版飞控的轨迹限制、姿态数学、控制分配、抗积分饱和、电机逆解和被动抗摆计算。
     */
    public sealed class DroneFlightControlV2Tests
    {
        private static readonly DroneResponseProfileParameters Sport = new(
            maximumHorizontalSpeed: 12f,
            maximumHorizontalAcceleration: 7f,
            maximumTiltDegrees: 35f,
            maximumVerticalSpeed: 5f,
            maximumYawSpeedDegrees: 160f,
            inputRiseRate: 10f,
            maximumHorizontalJerk: 15f,
            maximumVerticalAcceleration: 5f,
            maximumVerticalJerk: 15f,
            maximumYawAccelerationDegrees: 360f);

        [Test]
        public void Trajectory_YawHistoryDoesNotDelayForwardAxis()
        {
            var generator = new DroneTrajectoryGenerator();
            generator.Reset(Vector3.zero, 0f);
            for (var index = 0; index < 500; index++)
            {
                generator.Step(DroneControlInput.Create(0f, 1f, 0f, 0f), 0f, Sport, 0.02f);
            }

            var result = generator.Step(DroneControlInput.Create(0f, 0f, 1f, 0f), 0f, Sport, 0.02f);

            Assert.That(result.WorldVelocity.z, Is.GreaterThan(0f));
            Assert.That(result.WorldAcceleration.z, Is.GreaterThan(0f));
        }

        [Test]
        public void Trajectory_AccelerationAndJerkRespectProfileLimits()
        {
            var generator = new DroneTrajectoryGenerator();
            generator.Reset(Vector3.zero, 0f);
            var previousAcceleration = Vector3.zero;
            const float deltaTime = 0.02f;
            for (var index = 0; index < 100; index++)
            {
                var result = generator.Step(DroneControlInput.Create(1f, 1f, 1f, 1f), 0f, Sport, deltaTime);
                var horizontalAcceleration = Vector3.ProjectOnPlane(result.WorldAcceleration, Vector3.up);
                var horizontalJerk = Vector3.ProjectOnPlane(
                    result.WorldAcceleration - previousAcceleration,
                    Vector3.up).magnitude / deltaTime;
                Assert.That(horizontalAcceleration.magnitude,
                    Is.LessThanOrEqualTo(Sport.MaximumHorizontalAcceleration + 0.001f));
                Assert.That(horizontalJerk, Is.LessThanOrEqualTo(Sport.MaximumHorizontalJerk + 0.01f));
                Assert.That(Mathf.Abs(result.WorldAcceleration.y),
                    Is.LessThanOrEqualTo(Sport.MaximumVerticalAcceleration + 0.001f));
                previousAcceleration = result.WorldAcceleration;
            }
        }

        [Test]
        public void ForceTiltLimit_ClampsHorizontalComponentWithoutChangingVerticalForce()
        {
            var limited = DronePhysicalControlMath.LimitForceByTilt(new Vector3(100f, 10f, 40f), 30f);

            Assert.That(limited.y, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(Vector3.Angle(Vector3.up, limited), Is.LessThanOrEqualTo(30.001f));
        }

        [Test]
        public void ReducedAttitude_YawErrorDoesNotInjectRollOrPitch()
        {
            var rate = DronePhysicalControlMath.CalculateReducedAttitudeRate(
                Quaternion.identity,
                Vector3.up,
                170f,
                4f,
                3f,
                0.5f,
                5f);

            Assert.That(rate.x, Is.Zero.Within(0.0001f));
            Assert.That(rate.z, Is.Zero.Within(0.0001f));
            Assert.That(rate.y, Is.GreaterThan(0f));
        }

        [Test]
        public void InertiaTorque_IncludesGyroscopicCouplingAndStaysFinite()
        {
            var torque = DronePhysicalControlMath.CalculateLocalTorque(
                Vector3.zero,
                new Vector3(1f, 2f, 0f),
                new Vector3(1f, 2f, 3f),
                Quaternion.identity);

            Assert.That(torque.x, Is.Zero.Within(0.0001f));
            Assert.That(torque.y, Is.Zero.Within(0.0001f));
            Assert.That(torque.z, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(float.IsFinite(torque.sqrMagnitude), Is.True);
        }

        [Test]
        public void Allocator_UnconstrainedRequestRebuildsPhysicalWrench()
        {
            var allocator = CreateAllocator(maximumRotorThrust: 20f);

            var result = allocator.Allocate(24f, new Vector3(0.4f, 0.08f, -0.3f));

            Assert.That(allocator.IsValid, Is.True);
            Assert.That(result.RealizedThrustNewtons, Is.EqualTo(24f).Within(0.001f));
            Assert.That(result.RealizedTorqueNewtonMeters.x, Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(result.RealizedTorqueNewtonMeters.y, Is.EqualTo(0.08f).Within(0.001f));
            Assert.That(result.RealizedTorqueNewtonMeters.z, Is.EqualTo(-0.3f).Within(0.001f));
            Assert.That(result.RealizedForceBodyNewtons.x, Is.Zero.Within(0.001f));
            Assert.That(result.RealizedForceBodyNewtons.y, Is.EqualTo(24f).Within(0.001f));
            Assert.That(result.RealizedForceBodyNewtons.z, Is.Zero.Within(0.001f));
            Assert.That(result.ResidualThrustNewtons, Is.Zero.Within(0.001f));
            Assert.That(result.ResidualTorqueNewtonMeters.magnitude, Is.Zero.Within(0.001f));
            Assert.That(result.Saturation.IsSaturated, Is.False);
        }

        [Test]
        public void Allocator_SaturationReducesYawBeforeRollPitch()
        {
            var allocator = CreateAllocator(maximumRotorThrust: 8f);

            var result = allocator.Allocate(24f, new Vector3(0.4f, 8f, -0.3f));

            Assert.That(result.YawScale, Is.LessThan(1f));
            Assert.That(result.RollPitchScale, Is.EqualTo(1f).Within(0.001f));
            Assert.That(result.RealizedTorqueNewtonMeters.x, Is.EqualTo(0.4f).Within(0.01f));
            Assert.That(result.RealizedTorqueNewtonMeters.z, Is.EqualTo(-0.3f).Within(0.01f));
        }

        [Test]
        public void DirectionalAntiWindup_LeavesUnblockedDirectionIntegralUntouched()
        {
            var controller = new DronePidController(new DronePidSettings(0f, 1f, 0f, 10f, 10f, 0f));
            controller.StepWithMeasurement(1f, 0f, 0f, 0.5f);

            controller.ApplyDirectionalSaturation(DroneSaturationDirection.Negative);

            Assert.That(controller.Telemetry.IntegralState, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void MotorThrustInverse_ReconstructsRequestedThrust()
        {
            var motor = new DroneMotorModel(new DroneMotorSettings(0f, 10000f, 0.0000001f, 0.02f));
            var command = motor.CommandForThrust(4f);
            var state = motor.Step(command, 0.02f);

            Assert.That(state.ThrustNewtons, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void AntiSwing_IsCappedAndSportUsesHalfStrength()
        {
            var suspension = new DroneSuspensionState(
                true,
                0.5f,
                1f,
                new Vector3(0.05f, -0.9987f, 0f).normalized,
                new Vector3(0.05f, 0f, 0f));

            var normal = DroneSuspendedLoadAssist.CalculateCorrection(suspension, 35f, 1f, 4f, false);
            var sport = DroneSuspendedLoadAssist.CalculateCorrection(suspension, 35f, 1f, 4f, true);
            var stowed = DroneSuspendedLoadAssist.CalculateCorrection(default, 35f, 1f, 4f, false);

            Assert.That(normal.magnitude, Is.LessThanOrEqualTo(1.001f));
            Assert.That(sport.magnitude, Is.EqualTo(normal.magnitude * 0.5f).Within(0.001f));
            Assert.That(stowed, Is.EqualTo(Vector3.zero));
        }

        private static QuadrotorControlAllocator CreateAllocator(float maximumRotorThrust)
        {
            return new QuadrotorControlAllocator(
                new[]
                {
                    new Vector3(-0.25f, 0f, 0.25f),
                    new Vector3(0.25f, 0f, 0.25f),
                    new Vector3(-0.25f, 0f, -0.25f),
                    new Vector3(0.25f, 0f, -0.25f)
                },
                new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up },
                new[]
                {
                    DroneRotorDirection.CounterClockwise,
                    DroneRotorDirection.Clockwise,
                    DroneRotorDirection.Clockwise,
                    DroneRotorDirection.CounterClockwise
                },
                0.02f,
                maximumRotorThrust);
        }
    }
}
