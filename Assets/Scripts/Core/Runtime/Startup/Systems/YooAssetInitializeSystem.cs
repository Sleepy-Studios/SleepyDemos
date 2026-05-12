using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public sealed class YooAssetInitializeSystem : StartupSystemBase
    {
        public YooAssetInitializeSystem(StartupStateBase state) : base(state)
        {
        }

        public override async UniTask ExecuteAsync()
        {
            Report(0f, "初始化 YooAssets 默认资源包");
            var config = Context.Config;
            if (config != null)
            {
                await YooAssetResourceSystem.InitializeAsync(config.PackageName, config.PlayMode, config.BaseServerURL);
            }
            else
            {
                await YooAssetResourceSystem.InitializeAsync();
            }

            if (!YooAssetResourceSystem.IsInitialized)
            {
                throw new System.InvalidOperationException("YooAssets 初始化失败，未获得可用资源清单。");
            }

            Report(1f, "YooAssets 初始化完成");
        }
    }
}
