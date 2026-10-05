using System;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Hotfix.BlockPorters
{
    /// 每次请求独占加载器；只在结果仍有效时转交给当前背景。
    public sealed class BlockPortersThemeLoader : IDisposable
    {
        private readonly Func<IResourceLoader> createLoader;
        private IResourceLoader appliedLoader;
        private int version;
        private bool disposed;
        public string AppliedId { get; private set; }

        public BlockPortersThemeLoader(Func<IResourceLoader> factory) => createLoader = factory;

        /// <summary>旧请求允许自然完成后释放句柄，避免 Dispose 正在等待的 YooAsset 操作。</summary>
        public async UniTask<bool> ApplyAsync(BlockPortersThemeCatalog.Theme theme, Action<Texture2D> apply)
        {
            if (disposed || theme == null) return false;
            int request = ++version;
            IResourceLoader loader = null;
            try
            {
                loader = createLoader();
                var texture = await loader.LoadAssetAsync<Texture2D>(theme.BackgroundAddress);
                if (texture == null || disposed || request != version) return false;
                apply(texture);
                var previous = appliedLoader;
                appliedLoader = loader; loader = null;
                AppliedId = theme.Id;
                previous?.Dispose();
                return true;
            }
            finally { loader?.Dispose(); }
        }

        /// 作废在途请求，保持已应用背景及其句柄。
        public void Invalidate() => version++;
        /// 退出后隔离全部旧结果；当前资源立即释放，在途资源完成后释放。
        public void Dispose()
        {
            disposed = true; version++;
            appliedLoader?.Dispose(); appliedLoader = null;
        }
    }
}
