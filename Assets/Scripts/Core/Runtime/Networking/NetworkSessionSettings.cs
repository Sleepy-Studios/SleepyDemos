using System;
using UnityEngine;

namespace Core.Runtime.Networking
{
    [CreateAssetMenu(fileName = "SessionSettings", menuName = "Sleepy/Network Session Settings")]
    public sealed class NetworkSessionSettings : ScriptableObject
    {
        [SerializeField, Tooltip("Photon Fusion 应用 ID；不是账号密码或管理密钥。")]
        private string fusionAppId = string.Empty;
        [SerializeField] private string defaultRegion = "hk";
        [SerializeField] private string contentVersion = "jinx_p0_1";
        [SerializeField] private int protocolVersion = 1;
        [SerializeField, Range(1, 6)] private int maxPlayers = 6;

        /// Photon 客户端公开连接标识。
        public string FusionAppId => fusionAppId;
        /// 创建房间默认区域，加入时使用房间码中的区域。
        public string DefaultRegion => defaultRegion;
        /// 所有互通平台使用同一个内容兼容版本。
        public string ContentVersion => contentVersion;
        /// 网络协议版本，不随平台改变。
        public ushort ProtocolVersion => checked((ushort)protocolVersion);
        /// 房间最大人数。
        public int MaxPlayers => maxPlayers;
        /// 已配置合法格式的 App ID；不能代表 SDK 可用或互联网连接成功。
        public bool HasAppId => Guid.TryParse(fusionAppId, out _);
    }

    /// SDK 适配的唯一创建入口；未安装 SDK 时明确拒绝联网，离线验证必须显式选择。
    public static class NetworkSessionServices
    {
        private static Func<NetworkSessionSettings, INetworkSessionService> internetFactory;
        /// 当前进程是否已注册可用的互联网 SDK 适配。
        public static bool HasInternetAdapter => internetFactory != null;

        /// <summary>注册通用互联网传输，供 AOT SDK 启动适配调用。</summary>
        /// <param name="factory">独立会话工厂；null 清除注册，不影响已经创建的会话。</param>
        public static void RegisterInternetFactory(Func<NetworkSessionSettings, INetworkSessionService> factory)
        {
            internetFactory = factory;
        }

        /// <summary>创建独立互联网会话，不隐式使用离线替代。</summary>
        /// <param name="settings">平台共享的协议和 Photon 连接配置。</param>
        /// <returns>SDK 适配器；缺少配置或 SDK 时抛出明确异常。</returns>
        public static INetworkSessionService CreateInternet(NetworkSessionSettings settings)
        {
            if (settings == null || !settings.HasAppId)
                throw new InvalidOperationException("尚未配置 Photon Fusion App ID。请在 SessionSettings 中填写自己的 Fusion 2 应用 ID。");
            if (internetFactory == null)
                throw new InvalidOperationException("Photon Fusion SDK 尚未安装或 AOT 适配尚未注册。可以单独选择离线规则验证。");
            var service = internetFactory(settings);
            if (service == null || service.TransportKind != NetworkTransportKind.Internet)
            {
                service?.Dispose();
                throw new InvalidOperationException("互联网工厂必须返回真正的 Internet 会话。");
            }
            return service;
        }
    }
}
