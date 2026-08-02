using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    internal static class YooAssetResourceSystem
    {
        public const string DefaultPackageName = ResourceInitializeOptions.DefaultPackageName;

        private static bool initializing;
        private static bool packageInitialized;
        private static UniTask initializeTask;
        private static string initializedPackageName;
        private static ResourcePlayMode initializedPlayMode;

        public static bool IsInitialized { get; private set; }
        public static ResourcePackage DefaultPackage { get; private set; }

        public static UniTask InitializeAsync(
            string packageName = DefaultPackageName,
            ResourcePlayMode playMode = ResourcePlayMode.EditorSimulateMode,
            string hostServerURL = null)
        {
            return InitializeInternalWithPlayModeAsync(packageName, playMode, ToYooAssetPlayMode(playMode), hostServerURL);
        }

        private static UniTask InitializeInternalWithPlayModeAsync(
            string packageName,
            ResourcePlayMode requestedPlayMode,
#if UNITY_EDITOR
            EPlayMode playMode = EPlayMode.EditorSimulateMode,
#else
            EPlayMode playMode = EPlayMode.OfflinePlayMode,
#endif
            string hostServerURL = null)
        {
            packageName = string.IsNullOrWhiteSpace(packageName) ? DefaultPackageName : packageName;
            if (IsInitialized && initializedPackageName == packageName && initializedPlayMode == requestedPlayMode)
            {
                return UniTask.CompletedTask;
            }

            if (initializing)
            {
                return initializeTask;
            }

            initializing = true;
            initializeTask = packageInitialized && initializedPackageName == packageName && initializedPlayMode == requestedPlayMode
                ? RefreshManifestInternalAsync()
                : InitializeInternalAsync(packageName, requestedPlayMode, playMode, hostServerURL);
            return initializeTask;
        }

        private static EPlayMode ToYooAssetPlayMode(ResourcePlayMode playMode)
        {
            switch (playMode)
            {
                case ResourcePlayMode.HostPlayMode:
                    return EPlayMode.HostPlayMode;
                case ResourcePlayMode.OfflinePlayMode:
                    return EPlayMode.OfflinePlayMode;
                case ResourcePlayMode.EditorSimulateMode:
                default:
                    return EPlayMode.EditorSimulateMode;
            }
        }

        public static async UniTask<TextAsset> LoadTextAssetAsync(string location)
        {
            if (!IsInitialized)
            {
                await InitializeAsync();
            }

            if (!IsInitialized || DefaultPackage == null)
            {
                Debug.LogError($"YooAssets 未初始化，无法加载 TextAsset: {location}");
                return null;
            }

            var handle = DefaultPackage.LoadAssetAsync<TextAsset>(NormalizeLocation(location));
            await handle;
            if (handle.Status != EOperationStatus.Succeeded)
            {
                Debug.LogWarning($"YooAssets 加载 TextAsset 失败: {location}, {handle.Error}");
                handle.Release();
                return null;
            }

            var asset = handle.AssetObject as TextAsset;
            handle.Release();
            return asset;
        }

        public static async UniTask<T> LoadAssetAsync<T>(string location) where T : Object
        {
            if (!IsInitialized)
            {
                await InitializeAsync();
            }

            if (!IsInitialized || DefaultPackage == null)
            {
                Debug.LogError($"YooAssets 未初始化，无法加载资源: {location}");
                return null;
            }

            var handle = DefaultPackage.LoadAssetAsync<T>(NormalizeLocation(location));
            await handle;
            if (handle.Status != EOperationStatus.Succeeded)
            {
                Debug.LogWarning($"YooAssets 加载资源失败: {location}, {handle.Error}");
                handle.Release();
                return null;
            }

            var asset = handle.GetAssetObject<T>();
            handle.Release();
            return asset;
        }

        public static void ReleaseAsset(Object asset)
        {
            if (asset != null)
            {
                Resources.UnloadAsset(asset);
            }
        }

        public static async UniTask<DownloadReport> DownloadPackageAsync(
            int downloadingMaxNum,
            int failedTryAgain,
            System.Action<DownloadProgress> onProgress = null)
        {
            if (!IsInitialized)
            {
                await InitializeAsync();
            }

            if (!IsInitialized || DefaultPackage == null)
            {
                return new DownloadReport(false, 0, 0, "YooAssets 未初始化。");
            }

            var downloaderOptions = new ResourceDownloaderOptions(downloadingMaxNum, failedTryAgain);
            var downloader = DefaultPackage.CreateResourceDownloader(downloaderOptions);
            if (downloader.TotalDownloadCount == 0)
            {
                onProgress?.Invoke(new DownloadProgress(0, 0, 0, 0));
                return new DownloadReport(true, 0, 0, string.Empty);
            }

            var progress = new DownloadProgress(
                downloader.TotalDownloadCount,
                downloader.TotalDownloadBytes,
                0,
                0);
            onProgress?.Invoke(progress);
            downloader.DownloadProgressChanged += data =>
            {
                progress = new DownloadProgress(
                    data.TotalDownloadCount,
                    data.TotalDownloadBytes,
                    data.CurrentDownloadCount,
                    data.CurrentDownloadBytes);
                onProgress?.Invoke(progress);
            };

            downloader.StartDownload();
            while (!downloader.IsDone)
            {
                await UniTask.Yield();
            }

            return new DownloadReport(
                downloader.Status == EOperationStatus.Succeeded,
                downloader.TotalDownloadCount,
                downloader.TotalDownloadBytes,
                downloader.Status == EOperationStatus.Succeeded ? string.Empty : downloader.Error);
        }

        private static async UniTask InitializeInternalAsync(string packageName, ResourcePlayMode requestedPlayMode, EPlayMode playMode, string hostServerURL)
        {
            if (!YooAssets.IsInitialized)
            {
                YooAssets.Initialize();
            }

            if (!YooAssets.TryGetPackage(packageName, out var package))
            {
                package = YooAssets.CreatePackage(packageName);
            }

            DefaultPackage = package;
            var options = CreateInitializeOptions(DefaultPackage, playMode, hostServerURL);
            var operation = DefaultPackage.InitializePackageAsync(options);
            await operation;
            if (operation.Status != EOperationStatus.Succeeded)
            {
                initializing = false;
                Debug.LogError($"YooAssets 初始化失败: {operation.Error}");
                return;
            }

            packageInitialized = true;
            initializedPackageName = packageName;
            initializedPlayMode = requestedPlayMode;

            await LoadPackageManifestAsync();
            initializing = false;
            if (!IsInitialized)
            {
                Debug.LogError("YooAssets 初始化未获得有效资源清单，资源加载将不可用。");
            }
        }

        private static async UniTask RefreshManifestInternalAsync()
        {
            await LoadPackageManifestAsync();
            initializing = false;
        }

        private static async UniTask LoadPackageManifestAsync()
        {
            IsInitialized = false;
            var versionOperation = DefaultPackage.RequestPackageVersionAsync();
            await versionOperation;
            if (versionOperation.Status != EOperationStatus.Succeeded)
            {
                Debug.LogError($"YooAssets 请求资源版本失败: {versionOperation.Error}");
                return;
            }

            var manifestOptions = new LoadPackageManifestOptions(versionOperation.PackageVersion, 60);
            var manifestOperation = DefaultPackage.LoadPackageManifestAsync(manifestOptions);
            await manifestOperation;
            if (manifestOperation.Status != EOperationStatus.Succeeded)
            {
                Debug.LogError($"YooAssets 更新资源清单失败: {manifestOperation.Error}");
                return;
            }

            IsInitialized = true;
        }

        private static InitializePackageOptions CreateInitializeOptions(ResourcePackage package, EPlayMode playMode, string hostServerURL)
        {
            switch (playMode)
            {
                case EPlayMode.EditorSimulateMode:
#if UNITY_EDITOR
                    var buildResult = EditorSimulateBuildInvoker.Build(package.PackageName, (int)EBundleType.VirtualAssetBundle);
                    return new EditorSimulateModeOptions
                    {
                        EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory)
                    };
#else
                    return new OfflinePlayModeOptions
                    {
                        BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
                    };
#endif
                case EPlayMode.HostPlayMode:
                    var remoteService = new RemoteService(hostServerURL, hostServerURL);
                    var hostOptions = new HostPlayModeOptions
                    {
                        BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters(),
                        CacheFileSystemParameters = FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService)
                    };
                    hostOptions.BuiltinFileSystemParameters.AddParameter(EFileSystemParameter.CopyBuiltinPackageManifest, true);
                    return hostOptions;
                case EPlayMode.OfflinePlayMode:
                default:
                    return new OfflinePlayModeOptions
                    {
                        BuiltinFileSystemParameters = FileSystemParameters.CreateDefaultBuiltinFileSystemParameters()
                    };
            }
        }

        public static string NormalizeLocation(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return location;
            }

            return location.Replace('\\', '/');
        }

        private sealed class RemoteService : IRemoteService
        {
            private readonly string defaultHostServer;
            private readonly string fallbackHostServer;

            public RemoteService(string defaultHostServer, string fallbackHostServer)
            {
                this.defaultHostServer = defaultHostServer ?? string.Empty;
                this.fallbackHostServer = fallbackHostServer ?? string.Empty;
            }

            IReadOnlyList<string> IRemoteService.GetRemoteUrls(string fileName)
            {
                return new[]
                {
                    $"{defaultHostServer}/{fileName}",
                    $"{fallbackHostServer}/{fileName}"
                };
            }
        }
    }

    public readonly struct DownloadProgress
    {
        public DownloadProgress(int totalCount, long totalBytes, int currentCount, long currentBytes)
        {
            TotalCount = totalCount;
            TotalBytes = totalBytes;
            CurrentCount = currentCount;
            CurrentBytes = currentBytes;
        }

        public int TotalCount { get; }
        public long TotalBytes { get; }
        public int CurrentCount { get; }
        public long CurrentBytes { get; }
        public float Percent => TotalBytes <= 0 ? 1f : Mathf.Clamp01(CurrentBytes / (float)TotalBytes);
    }

    public readonly struct DownloadReport
    {
        public DownloadReport(bool success, int totalCount, long totalBytes, string error)
        {
            Success = success;
            TotalCount = totalCount;
            TotalBytes = totalBytes;
            Error = error;
        }

        public bool Success { get; }
        public int TotalCount { get; }
        public long TotalBytes { get; }
        public string Error { get; }
    }
}
