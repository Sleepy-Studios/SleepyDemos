using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Core.Runtime
{
    /// 资源加载器实例，负责跟踪由自己加载或实例化的资源，并在释放时统一回收句柄。
    public interface IResourceLoader : IDisposable
    {
        /// <summary>
        /// 同步实例化指定地址的 GameObject，并挂到目标父节点下。
        /// </summary>
        /// <param name="address">资源地址，会通过当前资源服务标准化。</param>
        /// <param name="parent">实例挂载父节点。</param>
        /// <returns>实例化成功的对象；加载或实例化失败时返回 null。</returns>
        GameObject Instantiate(string address, Transform parent);

        /// <summary>
        /// 同步实例化指定地址的 GameObject，并挂到目标父节点下。
        /// </summary>
        /// <param name="address">资源地址，会通过当前资源服务标准化。</param>
        /// <param name="parent">实例挂载父节点。</param>
        /// <param name="worldPositionStays">是否保持世界坐标。</param>
        /// <returns>实例化成功的对象；加载或实例化失败时返回 null。</returns>
        GameObject Instantiate(string address, Transform parent, bool worldPositionStays);

        /// <summary>
        /// 异步实例化指定地址的 GameObject，并挂到目标父节点下。
        /// </summary>
        /// <param name="address">资源地址，会通过当前资源服务标准化。</param>
        /// <param name="parent">实例挂载父节点。</param>
        /// <returns>实例化成功的对象；加载或实例化失败时返回 null。</returns>
        UniTask<GameObject> InstantiateAsync(string address, Transform parent);

        /// <summary>
        /// 异步实例化指定地址的 GameObject，并挂到目标父节点下。
        /// </summary>
        /// <param name="address">资源地址，会通过当前资源服务标准化。</param>
        /// <param name="parent">实例挂载父节点。</param>
        /// <param name="worldPositionStays">是否保持世界坐标。</param>
        /// <returns>实例化成功的对象；加载或实例化失败时返回 null。</returns>
        UniTask<GameObject> InstantiateAsync(string address, Transform parent, bool worldPositionStays);

        /// <summary>
        /// 同步加载指定地址的资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="address">资源地址，会通过当前资源服务标准化。</param>
        /// <returns>加载成功的资源对象；失败时返回 null。</returns>
        T LoadAsset<T>(string address) where T : Object;

        /// <summary>
        /// 异步加载指定地址的资源。
        /// </summary>
        /// <typeparam name="T">资源类型。</typeparam>
        /// <param name="address">资源地址，会通过当前资源服务标准化。</param>
        /// <returns>加载成功的资源对象；失败时返回 null。</returns>
        UniTask<T> LoadAssetAsync<T>(string address) where T : Object;

        /// <summary>
        /// 释放由当前加载器加载出的资源对象。
        /// </summary>
        /// <param name="asset">需要释放的资源对象。</param>
        void ReleaseAsset(Object asset);

        /// <summary>
        /// 释放由当前加载器实例化出的 GameObject。
        /// </summary>
        /// <param name="instance">需要释放的实例对象。</param>
        void ReleaseInstance(GameObject instance);
    }
}
