using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
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

        internal static async Task WaitForPagesAsync(string repository, string expectedCommit = null)
        {
            string commit = expectedCommit ?? (await GitHubPagesDeployment.GitAsync(repository, "rev-parse", "HEAD").ConfigureAwait(false)).Trim();
            string name = (await GitHubPagesDeployment.RunProcessAsync("gh", repository, 120000,
                "repo", "view", "--json", "nameWithOwner", "--jq", ".nameWithOwner").ConfigureAwait(false)).StandardOutput.Trim();
            DateTime deadline = DateTime.UtcNow.AddMinutes(20);
            while (DateTime.UtcNow < deadline)
            {
                string json = (await GitHubPagesDeployment.RunProcessAsync("gh", repository, 120000,
                    "api", $"repos/{name}/pages/builds/latest").ConfigureAwait(false)).StandardOutput;
                var build = JsonUtility.FromJson<PagesBuild>(json);
                if (build.commit == commit && build.status == "built")
                {
                    return;
                }
                if (build.commit == commit && build.status == "errored")
                {
                    throw new InvalidOperationException("Pages 部署失败。");
                }
                await Task.Delay(15000).ConfigureAwait(false);
            }
            throw new TimeoutException("Pages 在 20 分钟内未完成本次提交的部署。");
        }

        internal static async Task<DownloadRecord[]> VerifyAsync(string source, string package, string serverUrl, Func<string, bool> includeFile = null)
        {
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out Uri site) ||
                (site.Scheme != Uri.UriSchemeHttps && site.Scheme != Uri.UriSchemeHttp) ||
                !string.IsNullOrEmpty(site.Query) || !string.IsNullOrEmpty(site.Fragment) || !string.IsNullOrEmpty(site.UserInfo))
            {
                throw new InvalidOperationException("下载验证必须使用不含凭据、查询或片段的 HTTP(S) 平台根 URL。");
            }

            var records = new List<DownloadRecord>();
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            foreach (string file in GitHubPagesDeployment.GetSourceFiles(source, package))
            {
                if (includeFile != null && !includeFile(file))
                {
                    continue;
                }
                string expected = HashFile(file);
                bool matched = false;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    using var response = await client.GetAsync(serverUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(Path.GetFileName(file)) +
                        "?verification=" + Guid.NewGuid().ToString("N"), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        using var sha = SHA256.Create();
                        // 流式下载全部文件，避免大 Bundle 整块驻留内存。
                        string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                        if (actual == expected)
                        {
                            records.Add(new DownloadRecord { File = Path.GetFileName(file), Sha256 = actual, Bytes = new FileInfo(file).Length });
                            matched = true;
                            break;
                        }
                    }
                    await Task.Delay(15000).ConfigureAwait(false);
                }
                if (!matched)
                {
                    throw new InvalidOperationException("远端下载 SHA256 不匹配：" + Path.GetFileName(file));
                }
            }
            return records.ToArray();
        }

        internal static async Task VerifyRestoredVersionAsync(string serverUrl, string package, byte[] expected)
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            for (int attempt = 0; attempt < 4; attempt++)
            {
                using var response = await client.GetAsync(serverUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(package + ".version") +
                    "?verification=" + Guid.NewGuid().ToString("N")).ConfigureAwait(false);
                if (expected == null && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return;
                }
                if (expected != null && response.IsSuccessStatusCode &&
                    (await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false)).SequenceEqual(expected))
                {
                    return;
                }
                await Task.Delay(15000).ConfigureAwait(false);
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
