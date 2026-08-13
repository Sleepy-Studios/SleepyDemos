using UnityEngine;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// 可由无人机挂载的独立刚体载荷。
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class DronePayload : MonoBehaviour
    {
        [SerializeField] private string payloadType = "Generic";
        [SerializeField] private Transform connectionPoint;

        private Rigidbody cachedBody;

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
    }
}
