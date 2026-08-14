using UnityEngine;

namespace Hotfix.DroneFlight
{
    internal enum DroneWinchState { Stowed, Deploying, Deployed, Retracting, Carrying }
    internal enum DronePayloadSupportState { None, GroundSupported, TakingLoad, AirborneSupported, Unloading }

    /// <summary>旧六爪卷扬编译兼容壳；新变体不挂载此组件。</summary>
    public sealed class DroneWinchController : MonoBehaviour, IDroneExternalMassProvider,
        IDroneSuspensionStateProvider, IDroneExternalMassSynchronizer
    {
        internal DroneWinchState State { get; private set; } = DroneWinchState.Stowed;
        internal float CurrentLengthMeters { get; private set; }
        internal DronePayloadSupportState PayloadSupportState => DronePayloadSupportState.None;
        internal float PayloadUpwardSupportForceNewtons => 0f;
        internal float PayloadGripVerticalForceNewtons => 0f;
        internal float PayloadSupportedFraction => 0f;
        internal DroneSuspensionJointTelemetry JointTelemetry => default;
        internal float InstalledHardwareMassKilograms => 0f;
        internal float HardwareMassKilograms => 0f;
        internal float PayloadMassKilograms => 0f;
        internal float SupportedPayloadMassKilograms => 0f;
        internal float SupportedMassKilograms => 0f;

        float IDroneExternalMassProvider.SupportedMassKilograms => 0f;
        float IDroneExternalMassProvider.InstalledHardwareMassKilograms => 0f;
        float IDroneExternalMassProvider.HardwareMassKilograms => 0f;
        float IDroneExternalMassProvider.PayloadMassKilograms => 0f;
        float IDroneExternalMassProvider.SupportedPayloadMassKilograms => 0f;
        DroneSuspensionState IDroneSuspensionStateProvider.SuspensionState => default;
        void IDroneExternalMassSynchronizer.SynchronizeExternalMass() { }

        internal void Toggle() => State = State == DroneWinchState.Stowed
            ? DroneWinchState.Deployed
            : DroneWinchState.Stowed;
        internal void ResetStowed() => State = DroneWinchState.Stowed;
        internal void Step(float deltaTime) { }
        internal void StepPayloadMass(float deltaTime) { }
    }
}
