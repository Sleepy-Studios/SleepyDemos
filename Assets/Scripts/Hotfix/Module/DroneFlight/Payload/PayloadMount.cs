using UnityEngine;

namespace Hotfix.DroneFlight
{
    internal enum PayloadReleaseReason
    {
        None, Manual, Replaced, Overload, JointBreak, InvalidPayload, OwnerDisabled
    }

    internal readonly struct DroneGripContactSnapshot
    {
        internal DroneGripContactSnapshot(DronePayload payload, int distinctClawContacts, Vector3 worldContactCenter)
        {
            Payload = payload;
            DistinctClawContacts = distinctClawContacts;
            WorldContactCenter = worldContactCenter;
        }
        internal DronePayload Payload { get; }
        internal int DistinctClawContacts { get; }
        internal Vector3 WorldContactCenter { get; }
    }

    /// <summary>旧软抓取状态链兼容壳；新四爪模块自行维护约束。</summary>
    public sealed class PayloadMount : MonoBehaviour
    {
        internal DronePayload AttachedPayload { get; private set; }
        internal bool HasPayload => false;
        internal float AttachedMassKilograms => AttachedPayload != null ? AttachedPayload.Body.mass : 0f;
        internal bool IsAttachedPayloadGroundSupported => false;
        internal int AttachedPayloadSupportContactCount => 0;
        internal float AttachedPayloadUpwardSupportForceNewtons => 0f;
        internal float TakeupProgress => 0f;
        internal Vector3 GripWorldContactCenter { get; private set; }
        internal float CurrentVerticalGripForceNewtons => 0f;
        internal PayloadReleaseReason LastReleaseReason { get; private set; }
        internal ConfigurableJoint ActiveJoint => null;
        internal float MaximumPayloadMassKilograms => 0f;

        internal bool TryAttach(DronePayload payload) => false;
        internal bool TryAssistGrip(DronePayload payload, int distinctClawContacts) => false;
        internal bool TryAssistGrip(DroneGripContactSnapshot snapshot) => false;
        internal void StepTakeup(float deltaTime) { }
        internal void Configure(Transform point, float maximumMass, Rigidbody owner = null) { }
        internal void Configure(Transform point, DroneFlightConfig flightConfig, Rigidbody owner = null) { }
        internal void ConfigureIgnoredSupportColliders(Collider[] colliders) { }
        internal void NotifyJointBreak() => LastReleaseReason = PayloadReleaseReason.JointBreak;
        internal void Release(PayloadReleaseReason reason = PayloadReleaseReason.Manual)
        {
            AttachedPayload = null;
            LastReleaseReason = reason;
        }
    }
}
