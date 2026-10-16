using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Core.Runtime.Platform.TapTap
{
    /// TapTap H5 浏览器持久化桥；值和快照共用原有业务存储入口。
    internal static class TapTapStorage
    {
        private static string gameId;

        internal static void Initialize(string releaseGameId)
        {
            if (string.IsNullOrWhiteSpace(releaseGameId))
            {
                throw new ArgumentException("H5 存储需要发布配置的 GameId。", nameof(releaseGameId));
            }
            gameId = releaseGameId;
        }

        internal static string LoadValue(string key) => Invoke(0, key).value;

        internal static void SaveValue(string key, string json) => Invoke(1, key, json);

        internal static string LoadFile(string path) => Invoke(2, path).value;

        internal static void SaveFile(string path, string json) => Invoke(3, path, json);

        internal static DateTime GetFileWriteTimeUtc(string path)
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(Invoke(4, path).modifiedMilliseconds).UtcDateTime;
            }
            catch (FileNotFoundException)
            {
                return DateTime.FromFileTimeUtc(0);
            }
        }

        internal static string ToRelativePath(string path, string persistentRoot)
        {
            string root = Path.GetFullPath(persistentRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root, Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                throw new ArgumentException("TapTap 存档只能写入当前游戏的持久目录。", nameof(path));
            }
            string relative = fullPath.Substring(root.Length).Replace('\\', '/');
            if (relative.IndexOf(':') >= 0 || relative.IndexOf('\0') >= 0)
            {
                throw new ArgumentException("TapTap 存档路径无效。", nameof(path));
            }
            return relative;
        }

        private static Result Invoke(int operation, string path, string value = "")
        {
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
            var result = JsonUtility.FromJson<Result>(SleepyTapTap_Storage(operation, gameId, path, value));
            if (result == null || !result.ok)
            {
                if (result?.notFound == true)
                {
                    throw new FileNotFoundException(result.error);
                }
                throw new IOException(result?.error ?? "TapTap 存储没有返回结果。");
            }
            return result;
#else
            throw new PlatformNotSupportedException("当前平台未启用 TapTap 存储。");
#endif
        }

        [Serializable]
        private sealed class Result
        {
            public bool ok;
            public string value;
            public string error;
            public bool notFound;
            public long modifiedMilliseconds;
        }

#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string SleepyTapTap_Storage(int operation, string gameId, string path, string value);
#endif
    }
}
