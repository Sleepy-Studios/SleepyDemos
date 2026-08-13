using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>六爪抓斗从开合到弱约束抓取的运行状态。</summary>
    internal enum DroneGrappleState
    {
        Open,
        Closing,
        Contacting,
        AssistedGrip,
        Releasing,
        Broken
    }

    /// <summary>驱动八个物理爪，并在三爪接触门禁后请求有限活动弱约束。</summary>
    public sealed class DroneMechanicalHook : MonoBehaviour
    {
        [SerializeField] private PayloadMount payloadMount;
        [SerializeField] private Transform grappleCenter;
        [SerializeField] private HingeJoint[] clawJoints = Array.Empty<HingeJoint>();
        [SerializeField] private DroneGrappleContactSensor[] contactSensors = Array.Empty<DroneGrappleContactSensor>();
        [SerializeField] private float captureRadiusMeters = 0.38f;
        [SerializeField] private float openAngleDegrees = -42f;
        [SerializeField] private float closedAngleDegrees = 28f;
        [SerializeField] private float clawSpring = 4f;
        [SerializeField] private float clawDamper = 0.6f;
        [SerializeField] private int minimumDistinctContacts = 3;

        /// 抓斗是否处于闭合指令状态。
        internal bool IsClosed { get; private set; }

        /// 当前抓取状态。
        internal DroneGrappleState State { get; private set; } = DroneGrappleState.Open;

        /// 当前最多爪同时接触同一载荷的数量。
        internal int CurrentContactCount { get; private set; }

        /// 当前中文操作提示。
        internal string CurrentHint => Time.unscaledTime <= hintUntilTime ? currentHint : string.Empty;

        private string currentHint = string.Empty;
        private float hintUntilTime;

        private void Update()
        {
            if (!IsClosed || payloadMount == null || payloadMount.HasPayload)
            {
                if (payloadMount != null && payloadMount.HasPayload)
                {
                    State = DroneGrappleState.AssistedGrip;
                }

                return;
            }

            TryAttachFromContacts();
        }

        /// 闭合六爪并尝试抓取当前已满足接触门禁的载荷。
        internal bool CloseAndTryAttach()
        {
            IsClosed = true;
            State = DroneGrappleState.Closing;
            ApplyClawSprings(closing: true);
            return TryAttachFromContacts();
        }

        /// 张开六爪并立即解除抓取弱约束。
        internal void OpenAndRelease()
        {
            State = DroneGrappleState.Releasing;
            IsClosed = false;
            CurrentContactCount = 0;
            payloadMount?.Release(PayloadReleaseReason.Manual);
            ApplyClawSprings(closing: false);
            State = DroneGrappleState.Open;
        }

        /// <summary>
        /// 由 Prefab 装配或测试绑定六个关节、接触传感器与抓斗中心。
        /// </summary>
        /// <param name="mount">负责建立弱约束的挂载边界。</param>
        /// <param name="joints">八个活动爪 HingeJoint。</param>
        /// <param name="sensors">与关节一一对应的接触传感器。</param>
        /// <param name="center">载荷必须进入的抓斗包围区中心。</param>
        internal void Configure(
            PayloadMount mount,
            HingeJoint[] joints,
            DroneGrappleContactSensor[] sensors,
            Transform center,
            float captureRadius = 0.38f)
        {
            payloadMount = mount;
            clawJoints = joints ?? Array.Empty<HingeJoint>();
            contactSensors = sensors ?? Array.Empty<DroneGrappleContactSensor>();
            grappleCenter = center != null ? center : transform;
            captureRadiusMeters = Mathf.Max(0.01f, captureRadius);
            ResetOpen();
        }

        /// 将抓斗恢复为张开且无载荷状态。
        internal void ResetOpen()
        {
            payloadMount?.Release(PayloadReleaseReason.Manual);
            IsClosed = false;
            CurrentContactCount = 0;
            State = DroneGrappleState.Open;
            ApplyClawSprings(closing: false);
        }

        /// <summary>显示短时中文机制提示。</summary>
        internal void ShowHint(string message)
        {
            currentHint = message ?? string.Empty;
            hintUntilTime = Time.unscaledTime + 2f;
            if (!string.IsNullOrEmpty(currentHint))
            {
                Debug.Log($"[DroneFlight] {currentHint}", this);
            }
        }

        private bool TryAttachFromContacts()
        {
            var counts = new Dictionary<DronePayload, int>();
            foreach (var sensor in contactSensors)
            {
                if (sensor == null)
                {
                    continue;
                }

                foreach (var payload in sensor.Contacts)
                {
                    counts.TryGetValue(payload, out var count);
                    counts[payload] = count + 1;
                }
            }

            DronePayload best = null;
            CurrentContactCount = 0;
            var center = grappleCenter != null ? grappleCenter.position : transform.position;
            foreach (var pair in counts)
            {
                if (pair.Key == null || pair.Key.ConnectionPoint == null)
                {
                    continue;
                }

                if ((pair.Key.ConnectionPoint.position - center).sqrMagnitude
                    > captureRadiusMeters * captureRadiusMeters)
                {
                    continue;
                }

                if (pair.Value > CurrentContactCount)
                {
                    CurrentContactCount = pair.Value;
                    best = pair.Key;
                }
            }

            if (best == null || CurrentContactCount < Mathf.Max(3, minimumDistinctContacts))
            {
                State = CurrentContactCount > 0 ? DroneGrappleState.Contacting : DroneGrappleState.Closing;
                return false;
            }

            var attached = payloadMount.TryAssistGrip(best, CurrentContactCount);
            State = attached ? DroneGrappleState.AssistedGrip : DroneGrappleState.Contacting;
            return attached;
        }

        private void ApplyClawSprings(bool closing)
        {
            foreach (var joint in clawJoints)
            {
                if (joint == null)
                {
                    continue;
                }

                joint.useMotor = false;
                joint.useSpring = true;
                joint.spring = new JointSpring
                {
                    targetPosition = closing ? closedAngleDegrees : openAngleDegrees,
                    spring = Mathf.Max(0.1f, clawSpring),
                    damper = Mathf.Max(0f, clawDamper)
                };
                joint.breakForce = float.PositiveInfinity;
                joint.breakTorque = float.PositiveInfinity;
            }
        }
    }
}
