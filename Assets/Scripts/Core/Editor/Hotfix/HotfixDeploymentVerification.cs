using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Core.Editor.Hotfix
{
    internal static class HotfixDeploymentVerification
    {
        [Serializable]
        internal sealed class DownloadRecord
        {
            public string File;
            public string Sha256;
            public long Bytes;
        }

        [Serializable]
        private sealed class PagesBuild
        {
            public string status;
            public string commit;
        }

        internal static async Task WaitForPagesAsync(string repository, string expectedCommit = null, CancellationToken cancellationToken = default)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TimeSpan.FromMinutes(20));
            CancellationToken token = budget.Token;
            token.ThrowIfCancellationRequested();
            string commit = expectedCommit ?? (await GitHubPagesDeployment.GitWithCancellationAsync(repository, token, "rev-parse", "HEAD").ConfigureAwait(false)).Trim();
            string name = (await GitHubPagesDeployment.RunProcessWithCancellationAsync("gh", repository, 120000, token,
                "repo", "view", "--json", "nameWithOwner", "--jq", ".nameWithOwner").ConfigureAwait(false)).StandardOutput.Trim();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                string json = (await GitHubPagesDeployment.RunProcessWithCancellationAsync("gh", repository, 120000, token,
                    "api", $"repos/{name}/pages/builds/latest").ConfigureAwait(false)).StandardOutput;
                token.ThrowIfCancellationRequested();
                var build = JsonUtility.FromJson<PagesBuild>(json);
                if (build.commit == commit && build.status == "built")
                {
                    return;
                }
                if (build.commit == commit && build.status == "errored")
                {
                    throw new InvalidOperationException("Pages 部署失败。");
                }
                await Task.Delay(15000, token).ConfigureAwait(false);
            }
        }

        internal static async Task<DownloadRecord[]> VerifyAsync(string source, string package, string serverUrl,
            Func<string, bool> includeFile = null, CancellationToken cancellationToken = default)
        {
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri site) ||
                (site.Scheme != Uri.UriSchemeHttps && site.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(site.Query) || !string.IsNullOrEmpty(site.Fragment) || !string.IsNullOrEmpty(site.UserInfo))
            {
                throw new InvalidOperationException("下载验证必须使用不含凭据、查询或片段的 HTTP(S) 平台根 URL。");
            }

            var records = new List<DownloadRecord>();
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TimeSpan.FromMinutes(20));
            CancellationToken token = budget.Token;
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            foreach (string file in GitHubPagesDeployment.GetSourceFiles(source, package))
            {
                token.ThrowIfCancellationRequested();
                if (includeFile != null && !includeFile(file))
                {
                    continue;
                }
                string expected = HashFile(file);
                bool matched = false;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    using var response = await client.GetAsync(serverUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(Path.GetFileName(file)) +
                        "?verification=" + Guid.NewGuid().ToString("N"), HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                    using var cancelResponse = token.Register(response.Dispose);
                    if (response.IsSuccessStatusCode)
                    {
                        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        using var sha = SHA256.Create();
                        byte[] buffer = new byte[64 * 1024];
                        int count;
                        while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                        {
                            token.ThrowIfCancellationRequested();
                            sha.TransformBlock(buffer, 0, count, null, 0);
                        }
                        sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                        string actual = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
                        if (actual == expected)
                        {
                            records.Add(new DownloadRecord { File = Path.GetFileName(file), Sha256 = actual, Bytes = new FileInfo(file).Length });
                            matched = true;
                            break;
                        }
                    }
                    await Task.Delay(15000, token).ConfigureAwait(false);
                }
                if (!matched)
                {
                    throw new InvalidOperationException("远端下载 SHA256 不匹配：" + Path.GetFileName(file));
                }
            }
            token.ThrowIfCancellationRequested();
            return records.ToArray();
        }

        internal static async Task VerifyRestoredVersionAsync(string serverUrl, string package, byte[] expected, CancellationToken cancellationToken = default)
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(TimeSpan.FromMinutes(5));
            CancellationToken token = budget.Token;
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            for (int attempt = 0; attempt < 4; attempt++)
            {
                using var response = await client.GetAsync(serverUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(package + ".version") +
                    "?verification=" + Guid.NewGuid().ToString("N"), HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                using var cancelResponse = token.Register(response.Dispose);
                if (expected == null && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return;
                }
                if (expected != null && response.IsSuccessStatusCode)
                {
                    using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    byte[] actual = new byte[expected.Length + 1];
                    int total = 0;
                    int count;
                    while (total < actual.Length &&
                        (count = await stream.ReadAsync(actual, total, actual.Length - total, token).ConfigureAwait(false)) > 0)
                    {
                        total += count;
                    }
                    if (total == expected.Length && actual.Take(total).SequenceEqual(expected))
                    {
                        return;
                    }
                }
                await Task.Delay(15000, token).ConfigureAwait(false);
            }
            throw new InvalidOperationException("旧版本入口 HTTP 恢复尚未确认，请核对保留的发布提交。");
        }

        internal static string HashFile(string file)
        {
            using var sha = SHA256.Create();
            using var stream = System.IO.File.OpenRead(file);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
