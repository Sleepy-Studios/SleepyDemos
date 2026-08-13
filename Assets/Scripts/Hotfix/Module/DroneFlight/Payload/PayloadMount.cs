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

    /// <summary>只在多爪真实接触后，为抓斗建立可断裂的有限活动弱约束。</summary>
    public sealed class PayloadMount : MonoBehaviour
    {
        [SerializeField] private Rigidbody gripBody;
        [SerializeField] private Transform mountPoint;
        [SerializeField] private DroneFlightConfig config;
        [SerializeField] private float maximumPayloadMassKilograms = 0.6f;
        [SerializeField] private float jointBreakForceNewtons = 180f;
        [SerializeField] private float jointBreakTorqueNewtonMeters = 80f;
        [SerializeField] private float linearFreedomMeters = 0.035f;
        [SerializeField] private float angularFreedomDegrees = 12f;

        private ConfigurableJoint activeJoint;
        private float supportedMassKilograms;

        private const float SupportBlendFrequency = 8f;

        /// 当前挂载载荷。
        internal DronePayload AttachedPayload { get; private set; }

        /// 当前是否由抓斗弱约束辅助保持载荷。
        internal bool HasPayload => AttachedPayload != null && activeJoint != null;

        /// 当前载荷质量，单位 kg。
        internal float AttachedMassKilograms => AttachedPayload != null ? AttachedPayload.Body.mass : 0f;

        /// 当前由弱约束真实张力传递给无人机的载荷质量，单位 kg。
        internal float SupportedMassKilograms => supportedMassKilograms;

        /// 最近一次释放或拒绝原因。
        internal PayloadReleaseReason LastReleaseReason { get; private set; }

        /// 当前弱约束；只供定向测试和诊断读取。
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

        /// <summary>
        /// 兼容旧夹具的挂载入口；正式抓斗必须改用带接触数的入口。
        /// </summary>
        /// <param name="payload">保留独立 Rigidbody 的目标载荷。</param>
        internal bool TryAttach(DronePayload payload)
        {
            return TryAssistGrip(payload, 3);
        }

        /// <summary>
        /// 在满足多爪接触门禁后建立有限活动、可断裂的抓取弱约束。
        /// </summary>
        /// <param name="payload">被多个爪真实接触的目标载荷。</param>
        /// <param name="distinctClawContacts">接触该载荷的不同爪数量；少于三个会拒绝。</param>
        internal bool TryAssistGrip(DronePayload payload, int distinctClawContacts)
        {
            RefreshConfigValues();
            var ownerBody = ResolveGripBody();
            if (distinctClawContacts < 3
                || payload == null
                || payload.Body == null
                || ownerBody == null
                || payload.Body == ownerBody)
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

            var point = mountPoint != null ? mountPoint : ownerBody.transform;
            activeJoint = ownerBody.gameObject.AddComponent<ConfigurableJoint>();
            activeJoint.connectedBody = payload.Body;
            activeJoint.autoConfigureConnectedAnchor = false;
            activeJoint.anchor = ownerBody.transform.InverseTransformPoint(point.position);
            activeJoint.connectedAnchor = payload.Body.transform.InverseTransformPoint(payload.ConnectionPoint.position);
            activeJoint.xMotion = ConfigurableJointMotion.Limited;
            activeJoint.yMotion = ConfigurableJointMotion.Limited;
            activeJoint.zMotion = ConfigurableJointMotion.Limited;
            activeJoint.linearLimit = new SoftJointLimit { limit = Mathf.Max(0.005f, linearFreedomMeters) };
            activeJoint.linearLimitSpring = new SoftJointLimitSpring { spring = 900f, damper = 90f };
            activeJoint.angularXMotion = ConfigurableJointMotion.Limited;
            activeJoint.angularYMotion = ConfigurableJointMotion.Limited;
            activeJoint.angularZMotion = ConfigurableJointMotion.Limited;
            var angularLimit = new SoftJointLimit { limit = Mathf.Clamp(angularFreedomDegrees, 1f, 45f) };
            activeJoint.lowAngularXLimit = new SoftJointLimit { limit = -angularLimit.limit };
            activeJoint.highAngularXLimit = angularLimit;
            activeJoint.angularYLimit = angularLimit;
            activeJoint.angularZLimit = angularLimit;
            activeJoint.angularXLimitSpring = new SoftJointLimitSpring { spring = 35f, damper = 8f };
            activeJoint.angularYZLimitSpring = new SoftJointLimitSpring { spring = 35f, damper = 8f };
            activeJoint.breakForce = jointBreakForceNewtons;
            activeJoint.breakTorque = jointBreakTorqueNewtonMeters;
            activeJoint.enableCollision = false;
            activeJoint.enablePreprocessing = false;
            activeJoint.projectionMode = JointProjectionMode.PositionAndRotation;
            activeJoint.projectionDistance = 0.02f;
            activeJoint.projectionAngle = 5f;
            ConfigureJointMassScaling(activeJoint, ownerBody, payload.Body);
            payload.Body.interpolation = RigidbodyInterpolation.Interpolate;
            payload.Body.solverIterations = Mathf.Max(payload.Body.solverIterations, 12);
            payload.Body.solverVelocityIterations = Mathf.Max(payload.Body.solverVelocityIterations, 8);

            var relay = ownerBody.GetComponent<DronePayloadJointBreakRelay>()
                        ?? ownerBody.gameObject.AddComponent<DronePayloadJointBreakRelay>();
            relay.Configure(this);
            AttachedPayload = payload;
            supportedMassKilograms = 0f;
            LastReleaseReason = PayloadReleaseReason.None;
            PayloadChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 释放当前载荷并保留载荷独立刚体、世界位姿和碰撞。
        /// </summary>
        /// <param name="reason">供 HUD 与遥测记录的释放原因。</param>
        internal void Release(PayloadReleaseReason reason = PayloadReleaseReason.Manual)
        {
            if (activeJoint != null)
            {
                Destroy(activeJoint);
            }

            activeJoint = null;
            AttachedPayload = null;
            supportedMassKilograms = 0f;
            LastReleaseReason = reason;
            PayloadChanged?.Invoke();
        }

        /// <summary>
        /// 由场景装配或测试设置抓斗刚体、挂点和承载限制。
        /// </summary>
        /// <param name="point">抓斗中心挂点。</param>
        /// <param name="maximumMass">允许抓取的最大质量，单位 kg。</param>
        /// <param name="owner">弱约束所在的抓斗刚体；为空时使用同物体 Rigidbody。</param>
        internal void Configure(Transform point, float maximumMass, Rigidbody owner = null)
        {
            mountPoint = point;
            maximumPayloadMassKilograms = maximumMass;
            gripBody = owner;
        }

        /// <summary>绑定单一 DroneFlight 配置，使 Play Mode 门禁与弱约束参数立即跟随调校。</summary>
        internal void Configure(Transform point, DroneFlightConfig flightConfig, Rigidbody owner = null)
        {
            mountPoint = point;
            config = flightConfig;
            gripBody = owner;
            RefreshConfigValues();
        }

        internal void NotifyJointBreak()
        {
            if (activeJoint == null && AttachedPayload == null)
            {
                return;
            }

            activeJoint = null;
            AttachedPayload = null;
            supportedMassKilograms = 0f;
            LastReleaseReason = PayloadReleaseReason.JointBreak;
            PayloadChanged?.Invoke();
        }

        private Rigidbody ResolveGripBody()
        {
            return gripBody != null ? gripBody : GetComponent<Rigidbody>();
        }

        private void FixedUpdate()
        {
            if (!HasPayload)
            {
                supportedMassKilograms = 0f;
                return;
            }

            UpdateSupportedMassEstimate(activeJoint.currentForce, Physics.gravity, Time.fixedDeltaTime);
        }

        /// <summary>
        /// 根据弱约束沿重力方向的真实受力更新当前承载质量。
        /// </summary>
        /// <param name="jointForce">PhysX 上一个物理步报告的约束力。</param>
        /// <param name="gravity">当前世界重力向量。</param>
        /// <param name="deltaTime">本次物理步时长。</param>
        internal void UpdateSupportedMassEstimate(Vector3 jointForce, Vector3 gravity, float deltaTime)
        {
            var attachedMass = AttachedMassKilograms;
            if (attachedMass <= 0f || !float.IsFinite(deltaTime) || deltaTime <= 0f)
            {
                supportedMassKilograms = 0f;
                return;
            }

            var gravityMagnitude = gravity.magnitude;
            var forceMagnitude = jointForce.magnitude;
            if (!float.IsFinite(gravityMagnitude) || gravityMagnitude <= 0.0001f
                || !float.IsFinite(forceMagnitude))
            {
                return;
            }

            var gravityDirection = gravity / gravityMagnitude;
            var supportedForce = Mathf.Abs(Vector3.Dot(jointForce, gravityDirection));
            var targetMass = Mathf.Clamp(supportedForce / gravityMagnitude, 0f, attachedMass);
            var blend = 1f - Mathf.Exp(-SupportBlendFrequency * deltaTime);
            supportedMassKilograms = Mathf.Lerp(supportedMassKilograms, targetMass, blend);
        }

        private static void ConfigureJointMassScaling(Joint joint, Rigidbody body, Rigidbody connectedBody)
        {
            if (joint == null || body == null || connectedBody == null
                || body.mass <= 0f || connectedBody.mass <= 0f)
            {
                return;
            }

            joint.massScale = Mathf.Clamp(body.mass / connectedBody.mass, 0.05f, 1f);
            joint.connectedMassScale = Mathf.Clamp(connectedBody.mass / body.mass, 0.05f, 1f);
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
            angularFreedomDegrees = config.GrappleAngularFreedomDegrees;
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
