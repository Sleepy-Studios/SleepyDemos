using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    internal sealed class YooAssetResourceLoader : IResourceLoader
    {
        private readonly IResourceService service;
        private readonly List<AssetHandle> handles = new List<AssetHandle>();
        private readonly Dictionary<GameObject, AssetHandle> instanceHandles = new Dictionary<GameObject, AssetHandle>();
        private readonly Dictionary<Object, AssetHandle> assetHandles = new Dictionary<Object, AssetHandle>();
        private readonly HashSet<AssetHandle> pendingHandles = new HashSet<AssetHandle>();
        private bool disposed;

        public YooAssetResourceLoader() : this(ResourceServices.Default)
        {
        }

        public YooAssetResourceLoader(IResourceService service)
        {
            this.service = service;
        }

        public GameObject Instantiate(string address, Transform parent)
        {
            return Instantiate(address, parent, false);
        }

        public GameObject Instantiate(string address, Transform parent, bool worldPositionStays)
        {
            if (disposed)
            {
                return null;
            }
            if (!EnsureInitialized())
            {
                Debug.LogError($"YooAssets 未初始化，无法实例化资源: {address}");
                return null;
            }

            var location = service.NormalizeAddress(address);
            var handle = YooAssetResourceSystem.DefaultPackage.LoadAssetSync<GameObject>(location);
            handles.Add(handle);
            if (handle.Status != EOperationStatus.Succeeded)
            {
                Debug.LogError($"加载 UI 预制体失败: {address}, {handle.Error}");
                handle.Release();
                handles.Remove(handle);
                return null;
            }

            var instantiateOptions = new InstantiateOptions(true, parent, worldPositionStays);
            var instance = handle.InstantiateSync(instantiateOptions);
            if (instance == null)
            {
                Debug.LogError($"实例化 UI 预制体失败: {address}");
                handle.Release();
                handles.Remove(handle);
                return null;
            }

            instanceHandles[instance] = handle;
            return instance;
        }

        public async UniTask<GameObject> InstantiateAsync(string address, Transform parent)
        {
            return await InstantiateAsync(address, parent, false);
        }

        public async UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool worldPositionStays)
        {
            if (disposed)
            {
                return null;
            }
            if (!YooAssetResourceSystem.IsInitialized)
            {
                await service.InitializeAsync(ResourceInitializeOptions.Default);
            }

            if (disposed)
            {
                return null;
            }

            if (!YooAssetResourceSystem.IsInitialized || YooAssetResourceSystem.DefaultPackage == null)
            {
                Debug.LogError($"YooAssets 未初始化，无法实例化资源: {address}");
                return null;
            }

            var location = service.NormalizeAddress(address);
            var handle = YooAssetResourceSystem.DefaultPackage.LoadAssetAsync<GameObject>(location);
            handles.Add(handle);
            pendingHandles.Add(handle);
            bool retained = false;
            try
            {
                await handle;
                if (disposed)
                {
                    return null;
                }
                if (handle.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogError($"加载 UI 预制体失败: {address}, {handle.Error}");
                    return null;
                }
                var operation = handle.InstantiateAsync(new InstantiateOptions(true, parent, worldPositionStays));
                await operation;
                if (disposed)
                {
                    if (operation.Result != null)
                    {
                        Object.Destroy(operation.Result);
                    }
                    return null;
                }
                if (operation.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogError($"实例化 UI 预制体失败: {address}, {operation.Error}");
                    return null;
                }
                if (operation.Result != null)
                {
                    instanceHandles[operation.Result] = handle;
                    retained = true;
                }
                return operation.Result;
            }
            finally
            {
                pendingHandles.Remove(handle);
                if (!retained && handles.Remove(handle))
                {
                    handle.Release();
                }
            }
        }

        public T LoadAsset<T>(string address) where T : Object
        {
            if (disposed)
            {
                return null;
            }
            if (!EnsureInitialized())
            {
                Debug.LogError($"YooAssets 未初始化，无法加载资源: {address}");
                return null;
            }

            var location = service.NormalizeAddress(address);
            var handle = YooAssetResourceSystem.DefaultPackage.LoadAssetSync<T>(location);
            handles.Add(handle);
            if (handle.Status != EOperationStatus.Succeeded)
            {
                Debug.LogError($"加载资源失败: {address}, {handle.Error}");
                handle.Release();
                handles.Remove(handle);
                return null;
            }

            var asset = handle.GetAssetObject<T>();
            if (asset != null)
            {
                assetHandles[asset] = handle;
            }

            return asset;
        }

        public async UniTask<T> LoadAssetAsync<T>(string address) where T : Object
        {
            if (disposed)
            {
                return null;
            }
            if (!YooAssetResourceSystem.IsInitialized)
            {
                await service.InitializeAsync(ResourceInitializeOptions.Default);
            }

            if (disposed)
            {
                return null;
            }

            if (!YooAssetResourceSystem.IsInitialized || YooAssetResourceSystem.DefaultPackage == null)
            {
                Debug.LogError($"YooAssets 未初始化，无法加载资源: {address}");
                return null;
            }

            var location = service.NormalizeAddress(address);
            var handle = YooAssetResourceSystem.DefaultPackage.LoadAssetAsync<T>(location);
            handles.Add(handle);
            pendingHandles.Add(handle);
            bool retained = false;
            try
            {
                await handle;
                if (disposed)
                {
                    return null;
                }
                if (handle.Status != EOperationStatus.Succeeded)
                {
                    Debug.LogError($"加载资源失败: {address}, {handle.Error}");
                    return null;
                }
                var asset = handle.GetAssetObject<T>();
                if (asset != null)
                {
                    assetHandles[asset] = handle;
                    retained = true;
                }
                return asset;
            }
            finally
            {
                pendingHandles.Remove(handle);
                if (!retained && handles.Remove(handle))
                {
                    handle.Release();
                }
            }
        }

        private bool EnsureInitialized()
        {
            if (!YooAssetResourceSystem.IsInitialized)
            {
                service.InitializeAsync(ResourceInitializeOptions.Default).GetAwaiter().GetResult();
            }

            return YooAssetResourceSystem.IsInitialized && YooAssetResourceSystem.DefaultPackage != null;
        }

        public void ReleaseAsset(Object asset)
        {
            if (asset == null)
            {
                return;
            }

            if (assetHandles.TryGetValue(asset, out var handle))
            {
                assetHandles.Remove(asset);
                handle.Release();
                handles.Remove(handle);
            }
        }

        public void ReleaseInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instanceHandles.TryGetValue(instance, out var handle))
            {
                instanceHandles.Remove(instance);
                Object.Destroy(instance);
                handle.Release();
                handles.Remove(handle);
                return;
            }

            Object.Destroy(instance);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            foreach (var pair in instanceHandles)
            {
                if (pair.Key != null)
                {
                    Object.Destroy(pair.Key);
                }
            }
            instanceHandles.Clear();
            assetHandles.Clear();

            for (int i = handles.Count - 1; i >= 0; i--)
            {
                // 在途操作保留引用直到 finally，避免 Provider 因零引用挂起或迟到实例失去所有者。
                if (!pendingHandles.Contains(handles[i]))
                {
                    handles[i]?.Release();
                    handles.RemoveAt(i);
                }
            }
        }

    }
}
