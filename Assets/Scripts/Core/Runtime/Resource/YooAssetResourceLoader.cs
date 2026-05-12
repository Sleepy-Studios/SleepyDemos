using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YooAsset;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    public sealed class YooAssetResourceLoader : IResourceLoader
    {
        private readonly List<AssetHandle> handles = new List<AssetHandle>();
        private readonly Dictionary<GameObject, AssetHandle> instanceHandles = new Dictionary<GameObject, AssetHandle>();

        public async UniTask<GameObject> InstantiateAsync(string address, Transform parent)
        {
            if (!YooAssetResourceSystem.IsInitialized)
            {
                await YooAssetResourceSystem.InitializeAsync();
            }

            if (!YooAssetResourceSystem.IsInitialized || YooAssetResourceSystem.DefaultPackage == null)
            {
                Debug.LogError($"YooAssets 未初始化，无法实例化资源: {address}");
                return null;
            }

            var location = NormalizeLocation(address);
            var handle = YooAssetResourceSystem.DefaultPackage.LoadAssetAsync<GameObject>(location);
            handles.Add(handle);
            await handle.Task.AsUniTask();

            if (handle.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"加载 UI 预制体失败: {address}, {handle.LastError}");
                return null;
            }

            var operation = handle.InstantiateAsync(parent);
            await operation.Task.AsUniTask();
            if (operation.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"实例化 UI 预制体失败: {address}, {operation.Error}");
                return null;
            }

            if (operation.Result != null)
            {
                instanceHandles[operation.Result] = handle;
            }

            return operation.Result;
        }

        public async UniTask<T> LoadAssetAsync<T>(string address) where T : Object
        {
            if (!YooAssetResourceSystem.IsInitialized)
            {
                await YooAssetResourceSystem.InitializeAsync();
            }

            if (!YooAssetResourceSystem.IsInitialized || YooAssetResourceSystem.DefaultPackage == null)
            {
                Debug.LogError($"YooAssets 未初始化，无法加载资源: {address}");
                return null;
            }

            var location = NormalizeLocation(address);
            var handle = YooAssetResourceSystem.DefaultPackage.LoadAssetAsync<T>(location);
            handles.Add(handle);
            await handle.Task.AsUniTask();
            if (handle.Status != EOperationStatus.Succeed)
            {
                Debug.LogError($"加载资源失败: {address}, {handle.LastError}");
                return null;
            }

            return handle.GetAssetObject<T>();
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
            foreach (var pair in instanceHandles)
            {
                if (pair.Key != null)
                {
                    Object.Destroy(pair.Key);
                }
            }
            instanceHandles.Clear();

            for (int i = handles.Count - 1; i >= 0; i--)
            {
                handles[i]?.Release();
            }
            handles.Clear();
        }

        private static string NormalizeLocation(string address)
        {
            if (string.IsNullOrEmpty(address))
            {
                return address;
            }

            return address.Replace('\\', '/');
        }
    }
}
