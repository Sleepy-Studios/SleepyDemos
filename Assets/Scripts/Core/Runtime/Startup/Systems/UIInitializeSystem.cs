using Cysharp.Threading.Tasks;

namespace Core.Runtime
{
    public sealed class UIInitializeSystem : StartupSystemBase
    {
        public UIInitializeSystem(StartupStateBase state) : base(state)
        {
        }

        public override async UniTask ExecuteAsync()
        {
            Report(0f, "创建 UIRootCanvas、UI Camera 和层级 Canvas");
            await UIManager.Instance.InitializeAsync();
            Report(1f, "UI 系统初始化完成");
        }
    }
}
