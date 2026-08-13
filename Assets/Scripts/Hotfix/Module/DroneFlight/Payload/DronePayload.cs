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
        [SerializeField] private string payloadType = "Generic";
        [SerializeField] private Transform connectionPoint;

        private Rigidbody cachedBody;

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

        /// <summary>
        /// 运行时装配载荷类型和连接点。
        /// </summary>
        internal void Configure(string type, Transform point = null)
        {
            payloadType = string.IsNullOrWhiteSpace(type) ? "Generic" : type;
            connectionPoint = point;
        }

        /// 捕获当前载荷初始状态。
        internal DronePayloadSnapshot CaptureSnapshot()
        {
            return new DronePayloadSnapshot(this);
        }
    }
}
