using System;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>六爪抓斗从开合到软辅助抓取的运行状态。</summary>
    internal enum DroneGrappleState
    {
        Open,
        Closing,
        Contacting,
        AssistedGrip,
        Releasing,
        Broken
    }

    /// <summary>驱动单刚体复合六爪，并在真实三爪接触后请求软辅助约束。</summary>
    public sealed class DroneMechanicalHook : MonoBehaviour
    {
        [SerializeField] private PayloadMount payloadMount;
        [SerializeField] private Transform grappleCenter;
        [SerializeField] private Transform[] clawRoots = Array.Empty<Transform>();
        [SerializeField] private DroneGrappleContactCollector contactCollector;
        [SerializeField] private float captureRadiusMeters = 0.266f;
        [SerializeField] private float openAngleDegrees = -42f;
        [SerializeField] private float closedAngleDegrees = 28f;
        [SerializeField] private float clawAngularSpeedDegreesPerSecond = 180f;
        [SerializeField] private int minimumDistinctContacts = 3;

        [SerializeField, HideInInspector] private Quaternion[] clawBaseRotations = Array.Empty<Quaternion>();
        private float currentClawAngleDegrees = -42f;
        private string currentHint = string.Empty;
        private float hintUntilTime;

        /// 抓斗是否处于闭合指令状态。
        internal bool IsClosed { get; private set; }

        /// 当前抓取状态。
        internal DroneGrappleState State { get; private set; } = DroneGrappleState.Open;

        /// 当前最多爪同时接触同一载荷的数量。
        internal int CurrentContactCount { get; private set; }

        /// 当前中文操作提示。
        internal string CurrentHint => Time.unscaledTime <= hintUntilTime ? currentHint : string.Empty;

        private void FixedUpdate()
        {
            EnsureClawBaseRotations();
            StepClawAnimation(Time.fixedDeltaTime);
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
            return TryAttachFromContacts();
        }

        /// 张开六爪并立即解除抓取软约束。
        internal void OpenAndRelease()
        {
            State = DroneGrappleState.Releasing;
            IsClosed = false;
            CurrentContactCount = 0;
            payloadMount?.Release(PayloadReleaseReason.Manual);
            State = DroneGrappleState.Open;
        }

        /// <summary>由 Prefab 装配抓斗、六爪动画根和统一接触收集器。</summary>
        /// <param name="mount">负责建立软约束的挂载边界。</param>
        /// <param name="claws">六个复合 Collider 的动画根。</param>
        /// <param name="collector">按爪编号汇总真实碰撞点的收集器。</param>
        /// <param name="center">载荷必须进入的抓斗包围区中心。</param>
        /// <param name="captureRadius">抓取包围区半径，单位 m。</param>
        internal void Configure(
            PayloadMount mount,
            Transform[] claws,
            DroneGrappleContactCollector collector,
            Transform center,
            float captureRadius = 0.266f)
        {
            payloadMount = mount;
            clawRoots = claws ?? Array.Empty<Transform>();
            contactCollector = collector;
            grappleCenter = center != null ? center : transform;
            captureRadiusMeters = Mathf.Max(0.01f, captureRadius);
            CaptureClawBaseRotations();
            ResetOpen();
        }

        /// 将抓斗恢复为张开且无载荷状态。
        internal void ResetOpen()
        {
            payloadMount?.Release(PayloadReleaseReason.Manual);
            IsClosed = false;
            CurrentContactCount = 0;
            State = DroneGrappleState.Open;
            currentClawAngleDegrees = openAngleDegrees;
            ApplyClawAngle(currentClawAngleDegrees);
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
            var center = grappleCenter != null ? grappleCenter.position : transform.position;
            if (contactCollector == null
                || !contactCollector.TryGetBestSnapshot(center, captureRadiusMeters, out var snapshot))
            {
                CurrentContactCount = 0;
                State = DroneGrappleState.Closing;
                return false;
            }

            CurrentContactCount = snapshot.DistinctClawCount;
            if (CurrentContactCount < Mathf.Max(3, minimumDistinctContacts))
            {
                State = DroneGrappleState.Contacting;
                return false;
            }

            var attached = payloadMount.TryAssistGrip(snapshot);
            State = attached ? DroneGrappleState.AssistedGrip : DroneGrappleState.Contacting;
            return attached;
        }

        private void StepClawAnimation(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            var target = IsClosed ? closedAngleDegrees : openAngleDegrees;
            currentClawAngleDegrees = Mathf.MoveTowards(
                currentClawAngleDegrees,
                target,
                Mathf.Max(1f, clawAngularSpeedDegreesPerSecond) * deltaTime);
            ApplyClawAngle(currentClawAngleDegrees);
        }

        private void CaptureClawBaseRotations()
        {
            clawBaseRotations = new Quaternion[clawRoots.Length];
            for (var index = 0; index < clawRoots.Length; index++)
            {
                if (clawRoots[index] != null)
                {
                    clawBaseRotations[index] = clawRoots[index].localRotation;
                }
            }
        }

        private void EnsureClawBaseRotations()
        {
            if (clawBaseRotations == null || clawBaseRotations.Length != clawRoots.Length)
            {
                CaptureClawBaseRotations();
            }
        }

        private void ApplyClawAngle(float angleDegrees)
        {
            EnsureClawBaseRotations();

            for (var index = 0; index < clawRoots.Length; index++)
            {
                if (clawRoots[index] != null)
                {
                    clawRoots[index].localRotation = clawBaseRotations[index]
                                                          * Quaternion.AngleAxis(angleDegrees, Vector3.right);
                }
            }
        }
    }
}
