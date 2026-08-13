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
            float inputRiseRate)
        {
            MaximumHorizontalSpeed = maximumHorizontalSpeed;
            MaximumHorizontalAcceleration = maximumHorizontalAcceleration;
            MaximumTiltDegrees = maximumTiltDegrees;
            MaximumVerticalSpeed = maximumVerticalSpeed;
            MaximumYawSpeedDegrees = maximumYawSpeedDegrees;
            InputRiseRate = inputRiseRate;
        }

        internal float MaximumHorizontalSpeed { get; }

        internal float MaximumHorizontalAcceleration { get; }

        internal float MaximumTiltDegrees { get; }

        internal float MaximumVerticalSpeed { get; }

        internal float MaximumYawSpeedDegrees { get; }

        internal float InputRiseRate { get; }
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
