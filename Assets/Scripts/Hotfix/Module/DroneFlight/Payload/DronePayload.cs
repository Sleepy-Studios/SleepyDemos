using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>完整场景复位使用的载荷初始快照。</summary>
    internal readonly struct DronePayloadSnapshot
    {
        internal DronePayloadSnapshot(DronePayload payload)
        {
            Payload = payload;
            ActiveSelf = payload != null && payload.gameObject.activeSelf;
            Position = payload != null ? payload.transform.position : Vector3.zero;
            Rotation = payload != null ? payload.transform.rotation : Quaternion.identity;
            IsKinematic = payload != null && payload.Body.isKinematic;
            UseGravity = payload != null && payload.Body.useGravity;
        }

        internal DronePayload Payload { get; }
        internal bool ActiveSelf { get; }
        internal Vector3 Position { get; }
        internal Quaternion Rotation { get; }
        internal bool IsKinematic { get; }
        internal bool UseGravity { get; }

        internal void Restore()
        {
            if (Payload == null)
            {
                return;
            }

            var body = Payload.Body;
            Payload.gameObject.SetActive(true);
            body.position = Position;
            body.rotation = Rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.isKinematic = IsKinematic;
            body.useGravity = UseGravity;
            Payload.gameObject.SetActive(ActiveSelf);
        }
    }

    /// <summary>
    /// 可由无人机挂载的独立刚体载荷。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DronePayload : MonoBehaviour
    {
        private const int SupportConfirmationSteps = 3;
        private const float MinimumUpwardSupportDot = 0.55f;

        [SerializeField] private string payloadType = "Generic";
        [SerializeField] private Transform connectionPoint;

        private Rigidbody cachedBody;
        private readonly HashSet<Collider> ignoredSupportColliders = new();
        private readonly HashSet<Collider> currentSupportingColliders = new();
        private int consecutiveSupportedSteps;
        private int consecutiveUnsupportedSteps;
        private float accumulatedUpwardSupportImpulse;

        private void Awake()
        {
            cachedBody = GetComponent<Rigidbody>();
            cachedBody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        /// <summary>载荷刚体。</summary>
        internal Rigidbody Body => cachedBody != null ? cachedBody : cachedBody = GetComponent<Rigidbody>();

        /// <summary>玩法类型标识。</summary>
        internal string PayloadType => payloadType;

        /// <summary>载荷自身连接点。</summary>
        internal Transform ConnectionPoint => connectionPoint != null ? connectionPoint : transform;

        /// 当前是否已确认由场景地面或其它外部物体向上支撑。
        internal bool IsGroundSupported { get; private set; }

        /// 是否已通过连续三个物理步确认“有支撑”或“无支撑”。
        internal bool IsSupportStateConfirmed { get; private set; }

        /// 最近一个已完成物理步中的有效外部支撑 Collider 数量。
        internal int EffectiveSupportContactCount { get; private set; }

        /// 最近一个已完成物理步中的外部竖直支持力，单位 N。
        internal float LastUpwardSupportForceNewtons { get; private set; }

        /// <summary>
        /// 运行时装配载荷类型和连接点。
        /// </summary>
        internal void Configure(string type, Transform point = null)
        {
            payloadType = string.IsNullOrWhiteSpace(type) ? "Generic" : type;
            connectionPoint = point;
        }

        /// <summary>
        /// 配置不得作为地面支撑来源的无人机与抓斗机构 Collider。
        /// </summary>
        /// <param name="colliders">机体、吊杆、抓斗底座和爪体的 Collider。</param>
        internal void ConfigureIgnoredSupportColliders(IEnumerable<Collider> colliders)
        {
            ignoredSupportColliders.Clear();
            if (colliders != null)
            {
                foreach (var collider in colliders)
                {
                    if (collider != null)
                    {
                        ignoredSupportColliders.Add(collider);
                    }
                }
            }

            currentSupportingColliders.ExceptWith(ignoredSupportColliders);
        }

        /// 清除抓斗释放时临时配置的机构 Collider 忽略列表。
        internal void ClearIgnoredSupportColliders()
        {
            ignoredSupportColliders.Clear();
        }

        internal void ReportSupportContact(Collider otherCollider, Vector3 normal, float upwardImpulse = 0f)
        {
            if (otherCollider == null || ignoredSupportColliders.Contains(otherCollider))
            {
                return;
            }

            var gravity = Physics.gravity;
            if (gravity.sqrMagnitude <= 0.000001f || normal.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            if (Vector3.Dot(normal.normalized, -gravity.normalized) >= MinimumUpwardSupportDot)
            {
                currentSupportingColliders.Add(otherCollider);
                if (float.IsFinite(upwardImpulse) && upwardImpulse > 0f)
                {
                    accumulatedUpwardSupportImpulse += upwardImpulse;
                }
            }
        }

        internal void CompleteSupportPhysicsStep()
        {
            EffectiveSupportContactCount = currentSupportingColliders.Count;
            LastUpwardSupportForceNewtons = Time.fixedDeltaTime > 0f
                ? accumulatedUpwardSupportImpulse / Time.fixedDeltaTime
                : 0f;
            StepSupportState(EffectiveSupportContactCount > 0);
            currentSupportingColliders.Clear();
            accumulatedUpwardSupportImpulse = 0f;
        }

        internal void StepSupportState(bool hasExternalUpwardSupport)
        {
            if (hasExternalUpwardSupport)
            {
                consecutiveSupportedSteps++;
                consecutiveUnsupportedSteps = 0;
                if (consecutiveSupportedSteps >= SupportConfirmationSteps)
                {
                    IsGroundSupported = true;
                    IsSupportStateConfirmed = true;
                }

                return;
            }

            consecutiveSupportedSteps = 0;
            consecutiveUnsupportedSteps++;
            if (consecutiveUnsupportedSteps >= SupportConfirmationSteps)
            {
                IsGroundSupported = false;
                IsSupportStateConfirmed = true;
            }
        }

        private void FixedUpdate()
        {
            CompleteSupportPhysicsStep();
        }

        private void OnCollisionStay(Collision collision)
        {
            var upwardImpulse = Mathf.Max(
                0f,
                Vector3.Dot(collision.impulse, -Physics.gravity.normalized));
            var remainingImpulse = upwardImpulse;
            for (var index = 0; index < collision.contactCount; index++)
            {
                var contact = collision.GetContact(index);
                var contactImpulse = collision.contactCount > 0
                    ? remainingImpulse / collision.contactCount
                    : 0f;
                ReportSupportContact(contact.otherCollider, contact.normal, contactImpulse);
            }
        }

        private void OnCollisionExit(Collision collision)
        {
            if (collision.collider != null)
            {
                currentSupportingColliders.Remove(collision.collider);
            }
        }

        private void OnDisable()
        {
            currentSupportingColliders.Clear();
            EffectiveSupportContactCount = 0;
            LastUpwardSupportForceNewtons = 0f;
            accumulatedUpwardSupportImpulse = 0f;
            consecutiveSupportedSteps = 0;
            consecutiveUnsupportedSteps = 0;
            IsGroundSupported = false;
            IsSupportStateConfirmed = false;
        }

        /// 捕获当前载荷初始状态。
        internal DronePayloadSnapshot CaptureSnapshot()
        {
            return new DronePayloadSnapshot(this);
        }
    }
}
