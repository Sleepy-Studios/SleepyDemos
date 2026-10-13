using FishNet.Managing;
using FishNet.Object;
using UnityEngine;

namespace Core.Runtime.Networking
{
    /// 正式公共宿主 Prefab 上的 AOT 配置；不携带任何 Demo 规则。
    [RequireComponent(typeof(NetworkManager))]
    public sealed class FishNetSessionRuntime : MonoBehaviour
    {
        [SerializeField] private NetworkObject predictionPrefab;

        /// 已纳入宿主 SpawnablePrefabs 的正式预测载体。
        public NetworkObject PredictionPrefab => predictionPrefab;
        internal FishNetNetworkSessionService Service { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAdapter()
        {
            NetworkSessionServices.RegisterInternetFactory(settings => new FishNetNetworkSessionService(settings));
        }
    }
}
