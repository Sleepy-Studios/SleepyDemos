using System;
using System.Collections.Generic;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Luban;
using UnityEngine;

namespace Cfg
{
    /// Luban 业务配置的幂等初始化入口。
    public static class LubanConfigService
    {
        /// Luban 二进制配置的资源目录地址。
        public const string ResourceBaseAddress = "LoadResources/Config/Luban";

        private static UniTaskCompletionSource initializationSource;

        /// 配置是否已经完整加载并发布给静态访问器。
        public static bool IsInitialized => Tables.IsInitialized;

        /// 初始化全部 Luban 客户端配置；并发或重复调用共享同一次初始化。
        public static UniTask InitializeAsync()
        {
            if (Tables.IsInitialized)
            {
                return UniTask.CompletedTask;
            }

            if (initializationSource != null)
            {
                return initializationSource.Task;
            }

            var completionSource = new UniTaskCompletionSource();
            initializationSource = completionSource;
            InitializeCoreAsync(completionSource).Forget();
            return completionSource.Task;
        }

        private static async UniTaskVoid InitializeCoreAsync(UniTaskCompletionSource completionSource)
        {
            try
            {
                var generatedTables = await LoadFromResourcesAsync();
                Tables.SetInstance(generatedTables);
                completionSource.TrySetResult();
            }
            catch (Exception exception)
            {
                Tables.ResetAfterFailure();
                if (ReferenceEquals(initializationSource, completionSource))
                {
                    initializationSource = null;
                }

                completionSource.TrySetException(exception);
            }
        }

        private static async UniTask<GeneratedTables> LoadFromResourcesAsync()
        {
            using (var loader = ResourceServices.CreateLoader())
            {
                return await LoadTablesAsync(async (tableName, address) =>
                {
                    TextAsset asset;
                    try
                    {
                        asset = await loader.LoadAssetAsync<TextAsset>(address);
                    }
                    catch (Exception exception)
                    {
                        throw CreateLoadException(tableName, address, "IResourceLoader 抛出异常。", exception);
                    }

                    if (asset == null)
                    {
                        throw CreateLoadException(tableName, address, "IResourceLoader 返回空 TextAsset。", null);
                    }

                    return asset.bytes;
                });
            }
        }

        internal static async UniTask<GeneratedTables> LoadTablesAsync(
            Func<string, string, UniTask<byte[]>> loadBytesAsync)
        {
            if (loadBytesAsync == null)
            {
                throw new ArgumentNullException(nameof(loadBytesAsync));
            }

            var buffers = new Dictionary<string, byte[]>(Tables.TableCount, StringComparer.Ordinal);
            for (var index = 0; index < Tables.DataFileNames.Count; index++)
            {
                var tableName = Tables.DataFileNames[index];
                var address = GetResourceAddress(tableName);
                byte[] bytes;
                try
                {
                    bytes = await loadBytesAsync(tableName, address);
                }
                catch (Exception exception) when (!(exception is InvalidOperationException))
                {
                    throw CreateLoadException(tableName, address, "读取 bytes 失败。", exception);
                }

                if (bytes == null || bytes.Length == 0)
                {
                    throw CreateLoadException(tableName, address, "资源 bytes 为空。", null);
                }

                buffers.Add(tableName, bytes);
                Tables.SetLoadProgress((index + 1f) / Tables.TableCount);
            }

            try
            {
                return Tables.CreateGeneratedTables(tableName =>
                {
                    if (!buffers.TryGetValue(tableName, out var bytes))
                    {
                        throw CreateLoadException(
                            tableName,
                            GetResourceAddress(tableName),
                            "GeneratedTables 请求了未预加载的数据文件。",
                            null);
                    }

                    return new ByteBuf(bytes);
                });
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Luban 配置反序列化失败。资源目录：{ResourceBaseAddress}，表文件：{string.Join(", ", Tables.DataFileNames)}。",
                    exception);
            }
        }

        internal static string GetResourceAddress(string tableName)
        {
            return $"{ResourceBaseAddress}/{tableName}";
        }

        internal static void ResetForTests()
        {
            initializationSource = null;
            Tables.ResetAfterFailure();
        }

        private static InvalidOperationException CreateLoadException(
            string tableName,
            string address,
            string detail,
            Exception innerException)
        {
            return new InvalidOperationException(
                $"Luban 配置加载失败。表：{tableName}，资源地址：{address}。{detail}",
                innerException);
        }
    }
}
