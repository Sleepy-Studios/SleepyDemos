using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;
using YooSceneHandle = YooAsset.SceneHandle;

namespace Core.Runtime
{
    internal sealed class YooAssetResourceSceneLoader : IResourceSceneLoader
    {
        private sealed class YooAssetSceneHandle : IResourceSceneHandle
        {
            internal YooAssetSceneHandle(string address, YooSceneHandle handle)
            {
                Address = address;
                Handle = handle;
            }

            public string Address { get; }
            public Scene Scene => Handle.SceneObject;
            internal YooSceneHandle Handle { get; }
        }

        private readonly IResourceService service;
        private readonly HashSet<YooAssetSceneHandle> handles = new HashSet<YooAssetSceneHandle>();

        internal YooAssetResourceSceneLoader(IResourceService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public async UniTask<ResourceSceneLoadResult> LoadSceneAsync(
            string address,
            LoadSceneMode loadMode = LoadSceneMode.Additive,
            Action<float> onProgress = null)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return ResourceSceneLoadResult.Failure("场景资源地址为空。");
            }

            if (!YooAssetResourceSystem.IsInitialized)
            {
                await service.InitializeAsync(ResourceInitializeOptions.Default);
            }

            var package = YooAssetResourceSystem.DefaultPackage;
            if (!YooAssetResourceSystem.IsInitialized || package == null)
            {
                return ResourceSceneLoadResult.Failure($"YooAssets 未初始化，无法加载场景: {address}");
            }

            var location = service.NormalizeAddress(address);
            var sceneHandle = package.LoadSceneAsync(location, loadMode);
            try
            {
                onProgress?.Invoke(0f);
                while (!sceneHandle.IsDone)
                {
                    onProgress?.Invoke(Mathf.Clamp01(sceneHandle.Progress));
                    await UniTask.Yield();
                }

                await sceneHandle;
                onProgress?.Invoke(Mathf.Clamp01(sceneHandle.Progress));
                if (sceneHandle.Status != EOperationStatus.Succeeded)
                {
                    var error = sceneHandle.Error;
                    sceneHandle.Release();
                    return ResourceSceneLoadResult.Failure(
                        $"场景加载失败: {address}, {error}");
                }

                var wrapper = new YooAssetSceneHandle(location, sceneHandle);
                handles.Add(wrapper);
                onProgress?.Invoke(1f);
                return ResourceSceneLoadResult.Success(wrapper);
            }
            catch (Exception exception)
            {
                if (sceneHandle.IsValid)
                {
                    sceneHandle.Release();
                }

                return ResourceSceneLoadResult.Failure(
                    $"场景加载异常: {address}, {exception.Message}");
            }
        }

        public async UniTask<ResourceSceneUnloadResult> UnloadSceneAsync(
            IResourceSceneHandle handle,
            Action<float> onProgress = null)
        {
            if (!(handle is YooAssetSceneHandle yooHandle) || !handles.Contains(yooHandle))
            {
                return ResourceSceneUnloadResult.Failure("场景句柄不属于当前资源场景加载器或已经卸载。");
            }

            try
            {
                var operation = yooHandle.Handle.UnloadSceneAsync();
                onProgress?.Invoke(0f);
                while (!operation.IsDone)
                {
                    onProgress?.Invoke(Mathf.Clamp01(operation.Progress));
                    await UniTask.Yield();
                }

                await operation;
                onProgress?.Invoke(Mathf.Clamp01(operation.Progress));
                if (operation.Status != EOperationStatus.Succeeded)
                {
                    return ResourceSceneUnloadResult.Failure(
                        $"场景卸载失败: {yooHandle.Address}, {operation.Error}");
                }

                handles.Remove(yooHandle);
                onProgress?.Invoke(1f);
                return ResourceSceneUnloadResult.Success();
            }
            catch (Exception exception)
            {
                return ResourceSceneUnloadResult.Failure(
                    $"场景卸载异常: {yooHandle.Address}, {exception.Message}");
            }
        }
    }
}
