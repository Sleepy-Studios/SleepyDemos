using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>记录单个活动爪真实接触到的载荷。</summary>
    public sealed class DroneGrappleContactSensor : MonoBehaviour
    {
        private readonly HashSet<DronePayload> manualContacts = new();
        private readonly Dictionary<Collider, DronePayload> colliderContacts = new();
        private readonly HashSet<DronePayload> combinedContacts = new();

        internal IEnumerable<DronePayload> Contacts
        {
            get
            {
                combinedContacts.Clear();
                combinedContacts.UnionWith(manualContacts);
                foreach (var payload in colliderContacts.Values)
                {
                    if (payload != null)
                    {
                        combinedContacts.Add(payload);
                    }
                }

                return combinedContacts;
            }
        }

        internal void ReportContact(DronePayload payload, bool isContacting)
        {
            if (payload == null)
            {
                return;
            }

            if (isContacting)
            {
                manualContacts.Add(payload);
            }
            else
            {
                manualContacts.Remove(payload);
            }
        }

        /// <summary>按实际 Collider 记录接触，供物理回调与确定性测试共用。</summary>
        internal void ReportColliderContact(Collider collider, bool isContacting)
        {
            if (collider == null)
            {
                return;
            }

            if (!isContacting)
            {
                colliderContacts.Remove(collider);
                return;
            }

            var payload = collider.GetComponentInParent<DronePayload>();
            if (payload != null)
            {
                colliderContacts[collider] = payload;
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            ReportCollision(collision, true);
        }

        private void OnCollisionStay(Collision collision)
        {
            ReportCollision(collision, true);
        }

        private void OnCollisionExit(Collision collision)
        {
            ReportCollision(collision, false);
        }

        private void ReportCollision(Collision collision, bool isContacting)
        {
            ReportColliderContact(collision.collider, isContacting);
        }

        private void OnDisable()
        {
            manualContacts.Clear();
            colliderContacts.Clear();
            combinedContacts.Clear();
        }
    }
}
