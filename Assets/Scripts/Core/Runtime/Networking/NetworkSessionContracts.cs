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
        /// 包含协议和区域的房间码。
        public string Code { get; set; }
        /// 当前规则与资源内容版本。
        public string ContentVersion { get; set; }
        /// 本地玩家显示名。
        public string DisplayName { get; set; }
    }

    /// 成功加入后的不可变会话信息。
    public sealed class NetworkSessionInfo
    {
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

    /// 权威更换事件；同一会话每次交接递增 Epoch，空 MemberId 表示没有权威。
    public readonly struct NetworkAuthorityInfo
    {
        /// <summary>记录当前权威和交接代次。</summary>
        /// <param name="memberId">权威成员；离房或无权威时为空。</param>
        /// <param name="epoch">同一会话内递增；离房时可以为零。</param>
        public NetworkAuthorityInfo(string memberId, ulong epoch) { MemberId = memberId; Epoch = epoch; }
        /// 当前权威成员。
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
