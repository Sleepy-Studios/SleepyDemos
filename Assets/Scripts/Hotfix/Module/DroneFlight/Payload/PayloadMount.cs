using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>载荷释放原因。</summary>
    internal enum PayloadReleaseReason
    {
        None,
        Manual,
        Replaced,
        Overload,
        JointBreak,
        InvalidPayload,
        OwnerDisabled
    }

    /// <summary>
    /// 统一挂载边界：检查质量、建立物理 Joint、记录状态和释放原因。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PayloadMount : MonoBehaviour
    {
        [SerializeField] private Transform mountPoint;
        [SerializeField] private float maximumPayloadMassKilograms = 0.6f;
        [SerializeField] private float jointBreakForceNewtons = 250f;
        [SerializeField] private float jointBreakTorqueNewtonMeters = 100f;

        private ConfigurableJoint activeJoint;

        /// <summary>当前挂载载荷。</summary>
        internal DronePayload AttachedPayload { get; private set; }

        /// <summary>当前是否挂载载荷。</summary>
        internal bool HasPayload => AttachedPayload != null && activeJoint != null;

        /// <summary>当前载荷质量，单位 kg。</summary>
        internal float AttachedMassKilograms => AttachedPayload != null ? AttachedPayload.Body.mass : 0f;

        /// <summary>最近一次释放或拒绝原因。</summary>
        internal PayloadReleaseReason LastReleaseReason { get; private set; }

        /// <summary>挂载状态变化事件。</summary>
        internal event Action PayloadChanged;

        /// <summary>
        /// 尝试通过 ConfigurableJoint 连接独立载荷刚体。
        /// </summary>
        internal bool TryAttach(DronePayload payload)
        {
            if (payload == null || payload.Body == null || payload.Body == GetComponent<Rigidbody>())
            {
                LastReleaseReason = PayloadReleaseReason.InvalidPayload;
                return false;
            }

            if (!float.IsFinite(payload.Body.mass) || payload.Body.mass <= 0f
                || payload.Body.mass > maximumPayloadMassKilograms)
            {
                LastReleaseReason = PayloadReleaseReason.Overload;
                Debug.LogWarning(
                    $"[DroneFlight] 载荷 {payload.name} 质量 {payload.Body.mass:F2} kg 超过上限 {maximumPayloadMassKilograms:F2} kg。",
                    payload);
                return false;
            }

            if (HasPayload)
            {
                Release(PayloadReleaseReason.Replaced);
            }

            var point = mountPoint != null ? mountPoint : transform;
            activeJoint = gameObject.AddComponent<ConfigurableJoint>();
            activeJoint.connectedBody = payload.Body;
            activeJoint.autoConfigureConnectedAnchor = false;
            activeJoint.anchor = transform.InverseTransformPoint(point.position);
            activeJoint.connectedAnchor = payload.Body.transform.InverseTransformPoint(payload.ConnectionPoint.position);
            activeJoint.xMotion = ConfigurableJointMotion.Locked;
            activeJoint.yMotion = ConfigurableJointMotion.Locked;
            activeJoint.zMotion = ConfigurableJointMotion.Locked;
            activeJoint.angularXMotion = ConfigurableJointMotion.Locked;
            activeJoint.angularYMotion = ConfigurableJointMotion.Locked;
            activeJoint.angularZMotion = ConfigurableJointMotion.Locked;
            activeJoint.breakForce = jointBreakForceNewtons;
            activeJoint.breakTorque = jointBreakTorqueNewtonMeters;
            activeJoint.enableCollision = false;

            AttachedPayload = payload;
            LastReleaseReason = PayloadReleaseReason.None;
            PayloadChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 释放当前载荷并保留载荷独立刚体和碰撞。
        /// </summary>
        internal void Release(PayloadReleaseReason reason = PayloadReleaseReason.Manual)
        {
            if (activeJoint != null)
            {
                Destroy(activeJoint);
            }

            activeJoint = null;
            AttachedPayload = null;
            LastReleaseReason = reason;
            PayloadChanged?.Invoke();
        }

        /// <summary>
        /// 由场景装配或测试设置挂点与承载限制。
        /// </summary>
        internal void Configure(Transform point, float maximumMass)
        {
            mountPoint = point;
            maximumPayloadMassKilograms = maximumMass;
        }

        private void OnJointBreak(float breakForce)
        {
            activeJoint = null;
            AttachedPayload = null;
            LastReleaseReason = PayloadReleaseReason.JointBreak;
            PayloadChanged?.Invoke();
        }

        private void OnDisable()
        {
            if (HasPayload)
            {
                Release(PayloadReleaseReason.OwnerDisabled);
            }
        }
    }
}
