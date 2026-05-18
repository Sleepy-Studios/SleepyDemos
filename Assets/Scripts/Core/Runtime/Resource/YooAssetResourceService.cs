using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    internal sealed class YooAssetResourceService : IResourceService
    {
        private readonly YooAssetResourceLoader sharedLoader;

        public YooAssetResourceService()
        {
            sharedLoader = new YooAssetResourceLoader(this);
        }

        public bool IsInitialized => YooAssetResourceSystem.IsInitialized;

        public string NormalizeAddress(string address)
        {
            return YooAssetResourceSystem.NormalizeLocation(address);
        }

        public UniTask InitializeAsync(ResourceInitializeOptions options)
        {
            return YooAssetResourceSystem.InitializeAsync(options.PackageName, options.PlayMode, options.HostServerURL);
        }

        public UniTask<DownloadReport> DownloadPackageAsync(int downloadingMaxNum, int failedTryAgain, Action<DownloadProgress> onProgress = null)
        {
            return YooAssetResourceSystem.DownloadPackageAsync(downloadingMaxNum, failedTryAgain, onProgress);
        }

        public IResourceLoader CreateLoader()
        {
            return new YooAssetResourceLoader(this);
        }

        public async UniTask<ResourceLoadResult<T>> LoadAssetAsync<T>(string address) where T : Object
        {
            var asset = await sharedLoader.LoadAssetAsync<T>(address);
            return asset != null
                ? ResourceLoadResult<T>.SuccessResult(asset, NormalizeAddress(address))
                : ResourceLoadResult<T>.Failure(NormalizeAddress(address), $"资源加载失败: {address}");
        }

        public async UniTask<ResourceLoadResult<TextAsset>> LoadTextAssetAsync(string address)
        {
            var asset = await sharedLoader.LoadAssetAsync<TextAsset>(address);
            return asset != null
                ? ResourceLoadResult<TextAsset>.SuccessResult(asset, NormalizeAddress(address))
                : ResourceLoadResult<TextAsset>.Failure(NormalizeAddress(address), $"TextAsset 加载失败: {address}");
        }

        public void ReleaseAsset(Object asset)
        {
            sharedLoader.ReleaseAsset(asset);
        }
    }
}
