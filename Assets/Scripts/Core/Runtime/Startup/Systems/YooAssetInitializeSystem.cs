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
                var options = new ResourceInitializeOptions(config.PackageName, config.PlayMode, config.BaseServerURL);
                await ResourceServices.Default.InitializeAsync(options);
            }
            else
            {
                await ResourceServices.Default.InitializeAsync(ResourceInitializeOptions.Default);
            }

            if (!ResourceServices.Default.IsInitialized)
            {
                throw new System.InvalidOperationException("YooAssets 初始化失败，未获得可用资源清单。");
            }

            Report(1f, "YooAssets 初始化完成");
        }
    }
}
