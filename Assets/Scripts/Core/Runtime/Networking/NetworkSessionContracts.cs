using System;
using UnityEngine;

namespace Core.Runtime.Networking
{
    /// 会话传输性质；Offline 只表示本机验证，不能作为互联网连接成功的证据。
    public enum NetworkTransportKind { Offline, Internet }
    /// 会话生命周期，不表示具体 SDK 的连接阶段。
    public enum NetworkSessionState { Idle, Connecting, Joined, Leaving, Faulted, Disposed }

    /// 创建房间的稳定输入。
    public sealed class NetworkCreateOptions
    {
        /// Host 对外公布的地址；传输默认监听本机可用接口。
        public string Address { get; set; } = "127.0.0.1";
        /// 直连 UDP 端口。
        public ushort Port { get; set; } = 7777;
        /// 目标区域。
        public string Region { get; set; }
        /// 网络协议版本，零无效。
        public ushort ProtocolVersion { get; set; }
        /// 规则与资源内容版本；互联网适配器必须在入房前核对完全相等。
        public string ContentVersion { get; set; }
        /// 房间容量；具体传输层仍须检查 SDK 限制。
        public int MaxPlayers { get; set; }
        /// 本地玩家显示名，不作为身份或授权凭据。
        public string LocalDisplayName { get; set; }
    }

    /// 加入房间的稳定输入。
    public sealed class NetworkJoinOptions
    {
        /// Host 的 IP 或域名，不依赖房间码服务。
        public string Address { get; set; }
        /// Host 的 UDP 端口。
        public ushort Port { get; set; } = 7777;
        /// 协议版本必须与 Host 完全一致。
        public ushort ProtocolVersion { get; set; } = 1;
        /// 包含协议和区域的房间码。
        public string Code { get; set; }
        /// 当前规则与资源内容版本。
        public string ContentVersion { get; set; }
        /// 本地玩家显示名。
        public string DisplayName { get; set; }
        /// 交给服务器业务验签的入房凭据；客户端不能直接指定可信成员身份。
        public string Ticket { get; set; }
    }

    /// 仅启动服务器的输入；不会连接本地客户端或创建本地玩家。
    public sealed class NetworkServerOptions
    {
        /// 对外公布的地址；传输监听本机可用接口。
        public string Address { get; set; } = "127.0.0.1";
        /// 服务器 UDP 端口。
        public ushort Port { get; set; } = 7777;
        /// 协议版本；零使用服务配置。
        public ushort ProtocolVersion { get; set; }
        /// 内容版本；空值使用服务配置。
        public string ContentVersion { get; set; }
        /// 已认证玩家上限，范围为一至八。
        public int MaxPlayers { get; set; } = 8;
        /// 独立服务器身份；空值生成唯一身份，不占成员名额。
        public string ServerId { get; set; }
        /// 可选的正式宿主 Prefab；传入后直接实例化，无需资源服务初始化。
        public GameObject HostPrefab { get; set; }
    }

    /// 服务器收到的认证请求；连接身份由传输层填写。
    public readonly struct NetworkAuthenticationRequest
    {
        /// <summary>由服务器传输适配构造认证请求；业务调用不能改变已绑定连接的身份。</summary>
        /// <param name="connectionId">传输适配从真实连接读取的临时编号。</param>
        /// <param name="ticket">客户端提交、待业务验证的凭据。</param>
        /// <param name="displayName">客户端请求的显示名，尚未被信任。</param>
        /// <param name="protocolVersion">客户端握手声明的协议版本。</param>
        /// <param name="contentVersion">客户端握手声明的规则与资源版本。</param>
        public NetworkAuthenticationRequest(int connectionId, string ticket, string displayName,
            ushort protocolVersion, string contentVersion)
        {
            ConnectionId = connectionId;
            Ticket = ticket;
            DisplayName = displayName;
            ProtocolVersion = protocolVersion;
            ContentVersion = contentVersion;
        }

        /// 当前真实连接编号，仅用于此次连接，不能作为稳定玩家身份。
        public int ConnectionId { get; }
        /// 客户端提交的待验证凭据。
        public string Ticket { get; }
        /// 客户端请求的显示名；尚未被信任。
        public string DisplayName { get; }
        /// 客户端协议版本。
        public ushort ProtocolVersion { get; }
        /// 客户端内容版本。
        public string ContentVersion { get; }
    }

    /// 业务验证凭据后授予的稳定身份，Core 不解释票据格式。
    public readonly struct NetworkAuthenticationResult
    {
        /// <summary>接受后在登记成员和生成预测载体前绑定身份；默认拒绝重复在线身份。</summary>
        /// <param name="accepted">是否允许此次连接。</param>
        /// <param name="memberId">接受时必填的可信稳定身份，最多 128 字符。</param>
        /// <param name="displayName">服务器确认的显示名，空值为空字符串，最多 32 字符。</param>
        /// <param name="reason">拒绝时向客户端显示的原因。</param>
        /// <param name="replaceExistingConnection">认证方明确授权重连时才设为 true；旧连接会被踢出。</param>
        public NetworkAuthenticationResult(bool accepted, string memberId = null, string displayName = null,
            string reason = null, bool replaceExistingConnection = false)
        {
            Accepted = accepted;
            MemberId = memberId;
            DisplayName = displayName;
            Reason = reason;
            ReplaceExistingConnection = replaceExistingConnection;
        }

        /// 是否通过服务器业务认证。
        public bool Accepted { get; }
        /// 验证票据后确认的稳定成员身份。
        public string MemberId { get; }
        /// 服务器确认的显示名。
        public string DisplayName { get; }
        /// 认证拒绝原因。
        public string Reason { get; }
        /// 是否明确允许替换该身份仍在线的旧连接。
        public bool ReplaceExistingConnection { get; }
    }

