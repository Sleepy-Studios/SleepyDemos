using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public sealed class HybridMetadataSystem : StartupSystemBase
    {
        public HybridMetadataSystem(StartupStateBase state) : base(state)
        {
        }

        public override async UniTask ExecuteAsync()
        {
            Report(0f, "为 HybridCLR 补充泛型元数据");
            if (Context.Config != null)
            {
                await HybridAotAssemblyLoader.LoadMetadataAsync(Context.Config.AotAssemblies);
            }

            Report(1f, "AOT 元数据加载完成");
        }
    }
}
