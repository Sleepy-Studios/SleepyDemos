using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HybridCLR;
using UnityEngine;

namespace Core.Runtime
{
    public static class HybridAotAssemblyLoader
    {
        public static async UniTask LoadMetadataAsync(IEnumerable<string> aotAssemblies)
        {
            await LoadMetadataInternalAsync(aotAssemblies, HomologousImageMode.SuperSet);
        }

        private static async UniTask LoadMetadataInternalAsync(IEnumerable<string> aotAssemblies, HomologousImageMode mode)
        {
            if (aotAssemblies == null)
            {
                return;
            }

            foreach (var assemblyName in aotAssemblies)
            {
                if (string.IsNullOrWhiteSpace(assemblyName))
                {
                    continue;
                }

                var result = await ResourceServices.Default.LoadTextAssetAsync(assemblyName);
                if (!result.Success)
                {
                    Debug.LogWarning($"[HybridCLR] 跳过 AOT 元数据，未找到资源: {assemblyName}");
                    continue;
                }

                var bytes = result.Asset.bytes;
                LoadImageErrorCode errorCode;
                try
                {
                    errorCode = RuntimeApi.LoadMetadataForAOTAssembly(bytes, mode);
                }
                finally
                {
                    ResourceServices.Default.ReleaseAsset(result.Asset);
                }

                Debug.Log($"[HybridCLR] 加载 AOT 元数据: {assemblyName}, mode: {mode}, result: {errorCode}, size: {FormatBytes(bytes.Length)}");
            }
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
