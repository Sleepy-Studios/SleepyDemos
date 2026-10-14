using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Renci.SshNet;

namespace Core.Editor.Hotfix
{
    internal static class SshBundleDeployment
    {
        internal static void Test(string host, int port, string user, string keyFile, string fingerprint)
        {
            using var client = new SftpClient(CreateConnection(host, port, user, keyFile, fingerprint));
            Pin(client, fingerprint);
            client.Connect();
        }

        internal static void Upload(string host, int port, string user, string keyFile, string fingerprint,
            string root, string platform, string source, string package, Action<byte[]> beforeActivate = null,
            Action afterActivate = null, Action<bool, byte[]> onRollback = null)
        {
            string target = GetRemotePath(root, platform);
            string[] files = GitHubPagesDeployment.GetSourceFiles(GitHubPagesDeployment.NormalizeDirectory(source), package)
                .OrderBy(file => Path.GetFileName(file) == package + ".version" ? 1 : 0).ToArray();
            using var client = new SftpClient(CreateConnection(host, port, user, keyFile, fingerprint));
            Pin(client, fingerprint);
            client.Connect();
            string current = string.Empty;
            foreach (string segment in target.Split('/').Where(segment => segment.Length > 0))
            {
                current += "/" + segment;
                if (!client.Exists(current))
                {
                    client.CreateDirectory(current);
                }
                if (!client.Get(current).IsDirectory || client.Get(current).IsSymbolicLink)
                {
                    throw new InvalidOperationException("远端路径必须是普通目录，不能经过符号链接。");
                }
            }

            foreach (string file in files)
            {
                string destination = target + "/" + Path.GetFileName(file);
                if (client.Exists(destination) && client.Get(destination).IsSymbolicLink)
                {
                    throw new InvalidOperationException("拒绝覆盖远端符号链接。");
                }
                if (client.Exists(destination) && Path.GetFileName(file) != package + ".version")
                {
                    using var stream = client.OpenRead(destination);
                    using var sha = SHA256.Create();
                    string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                    if (actual != HotfixDeploymentVerification.HashFile(file))
                    {
                        throw new InvalidOperationException("同名已发布资源内容不同，请使用新的资源版本：" + Path.GetFileName(file));
                    }
                }
            }
            foreach (string file in files)
            {
                string destination = target + "/" + Path.GetFileName(file);
                if (Path.GetFileName(file) == package + ".version")
                {
                    byte[] previous = client.Exists(destination) ? ReadVersion(client, destination) : null;
                    byte[] attempted = File.ReadAllBytes(file);
                    beforeActivate?.Invoke(previous);
                    try
                    {
                        UploadAtomic(client, file, destination);
                        afterActivate?.Invoke();
                    }
                    catch
                    {
                        try
                        {
                            byte[] currentVersion = client.Exists(destination) ? ReadVersion(client, destination) : null;
                            if ((currentVersion == null && previous == null) ||
                                (currentVersion != null && previous != null && currentVersion.SequenceEqual(previous)))
                            {
                                onRollback?.Invoke(true, previous);
                            }
                            else if (currentVersion != null && currentVersion.SequenceEqual(attempted))
                            {
                                if (previous == null)
                                {
                                    client.DeleteFile(destination);
                                }
                                else
                                {
                                    string recovery = destination + ".restore-" + Guid.NewGuid().ToString("N");
                                    using var stream = new MemoryStream(previous);
                                    client.UploadFile(stream, recovery, false);
                                    client.RenameFile(recovery, destination, true);
                                }
                                onRollback?.Invoke(true, previous);
                            }
                            else
                            {
                                throw new InvalidOperationException("远端版本已被其他发布更改，不能覆盖。");
                            }
                        }
                        catch
                        {
                            onRollback?.Invoke(false, previous);
                            throw new InvalidOperationException("SSH 激活或校验失败，旧版本入口恢复失败；保留上传及恢复临时文件供人工检查。");
                        }
                        throw new InvalidOperationException("SSH 激活或校验失败，已恢复旧版本入口。");
                    }
                    continue;
                }
                UploadAtomic(client, file, destination);
            }
        }

        private static byte[] ReadVersion(SftpClient client, string path)
        {
            if (client.GetAttributes(path).Size > 1024)
            {
                throw new InvalidOperationException("远端版本文件过大。");
            }
            using var stream = client.OpenRead(path);
            using var content = new MemoryStream();
            stream.CopyTo(content);
            return content.ToArray();
        }

        private static void UploadAtomic(SftpClient client, string file, string destination)
        {
            string temporary = destination + ".upload-" + Guid.NewGuid().ToString("N");
            using (var stream = File.OpenRead(file))
            {
                client.UploadFile(stream, temporary, false);
            }
            // POSIX rename 保证旧版本一直可读；服务端不支持时直接失败，不先删除旧文件。
            client.RenameFile(temporary, destination, true);
        }

        internal static string GetRemotePath(string root, string platform)
        {
            root = (root ?? string.Empty).TrimEnd('/');
            if (!root.StartsWith("/", StringComparison.Ordinal) || root.Length < 2 || root.Any(char.IsControl) ||
                root.Split('/').Any(part => part == "." || part == "..") || !Regex.IsMatch(platform ?? string.Empty, @"\A[A-Za-z0-9_-]+\z"))
            {
                throw new InvalidOperationException("远端根必须是非根目录的绝对路径，不能包含相对跳转或控制字符。");
            }
            return root + "/" + platform;
        }

        private static ConnectionInfo CreateConnection(string host, int port, string user, string keyFile, string fingerprint)
        {
            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) || port < 1 || port > 65535 ||
                !Regex.IsMatch(fingerprint ?? string.Empty, @"\ASHA256:[A-Za-z0-9+/]{43}=?\z"))
            {
                throw new InvalidOperationException("SSH 地址、用户、端口和可信 SHA256 主机指纹必须完整。");
            }
            return new ConnectionInfo(host, port, user, new PrivateKeyAuthenticationMethod(user, new PrivateKeyFile(keyFile)))
            {
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        private static void Pin(SftpClient client, string fingerprint)
        {
            client.HostKeyReceived += (_, args) =>
            {
                using var sha = SHA256.Create();
                string actual = "SHA256:" + Convert.ToBase64String(sha.ComputeHash(args.HostKey)).TrimEnd('=');
                args.CanTrust = string.Equals(actual, fingerprint.TrimEnd('='), StringComparison.Ordinal);
            };
            client.OperationTimeout = TimeSpan.FromMinutes(10);
            client.KeepAliveInterval = TimeSpan.FromSeconds(30);
        }
    }
}
