using System;
using UnityEngine;

namespace Core.Runtime.Networking
{
    [CreateAssetMenu(fileName = "SessionSettings", menuName = "Sleepy/Network Session Settings")]
    public sealed class NetworkSessionSettings : ScriptableObject
    {
        [SerializeField] private string contentVersion = "sleepy_v1";
        [SerializeField] private int protocolVersion = 1;

        /// 所有互通平台使用同一个内容兼容版本。
        public string ContentVersion => contentVersion;
        /// 网络协议版本，不随平台改变。
        public ushort ProtocolVersion => checked((ushort)protocolVersion);
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
        /// <param name="settings">平台共享的协议和内容版本，不需要 App ID。</param>
        /// <returns>SDK 适配器；缺少配置或 SDK 时抛出明确异常。</returns>
        public static INetworkSessionService CreateInternet(NetworkSessionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (internetFactory == null)
                throw new InvalidOperationException("FishNet AOT 适配尚未注册，请通过正式启动入口运行。");
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