    /// 成功加入后的不可变会话信息。
    public sealed class NetworkSessionInfo
    {
        /// <summary>记录实际直连会话；不生成需要外部目录服务的随机码。</summary>
        /// <param name="address">连接地址。</param>
        /// <param name="port">UDP 端口。</param>
        /// <param name="contentVersion">共享内容版本。</param>
        /// <param name="maxPlayers">实际容量。</param>
        public NetworkSessionInfo(string address, ushort port, string contentVersion, int maxPlayers)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("地址不能为空。", nameof(address));
            if (port == 0) throw new ArgumentOutOfRangeException(nameof(port));
            if (string.IsNullOrWhiteSpace(contentVersion)) throw new ArgumentException("内容版本不能为空。", nameof(contentVersion));
            if (maxPlayers < 1) throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            Address = address;
            Port = port;
            ContentVersion = contentVersion;
            MaxPlayers = maxPlayers;
            TransportKind = NetworkTransportKind.Internet;
        }

        /// <summary>记录实际加入的会话信息。</summary>
        /// <param name="code">有效规范房间码。</param>
        /// <param name="contentVersion">非空的规则与资源内容版本。</param>
        /// <param name="maxPlayers">实际房间容量，至少一人。</param>
        /// <param name="transportKind">实际传输性质，离线会话必须标示 Offline。</param>
        public NetworkSessionInfo(NetworkRoomCode code, string contentVersion, int maxPlayers,
            NetworkTransportKind transportKind)
        {
            if (!code.IsValid) throw new ArgumentException("房间码无效。", nameof(code));
            if (string.IsNullOrWhiteSpace(contentVersion)) throw new ArgumentException("内容版本不能为空。", nameof(contentVersion));
            if (maxPlayers < 1) throw new ArgumentOutOfRangeException(nameof(maxPlayers));
            Code = code;
            ContentVersion = contentVersion;
            MaxPlayers = maxPlayers;
            TransportKind = transportKind;
        }

        /// 房间码。
        public NetworkRoomCode Code { get; }
        /// 直连地址；旧离线验证没有网络地址。
        public string Address { get; }
        /// 直连端口；离线验证为零。
        public ushort Port { get; }
        /// 规则与资源内容版本。
        public string ContentVersion { get; }
        /// 容量上限。
        public int MaxPlayers { get; }
        /// 是否为互联网或离线验证会话。
        public NetworkTransportKind TransportKind { get; }
    }

    /// 传输层确认的房间成员，不依赖 SDK PlayerRef 类型。
    public readonly struct NetworkMemberInfo
    {
        /// <summary>记录传输层确认的成员。</summary>
        /// <param name="memberId">会话内唯一、非空的可信身份。</param>
        /// <param name="displayName">用于显示的名称，空值转为空字符串。</param>
        /// <param name="isLocal">该成员是否由本地客户端控制。</param>
        public NetworkMemberInfo(string memberId, string displayName, bool isLocal)
        {
            if (string.IsNullOrWhiteSpace(memberId)) throw new ArgumentException("成员标识不能为空。", nameof(memberId));
            MemberId = memberId;
            DisplayName = displayName ?? string.Empty;
            IsLocal = isLocal;
        }
        /// 当前会话内的可信成员标识。
        public string MemberId { get; }
        /// 显示名，不用于权限判断。
        public string DisplayName { get; }
        /// 是否为本地成员。
        public bool IsLocal { get; }
    }

    /// 权威更换事件；独立服务器不占成员名额，空 MemberId 表示没有权威。
    public readonly struct NetworkAuthorityInfo
    {
        /// <summary>记录当前权威和交接代次。</summary>
        /// <param name="memberId">权威玩家或服务器身份；离房或无权威时为空。</param>
        /// <param name="epoch">同一会话内递增；离房时可以为零。</param>
        public NetworkAuthorityInfo(string memberId, ulong epoch) { MemberId = memberId; Epoch = epoch; }
        /// 当前权威玩家或独立服务器身份。
        public string MemberId { get; }
        /// 权威代次。
        public ulong Epoch { get; }
    }

    /// 本地角色姿态；不存在 Camera 或 AudioListener 所有权。
    public readonly struct NetworkAvatarPose
    {
        /// <summary>记录角色姿态，不持有场景相机。</summary>
        /// <param name="position">角色世界位置。</param>
        /// <param name="rotation">身体世界旋转。</param>
        /// <param name="headPitch">头部俯仰角，单位为度。</param>
        public NetworkAvatarPose(Vector3 position, Quaternion rotation, float headPitch)
        { Position = position; Rotation = rotation; HeadPitch = headPitch; }
        /// 世界坐标位置。
        public Vector3 Position { get; }
        /// 角色身体旋转。
        public Quaternion Rotation { get; }
        /// 头部俯仰角，单位为度。
        public float HeadPitch { get; }
    }

    /// 传输层填充成员身份后的角色姿态事件。
    public readonly struct NetworkAvatarPoseEvent
    {
        /// <summary>附加传输层确认的角色身份。</summary>
        /// <param name="memberId">可信发送成员身份。</param>
        /// <param name="pose">最新角色姿态。</param>
        public NetworkAvatarPoseEvent(string memberId, NetworkAvatarPose pose) { MemberId = memberId; Pose = pose; }
        /// 传输层确认的发送成员。
        public string MemberId { get; }
        /// 最新姿态。
        public NetworkAvatarPose Pose { get; }
    }
}
