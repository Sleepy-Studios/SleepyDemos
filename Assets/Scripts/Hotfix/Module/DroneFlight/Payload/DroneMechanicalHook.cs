using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// 机械抓钩只负责候选检测、爪动画与调用统一挂载接口。
    /// </summary>
    public sealed class DroneMechanicalHook : MonoBehaviour
    {
        [SerializeField] private PayloadMount payloadMount;
        [SerializeField] private Transform leftClaw;
        [SerializeField] private Transform rightClaw;
        [SerializeField] private float detectionRadiusMeters = 0.35f;
        [SerializeField] private LayerMask payloadLayers = ~0;
        [SerializeField] private float openAngleDegrees = 35f;

        private readonly Collider[] overlapBuffer = new Collider[16];

        /// <summary>抓钩当前是否闭合。</summary>
        internal bool IsClosed { get; private set; }

        /// <summary>闭合抓钩并尝试连接最近的合法载荷。</summary>
        internal bool CloseAndTryAttach()
        {
            IsClosed = true;
            ApplyClawPose();
            if (payloadMount == null || payloadMount.HasPayload)
            {
                return payloadMount != null && payloadMount.HasPayload;
            }

            var count = Physics.OverlapSphereNonAlloc(
                transform.position,
                Mathf.Max(0.05f, detectionRadiusMeters),
                overlapBuffer,
                payloadLayers,
                QueryTriggerInteraction.Collide);
            DronePayload nearest = null;
            var nearestDistance = float.PositiveInfinity;
            var visited = new HashSet<DronePayload>();
            for (var index = 0; index < count; index++)
            {
                var payload = overlapBuffer[index] != null
                    ? overlapBuffer[index].GetComponentInParent<DronePayload>()
                    : null;
                if (payload == null || !visited.Add(payload))
                {
                    continue;
                }

                var distance = (payload.ConnectionPoint.position - transform.position).sqrMagnitude;
                if (distance < nearestDistance)
                {
                    nearest = payload;
                    nearestDistance = distance;
                }
            }

            return nearest != null && payloadMount.TryAttach(nearest);
        }

        /// <summary>张开抓钩并释放当前载荷。</summary>
        internal void OpenAndRelease()
        {
            IsClosed = false;
            ApplyClawPose();
            payloadMount?.Release(PayloadReleaseReason.Manual);
        }

        /// <summary>由场景装配或测试绑定挂载系统和活动爪。</summary>
        internal void Configure(PayloadMount mount, Transform left, Transform right)
        {
            payloadMount = mount;
            leftClaw = left;
            rightClaw = right;
            ApplyClawPose();
        }

        private void ApplyClawPose()
        {
            var angle = IsClosed ? 0f : openAngleDegrees;
            if (leftClaw != null)
            {
                leftClaw.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (rightClaw != null)
            {
                rightClaw.localRotation = Quaternion.Euler(0f, 0f, -angle);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRadiusMeters);
        }
    }
}
