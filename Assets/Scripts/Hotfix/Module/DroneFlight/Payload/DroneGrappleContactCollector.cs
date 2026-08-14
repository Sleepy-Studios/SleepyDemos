using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>按爪编号与具体 Collider 统计四爪真实接触，避免一次 Exit 清空复合碰撞。</summary>
    public sealed class DroneGrappleContactCollector : MonoBehaviour
    {
        private sealed class Candidate
        {
            internal readonly Dictionary<int, HashSet<int>> Contacts = new();
            internal readonly Dictionary<int, Vector3> Points = new();
            internal int StableSteps;
        }

        private readonly Dictionary<DronePayload, Candidate> candidates = new();

        internal int ActiveContactCount { get; private set; }

        internal void Report(int clawIndex, Collider payloadCollider, Vector3 worldPoint)
        {
            var payload = payloadCollider != null ? payloadCollider.GetComponentInParent<DronePayload>() : null;
            if (payload == null)
            {
                return;
            }

            if (!candidates.TryGetValue(payload, out var candidate))
            {
                candidate = new Candidate();
                candidates.Add(payload, candidate);
            }

            if (!candidate.Contacts.TryGetValue(clawIndex, out var colliderIds))
            {
                colliderIds = new HashSet<int>();
                candidate.Contacts.Add(clawIndex, colliderIds);
            }

            colliderIds.Add(payloadCollider.GetInstanceID());
            candidate.Points[clawIndex] = worldPoint;
            RefreshCount();
        }

        internal void Remove(int clawIndex, Collider payloadCollider)
        {
            var payload = payloadCollider != null ? payloadCollider.GetComponentInParent<DronePayload>() : null;
            if (payload == null || !candidates.TryGetValue(payload, out var candidate)
                || !candidate.Contacts.TryGetValue(clawIndex, out var colliderIds))
            {
                return;
            }

            colliderIds.Remove(payloadCollider.GetInstanceID());
            if (colliderIds.Count == 0)
            {
                candidate.Contacts.Remove(clawIndex);
                candidate.Points.Remove(clawIndex);
                candidate.StableSteps = 0;
            }

            if (candidate.Contacts.Count == 0)
            {
                candidates.Remove(payload);
            }

            RefreshCount();
        }

        internal bool TryGetOpposingCandidate(
            Transform grappleRoot,
            float radius,
            float halfHeight,
            int requiredStableSteps,
            out DronePayload payload,
            out Vector3 contactCentroid,
            out int distinctClaws)
        {
            payload = null;
            contactCentroid = Vector3.zero;
            distinctClaws = 0;
            foreach (var pair in candidates)
            {
                var candidate = pair.Value;
                var hasOpposingPair = (candidate.Contacts.ContainsKey(0) && candidate.Contacts.ContainsKey(2))
                                      || (candidate.Contacts.ContainsKey(1) && candidate.Contacts.ContainsKey(3));
                var local = grappleRoot.InverseTransformPoint(pair.Key.Body.worldCenterOfMass);
                var enclosed = new Vector2(local.x, local.z).magnitude <= radius
                               && Mathf.Abs(local.y) <= halfHeight;
                candidate.StableSteps = hasOpposingPair && enclosed
                    ? candidate.StableSteps + 1
                    : 0;
                if (candidate.StableSteps < requiredStableSteps)
                {
                    continue;
                }

                var sum = Vector3.zero;
                foreach (var point in candidate.Points.Values)
                {
                    sum += point;
                }

                payload = pair.Key;
                distinctClaws = candidate.Contacts.Count;
                contactCentroid = candidate.Points.Count > 0
                    ? sum / candidate.Points.Count
                    : pair.Key.Body.worldCenterOfMass;
                return true;
            }

            return false;
        }

        internal void Clear()
        {
            candidates.Clear();
            ActiveContactCount = 0;
        }

        private void RefreshCount()
        {
            var count = 0;
            foreach (var candidate in candidates.Values)
            {
                count += candidate.Contacts.Count;
            }

            ActiveContactCount = count;
        }
    }
}
