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

    /// <summary>三爪真实接触后建立零误差、可断裂的软保险约束。</summary>
    public sealed class PayloadMount : MonoBehaviour
    {
        [SerializeField] private Rigidbody gripBody;
        [SerializeField] private Transform mountPoint;
        [SerializeField] private DroneFlightConfig config;
        [SerializeField] private float maximumPayloadMassKilograms = 0.6f;
        [SerializeField] private float jointBreakForceNewtons = 180f;
        [SerializeField] private float jointBreakTorqueNewtonMeters = 80f;
        [SerializeField] private float linearFreedomMeters = 0.025f;
        [SerializeField] private float takeupSeconds = 0.3f;
        [SerializeField] private float workingSpringNewtonsPerMeter = 250f;
        [SerializeField] private float workingDamperNewtonSecondsPerMeter = 25f;
        [SerializeField] private Collider[] ignoredSupportColliders = Array.Empty<Collider>();

        private ConfigurableJoint activeJoint;
        private float takeupElapsedSeconds;

        /// 当前挂载载荷。
        internal DronePayload AttachedPayload { get; private set; }

        /// 当前是否由抓斗软约束辅助保持载荷。
        internal bool HasPayload => AttachedPayload != null && activeJoint != null;

        /// 当前载荷质量，单位 kg。
        internal float AttachedMassKilograms => AttachedPayload != null ? AttachedPayload.Body.mass : 0f;

        /// 当前载荷是否仍由地面或其它非机构物体支撑。
        internal bool IsAttachedPayloadGroundSupported => AttachedPayload != null
                                                         && (!AttachedPayload.IsSupportStateConfirmed
                                                             || AttachedPayload.IsGroundSupported);

        /// 当前载荷的有效外部支撑 Collider 数量。
        internal int AttachedPayloadSupportContactCount => AttachedPayload != null
            ? AttachedPayload.EffectiveSupportContactCount
            : 0;

        /// 当前载荷受到的外部竖直支持力。
        internal float AttachedPayloadUpwardSupportForceNewtons => AttachedPayload != null
            ? AttachedPayload.LastUpwardSupportForceNewtons
            : 0f;

        /// 软约束从零到工作刚度的进度。
        internal float TakeupProgress => HasPayload
            ? Mathf.Clamp01(takeupElapsedSeconds / Mathf.Max(0.01f, takeupSeconds))
            : 0f;

        /// 最近一次抓取使用的世界接触质心。
        internal Vector3 GripWorldContactCenter { get; private set; }

        /// 当前约束力的竖直分量，仅用于 F3 诊断。
        internal float CurrentVerticalGripForceNewtons
        {
            get
            {
                if (activeJoint == null || activeJoint.connectedBody == null)
                {
                    return 0f;
                }

                return Mathf.Max(0f, Vector3.Dot(activeJoint.currentForce, -Physics.gravity.normalized));
            }
        }

        /// 最近一次释放或拒绝原因。
        internal PayloadReleaseReason LastReleaseReason { get; private set; }

        /// 当前软约束；只供定向测试和诊断读取。
        internal ConfigurableJoint ActiveJoint => activeJoint;

        internal float MaximumPayloadMassKilograms
        {
            get
            {
                RefreshConfigValues();
                return maximumPayloadMassKilograms;
            }
        }

        /// 挂载状态变化事件。
        internal event Action PayloadChanged;

        private void FixedUpdate()
        {
            StepTakeup(Time.fixedDeltaTime);
        }

        /// <summary>兼容旧夹具的挂载入口；正式抓斗使用真实接触快照。</summary>
        internal bool TryAttach(DronePayload payload)
        {
            return TryAssistGrip(payload, 3);
        }

        /// <summary>兼容旧调用，以载荷当前连接点作为零误差接触点。</summary>
        internal bool TryAssistGrip(DronePayload payload, int distinctClawContacts)
        {
            var point = payload != null && payload.ConnectionPoint != null
                ? payload.ConnectionPoint.position
                : Vector3.zero;
            return TryAssistGrip(new DroneGripContactSnapshot(payload, distinctClawContacts, point));
        }

        /// <summary>根据真实接触质心建立零误差软约束。</summary>
        internal bool TryAssistGrip(DroneGripContactSnapshot snapshot)
        {
            RefreshConfigValues();
            var payload = snapshot.Payload;
            var ownerBody = ResolveGripBody();
            if (snapshot.DistinctClawCount < 3
                || payload == null
                || payload.Body == null
                || ownerBody == null
                || payload.Body == ownerBody
                || !IsFinite(snapshot.WorldContactCenter))
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
                if (AttachedPayload == payload)
                {
                    return true;
                }

                Release(PayloadReleaseReason.Replaced);
            }

            GripWorldContactCenter = snapshot.WorldContactCenter;
            activeJoint = ownerBody.gameObject.AddComponent<ConfigurableJoint>();
            activeJoint.connectedBody = payload.Body;
            activeJoint.autoConfigureConnectedAnchor = false;
            activeJoint.anchor = ownerBody.transform.InverseTransformPoint(GripWorldContactCenter);
            activeJoint.connectedAnchor = payload.Body.transform.InverseTransformPoint(GripWorldContactCenter);
            activeJoint.xMotion = ConfigurableJointMotion.Limited;
            activeJoint.yMotion = ConfigurableJointMotion.Limited;
            activeJoint.zMotion = ConfigurableJointMotion.Limited;
            activeJoint.linearLimit = new SoftJointLimit { limit = Mathf.Max(0.005f, linearFreedomMeters) };
            activeJoint.linearLimitSpring = new SoftJointLimitSpring();
            activeJoint.angularXMotion = ConfigurableJointMotion.Free;
            activeJoint.angularYMotion = ConfigurableJointMotion.Free;
            activeJoint.angularZMotion = ConfigurableJointMotion.Free;
            activeJoint.breakForce = jointBreakForceNewtons;
            activeJoint.breakTorque = jointBreakTorqueNewtonMeters;
            activeJoint.enableCollision = false;
            activeJoint.enablePreprocessing = true;
            activeJoint.projectionMode = JointProjectionMode.None;
            activeJoint.massScale = 1f;
            activeJoint.connectedMassScale = 1f;
            payload.Body.interpolation = RigidbodyInterpolation.Interpolate;
            payload.Body.solverIterations = Mathf.Max(payload.Body.solverIterations, 12);
            payload.Body.solverVelocityIterations = Mathf.Max(payload.Body.solverVelocityIterations, 8);

            var relay = ownerBody.GetComponent<DronePayloadJointBreakRelay>()
                        ?? ownerBody.gameObject.AddComponent<DronePayloadJointBreakRelay>();
            relay.Configure(this);
            takeupElapsedSeconds = 0f;
            AttachedPayload = payload;
            AttachedPayload.ConfigureIgnoredSupportColliders(ignoredSupportColliders);
            LastReleaseReason = PayloadReleaseReason.None;
            PayloadChanged?.Invoke();
            return true;
        }

        /// <summary>推进软保险约束从无力到工作刚度的接入过程。</summary>
        internal void StepTakeup(float deltaTime)
        {
            if (activeJoint == null || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            takeupElapsedSeconds = Mathf.Min(
                takeupElapsedSeconds + deltaTime,
                Mathf.Max(0.01f, takeupSeconds));
            var progress = Mathf.SmoothStep(0f, 1f, TakeupProgress);
            activeJoint.linearLimitSpring = new SoftJointLimitSpring
            {
                spring = Mathf.Max(0f, workingSpringNewtonsPerMeter) * progress,
                damper = Mathf.Max(0f, workingDamperNewtonSecondsPerMeter) * progress
            };
        }

        /// <summary>释放当前载荷并保留载荷独立刚体、世界位姿和碰撞。</summary>
        internal void Release(PayloadReleaseReason reason = PayloadReleaseReason.Manual)
        {
            var releasedPayload = AttachedPayload;
            if (activeJoint != null)
            {
                Destroy(activeJoint);
            }

            activeJoint = null;
            AttachedPayload = null;
            takeupElapsedSeconds = 0f;
            GripWorldContactCenter = Vector3.zero;
            releasedPayload?.ClearIgnoredSupportColliders();
            LastReleaseReason = reason;
            PayloadChanged?.Invoke();
        }

        /// <summary>由 Prefab 装配或测试设置抓斗刚体、挂点和承载限制。</summary>
        internal void Configure(Transform point, float maximumMass, Rigidbody owner = null)
        {
            mountPoint = point;
            maximumPayloadMassKilograms = maximumMass;
            gripBody = owner;
        }

        /// <summary>绑定单一 DroneFlight 配置，使 Play Mode 门禁与软约束立即跟随调校。</summary>
        internal void Configure(Transform point, DroneFlightConfig flightConfig, Rigidbody owner = null)
        {
            mountPoint = point;
            config = flightConfig;
            gripBody = owner;
            RefreshConfigValues();
        }

        /// <summary>配置不得作为地面支撑来源的无人机与抓斗机构 Collider。</summary>
        internal void ConfigureIgnoredSupportColliders(Collider[] colliders)
        {
            ignoredSupportColliders = colliders ?? Array.Empty<Collider>();
            AttachedPayload?.ConfigureIgnoredSupportColliders(ignoredSupportColliders);
        }

        internal void NotifyJointBreak()
        {
            if (activeJoint == null && AttachedPayload == null)
            {
                return;
            }

            var releasedPayload = AttachedPayload;
            activeJoint = null;
            AttachedPayload = null;
            takeupElapsedSeconds = 0f;
            releasedPayload?.ClearIgnoredSupportColliders();
            LastReleaseReason = PayloadReleaseReason.JointBreak;
            PayloadChanged?.Invoke();
        }

        private Rigidbody ResolveGripBody()
        {
            return gripBody != null ? gripBody : GetComponent<Rigidbody>();
        }

        private void RefreshConfigValues()
        {
            if (config == null || !config.TryValidate(out _))
            {
                return;
            }

            maximumPayloadMassKilograms = config.MaximumPayloadMassKilograms;
            jointBreakForceNewtons = config.GrappleBreakForceNewtons;
            jointBreakTorqueNewtonMeters = config.GrappleBreakTorqueNewtonMeters;
            linearFreedomMeters = config.GrappleLinearFreedomMeters;
            takeupSeconds = config.GrappleTakeupSeconds;
            workingSpringNewtonsPerMeter = config.GrappleWorkingSpring;
            workingDamperNewtonSecondsPerMeter = config.GrappleWorkingDamper;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
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
