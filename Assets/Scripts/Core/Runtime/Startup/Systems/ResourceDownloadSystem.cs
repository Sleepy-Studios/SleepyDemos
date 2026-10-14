using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    // 临时 Jenkins 主包兼容性拒绝验收，完成后删除。
    public sealed class ResourceDownloadSystem : StartupSystemBase
    {
        public ResourceDownloadSystem(StartupStateBase state) : base(state)
        {
        }

        public override async UniTask ExecuteAsync()
        {
            Report(0f, "检查资源更新");
            var report = await ResourceServices.Default.DownloadPackageAsync(10, 3, progress =>
            {
                var size = progress.TotalBytes <= 0
                    ? "无需下载"
                    : $"{FormatBytes(progress.CurrentBytes)} / {FormatBytes(progress.TotalBytes)} ({progress.CurrentCount}/{progress.TotalCount})";
                Report(progress.Percent, "下载资源补丁", size);
            });

            if (!report.Success)
            {
                Report(0f, $"资源更新失败: {report.Error}");
                throw new System.InvalidOperationException($"资源更新失败: {report.Error}");
            }

            Report(1f, report.TotalCount <= 0 ? "无需下载资源补丁" : "资源补丁下载完成");
        }
        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
            {
                return $"{bytes} B";
            }

            if (bytes < 1024 * 1024)
            {
                return $"{bytes / 1024f:F2} KB";
            }

            return $"{bytes / 1024f / 1024f:F2} MB";
        }
    }
}
