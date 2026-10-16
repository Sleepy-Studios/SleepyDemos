using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Core.Editor.Hotfix;
using Core.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Module
{
    public sealed class GitHubPagesDeploymentTests
    {
        [Test]
        public void AutomationRequest_OpeningBuildWindowPreservesSharedConfiguration()
        {
            var config = AssetDatabase.LoadAssetAtPath<HotfixConfig>("Assets/LoadResources/Config/HotfixConfig.asset");
            string original = EditorJsonUtility.ToJson(config);
            string request = Environment.GetEnvironmentVariable("SLEEPY_BUILD_REQUEST");
            HotfixBuildWindow window = null;
            try
            {
                config.GameId = "kagit_kart";
                config.BaseServerURL = config.RemoteBaseServerURL = "https://example.com/TapTap/kagit_kart/hotupdate/WebGL";
                config.HotfixSourcePath = "Automation/Fixed/Hotfix";
                string expected = EditorJsonUtility.ToJson(config);
                Environment.SetEnvironmentVariable("SLEEPY_BUILD_REQUEST", "automation-request.json");
                window = ScriptableObject.CreateInstance<HotfixBuildWindow>();
                Assert.That(EditorJsonUtility.ToJson(config), Is.EqualTo(expected), "打开窗口不得用本地偏好覆盖自动化配置");
            }
            finally
            {
                if (window != null) UnityEngine.Object.DestroyImmediate(window);
                EditorJsonUtility.FromJsonOverwrite(original, config);
                Environment.SetEnvironmentVariable("SLEEPY_BUILD_REQUEST", request);
            }
        }

        [TestCase("AGFzbQEAAAABBAFgAAADAwIAAAciAhBfX21haW5fYXJnY19hcmd2AAALU2VuZE1lc3NhZ2UAAQoHAgIACwIACw==", true)]
        [TestCase("AGFzbQEAAAABBAFgAAADAgEABwgBBHNicmsAAAoEAQIACw==", false)]
        public void TapTapWasm_RequiresGameEntryAndUnityMessageBridge(string module, bool accepted)
        {
            // 两份最小合法模块分别导出 Unity 入口和 SendMessage、或仅导出 sbrk。
            string path = Path.Combine(Path.GetTempPath(), "SleepyDemos-Wasm-" + Guid.NewGuid().ToString("N") + ".wasm");
            try
            {
                File.WriteAllBytes(path, Convert.FromBase64String(module));
                if (accepted) Assert.DoesNotThrow(() => TapTapBuildAdapter.ValidateWasm(path));
                else Assert.Throws<InvalidOperationException>(() => TapTapBuildAdapter.ValidateWasm(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Test]
        public void PublicationFiles_IncludeOnlyDownloadResources_RejectUnsafeVersion()
        {
            string root = Path.Combine(Path.GetTempPath(), "SleepyDemos-PublishGuard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                File.WriteAllText(Path.Combine(root, "DefaultPackage.version"), "1.0");
                foreach (string name in new[] { "DefaultPackage_1.0.bytes", "DefaultPackage_1.0.hash", "a.bundle", "game.apk", "archive.zip" })
                {
                    File.WriteAllText(Path.Combine(root, name), "content");
                }
                Type type = typeof(HotfixBuildWindow).Assembly.GetType("Core.Editor.Hotfix.GitHubPagesDeployment", true);
                MethodInfo getFiles = type.GetMethod("GetSourceFiles", BindingFlags.Static | BindingFlags.NonPublic);
                var files = (string[])getFiles.Invoke(null, new object[] { root, "DefaultPackage" });
                Assert.That(files.Length, Is.EqualTo(4));
                Assert.That(Array.Exists(files, file => file.EndsWith(".apk") || file.EndsWith(".zip")), Is.False);
                File.WriteAllText(Path.Combine(root, "DefaultPackage.version"), "../escape");
                var exception = Assert.Throws<TargetInvocationException>(() => getFiles.Invoke(null, new object[] { root, "DefaultPackage" }));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(File.ReadAllText(Path.Combine(root, "a.bundle")), Is.EqualTo("content"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void TapTapH5Archive_RequiresIndexAndPreservesNestedBytesUnderSingleGameRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "SleepyDemos-H5Archive-" + Guid.NewGuid().ToString("N"));
            string webgl = Path.Combine(root, "webgl");
            string archive = Path.Combine(root, "h5.zip");
            var files = new Dictionary<string, byte[]>
            {
                ["index.html"] = Encoding.UTF8.GetBytes("<html>Unity</html>"),
                ["Build/game.wasm.unityweb"] = new byte[] { 0, 97, 115, 109, 255 },
                ["Build/game.data.unityweb"] = new byte[] { 128, 0, 255, 1 },
                ["StreamingAssets/yoo/BuiltinCatalog.bytes"] = new byte[] { 0, 1, 2, 3 }
            };
            try
            {
                foreach (var file in files)
                {
                    if (file.Key == "index.html")
                    {
                        continue;
                    }
                    string path = Path.Combine(webgl, file.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, file.Value);
                }
                Assert.Throws<InvalidOperationException>(() => TapTapBuildAdapter.CreateArchive(webgl, archive));
                Assert.That(File.Exists(archive), Is.False);
                File.WriteAllBytes(Path.Combine(webgl, "index.html"), files["index.html"]);
                Assert.Throws<InvalidOperationException>(() => TapTapBuildAdapter.CreateArchive(webgl, Path.Combine(webgl, "h5.zip")));
                Assert.That(File.Exists(Path.Combine(webgl, "h5.zip")), Is.False);
                TapTapBuildAdapter.CreateArchive(webgl, archive);

                using var zip = ZipFile.OpenRead(archive);
                Assert.That(zip.Entries.Count, Is.EqualTo(files.Count));
                Assert.That(zip.GetEntry("game/index.html"), Is.Not.Null);
                foreach (var entry in zip.Entries)
                {
                    Assert.That(entry.FullName, Does.StartWith("game/"));
                    string relative = entry.FullName.Substring("game/".Length);
                    Assert.That(files.ContainsKey(relative), Is.True);
                    using var input = entry.Open();
                    using var bytes = new MemoryStream();
                    input.CopyTo(bytes);
                    Assert.That(bytes.ToArray(), Is.EqualTo(files[relative]));
                }
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void PublicationPath_SeparatesNativeAndTapTap_RejectsTraversal()
        {
            Assert.That(GitHubPagesDeployment.GetResourcePath("SleepyDemos/TapTap/kagit_kart", "WebGL"),
                Is.EqualTo("unity/SleepyDemos/TapTap/kagit_kart/hotupdate/WebGL"));
            Assert.That(GitHubPagesDeployment.GetResourcePath("SleepyDemos", "PC"),
                Is.EqualTo("unity/SleepyDemos/hotupdate/PC"));
            Assert.Throws<InvalidOperationException>(() =>
                GitHubPagesDeployment.GetResourcePath("SleepyDemos/TapTap/../other", "WebGL"));
            Assert.Throws<InvalidOperationException>(() =>
                GitHubPagesDeployment.GetResourcePath("SleepyDemos/TapTap/kagit_kart", "../PC"));
        }

        [TestCase("/")]
        [TestCase("/publish/../other")]
        [TestCase("relative")]
        public void SshRoot_RejectsUnsafeTarget(string root)
        {
            Type type = typeof(HotfixBuildWindow).Assembly.GetType("Core.Editor.Hotfix.SshBundleDeployment", true);
            MethodInfo resolve = type.GetMethod("GetRemotePath", BindingFlags.Static | BindingFlags.NonPublic);
            var exception = Assert.Throws<TargetInvocationException>(() => resolve.Invoke(null, new object[] { root, "PC" }));
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        }

        [Test]
        public async Task HttpVerification_StalledResponseBody_CancelsWithinBudget()
        {
            string root = Path.Combine(Path.GetTempPath(), "SleepyDemos-VersionRecovery-" + Guid.NewGuid().ToString("N"));
            var listener = new TcpListener(IPAddress.Loopback, 0);
            using var serverStop = new CancellationTokenSource();
            using var budget = new CancellationTokenSource();
            var headersSent = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            TcpClient connection = null;
            Task server = null;
            Task verification = null;
            try
            {
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "DefaultPackage.version"), "1.0");
                foreach (string name in new[] { "DefaultPackage_1.0.bytes", "DefaultPackage_1.0.hash", "a.bundle" })
                {
                    File.WriteAllText(Path.Combine(root, name), "content");
                }
                listener.Start();
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                server = Task.Run(async () =>
                {
                    connection = await listener.AcceptTcpClientAsync();
                    using var stream = connection.GetStream();
                    await stream.ReadAsync(new byte[4096], 0, 4096, serverStop.Token);
                    byte[] headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1024\r\nConnection: close\r\n\r\n");
                    await stream.WriteAsync(headers, 0, headers.Length, serverStop.Token);
                    await stream.FlushAsync(serverStop.Token);
                    headersSent.TrySetResult(true);
                    // 保持连接开放却不发送正文，复现 HeadersRead 后的读取挂起。
                    await Task.Delay(Timeout.Infinite, serverStop.Token);
                });
                Type type = typeof(HotfixBuildWindow).Assembly.GetType("Core.Editor.Hotfix.HotfixDeploymentVerification", true);
                MethodInfo verify = type.GetMethod("VerifyAsync", BindingFlags.Static | BindingFlags.NonPublic);
                verification = (Task)verify.Invoke(null, new object[]
                {
                    root, "DefaultPackage", "http://127.0.0.1:" + port,
                    new Func<string, bool>(file => Path.GetFileName(file) == "a.bundle"), budget.Token
                });
                Assert.That(await Task.WhenAny(headersSent.Task, server, Task.Delay(10000)), Is.SameAs(headersSent.Task),
                    "验证请求必须收到服务器的成功响应头。");
                budget.CancelAfter(250);
                Assert.That(await Task.WhenAny(verification, Task.Delay(5000)), Is.SameAs(verification),
                    "正文停止发送时，取消预算必须终止验证。");
                Assert.That(budget.IsCancellationRequested, Is.True);
                Assert.That(verification.IsFaulted || verification.IsCanceled, Is.True,
                    "未收到正文不能被判为下载验证成功。");
            }
            finally
            {
                budget.Cancel();
                serverStop.Cancel();
                listener.Stop();
                connection?.Dispose();
                if (server != null)
                {
                    try
                    {
                        await server;
                    }
                    catch (Exception) when (serverStop.IsCancellationRequested)
                    {
                    }
                }
                if (verification != null && await Task.WhenAny(verification, Task.Delay(2000)) == verification)
                {
                    try
                    {
                        await verification;
                    }
                    catch (Exception) when (budget.IsCancellationRequested)
                    {
                    }
                }
                DeleteRecoveryFixture(root);
            }
        }

        [TestCase("new", true)]
        [TestCase("concurrent", false)]
        [TestCase("old", true)]
        public async Task PagesVersionRecovery_RestoresOwnRelease_PreservesConcurrentRelease(string onlineVersion, bool canRestore)
        {
            string root = Path.Combine(Path.GetTempPath(), "SleepyDemos-VersionRecovery-" + Guid.NewGuid().ToString("N"));
            string repository = Path.Combine(root, "publisher");
            string remote = Path.Combine(root, "remote.git");
            string resourcePath = "unity/SleepyDemos/hotupdate/PC";
            string versionPath = Path.Combine(repository, resourcePath, "DefaultPackage.version");
            Type type = typeof(HotfixBuildWindow).Assembly.GetType("Core.Editor.Hotfix.GitHubPagesDeployment", true);
            MethodInfo git = type.GetMethod("GitAsync", BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo restore = type.GetMethod("RestoreVersionAsync", BindingFlags.Static | BindingFlags.NonPublic);
            Task<string> Git(string directory, params string[] arguments) => (Task<string>)git.Invoke(null, new object[] { directory, arguments });
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(versionPath));
                Directory.CreateDirectory(remote);
                await Git(remote, "init", "--bare");
                await Git(repository, "init", "-b", "main");
                await Git(repository, "config", "user.name", "Publication Guard");
                await Git(repository, "config", "user.email", "publication-guard@example.invalid");
                await Git(repository, "config", "commit.gpgsign", "false");
                await Git(repository, "config", "core.hooksPath", Path.Combine(root, "no-hooks"));
                File.WriteAllText(versionPath, onlineVersion, new System.Text.UTF8Encoding(false));
                await Git(repository, "add", "--", resourcePath);
                await Git(repository, "commit", "-m", "test fixture");
                await Git(repository, "remote", "add", "origin", remote);
                await Git(repository, "push", "origin", "main");
                if (onlineVersion == "old")
                {
                    File.WriteAllText(versionPath, "new", new System.Text.UTF8Encoding(false));
                    await Git(repository, "add", "--", resourcePath);
                    await Git(repository, "commit", "-m", "unpublished activation");
                }
                string attemptedCommit = (await Git(repository, "rev-parse", "HEAD")).Trim();
                Func<Task<string>> recover = () => (Task<string>)restore.Invoke(null,
                    new object[] { repository, resourcePath, "DefaultPackage", "new", System.Text.Encoding.UTF8.GetBytes("old"), root + "-unity" });
                if (canRestore)
                {
                    await recover();
                    Assert.That(File.ReadAllText(versionPath), Is.EqualTo("old"));
                    Assert.That((await Git(remote, "show", "main:" + resourcePath + "/DefaultPackage.version")).Trim(), Is.EqualTo("old"));
                    if (onlineVersion == "old")
                    {
                        Assert.That((await Git(remote, "rev-parse", "main")).Trim(), Is.Not.EqualTo(attemptedCommit));
                        await Git(remote, "merge-base", "--is-ancestor", attemptedCommit, "main");
                    }
                }
                else
                {
                    InvalidOperationException failure = null;
                    try
                    {
                        await recover();
                    }
                    catch (InvalidOperationException exception)
                    {
                        failure = exception;
                    }
                    Assert.That(failure, Is.Not.Null);
                    Assert.That(File.ReadAllText(versionPath), Is.EqualTo(onlineVersion));
                    Assert.That((await Git(remote, "show", "main:" + resourcePath + "/DefaultPackage.version")).Trim(), Is.EqualTo(onlineVersion));
                }
            }
            finally
            {
                DeleteRecoveryFixture(root);
            }
        }

        private static void DeleteRecoveryFixture(string root)
        {
            string fullRoot = Path.GetFullPath(root);
            string temporaryRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(fullRoot);
            const string prefix = "SleepyDemos-VersionRecovery-";
            if (!string.Equals(Path.GetDirectoryName(fullRoot), temporaryRoot, StringComparison.OrdinalIgnoreCase) ||
                !name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name.Substring(prefix.Length), "N", out _))
            {
                throw new InvalidOperationException("拒绝清理不属于本次恢复测试的目录。");
            }
            if (!Directory.Exists(fullRoot))
            {
                return;
            }

            var entries = new List<string> { fullRoot };
            var pending = new Stack<string>();
            pending.Push(fullRoot);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException("测试临时目录不能经过链接。");
                }
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                {
                    FileAttributes attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException("测试临时目录包含链接，拒绝递归清理。");
                    }
                    entries.Add(entry);
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        pending.Push(entry);
                    }
                }
            }
            // Git 会把 object 标为只读；完整确认归属和链接后，仅移除本次 fixture 的只读属性。
            foreach (string entry in entries)
            {
                File.SetAttributes(entry, File.GetAttributes(entry) & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(fullRoot, true);
        }

        [Test]
        public void ClearAsync_UnsafeDirectories_PreserveExistingFiles()
        {
            string testRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "SleepyDemos-PagesGuard-" + Guid.NewGuid().ToString("N")));
            string pcDirectory = Path.Combine(testRoot, "unity", "SleepyDemos", "hotupdate", "PC");
            string androidDirectory = Path.Combine(testRoot, "unity", "SleepyDemos", "hotupdate", "Android");
            string otherProjectDirectory = Path.Combine(testRoot, "unity", "OtherGame", "hotupdate", "PC");
            string[] protectedFiles =
            {
                Path.Combine(pcDirectory, "keep.bundle"),
                Path.Combine(androidDirectory, "keep.bundle"),
                Path.Combine(otherProjectDirectory, "keep.bundle")
            };

            try
            {
                foreach (string file in protectedFiles)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(file));
                    File.WriteAllText(file, "keep");
                }

                var deploymentType = typeof(HotfixBuildWindow).Assembly.GetType("Core.Editor.Hotfix.GitHubPagesDeployment", true);
                MethodInfo clear = deploymentType.GetMethod("ClearAsync", BindingFlags.Static | BindingFlags.NonPublic);
                string[] invalidPaths =
                {
                    "", "/", "unity/SleepyDemos/hotupdate", "unity/../hotupdate/PC",
                    "unity/SleepyDemos/hotupdate/../Android", "unity/SleepyDemos/hotupdate/PC/.."
                };

                foreach (string invalidPath in invalidPaths)
                {
                    Assert.ThrowsAsync<InvalidOperationException>(() => (Task<string>)clear.Invoke(null, new object[] { testRoot, invalidPath }));
                }

                string nestedDirectory = Path.Combine(pcDirectory, "unexpected");
                Directory.CreateDirectory(nestedDirectory);
                string nestedFile = Path.Combine(nestedDirectory, "keep.bundle");
                File.WriteAllText(nestedFile, "keep");
                Assert.ThrowsAsync<InvalidOperationException>(() => (Task<string>)clear.Invoke(null, new object[] { testRoot, "unity/SleepyDemos/hotupdate/PC" }));
                Assert.That(File.ReadAllText(nestedFile), Is.EqualTo("keep"));
                foreach (string file in protectedFiles)
                {
                    Assert.That(File.ReadAllText(file), Is.EqualTo("keep"));
                }
            }
            finally
            {
                if (Directory.Exists(testRoot))
                {
                    Directory.Delete(testRoot, true);
                }
            }
        }
    }
}
