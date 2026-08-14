using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>旧六爪单摆遥测兼容结构；新装备通过 DroneEquipmentSnapshot 诊断。</summary>
    internal readonly struct DroneSuspensionJointTelemetry
    {
        internal DroneSuspensionJointTelemetry(
            float twistDegrees,
            float swingDegrees,
            float twistLimitDegrees,
            float swingLimitDegrees,
            float swingRateDegreesPerSecond,
            float passiveDampingTorqueNewtonMeters,
            bool isCableTaut)
        {
            TwistDegrees = twistDegrees;
            SwingDegrees = swingDegrees;
            TwistLimitDegrees = twistLimitDegrees;
            SwingLimitDegrees = swingLimitDegrees;
            SwingRateDegreesPerSecond = swingRateDegreesPerSecond;
            PassiveDampingTorqueNewtonMeters = passiveDampingTorqueNewtonMeters;
            IsCableTaut = isCableTaut;
        }

        internal float TwistDegrees { get; }
        internal float SwingDegrees { get; }
        internal float TwistLimitDegrees { get; }
        internal float SwingLimitDegrees { get; }
        internal float SwingRateDegreesPerSecond { get; }
        internal float PassiveDampingTorqueNewtonMeters { get; }
        internal bool IsCableTaut { get; }
        internal float TopTwistDegrees => TwistDegrees;
        internal float TopSwingDegrees => SwingDegrees;
        internal float BottomTwistDegrees => 0f;
        internal float BottomSwingDegrees => 0f;
        internal float TopTwistLimitDegrees => TwistLimitDegrees;
        internal float TopSwingLimitDegrees => SwingLimitDegrees;
        internal float BottomTwistLimitDegrees => 0f;
        internal float BottomSwingLimitDegrees => 0f;
    }

    /// <summary>旧 Prefab 迁移占位；内容构建器会从运行时变体移除此组件。</summary>
    public sealed class DroneSuspensionRig : MonoBehaviour
    {
        [SerializeField] private Rigidbody grappleBody;
        internal Rigidbody GrappleBody => grappleBody;
        internal bool IsPhysicsActive { get; private set; }
        internal float HardwareMassKilograms => grappleBody != null ? grappleBody.mass : 0f;
        internal float CurrentCableLengthMeters { get; private set; }
        internal bool IsCableTaut => IsPhysicsActive;
        internal DroneSuspensionJointTelemetry JointTelemetry => default;

        internal void SetPhysicsActive(bool active) => IsPhysicsActive = active;
        internal void SetDeploymentProgress(float normalizedProgress) { }
        internal void SetCableLength(float lengthMeters) => CurrentCableLengthMeters = lengthMeters;
        internal void SetTotalHardwareMass(float totalMassKilograms)
        {
            if (grappleBody != null)
            {
                grappleBody.mass = Mathf.Max(0.001f, totalMassKilograms);
            }
        }
        internal void ApplyPassiveDampingDrive(float supportedPayloadMassKilograms) { }
        internal bool CanDock(float positionToleranceMeters, float relativeSpeedToleranceMetersPerSecond) => true;
    }
}
