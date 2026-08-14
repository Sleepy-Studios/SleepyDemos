using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>一次六爪真实接触的只读快照。</summary>
    internal readonly struct DroneGripContactSnapshot
    {
        internal DroneGripContactSnapshot(DronePayload payload, int distinctClawCount, Vector3 worldContactCenter)
        {
            Payload = payload;
            DistinctClawCount = distinctClawCount;
            WorldContactCenter = worldContactCenter;
        }

        internal DronePayload Payload { get; }
        internal int DistinctClawCount { get; }
        internal Vector3 WorldContactCenter { get; }
        internal bool IsValid => Payload != null && DistinctClawCount > 0;
    }

    /// <summary>在单一抓斗 Rigidbody 上按爪编号汇总真实碰撞点。</summary>
    public sealed class DroneGrappleContactCollector : MonoBehaviour
    {
        [Serializable]
        private sealed class ClawColliderGroup
        {
            [SerializeField] private Collider[] colliders = Array.Empty<Collider>();

            internal ClawColliderGroup(Collider[] source)
            {
                colliders = source ?? Array.Empty<Collider>();
            }

            internal Collider[] Colliders => colliders ?? Array.Empty<Collider>();
        }

        [SerializeField] private ClawColliderGroup[] clawColliderGroups = Array.Empty<ClawColliderGroup>();

        private readonly Dictionary<Collider, int> clawByCollider = new();
        private readonly Dictionary<ContactKey, ContactSample> contacts = new();

        /// Prefab 中已持久化的爪编号数量。
        internal int ConfiguredClawCount => clawColliderGroups?.Length ?? 0;

        private void Awake()
        {
            RebuildColliderLookup();
        }

        /// 当前仍有效的载荷接触数上限。
        internal int MaximumDistinctClawContacts
        {
            get
            {
                PruneStaleContacts();
                var counts = new Dictionary<DronePayload, HashSet<int>>();
                foreach (var pair in contacts)
                {
                    if (pair.Key.Payload == null)
                    {
                        continue;
                    }

                    if (!counts.TryGetValue(pair.Key.Payload, out var claws))
                    {
                        claws = new HashSet<int>();
                        counts.Add(pair.Key.Payload, claws);
                    }

                    claws.Add(pair.Key.ClawIndex);
                }

                var maximum = 0;
                foreach (var claws in counts.Values)
                {
                    maximum = Mathf.Max(maximum, claws.Count);
                }

                return maximum;
            }
        }

        /// <summary>绑定六爪复合 Collider 与稳定编号。</summary>
        internal void Configure(Collider[][] clawColliders)
        {
            contacts.Clear();
            if (clawColliders == null)
            {
                clawColliderGroups = Array.Empty<ClawColliderGroup>();
                clawByCollider.Clear();
                return;
            }

            clawColliderGroups = new ClawColliderGroup[clawColliders.Length];
            for (var clawIndex = 0; clawIndex < clawColliders.Length; clawIndex++)
            {
                clawColliderGroups[clawIndex] = new ClawColliderGroup(clawColliders[clawIndex]);
            }

            RebuildColliderLookup();
        }

        /// <summary>确认指定复合 Collider 在场景重新加载后仍能解析到稳定爪编号。</summary>
        internal bool TryGetClawIndex(Collider collider, out int clawIndex)
        {
            clawIndex = -1;
            EnsureColliderLookup();
            return collider != null && clawByCollider.TryGetValue(collider, out clawIndex);
        }

        /// <summary>返回包围区内接触爪数最多的载荷及真实接触质心。</summary>
        internal bool TryGetBestSnapshot(Vector3 captureCenter, float captureRadius, out DroneGripContactSnapshot snapshot)
        {
            PruneStaleContacts();
            var aggregates = new Dictionary<DronePayload, ContactAggregate>();
            foreach (var pair in contacts)
            {
                var payload = pair.Key.Payload;
                if (payload == null || payload.ConnectionPoint == null
                    || (payload.ConnectionPoint.position - captureCenter).sqrMagnitude > captureRadius * captureRadius)
                {
                    continue;
                }

                if (!aggregates.TryGetValue(payload, out var aggregate))
                {
                    aggregate = new ContactAggregate();
                }

                aggregate.Claws.Add(pair.Key.ClawIndex);
                aggregate.PointSum += pair.Value.WorldPoint;
                aggregate.PointCount++;
                aggregates[payload] = aggregate;
            }

            DronePayload bestPayload = null;
            ContactAggregate best = null;
            foreach (var pair in aggregates)
            {
                if (bestPayload == null || pair.Value.Claws.Count > best.Claws.Count)
                {
                    bestPayload = pair.Key;
                    best = pair.Value;
                }
            }

            if (bestPayload == null || best.PointCount <= 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = new DroneGripContactSnapshot(
                bestPayload,
                best.Claws.Count,
                best.PointSum / best.PointCount);
            return true;
        }

        /// <summary>供确定性测试注入或移除单爪接触。</summary>
        internal void ReportContact(int clawIndex, DronePayload payload, Vector3 worldPoint, bool isContacting)
        {
            ReportContact(clawIndex, payload, null, worldPoint, isContacting);
        }

        /// <summary>供确定性测试验证同一爪与载荷多 Collider 的独立接触计数。</summary>
        internal void ReportContact(
            int clawIndex,
            DronePayload payload,
            Collider payloadCollider,
            Vector3 worldPoint,
            bool isContacting)
        {
            if (payload == null || clawIndex < 0)
            {
                return;
            }

            var key = new ContactKey(payload, clawIndex, payloadCollider);
            if (!isContacting)
            {
                contacts.Remove(key);
                return;
            }

            contacts[key] = new ContactSample(worldPoint, CurrentPhysicsTime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            RecordCollision(collision);
        }

        private void OnCollisionStay(Collision collision)
        {
            RecordCollision(collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            var payload = collision.collider.GetComponentInParent<DronePayload>();
            if (payload == null)
            {
                return;
            }

            var stale = new List<ContactKey>();
            foreach (var pair in contacts)
            {
                if (pair.Key.Payload == payload && pair.Key.OtherCollider == collision.collider)
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (var key in stale)
            {
                contacts.Remove(key);
            }
        }

        private void RecordCollision(Collision collision)
        {
            if (collision == null)
            {
                return;
            }

            EnsureColliderLookup();
            for (var index = 0; index < collision.contactCount; index++)
            {
                var contact = collision.GetContact(index);
                if (!clawByCollider.TryGetValue(contact.thisCollider, out var clawIndex))
                {
                    continue;
                }

                var payload = contact.otherCollider != null
                    ? contact.otherCollider.GetComponentInParent<DronePayload>()
                    : null;
                if (payload == null)
                {
                    continue;
                }

                contacts[new ContactKey(payload, clawIndex, contact.otherCollider)] =
                    new ContactSample(contact.point, CurrentPhysicsTime);
            }
        }

        private void PruneStaleContacts()
        {
            var maximumAge = Mathf.Max(Time.fixedDeltaTime * 2.5f, 0.05f);
            var now = CurrentPhysicsTime;
            var stale = new List<ContactKey>();
            foreach (var pair in contacts)
            {
                if (pair.Key.Payload == null || now - pair.Value.PhysicsTime > maximumAge)
                {
                    stale.Add(pair.Key);
                }
            }

            foreach (var key in stale)
            {
                contacts.Remove(key);
            }
        }

        private static float CurrentPhysicsTime => Time.fixedTime;

        private void EnsureColliderLookup()
        {
            if (clawByCollider.Count == 0 && ConfiguredClawCount > 0)
            {
                RebuildColliderLookup();
            }
        }

        private void RebuildColliderLookup()
        {
            clawByCollider.Clear();
            if (clawColliderGroups == null)
            {
                return;
            }

            for (var clawIndex = 0; clawIndex < clawColliderGroups.Length; clawIndex++)
            {
                var group = clawColliderGroups[clawIndex];
                if (group == null)
                {
                    continue;
                }

                foreach (var collider in group.Colliders)
                {
                    if (collider != null)
                    {
                        clawByCollider[collider] = clawIndex;
                    }
                }
            }
        }

        private void OnDisable()
        {
            contacts.Clear();
        }

        private readonly struct ContactKey : IEquatable<ContactKey>
        {
            internal ContactKey(DronePayload payload, int clawIndex, Collider otherCollider)
            {
                Payload = payload;
                ClawIndex = clawIndex;
                OtherCollider = otherCollider;
            }

            internal DronePayload Payload { get; }
            internal int ClawIndex { get; }
            internal Collider OtherCollider { get; }

            public bool Equals(ContactKey other)
            {
                return Payload == other.Payload && ClawIndex == other.ClawIndex && OtherCollider == other.OtherCollider;
            }

            public override bool Equals(object obj)
            {
                return obj is ContactKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Payload, ClawIndex, OtherCollider);
            }
        }

        private readonly struct ContactSample
        {
            internal ContactSample(Vector3 worldPoint, float physicsTime)
            {
                WorldPoint = worldPoint;
                PhysicsTime = physicsTime;
            }

            internal Vector3 WorldPoint { get; }
            internal float PhysicsTime { get; }
        }

        private sealed class ContactAggregate
        {
            internal readonly HashSet<int> Claws = new();
            internal Vector3 PointSum;
            internal int PointCount;
        }
    }
}
