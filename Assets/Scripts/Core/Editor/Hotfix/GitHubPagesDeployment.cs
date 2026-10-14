using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Core.Editor.Hotfix
{
    internal static class GitHubPagesDeployment
    {
        private const long MaximumFileSize = 100L * 1024 * 1024;
        private const long MaximumSiteSize = 1000L * 1000 * 1000;
        private const int GitTimeoutMilliseconds = 120000;
        private const int PushTimeoutMilliseconds = 20 * 60 * 1000;

        internal static string GetResourcePath(string projectName, string platformFolder)
        {
            ValidateSegment(projectName, "项目名");
            ValidateSegment(platformFolder, "平台名");
            return $"unity/{projectName}/hotupdate/{platformFolder}";
        }

        internal static Task<string> UploadAsync(
            string repositoryPath,
            string resourcePath,
            string bundlePath,
            string packageName,
            bool activateVersion = true,
            CancellationToken cancellationToken = default)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Task.Run(async () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                string repository = NormalizeDirectory(repositoryPath);
                string target = ResolveTarget(repository, resourcePath, projectRoot);
                string source = NormalizeDirectory(bundlePath);
                if (IsWithin(source, repository) || IsWithin(repository, source))
                {
                    throw new InvalidOperationException("资源源目录与发布仓库不能互相包含。");
                }

                string[] sourceFiles = GetSourceFiles(source, packageName)
                    .Where(file => activateVersion || Path.GetFileName(file) != packageName + ".version").ToArray();
                ValidateFlatDirectory(target);
                await ValidateRepositoryAsync(repository, resourcePath, cancellationToken);
                ValidateSiteSize(repository, target, sourceFiles);

                EnsureNoLinks(target);
                Directory.CreateDirectory(target);
                // 同一资源版本必须不可变；版本指针可前进，已有清单和 Bundle 只能原样重复发布。
                foreach (string sourceFile in sourceFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string targetFile = Path.Combine(target, Path.GetFileName(sourceFile));
                    EnsureNoLinks(targetFile);
                    if (File.Exists(targetFile) && Path.GetFileName(sourceFile) != packageName + ".version" &&
                        !HasSameContent(sourceFile, targetFile))
                    {
                        throw new InvalidOperationException("同名已发布资源内容不同，请使用新的资源版本：" + Path.GetFileName(sourceFile));
                    }
                }
                foreach (string sourceFile in sourceFiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string targetFile = Path.Combine(target, Path.GetFileName(sourceFile));
                    EnsureNoLinks(sourceFile);
                    EnsureNoLinks(targetFile);
                    if (!File.Exists(targetFile) || !HasSameContent(sourceFile, targetFile))
                    {
                        File.Copy(sourceFile, targetFile, true);
                    }
                }

                return await CommitAndPushAsync(repository, resourcePath, "更新", target, cancellationToken);
            });
        }

        internal static Task<string> ClearAsync(string repositoryPath, string resourcePath)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Task.Run(async () =>
            {
                string repository = NormalizeDirectory(repositoryPath);
                string target = ResolveTarget(repository, resourcePath, projectRoot);
                ValidateFlatDirectory(target);
                await ValidateRepositoryAsync(repository, resourcePath);

                ValidateFlatDirectory(target);
                Directory.CreateDirectory(target);
                string[] files = Directory.GetFiles(target, "*", SearchOption.TopDirectoryOnly);
                // 删除前完整检查，避免遇到链接时只清空了一部分目录。
                foreach (string file in files)
                {
                    EnsureNoLinks(file);
                }

                foreach (string file in files)
                {
                    EnsureNoLinks(file);
                    File.Delete(file);
                }

                File.WriteAllText(Path.Combine(target, ".gitkeep"), string.Empty);
                return await CommitAndPushAsync(repository, resourcePath, "清空", target);
            });
        }

        internal static Task<string> RestoreVersionAsync(string repositoryPath, string resourcePath, string packageName,
            string attemptedVersion, byte[] previousVersion, string projectRoot)
        {
            return RestoreVersionWithCancellationAsync(repositoryPath, resourcePath, packageName,
                attemptedVersion, previousVersion, projectRoot, CancellationToken.None);
        }

        internal static async Task<string> RestoreVersionWithCancellationAsync(string repositoryPath, string resourcePath, string packageName,
            string attemptedVersion, byte[] previousVersion, string projectRoot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string repository = NormalizeDirectory(repositoryPath);
            string target = ResolveTarget(repository, resourcePath, projectRoot);
            ValidateFlatDirectory(target);
            string relative = resourcePath + "/" + packageName + ".version";
            ValidateSegment(packageName, "资源包名");
            string versionPath = Path.Combine(target, packageName + ".version");
            EnsureNoLinks(versionPath);
            await GitWithCancellationAsync(repository, cancellationToken, "fetch", "origin", "main");
            var remote = await RunProcessAsync("git", repository, GitTimeoutMilliseconds, false, cancellationToken, "show", "FETCH_HEAD:" + relative);
            string old = previousVersion == null ? null : Encoding.UTF8.GetString(previousVersion).Trim();
            string localHead = (await GitWithCancellationAsync(repository, cancellationToken, "rev-parse", "HEAD")).Trim();
            string remoteHead = (await GitWithCancellationAsync(repository, cancellationToken, "rev-parse", "FETCH_HEAD")).Trim();
            bool remoteIsPrevious = remote.ExitCode == 0 && remote.StandardOutput.Trim() == old;
            if (remote.ExitCode != 0 && previousVersion == null)
            {
                remoteIsPrevious = string.IsNullOrWhiteSpace(await GitWithCancellationAsync(repository, cancellationToken,
                    "ls-tree", "--name-only", "FETCH_HEAD", "--", relative));
            }
            if (!File.Exists(versionPath) || File.ReadAllText(versionPath).Trim() != attemptedVersion ||
                !string.IsNullOrWhiteSpace(await GitWithCancellationAsync(repository, cancellationToken, "status", "--porcelain", "--untracked-files=all")))
            {
                throw new InvalidOperationException("Pages 版本或远端提交已改变，不能安全恢复旧版本；保留本地发布提交供人工处理。");
            }
            if (remoteIsPrevious && localHead != remoteHead)
            {
                var ancestor = await RunProcessAsync("git", repository, GitTimeoutMilliseconds, false, cancellationToken,
                    "merge-base", "--is-ancestor", remoteHead, localHead);
                string pendingPaths = (await GitWithCancellationAsync(repository, cancellationToken,
                    "diff", "--name-only", remoteHead + ".." + localHead)).Trim();
                if (ancestor.ExitCode != 0 || pendingPaths != relative)
                {
                    throw new InvalidOperationException("Pages 远端旧版本与本地激活提交关系不明，不能安全恢复；保留提交供人工处理。");
                }
                // 远端暂时仍旧也必须推送激活提交的恢复后继，阻止已收请求的迟到推送再次激活。
            }
            else if (remote.ExitCode != 0 || remote.StandardOutput.Trim() != attemptedVersion || localHead != remoteHead)
            {
                throw new InvalidOperationException("Pages 版本或远端提交已改变，不能安全恢复旧版本；保留本地发布提交供人工处理。");
            }
            if (previousVersion == null)
            {
                File.Delete(versionPath);
            }
            else
            {
                File.WriteAllBytes(versionPath, previousVersion);
            }
            cancellationToken.ThrowIfCancellationRequested();
            await CommitAndPushAsync(repository, resourcePath, "恢复版本入口", target, cancellationToken);
            return (await GitWithCancellationAsync(repository, cancellationToken, "rev-parse", "HEAD")).Trim();
        }

        internal static string NormalizeDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                throw new InvalidOperationException("目录必须使用完整绝对路径。");
            }

            string normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            EnsureNoLinks(normalized);
            if (!Directory.Exists(normalized))
            {
                throw new DirectoryNotFoundException($"目录不存在：{normalized}");
            }

            return normalized;
        }

        private static string ResolveTarget(string repository, string resourcePath, string projectRoot)
        {
            string[] segments = (resourcePath ?? string.Empty).Split('/');
            if (segments.Length != 4 || segments[0] != "unity" || segments[2] != "hotupdate")
            {
                throw new InvalidOperationException("资源目录必须是 unity/<项目>/hotupdate/<平台>。");
            }

            ValidateSegment(segments[1], "项目名");
            ValidateSegment(segments[3], "平台名");
            if (IsWithin(repository, Path.GetFullPath(projectRoot)) ||
                (Directory.Exists(Path.Combine(repository, "Assets")) &&
                 Directory.Exists(Path.Combine(repository, "ProjectSettings"))))
            {
                throw new InvalidOperationException("发布仓库不能使用 Unity 源码目录。");
            }

            string target = Path.GetFullPath(Path.Combine(repository, resourcePath));
            if (!IsWithin(target, repository) || string.Equals(target, repository, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("资源目录必须严格位于发布仓库内。");
            }

            EnsureNoLinks(target);
            return target;
        }

        private static void ValidateSegment(string segment, string description)
        {
            if (string.IsNullOrEmpty(segment) || !Regex.IsMatch(segment, @"\A[A-Za-z0-9_-]+\z"))
            {
                throw new InvalidOperationException($"{description}只允许英文字母、数字、下划线和连字符。");
            }

            string deviceName = segment.ToUpperInvariant();
            if (deviceName == "CON" || deviceName == "PRN" || deviceName == "AUX" || deviceName == "NUL" ||
                Regex.IsMatch(deviceName, @"\A(COM|LPT)[1-9]\z"))
            {
                throw new InvalidOperationException($"{description}不能使用 Windows 保留名称。");
            }
        }

        private static bool IsWithin(string path, string directory)
        {
            return string.Equals(path, directory, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                                   Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        internal static void EnsureNoLinks(string path)
        {
            for (string current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(current);
                }
                catch (FileNotFoundException)
                {
                    continue;
                }
                catch (DirectoryNotFoundException)
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException($"目录与文件不能经过符号链接或 Junction：{current}");
                }
            }
        }

        private static void ValidateFlatDirectory(string directory)
        {
            EnsureNoLinks(directory);
            if (!Directory.Exists(directory))
            {
                return;
            }

            foreach (string entry in Directory.GetFileSystemEntries(directory))
            {
                EnsureNoLinks(entry);
                if (Directory.Exists(entry))
                {
                    throw new InvalidOperationException($"平台资源目录包含子目录，请人工检查：{entry}");
                }
            }
        }

        internal static string[] GetSourceFiles(string source, string packageName)
        {
            ValidateSegment(packageName, "资源包名");
            string versionFile = Path.Combine(source, packageName + ".version");
            EnsureNoLinks(versionFile);
            if (!File.Exists(versionFile))
            {
                throw new FileNotFoundException("缺少资源包版本文件。", versionFile);
            }

            if (new FileInfo(versionFile).Length > 1024)
            {
                throw new InvalidOperationException("资源版本文件过大。");
            }
            string version = File.ReadAllText(versionFile).Trim();
            if (!Regex.IsMatch(version, @"\A[A-Za-z0-9_-][A-Za-z0-9_.-]*\z"))
            {
                throw new InvalidOperationException("资源包版本为空或包含非法文件名字符。");
            }

            string manifestName = $"{packageName}_{version}.bytes";
            string hashName = $"{packageName}_{version}.hash";
            var files = new List<string>();
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.TopDirectoryOnly))
            {
                EnsureNoLinks(file);
                string name = Path.GetFileName(file);
                if (name == Path.GetFileName(versionFile) || name == manifestName || name == hashName ||
                    name.EndsWith(".bundle", StringComparison.Ordinal))
                {
                    if (new FileInfo(file).Length == 0)
                    {
                        throw new InvalidOperationException($"资源文件为空，请重新构建：{file}");
                    }

                    files.Add(file);
                }
            }

            if (!files.Contains(Path.Combine(source, manifestName)) || !files.Contains(Path.Combine(source, hashName)))
            {
                throw new InvalidOperationException($"缺少当前版本清单或哈希文件：{manifestName}、{hashName}。");
            }

            if (!files.Any(file => file.EndsWith(".bundle", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("源目录没有非空 Bundle，请选择正式 YooAsset 构建目录。");
            }

            return files.ToArray();
        }

        private static void ValidateFileSize(string file)
        {
            if (new FileInfo(file).Length > MaximumFileSize)
            {
                throw new InvalidOperationException($"文件超过 GitHub 的 100 MiB 限制：{file}");
            }
        }

        private static void ValidateSiteSize(string repository, string target, string[] sourceFiles)
        {
            long totalSize = 0;
            var pending = new Stack<string>();
            pending.Push(repository);
            while (pending.Count > 0)
            {
                string directory = pending.Pop();
                foreach (string entry in Directory.GetFileSystemEntries(directory))
                {
                    if (directory == repository && Path.GetFileName(entry) == ".git")
                    {
                        continue;
                    }

                    EnsureNoLinks(entry);
                    if (Directory.Exists(entry))
                    {
                        pending.Push(entry);
                    }
                    else
                    {
                        ValidateFileSize(entry);
                        totalSize += new FileInfo(entry).Length;
                    }
                }
            }

            foreach (string sourceFile in sourceFiles)
            {
                ValidateFileSize(sourceFile);
                string targetFile = Path.Combine(target, Path.GetFileName(sourceFile));
                totalSize += new FileInfo(sourceFile).Length;
                if (File.Exists(targetFile))
                {
                    totalSize -= new FileInfo(targetFile).Length;
                }
            }

            if (totalSize >= MaximumSiteSize)
            {
                throw new InvalidOperationException("上传后站点文件总量将达到 1 GB，请先清理旧包。");
            }
        }

        private static bool HasSameContent(string sourceFile, string targetFile)
        {
            if (new FileInfo(sourceFile).Length != new FileInfo(targetFile).Length)
            {
                return false;
            }

            using var sha256 = SHA256.Create();
            using var source = File.OpenRead(sourceFile);
            using var target = File.OpenRead(targetFile);
            return sha256.ComputeHash(source).SequenceEqual(sha256.ComputeHash(target));
        }

        private static async Task ValidateRepositoryAsync(string repository, string resourcePath, CancellationToken cancellationToken = default)
        {
            string gitRoot = (await GitWithCancellationAsync(repository, cancellationToken, "rev-parse", "--show-toplevel")).Trim();
            if (!string.Equals(Path.GetFullPath(gitRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                    repository, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("发布路径必须是 Git 仓库根目录。");
            }

            string branch = (await GitWithCancellationAsync(repository, cancellationToken, "branch", "--show-current")).Trim();
            if (branch != "main")
            {
                throw new InvalidOperationException("发布仓库必须位于 main 分支。");
            }

            if (!string.IsNullOrWhiteSpace(await GitWithCancellationAsync(repository, cancellationToken, "status", "--porcelain", "--untracked-files=all")))
            {
                throw new InvalidOperationException("发布仓库存在未提交修改，请先处理后再操作。");
            }

            await RunProcessWithCancellationAsync("gh", repository, GitTimeoutMilliseconds, cancellationToken, "auth", "status");
            await GitWithCancellationAsync(repository, cancellationToken, "fetch", "origin", "main");
            string counts = (await GitWithCancellationAsync(repository, cancellationToken, "rev-list", "--left-right", "--count", "HEAD...FETCH_HEAD")).Trim();
            string[] parts = counts.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2 || !long.TryParse(parts[1], out long remoteCommits) || remoteCommits != 0)
            {
                throw new InvalidOperationException("远端 main 已领先或产生分歧，请人工同步发布仓库后重试。");
            }

            string projectName = resourcePath.Split('/')[1];
            string pendingPaths = await GitWithCancellationAsync(repository, cancellationToken, "diff", "--name-only", "FETCH_HEAD..HEAD");
            foreach (string pendingPath in pendingPaths.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string path = pendingPath.TrimEnd('\r');
                if (path == ".nojekyll" || path == "index.html" ||
                    path == $"unity/{projectName}/hotupdate/PC/.gitkeep" ||
                    path == $"unity/{projectName}/hotupdate/Android/.gitkeep" ||
                    path.StartsWith(resourcePath + "/", StringComparison.Ordinal))
                {
                    continue;
                }

                throw new InvalidOperationException($"本地有其他未推送文件，先人工核对：{path}");
            }
        }

        private static async Task<string> CommitAndPushAsync(string repository, string resourcePath, string operation, string target,
            CancellationToken cancellationToken = default)
        {
            ValidateFlatDirectory(target);
            await GitWithCancellationAsync(repository, cancellationToken, "add", "--", resourcePath);
            var diff = await RunProcessAsync("git", repository, GitTimeoutMilliseconds, false, cancellationToken, "diff", "--cached", "--quiet");
            if (diff.ExitCode != 0 && diff.ExitCode != 1)
            {
                throw new InvalidOperationException("无法检查暂存区，保留当前文件供人工处理。");
            }

            if (diff.ExitCode == 1)
            {
                await GitWithCancellationAsync(repository, cancellationToken, "commit", "-m", $"chore: {operation} {resourcePath}", "--", resourcePath);
            }

            // 已有本地发布提交可能来自上一次失败；无文件变化时仍允许重试推送。
            await RunProcessWithCancellationAsync("git", repository, PushTimeoutMilliseconds, cancellationToken,
                "-c", "credential.helper=", "-c", "credential.helper=!gh auth git-credential",
                "-c", "http.version=HTTP/1.1", "-c", "http.postBuffer=1048576",
                "-c", "http.lowSpeedLimit=1024", "-c", "http.lowSpeedTime=60",
                "push", "origin", "main");
            return "推送完成，等待 Pages 部署";
        }

        internal static async Task<string> GitAsync(string repository, params string[] arguments)
        {
            return await GitWithCancellationAsync(repository, CancellationToken.None, arguments);
        }

        internal static async Task<string> GitWithCancellationAsync(string repository, CancellationToken cancellationToken, params string[] arguments)
        {
            var result = await RunProcessWithCancellationAsync("git", repository, GitTimeoutMilliseconds, cancellationToken, arguments);
            return result.StandardOutput;
        }

        internal static Task<(int ExitCode, string StandardOutput)> RunProcessAsync(
            string fileName,
            string workingDirectory,
            int timeoutMilliseconds,
            params string[] arguments)
        {
            return RunProcessAsync(fileName, workingDirectory, timeoutMilliseconds, true, CancellationToken.None, arguments);
        }

        internal static Task<(int ExitCode, string StandardOutput)> RunProcessWithCancellationAsync(
            string fileName, string workingDirectory, int timeoutMilliseconds, CancellationToken cancellationToken, params string[] arguments)
        {
            return RunProcessAsync(fileName, workingDirectory, timeoutMilliseconds, true, cancellationToken, arguments);
        }

        private static Task<(int ExitCode, string StandardOutput)> RunProcessAsync(
            string fileName,
            string workingDirectory,
            int timeoutMilliseconds,
            bool requireSuccess,
            params string[] arguments)
        {
            return RunProcessAsync(fileName, workingDirectory, timeoutMilliseconds, requireSuccess, CancellationToken.None, arguments);
        }

        private static async Task<(int ExitCode, string StandardOutput)> RunProcessAsync(
            string fileName, string workingDirectory, int timeoutMilliseconds, bool requireSuccess,
            CancellationToken cancellationToken, params string[] arguments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = string.Join(" ", arguments.Select(QuoteArgument)),
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            process.StartInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";
            process.StartInfo.EnvironmentVariables["GH_PROMPT_DISABLED"] = "1";
            if (!process.Start())
            {
                throw new InvalidOperationException($"无法启动 {fileName}，请确认已安装并加入 PATH。");
            }

            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();
            Task waitTask = Task.Run(() => process.WaitForExit());
            Task completion = Task.WhenAll(waitTask, standardOutputTask, standardErrorTask);
            using var timeout = new CancellationTokenSource(timeoutMilliseconds);
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            if (await Task.WhenAny(completion, Task.Delay(Timeout.Infinite, stop.Token)) != completion)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        using var killer = new Process
                        {
                            StartInfo = new ProcessStartInfo
                            {
                                FileName = Path.Combine(Environment.SystemDirectory, "taskkill.exe"),
                                Arguments = "/PID " + process.Id + " /T /F",
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WindowStyle = ProcessWindowStyle.Hidden,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true
                            }
                        };
                        if (!killer.Start())
                        {
                            throw new InvalidOperationException("无法启动本次发布进程树停止操作。");
                        }
                        Task killerCompletion = Task.WhenAll(Task.Run(() => killer.WaitForExit()),
                            killer.StandardOutput.ReadToEndAsync(), killer.StandardError.ReadToEndAsync());
                        if (await Task.WhenAny(killerCompletion, Task.Delay(10000)) != killerCompletion || killer.ExitCode != 0)
                        {
                            throw new InvalidOperationException("本次发布进程树停止失败。");
                        }
                        await killerCompletion;
                    }
                    if (await Task.WhenAny(completion, Task.Delay(5000)) != completion)
                    {
                        throw new InvalidOperationException("无法确认本次发布进程完全停止。");
                    }
                    await completion;
                }
                catch
                {
                    var failure = new InvalidOperationException($"{fileName} 发布进程 PID={process.Id} 停止未确认，不能安全恢复版本入口；保留本地提交及版本备份供人工处理。");
                    failure.Data["SleepyProcessStopUnconfirmed"] = true;
                    throw failure;
                }
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"{fileName} 执行超时，本次进程已终止；本地文件与提交会保留。");
            }

            await completion;
            cancellationToken.ThrowIfCancellationRequested();
            string standardOutput = await standardOutputTask;
            await standardErrorTask;
            if (requireSuccess && process.ExitCode != 0)
            {
                // CLI 输出可能含认证信息，不转发原始输出。
                string operation = arguments.Contains("push") ? "push" : arguments.FirstOrDefault();
                throw new InvalidOperationException($"{fileName} {operation} 执行失败（退出码 {process.ExitCode}）；请检查网络、发布仓库和 GitHub CLI 认证。本地文件与提交会保留。");
            }

            return (process.ExitCode, standardOutput);
        }

        private static string QuoteArgument(string argument)
        {
            var quoted = new StringBuilder("\"");
            int backslashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\')
                {
                    backslashes++;
                    continue;
                }

                quoted.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
                quoted.Append(character);
                backslashes = 0;
            }

            quoted.Append('\\', backslashes * 2);
            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
