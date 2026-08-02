using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Core.Editor.AssetNaming;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Core.Editor.Config
{
    public static class LubanEditorTools
    {
        private const string MenuRoot = "Tools/SleepyDemos/Luban/";

        /// 生成客户端 C#、bytes、JSON 与 Tables 静态访问器。
        [MenuItem(MenuRoot + "生成客户端配置")]
        public static async void GenerateClientConfigs()
        {
            try
            {
                var configRoot = EnsureConfigRepository();
                await EnsureDotnetAsync(configRoot);
                var result = await RunPowerShellScriptAsync(configRoot, "gen.ps1", $"-ParentProjectRoot \"{ProjectRoot}\"");
                LogProcessResult("生成客户端配置", result);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Luban 生成失败，退出码：{result.ExitCode}");
                }

                AssetDatabase.Refresh();
                LubanTemplateClassGenerator.GenerateDefault();
                LoadResourcesYooAssetCollectorSetup.EnsureCollectors();
                LoadResourcesAssetNamingPostprocessor.SyncAllLabels();
                AssetDatabase.Refresh();
                Debug.Log("[Luban] 客户端 C#、bytes、JSON 与 Tables 访问器生成完成。");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// 仅校验策划定义和数据，不替换生成目录。
        [MenuItem(MenuRoot + "仅校验配置")]
        public static async void ValidateConfigs()
        {
            try
            {
                var configRoot = EnsureConfigRepository();
                await EnsureDotnetAsync(configRoot);
                var result = await RunPowerShellScriptAsync(configRoot, "validate.ps1", string.Empty);
                LogProcessResult("校验配置", result);
                if (result.ExitCode != 0)
                {
                    throw new InvalidOperationException($"Luban 校验失败，退出码：{result.ExitCode}");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// 根据当前 GeneratedTables.cs 重新生成 Tables 静态访问器。
        [MenuItem(MenuRoot + "重新生成 Tables 访问器")]
        public static void RegenerateTablesAccessor()
        {
            try
            {
                var changed = LubanTemplateClassGenerator.GenerateDefault();
                Debug.Log(changed
                    ? "[Luban] Tables 访问器已更新。"
                    : "[Luban] Tables 访问器内容未变化，无需写入。");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// 在文件管理器中打开策划配置目录。
        [MenuItem(MenuRoot + "打开策划配置目录")]
        public static void OpenConfigDirectory()
        {
            try
            {
                EditorUtility.RevealInFinder(EnsureConfigRepository());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)?.FullName
                                             ?? throw new InvalidOperationException("无法确定 Unity 项目根目录。");

        private static string EnsureConfigRepository()
        {
            var configRoot = Path.Combine(ProjectRoot, "SleepyConfigs");
            var configPath = Path.Combine(configRoot, "luban.conf");
            var generationScript = Path.Combine(configRoot, "gen.ps1");
            if (!Directory.Exists(configRoot) || !File.Exists(configPath) || !File.Exists(generationScript))
            {
                throw new DirectoryNotFoundException(
                    $"SleepyConfigs 子模块未初始化：{configRoot}。请执行 git submodule update --init --recursive。");
            }

            return configRoot;
        }

        private static async Task EnsureDotnetAsync(string workingDirectory)
        {
            var result = await RunProcessAsync("dotnet", "--version", workingDirectory);
            if (result.ExitCode != 0 || string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                LogProcessResult("检查 dotnet", result);
                throw new InvalidOperationException("未找到可用的 dotnet；Luban v4.10.2 需要 .NET 8。");
            }

            var majorText = result.StandardOutput.Trim().Split('.')[0];
            if (!int.TryParse(majorText, out var majorVersion) || majorVersion < 8)
            {
                throw new InvalidOperationException($"dotnet 版本过低：{result.StandardOutput.Trim()}。Luban v4.10.2 需要 .NET 8。");
            }
        }

        private static Task<ProcessResult> RunPowerShellScriptAsync(
            string workingDirectory,
            string scriptName,
            string arguments)
        {
            var scriptPath = Path.Combine(workingDirectory, scriptName);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException("缺少 Luban PowerShell 脚本。", scriptPath);
            }

            var processArguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"";
            if (!string.IsNullOrWhiteSpace(arguments))
            {
                processArguments += " " + arguments;
            }

            return RunProcessAsync("powershell.exe", processArguments, workingDirectory);
        }

        private static async Task<ProcessResult> RunProcessAsync(
            string fileName,
            string arguments,
            string workingDirectory)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                if (!process.Start())
                {
                    throw new InvalidOperationException($"无法启动进程：{fileName}");
                }

                var standardOutputTask = process.StandardOutput.ReadToEndAsync();
                var standardErrorTask = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                return new ProcessResult(
                    process.ExitCode,
                    await standardOutputTask,
                    await standardErrorTask,
                    $"{fileName} {arguments}");
            }
        }

        private static void LogProcessResult(string operation, ProcessResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                Debug.Log($"[Luban] {operation} 标准输出：\n{result.StandardOutput}");
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                Debug.LogError($"[Luban] {operation} 错误输出：\n{result.StandardError}");
            }

            Debug.Log($"[Luban] {operation} 进程结束，退出码：{result.ExitCode}，命令：{result.CommandLine}");
        }

        private readonly struct ProcessResult
        {
            public ProcessResult(int exitCode, string standardOutput, string standardError, string commandLine)
            {
                ExitCode = exitCode;
                StandardOutput = standardOutput ?? string.Empty;
                StandardError = standardError ?? string.Empty;
                CommandLine = commandLine ?? string.Empty;
            }

            public int ExitCode { get; }
            public string StandardOutput { get; }
            public string StandardError { get; }
            public string CommandLine { get; }
        }
    }
}
