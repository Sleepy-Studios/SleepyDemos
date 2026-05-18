using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    public interface IResourceService
    {
        bool IsInitialized { get; }
        string NormalizeAddress(string address);
        UniTask InitializeAsync(ResourceInitializeOptions options);
        UniTask<DownloadReport> DownloadPackageAsync(int downloadingMaxNum, int failedTryAgain, Action<DownloadProgress> onProgress = null);
        IResourceLoader CreateLoader();
        UniTask<ResourceLoadResult<T>> LoadAssetAsync<T>(string address) where T : Object;
        UniTask<ResourceLoadResult<TextAsset>> LoadTextAssetAsync(string address);
        void ReleaseAsset(Object asset);
    }
}
