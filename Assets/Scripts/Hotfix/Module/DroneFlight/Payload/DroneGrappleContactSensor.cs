using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>单个物理爪的接触传感器。</summary>
    public sealed class DroneGrappleContactSensor : MonoBehaviour
    {
        [SerializeField] private DroneGrappleContactCollector collector;
        [SerializeField] private int clawIndex;

        internal void Configure(DroneGrappleContactCollector target, int index)
        {
            collector = target;
            clawIndex = index;
        }

        private void OnCollisionEnter(Collision collision)
        {
            Report(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            Report(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.collider != null)
            {
                collector?.Remove(clawIndex, collision.collider);
            }
        }

        private void Report(Collision collision)
        {
            if (collector == null || collision.collider == null || collision.contactCount == 0)
            {
                return;
            }

            collector.Report(clawIndex, collision.collider, collision.GetContact(0).point);
        }
    }
}
