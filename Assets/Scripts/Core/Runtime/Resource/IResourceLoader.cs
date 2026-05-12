using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    public interface IResourceLoader : IDisposable
    {
        UniTask<GameObject> InstantiateAsync(string address, Transform parent);
        UniTask<T> LoadAssetAsync<T>(string address) where T : Object;
        void ReleaseInstance(GameObject instance);
    }
}
