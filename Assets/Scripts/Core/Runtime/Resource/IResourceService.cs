using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    /// 项目运行时资源服务抽象，屏蔽业务层对具体资源框架的依赖。
    public interface IResourceService
    {
        /// 当前默认资源包是否已经完成初始化并拥有可用清单。
        bool IsInitialized { get; }

        /// <summary>
        /// 标准化资源地址。
        /// </summary>
        /// <param name="address">外部传入的资源地址。</param>
        /// <returns>可传给底层资源系统的标准地址。</returns>
        string NormalizeAddress(string address);

        /// <summary>
        /// 初始化默认资源服务。
        /// </summary>
        /// <param name="options">初始化选项，包含包名、运行模式和远端地址。</param>
        /// <returns>初始化异步任务。</returns>
        UniTask InitializeAsync(ResourceInitializeOptions options);

        /// <summary>
        /// 下载默认资源包的远端资源。
        /// </summary>
        /// <param name="downloadingMaxNum">最大并发下载数量。</param>
        /// <param name="failedTryAgain">单个文件失败重试次数。</param>
        /// <param name="onProgress">下载进度回调；为空时不回调。</param>
        /// <returns>下载结果报告。</returns>
        UniTask<DownloadReport> DownloadPackageAsync(int downloadingMaxNum, int failedTryAgain, Action<DownloadProgress> onProgress = null);

        /// 创建独立资源加载器。调用方负责在生命周期结束时释放。
        IResourceLoader CreateLoader();

        /// <summary>
        /// 同步加载指定地址资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="address">资源地址。</param>
        /// <returns>资源加载结果。</returns>
        ResourceLoadResult<T> LoadAsset<T>(string address) where T : Object;

        /// <summary>
        /// 异步加载指定地址资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="address">资源地址。</param>
        /// <returns>资源加载结果。</returns>
        UniTask<ResourceLoadResult<T>> LoadAssetAsync<T>(string address) where T : Object;

        /// <summary>
        /// 异步加载文本资源。
        /// </summary>
        /// <param name="address">文本资源地址。</param>
        /// <returns>文本资源加载结果。</returns>
        UniTask<ResourceLoadResult<TextAsset>> LoadTextAssetAsync(string address);

        /// <summary>
        /// 释放由默认资源服务加载出的资源。
        /// </summary>
        /// <param name="asset">需要释放的资源对象。</param>
        void ReleaseAsset(Object asset);
    }
}
