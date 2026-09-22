using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Core.Editor.Streamline
{
    internal sealed class StreamlineBuildProcessor : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string PluginFolder = "Assets/Plugins/Streamline/x86_64";
        private static readonly string[] RuntimeFiles =
        {
            "sl.interposer.dll", "sl.common.dll", "sl.pcl.dll", "sl.dlss.dll", "sl.reflex.dll",
            "sl.dlss_g.dll", "sl.dlss_d.dll", "nvngx_dlss.dll", "nvngx_dlssg.dll", "nvngx_dlssd.dll",
            "NvLowLatencyVk.dll", "nvngx_dlss.license.txt", "reflex.license.txt", "streamline.license.txt"
        };

        /// 在普通构建阶段检查依赖，构建完成后复制官方运行库。
        public int callbackOrder => 0;

        /// <summary>Windows x64 构建部署了桥接时，检查原生依赖是否齐全。</summary>
        /// <param name="report">Unity 当前构建报告；缺少依赖时中止构建。</param>
        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            // 未部署桥接时普通游戏构建仍可使用原有渲染；部署后要求依赖完整。
            if (!File.Exists(Path.Combine(PluginFolder, "GfxPluginSleepyStreamline.dll"))) return;
            foreach (string file in RuntimeFiles)
            {
                if (!File.Exists(Path.Combine(PluginFolder, "Streamline~", file)))
                    throw new BuildFailedException("Streamline 依赖缺失：" + file + "。请运行 scripts/streamline/Build-Bridge.ps1 -Deploy。");
            }
        }

        /// <summary>将官方 production DLL 与许可证复制到桥接旁的独立目录。</summary>
        /// <param name="report">Unity 当前构建报告，提供目标平台与输出位置。</param>
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.StandaloneWindows64) return;
            if (!File.Exists(Path.Combine(PluginFolder, "GfxPluginSleepyStreamline.dll"))) return;
            string output = report.summary.outputPath;
            string dataFolder = Path.Combine(Path.GetDirectoryName(output), Path.GetFileNameWithoutExtension(output) + "_Data");
            string destination = Path.Combine(dataFolder, "Plugins", "x86_64", "Streamline~");
            Directory.CreateDirectory(destination);
            foreach (string file in RuntimeFiles)
                File.Copy(Path.Combine(PluginFolder, "Streamline~", file), Path.Combine(destination, file), true);
            Debug.Log("Streamline production 依赖已复制到：" + destination);
        }
    }
}
