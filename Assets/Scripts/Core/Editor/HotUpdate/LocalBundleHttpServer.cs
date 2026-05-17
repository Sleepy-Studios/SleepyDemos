using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Core.Editor.HotUpdate
{
    internal static class LocalBundleHttpServer
    {
        private static readonly object SyncRoot = new object();

        private static HttpListener listener;
        private static CancellationTokenSource cancellationTokenSource;
        private static string rootDirectory;
        private static int port;

        public static bool IsRunning => listener != null && listener.IsListening;
        public static int Port => port;

        public static bool IsPortAvailable(int listenPort)
        {
            if (listenPort <= 0 || listenPort > 65535)
            {
                return false;
            }

            return IPGlobalProperties.GetIPGlobalProperties()
                .GetActiveTcpListeners()
                .All(endPoint => endPoint.Port != listenPort);
        }

        public static int FindAvailablePort(int preferredPort, int maxAttempts = 100)
        {
            int startPort = Mathf.Clamp(preferredPort, 1024, 65535);
            int attempts = Math.Max(1, maxAttempts);
            for (int i = 0; i < attempts && startPort + i <= 65535; i++)
            {
                int candidatePort = startPort + i;
                if (IsPortAvailable(candidatePort))
                {
                    return candidatePort;
                }
            }

            throw new InvalidOperationException($"未能从端口 {startPort} 开始找到可用端口，请关闭占用服务后重试。");
        }

        public static void Start(string directory, int listenPort)
        {
            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException("模拟服务器目录为空，无法启动本地 HTTP 服务。");
            }

            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"模拟服务器目录不存在: {directory}");
            }

            if (listenPort <= 0 || listenPort > 65535)
            {
                throw new InvalidOperationException($"端口非法: {listenPort}");
            }

            lock (SyncRoot)
            {
                if (IsRunning)
                {
                    if (string.Equals(rootDirectory, Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase) && port == listenPort)
                    {
                        return;
                    }

                    Stop();
                }

                rootDirectory = ResolveServingRoot(Path.GetFullPath(directory));
                port = listenPort;
                cancellationTokenSource = new CancellationTokenSource();
                listener = new HttpListener();
                listener.Prefixes.Add($"http://*:{listenPort}/");
                listener.Start();
                _ = Task.Run(() => ListenLoopAsync(listener, cancellationTokenSource.Token));
            }
        }

        public static void Stop()
        {
            lock (SyncRoot)
            {
                try
                {
                    cancellationTokenSource?.Cancel();
                }
                catch
                {
                }

                try
                {
                    listener?.Stop();
                    listener?.Close();
                }
                catch
                {
                }

                cancellationTokenSource?.Dispose();
                cancellationTokenSource = null;
                listener = null;
                rootDirectory = null;
                port = 0;
            }
        }

        public static string GetLocalIPv4()
        {
            string fallback = "127.0.0.1";
            try
            {
                var hostEntry = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var address in hostEntry.AddressList)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
                    {
                        return address.ToString();
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"获取局域网 IP 失败: {exception.Message}");
            }

            return fallback;
        }

        private static async Task ListenLoopAsync(HttpListener currentListener, CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && currentListener.IsListening)
            {
                HttpListenerContext context = null;
                try
                {
                    context = await currentListener.GetContextAsync();
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }

                if (context == null)
                {
                    continue;
                }

                _ = Task.Run(() => ProcessRequest(context), cancellationToken);
            }
        }

        private static void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                string servingRoot = ResolveServingRoot(rootDirectory);
                string relativePath = Uri.UnescapeDataString(context.Request.Url.AbsolutePath.TrimStart('/'));
                if (string.IsNullOrEmpty(relativePath))
                {
                    relativePath = "index.html";
                }

                relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
                string fullPath = Path.GetFullPath(Path.Combine(servingRoot, relativePath));

                if (!fullPath.StartsWith(servingRoot, StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.Response.Close();
                    return;
                }

                if (Directory.Exists(fullPath))
                {
                    fullPath = Path.Combine(fullPath, "index.html");
                }

                if (!File.Exists(fullPath))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    context.Response.Close();
                    return;
                }

                byte[] bytes = File.ReadAllBytes(fullPath);
                context.Response.ContentType = GetContentType(Path.GetExtension(fullPath));
                context.Response.ContentLength64 = bytes.LongLength;
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.OutputStream.Write(bytes, 0, bytes.Length);
                context.Response.OutputStream.Flush();
                context.Response.Close();
            }
            catch (Exception exception)
            {
                try
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    context.Response.Close();
                }
                catch
                {
                }

                Debug.LogError($"本地模拟服务器处理请求失败: {exception}");
            }
        }

        private static string GetContentType(string extension)
        {
            switch ((extension ?? string.Empty).ToLowerInvariant())
            {
                case ".html":
                case ".htm":
                    return "text/html";
                case ".json":
                    return "application/json";
                case ".txt":
                case ".manifest":
                    return "text/plain";
                case ".xml":
                    return "application/xml";
                case ".png":
                    return "image/png";
                case ".jpg":
                case ".jpeg":
                    return "image/jpeg";
                case ".webp":
                    return "image/webp";
                case ".mp3":
                    return "audio/mpeg";
                case ".wav":
                    return "audio/wav";
                case ".ogg":
                    return "audio/ogg";
                case ".mp4":
                    return "video/mp4";
                default:
                    return "application/octet-stream";
            }
        }

        private static string ResolveServingRoot(string configuredRoot)
        {
            if (string.IsNullOrWhiteSpace(configuredRoot) || !Directory.Exists(configuredRoot))
            {
                return configuredRoot;
            }

            if (ContainsPackageVersionFile(configuredRoot))
            {
                return configuredRoot;
            }

            string latestChildWithVersion = Directory.GetDirectories(configuredRoot)
                .Where(ContainsPackageVersionFile)
                .OrderByDescending(path => new DirectoryInfo(path).CreationTimeUtc)
                .FirstOrDefault();

            return string.IsNullOrEmpty(latestChildWithVersion) ? configuredRoot : latestChildWithVersion;
        }

        private static bool ContainsPackageVersionFile(string directory)
        {
            return Directory.Exists(directory) &&
                   Directory.GetFiles(directory, "*.version", SearchOption.TopDirectoryOnly).Length > 0;
        }
    }
}
