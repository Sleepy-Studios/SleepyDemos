using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>把 Joint 断裂消息转发给可能位于其它物体上的 PayloadMount。</summary>
    public sealed class DronePayloadJointBreakRelay : MonoBehaviour
    {
        private PayloadMount payloadMount;

        internal void Configure(PayloadMount mount)
        {
            payloadMount = mount;
        }

        private void OnJointBreak(float breakForce)
        {
            payloadMount?.NotifyJointBreak();
        }
    }
}
