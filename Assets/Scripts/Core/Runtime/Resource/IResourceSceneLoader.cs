using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace Core.Runtime
{
    /// 资源场景句柄；业务层只用它配对卸载，不接触具体资源框架句柄。
    public interface IResourceSceneHandle
    {
        /// 标准化后的资源地址。
        string Address { get; }

        /// 已加载的 Unity 场景。
        Scene Scene { get; }
    }

    /// 场景资源加载结果。
    public readonly struct ResourceSceneLoadResult
    {
        private ResourceSceneLoadResult(bool succeeded, IResourceSceneHandle handle, string error)
        {
            Succeeded = succeeded;
            Handle = handle;
            Error = error;
        }

        /// 是否加载成功。
        public bool Succeeded { get; }

        /// 加载成功时返回的场景句柄。
        public IResourceSceneHandle Handle { get; }

        /// 加载失败时的错误信息。
        public string Error { get; }

        /// <summary>
        /// 创建成功结果。
        /// </summary>
        /// <param name="handle">已加载且需要显式卸载的场景句柄。</param>
        public static ResourceSceneLoadResult Success(IResourceSceneHandle handle)
        {
            return new ResourceSceneLoadResult(true, handle, null);
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        /// <param name="error">底层场景加载错误。</param>
        public static ResourceSceneLoadResult Failure(string error)
        {
            return new ResourceSceneLoadResult(false, null, error);
        }
    }

    /// 场景资源卸载结果。
    public readonly struct ResourceSceneUnloadResult
    {
        private ResourceSceneUnloadResult(bool succeeded, string error)
        {
            Succeeded = succeeded;
            Error = error;
        }

        /// 是否卸载成功。
        public bool Succeeded { get; }

        /// 卸载失败时的错误信息。
        public string Error { get; }

        /// 创建成功结果。
        public static ResourceSceneUnloadResult Success()
        {
            return new ResourceSceneUnloadResult(true, null);
        }

        /// <summary>
        /// 创建失败结果。
        /// </summary>
        /// <param name="error">底层场景卸载错误。</param>
        public static ResourceSceneUnloadResult Failure(string error)
        {
            return new ResourceSceneUnloadResult(false, error);
        }
    }

    /// 通过当前资源框架异步加载和卸载场景。
    public interface IResourceSceneLoader
    {
        /// <summary>
        /// 异步加载场景资源。
        /// </summary>
        /// <param name="address">资源地址，会通过默认资源服务标准化。</param>
        /// <param name="loadMode">Unity 场景加载模式；业务 Demo 默认使用 Additive。</param>
        /// <param name="onProgress">底层真实加载进度；可能重复但始终位于 0 到 1。</param>
        /// <returns>成功时返回必须配对卸载的场景句柄。</returns>
        UniTask<ResourceSceneLoadResult> LoadSceneAsync(
            string address,
            LoadSceneMode loadMode = LoadSceneMode.Additive,
            Action<float> onProgress = null);

        /// <summary>
        /// 异步卸载由当前加载器创建的场景句柄。
        /// </summary>
        /// <param name="handle">此前加载成功返回的场景句柄。</param>
        /// <param name="onProgress">底层真实卸载进度；可能重复但始终位于 0 到 1。</param>
        /// <returns>卸载结果；成功后句柄不可再次使用。</returns>
        UniTask<ResourceSceneUnloadResult> UnloadSceneAsync(
            IResourceSceneHandle handle,
            Action<float> onProgress = null);
    }
}
