using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>飞行控制层级。</summary>
    internal enum DroneFlightMode
    {
        Rate,
        Attitude,
        Position
    }

    /// <summary>消费级无人机的响应档位。</summary>
    internal enum DroneResponseProfile
    {
        Cine,
        Normal,
        Sport
    }

    /// <summary>飞控运行状态。</summary>
    internal enum DroneFlightOperationState
    {
        Disarmed,
        ArmedIdle,
        TakingOff,
        Flying,
        Landing,
        Fault
    }

    /// <summary>
    /// 当前响应档位的不可变运行参数。
    /// </summary>
    internal readonly struct DroneResponseProfileParameters
    {
        internal DroneResponseProfileParameters(
            float maximumHorizontalSpeed,
            float maximumHorizontalAcceleration,
            float maximumTiltDegrees,
            float maximumVerticalSpeed,
            float maximumYawSpeedDegrees,
            float inputRiseRate,
            float maximumHorizontalJerk = 6f,
            float maximumVerticalAcceleration = 3f,
            float maximumVerticalJerk = 8f,
            float maximumYawAccelerationDegrees = 180f)
        {
            MaximumHorizontalSpeed = maximumHorizontalSpeed;
            MaximumHorizontalAcceleration = maximumHorizontalAcceleration;
            MaximumTiltDegrees = maximumTiltDegrees;
            MaximumVerticalSpeed = maximumVerticalSpeed;
            MaximumYawSpeedDegrees = maximumYawSpeedDegrees;
            InputRiseRate = inputRiseRate;
            MaximumHorizontalJerk = maximumHorizontalJerk;
            MaximumVerticalAcceleration = maximumVerticalAcceleration;
            MaximumVerticalJerk = maximumVerticalJerk;
            MaximumYawAccelerationDegrees = maximumYawAccelerationDegrees;
        }

        internal float MaximumHorizontalSpeed { get; }

        internal float MaximumHorizontalAcceleration { get; }

        internal float MaximumTiltDegrees { get; }

        internal float MaximumVerticalSpeed { get; }

        internal float MaximumYawSpeedDegrees { get; }

        internal float InputRiseRate { get; }

        internal float MaximumHorizontalJerk { get; }

        internal float MaximumVerticalAcceleration { get; }

        internal float MaximumVerticalJerk { get; }

        internal float MaximumYawAccelerationDegrees { get; }
    }

    /// <summary>单个控制轴当前无法继续输出的方向。</summary>
    internal enum DroneSaturationDirection
    {
        None,
        Positive,
        Negative,
        Both
    }

    /// <summary>三轴力矩和总推力的分配饱和状态。</summary>
    internal readonly struct DroneControlSaturation
    {
        internal DroneControlSaturation(
            DroneSaturationDirection thrust,
            DroneSaturationDirection pitch,
            DroneSaturationDirection yaw,
            DroneSaturationDirection roll)
        {
            Thrust = thrust;
            Pitch = pitch;
            Yaw = yaw;
            Roll = roll;
        }

        internal DroneSaturationDirection Thrust { get; }
        internal DroneSaturationDirection Pitch { get; }
        internal DroneSaturationDirection Yaw { get; }
        internal DroneSaturationDirection Roll { get; }
        internal bool IsSaturated => Thrust != DroneSaturationDirection.None
                                     || Pitch != DroneSaturationDirection.None
                                     || Yaw != DroneSaturationDirection.None
                                     || Roll != DroneSaturationDirection.None;
    }

    /// <summary>固定物理步读取的一致机体状态。</summary>
    internal readonly struct DroneStateSnapshot
    {
        internal DroneStateSnapshot(
            Vector3 position,
            Vector3 velocity,
            Vector3 acceleration,
            Quaternion rotation,
            Vector3 localAngularVelocity,
            Vector3 localAngularAcceleration)
        {
            Position = position;
            Velocity = velocity;
            Acceleration = acceleration;
            Rotation = rotation;
            LocalAngularVelocity = localAngularVelocity;
            LocalAngularAcceleration = localAngularAcceleration;
        }

        internal Vector3 Position { get; }
        internal Vector3 Velocity { get; }
        internal Vector3 Acceleration { get; }
        internal Quaternion Rotation { get; }
        internal Vector3 LocalAngularVelocity { get; }
        internal Vector3 LocalAngularAcceleration { get; }
    }

    /// <summary>轨迹生成器提供给位置和速度控制器的连续目标。</summary>
    internal readonly struct DroneTrajectorySetpoint
    {
        internal DroneTrajectorySetpoint(
            Vector3 worldVelocity,
            Vector3 worldAcceleration,
            float yawDegrees,
            float yawRateRadians,
            float yawAccelerationRadians)
        {
            WorldVelocity = worldVelocity;
            WorldAcceleration = worldAcceleration;
            YawDegrees = yawDegrees;
            YawRateRadians = yawRateRadians;
            YawAccelerationRadians = yawAccelerationRadians;
        }

        internal Vector3 WorldVelocity { get; }
        internal Vector3 WorldAcceleration { get; }
        internal float YawDegrees { get; }
        internal float YawRateRadians { get; }
        internal float YawAccelerationRadians { get; }
    }

    /// <summary>控制器希望机体实现的世界推力和局部力矩。</summary>
    internal readonly struct DroneDesiredWrench
    {
        internal DroneDesiredWrench(Vector3 worldForceNewtons, Vector3 localTorqueNewtonMeters)
        {
            WorldForceNewtons = worldForceNewtons;
            LocalTorqueNewtonMeters = localTorqueNewtonMeters;
        }

        internal Vector3 WorldForceNewtons { get; }
        internal Vector3 LocalTorqueNewtonMeters { get; }
    }

    /// <summary>
    /// Rigidbody 状态进入控制链时的只读快照。
    /// </summary>
    internal readonly struct DroneFlightState
    {
        internal DroneFlightState(
            Vector3 position,
            Vector3 worldVelocity,
            Quaternion rotation,
            Vector3 localAngularVelocity,
            float height,
            bool isGrounded)
        {
            Position = position;
            WorldVelocity = worldVelocity;
            Rotation = rotation;
            LocalAngularVelocity = localAngularVelocity;
            Height = height;
            IsGrounded = isGrounded;
        }

        internal Vector3 Position { get; }

        internal Vector3 WorldVelocity { get; }

        internal Quaternion Rotation { get; }

        internal Vector3 LocalAngularVelocity { get; }

        internal float Height { get; }

        internal bool IsGrounded { get; }
    }

    /// <summary>
    /// 各级控制环共享的目标值快照。
    /// </summary>
    internal readonly struct DroneControlSetpoint
    {
        internal DroneControlSetpoint(
            Vector3 worldPosition,
            Vector3 worldVelocity,
            Quaternion attitude,
            Vector3 localAngularVelocity,
            DroneFlightMode mode)
        {
            WorldPosition = worldPosition;
            WorldVelocity = worldVelocity;
            Attitude = attitude;
            LocalAngularVelocity = localAngularVelocity;
            Mode = mode;
        }

        internal Vector3 WorldPosition { get; }

        internal Vector3 WorldVelocity { get; }

        internal Quaternion Attitude { get; }

        internal Vector3 LocalAngularVelocity { get; }

        internal DroneFlightMode Mode { get; }
    }
}
