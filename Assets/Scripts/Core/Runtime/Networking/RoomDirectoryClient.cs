using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Core.Runtime.Networking
{
    /// 房间目录返回的短期连接授权；身份凭据只保留在当前进程内存。
    [Serializable]
    public sealed class RoomJoinGrant
    {
        public string roomId, code, address, memberId, ticket, resumeToken, contentVersion;
        public int port, protocolVersion;
    }

    /// HTTP 目录负责找房，比赛数据继续走独立 UDP 会话。
    public sealed class RoomDirectoryClient
    {
        private readonly string baseUrl;

        /// <summary>绑定开发配置中的房间服务，不向玩家暴露连接地址。</summary>
        /// <param name="url">HTTP/HTTPS 基地址，不包含业务路径。</param>
        public RoomDirectoryClient(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            {
                throw new ArgumentException("房间服务地址无效。", nameof(url));
            }
            baseUrl = url.TrimEnd('/');
        }

        /// <summary>提交创建请求并等待服务器准备完成。</summary>
        /// <param name="json">包含配置、显示名、版本及幂等请求 ID 的 JSON。</param>
        /// <param name="token">取消当前请求，不取消服务器的清理过程。</param>
        public UniTask<RoomJoinGrant> CreateAsync(string json, CancellationToken token) => RequestAsync("/v1/rooms", json, token);

        /// <summary>用六位数字码换取一次性入房凭据。</summary>
        /// <param name="code">完整六位码，保留前导零。</param>
        /// <param name="json">显示名与兼容版本。</param>
        /// <param name="token">当前场景请求令牌。</param>
        public UniTask<RoomJoinGrant> JoinAsync(string code, string json, CancellationToken token)
        {
            if (code == null || code.Length != 6)
            {
                throw new ArgumentException("请输入6位数字联机码。", nameof(code));
            }
            foreach (char value in code)
            {
                if (value < '0' || value > '9') throw new ArgumentException("请输入6位数字联机码。", nameof(code));
            }
            return RequestAsync("/v1/rooms/" + code + "/join", json, token);
        }

        /// <summary>使用原会话秘密申请恢复身份，不使用公开联机码认领车手。</summary>
        /// <param name="json">重连凭据与兼容版本。</param>
        /// <param name="token">当前重连操作令牌。</param>
        public UniTask<RoomJoinGrant> ResumeAsync(string json, CancellationToken token) => RequestAsync("/v1/sessions/resume", json, token);

        /// <summary>主动离开时撤销原身份的重连资格，包括 UDP 已断开的情形。</summary>
        /// <param name="json">原重连凭据和兼容版本。</param>
        /// <param name="token">只限制 HTTP 请求等待，不回滚服务器撤销。</param>
        public async UniTask LeaveAsync(string json, CancellationToken token)
        {
            await RequestTextAsync("/v1/sessions/leave", json, token);
        }

        private async UniTask<RoomJoinGrant> RequestAsync(string path, string json, CancellationToken token)
        {
            var grant = JsonUtility.FromJson<RoomJoinGrant>(await RequestTextAsync(path, json, token));
            if (grant == null || string.IsNullOrWhiteSpace(grant.ticket) || string.IsNullOrWhiteSpace(grant.memberId)
                || string.IsNullOrWhiteSpace(grant.address) || grant.port is < 1 or > 65535 || grant.code?.Length != 6)
            {
                throw new InvalidOperationException("房间服务返回的连接信息无效。");
            }
            return grant;
        }

        private async UniTask<string> RequestTextAsync(string path, string json, CancellationToken token)
        {
            using var request = new UnityWebRequest(baseUrl + path, "POST");
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 100;
            try
            {
                await request.SendWebRequest().ToUniTask(cancellationToken: token);
            }
            catch (UnityWebRequestException)
            {
                var error = JsonUtility.FromJson<DirectoryError>(request.downloadHandler.text.StartsWith("{") ? request.downloadHandler.text : "{}");
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(error?.message) ? "房间服务暂时不可用，请稍后重试。" : error.message);
            }
            return request.downloadHandler.text;
        }

        [Serializable]
        private sealed class DirectoryError
        {
            public string code, message;
        }
    }
}
