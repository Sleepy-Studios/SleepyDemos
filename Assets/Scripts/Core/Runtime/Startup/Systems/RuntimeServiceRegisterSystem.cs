using System;
using Cysharp.Threading.Tasks;
using UnityEngine.U2D;

namespace Core.Runtime
{
    public sealed class RuntimeServiceRegisterSystem : StartupSystemBase
    {
        public RuntimeServiceRegisterSystem(StartupStateBase state) : base(state)
        {
        }

        public override UniTask ExecuteAsync()
        {
            Report(0f, "注册图集加载和运行时服务预留点");
            SpriteAtlasManager.atlasRequested -= OnAtlasRequested;
            SpriteAtlasManager.atlasRequested += OnAtlasRequested;
            Report(1f, "运行时服务注册完成");
            return UniTask.CompletedTask;
        }

        private static void OnAtlasRequested(string atlasName, Action<SpriteAtlas> callback)
        {
            LoadAtlasAsync(atlasName, callback).Forget();
        }

        private static async UniTaskVoid LoadAtlasAsync(string atlasName, Action<SpriteAtlas> callback)
        {
            var result = await ResourceServices.Default.LoadAssetAsync<SpriteAtlas>(atlasName);
            if (result.Success)
            {
                callback?.Invoke(result.Asset);
            }
        }
    }
}
