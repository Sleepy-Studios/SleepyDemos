using System;
using System.Threading;
using Core.Runtime;
using Core.Runtime.Localization;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hotfix.SceneManagement
{
    /// 一次业务场景的私有词表与资源句柄；加载前注册，实际卸载后释放。
    internal sealed class GameSceneLanguageScope : IDisposable
    {
        private readonly IResourceLoader loader;
        private readonly CancellationTokenSource lifetime = new();
        private IDisposable catalogLease;
        private Scene scene;
        private bool disposed;

        internal GameSceneLanguageScope(IDisposable lease, IResourceLoader loader = null)
        {
            catalogLease = lease;
            this.loader = loader;
            Application.quitting += Dispose;
        }

        internal static async UniTask<GameSceneLanguageScope> LoadAsync(GameSceneDefinition definition,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(definition.LanguageCatalogAddress))
            {
                return null;
            }
            var context = new GameSceneLanguageScope(null, ResourceServices.CreateLoader());
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(context.lifetime.Token, cancellationToken);
            try
            {
                var catalog = await context.loader.LoadAssetAsync<LanguageCatalog>(definition.LanguageCatalogAddress)
                    .AttachExternalCancellation(linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                if (catalog == null)
                {
                    throw new InvalidOperationException("场景私有文案加载失败：" + definition.LanguageCatalogAddress);
                }
                context.catalogLease = LanguageService.RegisterCatalog(catalog);
                return context;
            }
            catch
            {
                context.Dispose();
                throw;
            }
        }

        internal void BindScene(Scene value)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(GameSceneLanguageScope));
            }
            scene = value;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnSceneUnloaded(Scene unloaded)
        {
            if (unloaded == scene)
            {
                Dispose();
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            Application.quitting -= Dispose;
            lifetime.Cancel();
            try
            {
                catalogLease?.Dispose();
            }
            finally
            {
                try
                {
                    loader?.Dispose();
                }
                finally
                {
                    lifetime.Dispose();
                }
            }
        }
    }
}
