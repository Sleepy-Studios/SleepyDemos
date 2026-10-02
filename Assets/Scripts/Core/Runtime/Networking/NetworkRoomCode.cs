using System;
using System.Globalization;
using System.Security.Cryptography;

namespace Core.Runtime.Networking
{
    /// 房间码中的协议版本、区域和随机标识；平台不是房间隔离条件。
    public readonly struct NetworkRoomCode : IEquatable<NetworkRoomCode>
    {
        internal NetworkRoomCode(ushort protocolVersion, string region, string token)
        {
            ProtocolVersion = protocolVersion;
            Region = region;
            Token = token;
        }

        /// 网络协议版本。
        public ushort ProtocolVersion { get; }
        /// 小写区域标识。
        public string Region { get; }
        /// 八位大写随机标识。
        public string Token { get; }
        /// 是否为解析或生成得到的有效房间码。
        public bool IsValid => ProtocolVersion > 0 && Region != null && Token != null;
        /// 规范房间码，同时作为传输层会话名称。
        public string SessionName => IsValid ? $"V{ProtocolVersion}-{Region}-{Token}" : string.Empty;

        /// 返回规范房间码。
        public override string ToString() => SessionName;

        /// <summary>比较规范房间码。</summary>
        /// <param name="other">待比较的房间码。</param>
        public bool Equals(NetworkRoomCode other) => ProtocolVersion == other.ProtocolVersion &&
            string.Equals(Region, other.Region, StringComparison.Ordinal) &&
            string.Equals(Token, other.Token, StringComparison.Ordinal);

        /// <summary>比较另一个对象是否表示相同房间码。</summary>
        /// <param name="obj">待比较对象。</param>
        public override bool Equals(object obj) => obj is NetworkRoomCode other && Equals(other);
        /// 返回规范码的哈希。
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(SessionName);
    }

    /// 不依赖联网 SDK 的房间码编码；随机碰撞由传输层创建房间时检测并重试。
    public static class NetworkRoomCodeCodec
    {
        private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        private const int TokenLength = 8;

        /// <summary>生成包含协议和区域的随机房间码，不创建真实房间。</summary>
        /// <param name="protocolVersion">非零协议版本。</param>
        /// <param name="region">二到八位 ASCII 字母或数字的区域，第一位必须是字母。</param>
        public static NetworkRoomCode Generate(ushort protocolVersion, string region)
        {
            var bytes = new byte[TokenLength];
            using (var random = RandomNumberGenerator.Create()) random.GetBytes(bytes);
            var token = new char[TokenLength];
            for (int i = 0; i < token.Length; i++) token[i] = Alphabet[bytes[i] & 31];
            return Encode(protocolVersion, region, new string(token));
        }

        /// <summary>校验并规范化房间码组成部分。</summary>
        /// <param name="protocolVersion">非零协议版本。</param>
        /// <param name="region">区域标识；规范化为小写，不推断 SDK 区域可用性。</param>
        /// <param name="token">八位字母或数字标识，不允许 I、O、0、1。</param>
        public static NetworkRoomCode Encode(ushort protocolVersion, string region, string token)
        {
            if (protocolVersion == 0) throw new ArgumentOutOfRangeException(nameof(protocolVersion));
            string normalizedRegion = region?.ToLowerInvariant();
            string normalizedToken = token?.ToUpperInvariant();
            if (!IsRegion(normalizedRegion)) throw new ArgumentException("区域标识无效。", nameof(region));
            if (!IsToken(normalizedToken)) throw new ArgumentException("房间随机标识无效。", nameof(token));
            return new NetworkRoomCode(protocolVersion, normalizedRegion, normalizedToken);
        }

        /// <summary>解析房间码；仅接受大小写和首尾空白差异。</summary>
        /// <param name="value">V版本-区域-随机标识形式的房间码。</param>
        /// <param name="code">成功时返回规范房间码，失败时为默认无效值。</param>
        /// <returns>是否符合房间码格式；不代表房间存在。</returns>
        public static bool TryParse(string value, out NetworkRoomCode code)
        {
            code = default;
            if (string.IsNullOrWhiteSpace(value)) return false;
            string[] parts = value.Trim().Split('-');
            if (parts.Length != 3 || parts[0].Length < 2 || char.ToUpperInvariant(parts[0][0]) != 'V') return false;
            string version = parts[0].Substring(1);
            if (version[0] == '0' || !ushort.TryParse(version, NumberStyles.None,
                    CultureInfo.InvariantCulture, out ushort number) || number == 0) return false;
            string region = parts[1].ToLowerInvariant();
            string token = parts[2].ToUpperInvariant();
            if (!IsRegion(region) || !IsToken(token)) return false;
            code = new NetworkRoomCode(number, region, token);
            return true;
        }

        private static bool IsRegion(string region)
        {
            if (region == null || region.Length < 2 || region.Length > 8 ||
                region[0] < 'a' || region[0] > 'z') return false;
            foreach (char c in region) if ((c < 'a' || c > 'z') && (c < '0' || c > '9')) return false;
            return true;
        }

        private static bool IsToken(string token)
        {
            if (token == null || token.Length != TokenLength) return false;
            foreach (char c in token) if (Alphabet.IndexOf(c) < 0) return false;
            return true;
        }
    }
}
