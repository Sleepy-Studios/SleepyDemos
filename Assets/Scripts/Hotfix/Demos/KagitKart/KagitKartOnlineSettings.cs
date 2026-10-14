using System;
using System.IO;
using UnityEngine;

namespace Hotfix.KagitKart
{
    /// 仅开发配置连接环境，玩家始终使用联机码。
    [Serializable]
    public sealed class KagitKartOnlineSettings
    {
        public const int ProtocolVersion = 2;
        public const string ContentVersion = "kagit_server_v1";
        public string directoryUrl = "http://127.0.0.1:18080";

        public static KagitKartOnlineSettings Load()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "ServerConnection.json");
            return File.Exists(path) ? JsonUtility.FromJson<KagitKartOnlineSettings>(File.ReadAllText(path)) : new KagitKartOnlineSettings();
        }
    }
}
